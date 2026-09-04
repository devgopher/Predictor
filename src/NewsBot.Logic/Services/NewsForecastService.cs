using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Data;
using NewsBot.Logic.Data.Entities;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface INewsForecastService
{
    bool IsAvailable { get; }
    Task<string?> BuildForecastAsync(long telegramUserId, string question, string lang, CancellationToken token);
}

public class NewsForecastService : INewsForecastService
{
    private readonly INewsPredictorAiClient _aiClient;
    private readonly INewsArchiveService _archiveService;
    private readonly IForecastRetrievalService _retrievalService;
    private readonly INewsService _newsService;
    private readonly IUserSettingsService _userSettings;
    private readonly NewsPredictorAiSettings _aiSettings;
    private readonly ILogger<NewsForecastService> _logger;

    public NewsForecastService(
        INewsPredictorAiClient aiClient,
        INewsArchiveService archiveService,
        IForecastRetrievalService retrievalService,
        INewsService newsService,
        IUserSettingsService userSettings,
        IOptions<NewsPredictorAiSettings> aiSettings,
        ILogger<NewsForecastService> logger)
    {
        _aiClient = aiClient;
        _archiveService = archiveService;
        _retrievalService = retrievalService;
        _newsService = newsService;
        _userSettings = userSettings;
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public bool IsAvailable => _aiClient.IsAvailable;

    public async Task<string?> BuildForecastAsync(long telegramUserId, string question, string lang, CancellationToken token)
    {
        if (!IsAvailable) return null;

        if (await _archiveService.GetTotalCountAsync(telegramUserId, token) == 0)
            await RefreshNewsArchiveAsync(telegramUserId, token);

        var shortTerm = await _retrievalService.GetRelevantArticlesAsync(telegramUserId, NewsHorizon.Short, question, token);
        var mediumTerm = await _retrievalService.GetRelevantArticlesAsync(telegramUserId, NewsHorizon.Medium, question, token);
        var longTerm = await _retrievalService.GetRelevantArticlesAsync(telegramUserId, NewsHorizon.Long, question, token);

        if (shortTerm.Count == 0 && mediumTerm.Count == 0 && longTerm.Count == 0)
        {
            await RefreshNewsArchiveAsync(telegramUserId, token);
            shortTerm = await _retrievalService.GetRelevantArticlesAsync(telegramUserId, NewsHorizon.Short, question, token);
            mediumTerm = await _retrievalService.GetRelevantArticlesAsync(telegramUserId, NewsHorizon.Medium, question, token);
            longTerm = await _retrievalService.GetRelevantArticlesAsync(telegramUserId, NewsHorizon.Long, question, token);
        }

        if (shortTerm.Count == 0 && mediumTerm.Count == 0 && longTerm.Count == 0)
            return null;

        var userPrompt = BuildForecastPrompt(question, lang, shortTerm, mediumTerm, longTerm);
        var answer = await _aiClient.CompleteAsync(
            _aiSettings.Instruction,
            userPrompt,
            token);

        if (string.IsNullOrWhiteSpace(answer))
            _logger.LogWarning("Empty forecast for question: {Question}", question);

        return answer;
    }

    private async Task RefreshNewsArchiveAsync(long telegramUserId, CancellationToken token)
    {
        var settings = await _userSettings.GetOrCreateAsync(telegramUserId, token);
        var lang = string.IsNullOrWhiteSpace(settings.Language) ? "en" : settings.Language;
        var articles = await _newsService.FetchNewsAsync(
            lang,
            _userSettings.GetCategories(settings),
            _userSettings.GetKeywords(settings),
            _userSettings.GetTelegramChannels(settings),
            token);

        await _archiveService.ArchiveArticlesAsync(telegramUserId, articles, token);
        await _userSettings.SaveRecentNewsAsync(telegramUserId, articles, token);
    }

    private string BuildForecastPrompt(
        string question,
        string lang,
        IReadOnlyList<StoredNewsArticle> shortTerm,
        IReadOnlyList<StoredNewsArticle> mediumTerm,
        IReadOnlyList<StoredNewsArticle> longTerm)
    {
        return $"""
            Event / outcome to assess:
            {question}

            Analyze probability using ONLY the news excerpts below. Respond in language: {lang}.
            Do not use knowledge outside these excerpts.

            === {NewsHorizon.Short.GetLabel(lang)} ===
            {FormatStoredArticles(shortTerm)}

            === {NewsHorizon.Medium.GetLabel(lang)} ===
            {FormatStoredArticles(mediumTerm)}

            === {NewsHorizon.Long.GetLabel(lang)} ===
            {FormatStoredArticles(longTerm)}

            For each horizon:
            1) Estimate probability of the outcome (percentage), or state that data is insufficient.
            2) Brief reasoning citing specific news items above.

            Then provide a consolidated probability estimate.
            Cite source links when relevant.
            """;
    }

    private static string FormatStoredArticles(IReadOnlyList<StoredNewsArticle> articles)
    {
        if (articles.Count == 0) return "(no archived news for this period)";

        return string.Join("\n\n", articles.Select((a, i) =>
        {
            var date = (a.PublishedAt ?? a.FetchedAt).ToString("yyyy-MM-dd");
            var text = a.Content ?? a.Description ?? a.Title;
            return $"""
                [{i + 1}] {a.Title} ({date})
                Source: {a.Source ?? "unknown"}
                Link: {a.Url}
                Text: {Truncate(text, 600)}
                """;
        }));
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
