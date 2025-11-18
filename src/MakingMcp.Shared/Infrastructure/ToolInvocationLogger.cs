using System.Text.Json;

namespace MakingMcp.Shared.Infrastructure;

/// <summary>
/// Central place to record tool invocations into the console dashboard.
/// Tools call Log(...) with their function name and arguments, and the
/// web host wires up the dashboard.
/// </summary>
public static class ToolInvocationLogger
{
    /// <summary>
    /// Console dashboard instance used to render logs.
    /// This is set by the web host at startup.
    /// </summary>
    public static ConsoleDashboard? Dashboard { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static void Log(string functionName, object args, string clientId)
    {
        var dashboard = Dashboard;
        if (dashboard is null)
        {
            return;
        }

        string argsJson;
        try
        {
            argsJson = JsonSerializer.Serialize(args, JsonOptions);
        }
        catch
        {
            argsJson = "<failed-to-serialize-args>";
        }

        dashboard.LogFunctionCall(clientId, functionName, argsJson);
    }
}
