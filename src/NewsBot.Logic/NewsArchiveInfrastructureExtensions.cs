using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Data;
using NewsBot.Logic.Services;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic;

public static class NewsArchiveInfrastructureExtensions
{
    public static IServiceCollection AddNewsArchiveInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NewsSettings>(configuration.GetSection(NewsSettings.SectionName));
        services.Configure<NewsArchiveCollectorSettings>(configuration.GetSection(NewsArchiveCollectorSettings.SectionName));
        services.Configure<ForecastSettings>(configuration.GetSection(ForecastSettings.SectionName));
        services.Configure<NewsEmbeddingSettings>(configuration.GetSection(NewsEmbeddingSettings.SectionName));

        services.AddNewsBotDatabase(configuration);
        services.AddNewsProviders(configuration);
        services.AddHttpClient(nameof(NewsArchiveCollectorService));
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
        services.AddScoped<INewsArchiveCollectorService, NewsArchiveCollectorService>();
        services.AddScoped<IArticleEmbeddingService, ArticleEmbeddingService>();

        return services;
    }
}
