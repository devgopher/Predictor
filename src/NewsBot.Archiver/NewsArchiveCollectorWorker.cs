using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsBot.Logic.Services;
using NewsBot.Logic.Settings;

namespace NewsBot.Archiver;

public class NewsArchiveCollectorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NewsArchiveCollectorSettings _settings;
    private readonly ILogger<NewsArchiveCollectorWorker> _logger;

    public NewsArchiveCollectorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NewsArchiveCollectorSettings> settings,
        ILogger<NewsArchiveCollectorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("News archive collector is disabled in configuration.");
            return;
        }

        _logger.LogInformation(
            "News archive collector started. Poll interval: {Hours}h, lookback: {Days} days.",
            _settings.PollIntervalHours,
            _settings.LookbackDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCollectionCycleAsync(stoppingToken);

            var delay = TimeSpan.FromHours(_settings.PollIntervalHours);
            _logger.LogInformation("Next archive poll in {Hours} hours.", _settings.PollIntervalHours);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunCollectionCycleAsync(CancellationToken token)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var collector = scope.ServiceProvider.GetRequiredService<INewsArchiveCollectorService>();
            var report = await collector.CollectAsync(token);

            if (report.FailureCount > 0)
            {
                _logger.LogWarning(
                    "Archive cycle completed with {FailureCount} API refusals. Saved {Saved} articles.",
                    report.FailureCount,
                    report.ArticlesSaved);

                foreach (var failure in report.Failures)
                {
                    _logger.LogWarning(
                        "Refusal detail: {Provider}/{Language}/{Category} — {Error}",
                        failure.Provider,
                        failure.Language,
                        failure.Category,
                        failure.Error);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Archive cycle completed successfully. Saved {Saved} articles.",
                    report.ArticlesSaved);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Archive collection cycle failed unexpectedly.");
        }
    }
}
