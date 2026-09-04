using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface IRssNewsService
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

public class RssNewsService : IRssNewsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RssProviderSettings _settings;
    private readonly ILogger<RssNewsService> _logger;

    public RssNewsService(
        IHttpClientFactory httpClientFactory,
        IOptions<NewsSettings> settings,
        ILogger<RssNewsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value.Rss;
        _logger = logger;
    }

    public bool IsEnabled => _settings.Enabled;

    public Task<IReadOnlyList<NewsArticle>> FetchByCategoryAsync(
        string category,
        string language,
        int max,
        CancellationToken token) =>
        FetchInternalAsync(category, language, max, null, null, token);

    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(
        string keyword,
        string language,
        int max,
        CancellationToken token)
    {
        var articles = await FetchInternalAsync("general", language, max * 3, null, null, token);
        return articles
            .Where(a =>
                a.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (a.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Content?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(max)
            .ToList();
    }

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

        var feedUrl = RssFeedParser.ResolveFeedUrl(_settings, category, language);
        if (feedUrl == null)
            return NewsProviderFetchResult.Fail($"RSS feed not configured for {language}/{category}");

        try
        {
            var feedClient = _httpClientFactory.CreateClient(nameof(RssNewsService));
            var xml = await feedClient.GetStringAsync(feedUrl, token);
            var articles = RssFeedParser.Parse(xml)
                .Where(a => IsInDateRange(a, from, to))
                .Take(max)
                .ToList();

            await EnrichWithArticleContentAsync(articles, token);
            return NewsProviderFetchResult.Ok(articles);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RSS fetch failed: {Url}", feedUrl);
            return NewsProviderFetchResult.Fail($"{feedUrl}: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<NewsArticle>> FetchInternalAsync(
        string category,
        string language,
        int max,
        DateTime? from,
        DateTime? to,
        CancellationToken token)
    {
        if (!IsEnabled) return [];

        var feedUrl = RssFeedParser.ResolveFeedUrl(_settings, category, language);
        if (feedUrl == null) return [];

        try
        {
            var feedClient = _httpClientFactory.CreateClient(nameof(RssNewsService));
            var xml = await feedClient.GetStringAsync(feedUrl, token);
            var articles = RssFeedParser.Parse(xml);

            if (from.HasValue && to.HasValue)
                articles = articles.Where(a => IsInDateRange(a, from.Value, to.Value)).ToList();

            articles = articles.Take(max).ToList();
            await EnrichWithArticleContentAsync(articles, token);
            return articles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RSS fetch failed: {Url}", feedUrl);
            return [];
        }
    }

    private async Task EnrichWithArticleContentAsync(List<NewsArticle> articles, CancellationToken token)
    {
        if (!_settings.FetchArticleContent || articles.Count == 0)
            return;

        var articleClient = _httpClientFactory.CreateClient($"{nameof(RssNewsService)}.Article");

        foreach (var article in articles)
        {
            if (string.IsNullOrWhiteSpace(article.Url))
                continue;

            try
            {
                using var response = await articleClient.GetAsync(article.Url, token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("RSS article page HTTP {Status} for {Url}", (int)response.StatusCode, article.Url);
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(token);
                var content = ArticleContentExtractor.ExtractText(html, _settings.MaxArticleContentLength);
                if (!string.IsNullOrWhiteSpace(content))
                    article.Content = content;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "RSS article page fetch failed for {Url}", article.Url);
            }
        }
    }

    private static bool IsInDateRange(NewsArticle article, DateTime from, DateTime to)
    {
        var date = article.PublishedAt ?? DateTime.UtcNow;
        return date >= from && date <= to;
    }
}
