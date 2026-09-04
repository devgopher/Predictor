using Botticelli.Interfaces;
using Botticelli.Shared.API.Client.Requests;
using Botticelli.Shared.Constants;
using Botticelli.Shared.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Data;
using NewsBot.Logic.Data.Entities;
using NewsBot.Logic.Localization;
using NewsBot.Logic.Services;

namespace NewsBot;

public class ForecastQueueWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBot _bot;
    private readonly ILogger<ForecastQueueWorker> _logger;

    public ForecastQueueWorker(
        IServiceScopeFactory scopeFactory,
        IBot bot,
        ILogger<ForecastQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        using (var scope = _scopeFactory.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IForecastQueueService>();
            await queue.ResetInterruptedJobsAsync(stoppingToken);
        }

        _logger.LogInformation("Forecast queue worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await ProcessNextJobAsync(stoppingToken);
            if (!processed)
                await Task.Delay(IdleDelay, stoppingToken);
        }
    }

    private async Task<bool> ProcessNextJobAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IForecastQueueService>();
        var job = await queue.ClaimNextPendingAsync(token);
        if (job == null)
            return false;

        _logger.LogInformation("Processing forecast job {JobId} for user {UserId}", job.Id, job.TelegramUserId);

        var lang = L10n.NormalizeLang(job.Language);
        var forecastService = scope.ServiceProvider.GetRequiredService<INewsForecastService>();

        try
        {
            var answer = await forecastService.BuildForecastAsync(job.TelegramUserId, job.Question, lang, token);
            if (string.IsNullOrWhiteSpace(answer))
            {
                await queue.FailAsync(job.Id, "No archived news for forecast.", token);
                await SendTextAsync(job.ChatId, L10n.T("forecast_no_data", lang), token);
                return true;
            }

            var docx = ForecastDocxBuilder.BuildDocument(job.Question, answer, lang);
            await queue.CompleteAsync(job.Id, answer, token);
            await SendDocumentAsync(job, docx, lang, token);

            _logger.LogInformation("Forecast job {JobId} completed.", job.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Forecast job {JobId} failed.", job.Id);
            await queue.FailAsync(job.Id, ex.Message, token);
            await SendTextAsync(job.ChatId, L10n.T("forecast_failed", lang), token);
        }

        return true;
    }

    private async Task SendDocumentAsync(ForecastJob job, byte[] docx, string lang, CancellationToken token)
    {
        var request = new SendMessageRequest
        {
            Message = new Message
            {
                ChatIds = [job.ChatId],
                Body = L10n.T("forecast_ready", lang),
                Attachments =
                [
                    new BinaryBaseAttachment(
                        Guid.NewGuid().ToString(),
                        job.DocumentFileName,
                        MediaType.Document,
                        string.Empty,
                        docx)
                ]
            }
        };

        await _bot.SendMessageAsync(request, token);
    }

    private async Task SendTextAsync(string chatId, string text, CancellationToken token)
    {
        await _bot.SendMessageAsync(new SendMessageRequest
        {
            Message = new Message
            {
                ChatIds = [chatId],
                Body = text
            }
        }, token);
    }
}
