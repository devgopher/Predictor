namespace Predictor.Ollama.Options;

public class OllamaAgentOptions
{
    public string BaseUrl { get; set; } = OllamaOptions.DefaultBaseUrl;
    public string BaseModel { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.2;
    public int NumCtx { get; set; } = 8192;
    public string SystemPrompt { get; set; } = string.Empty;
    public bool AllowUrlFetch { get; set; }
    public int UrlFetchMaxBytes { get; set; } = 65_536;
    public int UrlFetchTimeoutSeconds { get; set; } = 15;
    public bool AllowLocalhostUrlFetch { get; set; }

    public string ResolvedModel =>
        string.IsNullOrWhiteSpace(AgentName) ? BaseModel : AgentName;

    public string ResolvedBaseUrl
    {
        get
        {
            var url = string.IsNullOrWhiteSpace(BaseUrl) ? OllamaOptions.DefaultBaseUrl : BaseUrl.Trim();
            return url.TrimEnd('/');
        }
    }
}