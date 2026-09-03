namespace Predictor.Ollama.Options;

public class OllamaOptions
{
    public const string Section = "Ollama";
    public const string EmbeddingKey = "embedding";
    public const string ChatKey = "chat";
    public const string DefaultBaseUrl = "http://localhost:11434";

    public Dictionary<string, OllamaAgentOptions> Agents { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public OllamaAgentOptions Embedding => GetAgent(EmbeddingKey);

    public OllamaAgentOptions Chat => GetAgent(ChatKey);

    public OllamaAgentOptions GetAgent(string name)
    {
        if (Agents.TryGetValue(name, out var agent))
            return agent;

        foreach (var pair in Agents)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        var known = Agents.Count == 0 ? "(none)" : string.Join(", ", Agents.Keys);
        throw new InvalidOperationException(
            $"Ollama agent '{name}' is not configured. Add Ollama:Agents:{name} in appsettings.json. Known agents: {known}.");
    }
}