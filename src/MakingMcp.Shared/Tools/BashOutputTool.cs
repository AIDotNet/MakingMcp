using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class BashOutputTool
{
    [McpServerTool(Name = "BashOutput"), KernelFunction("BashOutput"), Description(
         "\n- Retrieves output from a running or completed background bash shell\n- Takes a shell_id parameter identifying the shell\n- Always returns only new output since the last check\n- Returns stdout and stderr output along with shell status\n- Supports optional regex filtering to show only lines matching a pattern\n- Use this tool when you need to monitor or check the output of a long-running shell\n- Shell IDs can be found using the /bashes command\n")]
    public static async  Task<string> BashOutput(
        [Description("The ID of the background shell to retrieve output from")]
        string bash_id,
        [Description(
            "Optional regular expression to filter the output lines. Only lines matching this regex will be included in the result. Any lines that do not match will no longer be available to read.")]
        string? filter)
    {
        if (string.IsNullOrWhiteSpace(bash_id))
        {
            return await Task.FromResult(Error("bash_id must be provided."));
        }

        if (!BashTool.Sessions.TryGetValue(bash_id, out var session))
        {
            return await Task.FromResult(Error($"No active bash session found for id: {bash_id}"));
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