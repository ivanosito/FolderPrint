using FolderPrint.Core.Models;

namespace FolderPrint.Tests;

public class ProjectReferenceSmokeTests
{
    [Fact]
    public void CoreProjectReference_is_available_to_tests()
    {
        Assert.Equal("FolderPrint.Core", typeof(FileFingerprint).Assembly.GetName().Name);
    }
}
