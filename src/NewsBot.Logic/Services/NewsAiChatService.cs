using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface INewsAiChatService
{
    bool IsAvailable { get; }
    Task<string?> AskAboutNewsAsync(IReadOnlyList<NewsArticle> articles, string question, CancellationToken token);
}

public class NewsAiChatService : INewsAiChatService
{
    private readonly INewsSummarizerAiClient _aiClient;
    private readonly NewsSummarizerAiSettings _settings;
    private readonly ILogger<NewsAiChatService> _logger;

    public NewsAiChatService(
        INewsSummarizerAiClient aiClient,
        IOptions<NewsSummarizerAiSettings> settings,
        ILogger<NewsAiChatService> logger)
    {
        _aiClient = aiClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsAvailable => _aiClient.IsAvailable;

    public async Task<string?> AskAboutNewsAsync(
        IReadOnlyList<NewsArticle> articles,
        string question,
        CancellationToken token)
    {
        if (!IsAvailable) return null;

        var context = BuildNewsContext(articles);
        var userPrompt = $"""
            Recent news loaded by the user:

            {context}

            User question:
            {question}

            Answer the question based on the news above. Cite source links when relevant. If the news do not contain enough information, say so clearly.
            """;

        var answer = await _aiClient.CompleteAsync(_settings.AskInstruction, userPrompt, token);
        if (string.IsNullOrWhiteSpace(answer))
            _logger.LogWarning("Empty AI answer for question: {Question}", question);

        return answer;
    }

    internal static string BuildNewsContext(IReadOnlyList<NewsArticle> articles)
    {
        if (articles.Count == 0) return "(no news loaded)";

        return string.Join("\n\n", articles.Select((a, i) =>
        {
            var text = a.Content ?? a.Description ?? a.Title;
            return $"""
                [{i + 1}] {a.Title}
                Source: {a.Source ?? "unknown"}
                Link: {a.Url}
                Text: {text}
                """;
        }));
    }
}
