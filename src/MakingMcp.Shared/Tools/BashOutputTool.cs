using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MakingMcp.Shared.Infrastructure;
using MakingMcp.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Shared.Tools;

public class BashOutputTool
{
    [McpServerTool(Name = "BashOutput"), Description(
         """

         - Retrieves output from a running or completed background bash shell
         - Takes a shell_id parameter identifying the shell
         - Always returns only new output since the last check
         - Returns stdout and stderr output along with shell status
         - Supports optional regex filtering to show only lines matching a pattern
         - Use this tool when you need to monitor or check the output of a long-running shell
         - Shell IDs can be found using the /bashes command
         """)]
    public static async Task<string> BashOutput(
        McpServer mcpServer,
        [Description("The ID of the background shell to retrieve output from")]
        [Required]
        string bashId,
        [Description(
            """
            Optional regular expression to filter the output lines. Only lines matching this regex will be included in the result. Any lines that do not match will no longer be available to read.
            """)]
        string? filter)
    {
        // Log BashOutput tool invocation so the dashboard can show function and arguments.
        ToolInvocationLogger.Log("BashOutput.BashOutput", new
        {
            bashId,
            filter
        }, mcpServer.SessionId);

        if (string.IsNullOrWhiteSpace(bashId))
        {
            return await Task.FromResult(Error("bash_id must be provided."));
        }

        if (!BashTool.Sessions.TryGetValue(bashId, out var session))
        {
            return await Task.FromResult(Error($"No active bash session found for id: {bashId}"));
        }

        Regex? regex = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                regex = new Regex(filter, RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                return await Task.FromResult(Error($"Invalid filter regex: {ex.Message}"));
            }
        }

        var (stdout, stderr, completed) = session.Consume(regex);

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return stderr;
        }

        return stdout;
    }

    private static string Error(string message)
    {
        return $"ERROR: {message}";
    }
}