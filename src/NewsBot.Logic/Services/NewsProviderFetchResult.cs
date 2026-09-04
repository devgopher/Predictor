using NewsBot.Logic.Models;

namespace NewsBot.Logic.Services;

public sealed class NewsProviderFetchResult
{
    public IReadOnlyList<NewsArticle> Articles { get; init; } = [];
    public string? Error { get; init; }
    public int? StatusCode { get; init; }

    public static NewsProviderFetchResult Ok(IReadOnlyList<NewsArticle> articles) =>
        new() { Articles = articles };

    public static NewsProviderFetchResult Fail(string error, int? statusCode = null) =>
        new() { Error = error, StatusCode = statusCode };
}
