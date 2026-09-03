using System.Text.Encodings.Web;

namespace Predictor.Predictions;

internal static class PredictionServiceCollectionExtensions
{
    public static IServiceCollection AddPredictions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PredictionOptions>(configuration.GetSection(PredictionOptions.Section));
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new UtcIso8601DateTimeOffsetConverter());
            options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });
        services.AddSingleton<PredictionStore>();
        services.AddSingleton<PredictionQueue>();
        services.AddHostedService<PredictionWorker>();
        return services;
    }
}
