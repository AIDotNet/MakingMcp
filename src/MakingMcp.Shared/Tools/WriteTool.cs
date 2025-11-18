using System.ComponentModel;
using MakingMcp.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Shared.Tools;

public class WriteTool
{
    [McpServerTool(Name = "Write"),  Description(
         """
         Writes a file to the local filesystem.

         Usage:
         - This tool will overwrite the existing file if there is one at the provided path.
         - If this is an existing file, you MUST use the Read tool first to read the file's contents. This tool will fail if you did not read the file first.
         - ALWAYS prefer editing existing files in the codebase. NEVER write new files unless explicitly required.
         - NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.
         - Only use emojis if the user explicitly requests it. Avoid writing emojis to files unless asked.
         """)]
    public static async Task<string> Write(
        [Description("The content to write to the file")]
        string content,
        [Description("The absolute path to the file to write (must be absolute, not relative)")]
        string file_path
    )
    {
        if (!EditTool.TryNormalizeAbsolutePath(file_path, out var normalizedPath, out var error))
        {
            return await Task.FromResult(EditTool.Error(error));
        }

        if (File.Exists(normalizedPath) && !EditTool.HasRead(normalizedPath))
        {
            return await Task.FromResult(
                EditTool.Error("You must call the Read tool on this file before attempting to overwrite it."));
        }

        try
        {
            var directory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(normalizedPath, content);
            EditTool.MarkRead(normalizedPath);

            return "Successfully wrote file: " + normalizedPath;
        }
        catch (Exception ex)
        {
            return EditTool.Error($"Failed to write file: {ex.Message}");
        }
    }
}