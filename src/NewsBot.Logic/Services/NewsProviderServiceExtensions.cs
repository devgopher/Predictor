using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Services;

public static class NewsProviderServiceExtensions
{
    public static IServiceCollection AddNewsProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NewsSettings>(configuration.GetSection(NewsSettings.SectionName));

        services.AddHttpClient<INewsApiNewsService, NewsApiNewsService>();
        services.AddHttpClient(nameof(RssNewsService));
        services.AddHttpClient($"{nameof(RssNewsService)}.Article")
            .ConfigureHttpClient((sp, client) =>
            {
                var rss = sp.GetRequiredService<IOptions<NewsSettings>>().Value.Rss;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, rss.ArticleFetchTimeoutSeconds));
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", rss.ResolveUserAgent());
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,ru;q=0.8");
            });

        services.AddScoped<IRssNewsService, RssNewsService>();

        return services;
    }
}
