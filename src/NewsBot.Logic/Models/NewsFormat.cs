namespace NewsBot.Logic.Models;

public enum NewsFormat
{
    Full1000,
    Medium500_1000,
    Short50_100,
    Tiny50
}

public static class NewsFormatExtensions
{
    public static (int Min, int Max) GetCharRange(this NewsFormat format) => format switch
    {
        NewsFormat.Full1000 => (0, 1000),
        NewsFormat.Medium500_1000 => (500, 1000),
        NewsFormat.Short50_100 => (50, 100),
        NewsFormat.Tiny50 => (40, 60),
        _ => (0, 1000)
    };

    public static string GetAiHint(this NewsFormat format) => format switch
    {
        NewsFormat.Full1000 => "Summarize in up to 1000 characters.",
        NewsFormat.Medium500_1000 => "Summarize in 500 to 1000 characters.",
        NewsFormat.Short50_100 => "Summarize in 50 to 100 characters.",
        NewsFormat.Tiny50 => "Summarize in approximately 50 characters.",
        _ => "Summarize concisely."
    };

    public static bool TryParse(string? value, out NewsFormat format)
    {
        format = NewsFormat.Medium500_1000;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value.ToLowerInvariant() switch
        {
            "full1000" or "full" or "1000" => Set(NewsFormat.Full1000, out format),
            "medium500_1000" or "medium" or "500" => Set(NewsFormat.Medium500_1000, out format),
            "short50_100" or "short" or "100" => Set(NewsFormat.Short50_100, out format),
            "tiny50" or "tiny" or "50" => Set(NewsFormat.Tiny50, out format),
            _ => false
        };
    }

    private static bool Set(NewsFormat value, out NewsFormat format)
    {
        format = value;
        return true;
    }
}
