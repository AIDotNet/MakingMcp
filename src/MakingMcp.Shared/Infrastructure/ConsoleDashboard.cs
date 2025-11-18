using System.Collections.Concurrent;
using Spectre.Console;

namespace MakingMcp.Shared.Infrastructure;

/// <summary>
/// Renders a console dashboard using Spectre.Console panels:
/// - ASCII Art Title Header
/// - Agent Output: Shows MCP client and function logs
/// - Agent Status: Shows current phase, tokens, model info
/// - Recent Tool Calls: Shows recent tool invocations
/// </summary>
public sealed class ConsoleDashboard
{
    private readonly object _lock = new();
    private readonly ConcurrentQueue<(DateTime Timestamp, string ClientId, string Message)> _entries = new();
    private readonly ConcurrentQueue<(DateTime Timestamp, string ToolName, string Args)> _toolCalls = new();
    private readonly int _maxEntries;
    private bool _started;

    private Layout? _layout;

    public string Version { get; }
    public string Model { get; }
    public bool SearchEnabled { get; }

    // Status tracking
    public string CurrentPhase { get; set; } = "Initializing";
    public int UniqueToolCalls { get; private set; }
    public string Status { get; set; } = "Running";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int AvailableAgents { get; set; } = 1;

    public ConsoleDashboard(string version, string model, bool searchEnabled, int maxEntries = 200)
    {
        Version = version;
        Model = string.IsNullOrWhiteSpace(model) ? "N/A" : model;
        SearchEnabled = searchEnabled;
        _maxEntries = maxEntries;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        // Run live rendering on a background thread so we do not block the web host.
        // Use fire-and-forget pattern to ensure non-blocking
        _ = Task.Run(async () =>
        {
            try
            {
                // Fixed heights for each section
                var titleHeight = 8;
                var statusHeight = 13;

                _layout = new Layout("Root")
                    .SplitRows(
                        new Layout("Title").Size(titleHeight),
                        new Layout("Content"));

                // Split content into left (output) and right (status + tool calls)
                _layout["Content"].SplitColumns(
                    new Layout("Output"),
                    new Layout("Right").Size(60));

                // Split right side into status and tool calls
                // Status has fixed height, Tool Calls fills remaining space
                _layout["Right"].SplitRows(
                    new Layout("Status").Size(statusHeight),
                    new Layout("ToolCalls"));

                // Initialize panels
                _layout["Title"].Update(BuildTitlePanel());
                _layout["Output"].Update(BuildOutputPanel());
                _layout["Status"].Update(BuildStatusPanel());
                _layout["ToolCalls"].Update(BuildToolCallsPanel());

                await AnsiConsole.Live(_layout)
                    .StartAsync(async ctx =>
                    {
                        while (true)
                        {
                            UpdatePanels();
                            ctx.Refresh();
                            await Task.Delay(200);
                        }
                    });
            }
            catch (Exception ex)
            {
                // Log error but don't crash the service
                AnsiConsole.MarkupLine($"[red]Dashboard error: {ex.Message}[/]");
            }
        });
    }

    public void LogClientConnected(string clientId, string? toolCategory)
    {
        Enqueue(clientId, $"Client connected (tools={toolCategory ?? "all"})");
    }

    public void LogFunctionCall(string clientId, string functionName, string argsJson)
    {
        Enqueue(clientId, $"Function {functionName} args={argsJson}");

        // Track tool calls
        _toolCalls.Enqueue((DateTime.UtcNow, functionName, argsJson));
        while (_toolCalls.Count > 20 && _toolCalls.TryDequeue(out _))
        {
            // Keep only last 20 tool calls
        }

        // Update unique tool calls count
        UniqueToolCalls = _toolCalls.Select(t => t.ToolName).Distinct().Count();
    }

    private void Enqueue(string clientId, string message)
    {
        _entries.Enqueue((DateTime.UtcNow, clientId, message));

        while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
        {
            // Drop oldest items when buffer is full.
        }
    }

