namespace FolderPrint.Cli;

public sealed class CommandParseResult
{
    private CommandParseResult(ParsedCommand? command, string? errorMessage)
    {
        Command = command;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => Command is not null;

    public ParsedCommand? Command { get; }

    public string? ErrorMessage { get; }

    public int ExitCode => IsSuccess ? ExitCodes.Success : ExitCodes.UsageError;

    public static CommandParseResult Success(ParsedCommand command) => new(command, null);

    public static CommandParseResult UsageError(string message) => new(null, message);
}