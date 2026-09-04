namespace NewsBot.Logic.Models;

public class NewsArticle
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Content { get; set; }
    public required string Url { get; init; }
    public string? Source { get; init; }
    public DateTime? PublishedAt { get; init; }
}
