using FolderPrint.Cli;

namespace FolderPrint.Tests.Cli;

public class CommandParserTests
{
    public static IEnumerable<object[]> CommandsWithFolder()
    {
        yield return new object[] { "register", CommandKind.Register };
        yield return new object[] { "verify", CommandKind.Verify };
        yield return new object[] { "unregister", CommandKind.Unregister };
        yield return new object[] { "duplicates", CommandKind.Duplicates };
        yield return new object[] { "refresh", CommandKind.Refresh };
    }

    [Theory]
    [MemberData(nameof(CommandsWithFolder))]
    public void Parse_valid_folder_command_returns_command_with_folder(string commandName, CommandKind expectedKind)
    {
        var result = CommandParser.Parse(new[] { commandName, "C:\\Data Folder" });

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Command);
        Assert.Equal(expectedKind, result.Command.Kind);
        Assert.Equal("C:\\Data Folder", result.Command.FolderPath);
    }

    [Fact]
    public void Parse_list_without_folder_returns_list_command()
    {
        var result = CommandParser.Parse(new[] { "list" });

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Command);
        Assert.Equal(CommandKind.List, result.Command.Kind);
        Assert.Null(result.Command.FolderPath);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown-command")]
    [InlineData("register")]
    [InlineData("verify")]
    [InlineData("unregister")]
    [InlineData("duplicates")]
    [InlineData("refresh")]
    [InlineData("list", "extra")]
    [InlineData("register", "folder", "extra")]
    public void Parse_invalid_shape_returns_usage_error(params string[] args)
    {
        var result = CommandParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Command);
        Assert.Equal(ExitCodes.UsageError, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void GetExitCode_returns_success_for_valid_command()
    {
        var result = CommandParser.Parse(new[] { "list" });

        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public void GetExitCode_returns_usage_error_for_invalid_command()
    {
        var result = CommandParser.Parse(new[] { "register" });

        Assert.Equal(ExitCodes.UsageError, result.ExitCode);
    }
}