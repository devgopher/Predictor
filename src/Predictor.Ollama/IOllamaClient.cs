namespace Predictor.Ollama;

public interface IOllamaClient
{
    Task<float[]?> EmbedAsync(string text, CancellationToken token = default);

    Task<string> ChatAsync(string userPrompt, string? systemPrompt = null, CancellationToken token = default);

    Task<string> WriteConclusionsAsync(string context, string? question = null, CancellationToken token = default);
}
