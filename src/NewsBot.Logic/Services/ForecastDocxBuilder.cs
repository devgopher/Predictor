using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.RegularExpressions;

namespace NewsBot.Logic.Services;

public static class ForecastDocxBuilder
{
    private static readonly HashSet<char> InvalidFileNameChars =
        new(Path.GetInvalidFileNameChars());

    public static string BuildFileName(string question, string lang)
    {
        var prefix = lang.ToLowerInvariant() switch
        {
            "ru" => "ответ_на_",
            "de" => "antwort_auf_",
            "fr" => "reponse_a_",
            "es" => "respuesta_a_",
            _ => "answer_to_"
        };

        var slug = Slugify(question);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "forecast";

        return $"{prefix}{slug}.docx";
    }

    public static byte[] BuildDocument(string question, string answer, string lang)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            body.AppendChild(CreateHeadingParagraph(question));
            body.AppendChild(CreateMetaParagraph(lang));
            body.AppendChild(CreateSpacerParagraph());

            foreach (var paragraph in SplitParagraphs(answer))
                body.AppendChild(CreateBodyParagraph(paragraph));
        }

        return stream.ToArray();
    }

    private static string Slugify(string text)
    {
        var normalized = text.ToLowerInvariant().Trim();
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
            {
                AppendUnderscore(builder);
                continue;
            }

            if (InvalidFileNameChars.Contains(ch) || ch is '.' or ',' or '?' or '!' or ':' or ';' or '"' or '\'' or '(' or ')')
                continue;

            builder.Append(ch);
        }

        var slug = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
        if (slug.Length > 80)
            slug = slug[..80].TrimEnd('_');

        return slug;
    }

    private static void AppendUnderscore(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '_')
            builder.Append('_');
    }

    private static Paragraph CreateHeadingParagraph(string text) =>
        new(new Run(new RunProperties(new Bold()), new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Paragraph CreateMetaParagraph(string lang) =>
        new(new Run(new Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Language: {lang}")
        {
            Space = SpaceProcessingModeValues.Preserve
        }));

    private static Paragraph CreateSpacerParagraph() => new(new Run(new Text(string.Empty)));

    private static Paragraph CreateBodyParagraph(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield return string.Empty;

        foreach (var part in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part;
    }
}
