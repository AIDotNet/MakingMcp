using System.ComponentModel;
using System.Text.Json;
using AIDotNet.Toon;
using MakingMcp.Model;
using MakingMcp.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Shared.Tools;

public class MultiEditTool
{
    [McpServerTool(Name = "MultiEdit"), Description(
         """
         This is a tool for making multiple edits to a single file in one operation. It is built on top of the Edit tool and allows you to perform multiple find-and-replace operations efficiently. Prefer this tool over the Edit tool when you need to make multiple edits to the same file.

         Before using this tool:

         1. Use the Read tool to understand the file's contents and context
         2. Verify the directory path is correct

         To make multiple file edits, provide the following:
         1. file_path: The absolute path to the file to modify (must be absolute, not relative)
         2. edits: An array of edit operations to perform, where each edit contains:
            - old_string: The text to replace (must match the file contents exactly, including all whitespace and indentation)
            - new_string: The edited text to replace the old_string
            - replace_all: Replace all occurences of old_string. This parameter is optional and defaults to false.

         IMPORTANT:
         - All edits are applied in sequence, in the order they are provided
         - Each edit operates on the result of the previous edit
         - All edits must be valid for the operation to succeed - if any edit fails, none will be applied
         - This tool is ideal when you need to make several changes to different parts of the same file
         - For Jupyter notebooks (.ipynb files), use the NotebookEdit instead

         CRITICAL REQUIREMENTS:
         1. All edits follow the same requirements as the single Edit tool
         2. The edits are atomic - either all succeed or none are applied
         3. Plan your edits carefully to avoid conflicts between sequential operations

         WARNING:
         - The tool will fail if edits.old_string doesn't match the file contents exactly (including whitespace)
         - The tool will fail if edits.old_string and edits.new_string are the same
         - Since edits are applied in sequence, ensure that earlier edits don't affect the text that later edits are trying to find

         When making edits:
         - Ensure all edits result in idiomatic, correct code
         - Do not leave the code in a broken state
         - Always use absolute file paths (starting with /)
         - Only use emojis if the user explicitly requests it. Avoid adding emojis to files unless asked.
         - Use replace_all for replacing and renaming strings across the file. This parameter is useful if you want to rename a variable for instance.

         If you want to create a new file, use:
         - A new file path, including dir name if needed
         - First edit: empty old_string and the new file's contents as new_string
         - Subsequent edits: normal edit operations on the created content
         """)]
    public static async Task<string> MultiEdit(
        [Description("Array of edit operations to perform sequentially on the file")]
        MultiEditInput[] edits,
        [Description("The absolute path to the file to modify")]
        string file_path)
    {
        if (edits is not { Length: > 0 })
        {
            return await Task.FromResult(EditTool.Error("At least one edit operation must be supplied."));
        }

        if (!EditTool.TryNormalizeAbsolutePath(file_path, out var normalizedPath, out var normalizeEditTool))
        {
            return await Task.FromResult(EditTool.Error(normalizeEditTool));
        }

        if (!File.Exists(normalizedPath))
        {
            return await Task.FromResult(EditTool.Error($"File not found: {normalizedPath}"));
        }

        if (!EditTool.HasRead(normalizedPath))
        {
            return await Task.FromResult(
                EditTool.Error("You must call the Read tool on this file before attempting to edit it."));
        }

        try
        {
            var originalContent = await File.ReadAllTextAsync(normalizedPath);
            var updatedContent = originalContent;
            var totalChanges = 0;
            for (var index = 0; index < edits.Length; index++)
            {
                var edit = edits[index];

                if (string.IsNullOrEmpty(edit.OldString))
                {
                    return EditTool.Error($"Edit {index + 1}: old_string must be provided.");
                }

                if (string.IsNullOrEmpty(edit.NewString))
                {
                    return EditTool.Error($"Edit {index + 1}: new_string must be provided.");
                }

                if (string.Equals(edit.OldString, edit.NewString, StringComparison.Ordinal))
                {
                    return EditTool.Error($"Edit {index + 1}: old_string and new_string must differ.");
                }

                var occurrences = EditTool.CountOccurrences(updatedContent, edit.OldString);
                if (occurrences == 0)
                {
                    return EditTool.Error($"Edit {index + 1}: no occurrences of old_string were found.");
                }

                if (!edit.ReplaceAll && occurrences > 1)
                {
                    return EditTool.Error(
                        $"Edit {index + 1}: old_string is not unique. Provide more context or set replace_all to true.");
                }

                updatedContent = edit.ReplaceAll
                    ? updatedContent.Replace(edit.OldString, edit.NewString, StringComparison.Ordinal)
                    : EditTool.ReplaceFirst(updatedContent, edit.OldString, edit.NewString);

                var replacementCount = edit.ReplaceAll ? occurrences : 1;
                totalChanges += replacementCount;
            }

            await File.WriteAllTextAsync(normalizedPath, updatedContent);

            return ToonSerializer.Serialize(new
            {
                filePath = file_path,
                message = " Successfully applied all edits.",
                totalEdits = edits.Length,
                totalChanges,
                lengthChange = updatedContent.Length - originalContent.Length,
            });
        }
        catch (Exception ex)
        {
            return EditTool.Error($"MultiEdit failed: {ex.Message}");
        }
    }
}