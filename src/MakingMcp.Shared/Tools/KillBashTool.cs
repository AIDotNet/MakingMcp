using System.ComponentModel;
using MakingMcp.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Shared.Tools;

public class KillBashTool
{
    [McpServerTool(Name = "KillBash"), Description(
         """
         - Kills a running background bash shell by its ID
         - Takes a shell_id parameter identifying the shell to kill
         - Returns a success or failure status 
         - Use this tool when you need to terminate a long-running shell
         - Shell IDs can be found using the /bashes command
         """)]
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