using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface INewsApiNewsService
{
    bool IsEnabled { get; }
    Task<IReadOnlyList<NewsArticle>> FetchByCategoryAsync(string category, string language, int max, CancellationToken token);
    Task<IReadOnlyList<NewsArticle>> SearchAsync(string keyword, string language, int max, CancellationToken token);
    Task<NewsProviderFetchResult> FetchByDateRangeAsync(
        string category,
        string language,
        DateTime from,
        DateTime to,
        int max,
        CancellationToken token);
}

public class NewsApiNewsService : INewsApiNewsService
{
    private readonly HttpClient _httpClient;
    private readonly NewsApiProviderSettings _settings;
    private readonly ILogger<NewsApiNewsService> _logger;

    public NewsApiNewsService(
        HttpClient httpClient,
        IOptions<NewsSettings> settings,
        ILogger<NewsApiNewsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value.NewsApi;
        _logger = logger;
    }

    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public Task<IReadOnlyList<NewsArticle>> FetchByCategoryAsync(
        string category,
        string language,
        int max,
        CancellationToken token) =>
        FetchTopHeadlinesAsync(category, language, max, token);

    public Task<IReadOnlyList<NewsArticle>> SearchAsync(
        string keyword,
        string language,
        int max,
        CancellationToken token) =>
        FetchEverythingAsync(keyword, language, max, null, null, token);

    public async Task<NewsProviderFetchResult> FetchByDateRangeAsync(
        string category,
        string language,
        DateTime from,
        DateTime to,
        int max,
        CancellationToken token)
    {
        if (!IsEnabled)
            return NewsProviderFetchResult.Ok([]);

        try
        {
            var url = NewsProviderUrlBuilder.Build(_settings.BaseUrl, _settings.SearchEndpoint, new Dictionary<string, string?>
            {
                ["q"] = category,
                ["language"] = language,
                ["from"] = from.ToString("yyyy-MM-dd"),
                ["to"] = to.ToString("yyyy-MM-dd"),
                ["pageSize"] = max.ToString(),
                ["sortBy"] = "publishedAt",
                [_settings.ApiKeyParameter] = _settings.ApiKey
            });

            using var response = await _httpClient.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(token);
                return NewsProviderFetchResult.Fail(
                    $"HTTP {(int)response.StatusCode}: {Truncate(body, 300)}",
                    (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<NewsApiResponse>(token);
            return NewsProviderFetchResult.Ok(MapArticles(payload));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NewsAPI date-range fetch failed for {Category}/{Language}", category, language);
            return NewsProviderFetchResult.Fail(ex.Message);
        }
    }

    private async Task<IReadOnlyList<NewsArticle>> FetchTopHeadlinesAsync(
        string category,
        string language,
        int max,
        CancellationToken token)
    {
        if (!IsEnabled) return [];

        try
        {
            var url = NewsProviderUrlBuilder.Build(_settings.BaseUrl, _settings.TopHeadlinesEndpoint, new Dictionary<string, string?>
            {
                ["category"] = category,
                ["language"] = language,
                ["pageSize"] = max.ToString(),
                [_settings.ApiKeyParameter] = _settings.ApiKey
            });

            var response = await _httpClient.GetFromJsonAsync<NewsApiResponse>(url, token);
            return MapArticles(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NewsAPI top-headlines failed for {Category}", category);
            return [];
        }
    }

    private async Task<IReadOnlyList<NewsArticle>> FetchEverythingAsync(
        string keyword,
        string language,
        int max,
        DateTime? from,
        DateTime? to,
        CancellationToken token)
    {
        if (!IsEnabled) return [];

        try
        {
            var query = new Dictionary<string, string?>
            {
                ["q"] = keyword,
                ["language"] = language,
                ["pageSize"] = max.ToString(),
                ["sortBy"] = "publishedAt",
                [_settings.ApiKeyParameter] = _settings.ApiKey
            };

            if (from.HasValue)
                query["from"] = from.Value.ToString("yyyy-MM-dd");
            if (to.HasValue)
                query["to"] = to.Value.ToString("yyyy-MM-dd");

            var url = NewsProviderUrlBuilder.Build(_settings.BaseUrl, _settings.SearchEndpoint, query);
            var response = await _httpClient.GetFromJsonAsync<NewsApiResponse>(url, token);
            return MapArticles(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NewsAPI search failed for {Keyword}", keyword);
            return [];
        }
    }

    private static List<NewsArticle> MapArticles(NewsApiResponse? response) =>
        response?.Articles?.Select(a => new NewsArticle
        {
            Title = a.Title ?? string.Empty,
            Description = a.Description,
            Content = a.Content,
            Url = a.Url ?? string.Empty,
            Source = a.Source?.Name ?? "NewsAPI",
            PublishedAt = a.PublishedAt
        }).Where(a => !string.IsNullOrWhiteSpace(a.Url)).ToList() ?? [];

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";

    private sealed class NewsApiResponse
    {
        [JsonPropertyName("articles")]
        public List<NewsApiArticle>? Articles { get; set; }
    }

    private sealed class NewsApiArticle
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("source")]
        public NewsApiSource? Source { get; set; }
    }

    private sealed class NewsApiSource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
