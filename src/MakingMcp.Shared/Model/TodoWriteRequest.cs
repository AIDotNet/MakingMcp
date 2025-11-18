using System.Text.Json.Serialization;

namespace MakingMcp.Model;

/// <summary>
/// Request to write/update todo list
/// </summary>
public class TodoWriteRequest
{
    /// <summary>
    /// List of todo items to create or update
    /// </summary>
    [JsonPropertyName("todos")]
    public List<TodoItemInput> Todos { get; set; } = new();
}

/// <summary>
/// Input for creating or updating a todo item
/// </summary>
public class TodoItemInput
{
    /// <summary>
    /// Task description
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the task (pending, in_progress, completed)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Optional notes or additional context for the task
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Optional ID for updating existing task
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
