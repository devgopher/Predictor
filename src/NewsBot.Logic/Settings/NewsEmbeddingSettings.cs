namespace NewsBot.Logic.Settings;

public class NewsEmbeddingSettings
{
    public const string SectionName = "NewsEmbedding";

    public bool Enabled { get; set; }
    public string Url { get; set; } = "http://localhost:11434/api/embeddings";
    public string Model { get; set; } = "embeddinggemma";
}
