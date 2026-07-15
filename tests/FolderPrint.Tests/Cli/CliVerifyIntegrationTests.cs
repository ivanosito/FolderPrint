using FolderPrint.Cli;
using FolderPrint.Core.Catalog;

namespace FolderPrint.Tests.Cli;

public sealed class CliVerifyIntegrationTests
{
    [Fact]
    public void Run_RegisterThenVerifyAndModify_UsesRealScannerAndMapsCleanThenDrift()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"FolderPrint-Verify-Integration-{Guid.NewGuid():N}");
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "root")).FullName;
        var filePath = Path.Combine(root, "file.txt");
        File.WriteAllText(filePath, "baseline");
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(new CatalogStore(catalogPath), output, error);

        try
        {
            Assert.Equal(ExitCodes.Success, runner.Run(["register", root]));
            output.GetStringBuilder().Clear();

            var cleanExit = runner.Run(["verify", root]);

            Assert.Equal(ExitCodes.Success, cleanExit);
            Assert.Contains("Result: Clean", output.ToString());
            Assert.NotNull(Assert.Single(new CatalogStore(catalogPath).Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);

            output.GetStringBuilder().Clear();
            File.WriteAllText(filePath, "changed");

            var driftExit = runner.Run(["verify", root]);

            Assert.Equal(ExitCodes.DifferencesFound, driftExit);
            Assert.Contains("[Modified]", output.ToString());
            Assert.Contains("file.txt", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void Run_VerifyCatalogWithNullRegistration_ReturnsCatalogErrorBeforeScan()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"FolderPrint-Verify-Catalog-{Guid.NewGuid():N}");
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "root")).FullName;
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        var store = new CatalogStore(catalogPath);
        Assert.True(store.Save(new IntegrityCatalog([null!])).IsSuccess);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var scanCalls = 0;
        var runner = new CliRunner(
            store,
            output,
            error,
            _ =>
            {
                scanCalls++;
                throw new InvalidOperationException("Scan must not run.");
            });

        try
        {
            var exitCode = runner.Run(["verify", root]);

            Assert.Equal(ExitCodes.CatalogError, exitCode);
            Assert.Equal(0, scanCalls);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("catalog", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
