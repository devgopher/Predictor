using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Data;
using NewsBot.Logic.Data.Entities;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface IForecastRetrievalService
{
    Task<IReadOnlyList<StoredNewsArticle>> GetRelevantArticlesAsync(
        long telegramUserId,
        NewsHorizon horizon,
        string question,
        CancellationToken token);
}

public class ForecastRetrievalService : IForecastRetrievalService
{
    private readonly NewsBotContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IArticleEmbeddingService _articleEmbeddingService;
    private readonly ForecastSettings _forecastSettings;
    private readonly NewsEmbeddingSettings _embeddingSettings;
    private readonly ILogger<ForecastRetrievalService> _logger;

    public ForecastRetrievalService(
        NewsBotContext context,
        IEmbeddingService embeddingService,
        IArticleEmbeddingService articleEmbeddingService,
        IOptions<ForecastSettings> forecastSettings,
        IOptions<NewsEmbeddingSettings> embeddingSettings,
        ILogger<ForecastRetrievalService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _articleEmbeddingService = articleEmbeddingService;
        _forecastSettings = forecastSettings.Value;
        _embeddingSettings = embeddingSettings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoredNewsArticle>> GetRelevantArticlesAsync(
        long telegramUserId,
        NewsHorizon horizon,
        string question,
        CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var (from, to) = horizon.GetDateRange(_forecastSettings, now);
        var max = _forecastSettings.MaxArticlesPerHorizon;
        var poolSize = _forecastSettings.RetrievalCandidatePool;

        var candidates = await _context.StoredNewsArticles
            .AsNoTracking()
            .Where(a => a.TelegramUserId == telegramUserId || a.TelegramUserId == NewsArchiveConstants.GlobalUserId)
            .Where(a => (a.PublishedAt ?? a.FetchedAt) >= from && (a.PublishedAt ?? a.FetchedAt) <= to)
            .OrderByDescending(a => a.PublishedAt ?? a.FetchedAt)
            .Take(poolSize)
            .ToListAsync(token);

        if (candidates.Count == 0)
            return [];

        if (string.IsNullOrWhiteSpace(question))
            return candidates.Take(max).ToList();

        var ftsScores = _forecastSettings.UseFullTextSearch
            ? await GetFtsScoresAsync(candidates, question, token)
            : new Dictionary<long, double>();

        Dictionary<long, float>? semanticScores = null;
        if (_forecastSettings.UseEmbeddings && _embeddingService.IsAvailable)
        {
            await _articleEmbeddingService.EnsureEmbeddingsAsync(candidates.Select(c => c.Id).ToList(), token);
            semanticScores = await GetSemanticScoresAsync(question, candidates, token);
        }

        var keywordScores = GetKeywordScores(candidates, question);

        var ranked = candidates
            .Select(a =>
            {
                ftsScores.TryGetValue(a.Id, out var fts);
                keywordScores.TryGetValue(a.Id, out var keyword);

                var semantic = 0f;
                if (semanticScores != null)
                    semanticScores.TryGetValue(a.Id, out semantic);

                var score = Math.Max(keyword * 0.45, Math.Max(fts * 0.75, semantic * 1.0));
                return (Article: a, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Article.PublishedAt ?? x.Article.FetchedAt)
            .Take(max)
            .Select(x => x.Article)
            .ToList();

        if (ranked.Count > 0)
            return ranked;

        _logger.LogDebug(
            "No ranked articles for horizon {Horizon}, falling back to latest {Count}",
            horizon,
            max);

        return candidates.Take(max).ToList();
    }

    private async Task<Dictionary<long, float>> GetSemanticScoresAsync(
        string question,
        IReadOnlyList<StoredNewsArticle> candidates,
        CancellationToken token)
    {
        var result = new Dictionary<long, float>();
        var queryVector = await _embeddingService.EmbedAsync(question, token);
        if (queryVector == null || queryVector.Length == 0)
            return result;

        var ids = candidates.Select(c => c.Id).ToList();
        var embeddings = await _context.ArticleEmbeddings
            .AsNoTracking()
            .Where(e => ids.Contains(e.StoredNewsArticleId) && e.Model == _embeddingSettings.Model)
            .ToListAsync(token);

        foreach (var embedding in embeddings)
        {
            var vector = EmbeddingVectorHelper.FromBytes(embedding.Vector);
            var score = EmbeddingVectorHelper.CosineSimilarity(queryVector, vector);
            if (score > 0)
                result[embedding.StoredNewsArticleId] = score;
        }

        return result;
    }

    private async Task<Dictionary<long, double>> GetFtsScoresAsync(
        IReadOnlyList<StoredNewsArticle> candidates,
        string question,
        CancellationToken token)
    {
        var matchQuery = FtsQueryBuilder.BuildMatchQuery(question);
        if (string.IsNullOrWhiteSpace(matchQuery))
            return new Dictionary<long, double>();

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var result = new Dictionary<long, double>();

        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(token);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT fts.rowid, bm25(StoredNewsArticles_fts) AS rank
                FROM StoredNewsArticles_fts fts
                WHERE StoredNewsArticles_fts MATCH $query
                ORDER BY rank
                LIMIT $limit
                """;
            command.Parameters.Add(new SqliteParameter("$query", matchQuery));
            command.Parameters.Add(new SqliteParameter("$limit", _forecastSettings.RetrievalCandidatePool));

            await using var reader = await command.ExecuteReaderAsync(token);
            var rank = 0;
            while (await reader.ReadAsync(token))
            {
                var id = reader.GetInt64(0);
                if (!candidateIds.Contains(id))
                    continue;

                rank++;
                result[id] = 1.0 / rank;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTS search failed for query: {Query}", matchQuery);
        }

        return result;
    }

    private static Dictionary<long, double> GetKeywordScores(IReadOnlyList<StoredNewsArticle> candidates, string question)
    {
        var terms = question.Split([' ', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        var result = new Dictionary<long, double>();
        if (terms.Count == 0)
            return result;

        foreach (var article in candidates)
        {
            var haystack = $"{article.Title}\n{article.Description}\n{article.Content}";
            var hits = terms.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (hits > 0)
                result[article.Id] = (double)hits / terms.Count;
        }

        return result;
    }
}
