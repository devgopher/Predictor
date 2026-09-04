using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface IAiClient
{
    bool IsAvailable { get; }
    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken token, int? maxTokens = null);
}

public interface INewsSummarizerAiClient : IAiClient;

public interface INewsPredictorAiClient : IAiClient;

public class AiClient<TSettings> : IAiClient where TSettings : AiProviderSettings
{
    private readonly TSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public AiClient(IOptions<TSettings> settings, HttpClient httpClient, ILogger<AiClient<TSettings>> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsAvailable =>
        _settings.Enabled && (!string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.AllowEmptyApiKey);

    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken token, int? maxTokens = null)
    {
        if (!IsAvailable) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            var payload = new ChatCompletionRequest
            {
                Model = _settings.Model,
                Temperature = _settings.Temperature,
                MaxTokens = maxTokens ?? _settings.MaxTokens,
                Messages =
                [
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = userPrompt }
                ]
            };

            request.Content = JsonContent.Create(payload);
            var response = await _httpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(token);
            return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI request failed ({SettingsType})", typeof(TSettings).Name);
            return null;
        }
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}

public class NewsSummarizerAiClient : AiClient<NewsSummarizerAiSettings>, INewsSummarizerAiClient
{
    public NewsSummarizerAiClient(
        IOptions<NewsSummarizerAiSettings> settings,
        HttpClient httpClient,
        ILogger<AiClient<NewsSummarizerAiSettings>> logger)
        : base(settings, httpClient, logger)
    {
    }
}

public class NewsPredictorAiClient : AiClient<NewsPredictorAiSettings>, INewsPredictorAiClient
{
    public NewsPredictorAiClient(
        IOptions<NewsPredictorAiSettings> settings,
        HttpClient httpClient,
        ILogger<AiClient<NewsPredictorAiSettings>> logger)
        : base(settings, httpClient, logger)
    {
    }
}
