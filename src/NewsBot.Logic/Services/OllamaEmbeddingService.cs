using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface IEmbeddingService
{
    bool IsAvailable { get; }
    Task<float[]?> EmbedAsync(string text, CancellationToken token);
}

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly NewsEmbeddingSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        IOptions<NewsEmbeddingSettings> settings,
        HttpClient httpClient,
        ILogger<OllamaEmbeddingService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsAvailable => _settings.Enabled;

    public async Task<float[]?> EmbedAsync(string text, CancellationToken token)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var payload = new OllamaEmbeddingRequest
            {
                Model = _settings.Model,
                Prompt = text
            };

            using var response = await _httpClient.PostAsJsonAsync(_settings.Url, payload, token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Embedding request failed: HTTP {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(token);
            return result?.Embedding?.Count > 0 ? result.Embedding.ToArray() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding request failed");
            return null;
        }
    }

    private sealed class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public List<float>? Embedding { get; set; }
    }
}
