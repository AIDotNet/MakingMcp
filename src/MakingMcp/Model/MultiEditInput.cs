using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MakingMcp.Model;

public class MultiEditRequest
{
    [JsonPropertyName("filePath")]
    [Description("The absolute path to the file to modify")]
    public string FilePath { get; set; }
    
    [JsonPropertyName("edits")]
    [Description("Array of edit operations to perform sequentially on the file")]
    public MultiEditInput[] Edits { get; set; }
}

public class MultiEditInput
{
    [JsonPropertyName("new_string")]
    [Description("The text to replace it with")]
    public string NewString { get; set; }
    
    [JsonPropertyName("old_string")]
    [Description( "The text to replace")]
    public string OldString { get; set; }
    
    [JsonPropertyName("replace_all")]
    [Description("Replace all occurences of old_string (default false).")]
    public bool ReplaceAll { get; set; }
}