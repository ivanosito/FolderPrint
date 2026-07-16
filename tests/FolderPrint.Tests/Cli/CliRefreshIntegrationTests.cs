using FolderPrint.Cli;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Cli;

public sealed class CliRefreshIntegrationTests : IDisposable
{
    private readonly string tempDirectory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"FolderPrint-Refresh-Integration-{Guid.NewGuid():N}"))
        .FullName;

    [Fact]
    public void Run_RegisterDriftRefreshVerify_TrustsNewBaselineWithoutMutatingTarget()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "target")).FullName;
        var changedPath = Path.Combine(root, "changed.txt");
        var removedPath = Path.Combine(root, "removed.txt");
        File.WriteAllText(changedPath, "original");
        File.WriteAllText(removedPath, "remove me");
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        var store = new CatalogStore(catalogPath);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(store, output, error);
        Assert.Equal(ExitCodes.Success, runner.Run(["register", root]));
        var registered = Assert.Single(store.Load().Catalog!.RegisteredFolders);

        File.WriteAllText(changedPath, "intentional change");
        File.Delete(removedPath);
        var addedPath = Path.Combine(root, "added.txt");
        File.WriteAllText(addedPath, "intentional addition");
        var beforeRefresh = CaptureTarget(root);
        output.GetStringBuilder().Clear();

        var refreshExit = runner.Run(["refresh", root]);

        Assert.Equal(ExitCodes.Success, refreshExit);
        Assert.Equal(
            $"Refreshed folder: {root}{Environment.NewLine}Files: 2{Environment.NewLine}",
            output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        AssertTargetUnchanged(beforeRefresh, CaptureTarget(root));
        var refreshed = Assert.Single(store.Load().Catalog!.RegisteredFolders);
        Assert.Equal(registered.Id, refreshed.Id);
        Assert.Equal(registered.RootPath, refreshed.RootPath);
        Assert.Equal(registered.CreatedAtUtc, refreshed.CreatedAtUtc);
        Assert.NotNull(refreshed.LastVerifiedAtUtc);
        Assert.Equal(["added.txt", "changed.txt"], refreshed.Files.Select(file => file.RelativePath));

        output.GetStringBuilder().Clear();
        var cleanExit = runner.Run(["verify", root]);

        Assert.Equal(ExitCodes.Success, cleanExit);
        Assert.Contains("Result: Clean", output.ToString());
        Assert.Equal(string.Empty, error.ToString());

        output.GetStringBuilder().Clear();
        File.AppendAllText(addedPath, " later drift");
        var driftExit = runner.Run(["verify", root]);

        Assert.Equal(ExitCodes.DifferencesFound, driftExit);
        Assert.Contains("[Modified]", output.ToString());
        Assert.Contains("added.txt", output.ToString());
    }

    [Fact]
    public void Run_RefreshOneRegistration_PreservesSurvivorForListVerifyRegisterAndUnregister()
    {
        var targetRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "target-two")).FullName;
        var survivorRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "survivor")).FullName;
        File.WriteAllText(Path.Combine(targetRoot, "target.txt"), "target");
        File.WriteAllText(Path.Combine(survivorRoot, "survivor.txt"), "survivor");
        var store = new CatalogStore(Path.Combine(tempDirectory, "state-two", "catalog.json"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(store, output, error);
        Assert.Equal(ExitCodes.Success, runner.Run(["register", targetRoot]));
        Assert.Equal(ExitCodes.Success, runner.Run(["register", survivorRoot]));
        var before = store.Load().Catalog!.RegisteredFolders;
        var survivorBefore = before.Single(folder => folder.RootPath == survivorRoot);
        File.WriteAllText(Path.Combine(targetRoot, "new.txt"), "new baseline");
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Success, runner.Run(["refresh", targetRoot]));

        var after = store.Load().Catalog!.RegisteredFolders;
        Assert.Equal(before.Select(folder => folder.Id), after.Select(folder => folder.Id));
        AssertFolder(survivorBefore, after.Single(folder => folder.Id == survivorBefore.Id));

        output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, runner.Run(["list"]));
        Assert.Contains($"Path: {targetRoot}", output.ToString());
        Assert.Contains($"Path: {survivorRoot}", output.ToString());

        output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, runner.Run(["verify", survivorRoot]));
        Assert.Contains("Result: Clean", output.ToString());

        var newRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "new-registration")).FullName;
        output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, runner.Run(["register", newRoot]));
        output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, runner.Run(["unregister", newRoot]));
        Assert.Equal(string.Empty, error.ToString());
    }

    private static TargetState CaptureTarget(string root)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                path => new FileState(
                    File.ReadAllBytes(path),
                    File.GetLastWriteTimeUtc(path),
                    File.GetAttributes(path)),
                StringComparer.Ordinal);
        return new TargetState(
            Directory.GetLastWriteTimeUtc(root),
            File.GetAttributes(root),
            files);
    }

    private static void AssertTargetUnchanged(TargetState expected, TargetState actual)
    {
        Assert.Equal(expected.LastWriteUtc, actual.LastWriteUtc);
        Assert.Equal(expected.Attributes, actual.Attributes);
        Assert.Equal(expected.Files.Keys.OrderBy(path => path), actual.Files.Keys.OrderBy(path => path));
        foreach (var relativePath in expected.Files.Keys)
        {
            var expectedFile = expected.Files[relativePath];
            var actualFile = actual.Files[relativePath];
            Assert.Equal(expectedFile.Bytes, actualFile.Bytes);
            Assert.Equal(expectedFile.LastWriteUtc, actualFile.LastWriteUtc);
            Assert.Equal(expectedFile.Attributes, actualFile.Attributes);
        }
    }

    private static void AssertFolder(RegisteredFolder expected, RegisteredFolder actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.RootPath, actual.RootPath);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.LastVerifiedAtUtc, actual.LastVerifiedAtUtc);
        Assert.Equal(expected.Files, actual.Files);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record TargetState(
        DateTime LastWriteUtc,
        FileAttributes Attributes,
        IReadOnlyDictionary<string, FileState> Files);

    private sealed record FileState(
        byte[] Bytes,
        DateTime LastWriteUtc,
        FileAttributes Attributes);
}
