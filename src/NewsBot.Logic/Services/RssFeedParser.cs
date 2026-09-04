using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NewsBot.Logic.Models;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

internal static partial class RssFeedParser
{
    public static List<NewsArticle> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";
        var items = doc.Descendants("item").Any()
            ? doc.Descendants("item")
            : doc.Descendants(atom + "entry");

        return items.Select(item =>
        {
            var title = item.Element("title")?.Value ?? item.Element(atom + "title")?.Value ?? "Untitled";
            var link = item.Element("link")?.Value
                       ?? item.Element("link")?.Attribute("href")?.Value
                       ?? item.Element(atom + "link")?.Attribute("href")?.Value
                       ?? string.Empty;
            var description = item.Element("description")?.Value
                              ?? item.Element(atom + "summary")?.Value
                              ?? item.Element("content")?.Value
                              ?? item.Element(atom + "content")?.Value;
            var pubDate = item.Element("pubDate")?.Value
                          ?? item.Element(atom + "published")?.Value
                          ?? item.Element(atom + "updated")?.Value;

            DateTime? published = null;
            if (pubDate != null && DateTime.TryParse(pubDate, out var dt))
                published = dt.ToUniversalTime();

            return new NewsArticle
            {
                Title = StripHtml(title),
                Description = StripHtml(description ?? string.Empty),
                Url = link.Trim(),
                Source = "RSS",
                PublishedAt = published
            };
        }).Where(a => !string.IsNullOrWhiteSpace(a.Url)).ToList();
    }

    public static string? ResolveFeedUrl(RssProviderSettings settings, string category, string language) =>
        ResolveFeedUrl(settings.Feeds, category, language);

    public static string? ResolveFeedUrl(
        Dictionary<string, Dictionary<string, string>> feeds,
        string category,
        string language)
    {
        if (feeds.TryGetValue(language, out var langFeeds) &&
            langFeeds.TryGetValue(category, out var url))
            return url;

        if (feeds.TryGetValue("en", out var enFeeds) &&
            enFeeds.TryGetValue(category, out var enUrl))
            return enUrl;

        return feeds.GetValueOrDefault("en")?.GetValueOrDefault("general");
    }

    private static string StripHtml(string input) =>
        HtmlTagRegex().Replace(input, string.Empty).Trim();

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();
}

internal static partial class ArticleContentExtractor
{
    public static string ExtractText(string html, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        html = ScriptTagRegex().Replace(html, string.Empty);
        html = StyleTagRegex().Replace(html, string.Empty);

        var articleMatch = ArticleTagRegex().Match(html);
        var mainMatch = MainTagRegex().Match(html);
        var chunk = articleMatch.Success ? articleMatch.Value
            : mainMatch.Success ? mainMatch.Value
            : html;

        var text = HtmlTagRegex().Replace(chunk, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex().Replace(text, " ").Trim();

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength].TrimEnd() + "…";
    }

    [GeneratedRegex("<script[\\s\\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex("<style[\\s\\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleTagRegex();

    [GeneratedRegex("<article[\\s\\S]*?</article>", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleTagRegex();

    [GeneratedRegex("<main[\\s\\S]*?</main>", RegexOptions.IgnoreCase)]
    private static partial Regex MainTagRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}

internal static class NewsProviderUrlBuilder
{
    public static string Build(string baseUrl, string endpoint, Dictionary<string, string?> query)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedEndpoint = endpoint.TrimStart('/');
        var queryString = string.Join('&',
            query.Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

        return $"{trimmedBase}/{trimmedEndpoint}?{queryString}";
    }
}
