using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Models;

namespace NewsBot.Logic.Services;

public interface ITelegramChannelReader
{
    Task<IReadOnlyList<NewsArticle>> FetchChannelPostsAsync(string channelInput, int maxPosts, CancellationToken token);
}

public class TelegramChannelReader : ITelegramChannelReader
{
    private static readonly Regex MessageBlockRegex = new(
        @"data-post=""([^""]+)""[\s\S]*?<div class=""tgme_widget_message_text js-message_text""[^>]*>([\s\S]*?)</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex BrRegex = new("<br\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramChannelReader> _logger;

    public TelegramChannelReader(HttpClient httpClient, ILogger<TelegramChannelReader> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsArticle>> FetchChannelPostsAsync(
        string channelInput,
        int maxPosts,
        CancellationToken token)
    {
        var username = TelegramChannelUtils.NormalizeUsername(channelInput);
        if (username == null)
        {
            _logger.LogWarning("Invalid Telegram channel input: {Input}", channelInput);
            return [];
        }

        try
        {
            var url = $"https://t.me/s/{username}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await _httpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(token);
            return ParsePosts(html, username, maxPosts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Telegram channel {Channel}", username);
            return [];
        }
    }

    internal static IReadOnlyList<NewsArticle> ParsePosts(string html, string username, int maxPosts)
    {
        var articles = new List<NewsArticle>();
        var matches = MessageBlockRegex.Matches(html);

        foreach (Match match in matches)
        {
            if (articles.Count >= maxPosts) break;

            var dataPost = match.Groups[1].Value;
            var rawHtml = match.Groups[2].Value;
            var text = StripHtml(rawHtml).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var title = text.Length > 120 ? text[..120].TrimEnd() + "…" : text;
            var postUrl = $"https://t.me/{dataPost}";

            articles.Add(new NewsArticle
            {
                Title = title,
                Description = text,
                Content = text,
                Url = postUrl,
                Source = $"Telegram @{username}",
                PublishedAt = null
            });
        }

        articles.Reverse();
        return articles.Take(maxPosts).ToList();
    }

    private static string StripHtml(string html)
    {
        var withBreaks = BrRegex.Replace(html, "\n");
        var withoutTags = HtmlTagRegex.Replace(withBreaks, string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Replace('\u00a0', ' ').Trim();
    }
}

public static class TelegramChannelUtils
{
    public static string? NormalizeUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var value = input.Trim();

        if (value.StartsWith('@'))
            return CleanUsername(value[1..]);

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return null;

            var first = segments[0];
            if (first.Equals("s", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
                return CleanUsername(segments[1]);

            return CleanUsername(first);
        }

        if (value.Contains("t.me/", StringComparison.OrdinalIgnoreCase))
        {
            var idx = value.IndexOf("t.me/", StringComparison.OrdinalIgnoreCase);
            var path = value[(idx + 5)..].TrimEnd('/');
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            if (parts[0].Equals("s", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
                return CleanUsername(parts[1]);
            return CleanUsername(parts[0]);
        }

        return CleanUsername(value);
    }

    private static string? CleanUsername(string value)
    {
        var username = value.Split('/')[0].Trim();
        if (string.IsNullOrWhiteSpace(username)) return null;
        if (username.Equals("joinchat", StringComparison.OrdinalIgnoreCase)) return null;
        if (username.StartsWith('+')) return null;
        return username;
    }

    public static string ToDisplayUrl(string username) => $"https://t.me/{username}";
}
