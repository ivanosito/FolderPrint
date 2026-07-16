using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using FolderPrint.Core.Registration;
using FolderPrint.Core.Reporting;
using FolderPrint.Core.Scanning;
using FolderPrint.Core.Verification;

namespace FolderPrint.Cli;

public sealed class CliRunner
{
    private readonly CatalogStore catalogStore;
    private readonly RegistrationService registrationService;
    private readonly UnregistrationService unregistrationService;
    private readonly RefreshService refreshService;
    private readonly Func<string, FolderSnapshot> scanFolderForVerification;
    private readonly Func<RegisteredFolder, FolderSnapshot, VerificationResult> compareFolders;
    private readonly TextWriter output;
    private readonly TextWriter error;

    public CliRunner(
        CatalogStore? catalogStore = null,
        TextWriter? output = null,
        TextWriter? error = null,
        Func<string, FolderSnapshot>? verificationScan = null,
        Func<RegisteredFolder, FolderSnapshot, VerificationResult>? verificationCompare = null,
        Func<string, FolderSnapshot>? refreshScan = null,
        Func<DateTimeOffset>? refreshClock = null)
    {
        this.catalogStore = catalogStore ?? new CatalogStore(new CatalogPathProvider().GetCatalogPath());
        var folderScanner = new FolderScanner(new FileHasher());
        registrationService = new RegistrationService(this.catalogStore, folderScanner);
        unregistrationService = new UnregistrationService(this.catalogStore);
        refreshService = new RefreshService(
            this.catalogStore,
            refreshScan ?? folderScanner.Scan,
            refreshClock);
        scanFolderForVerification = verificationScan ?? folderScanner.Scan;
        compareFolders = verificationCompare ?? new VerificationService().Compare;
        this.output = output ?? Console.Out;
        this.error = error ?? Console.Error;
    }

    public int Run(string[] args)
    {
        var result = CommandParser.Parse(args);

        if (!result.IsSuccess)
        {
            WriteUsageError(result.ErrorMessage);
            return result.ExitCode;
        }

        try
        {
            return result.Command!.Kind switch
            {
                CommandKind.List => RunList(),
                CommandKind.Register => RunRegister(result.Command.FolderPath!),
                CommandKind.Unregister => RunUnregister(result.Command.FolderPath!),
                CommandKind.Verify => RunVerify(result.Command.FolderPath!),
                CommandKind.Refresh => RunRefresh(result.Command.FolderPath!),
                _ => result.ExitCode
            };
        }
        catch
        {
            error.WriteLine("Unexpected error.");
            return ExitCodes.UnexpectedError;
        }
    }

    private int RunVerify(string requestedRootPath)
    {
        string normalizedRootPath;
        try
        {
            normalizedRootPath = RegistrationService.NormalizeRootPath(requestedRootPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error.WriteLine("Folder path is invalid.");
            return ExitCodes.NotFound;
        }

        var loadResult = catalogStore.Load();
        if (!loadResult.IsSuccess)
        {
            error.WriteLine(loadResult.ErrorMessage);
            return ExitCodes.CatalogError;
        }

        var lookup = RegisteredFolderLookup.Find(loadResult.Catalog!, normalizedRootPath);
        if (lookup.Status != RegisteredFolderLookupStatus.Success)
        {
            error.WriteLine(lookup.ErrorMessage);
            return lookup.Status switch
            {
                RegisteredFolderLookupStatus.InvalidRoot or RegisteredFolderLookupStatus.NotFound => ExitCodes.NotFound,
                RegisteredFolderLookupStatus.CatalogError => ExitCodes.CatalogError,
                _ => ExitCodes.UnexpectedError
            };
        }

        var rootValidation = ValidateVerificationRoot(normalizedRootPath);
        if (rootValidation != ExitCodes.Success)
        {
            return rootValidation;
        }

        FolderSnapshot snapshot;
        try
        {
            snapshot = scanFolderForVerification(normalizedRootPath);
        }
        catch (FileNotFoundException)
        {
            return ClassifyScanFailure(normalizedRootPath);
        }
        catch (DirectoryNotFoundException)
        {
            return ClassifyScanFailure(normalizedRootPath);
        }
        catch (IOException)
        {
            return ClassifyScanFailure(normalizedRootPath);
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine("Folder scan failed.");
            return ExitCodes.ScanError;
        }

        var verificationResult = compareFolders(lookup.RegisteredFolder!, snapshot);
        var updatedCatalog = loadResult.Catalog!.WithLastVerifiedAt(
            lookup.RegisteredFolderIndex,
            verificationResult.VerifiedAtUtc);
        var saveResult = catalogStore.SaveIfUnchanged(updatedCatalog, loadResult.Version);
        if (!saveResult.IsSuccess)
        {
            error.WriteLine(saveResult.ErrorMessage);
            return ExitCodes.CatalogError;
        }

        foreach (var line in ReportFormatter.FormatVerification(verificationResult))
        {
            output.WriteLine(line);
        }

        return verificationResult.HasDifferences ? ExitCodes.DifferencesFound : ExitCodes.Success;
    }

    private int ClassifyScanFailure(string normalizedRootPath)
    {
        try
        {
            var attributes = File.GetAttributes(normalizedRootPath);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                error.WriteLine("Folder scan failed.");
                return ExitCodes.ScanError;
            }

            return WriteRootNotFound(normalizedRootPath);
        }
        catch (FileNotFoundException)
        {
            return WriteRootNotFound(normalizedRootPath);
        }
        catch (DirectoryNotFoundException)
        {
            return WriteRootNotFound(normalizedRootPath);
        }
        catch (IOException)
        {
            error.WriteLine("Folder scan failed.");
            return ExitCodes.ScanError;
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine("Folder scan failed.");
            return ExitCodes.ScanError;
        }
    }

