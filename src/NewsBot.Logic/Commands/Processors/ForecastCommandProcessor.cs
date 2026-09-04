using Botticelli.Framework.Commands.Processors;
using Botticelli.Framework.Commands.Utils;
using Botticelli.Framework.Commands.Validators;
using Botticelli.Shared.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Data;
using NewsBot.Logic.Localization;
using NewsBot.Logic.Services;

namespace NewsBot.Logic.Commands.Processors;

public class ForecastCommandProcessor : CommandProcessor<ForecastCommand>
{
    private readonly IUserSettingsService _userSettings;
    private readonly INewsForecastService _forecastService;
    private readonly IForecastQueueService _forecastQueue;

    public ForecastCommandProcessor(
        ILogger<ForecastCommandProcessor> logger,
        ICommandValidator<ForecastCommand> commandValidator,
        IValidator<Message> messageValidator,
        IUserSettingsService userSettings,
        INewsForecastService forecastService,
        IForecastQueueService forecastQueue)
        : base(logger, commandValidator, messageValidator)
    {
        _userSettings = userSettings;
        _forecastService = forecastService;
        _forecastQueue = forecastQueue;
    }

    protected override async Task InnerProcess(Message message, CancellationToken token)
    {
        var userId = ProcessorHelpers.GetUserId(message);
        if (userId == null) return;

        var settings = await _userSettings.GetOrCreateAsync(userId.Value, token);
        var lang = L10n.NormalizeLang(settings.Language);

        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("news_lang_required", "en") }, token);
            return;
        }

        if (!_forecastService.IsAvailable)
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("forecast_ai_disabled", lang) }, token);
            return;
        }

        var question = (message.Body ?? string.Empty).GetArguments().Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("forecast_help", lang) }, token);
            return;
        }

        var chatId = message.ChatIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(chatId))
        {
            Logger.LogWarning("Forecast command without chat id from user {UserId}", userId);
            return;
        }

        var job = await _forecastQueue.EnqueueAsync(userId.Value, chatId, question, lang, token);

        await SendMessage(new Message
        {
            ChatIds = message.ChatIds,
            Body = L10n.T("forecast_queued", lang, job.DocumentFileName)
        }, token);
    }
}
