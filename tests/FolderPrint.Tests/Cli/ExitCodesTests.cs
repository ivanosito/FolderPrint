using FolderPrint.Cli;

namespace FolderPrint.Tests.Cli;

public class ExitCodesTests
{
    [Fact]
    public void ExitCodes_match_architecture_values()
    {
        Assert.Equal(0, ExitCodes.Success);
        Assert.Equal(1, ExitCodes.DifferencesFound);
        Assert.Equal(2, ExitCodes.UsageError);
        Assert.Equal(3, ExitCodes.NotFound);
        Assert.Equal(4, ExitCodes.CatalogError);
        Assert.Equal(5, ExitCodes.ScanError);
        Assert.Equal(10, ExitCodes.UnexpectedError);
    }
}