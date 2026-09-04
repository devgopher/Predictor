namespace NewsBot.Logic.Settings;

public class AiProviderSettings
{
    public bool Enabled { get; set; }
    public bool AllowEmptyApiKey { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Url { get; set; } = "https://api.deepseek.com/v1/chat/completions";
    public string Model { get; set; } = "deepseek-chat";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public string Instruction { get; set; } = string.Empty;
}

/// <summary>AI for shortening and retelling news (/news formatting, /ask).</summary>
public class NewsSummarizerAiSettings : AiProviderSettings
{
    public const string SectionName = "NewsSummarizerAi";

    public string AskInstruction { get; set; } =
        "You answer user questions about recently loaded news. Use only the provided news context. Be concise and cite source links when relevant.";

    public NewsSummarizerAiSettings()
    {
        Instruction =
            "You retell news from the given text. Retell the incoming news within the specified length limit and keep the source link provided in the original message.";
    }
}

/// <summary>AI for probability forecasts (/forecast).</summary>
public class NewsPredictorAiSettings : AiProviderSettings
{
    public const string SectionName = "NewsPredictorAi";

    public NewsPredictorAiSettings()
    {
        MaxTokens = 4000;
        Instruction =
            "You estimate the probability of event outcomes based ONLY on the news excerpts provided below. " +
            "Analyze short-term (last week), medium-term (1-6 months), and long-term (6 months to 5 years) news separately, " +
            "then give a consolidated probability in percent. If news is insufficient for a horizon, say so and do NOT invent percentages. " +
            "Cite source links from the provided news. Never use outside knowledge.";
    }
}

public class NewsSettings
{
    public const string SectionName = "NewsSettings";

    public int MaxArticlesPerRequest { get; set; } = 5;

    /// <summary>Provider names in fallback order, e.g. GNews, NewsApi, Rss.</summary>
    public List<string> ProviderOrder { get; set; } = ["GNews", "NewsApi", "Rss"];

    public GNewsProviderSettings GNews { get; set; } = new();
    public NewsApiProviderSettings NewsApi { get; set; } = new();
    public RssProviderSettings Rss { get; set; } = new();
}

public class GNewsProviderSettings
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://gnews.io/api/v4";
    public string ApiKey { get; set; } = string.Empty;
    public string TopHeadlinesEndpoint { get; set; } = "top-headlines";
    public string SearchEndpoint { get; set; } = "search";
    public string ApiKeyParameter { get; set; } = "apikey";
}

public class NewsApiProviderSettings
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://newsapi.org/v2";
    public string ApiKey { get; set; } = string.Empty;
    public string TopHeadlinesEndpoint { get; set; } = "top-headlines";
    public string SearchEndpoint { get; set; } = "everything";
    public string ApiKeyParameter { get; set; } = "apiKey";
}

public class RssProviderSettings
{
    public bool Enabled { get; set; } = true;
    public bool FetchArticleContent { get; set; } = true;
    public int MaxArticleContentLength { get; set; } = 8000;
    public int ArticleFetchTimeoutSeconds { get; set; } = 20;

    /// <summary>Direct User-Agent string. Used when UserAgentProfile is empty or not found.</summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>Key from UserAgents, e.g. Chrome131.</summary>
    public string UserAgentProfile { get; set; } = "Chrome131";

    public Dictionary<string, string> UserAgents { get; set; } = new()
    {
        ["Chrome131"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        ["Chrome124"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        ["Chrome120"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public Dictionary<string, Dictionary<string, string>> Feeds { get; set; } = new();
}

public class ForecastSettings
{
    public const string SectionName = "Forecast";

    public int ShortTermDays { get; set; } = 7;
    public int MediumTermMaxMonths { get; set; } = 6;
    public int LongTermMinMonths { get; set; } = 6;
    public int LongTermMaxYears { get; set; } = 5;
    public int MaxArticlesPerHorizon { get; set; } = 10;

    /// <summary>Max articles loaded per horizon before ranking (FTS / embeddings).</summary>
    public int RetrievalCandidatePool { get; set; } = 150;

    public bool UseFullTextSearch { get; set; } = true;
    public bool UseEmbeddings { get; set; } = true;
}

public class UserDbSettings
{
    public const string SectionName = "UserDb";

    public string ConnectionString { get; set; } = "Data Source=newsbot_users.db";
}

public class NewsArchiveCollectorSettings
{
    public const string SectionName = "NewsArchiveCollector";

    public bool Enabled { get; set; } = true;
    public int PollIntervalHours { get; set; } = 2;
    public int LookbackDays { get; set; } = 30;
    public int MaxArticlesPerQuery { get; set; } = 20;
    public List<string> Languages { get; set; } = ["en", "ru"];
    public List<string> Categories { get; set; } =
    [
        "general", "world", "politics", "business", "technology", "science", "health", "sports", "entertainment"
    ];
}