    private Panel BuildTitlePanel()
    {
        var asciiArt = new FigletText("MakingMcp AI Agent")
            .Color(Color.Cyan1);

        return new Panel(asciiArt)
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Magenta),
            Padding = new Padding(2, 0)
        };
    }

    private Panel BuildOutputPanel()
    {
        var table = new Table
        {
            Border = TableBorder.Rounded,
            ShowRowSeparators = false,
            Expand = true
        };

        table.AddColumn(new TableColumn("[grey]Time[/]").NoWrap().Width(10));
        table.AddColumn(new TableColumn("[grey]Client[/]").NoWrap().Width(15));
        table.AddColumn(new TableColumn("[grey]Message[/]"));

        return new Panel(table)
        {
            Header = new PanelHeader("Agent Output", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };
    }

    private Panel BuildStatusPanel()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());

        return new Panel(grid)
        {
            Header = new PanelHeader("Agent Status", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };
    }

    private Panel BuildToolCallsPanel()
    {
        var table = new Table
        {
            Border = TableBorder.None,
            ShowHeaders = false,
            Expand = true
        };

        table.AddColumn(new TableColumn("").NoWrap());

        return new Panel(table)
        {
            Header = new PanelHeader("Recent Tool Calls", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
    }

    private void UpdatePanels()
    {
        if (_layout is null)
        {
            return;
        }

        _layout["Output"].Update(BuildOutputPanelWithData());
        _layout["Status"].Update(BuildStatusPanelWithData());
        _layout["ToolCalls"].Update(BuildToolCallsPanelWithData());
    }

    private Panel BuildOutputPanelWithData()
    {
        var table = new Table
        {
            Border = TableBorder.None,
            ShowRowSeparators = false,
            Expand = true
        };

        table.AddColumn(new TableColumn("[grey]Time[/]").NoWrap().Width(10));
        table.AddColumn(new TableColumn("[grey]Client[/]").NoWrap().Width(15));
        table.AddColumn(new TableColumn("[grey]Message[/]"));

        foreach (var (timestamp, clientId, message) in _entries.OrderBy(e => e.Timestamp).TakeLast(20))
        {
            var truncatedMessage = message.Length > 80 ? message.Substring(0, 77) + "..." : message;
            table.AddRow(
                new Markup($"[dim]{timestamp:HH:mm:ss}[/]"),
                new Markup($"[blue]{clientId.Substring(0, Math.Min(12, clientId.Length))}[/]"),
                new Markup(truncatedMessage.EscapeMarkup()));
        }

        return new Panel(table)
        {
            Header = new PanelHeader("Agent Output", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };
    }

    private Panel BuildStatusPanelWithData()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());

        grid.AddRow(new Markup($"[bold]Current Phase:[/] [cyan]※ {CurrentPhase.EscapeMarkup()}[/]"));
        grid.AddRow(new Markup($"[bold]Unique Tool Calls:[/] [yellow]{UniqueToolCalls}[/]"));
        grid.AddRow(new Markup($"[bold]Status:[/] [green]{Status.EscapeMarkup()}[/]"));
        grid.AddRow(new Markup($"[bold]Tokens:[/] [green]↑ {InputTokens}[/]  [yellow]↓ {OutputTokens}[/]"));
        grid.AddEmptyRow();
        grid.AddRow(new Rule().LeftJustified());
        grid.AddEmptyRow();
        grid.AddRow(new Markup($"[bold]Model:[/] [cyan]{Model.EscapeMarkup()}[/] | Available Agents: [yellow]{AvailableAgents}[/]"));

        return new Panel(grid)
        {
            Header = new PanelHeader("Agent Status", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };
    }

    private Panel BuildToolCallsPanelWithData()
    {
        var table = new Table
        {
            Border = TableBorder.None,
            ShowHeaders = false,
            Expand = true
        };

        table.AddColumn(new TableColumn("").NoWrap());

        foreach (var (timestamp, toolName, args) in _toolCalls.OrderByDescending(t => t.Timestamp).Take(10))
        {
            var truncatedArgs = args.Length > 50 ? args.Substring(0, 47) + "..." : args;
            var timeStr = timestamp.ToString("HH:mm:ss");
            var line = $"[dim]{timeStr}[/] [cyan]{toolName.EscapeMarkup()}[/] [grey]{{{truncatedArgs.EscapeMarkup()}}}[/]";
            table.AddRow(new Markup(line));
        }

        return new Panel(table)
        {
            Header = new PanelHeader("Recent Tool Calls", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
    }
}
