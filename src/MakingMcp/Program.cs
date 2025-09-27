using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MakingMcp.Options;
using MakingMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace MakingMcp;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        OpenAIOptions.Init(args);

        var builder = Host.CreateApplicationBuilder(args);

        // 解析命令行参数 tools=Task,WebFetch,WebSearch,Write,Read,Edit,MultiEdit,Glob,Grep,Bash,BashOutput,KillBash
        var toolDictionary = new ConcurrentDictionary<string, McpServerTool[]>();
        PopulateToolDictionary(toolDictionary);

        var tools = args.FirstOrDefault(arg => arg.StartsWith("tools="))?["tools=".Length..];

        if (!string.IsNullOrEmpty(tools))
        {
            var selectedTools =
                tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var selectedToolList = new List<McpServerTool>();
            foreach (var tool in selectedTools)
            {
                if (toolDictionary.TryGetValue(tool.ToLower(), out var toolArray))
                {
                    selectedToolList.AddRange(toolArray);
                }
                else
                {
                    Console.WriteLine($"Warning: Tool '{tool}' not recognized.");
                }
            }

            if (selectedToolList.Count > 0)
            {
                // Clear existing tools and add only the selected ones
                toolDictionary.Clear();
                toolDictionary.TryAdd("selected", selectedToolList.ToArray());
            }
        }
        else
        {
            // If no tools specified, add all tools under "all" key
            if (toolDictionary.TryGetValue("all", out var allTools))
            {
                toolDictionary.Clear();
                toolDictionary.TryAdd("all", allTools);
            }
        }


        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        var stdio = builder.Services
            .AddMcpServer((options => { }))
            .WithStdioServerTransport();

        foreach (var tool in toolDictionary)
        {
            stdio.WithTools(tool.Value);
        }

        var host = builder.Build();


        await host.RunAsync();
    }

    private static void PopulateToolDictionary(ConcurrentDictionary<string, McpServerTool[]> toolDictionary)
    {
        // Get tools for each category
        var taskTools = !string.IsNullOrEmpty(OpenAIOptions.TASK_MODEL)
            ? GetToolsForType<TaskTool>()
            : [];

        var bashOutputTools = GetToolsForType<BashOutputTool>();
        var bashTools = GetToolsForType<BashTool>();
        var editTools = GetToolsForType<EditTool>();
        var globTools = GetToolsForType<GlobTool>();
        var killBashTools = GetToolsForType<KillBashTool>();
        var multiEditTools = GetToolsForType<MultiEditTool>();
        var readTools = GetToolsForType<ReadTool>();
        var webTools = GetToolsForType<WebTool>();
        var writeTools = GetToolsForType<WriteTool>();
        McpServerTool[] allTools =
        [
            .. taskTools,
            .. bashOutputTools,
            .. bashTools,
            .. editTools,
            .. globTools,
            .. killBashTools,
            .. multiEditTools,
            .. readTools,
            .. webTools,
            .. writeTools,
        ];

        toolDictionary.TryAdd("BashOutput", bashOutputTools);
        toolDictionary.TryAdd("Bash", bashTools);
        toolDictionary.TryAdd("Edit", editTools);
        toolDictionary.TryAdd("Glob", globTools);
        toolDictionary.TryAdd("LillBash", killBashTools);
        toolDictionary.TryAdd("MultiEdit", multiEditTools);
        toolDictionary.TryAdd("Read", readTools);
        toolDictionary.TryAdd("Write", writeTools);
        if (!string.IsNullOrEmpty(WebTool.GetTavilyApiKey()))
        {
            toolDictionary.TryAdd("Web", webTools);
        }

        if (!string.IsNullOrEmpty(OpenAIOptions.TASK_MODEL))
        {
            toolDictionary.TryAdd("Task", taskTools);
        }

        toolDictionary.TryAdd("all", allTools);
    }

    static McpServerTool[] GetToolsForType<[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods)]
        T>()
    {
        var tools = new List<McpServerTool>();
        var toolType = typeof(T);
        var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Any());

        foreach (var method in methods)
        {
            try
            {
                var tool = McpServerTool.Create(method, target: null, new McpServerToolCreateOptions());
                tools.Add(tool);
            }
            catch (Exception ex)
            {
                // Log error but continue with other tools
                Console.WriteLine($"Failed to add tool {toolType.Name}.{method.Name}: {ex.Message}");
            }
        }

        return [.. tools];
    }
}