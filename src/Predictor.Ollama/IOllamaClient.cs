namespace Predictor.Ollama;

public interface IOllamaClient
{
    Task<float[]?> EmbedAsync(string text, CancellationToken token = default);

    Task<OllamaChatResult> ChatAsync(string userPrompt, string? systemPrompt = null, CancellationToken token = default);

    Task<OllamaChatResult> WriteConclusionsAsync(string context, string? question = null, CancellationToken token = default);
}
