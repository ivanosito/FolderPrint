using FolderPrint.Core.Catalog;

namespace FolderPrint.Cli;

public sealed class CliRunner
{
    private readonly CatalogStore catalogStore;
    private readonly TextWriter output;
    private readonly TextWriter error;

    public CliRunner(CatalogStore? catalogStore = null, TextWriter? output = null, TextWriter? error = null)
    {
        this.catalogStore = catalogStore ?? new CatalogStore(new CatalogPathProvider().GetCatalogPath());
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

        if (result.Command?.Kind == CommandKind.List)
        {
            return RunList();
        }

        return result.ExitCode;
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
