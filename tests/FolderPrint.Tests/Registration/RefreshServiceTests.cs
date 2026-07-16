using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using FolderPrint.Core.Registration;

namespace FolderPrint.Tests.Registration;

public sealed class RefreshServiceTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly string tempDirectory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"FolderPrint-Refresh-Core-{Guid.NewGuid():N}"))
        .FullName;

    [Fact]
    public void Refresh_ValidAlias_ReplacesOnlyMatchAndReportsStoredRootAfterGuardedSave()
    {
        var targetRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "target")).FullName;
        var store = Store("success");
        var first = Folder("first", Path.Combine(tempDirectory, "first"), Created.AddMinutes(1));
        var target = Folder("target", targetRoot, Created.AddMinutes(2));
        var last = Folder("last", Path.Combine(tempDirectory, "last"), null);
        Assert.True(store.Save(new IntegrityCatalog([first, target, last])).IsSuccess);
        var scannedAt = Created.AddHours(1);
        var newFiles = new[]
        {
            Fingerprint("new.txt", 'b', 7, scannedAt),
            Fingerprint("same-hash.txt", 'b', 9, scannedAt)
        };
        var scanCalls = 0;
        var clockCalls = 0;
        var refreshTime = new DateTimeOffset(2026, 7, 15, 8, 30, 0, TimeSpan.FromHours(-5));
        var service = new RefreshService(
            store,
            root =>
            {
                scanCalls++;
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals(
                    RegistrationService.NormalizeRootPath(targetRoot),
                    root));
                return new FolderSnapshot("ignored", scannedAt, newFiles, []);
            },
            () =>
            {
                clockCalls++;
                return refreshTime;
            });

        var result = service.Refresh(targetRoot.ToUpperInvariant() + Path.DirectorySeparatorChar);

        Assert.Equal(RefreshStatus.Success, result.Status);
        Assert.Equal(target.RootPath, result.RootPath);
        Assert.Equal(2, result.FileCount);
        Assert.Empty(result.UnreadableFiles);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, scanCalls);
        Assert.Equal(1, clockCalls);
        var persisted = store.Load().Catalog!.RegisteredFolders;
        Assert.Equal(["first", "target", "last"], persisted.Select(folder => folder.Id));
        AssertFolder(first, persisted[0]);
        AssertFolder(last, persisted[2]);
        Assert.Equal(target.Id, persisted[1].Id);
        Assert.Equal(target.RootPath, persisted[1].RootPath);
        Assert.Equal(target.CreatedAtUtc, persisted[1].CreatedAtUtc);
        Assert.Equal(refreshTime.ToUniversalTime(), persisted[1].LastVerifiedAtUtc);
        Assert.Equal(newFiles, persisted[1].Files);
    }

    [Fact]
    public void Refresh_EmptySnapshot_IsSuccessfulAndClearsOnlyBaseline()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "empty")).FullName;
        var store = Store("empty-state");
        var target = Folder("target", root, null);
        Assert.True(store.Save(new IntegrityCatalog([target])).IsSuccess);
        var refreshedAt = Created.AddHours(2);
        var service = new RefreshService(
            store,
            path => new FolderSnapshot(path, refreshedAt, [], []),
            () => refreshedAt);

        var result = service.Refresh(root);

        Assert.Equal(RefreshStatus.Success, result.Status);
        Assert.Equal(0, result.FileCount);
        Assert.Empty(Assert.Single(store.Load().Catalog!.RegisteredFolders).Files);
    }

    [Fact]
    public void Refresh_InvalidMissingEmptyOrUnregistered_ReturnsWithoutTargetAccessOrMutation()
    {
        var missingStore = Store("missing");
        var emptyStore = Store("empty-catalog");
        Assert.True(emptyStore.Save(IntegrityCatalog.Empty).IsSuccess);
        var existingStore = Store("existing");
        Assert.True(existingStore.Save(new IntegrityCatalog([
            Folder("other", Path.Combine(tempDirectory, "other"), null)
        ])).IsSuccess);
        var existingBytes = File.ReadAllBytes(existingStore.CatalogPath);
        var calls = 0;
        FolderSnapshot Scan(string _) { calls++; throw new InvalidOperationException("must not scan"); }
        DateTimeOffset Clock() { calls++; throw new InvalidOperationException("must not read clock"); }

        var invalid = new RefreshService(missingStore, Scan, Clock).Refresh(new string((char)0, 1));
        var missing = new RefreshService(missingStore, Scan, Clock).Refresh(Path.Combine(tempDirectory, "requested"));
        var empty = new RefreshService(emptyStore, Scan, Clock).Refresh(Path.Combine(tempDirectory, "requested"));
        var unregistered = new RefreshService(existingStore, Scan, Clock).Refresh(Path.Combine(tempDirectory, "requested"));

        Assert.Equal(RefreshStatus.InvalidRoot, invalid.Status);
        Assert.Equal(RefreshStatus.NotFound, missing.Status);
        Assert.Equal(RefreshStatus.NotFound, empty.Status);
        Assert.Equal(RefreshStatus.NotFound, unregistered.Status);
        Assert.Equal(0, calls);
        Assert.False(File.Exists(missingStore.CatalogPath));
        Assert.False(File.Exists(missingStore.CatalogPath + ".lock"));
        Assert.Equal(existingBytes, File.ReadAllBytes(existingStore.CatalogPath));
    }

    [Fact]
    public void Refresh_MalformedAmbiguousOrInvalidCatalog_ReturnsCatalogErrorBeforeScan()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "catalog-root")).FullName;
        var malformed = Store("malformed");
        Directory.CreateDirectory(Path.GetDirectoryName(malformed.CatalogPath)!);
        File.WriteAllText(malformed.CatalogPath, "malformed");
        var malformedBytes = File.ReadAllBytes(malformed.CatalogPath);
        var ambiguous = Store("ambiguous");
        Assert.True(ambiguous.Save(new IntegrityCatalog([
            Folder("first", root, null),
            Folder("second", root + Path.DirectorySeparatorChar, null)
        ])).IsSuccess);
        var ambiguousBytes = File.ReadAllBytes(ambiguous.CatalogPath);
        var invalid = Store("invalid");
        Assert.True(invalid.Save(new IntegrityCatalog([
            Folder("target", root, null),
            Folder("invalid", Path.Combine(tempDirectory, "invalid"), null) with { Id = string.Empty }
        ])).IsSuccess);
        var invalidBytes = File.ReadAllBytes(invalid.CatalogPath);
        var scanCalls = 0;
        FolderSnapshot Scan(string _) { scanCalls++; throw new InvalidOperationException("must not scan"); }

        var malformedResult = new RefreshService(malformed, Scan).Refresh(root);
        var ambiguousResult = new RefreshService(ambiguous, Scan).Refresh(root);
        var invalidResult = new RefreshService(invalid, Scan).Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, malformedResult.Status);
        Assert.Equal(RefreshStatus.CatalogError, ambiguousResult.Status);
        Assert.Equal(RefreshStatus.CatalogError, invalidResult.Status);
        Assert.Equal(0, scanCalls);
        Assert.Equal(malformedBytes, File.ReadAllBytes(malformed.CatalogPath));
        Assert.Equal(ambiguousBytes, File.ReadAllBytes(ambiguous.CatalogPath));
        Assert.Equal(invalidBytes, File.ReadAllBytes(invalid.CatalogPath));
    }

    [Fact]
    public void Refresh_MissingOrFileValuedRegisteredRoot_ReturnsNotFoundWithoutScan()
    {
        var missingRoot = Path.Combine(tempDirectory, "missing-root");
        var fileRoot = Path.Combine(tempDirectory, "file-root");
        File.WriteAllText(fileRoot, "unchanged");
        var store = Store("root-validation");
        Assert.True(store.Save(new IntegrityCatalog([
            Folder("missing", missingRoot, null),
            Folder("file", fileRoot, null)
        ])).IsSuccess);
        var original = File.ReadAllBytes(store.CatalogPath);
        var calls = 0;
        var service = new RefreshService(
            store,
            _ => { calls++; throw new InvalidOperationException("must not scan"); },
            () => { calls++; throw new InvalidOperationException("must not read clock"); });

        var missing = service.Refresh(missingRoot);
        var file = service.Refresh(fileRoot);

        Assert.Equal(RefreshStatus.NotFound, missing.Status);
        Assert.Equal(RefreshStatus.NotFound, file.Status);
        Assert.Equal(0, calls);
        Assert.Equal(original, File.ReadAllBytes(store.CatalogPath));
        Assert.Equal("unchanged", File.ReadAllText(fileRoot));
    }

    [Fact]
    public void Refresh_ScanFailureOrUnreadables_ReturnsScanErrorAndPreservesCatalog()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "scan-root")).FullName;
        var store = Store("scan-failure");
        Assert.True(store.Save(new IntegrityCatalog([Folder("target", root, null)])).IsSuccess);
        var original = File.ReadAllBytes(store.CatalogPath);
        var clockCalls = 0;

        var exceptionResult = new RefreshService(
            store,
            _ => throw new IOException("scan broke"),
            () => { clockCalls++; return Created; }).Refresh(root);
        var unreadableResult = new RefreshService(
            store,
            path => new FolderSnapshot(path, Created, [Fingerprint("good.txt", 'a', 1, Created)], ["z.txt", "a.txt"]),
            () => { clockCalls++; return Created; }).Refresh(root);

        Assert.Equal(RefreshStatus.ScanError, exceptionResult.Status);
        Assert.Equal(RefreshStatus.ScanError, unreadableResult.Status);
        Assert.Equal(["a.txt", "z.txt"], unreadableResult.UnreadableFiles);
        Assert.Equal("Folder refresh failed because one or more files could not be read.", unreadableResult.ErrorMessage);
        Assert.Equal(0, clockCalls);
        Assert.Equal(original, File.ReadAllBytes(store.CatalogPath));
    }

    [Fact]
    public void Refresh_CatalogInsideRoot_ReturnsCatalogErrorBeforeScan()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "unsafe-root")).FullName;
        var store = new CatalogStore(Path.Combine(root, ".folderprint", "catalog.json"));
        Assert.True(store.Save(new IntegrityCatalog([Folder("target", root, null)])).IsSuccess);
        var original = File.ReadAllBytes(store.CatalogPath);
        var scanCalls = 0;

        var result = new RefreshService(
            store,
            _ => { scanCalls++; throw new InvalidOperationException("must not scan"); }).Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, result.Status);
        Assert.Equal(0, scanCalls);
        Assert.Equal(original, File.ReadAllBytes(store.CatalogPath));
    }

    [Fact]
    public void Refresh_LinkedRootResolvingOverCatalog_ReturnsCatalogErrorBeforeScan()
    {
        var physicalRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "physical-root")).FullName;
        var linkedRoot = Path.Combine(tempDirectory, "linked-root");
        try
        {
            Directory.CreateSymbolicLink(linkedRoot, physicalRoot);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException)
        {
            return;
        }

        var store = new CatalogStore(Path.Combine(physicalRoot, ".folderprint", "catalog.json"));
        Assert.True(store.Save(new IntegrityCatalog([Folder("target", linkedRoot, null)])).IsSuccess);
        var original = File.ReadAllBytes(store.CatalogPath);
        var scanCalls = 0;
        var service = new RefreshService(
            store,
            _ =>
            {
                scanCalls++;
                throw new InvalidOperationException("must not scan");
            });

        var result = service.Refresh(linkedRoot);

        Assert.Equal(RefreshStatus.CatalogError, result.Status);
        Assert.Equal(0, scanCalls);
        Assert.Equal(original, File.ReadAllBytes(store.CatalogPath));
    }

    [Fact]
    public void Refresh_CatalogChangesBeforeSave_FailsWithoutOverwritingConcurrentState()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "conflict-root")).FullName;
        var store = Store("conflict");
        var target = Folder("target", root, null);
        var concurrent = Folder("concurrent", Path.Combine(tempDirectory, "concurrent"), null);
        Assert.True(store.Save(new IntegrityCatalog([target])).IsSuccess);
        var service = new RefreshService(
            store,
            path => new FolderSnapshot(path, Created, [Fingerprint("fresh.txt", 'f', 5, Created)], []),
            () => Created.AddHours(1),
            () => Assert.True(store.Save(new IntegrityCatalog([target, concurrent])).IsSuccess));

        var result = service.Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, result.Status);
        Assert.Contains("changed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var persisted = store.Load().Catalog!.RegisteredFolders;
        Assert.Equal(["target", "concurrent"], persisted.Select(folder => folder.Id));
        Assert.Equal(target.Files, persisted[0].Files);
    }

    [Fact]
    public void Refresh_ConcurrentRegistrationOrVerification_PreservesCommittedWriterState()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "writer-root")).FullName;
        var newRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "new-root")).FullName;
        var registrationStore = Store("registration-writer");
        var registrationTarget = Folder("target", root, null);
        Assert.True(registrationStore.Save(new IntegrityCatalog([registrationTarget])).IsSuccess);
        var registration = new RegistrationService(
            registrationStore,
            path => new FolderSnapshot(path, Created, [], []),
            () => "concurrent-registration",
            () => Created);
        var registrationRefresh = new RefreshService(
            registrationStore,
            path => new FolderSnapshot(path, Created, [Fingerprint("fresh.txt", 'b', 2, Created)], []),
            () => Created.AddHours(1),
            () => Assert.Equal(RegistrationStatus.Success, registration.Register(newRoot).Status));

        var registrationResult = registrationRefresh.Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, registrationResult.Status);
        Assert.Equal(
            ["target", "concurrent-registration"],
            registrationStore.Load().Catalog!.RegisteredFolders.Select(folder => folder.Id));

        var verificationStore = Store("verification-writer");
        var verificationTarget = Folder("target", root, null);
        Assert.True(verificationStore.Save(new IntegrityCatalog([verificationTarget])).IsSuccess);
        var verifiedAt = Created.AddHours(2);
        var verificationRefresh = new RefreshService(
            verificationStore,
            path => new FolderSnapshot(path, Created, [Fingerprint("fresh.txt", 'c', 3, Created)], []),
            () => Created.AddHours(3),
            () =>
            {
                var loaded = verificationStore.Load();
                Assert.True(verificationStore.SaveIfUnchanged(
                    loaded.Catalog!.WithLastVerifiedAt(0, verifiedAt),
                    loaded.Version).IsSuccess);
            });

        var verificationResult = verificationRefresh.Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, verificationResult.Status);
        var verified = Assert.Single(verificationStore.Load().Catalog!.RegisteredFolders);
        Assert.Equal(verifiedAt, verified.LastVerifiedAtUtc);
        Assert.Equal(verificationTarget.Files, verified.Files);
    }

    [Fact]
    public void Refresh_ConcurrentUnregisterOrRefresh_NeverResurrectsOrOverwrites()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "lifecycle-root")).FullName;
        var unregisterStore = Store("unregister-writer");
        Assert.True(unregisterStore.Save(new IntegrityCatalog([Folder("target", root, null)])).IsSuccess);
        var unregisterRefresh = new RefreshService(
            unregisterStore,
            path => new FolderSnapshot(path, Created, [Fingerprint("fresh.txt", 'd', 4, Created)], []),
            () => Created.AddHours(1),
            () => Assert.Equal(
                UnregistrationStatus.Success,
                new UnregistrationService(unregisterStore).Unregister(root).Status));

        var unregisterResult = unregisterRefresh.Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, unregisterResult.Status);
        Assert.Empty(unregisterStore.Load().Catalog!.RegisteredFolders);

        var refreshStore = Store("refresh-writer");
        Assert.True(refreshStore.Save(new IntegrityCatalog([Folder("target", root, null)])).IsSuccess);
        var concurrentTime = Created.AddHours(2);
        var concurrent = new RefreshService(
            refreshStore,
            path => new FolderSnapshot(path, Created, [Fingerprint("concurrent.txt", 'e', 5, Created)], []),
            () => concurrentTime);
        var first = new RefreshService(
            refreshStore,
            path => new FolderSnapshot(path, Created, [Fingerprint("stale.txt", 'f', 6, Created)], []),
            () => Created.AddHours(3),
            () => Assert.Equal(RefreshStatus.Success, concurrent.Refresh(root).Status));

        var firstResult = first.Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, firstResult.Status);
        var persisted = Assert.Single(refreshStore.Load().Catalog!.RegisteredFolders);
        Assert.Equal(concurrentTime, persisted.LastVerifiedAtUtc);
        Assert.Equal("concurrent.txt", Assert.Single(persisted.Files).RelativePath);
    }

    [Fact]
    public void Refresh_VisibleExternalEditBeforeReplacement_FailsOnceAndCleansTemporaryFile()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "external-root")).FullName;
        var catalogPath = Path.Combine(tempDirectory, "external-edit", "catalog.json");
        var initialStore = new CatalogStore(catalogPath);
        Assert.True(initialStore.Save(new IntegrityCatalog([Folder("target", root, null)])).IsSuccess);
        var callbackCalls = 0;
        byte[]? externalBytes = null;
        var guardedStore = new CatalogStore(
            catalogPath,
            () =>
            {
                callbackCalls++;
                File.AppendAllText(catalogPath, " ");
                externalBytes = File.ReadAllBytes(catalogPath);
            });
        var service = new RefreshService(
            guardedStore,
            path => new FolderSnapshot(path, Created, [Fingerprint("fresh.txt", 'b', 2, Created)], []),
            () => Created.AddHours(1));

        var result = service.Refresh(root);

        Assert.Equal(RefreshStatus.CatalogError, result.Status);
        Assert.Equal(1, callbackCalls);
        Assert.Equal(externalBytes, File.ReadAllBytes(catalogPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(catalogPath)!, "*.tmp"));
    }

    private CatalogStore Store(string name) =>
        new(Path.Combine(tempDirectory, name, "catalog.json"));

    private static RegisteredFolder Folder(string id, string rootPath, DateTimeOffset? verified) =>
        new(id, Path.GetFullPath(rootPath), Created, verified, [Fingerprint("old.txt", 'a', 42, Created)]);

    private static FileFingerprint Fingerprint(
        string relativePath,
        char hashCharacter,
        long size,
        DateTimeOffset modified) =>
        new(relativePath, new string(hashCharacter, 64), size, modified);

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
}
