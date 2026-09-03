using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Predictor.Ollama.Options;

namespace Predictor.Ollama;

public class OllamaClient(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> options,
    UrlContentFetcher urlFetcher,
    ILogger<OllamaClient> logger)
    : IOllamaClient
{
    private const int MaxToolRounds = 6;
    private const int MaxTransientTries = 3;
    public const string OllamaHttpClientName = "Ollama";

    private static readonly TimeSpan[] TransientRetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];

    private static readonly Regex ThinkBlock = new(
        @"<think>[\s\S]*?</think>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly object[] FetchUrlTools =
    [
        new
        {
            type = "function",
            function = new
            {
                name = UrlContentFetcher.ToolName,
                description =
                    "Fetch text content from an HTTP or HTTPS URL. Use when the user provided a link and the page data is needed to answer.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new { type = "string", description = "Absolute http or https URL" },
                        maxBytes = new { type = "integer", description = "Optional response size limit in bytes" }
                    },
                    required = new[] { "url" }
                }
            }
        }
    ];

    private readonly OllamaOptions _options = options.Value;

    public async Task<float[]?> EmbedAsync(string text, CancellationToken token = default)
    {
        var agent = _options.Embedding;
        var client = CreateOllamaClient();
        var model = agent.ResolvedModel;
        var payload = new { model, input = text, keep_alive = 0 };
        using var response = await client.PostAsJsonAsync(ApiUrl(agent, "api/embed"), payload, token);
        if (!response.IsSuccessStatusCode)
        {
            var legacy = new { model, prompt = text, keep_alive = 0 };
            using var fallback = await client.PostAsJsonAsync(ApiUrl(agent, "api/embeddings"), legacy, token);
            if (!fallback.IsSuccessStatusCode)
            {
                logger.LogWarning("Ollama embeddings failed at {BaseUrl}: {Status}", agent.ResolvedBaseUrl, fallback.StatusCode);
                return null;
            }

            var legacyDoc = await fallback.Content.ReadFromJsonAsync<LegacyEmbeddingResponse>(token);
            return legacyDoc?.Embedding;
        }

        var modern = await response.Content.ReadFromJsonAsync<EmbedResponse>(token);
        return modern?.Embeddings?.FirstOrDefault();
    }

    public async Task<string> ChatAsync(string userPrompt, string? systemPrompt = null, CancellationToken token = default)
    {
        var agent = _options.Chat;
        var system = string.IsNullOrWhiteSpace(systemPrompt)
            ? agent.SystemPrompt
            : systemPrompt;

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(system))
            messages.Add(new { role = "system", content = system });
        messages.Add(new { role = "user", content = userPrompt });

        return await CompleteChatAsync(agent, messages, token);
    }

    public Task<string> WriteConclusionsAsync(string context, string? question = null, CancellationToken token = default)
    {
        var userPrompt = string.IsNullOrWhiteSpace(question)
            ? $"Напиши выводы по следующим данным:\n\n{context}"
            : $"Вопрос: {question}\n\nДанные:\n{context}\n\nНапиши выводы.";

        return ChatAsync(userPrompt, _options.Chat.SystemPrompt, token);
    }

    private async Task<string> CompleteChatAsync(
        OllamaAgentOptions agent,
        List<object> messages,
        CancellationToken token)
    {
        await UnloadEmbeddingIfNeededAsync(agent, token);

        var client = CreateOllamaClient();
        var chatUrl = ApiUrl(agent, "api/chat");
        var includeThink = true;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var payload = BuildChatPayload(agent, messages, includeThink);
            var raw = await PostChatAsync(client, chatUrl, payload, agent, token);
            if (!payload.ContainsKey("think"))
                includeThink = false;
            using var doc = JsonDocument.Parse(raw);
            var message = doc.RootElement.GetProperty("message");
            if (agent.AllowUrlFetch && TryGetToolCalls(message, out var toolCalls))
            {
                messages.Add(JsonSerializer.Deserialize<JsonElement>(message.GetRawText()));
                foreach (var call in toolCalls.EnumerateArray())
                {
                    var toolName = GetToolName(call) ?? UrlContentFetcher.ToolName;
                    var result = await ExecuteToolAsync(toolName, GetToolArguments(call), agent, token);
                    messages.Add(new
                    {
                        role = "tool",
                        content = TruncateToolResult(result, agent.NumCtx),
                        tool_name = toolName
                    });
                }

                continue;
            }

            var content = message.TryGetProperty("content", out var contentEl)
                ? contentEl.GetString() ?? string.Empty
                : string.Empty;
            return StripThink(content);
        }

        throw new InvalidOperationException("Ollama chat exceeded the tool-call round limit.");
    }

    private static Dictionary<string, object?> BuildChatPayload(
        OllamaAgentOptions agent,
        List<object> messages,
        bool includeThink)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = agent.ResolvedModel,
            ["stream"] = false,
            ["options"] = new
            {
                temperature = agent.Temperature,
                num_ctx = agent.NumCtx
            },
            ["messages"] = messages
        };
        if (includeThink)
            payload["think"] = false;
        if (agent.AllowUrlFetch)
            payload["tools"] = FetchUrlTools;
        return payload;
    }

    private async Task<string> PostChatAsync(
        HttpClient client,
        string chatUrl,
        Dictionary<string, object?> payload,
        OllamaAgentOptions agent,
        CancellationToken token)
    {
        Exception? lastException = null;
        HttpStatusCode? lastStatus = null;
        var lastBody = string.Empty;

        for (var attempt = 1; attempt <= MaxTransientTries; attempt++)
        {
            try
            {
                using var response = await client.PostAsJsonAsync(chatUrl, payload, token);
                var raw = await response.Content.ReadAsStringAsync(token);
                if (response.IsSuccessStatusCode)
                    return raw;

                lastStatus = response.StatusCode;
                lastBody = raw;

                if (ShouldOmitThink(response.StatusCode, raw) && payload.Remove("think"))
                {
                    logger.LogWarning(
                        "Ollama rejected think=false at {BaseUrl}; retrying without the think field.",
                        agent.ResolvedBaseUrl);
                    attempt--;
                    continue;
                }

                if (!IsTransientChatFailure(response.StatusCode, raw, ex: null) || attempt == MaxTransientTries)
                    break;

                logger.LogWarning(
                    "Transient Ollama chat failure ({Attempt}/{Max}) at {BaseUrl}: {Status} {Body}",
                    attempt, MaxTransientTries, agent.ResolvedBaseUrl, response.StatusCode, raw);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientChatFailure(null, ex.Message, ex))
            {
                lastException = ex;
                if (attempt == MaxTransientTries)
                    break;

                logger.LogWarning(
                    ex,
                    "Transient Ollama chat disconnect ({Attempt}/{Max}) at {BaseUrl}",
                    attempt, MaxTransientTries, agent.ResolvedBaseUrl);
            }

            var delayIndex = Math.Min(attempt - 1, TransientRetryDelays.Length - 1);
            await Task.Delay(TransientRetryDelays[delayIndex], token);
        }

        logger.LogError(
            lastException,
            "Ollama chat failed at {BaseUrl}: {Status} {Body}",
            agent.ResolvedBaseUrl, lastStatus, lastBody);
        throw new InvalidOperationException(FormatChatFailure(lastStatus, lastBody, lastException));
    }

    private async Task UnloadEmbeddingIfNeededAsync(OllamaAgentOptions chatAgent, CancellationToken token)
    {
        OllamaAgentOptions embedding;
        try
        {
            embedding = _options.Embedding;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (string.Equals(embedding.ResolvedModel, chatAgent.ResolvedModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(embedding.ResolvedBaseUrl, chatAgent.ResolvedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var client = CreateOllamaClient();
            if (!await IsModelLoadedAsync(client, embedding, token))
                return;

            // keep_alive 0 unloads after the call. Use /api/embed so we do not
            // accidentally treat a BERT embed model as a chat/generate model.
            var payload = new { model = embedding.ResolvedModel, input = ".", keep_alive = 0 };
            using var response = await client.PostAsJsonAsync(ApiUrl(embedding, "api/embed"), payload, token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug(
                    "Unload of embedding model {Model} returned {Status}",
                    embedding.ResolvedModel, response.StatusCode);
                return;
            }

            logger.LogInformation("Unloaded embedding model {Model} before chat to free VRAM.", embedding.ResolvedModel);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not unload embedding model {Model} before chat.", embedding.ResolvedModel);
        }
    }

    private static async Task<bool> IsModelLoadedAsync(
        HttpClient client,
        OllamaAgentOptions agent,
        CancellationToken token)
    {
        using var response = await client.GetAsync(ApiUrl(agent, "api/ps"), token);
        if (!response.IsSuccessStatusCode)
            return false;

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (!doc.RootElement.TryGetProperty("models", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var loaded in models.EnumerateArray())
        {
            var name = loaded.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var model = loaded.TryGetProperty("model", out var modelEl) ? modelEl.GetString() : null;
            if (ModelNamesMatch(agent.ResolvedModel, name) || ModelNamesMatch(agent.ResolvedModel, model))
                return true;
        }

        return false;
    }

    private static bool ModelNamesMatch(string requested, string? loaded)
    {
        if (string.IsNullOrWhiteSpace(loaded))
            return false;
        if (loaded.Equals(requested, StringComparison.OrdinalIgnoreCase))
            return true;

        static string BaseName(string value)
        {
            var slash = value.LastIndexOf('/');
            if (slash >= 0)
                value = value[(slash + 1)..];
            var colon = value.LastIndexOf(':');
            return colon >= 0 ? value[..colon] : value;
        }

        return BaseName(requested).Equals(BaseName(loaded), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldOmitThink(HttpStatusCode status, string body) =>
        status == HttpStatusCode.BadRequest &&
        body.Contains("think", StringComparison.OrdinalIgnoreCase);

    private static bool IsTransientChatFailure(HttpStatusCode? status, string? body, Exception? ex)
    {
        if (ex is OperationCanceledException)
            return false;

        if (ex is HttpRequestException or IOException)
            return true;

        if (status is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
            return false;

        if (status is HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or null)
        {
            if (string.IsNullOrWhiteSpace(body))
                return status is HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout;

            return ContainsTransientDisconnect(body);
        }

        return ContainsTransientDisconnect(body);
    }

    private static bool ContainsTransientDisconnect(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.Contains("wsarecv", StringComparison.OrdinalIgnoreCase)
            || text.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connection was aborted", StringComparison.OrdinalIgnoreCase)
            || text.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
            || text.Contains("read tcp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("write tcp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ECONNRESET", StringComparison.OrdinalIgnoreCase)
            || text.Contains("existing connection", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatChatFailure(HttpStatusCode? status, string body, Exception? ex)
    {
        var statusText = status?.ToString() ?? ex?.GetType().Name ?? "unknown";
        var detail = !string.IsNullOrWhiteSpace(body)
            ? body.Trim()
            : ex?.Message ?? "no response body";
        var runnerDied = ContainsTransientDisconnect(detail) || ContainsTransientDisconnect(ex?.Message);

        if (runnerDied)
        {
            return
                $"Ollama chat error: {statusText}. The Ollama model runner likely crashed or ran out of GPU/RAM " +
                $"(connection reset). Check `ollama ps`, free VRAM, and restart Ollama if needed. Details: {detail}";
        }

        return $"Ollama chat error: {statusText}. Details: {detail}";
    }

    private static string TruncateToolResult(string result, int numCtx)
    {
        var ctx = numCtx > 0 ? numCtx : 8192;
        var maxChars = Math.Clamp(ctx * 2, 4000, 12_000);
        if (result.Length <= maxChars)
            return result;

        return result[..maxChars] + "\n[truncated]";
    }

    private async Task<string> ExecuteToolAsync(
        string toolName,
        JsonElement arguments,
        OllamaAgentOptions agent,
        CancellationToken token)
    {
        if (!string.Equals(toolName, UrlContentFetcher.ToolName, StringComparison.OrdinalIgnoreCase))
            return $"Error: unknown tool '{toolName}'.";

        var url = ReadString(arguments, "url");
        var maxBytes = ReadInt(arguments, "maxBytes");
        logger.LogInformation("Tool {Tool} url={Url}", toolName, url);
        return await urlFetcher.FetchAsync(url, maxBytes, agent, token);
    }

    private static bool TryGetToolCalls(JsonElement message, out JsonElement toolCalls)
    {
        if (message.TryGetProperty("tool_calls", out toolCalls) &&
            toolCalls.ValueKind == JsonValueKind.Array &&
            toolCalls.GetArrayLength() > 0)
        {
            return true;
        }

        toolCalls = default;
        return false;
    }

    private static string? GetToolName(JsonElement call)
    {
        if (call.TryGetProperty("function", out var function) &&
            function.TryGetProperty("name", out var name) &&
            name.ValueKind == JsonValueKind.String)
        {
            return name.GetString();
        }

        if (call.TryGetProperty("name", out var direct) && direct.ValueKind == JsonValueKind.String)
            return direct.GetString();

        return null;
    }

    private static JsonElement GetToolArguments(JsonElement call)
    {
        if (call.TryGetProperty("function", out var function) &&
            function.TryGetProperty("arguments", out var nested))
        {
            return NormalizeArguments(nested);
        }

        if (call.TryGetProperty("arguments", out var direct))
            return NormalizeArguments(direct);

        return default;
    }

    private static JsonElement NormalizeArguments(JsonElement arguments)
    {
        if (arguments.ValueKind == JsonValueKind.String)
        {
            var text = arguments.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return default;

            using var parsed = JsonDocument.Parse(text);
            return parsed.RootElement.Clone();
        }

        return arguments.ValueKind == JsonValueKind.Object ? arguments.Clone() : default;
    }

    private static string? ReadString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static string StripThink(string content)
    {
        if (content.Contains("</think>", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("<think>", StringComparison.OrdinalIgnoreCase))
        {
            content = $"<think>{content}";
        }

        return ThinkBlock.Replace(content, string.Empty).Trim();
    }

    private HttpClient CreateOllamaClient() =>
        httpClientFactory.CreateClient(OllamaHttpClientName);

    private static string ApiUrl(OllamaAgentOptions agent, string path) =>
        $"{agent.ResolvedBaseUrl}/{path.TrimStart('/')}";

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }

    private sealed class LegacyEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
