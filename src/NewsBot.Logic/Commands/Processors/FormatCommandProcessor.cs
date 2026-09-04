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
using NewsBot.Logic.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace NewsBot.Logic.Commands.Processors;

public class FormatCommandProcessor : CommandProcessor<FormatCommand>
{
    private readonly IUserSettingsService _userSettings;

    public FormatCommandProcessor(
        ILogger<FormatCommandProcessor> logger,
        ICommandValidator<FormatCommand> commandValidator,
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
        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("news_lang_required", "en") }, token);
            return;
        }

        var arg = GetBody(message).GetArguments().Trim().ToLowerInvariant();
        var current = L10n.FormatLabel(settings.Format);

        if (string.IsNullOrWhiteSpace(arg))
        {
            var options = SendOptionsBuilder<InlineKeyboardMarkup>.CreateBuilder(ProcessorHelpers.FormatKeyboard());
            await SendMessage(new SendMessageRequest
            {
                Message = new Message { ChatIds = message.ChatIds, Body = L10n.T("format_help", lang, current) }
            }, options, token);
            return;
        }

        if (!NewsFormatExtensions.TryParse(arg, out var format))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("unknown_command", lang) }, token);
            return;
        }

        await _userSettings.SetFormatAsync(userId.Value, format, token);
        await SendMessage(new Message
        {
            ChatIds = message.ChatIds,
            Body = L10n.T("format_set", lang, L10n.FormatLabel(format))
        }, token);
    }

    private static string GetBody(Message message) =>
        !string.IsNullOrWhiteSpace(message.CallbackData) ? message.CallbackData! : message.Body ?? string.Empty;
}
