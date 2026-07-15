using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Catalog;

public sealed class CatalogValidatorTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private const string ValidSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Validate_ValidCatalogWithMissingPhysicalRoots_ReturnsSuccess()
    {
        var catalog = new IntegrityCatalog([Folder(MissingRoot("one")), Folder(MissingRoot("two"), "folder-2")]);

        var result = CatalogValidator.Validate(catalog);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_NullRegistrationOrFiles_ReturnsCatalogError()
    {
        var nullRegistration = CatalogValidator.Validate(new IntegrityCatalog([null!]));
        var nullFiles = CatalogValidator.Validate(new IntegrityCatalog([Folder(MissingRoot("null-files")) with { Files = null! }]));

        Assert.False(nullRegistration.IsSuccess);
        Assert.Contains("null registered folder", nullRegistration.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(nullFiles.IsSuccess);
        Assert.Contains("invalid registered folder", nullFiles.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "file.txt", ValidSha256)]
    [InlineData("folder", "../escape.txt", ValidSha256)]
    [InlineData("folder", "file.txt", "short")]
    public void Validate_InvalidRequiredOrFingerprintData_ReturnsCatalogError(
        string id,
        string relativePath,
        string sha256)
    {
        var folder = Folder(MissingRoot(Guid.NewGuid().ToString("N")), id) with
        {
            Files = [new FileFingerprint(relativePath, sha256, 1, Timestamp)]
        };

        var result = CatalogValidator.Validate(new IntegrityCatalog([folder]));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Validate_DuplicateOrdinalFingerprintPaths_ReturnsCatalogError()
    {
        var fingerprint = new FileFingerprint("file.txt", ValidSha256, 1, Timestamp);
        var folder = Folder(MissingRoot("duplicate-file")) with { Files = [fingerprint, fingerprint] };

        var result = CatalogValidator.Validate(new IntegrityCatalog([folder]));

        Assert.False(result.IsSuccess);
        Assert.Contains("duplicate file fingerprint", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_InvalidStoredRoot_ReturnsCatalogError()
    {
        var result = CatalogValidator.Validate(new IntegrityCatalog([Folder("\0")]));

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid registered folder path", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DuplicateNormalizedRoots_ReturnsCatalogError()
    {
        var root = MissingRoot("duplicate-root");
        var alternate = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        var catalog = new IntegrityCatalog([
            Folder(root, "folder-1"),
            Folder(alternate + Path.DirectorySeparatorChar, "folder-2")
        ]);

        var result = CatalogValidator.Validate(catalog);

        Assert.False(result.IsSuccess);
        Assert.Contains("multiple registrations", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static RegisteredFolder Folder(string rootPath, string id = "folder-1") =>
        new(id, rootPath, Timestamp, null, [new FileFingerprint("file.txt", ValidSha256, 1, Timestamp)]);

    private static string MissingRoot(string suffix) =>
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FolderPrint-Validator-Missing", suffix));
}
