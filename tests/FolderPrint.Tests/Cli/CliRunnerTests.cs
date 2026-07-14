using FolderPrint.Cli;
using FolderPrint.Core.Catalog;

namespace FolderPrint.Tests.Cli;

public sealed class CliRunnerTests
{
    [Fact]
    public void Run_ListWithMissingCatalog_PrintsEmptyMessageAndReturnsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var catalogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catalog.json");
        var runner = new CliRunner(new CatalogStore(catalogPath), output, error);

        var exitCode = runner.Run(new[] { "list" });

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("No folders are registered", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_ListWithMalformedCatalog_PrintsCatalogErrorAndReturnsCatalogError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalogPath = Path.Combine(directory, "catalog.json");
        File.WriteAllText(catalogPath, "invalid json");
        var runner = new CliRunner(new CatalogStore(catalogPath), output, error);

        var exitCode = runner.Run(new[] { "list" });

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("catalog", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_RegisterNestedFolder_PersistsBaselineAndReportsSuccess()
    {
        using var fixture = new RegistrationFixture();
        var nested = Directory.CreateDirectory(Path.Combine(fixture.RootPath, "nested")).FullName;
        File.WriteAllText(Path.Combine(nested, "file.txt"), "content");

        var exitCode = fixture.Runner.Run(new[] { "register", fixture.RootPath });

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(Path.GetFullPath(fixture.RootPath), fixture.Output.ToString());
        Assert.Contains("Files: 1", fixture.Output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, fixture.Error.ToString());
        Assert.Single(new CatalogStore(fixture.CatalogPath).Load().Catalog!.RegisteredFolders);
    }

    [Fact]
    public void Run_RegisterEmptyFolder_ReportsZeroFiles()
    {
        using var fixture = new RegistrationFixture();

        var exitCode = fixture.Runner.Run(new[] { "register", fixture.RootPath });

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Files: 0", fixture.Output.ToString());
        Assert.Empty(Assert.Single(new CatalogStore(fixture.CatalogPath).Load().Catalog!.RegisteredFolders).Files);
    }
    [Fact]
    public void Run_RegisterMissingFolder_ReturnsNotFoundWithoutSuccessOutput()
    {
        using var fixture = new RegistrationFixture();

        var exitCode = fixture.Runner.Run(new[] { "register", Path.Combine(fixture.RootPath, "missing") });

        Assert.Equal(ExitCodes.NotFound, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("not", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.CatalogPath));
    }

    [Fact]
    public void Run_RegisterDuplicate_ReturnsUsageErrorAndListStillWorks()
    {
        using var fixture = new RegistrationFixture();
        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(new[] { "register", fixture.RootPath }));
        fixture.Output.GetStringBuilder().Clear();

        var duplicateExitCode = fixture.Runner.Run(new[] { "register", fixture.RootPath + Path.DirectorySeparatorChar });

        Assert.Equal(ExitCodes.UsageError, duplicateExitCode);
        Assert.Contains("already registered", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        fixture.Error.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(new[] { "list" }));
        Assert.Contains(Path.GetFullPath(fixture.RootPath), fixture.Output.ToString());
    }

    [Fact]
    public void Run_RegisterWithMalformedCatalog_ReturnsCatalogErrorWithoutMutation()
    {
        using var fixture = new RegistrationFixture();
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.CatalogPath)!);
        File.WriteAllText(fixture.CatalogPath, "malformed");
        var original = File.ReadAllBytes(fixture.CatalogPath);

        var exitCode = fixture.Runner.Run(new[] { "register", fixture.RootPath });

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("catalog", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(fixture.CatalogPath));
    }

    [Fact]
    public void Run_RegisterWhenCatalogCannotBeSaved_ReturnsCatalogError()
    {
        using var fixture = new RegistrationFixture(blockCatalogParent: true);

        var exitCode = fixture.Runner.Run(new[] { "register", fixture.RootPath });

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("written", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_RegisterWithUnreadableFile_ReturnsScanErrorWithoutCatalog()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new RegistrationFixture();
        var lockedPath = Path.Combine(fixture.RootPath, "locked.txt");
        File.WriteAllText(lockedPath, "content");
        using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var exitCode = fixture.Runner.Run(new[] { "register", fixture.RootPath });

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("locked.txt", fixture.Error.ToString());
        Assert.False(File.Exists(fixture.CatalogPath));
    }

    private sealed class RegistrationFixture : IDisposable
    {
        private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"FolderPrint-Cli-{Guid.NewGuid():N}");

        public RegistrationFixture(bool blockCatalogParent = false)
        {
            RootPath = Directory.CreateDirectory(Path.Combine(tempDirectory, "root")).FullName;
            CatalogPath = blockCatalogParent
                ? Path.Combine(CreateBlockingFile(), "catalog.json")
                : Path.Combine(tempDirectory, "state", "catalog.json");
            Output = new StringWriter();
            Error = new StringWriter();
            Runner = new CliRunner(new CatalogStore(CatalogPath), Output, Error);
        }

        public string RootPath { get; }
        public string CatalogPath { get; }
        public StringWriter Output { get; }
        public StringWriter Error { get; }
        public CliRunner Runner { get; }

        private string CreateBlockingFile()
        {
            var path = Path.Combine(tempDirectory, "blocking-file");
            File.WriteAllText(path, "content");
            return path;
        }

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
            try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
