using Microsoft.Extensions.Configuration;

public class OpenAIOptions
{
    public static string? API_KEY { get; private set; }

    public static string? OPENAI_ENDPOINT { get; private set; }

    public static string? TASK_MODEL { get; private set; }

    public static string? EMBEDDING_MODEL { get; private set; } = "text-embedding-3-small";

    public static int MAX_OUTPUT_TOKENS { get; private set; } = 32000;

    private const string ApiKeyVariable = "OPENAI_API_KEY";
    private const string EndpointVariable = "OPENAI_ENDPOINT";
    private const string TaskModelVariable = "TASK_MODEL";
    private const string EmbeddingModelVariable = "EMBEDDING_MODEL";
    private const string MaxOutputTokensVariable = "MAX_OUTPUT_TOKENS";

    public static void Init()
    {
        API_KEY = ReadEnv(ApiKeyVariable);

        OPENAI_ENDPOINT = ReadEnv(EndpointVariable);
        if (!string.IsNullOrWhiteSpace(OPENAI_ENDPOINT) &&
            !Uri.IsWellFormedUriString(OPENAI_ENDPOINT, UriKind.Absolute))
        {
            throw new Exception(
                $"Environment variable {EndpointVariable} is not a valid absolute URL. Current value: {OPENAI_ENDPOINT}");
        }

        TASK_MODEL = ReadEnv(TaskModelVariable);

        var maxOutputTokens = ReadEnv(MaxOutputTokensVariable);
        if (!string.IsNullOrWhiteSpace(maxOutputTokens) && int.TryParse(maxOutputTokens, out var tokens))
        {
            MAX_OUTPUT_TOKENS = tokens;
        }

        var embeddingModel = ReadEnv(EmbeddingModelVariable);
        if (!string.IsNullOrWhiteSpace(embeddingModel))
        {
            EMBEDDING_MODEL = embeddingModel;
        }
    }

    public static void Init(IConfiguration configuration)
    {
        API_KEY = configuration[ApiKeyVariable];

        OPENAI_ENDPOINT = configuration[EndpointVariable];
        if (!string.IsNullOrWhiteSpace(OPENAI_ENDPOINT) &&
            !Uri.IsWellFormedUriString(OPENAI_ENDPOINT, UriKind.Absolute))
        {
            throw new Exception(
                $"Configuration {EndpointVariable} is not a valid absolute URL. Current value: {OPENAI_ENDPOINT}");
        }

        TASK_MODEL = configuration[TaskModelVariable];

        var maxOutputTokens = configuration[MaxOutputTokensVariable];
        if (!string.IsNullOrWhiteSpace(maxOutputTokens) && int.TryParse(maxOutputTokens, out var tokens))
        {
            MAX_OUTPUT_TOKENS = tokens;
        }

        var embeddingModel = configuration[EmbeddingModelVariable];
        if (!string.IsNullOrWhiteSpace(embeddingModel))
        {
            EMBEDDING_MODEL = embeddingModel;
        }
    }

    private static string? ReadEnv(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}