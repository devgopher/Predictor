using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Predictor.Ollama;
using Predictor.Ollama.Options;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddPredictorOllama(builder.Configuration);

using var host = builder.Build();

var settings = host.Services.GetRequiredService<IOptions<OllamaOptions>>().Value;
var ollama = host.Services.GetRequiredService<IOllamaClient>();

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("Predictor Ollama sample");
foreach (var (name, agent) in settings.Agents)
    Console.WriteLine($"  {name,-10}: {agent.ResolvedModel} @ {agent.ResolvedBaseUrl}");
Console.WriteLine();

const string facts =
    """
    https://en.wikipedia.org/wiki/2026_United_States_House_of_Representatives_elections
    """;

Console.WriteLine("=== 1. Embedding ===");
var embedding = await ollama.EmbedAsync(facts);
if (embedding is null || embedding.Length == 0)
{
    Console.Error.WriteLine("Embedding failed. Deploy the agent first:");
    Console.Error.WriteLine("  powershell -File scripts/deploy-embedding-agent.ps1");
    return 1;
}


Console.WriteLine("=== 2. Chat / conclusions ===");
try
{
    var conclusions = await ollama.WriteConclusionsAsync(
        facts,
        "Вероятность победы демократов в конгрессе?");
    Console.WriteLine(conclusions);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("If the Ollama runner died: run `ollama ps`, check VRAM, then restart Ollama and retry.");
    return 1;
}

Console.WriteLine();
Console.WriteLine("Done.");
return 0;
