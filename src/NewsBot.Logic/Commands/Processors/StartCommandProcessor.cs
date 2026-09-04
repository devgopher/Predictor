using Botticelli.Framework.Commands.Processors;
using Botticelli.Framework.Commands.Utils;
using Botticelli.Framework.Commands.Validators;
using Botticelli.Framework.SendOptions;
using Botticelli.Shared.API.Client.Requests;
using Botticelli.Shared.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Data;
using NewsBot.Logic.Localization;
using Telegram.Bot.Types.ReplyMarkups;

namespace NewsBot.Logic.Commands.Processors;

public class StartCommandProcessor : CommandProcessor<StartCommand>
{
    private readonly IUserSettingsService _userSettings;

    public StartCommandProcessor(
        ILogger<StartCommandProcessor> logger,
        ICommandValidator<StartCommand> commandValidator,
        IValidator<Message> messageValidator,
        IUserSettingsService userSettings)
        : base(logger, commandValidator, messageValidator)
    {
        _userSettings = userSettings;
    }

    protected override async Task InnerProcess(Message message, CancellationToken token)
    {
        var userId = ProcessorHelpers.GetUserId(message);
        if (userId == null) return;

        var settings = await _userSettings.GetOrCreateAsync(userId.Value, token);

        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            var options = SendOptionsBuilder<InlineKeyboardMarkup>.CreateBuilder(ProcessorHelpers.LanguageKeyboard());
            await SendMessage(new SendMessageRequest
            {
                Message = new Message { ChatIds = message.ChatIds, Body = L10n.T("welcome_new", "en") }
            }, options, token);
            return;
        }

        await SendMessage(new Message
        {
            ChatIds = message.ChatIds,
            Body = L10n.T("welcome_back", settings.Language)
        }, token);
    }
}
