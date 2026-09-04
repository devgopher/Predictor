using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Data;
using NewsBot.Logic.Data.Entities;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public interface IArticleEmbeddingService
{
    Task EnsureEmbeddingsAsync(IReadOnlyList<long> articleIds, CancellationToken token);
}

public class ArticleEmbeddingService : IArticleEmbeddingService
{
    private readonly NewsBotContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly NewsEmbeddingSettings _settings;
    private readonly ILogger<ArticleEmbeddingService> _logger;

    public ArticleEmbeddingService(
        NewsBotContext context,
        IEmbeddingService embeddingService,
        IOptions<NewsEmbeddingSettings> settings,
        ILogger<ArticleEmbeddingService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task EnsureEmbeddingsAsync(IReadOnlyList<long> articleIds, CancellationToken token)
    {
        if (!_embeddingService.IsAvailable || articleIds.Count == 0)
            return;

        var existing = await _context.ArticleEmbeddings
            .Where(e => articleIds.Contains(e.StoredNewsArticleId) && e.Model == _settings.Model)
            .Select(e => e.StoredNewsArticleId)
            .ToListAsync(token);

        var missingIds = articleIds.Except(existing).ToList();
        if (missingIds.Count == 0)
            return;

        var articles = await _context.StoredNewsArticles
            .Where(a => missingIds.Contains(a.Id))
            .ToListAsync(token);

        foreach (var article in articles)
        {
            var text = EmbeddingVectorHelper.BuildEmbeddingText(article.Title, article.Description, article.Content);
            var vector = await _embeddingService.EmbedAsync(text, token);
            if (vector == null || vector.Length == 0)
            {
                _logger.LogWarning("Failed to embed article {ArticleId}", article.Id);
                continue;
            }

            _context.ArticleEmbeddings.Add(new ArticleEmbedding
            {
                StoredNewsArticleId = article.Id,
                Model = _settings.Model,
                Vector = EmbeddingVectorHelper.ToBytes(vector),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(token);
    }
}
