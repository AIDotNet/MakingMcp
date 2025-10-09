using System.Collections.Concurrent;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class GlobTool
{
    // 文件系统目录结构缓存 - 缓存目录的文件列表和最后修改时间
    private static readonly ConcurrentDictionary<string, DirectoryCacheEntry> _directoryCache = new();

    // 缓存过期时间（30秒）
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);

    // 查询结果缓存 - 缓存具体的 pattern + path 组合结果
    private static readonly ConcurrentDictionary<string, QueryCacheEntry> _queryCache = new();

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

    private class DirectoryCacheEntry
    {
        public List<FileSystemEntryInfo> Entries { get; set; } = new();
        public DateTime CachedAt { get; set; }
        public DateTime DirectoryLastWrite { get; set; }
    }

    private class FileSystemEntryInfo
    {
        public string FullPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public DateTime LastWrite { get; set; }
    }

    private class QueryCacheEntry
    {
        public string Result { get; set; } = "";
        public DateTime CachedAt { get; set; }
    }

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
            // 生成查询缓存键
            var queryCacheKey = GetCacheKey(normalizedBasePath, pattern);

            // 检查查询缓存
            if (_queryCache.TryGetValue(queryCacheKey, out var cachedQuery))
            {
                if (DateTime.UtcNow - cachedQuery.CachedAt < _cacheExpiration)
                {
                    return cachedQuery.Result;
                }
                else
                {
                    _queryCache.TryRemove(queryCacheKey, out _);
                }
            }

            var regex = EditTool.GlobToRegex(pattern);
            List<FileSystemEntryInfo> allEntries;

            // 检查目录缓存
            if (_directoryCache.TryGetValue(normalizedBasePath, out var cachedDir))
            {
                var currentDirLastWrite = Directory.GetLastWriteTimeUtc(normalizedBasePath);

                // 验证缓存是否仍然有效
                if (DateTime.UtcNow - cachedDir.CachedAt < _cacheExpiration &&
                    cachedDir.DirectoryLastWrite >= currentDirLastWrite)
                {
                    allEntries = cachedDir.Entries;
                }
                else
                {
                    // 缓存过期，重新扫描
                    _directoryCache.TryRemove(normalizedBasePath, out _);
                    allEntries = ScanDirectory(normalizedBasePath);

                    _directoryCache.TryAdd(normalizedBasePath, new DirectoryCacheEntry
                    {
                        Entries = allEntries,
                        CachedAt = DateTime.UtcNow,
                        DirectoryLastWrite = currentDirLastWrite
                    });
                }
            }
            else
            {
                // 首次扫描，建立缓存
                allEntries = ScanDirectory(normalizedBasePath);

                _directoryCache.TryAdd(normalizedBasePath, new DirectoryCacheEntry
                {
                    Entries = allEntries,
                    CachedAt = DateTime.UtcNow,
                    DirectoryLastWrite = Directory.GetLastWriteTimeUtc(normalizedBasePath)
                });
            }

            // 在缓存的结果上应用正则匹配和排序
            var matchedEntries = allEntries
                .Where(e => regex.IsMatch(e.RelativePath))
                .OrderByDescending(e => e.LastWrite)
                .Take(EditTool.GlobOutputLimit)
                .ToList();

            var ordered = matchedEntries
                .Select(e => (e.RelativePath, e.FullPath, e.LastWrite))
                .ToList();

            if (ordered.Count == 0)
            {
                return
                    $"No entries matched the provided pattern.\npattern: {pattern}\nsearch root: {normalizedBasePath}";
            }

            var filenames = ordered.Select(e => e.FullPath).ToList();

            var result = JsonSerializer.Serialize(new
            {
                filenames = filenames,
                numFiles = ordered.Count,
                truncated = false,
            }, JsonSerializerOptions.Web);

            // 缓存查询结果
            _queryCache.TryAdd(queryCacheKey, new QueryCacheEntry
            {
                Result = result,
                CachedAt = DateTime.UtcNow
            });

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

        void ScanRecursive(string currentPath)
        {
            try
            {
                // 扫描文件
                foreach (var file in Directory.EnumerateFiles(currentPath))
                {
                    var relative = file.Length > basePathLength &&
                                   file[basePathLength] == Path.DirectorySeparatorChar
                        ? file.Substring(basePathLength + 1)
                        : Path.GetRelativePath(basePath, file);

                    var normalizedRelative = Path.DirectorySeparatorChar == '/'
                        ? relative
                        : relative.Replace(Path.DirectorySeparatorChar, '/');

                    entries.Add(new FileSystemEntryInfo
                    {
                        FullPath = file,
                        RelativePath = normalizedRelative,
                        LastWrite = File.GetLastWriteTimeUtc(file)
                    });
                }

                // 递归扫描子目录（跳过忽略的目录）
                foreach (var dir in Directory.EnumerateDirectories(currentPath))
                {
                    var dirName = Path.GetFileName(dir);
                    if (_ignoredDirectories.Contains(dirName))
                    {
                        continue;
                    }

                    ScanRecursive(dir);
                }
            }
            catch
            {
                // 忽略权限错误等异常
            }
        }

        ScanRecursive(basePath);
        return entries;
    }

    private static string GetCacheKey(string path, string pattern)
    {
        var combined = $"{path}|{pattern}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash);
    }

    private static string Error(string message)
    {
        return $"ERROR: {message}";
    }
}