using System.Text.Encodings.Web;

namespace Predictor.Predictions;

internal static class PredictionEndpoints
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

    public static IEndpointRouteBuilder MapPredictionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/predictions", Enqueue);
        app.MapGet("/api/predictions", ListCompleted);
        app.MapGet("/api/predictions/{requestId:guid}", GetById);
        return app;
    }

    private static IResult Enqueue(
        EnqueuePredictionRequest request,
        PredictionQueue queue,
        PredictionStore store,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Predictor.Predictions");
        Guid requestId;
        string requestIdText;

        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            requestId = Guid.NewGuid();
            requestIdText = requestId.ToString("D");
        }
        else if (Guid.TryParse(request.RequestId, out requestId))
        {
            requestIdText = requestId.ToString("D");
        }
        else
        {
            return Results.BadRequest(new
            {
                requestId = request.RequestId,
                status = "failure",
                error = "requestId must be a GUID."
            });
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new
                {
                    requestId = requestIdText,
                    status = "failure",
                    error = "text is required."
                });
            }

            if (store.ExistsCompleted(requestId) || queue.IsInFlight(requestId))
            {
                return Results.Conflict(new
                {
                    requestId = requestIdText,
                    status = "conflict",
                    error = "A prediction with this requestId already exists."
                });
            }

            var attempt = queue.TryEnqueue(new PredictionJob(requestId, request.Text));
            switch (attempt.Status)
            {
                case EnqueueStatus.Accepted:
                    store.ClearFailed(requestId);
                    logger.LogInformation("Prediction {RequestId} queued", requestId);
                    return Results.Ok(new { requestId = requestIdText, status = "created" });

                case EnqueueStatus.Duplicate:
                    return Results.Conflict(new
                    {
                        requestId = requestIdText,
                        status = "conflict",
                        error = attempt.Error
                    });

                default:
                    logger.LogError(
                        "Prediction {RequestId} enqueue failed: {Error}",
                        requestId,
                        attempt.Error);
                    return Results.Json(
                        new
                        {
                            requestId = requestIdText,
                            status = "failure",
                            error = attempt.Error ?? "Failed to enqueue prediction."
                        },
                        statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Prediction {RequestId} enqueue failed", requestId);
            return Results.Json(
                new { requestId = requestIdText, status = "failure", error = ex.Message },
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static IResult ListCompleted(PredictionStore store)
        => Results.Ok(store.ListCompleted());

    private static IResult GetById(Guid requestId, PredictionQueue queue, PredictionStore store)
    {
        var requestIdText = requestId.ToString("D");
        var completed = store.TryGetCompleted(requestId);
        if (completed is not null)
            return Results.Ok(completed);

        if (queue.IsInFlight(requestId))
        {
            return Results.Json(
                new { requestId = requestIdText, status = "queued" },
                statusCode: StatusCodes.Status202Accepted);
        }

        var failed = store.TryGetFailed(requestId);
        if (failed is not null)
        {
            return Results.Ok(new
            {
                requestId = requestIdText,
                createdAt = failed.CreatedAt,
                status = "failed",
                error = failed.Error
            });
        }

        return Results.NotFound(new { requestId = requestIdText, status = "not_found" });
    }
}
