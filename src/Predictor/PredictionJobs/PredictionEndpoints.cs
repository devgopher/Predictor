namespace Predictor.PredictionJobs;

public static class PredictionServiceCollectionExtensions
{
    public static IServiceCollection AddPredictions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PredictionOptions>(configuration.GetSection(PredictionOptions.Section));
        services.AddSingleton<PredictionStore>();
        services.AddSingleton<PredictionQueue>();
        services.AddHostedService<PredictionWorker>();
        return services;
    }
}