    private int ValidateVerificationRoot(string normalizedRootPath)
    {
        try
        {
            var attributes = File.GetAttributes(normalizedRootPath);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                return WriteRootNotFound(normalizedRootPath);
            }

            return ExitCodes.Success;
        }
        catch (FileNotFoundException)
        {
            return WriteRootNotFound(normalizedRootPath);
        }
        catch (DirectoryNotFoundException)
        {
            return WriteRootNotFound(normalizedRootPath);
        }
        catch (IOException)
        {
            error.WriteLine("Folder scan failed.");
            return ExitCodes.ScanError;
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine("Folder scan failed.");
            return ExitCodes.ScanError;
        }
    }

    private int WriteRootNotFound(string normalizedRootPath)
    {
        error.WriteLine($"Folder was not found or is not a directory: {normalizedRootPath}");
        return ExitCodes.NotFound;
    }

    private int RunRegister(string folderPath)
    {
        var result = registrationService.Register(folderPath);
        if (result.Status == RegistrationStatus.Success)
        {
            output.WriteLine($"Registered folder: {result.RootPath}");
            output.WriteLine($"Files: {result.FileCount}");
            return ExitCodes.Success;
        }

        error.WriteLine(result.ErrorMessage);
        foreach (var unreadableFile in result.UnreadableFiles)
        {
            error.WriteLine($"Unreadable: {unreadableFile}");
        }

        return result.Status switch
        {
            RegistrationStatus.AlreadyRegistered or RegistrationStatus.CatalogInsideRoot => ExitCodes.UsageError,
            RegistrationStatus.InvalidRoot => ExitCodes.NotFound,
            RegistrationStatus.CatalogError => ExitCodes.CatalogError,
            RegistrationStatus.ScanError => ExitCodes.ScanError,
            _ => ExitCodes.UnexpectedError
        };
    }

    private int RunUnregister(string folderPath)
    {
        var result = unregistrationService.Unregister(folderPath);
        if (result.Status == UnregistrationStatus.Success)
        {
            output.WriteLine($"Unregistered folder: {result.RootPath}");
            return ExitCodes.Success;
        }

        error.WriteLine(result.ErrorMessage);
        return result.Status switch
        {
            UnregistrationStatus.InvalidRoot or UnregistrationStatus.NotFound => ExitCodes.NotFound,
            UnregistrationStatus.CatalogError => ExitCodes.CatalogError,
            _ => ExitCodes.UnexpectedError
        };
    }

    private int RunRefresh(string folderPath)
    {
        var result = refreshService.Refresh(folderPath);
        if (result.Status == RefreshStatus.Success)
        {
            output.WriteLine($"Refreshed folder: {result.RootPath}");
            output.WriteLine($"Files: {result.FileCount}");
            return ExitCodes.Success;
        }

        error.WriteLine(result.ErrorMessage);
        foreach (var unreadableFile in result.UnreadableFiles)
        {
            error.WriteLine($"Unreadable: {unreadableFile}");
        }

        return result.Status switch
        {
            RefreshStatus.InvalidRoot or RefreshStatus.NotFound => ExitCodes.NotFound,
            RefreshStatus.CatalogError => ExitCodes.CatalogError,
            RefreshStatus.ScanError => ExitCodes.ScanError,
            _ => ExitCodes.UnexpectedError
        };
    }

    private int RunList()
    {
        var loadResult = catalogStore.Load();
        if (!loadResult.IsSuccess)
        {
            error.WriteLine(loadResult.ErrorMessage);
            return ExitCodes.CatalogError;
        }

        var catalog = loadResult.Catalog!;
        var validation = CatalogValidator.Validate(catalog);
        if (!validation.IsSuccess)
        {
            error.WriteLine(validation.ErrorMessage);
            return ExitCodes.CatalogError;
        }

        if (catalog.RegisteredFolders.Count == 0)
        {
            output.WriteLine("No folders are registered.");
            return ExitCodes.Success;
        }

        foreach (var line in ReportFormatter.FormatRegisteredFolders(catalog.RegisteredFolders))
        {
            output.WriteLine(line);
        }

        return ExitCodes.Success;
    }

    private void WriteUsageError(string? message)
    {
        error.WriteLine(message);
        error.WriteLine("Usage: folderprint <register|verify|unregister|duplicates|refresh> <folder>");
        error.WriteLine("       folderprint list");
    }
}
