using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Predictor.Ollama;
using Predictor.Ollama.Options;
using Predictor.PredictionJobs;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddPredictorOllama(builder.Configuration);
builder.Services.AddPredictions(builder.Configuration);

using var host = builder.Build();

var settings = host.Services.GetRequiredService<IOptions<OllamaOptions>>().Value;
var ollama = host.Services.GetRequiredService<IOllamaClient>();
var queue = host.Services.GetRequiredService<PredictionQueue>();
var store = host.Services.GetRequiredService<PredictionStore>();

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("Predictor Ollama sample");
foreach (var (name, agent) in settings.Agents)
    Console.WriteLine($"  {name,-10}: {agent.ResolvedModel} @ {agent.ResolvedBaseUrl}");
Console.WriteLine();

const string facts =
    """
    https://en.wikipedia.org/wiki/2026_United_States_House_of_Representatives_elections
    """;
const string question = "Вероятность победы демократов в конгрессе?";

Console.WriteLine("=== 1. Embedding ===");
var embedding = await ollama.EmbedAsync(facts);
if (embedding is null || embedding.Length == 0)
{
    Console.Error.WriteLine("Embedding failed. Deploy the agent first:");
    Console.Error.WriteLine("  powershell -File scripts/deploy-embedding-agent.ps1");
    return 1;
}

Console.WriteLine($"Embedding ok ({embedding.Length} dims)");
Console.WriteLine();

Console.WriteLine("=== 2. Prediction report ===");
await host.StartAsync();

var requestId = Guid.NewGuid();
var enqueue = queue.TryEnqueue(new PredictionJob(requestId, facts, question));
if (enqueue.Status != EnqueueStatus.Accepted)
{
    Console.Error.WriteLine(enqueue.Error ?? "Failed to enqueue prediction.");
    await host.StopAsync();
    return 1;
}

Console.WriteLine($"Queued {requestId:D}");
Console.WriteLine($"Waiting for report in {store.RootPath} ...");

try
{
    while (true)
    {
        var completed = store.TryGetCompleted(requestId);
        if (completed is not null)
        {
            var path = store.GetCompletedPath(requestId);
            Console.WriteLine();
            Console.WriteLine($"Report: {path}");
            if (!string.IsNullOrWhiteSpace(completed.Thinking))
                Console.WriteLine($"Thinking chars: {completed.Thinking.Length}");
            Console.WriteLine($"Text chars: {completed.Text.Length}");
            await host.StopAsync();
            return 0;
        }

        var failed = store.TryGetFailed(requestId);
        if (failed is not null)
        {
            Console.Error.WriteLine(failed.Error);
            Console.Error.WriteLine("If the Ollama runner died: run `ollama ps`, check VRAM, then restart Ollama and retry.");
            await host.StopAsync();
            return 1;
        }

        if (!queue.IsInFlight(requestId))
        {
            // Worker may have just finished writing; one more pass above on next iteration
            // is unnecessary — re-check files once more before failing.
            completed = store.TryGetCompleted(requestId);
            if (completed is not null)
                continue;

            failed = store.TryGetFailed(requestId);
            if (failed is not null)
                continue;

            Console.Error.WriteLine("Prediction left the queue without a completed or failed report.");
            await host.StopAsync();
            return 1;
        }

        await Task.Delay(500);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    await host.StopAsync();
    return 1;
}
