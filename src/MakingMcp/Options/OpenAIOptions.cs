namespace MakingMcp.Options;

public class OpenAIOptions
{
    public static string? API_KEY { get; set; }

    public static string? OPENAI_ENDPOINT { get; set; }

    public static string? TASK_MODEL { get; set; }

    public static string? EMBEDDING_MODEL { get; set; } = "text-embedding-3-small";

    public static int MAX_OUTPUT_TOKENS { get; set; } = 32000;

    public static void Init(string[] args)
    {
        // 转为字典，key全用大写做兼容
        var argDict = args?
                          .Select(a => a.Split(['='], 2))
                          .Where(a => a.Length == 2)
                          .ToDictionary(a => a[0].Trim().ToUpper(), a => a[1].Trim(), StringComparer.OrdinalIgnoreCase)
                      ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 通用方法：优先从args获取，没有再查环境变量
        string? GetConfig(string name)
        {
            if (argDict.TryGetValue(name.ToUpper(), out var v) && !string.IsNullOrWhiteSpace(v))
                return v;
            return Environment.GetEnvironmentVariable(name);
        }

        var apiKey = GetConfig(nameof(API_KEY));
        if (!string.IsNullOrWhiteSpace(apiKey))
            API_KEY = apiKey;

        var endpoint = GetConfig(nameof(OPENAI_ENDPOINT));
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            OPENAI_ENDPOINT = endpoint;

            // 校验是否正常Url
            var isValidUrl = Uri.IsWellFormedUriString(OPENAI_ENDPOINT, UriKind.Absolute);
            if (!isValidUrl)
                throw new Exception($"环境变量/参数 {nameof(OPENAI_ENDPOINT)} 的值不是合法的Url, 当前值为: {OPENAI_ENDPOINT}");
        }

        var taskModel = GetConfig(nameof(TASK_MODEL));
        if (!string.IsNullOrWhiteSpace(taskModel))
            TASK_MODEL = taskModel;

        var maxOutputTokens = GetConfig(nameof(MAX_OUTPUT_TOKENS));
        if (!string.IsNullOrWhiteSpace(maxOutputTokens) && int.TryParse(maxOutputTokens, out var tokens))
            MAX_OUTPUT_TOKENS = tokens;

        var embeddingModel = GetConfig(nameof(EMBEDDING_MODEL));
        if (!string.IsNullOrWhiteSpace(embeddingModel))
            EMBEDDING_MODEL = embeddingModel;
    }
}