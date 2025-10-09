using System.ComponentModel;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class KillBashTool
{
    [McpServerTool(Name = "KillBash"), KernelFunction("KillBash"), Description(
         "\n- Kills a running background bash shell by its ID\n- Takes a shell_id parameter identifying the shell to kill\n- Returns a success or failure status \n- Use this tool when you need to terminate a long-running shell\n- Shell IDs can be found using the /bashes command\n")]
    public static async Task<string> KillBash(
        [Description("The ID of the background shell to kill")]
        string shell_id)
    {
        if (string.IsNullOrWhiteSpace(shell_id))
        {
            return Error("shell_id must be provided.");
        }

        if (!BashTool.Sessions.TryRemove(shell_id, out var session))
        {
            return Error($"No active bash session found for id: {shell_id}");
        }

        try
        {
            if (!session.Process.HasExited)
            {
                BashTool.TryTerminate(session.Process);
                await session.Process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            session.Dispose();
            return Error($"Failed to terminate bash session: {ex.Message}");
        }

        session.Dispose();
        return "Successfully terminated bash session.";
    }

    private static string Error(string message)
    {
        return $"ERROR: {message}";
    }
}