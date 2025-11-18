using System.ComponentModel;
using System.Text.Json;
using MakingMcp.Shared.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class GlobTool
{
    // 需要忽略的常见目录（构建产物、依赖、缓存等 - 覆盖主流语言和框架）
    private static readonly HashSet<string> _ignoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        // 版本控制
        ".git", ".svn", ".hg", ".bzr",

        // IDE 和编辑器
        ".vs", ".vscode", ".idea", ".fleet", ".eclipse", ".settings",

        // JavaScript/TypeScript/Node
        "node_modules", "dist", "build", ".next", ".nuxt", ".output",
        "coverage", ".nyc_output", ".parcel-cache", ".cache", ".temp",

        // .NET/C#
        "bin", "obj", "packages", ".nuget",

        // Java/JVM
        "target", "out", ".gradle", ".mvn",

        // Python
        "__pycache__", ".pytest_cache", ".mypy_cache", ".tox",
        "venv", ".venv", "env", ".env", "virtualenv", ".virtualenv",
        "eggs", ".eggs", "*.egg-info", "dist", "build",

        // Ruby
        "vendor", ".bundle", "tmp",

        // Go
        "vendor",

        // Rust
        "target",

        // PHP
        "vendor",

        // Swift/Xcode
        "Pods", ".build", "DerivedData",

        // Android
        ".gradle", "build",

        // 通用临时文件和缓存
        "tmp", "temp", ".tmp", ".cache", ".local"
    };

    private class FileSystemEntryInfo
    {
        public string FullPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public DateTime LastWrite { get; set; }
    }

    [McpServerTool(Name = "Glob"),Description(
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
            var allEntries = ScanDirectory(normalizedBasePath);

            // 在结果上应用正则匹配和排序
            var matchedEntries = allEntries
                .Where(e => regex.IsMatch(e.RelativePath))
                .OrderByDescending(e => e.LastWrite)
                .Take(EditTool.GlobOutputLimit)
                .ToList();

            if (matchedEntries.Count == 0)
            {
                return $"No entries matched the provided pattern.\npattern: {pattern}\nsearch root: {normalizedBasePath}";
            }

            var filenames = matchedEntries.Select(e => e.FullPath).ToList();

            var result = JsonSerializer.Serialize(new
            {
                filenames = filenames,
                numFiles = matchedEntries.Count,
                truncated = false,
            }, JsonSerializerOptions.Web);

            return result;
        }
        catch (Exception ex)
        {
            return Error($"Failed to execute glob: {ex.Message}");
        }
    }

    private static List<FileSystemEntryInfo> ScanDirectory(string basePath)
    {
        var entries = new List<FileSystemEntryInfo>();
        var basePathLength = basePath.Length;
        var stack = new Stack<string>();
        stack.Push(basePath);

        while (stack.Count > 0)
        {
            var currentPath = stack.Pop();

            // 检查是否为忽略的目录（但不跳过根目录）
            if (currentPath != basePath)
            {
                var dirName = Path.GetFileName(currentPath);
                if (_ignoredDirectories.Contains(dirName))
                {
                    continue;
                }
            }

            try
            {
                // 1. 扫描当前目录的文件
                try
                {
                    foreach (var file in Directory.EnumerateFiles(currentPath))
                    {
                        try
                        {
                            var relative = file.Length > basePathLength &&
                                           file[basePathLength] == Path.DirectorySeparatorChar
                                ? file.Substring(basePathLength + 1)
                                : Path.GetRelativePath(basePath, file);

                            var normalizedRelative = Path.DirectorySeparatorChar == '/'
                                ? relative
                                : relative.Replace(Path.DirectorySeparatorChar, '/');

                            var info = new FileSystemEntryInfo
                            {
                                FullPath = file,
                                RelativePath = normalizedRelative,
                                LastWrite = File.GetLastWriteTimeUtc(file)
                            };

                            entries.Add(info);
                        }
                        catch
                        {
                            // 忽略单个文件的读取错误，继续处理其他文件
                        }
                    }
                }
                catch
                {
                    // 忽略整个目录的文件枚举错误，继续处理子目录
                }

                // 2. 将子目录添加到栈中
                try
                {
                    foreach (var subDir in Directory.EnumerateDirectories(currentPath))
                    {
                        stack.Push(subDir);
                    }
                }
                catch
                {
                    // 忽略子目录枚举错误
                }
            }
            catch
            {
                // 忽略整个目录的访问错误，继续处理栈中的其他目录
            }
        }

        return entries;
    }

    private static string Error(string message)
    {
        return $"ERROR: {message}";
    }
}
