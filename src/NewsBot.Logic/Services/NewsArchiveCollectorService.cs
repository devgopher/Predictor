using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Data;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NewsBot.Logic.Services;

public interface INewsArchiveCollectorService
{
    Task<ArchiveCollectionReport> CollectAsync(CancellationToken token);
}

public class ArchiveCollectionReport
{
    public int ArticlesCollected { get; set; }
    public int ArticlesSaved { get; set; }
    public int FailureCount { get; set; }
    public List<ArchiveFetchFailure> Failures { get; set; } = [];
}

public record ArchiveFetchFailure(
    string Provider,
    string Language,
    string Category,
    string Error,
    int? StatusCode = null);

public class NewsArchiveCollectorService : INewsArchiveCollectorService
{
    private readonly HttpClient _httpClient;
    private readonly INewsArchiveService _archiveService;
    private readonly INewsApiNewsService _newsApiNewsService;
    private readonly IRssNewsService _rssNewsService;
    private readonly NewsSettings _newsSettings;
    private readonly NewsArchiveCollectorSettings _collectorSettings;
    private readonly ILogger<NewsArchiveCollectorService> _logger;

    public NewsArchiveCollectorService(
        IHttpClientFactory httpClientFactory,
        INewsArchiveService archiveService,
        INewsApiNewsService newsApiNewsService,
        IRssNewsService rssNewsService,
        IOptions<NewsSettings> newsSettings,
        IOptions<NewsArchiveCollectorSettings> collectorSettings,
        ILogger<NewsArchiveCollectorService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(NewsArchiveCollectorService));
        _archiveService = archiveService;
        _newsApiNewsService = newsApiNewsService;
        _rssNewsService = rssNewsService;
        _newsSettings = newsSettings.Value;
        _collectorSettings = collectorSettings.Value;
        _logger = logger;
    }

    public async Task<ArchiveCollectionReport> CollectAsync(CancellationToken token)
    {
        var report = new ArchiveCollectionReport();
        var now = DateTime.UtcNow;
        var from = now.Date.AddDays(-_collectorSettings.LookbackDays);
        var to = now;

        _logger.LogInformation(
            "Archive collection started. Range: {From:yyyy-MM-dd} — {To:yyyy-MM-dd}, languages: {Languages}",
            from, to, string.Join(", ", _collectorSettings.Languages));

        var articles = new List<NewsArticle>();

        foreach (var language in _collectorSettings.Languages)
        {
            foreach (var category in _collectorSettings.Categories)
            {
                foreach (var provider in _newsSettings.ProviderOrder)
                {
                    var fetched = provider.ToLowerInvariant() switch
                    {
                        "gnews" => await FetchGNewsAsync(language, category, from, to, report, token),
                        "newsapi" => await FetchNewsApiAsync(language, category, from, to, report, token),
                        "rss" => await FetchRssAsync(language, category, from, to, report, token),
                        _ => []
                    };

                    articles.AddRange(fetched);
                }
            }
        }

        report.ArticlesCollected = articles.GroupBy(a => a.Url).Count();

        var deduped = articles
            .Where(a => IsInDateRange(a, from, to))
            .GroupBy(a => a.Url)
            .Select(g => g.First())
            .ToList();

        await _archiveService.ArchiveArticlesAsync(NewsArchiveConstants.GlobalUserId, deduped, token);
        report.ArticlesSaved = deduped.Count;

        _logger.LogInformation(
            "Archive collection finished. Collected: {Collected}, saved: {Saved}, failures: {Failures}",
            report.ArticlesCollected,
            report.ArticlesSaved,
            report.FailureCount);

        return report;
    }

    private async Task<List<NewsArticle>> FetchGNewsAsync(
        string language,
        string category,
        DateTime from,
        DateTime to,
        ArchiveCollectionReport report,
        CancellationToken token)
    {
        var cfg = _newsSettings.GNews;
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey))
            return [];

        try
        {
            var url = NewsProviderUrlBuilder.Build(cfg.BaseUrl, cfg.SearchEndpoint, new Dictionary<string, string?>
            {
                ["q"] = category,
                ["lang"] = language,
                ["max"] = _collectorSettings.MaxArticlesPerQuery.ToString(),
                ["from"] = from.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["to"] = to.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                [cfg.ApiKeyParameter] = cfg.ApiKey
            });

            using var response = await _httpClient.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(token);
                RegisterFailure(report, "GNews", language, category,
                    $"HTTP {(int)response.StatusCode}: {Truncate(body, 300)}", (int)response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<GNewsResponse>(token);
            return MapGNews(payload);
        }
        catch (Exception ex)
        {
            RegisterFailure(report, "GNews", language, category, ex.Message);
            return [];
        }
    }

    private async Task<List<NewsArticle>> FetchNewsApiAsync(
        string language,
        string category,
        DateTime from,
        DateTime to,
        ArchiveCollectionReport report,
        CancellationToken token)
    {
        if (!_newsApiNewsService.IsEnabled)
            return [];

        var result = await _newsApiNewsService.FetchByDateRangeAsync(
            category,
            language,
            from,
            to,
            _collectorSettings.MaxArticlesPerQuery,
            token);

        if (result.Error != null)
            RegisterFailure(report, "NewsApi", language, category, result.Error, result.StatusCode);

        return result.Articles.ToList();
    }

    private async Task<List<NewsArticle>> FetchRssAsync(
        string language,
        string category,
        DateTime from,
        DateTime to,
        ArchiveCollectionReport report,
        CancellationToken token)
    {
        if (!_rssNewsService.IsEnabled)
            return [];

        var result = await _rssNewsService.FetchByDateRangeAsync(
            category,
            language,
            from,
            to,
            _collectorSettings.MaxArticlesPerQuery,
            token);

        if (result.Error != null)
            RegisterFailure(report, "Rss", language, category, result.Error, result.StatusCode);

        return result.Articles.ToList();
    }

    private void RegisterFailure(
        ArchiveCollectionReport report,
        string provider,
        string language,
        string category,
        string error,
        int? statusCode = null)
    {
        report.FailureCount++;
        report.Failures.Add(new ArchiveFetchFailure(provider, language, category, error, statusCode));
        _logger.LogWarning(
            "Archive API refused: Provider={Provider}, Language={Language}, Category={Category}, Status={StatusCode}, Error={Error}",
            provider, language, category, statusCode, error);
    }

    private static bool IsInDateRange(NewsArticle article, DateTime from, DateTime to)
    {
        var date = article.PublishedAt ?? DateTime.UtcNow;
        return date >= from && date <= to;
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

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";

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
