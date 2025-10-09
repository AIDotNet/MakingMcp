using System.ComponentModel;

namespace MakingMcp.Model;

public class AgentConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public AgentTools Tools { get; set; } = AgentTools.None;
}

[Flags]
public enum AgentTools
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Edit = 1 << 2,
    Glob = 1 << 3,
    Grep = 1 << 4,
    Bash = 1 << 5,
    Web = 1 << 6,

    ReadEdit = Read | Edit,
    ReadWriteEditGlobGrep = Read | Write | Edit | Glob | Grep,
    All = Read | Write | Edit | Glob | Grep | Bash | Web
}