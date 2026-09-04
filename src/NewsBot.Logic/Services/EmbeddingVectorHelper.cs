using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NewsBot.Logic.Services;

internal static class EmbeddingVectorHelper
{
    public static byte[] ToBytes(IReadOnlyList<float> vector)
    {
        var array = vector as float[] ?? vector.ToArray();
        return MemoryMarshal.AsBytes(array.AsSpan()).ToArray();
    }

    public static float[] FromBytes(byte[] data)
    {
        if (data.Length == 0) return [];
        return MemoryMarshal.Cast<byte, float>(data).ToArray();
    }

    public static float CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count == 0 || b.Count == 0 || a.Count != b.Count)
            return 0f;

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0f;

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    public static string BuildEmbeddingText(string title, string? description, string? content)
    {
        var parts = new List<string> { title };
        if (!string.IsNullOrWhiteSpace(description))
            parts.Add(description);
        if (!string.IsNullOrWhiteSpace(content))
            parts.Add(content.Length <= 1500 ? content : content[..1500]);

        return string.Join("\n\n", parts);
    }
}

internal static partial class FtsQueryBuilder
{
    public static string? BuildMatchQuery(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var terms = TermRegex().Matches(question.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .Select(EscapeTerm)
            .Where(t => t.Length > 0)
            .ToList();

        if (terms.Count == 0)
            return null;

        return string.Join(" OR ", terms);
    }

    private static string EscapeTerm(string term) =>
        term.Replace("\"", "\"\"", StringComparison.Ordinal);

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex TermRegex();
}
