using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MakingMcp.Model;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class EditTool
{
    public const int GlobOutputLimit = 200;
    private static readonly ConcurrentDictionary<string, DateTime> ReadTracker = new(StringComparer.OrdinalIgnoreCase);

    [McpServerTool(Name = "Edit"), KernelFunction("Edit"), Description(
         "Performs exact string replacements in files. \n\nUsage:\n- You must use your `Read` tool at least once in the conversation before editing. This tool will error if you attempt an edit without reading the file. \n- When editing text from Read tool output, ensure you preserve the exact indentation (tabs/spaces) as it appears AFTER the line number prefix. The line number prefix format is: spaces + line number + tab. Everything after that tab is the actual file content to match. Never include any part of the line number prefix in the old_string or new_string.\n- ALWAYS prefer editing existing files in the codebase. NEVER write new files unless explicitly required.\n- Only use emojis if the user explicitly requests it. Avoid adding emojis to files unless asked.\n- The edit will FAIL if `old_string` is not unique in the file. Either provide a larger string with more surrounding context to make it unique or use `replace_all` to change every instance of `old_string`. \n- Use `replace_all` for replacing and renaming strings across the file. This parameter is useful if you want to rename a variable for instance.")]
    public static async Task<string> Edit(
        [Description("The absolute path to the file to modify")]
        string file_path,
        [Description("The text to replace it with (must be different from old_string)")]
        string new_string,
        [Description("The text to replace")] string old_string,
        [Description("Replace all occurences of old_string (default false)")]
        bool replace_all = false
    )
    {
        if (!TryNormalizeAbsolutePath(file_path, out var normalizedPath, out var error))
        {
            return await Task.FromResult(Error(error));
        }

        if (!File.Exists(normalizedPath))
        {
            // 创建文件
            Directory.CreateDirectory(Path.GetDirectoryName(normalizedPath));
            await File.WriteAllTextAsync(normalizedPath, string.Empty);
        }

        if (!HasRead(normalizedPath))
        {
            return await Task.FromResult(
                Error("You must call the Read tool on this file before attempting to edit it."));
        }

        if (string.IsNullOrEmpty(new_string))
        {
            return await Task.FromResult(Error("Parameter new_string must be provided."));
        }

        if (string.Equals(old_string, new_string, StringComparison.Ordinal))
        {
            return await Task.FromResult(Error("old_string and new_string must differ."));
        }

        try
        {
            if (File.Exists(normalizedPath))
            {
                var originalContent = await File.ReadAllTextAsync(normalizedPath);

                // Handle empty file case (newly created)
                if (string.IsNullOrEmpty(originalContent))
                {
                    // For empty files, we can only "replace" empty string or add content
                    if (string.IsNullOrEmpty(old_string))
                    {
                        // Adding content to empty file
                        return await HandleEmptyFileEdit(normalizedPath, new_string);
                    }
                    else
                    {
                        return Error($"Cannot find old_string '{old_string}' in empty file {normalizedPath}.");
                    }
                }

                var occurrences = CountOccurrences(originalContent, old_string);

                if (occurrences == 0)
                {
                    return Error($"No occurrences of the provided old_string were found in {normalizedPath}.");
                }

                if (!replace_all && occurrences > 1)
                {
                    return Error(
                        "old_string is not unique in the file. Provide more context or set replace_all to true.");
                }

                string updatedContent = replace_all
                    ? originalContent.Replace(old_string, new_string, StringComparison.Ordinal)
                    : ReplaceFirst(originalContent, old_string, new_string);

                await File.WriteAllTextAsync(normalizedPath, updatedContent);

                return JsonSerializer.Serialize(new
                {
                    filePath = file_path,
                    message = "Successfully applied edit.",
                    totalChanges = replace_all ? occurrences : 1,
                    lengthChange = updatedContent.Length - originalContent.Length,
                }, JsonSerializerOptions.Web);
            }
            else
            {
                await File.WriteAllTextAsync(normalizedPath, new_string);

                return JsonSerializer.Serialize(new
                {
                    filePath = file_path,
                    message = "Successfully applied edit.",
                    totalChanges = 1,
                    lengthChange = 1,
                }, JsonSerializerOptions.Web);
            }
        }
        catch (Exception ex)
        {
            return Error($"Failed to edit file: {ex.Message}");
        }
    }

    public static bool TryNormalizeAbsolutePath(string? path, out string normalizedPath, out string? error)
    {
        normalizedPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "An absolute path must be supplied.";
            return false;
        }

        if (!Path.IsPathRooted(path))
        {
            error = "The provided path must be absolute.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Unable to resolve path: {ex.Message}";
            return false;
        }
    }

    public static bool HasRead(string path)
    {
        return ReadTracker.ContainsKey(path);
    }

    public static void MarkRead(string path)
    {
        ReadTracker[path] = DateTime.UtcNow;
    }

    public static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var position = 0;

        while (true)
        {
            var index = text.IndexOf(value, position, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            position = index + value.Length;
        }

        return count;
    }

    public static string ReplaceFirst(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.Ordinal);
        return index < 0
            ? text
            : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + search.Length));
    }

    public static Regex GlobToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern.Replace('\\', '/'));
        escaped = escaped
            .Replace(@"\*\*", "::double_star::", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal)
            .Replace("::double_star::", ".*");

        return new Regex($"^{escaped}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    public static string LimitLines(string text, int? maxLines)
    {
        if (maxLines is null or <= 0)
        {
            return text;
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= maxLines)
        {
            return text;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < maxLines; i++)
        {
            builder.AppendLine(lines[i]);
        }

        return builder.ToString().TrimEnd();
    }

    public static string Error(string message)
    {
        return JsonSerializer.Serialize(new { error = message }, JsonSerializerOptions.Web);
    }

    private static async Task<string> CreateAndInitializeFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, string.Empty);
        MarkRead(filePath);
        return string.Empty; // No error
    }

    private static async Task<string> HandleEmptyFileEdit(string filePath, string newContent)
    {
        await File.WriteAllTextAsync(filePath, newContent, Encoding.UTF8);
        return JsonSerializer.Serialize(new
        {
            filePath,
            message = "Successfully added content to new file.",
            totalChanges = 1,
            lengthChange = newContent.Length,
        }, JsonSerializerOptions.Web);
    }
}