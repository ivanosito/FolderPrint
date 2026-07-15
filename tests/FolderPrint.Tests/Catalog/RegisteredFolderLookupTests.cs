using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Catalog;

public sealed class RegisteredFolderLookupTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Find_TrailingSeparatorAndDifferentCase_ReturnsSingleMatch()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FolderPrint-Lookup"));
        var registered = Folder(root.ToUpperInvariant());
        var catalog = new IntegrityCatalog([registered]);

        var result = RegisteredFolderLookup.Find(catalog, root + Path.DirectorySeparatorChar);

        Assert.Equal(RegisteredFolderLookupStatus.Success, result.Status);
        Assert.Same(registered, result.RegisteredFolder);
        Assert.Equal(0, result.RegisteredFolderIndex);
        Assert.Equal(Path.TrimEndingDirectorySeparator(root), result.NormalizedRootPath);
    }

    [Fact]
    public void Find_NoMatchingRegistration_ReturnsNotFound()
    {
        var catalog = new IntegrityCatalog([Folder(Path.Combine(Path.GetTempPath(), "other"))]);

        var result = RegisteredFolderLookup.Find(catalog, Path.Combine(Path.GetTempPath(), "requested"));

        Assert.Equal(RegisteredFolderLookupStatus.NotFound, result.Status);
        Assert.Null(result.RegisteredFolder);
    }

    [Fact]
    public void Find_MultipleNormalizedMatches_ReturnsCatalogError()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "duplicate"));
        var catalog = new IntegrityCatalog([Folder(root), Folder(root + Path.DirectorySeparatorChar)]);

        var result = RegisteredFolderLookup.Find(catalog, root);

        Assert.Equal(RegisteredFolderLookupStatus.CatalogError, result.Status);
        Assert.Contains("multiple", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Find_NullRegistrationOrInvalidBaseline_ReturnsCatalogError()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "invalid"));
        var invalidBaseline = Folder(root) with { Files = [null!] };

        var nullEntry = RegisteredFolderLookup.Find(new IntegrityCatalog([null!]), root);
        var invalidFiles = RegisteredFolderLookup.Find(new IntegrityCatalog([invalidBaseline]), root);

        Assert.Equal(RegisteredFolderLookupStatus.CatalogError, nullEntry.Status);
        Assert.Equal(RegisteredFolderLookupStatus.CatalogError, invalidFiles.Status);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Find_InvalidPersistedSha256_ReturnsCatalogError(string sha256)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "invalid-hash"));
        var folder = Folder(root) with { Files = [new FileFingerprint("file.txt", sha256, 1, Timestamp)] };

        var result = RegisteredFolderLookup.Find(new IntegrityCatalog([folder]), root);

        Assert.Equal(RegisteredFolderLookupStatus.CatalogError, result.Status);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("nested/../../escape.txt")]
    [InlineData("nested//file.txt")]
    [InlineData("./file.txt")]
    [InlineData("C:\\absolute.txt")]
    [InlineData("/absolute.txt")]
    public void Find_UnsafePersistedRelativePath_ReturnsCatalogError(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "unsafe-path"));
        var folder = Folder(root) with { Files = [new FileFingerprint(relativePath, ValidSha256, 1, Timestamp)] };

        var result = RegisteredFolderLookup.Find(new IntegrityCatalog([folder]), root);

        Assert.Equal(RegisteredFolderLookupStatus.CatalogError, result.Status);
    }

    [Fact]
    public void Find_DuplicateOrdinalPersistedPaths_ReturnsCatalogError()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "duplicate-path"));
        var folder = Folder(root) with
        {
            Files =
            [
                new FileFingerprint("file.txt", ValidSha256, 1, Timestamp),
                new FileFingerprint("file.txt", ValidSha256, 1, Timestamp)
            ]
        };

        var result = RegisteredFolderLookup.Find(new IntegrityCatalog([folder]), root);

        Assert.Equal(RegisteredFolderLookupStatus.CatalogError, result.Status);
        Assert.Contains("duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Find_MalformedRequestedPath_ReturnsInvalidRoot()
    {
        var result = RegisteredFolderLookup.Find(IntegrityCatalog.Empty, "\0");

        Assert.Equal(RegisteredFolderLookupStatus.InvalidRoot, result.Status);
    }

    private const string ValidSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static RegisteredFolder Folder(string rootPath) =>
        new("folder-id", rootPath, Timestamp, null, [new FileFingerprint("file.txt", ValidSha256, 1, Timestamp)]);
}
