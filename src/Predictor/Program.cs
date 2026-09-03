using Microsoft.Extensions.Options;
using Predictor.Ollama;
using Predictor.Ollama.Options;
using Predictor.Predictions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPredictorOllama(builder.Configuration);
builder.Services.AddPredictions(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", (IOptions<OllamaOptions> ollama) => Results.Ok(new
{
    status = "ok",
    ollama = new
    {
        embedding = new
        {
            baseUrl = ollama.Value.Embedding.ResolvedBaseUrl,
            model = ollama.Value.Embedding.ResolvedModel
        },
        chat = new
        {
            baseUrl = ollama.Value.Chat.ResolvedBaseUrl,
            model = ollama.Value.Chat.ResolvedModel,
            allowUrlFetch = ollama.Value.Chat.AllowUrlFetch
        }
    }
}));

app.MapPost("/api/embed", async (EmbedRequest request, IOllamaClient ollama, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "text is required" });

    var embedding = await ollama.EmbedAsync(request.Text, token);
    return embedding is null
        ? Results.StatusCode(StatusCodes.Status502BadGateway)
        : Results.Ok(new { dimensions = embedding.Length, embedding });
});

app.MapPost("/api/chat", async (ChatRequest? request, IOllamaClient ollama, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "message is required" });

    if (request != null)
    {
        var reply = await ollama.ChatAsync(request.Message, $"Today {DateTime.UtcNow.Date} UTC. {request.SystemPrompt}", token);
        return Results.Ok(new { reply });
    }
    
    return Results.StatusCode(StatusCodes.Status500InternalServerError);
});

app.MapPost("/api/conclusions", async (ConclusionsRequest request, IOllamaClient ollama, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.Context))
        return Results.BadRequest(new { error = "context is required" });

    var conclusions = await ollama.WriteConclusionsAsync(request.Context, request.Question, token);
    return Results.Ok(new { conclusions });
});

app.MapPredictionEndpoints();

app.Run();