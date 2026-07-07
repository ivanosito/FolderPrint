namespace FolderPrint.Cli;

public static class CommandParser
{
    private static readonly Dictionary<string, CommandKind> FolderCommands = new(StringComparer.Ordinal)
    {
        ["register"] = CommandKind.Register,
        ["verify"] = CommandKind.Verify,
        ["unregister"] = CommandKind.Unregister,
        ["duplicates"] = CommandKind.Duplicates,
        ["refresh"] = CommandKind.Refresh
    };

    public static CommandParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return CommandParseResult.UsageError("A command is required.");
        }

        var commandName = args[0];

        if (commandName == "list")
        {
            return args.Length == 1
                ? CommandParseResult.Success(new ParsedCommand(CommandKind.List, null))
                : CommandParseResult.UsageError("The list command does not accept a folder argument.");
        }

        if (FolderCommands.TryGetValue(commandName, out var commandKind))
        {
            return args.Length == 2
                ? CommandParseResult.Success(new ParsedCommand(commandKind, args[1]))
                : CommandParseResult.UsageError($"The {commandName} command requires exactly one folder argument.");
        }

        return CommandParseResult.UsageError($"Unknown command: {commandName}");
    }
}