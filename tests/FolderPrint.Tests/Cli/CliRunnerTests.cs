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
}
