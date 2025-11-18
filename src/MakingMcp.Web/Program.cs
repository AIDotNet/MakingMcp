using MakingMcp.Shared.Tools;
using MakingMcp.Tools;
using ModelContextProtocol.Server;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Principal;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/makingmcp-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting MakingMcp Web Service");

    if (args.Length > 0)
    {
        var command = args[0].ToLower();
        if (command == "install")
        {
            InstallWindowsService();
            return;
        }
        else if (command == "uninstall")
        {
            UninstallWindowsService();
            return;
        }
    }


    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    OpenAIOptions.Init(builder.Configuration);
    Log.Information("OpenAI configuration initialized");

    // Add Windows Service support
    builder.Services.AddWindowsService(options => { options.ServiceName = "MakingMcpWebService"; });

    // Create and populate the tool dictionary at startup
    var toolDictionary = new ConcurrentDictionary<string, McpServerTool[]>();
    PopulateToolDictionary(toolDictionary);

    builder.Services.AddMcpServer()
        .WithHttpTransport(options =>
        {
            // Configure per-session options to filter tools based on route category
            options.ConfigureSessionOptions = async (httpContext, mcpOptions, _) =>
            {
                // Determine tool category from route parameters
                var toolCategory = httpContext.Request.Query["tools"].ToString()?.ToLower();

                if (string.IsNullOrEmpty(toolCategory))
                {
                    toolCategory = "all";
                }

                var toolDictionary = new ConcurrentDictionary<string, McpServerTool[]>();
                PopulateToolDictionary(toolDictionary);

                var tools = toolCategory;

                Log.Information("Configuring session with tool category: {ToolCategory}", toolCategory);

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
                            Log.Information("Added tool: {Tool} with {Count} methods", tool, toolArray.Length);
                        }
                        else
                        {
                            Log.Warning("Tool '{Tool}' not recognized", tool);
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
                    if (toolDictionary.TryGetValue("all", out var allTools))
                    {
                        toolDictionary.Clear();
                        toolDictionary.TryAdd("all", allTools);
                    }
                }

                mcpOptions.ServerInfo = new ModelContextProtocol.Protocol.Implementation()
                {
                    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    Name = "MakingMcp Web Service",
                    Title = "MakingMcp Web Service"
                };
                mcpOptions.Capabilities = new();
                mcpOptions.Capabilities.Tools = new();
                var toolCollection = mcpOptions.ToolCollection = [];

                foreach (var tool in toolDictionary.SelectMany(x => x.Value))
                {
                    toolCollection.Add(tool);
                }

                await Task.CompletedTask;
            };
        });

    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapMcp("/mcp");

    Log.Information("MakingMcp Web Service started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


static void PopulateToolDictionary(ConcurrentDictionary<string, McpServerTool[]> toolDictionary)
{
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
    if (!string.IsNullOrEmpty(OpenAIOptions.TAVILY_API_KEY))
    {
        toolDictionary.TryAdd("Web", webTools);
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

static void InstallWindowsService()
{
    if (!IsAdministrator())
    {
        Console.WriteLine("Error: Administrator privileges required to install Windows Service.");
        Console.WriteLine("Please run this application as Administrator.");
        Environment.Exit(1);
        return;
    }

    try
    {
        var exePath = Environment.ProcessPath ??
                      throw new InvalidOperationException("Unable to determine executable path");
        var serviceName = "MakingMcpWebService";
        var displayName = "MakingMcp Web Service";
        var description = "MCP (Model Context Protocol) Web Service for AI tool integration";

        Console.WriteLine($"Installing Windows Service: {serviceName}");
        Console.WriteLine($"Executable: {exePath}");

        // Create the service using sc.exe
        var createProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments =
                    $"create \"{serviceName}\" binPath= \"\\\"{exePath}\\\"\" start= auto DisplayName= \"{displayName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        createProcess.Start();
        var output = createProcess.StandardOutput.ReadToEnd();
        var error = createProcess.StandardError.ReadToEnd();
        createProcess.WaitForExit();

        if (createProcess.ExitCode == 0)
        {
            Console.WriteLine("Service created successfully.");

            // Set service description
            var descProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"description \"{serviceName}\" \"{description}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            descProcess.Start();
            descProcess.WaitForExit();

            Console.WriteLine($"\nService '{serviceName}' installed successfully!");

            // Start the service automatically after installation
            Console.WriteLine($"Starting service '{serviceName}'...");
            var startProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"start \"{serviceName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            startProcess.Start();
            var startOutput = startProcess.StandardOutput.ReadToEnd();
            var startError = startProcess.StandardError.ReadToEnd();
            startProcess.WaitForExit();

            if (startProcess.ExitCode == 0)
            {
                Console.WriteLine($"Service '{serviceName}' started successfully!");
            }
            else
            {
                Console.WriteLine($"Service installed but failed to start. Exit code: {startProcess.ExitCode}");
                if (!string.IsNullOrEmpty(startOutput)) Console.WriteLine($"Output: {startOutput}");
                if (!string.IsNullOrEmpty(startError)) Console.WriteLine($"Error: {startError}");
                Console.WriteLine($"\nYou can start it manually with: sc start {serviceName}");
                Console.WriteLine($"Or use: net start {serviceName}");
            }
        }
        else
        {
            Console.WriteLine($"Failed to create service. Exit code: {createProcess.ExitCode}");
            if (!string.IsNullOrEmpty(output)) Console.WriteLine($"Output: {output}");
            if (!string.IsNullOrEmpty(error)) Console.WriteLine($"Error: {error}");
            Environment.Exit(1);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error installing service: {ex.Message}");
        Environment.Exit(1);
    }
}

static void UninstallWindowsService()
{
    if (!IsAdministrator())
    {
        Console.WriteLine("Error: Administrator privileges required to uninstall Windows Service.");
        Console.WriteLine("Please run this application as Administrator.");
        Environment.Exit(1);
        return;
    }

    try
    {
        var serviceName = "MakingMcpWebService";

        Console.WriteLine($"Uninstalling Windows Service: {serviceName}");

        // Stop the service first (if running)
        var stopProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"stop \"{serviceName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        stopProcess.Start();
        stopProcess.WaitForExit();

        // Wait a moment for the service to stop
        System.Threading.Thread.Sleep(1000);

        // Delete the service
        var deleteProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete \"{serviceName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        deleteProcess.Start();
        var output = deleteProcess.StandardOutput.ReadToEnd();
        var error = deleteProcess.StandardError.ReadToEnd();
        deleteProcess.WaitForExit();

        if (deleteProcess.ExitCode == 0)
        {
            Console.WriteLine($"Service '{serviceName}' uninstalled successfully!");
        }
        else
        {
            Console.WriteLine($"Failed to delete service. Exit code: {deleteProcess.ExitCode}");
            if (!string.IsNullOrEmpty(output)) Console.WriteLine($"Output: {output}");
            if (!string.IsNullOrEmpty(error)) Console.WriteLine($"Error: {error}");
            Environment.Exit(1);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error uninstalling service: {ex.Message}");
        Environment.Exit(1);
    }
}

static bool IsAdministrator()
{
    if (!OperatingSystem.IsWindows())
        return false;

    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}