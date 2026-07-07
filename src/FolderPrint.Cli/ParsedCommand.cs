namespace FolderPrint.Cli;

public sealed record ParsedCommand(CommandKind Kind, string? FolderPath);