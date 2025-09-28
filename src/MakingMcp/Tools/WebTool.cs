using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Tools;

public class WebTool
{
    private const string TavilyBaseUrl = "https://api.tavily.com";
    private const int DefaultMaxResults = 6;

    private static readonly HttpClient HttpClient = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = false
    };

    [McpServerTool(Name = "WebFetch"),
     KernelFunction("WebFetch"),
     Description(
         """
         - Fetches content from a specified URL and processes it using an AI model
         - Takes a URL and a prompt as input
         - Fetches the URL content, converts HTML to markdown
         - Processes the content with the prompt using a small, fast model
         - Returns the model's response about the content
         - Use this tool when you need to retrieve and analyze web content

         Usage notes:
         - IMPORTANT: If an MCP-provided web fetch tool is available, prefer using that tool instead of this one, as it may have fewer restrictions. All MCP-provided tools start with "mcp__".
         - The URL must be a fully-formed valid URL
         - HTTP URLs will be automatically upgraded to HTTPS
         - The prompt should describe what information you want to extract from the page
         - This tool is read-only and does not modify any files
         - Results may be summarized if the content is very large
         - Includes a self-cleaning 15-minute cache for faster responses when repeatedly accessing the same URL
         - When a URL redirects to a different host, the tool will inform you and provide the redirect URL in a special format. You should then make a new WebFetch request with the redirect URL to fetch the content.
         """)]
    public static async Task<string> WebFetch(
        [Description("The prompt to run on the fetched content")]
        string prompt,
        [Description("The URL to fetch content from")]
        string url)
    {
        var apiKey = GetTavilyApiKey();
        if (apiKey is null)
        {
            return Error("TAVILY_API_KEY environment variable is required for web access.");
        }

        var extractResult = await CallTavilyAsync(
            apiKey,
            "extract",
            new JsonObject
            {
                ["urls"] = url
            });

        if (!extractResult.Success)
        {
            return Error(extractResult.ErrorMessage ?? "Tavily extract request failed.");
        }

        var extracted = ExtractContentFromResponse(extractResult.Payload);
        
        if (string.IsNullOrWhiteSpace(extracted))
        {
            return Error("Tavily did not return any textual content for the requested URL.");
        }
        
        return JsonSerializer.Serialize(extractResult.Payload!["results"], JsonSerializerOptions.Web);
    }

    [McpServerTool(Name = "WebSearch"), KernelFunction("WebSearch"), Description(
         """
         - Allows Claude to search the web and use the results to inform responses
         - Provides up-to-date information for current events and recent data
         - Returns search result information formatted as search result blocks
         - Use this tool for accessing information beyond Claude's knowledge cutoff
         - Searches are performed automatically within a single API call

         Usage notes:
         - Domain filtering is supported to include or block specific websites
         - Web search is only available in the US
         - Account for "Today's date" in <env>. For example, if <env> says "Today's date: 2025-09-26", and the user wants the latest docs, do not use 2024 in the search query. Use 2025.

         """)]
    public static async Task<string> WebSearch(
        [Description("Only include search results from these domains")]
        string[]? allowed_domains,
        [Description("Never include search results from these domains")]
        string[]? blocked_domains,
        [Description("The search query to use")]
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("query must be provided.");
        }

        var apiKey = GetTavilyApiKey();
        if (apiKey is null)
        {
            return Error("TAVILY_API_KEY environment variable is required for web search.");
        }

        var payload = new JsonObject
        {
            ["query"] = query,
            ["search_depth"] = "advanced",
            ["max_results"] = DefaultMaxResults,
            ["include_answer"] = true,
            ["include_favicon"] = true
        };

        if (allowed_domains is { Length: > 0 })
        {
            payload["include_domains"] = JsonSerializer.SerializeToNode(allowed_domains, JsonOptions);
        }

        if (blocked_domains is { Length: > 0 })
        {
            payload["exclude_domains"] = JsonSerializer.SerializeToNode(blocked_domains, JsonOptions);
        }

        var searchResult = await CallTavilyAsync(apiKey, "search", payload);

        if (!searchResult.Success)
        {
            return Error(searchResult.ErrorMessage ?? "Tavily search request failed.");
        }

        var (answer, results) = ExtractSearchResults(searchResult.Payload);

        return JsonSerializer.Serialize(results, JsonSerializerOptions.Web);
    }

    private static bool TryNormalizeUrl(string? rawUrl, out string normalizedUrl, out string? error)
    {
        normalizedUrl = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = "url must be provided.";
            return false;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            error = "Provided url is not a valid absolute URI.";
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }


        normalizedUrl = uri.ToString();
        return true;
    }

    public static string? GetTavilyApiKey()
    {
        var key = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private static async Task<(bool Success, JsonNode? Payload, string? ErrorMessage)> CallTavilyAsync(
        string apiKey,
        string endpoint,
        JsonObject payload)
    {
        payload["api_key"] = apiKey;

        var requestContent = new StringContent(payload.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
        var url = $"{TavilyBaseUrl}/{endpoint}";

        try
        {
            using var response = await HttpClient.PostAsync(url, requestContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Tavily responded with {(int)response.StatusCode}: {responseBody}");
            }

            var json = JsonNode.Parse(responseBody, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (json is null)
            {
                return (false, null, "Unable to parse Tavily response.");
            }

            if (json["error"]?.GetValue<string>() is { } errorMessage && !string.IsNullOrWhiteSpace(errorMessage))
            {
                return (false, json, errorMessage);
            }

            return (true, json, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to contact Tavily: {ex.Message}");
        }
    }

    private static string ExtractContentFromResponse(JsonNode? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        static string? TryGetString(JsonNode? node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (node?[propertyName]?.GetValue<string>() is { } value && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        if (TryGetString(payload, "content", "markdown", "text") is { } content)
        {
            return content;
        }

        if (payload["data"] is JsonArray array)
        {
            var builder = new StringBuilder();
            foreach (var entry in array)
            {
                var text = entry?["content"]?.GetValue<string>() ?? entry?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text.Trim());
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }
        }

        return payload.ToString();
    }

    private static (string? Answer, List<SearchResult> Results) ExtractSearchResults(JsonNode? payload)
    {
        var answer = payload?["answer"]?.GetValue<string>();
        var results = new List<SearchResult>();

        if (payload?["results"] is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null)
                {
                    continue;
                }

                var title = item["title"]?.GetValue<string>();
                var url = item["url"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var snippet = item["content"]?.GetValue<string>();
                var published = item["published_date"]?.GetValue<string>();
                var score = item["score"]?.GetValue<double?>() ?? 0d;

                results.Add(new SearchResult(title.Trim(), url.Trim(), snippet?.Trim() ?? string.Empty,
                    published?.Trim() ?? string.Empty, score));
            }
        }

        return (answer?.Trim(), results);
    }

    private static string Error(string message)
    {
        var payload = new
        {
            status = "error",
            message
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private sealed record SearchResult(string Title, string Url, string Snippet, string Published, double Score);
}