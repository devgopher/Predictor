using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface INewsFormatterService
{
    Task<string> FormatArticleAsync(NewsArticle article, NewsFormat format, CancellationToken token);
}

public class NewsFormatterService : INewsFormatterService
{
    private readonly INewsSummarizerAiClient _aiClient;
    private readonly NewsSummarizerAiSettings _aiSettings;
    private readonly ILogger<NewsFormatterService> _logger;

    public NewsFormatterService(
        INewsSummarizerAiClient aiClient,
        IOptions<NewsSummarizerAiSettings> aiSettings,
        ILogger<NewsFormatterService> logger)
    {
        _aiClient = aiClient;
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public async Task<string> FormatArticleAsync(NewsArticle article, NewsFormat format, CancellationToken token)
    {
        if (!_aiClient.IsAvailable)
            return FormatHeadlineOnly(article);

        try
        {
            var sourceText = BuildSourceText(article);
            var userPrompt = $"""
                {format.GetAiHint()}
                Source link (must be included at the end): {article.Url}

                News text:
                {sourceText}
                """;

            var summary = await _aiClient.CompleteAsync(_aiSettings.Instruction, userPrompt, token);
            if (string.IsNullOrWhiteSpace(summary))
                return FormatHeadlineOnly(article);

            if (!summary.Contains(article.Url, StringComparison.OrdinalIgnoreCase))
                summary = $"{summary.Trim()}\n\n🔗 {article.Url}";

            return TruncateToRange(summary, format);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI formatting failed, falling back to headline for {Title}", article.Title);
            return FormatHeadlineOnly(article);
        }
    }

    private static string BuildSourceText(NewsArticle article)
    {
        var parts = new List<string> { article.Title };
        if (!string.IsNullOrWhiteSpace(article.Description))
            parts.Add(article.Description);
        if (!string.IsNullOrWhiteSpace(article.Content))
            parts.Add(article.Content);
        return string.Join("\n\n", parts);
    }

    private static string FormatHeadlineOnly(NewsArticle article) =>
        $"📰 {article.Title}\n🔗 {article.Url}";

    private static string TruncateToRange(string text, NewsFormat format)
    {
        var (_, max) = format.GetCharRange();
        if (text.Length <= max) return text;
        return text[..max].TrimEnd() + "…";
    }
}

public static class NewsServiceExtensions
{
    public static IServiceCollection AddNewsBotServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NewsSettings>(configuration.GetSection(NewsSettings.SectionName));
        services.Configure<NewsSummarizerAiSettings>(configuration.GetSection(NewsSummarizerAiSettings.SectionName));
        services.Configure<NewsPredictorAiSettings>(configuration.GetSection(NewsPredictorAiSettings.SectionName));
        services.Configure<ForecastSettings>(configuration.GetSection(ForecastSettings.SectionName));
        services.Configure<NewsEmbeddingSettings>(configuration.GetSection(NewsEmbeddingSettings.SectionName));

        services.AddNewsProviders(configuration);
        services.AddHttpClient<INewsService, NewsService>();
        services.AddHttpClient<ITelegramChannelReader, TelegramChannelReader>();
        services.AddHttpClient<INewsSummarizerAiClient, NewsSummarizerAiClient>();
        services.AddHttpClient<INewsPredictorAiClient, NewsPredictorAiClient>();
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
        services.AddSingleton<INewsAiChatService, NewsAiChatService>();
        services.AddScoped<INewsForecastService, NewsForecastService>();
        services.AddScoped<IForecastRetrievalService, ForecastRetrievalService>();
        services.AddScoped<IArticleEmbeddingService, ArticleEmbeddingService>();
        services.AddSingleton<INewsFormatterService, NewsFormatterService>();

        return services;
    }
}
