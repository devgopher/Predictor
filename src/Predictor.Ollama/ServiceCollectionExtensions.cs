using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Predictor.Ollama.Options;

namespace Predictor.Ollama;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPredictorOllama(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.Section));
        services.AddHttpClient(OllamaClient.OllamaHttpClientName, client =>
        {
            // First chat after a model load on Windows can take several minutes.
            client.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddHttpClient(UrlContentFetcher.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Predictor/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false
        });
        services.AddSingleton<UrlContentFetcher>();
        services.AddSingleton<IOllamaClient, OllamaClient>();
        return services;
    }
}
