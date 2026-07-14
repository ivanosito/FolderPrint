using FolderPrint.Core.Catalog;
using FolderPrint.Core.Registration;
using FolderPrint.Core.Scanning;

namespace FolderPrint.Cli;

public sealed class CliRunner
{
    private readonly CatalogStore catalogStore;
    private readonly RegistrationService registrationService;
    private readonly TextWriter output;
    private readonly TextWriter error;

    public CliRunner(CatalogStore? catalogStore = null, TextWriter? output = null, TextWriter? error = null)
    {
        this.catalogStore = catalogStore ?? new CatalogStore(new CatalogPathProvider().GetCatalogPath());
        registrationService = new RegistrationService(this.catalogStore, new FolderScanner(new FileHasher()));
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

        return result.Command!.Kind switch
        {
            CommandKind.List => RunList(),
            CommandKind.Register => RunRegister(result.Command.FolderPath!),
            _ => result.ExitCode
        };
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

    private int RunList()
    {
        var loadResult = catalogStore.Load();
        if (!loadResult.IsSuccess)
        {
            error.WriteLine(loadResult.ErrorMessage);
            return ExitCodes.CatalogError;
        }

        if (loadResult.Catalog!.RegisteredFolders.Count == 0)
        {
            output.WriteLine("No folders are registered.");
            return ExitCodes.Success;
        }

        output.WriteLine("Registered folders:");
        foreach (var folder in loadResult.Catalog.RegisteredFolders)
        {
            output.WriteLine(folder.RootPath);
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
