using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class GlobTool
{
    
    [McpServerTool(Name = "Glob"), KernelFunction("Glob"), Description(
         "- Fast file pattern matching tool that works with any codebase size\n- Supports glob patterns like \"**/*.js\" or \"src/**/*.ts\"\n- Returns matching file paths sorted by modification time\n- Use this tool when you need to find files by name patterns\n- When you are doing an open ended search that may require multiple rounds of globbing and grepping, use the Agent tool instead\n- You have the capability to call multiple tools in a single response. It is always better to speculatively perform multiple searches as a batch that are potentially useful.")]
    public static async Task<string> Glob(
        [Description(
            "The directory to search in. If not specified, the current working directory will be used. IMPORTANT: Omit this field to use the default directory. DO NOT enter \"undefined\" or \"null\" - simply omit it for the default behavior. Must be a valid directory path if provided.")]
        string? path,
        [Description("The glob pattern to match files against")]
        string pattern
    )
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return await Task.FromResult(Error("pattern must be provided."));
        }

        var basePath = string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : path;
        if (!EditTool.TryNormalizeAbsolutePath(basePath, out var normalizedBasePath, out var error))
        {
            return await Task.FromResult(Error(error));
        }

        if (!Directory.Exists(normalizedBasePath))
        {
            return await Task.FromResult(Error($"Directory not found: {normalizedBasePath}"));
        }

        try
        {
            var regex = EditTool.GlobToRegex(pattern);
            var entries = new List<(string Relative, string FullPath, DateTime LastWrite)>();

            foreach (var fileSystemEntry in Directory.EnumerateFileSystemEntries(normalizedBasePath, "*",
                         SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(normalizedBasePath, fileSystemEntry);
                var normalizedRelative = relative.Replace(Path.DirectorySeparatorChar, '/');

                if (!regex.IsMatch(normalizedRelative))
                {
                    continue;
                }

                var lastWrite = File.Exists(fileSystemEntry)
                    ? File.GetLastWriteTimeUtc(fileSystemEntry)
                    : Directory.GetLastWriteTimeUtc(fileSystemEntry);

                entries.Add((normalizedRelative, fileSystemEntry, lastWrite));
            }

            var ordered = entries
                .OrderByDescending(e => e.LastWrite)
                .Take(EditTool.GlobOutputLimit)
                .ToList();

            if (ordered.Count == 0)
            {
                return
                    $"No entries matched the provided pattern.\npattern: {pattern}\nsearch root: {normalizedBasePath}";
            }

            var filenames = ordered.Select(e => e.FullPath).ToList();

            return JsonSerializer.Serialize(new
            {
                filenames = filenames,
                numFiles = ordered.Count,
                truncated = false,
            }, JsonSerializerOptions.Web);
        }
        catch (Exception ex)
        {
            return Error($"Failed to execute glob: {ex.Message}");
        }
    }

    private static string Error(string message)
    {
        return $"ERROR: {message}";
    }
}