using FolderPrint.Cli;

var result = CommandParser.Parse(args);

if (!result.IsSuccess)
{
    Console.Error.WriteLine(result.ErrorMessage);
    Console.Error.WriteLine("Usage: folderprint <register|verify|unregister|duplicates|refresh> <folder>");
    Console.Error.WriteLine("       folderprint list");
}

return result.ExitCode;