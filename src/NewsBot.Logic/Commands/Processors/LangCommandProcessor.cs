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

public class LangCommandProcessor : CommandProcessor<LangCommand>
{
    private readonly IUserSettingsService _userSettings;

    public LangCommandProcessor(
        ILogger<LangCommandProcessor> logger,
        ICommandValidator<LangCommand> commandValidator,
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
        var lang = L10n.NormalizeLang(settings.Language);
        var arg = GetBody(message).GetArguments().Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(arg))
        {
            var options = SendOptionsBuilder<InlineKeyboardMarkup>.CreateBuilder(ProcessorHelpers.LanguageKeyboard());
            await SendMessage(new SendMessageRequest
            {
                Message = new Message { ChatIds = message.ChatIds, Body = L10n.T("lang_choose", lang) }
            }, options, token);
            return;
        }

        if (!L10n.SupportedLanguages.Any(l => l.Code == arg))
        {
            await SendMessage(new Message
            {
                ChatIds = message.ChatIds,
                Body = L10n.T("unknown_command", lang)
            }, token);
            return;
        }

        await _userSettings.SetLanguageAsync(userId.Value, arg, token);
        var label = L10n.SupportedLanguages.First(l => l.Code == arg).Label;

        await SendMessage(new Message
        {
            ChatIds = message.ChatIds,
            Body = L10n.T("lang_set", arg, label)
        }, token);
    }

    private static string GetBody(Message message) =>
        !string.IsNullOrWhiteSpace(message.CallbackData) ? message.CallbackData! : message.Body ?? string.Empty;
}
