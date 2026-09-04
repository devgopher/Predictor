using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NewsBot.Logic.Services;

public interface INewsService
{
    Task<IReadOnlyList<NewsArticle>> FetchNewsAsync(
        string language,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> keywords,
        IReadOnlyList<string> telegramChannels,
        CancellationToken token);
}

public class NewsService : INewsService
{
    private readonly HttpClient _httpClient;
    private readonly ITelegramChannelReader _telegramChannelReader;
    private readonly INewsApiNewsService _newsApiNewsService;
    private readonly IRssNewsService _rssNewsService;
    private readonly NewsSettings _settings;
    private readonly ILogger<NewsService> _logger;

    public NewsService(
        HttpClient httpClient,
        ITelegramChannelReader telegramChannelReader,
        INewsApiNewsService newsApiNewsService,
        IRssNewsService rssNewsService,
        IOptions<NewsSettings> settings,
        ILogger<NewsService> logger)
    {
        _httpClient = httpClient;
        _telegramChannelReader = telegramChannelReader;
        _newsApiNewsService = newsApiNewsService;
        _rssNewsService = rssNewsService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsArticle>> FetchNewsAsync(
        string language,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> keywords,
        IReadOnlyList<string> telegramChannels,
        CancellationToken token)
    {
        var articles = new List<NewsArticle>();
        var max = _settings.MaxArticlesPerRequest;

        if (telegramChannels.Count > 0)
        {
            foreach (var channel in telegramChannels.Take(5))
            {
                var posts = await _telegramChannelReader.FetchChannelPostsAsync(channel, max, token);
                if (keywords.Count > 0)
                {
                    posts = posts.Where(p =>
                        keywords.Any(k =>
                            p.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                            (p.Description?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)))
                        .ToList();
                }
                articles.AddRange(posts);
                if (articles.Count >= max) break;
            }
        }

        if (articles.Count < max)
        {
            if (keywords.Count > 0)
            {
                foreach (var keyword in keywords.Take(3))
                {
                    articles.AddRange(await SearchByKeywordAsync(keyword, language, max, token));
                    if (articles.Count >= max) break;
                }
            }
            else if (telegramChannels.Count == 0)
            {
                var cats = categories.Count > 0 ? categories : ["general"];
                foreach (var category in cats.Take(3))
                {
                    articles.AddRange(await FetchByCategoryAsync(category, language, max, token));
                    if (articles.Count >= max) break;
                }
            }
        }

        return articles
            .GroupBy(a => a.Url)
            .Select(g => g.First())
            .Take(max)
            .ToList();
    }

    private async Task<IReadOnlyList<NewsArticle>> FetchByCategoryAsync(
        string category,
        string language,
        int max,
        CancellationToken token)
    {
        foreach (var provider in _settings.ProviderOrder)
        {
            var articles = provider.ToLowerInvariant() switch
            {
                "gnews" => await TryGNewsTopAsync(category, language, max, token),
                "newsapi" => _newsApiNewsService.IsEnabled
                    ? await _newsApiNewsService.FetchByCategoryAsync(category, language, max, token)
                    : [],
                "rss" => _rssNewsService.IsEnabled
                    ? await _rssNewsService.FetchByCategoryAsync(category, language, max, token)
                    : [],
                _ => []
            };

            if (articles.Count > 0) return articles;
        }

        return [];
    }

    private async Task<IReadOnlyList<NewsArticle>> SearchByKeywordAsync(
        string keyword,
        string language,
        int max,
        CancellationToken token)
    {
        foreach (var provider in _settings.ProviderOrder)
        {
            var articles = provider.ToLowerInvariant() switch
            {
                "gnews" => await TryGNewsSearchAsync(keyword, language, max, token),
                "newsapi" => _newsApiNewsService.IsEnabled
                    ? await _newsApiNewsService.SearchAsync(keyword, language, max, token)
                    : [],
                "rss" => _rssNewsService.IsEnabled
                    ? await _rssNewsService.SearchAsync(keyword, language, max, token)
                    : [],
                _ => []
            };

            if (articles.Count > 0) return articles;
        }

        return [];
    }

    private async Task<List<NewsArticle>> TryGNewsTopAsync(string category, string language, int max, CancellationToken token)
    {
        var cfg = _settings.GNews;
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey)) return [];

        try
        {
            var url = NewsProviderUrlBuilder.Build(cfg.BaseUrl, cfg.TopHeadlinesEndpoint, new Dictionary<string, string?>
            {
                ["category"] = category,
                ["lang"] = language,
                ["max"] = max.ToString(),
                [cfg.ApiKeyParameter] = cfg.ApiKey
            });

            var response = await _httpClient.GetFromJsonAsync<GNewsResponse>(url, token);
            return MapGNews(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GNews top-headlines failed for {Category}", category);
            return [];
        }
    }

    private async Task<List<NewsArticle>> TryGNewsSearchAsync(string keyword, string language, int max, CancellationToken token)
    {
        var cfg = _settings.GNews;
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey)) return [];

        try
        {
            var url = NewsProviderUrlBuilder.Build(cfg.BaseUrl, cfg.SearchEndpoint, new Dictionary<string, string?>
            {
                ["q"] = keyword,
                ["lang"] = language,
                ["max"] = max.ToString(),
                [cfg.ApiKeyParameter] = cfg.ApiKey
            });

            var response = await _httpClient.GetFromJsonAsync<GNewsResponse>(url, token);
            return MapGNews(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GNews search failed for {Keyword}", keyword);
            return [];
        }
    }

    private static List<NewsArticle> MapGNews(GNewsResponse? response) =>
        response?.Articles?.Select(a => new NewsArticle
        {
            Title = a.Title ?? string.Empty,
            Description = a.Description,
            Content = a.Content,
            Url = a.Url ?? string.Empty,
            Source = a.Source?.Name ?? "GNews",
            PublishedAt = a.PublishedAt
        }).Where(a => !string.IsNullOrWhiteSpace(a.Url)).ToList() ?? [];

    private sealed class GNewsResponse
    {
        [JsonPropertyName("articles")]
        public List<GNewsArticle>? Articles { get; set; }
    }

    private sealed class GNewsArticle
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
        public GNewsSource? Source { get; set; }
    }

    private sealed class GNewsSource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
