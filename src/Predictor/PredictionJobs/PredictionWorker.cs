using Predictor.Ollama;

namespace Predictor.Predictions;

internal sealed class PredictionWorker : BackgroundService
{
    private readonly PredictionQueue _queue;
    private readonly PredictionStore _store;
    private readonly IOllamaClient _ollama;
    private readonly ILogger<PredictionWorker> _logger;

    public PredictionWorker(
        PredictionQueue queue,
        PredictionStore store,
        IOllamaClient ollama,
        ILogger<PredictionWorker> logger)
    {
        _queue = queue;
        _store = store;
        _ollama = ollama;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Prediction worker started. Output folder: {Path}", _store.RootPath);

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Prediction {RequestId} started", job.RequestId);
                var text = await _ollama.WriteConclusionsAsync(job.Text, token: stoppingToken);
                var result = new PredictionResult(job.RequestId, DateTimeOffset.UtcNow, text);
                await _store.SaveCompletedAsync(result, stoppingToken);
                _logger.LogInformation("Prediction {RequestId} completed", job.RequestId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Prediction {RequestId} cancelled because the host is stopping",
                    job.RequestId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prediction {RequestId} failed", job.RequestId);
                try
                {
                    await _store.SaveFailedAsync(
                        new FailedPrediction(job.RequestId, DateTimeOffset.UtcNow, ex.Message),
                        CancellationToken.None);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(
                        saveEx,
                        "Could not write failed sidecar for prediction {RequestId}",
                        job.RequestId);
                }
            }
            finally
            {
                _queue.MarkCompleted(job.RequestId);
            }
        }
    }
}
