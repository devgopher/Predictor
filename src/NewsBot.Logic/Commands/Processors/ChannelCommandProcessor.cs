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

public class ChannelCommandProcessor : CommandProcessor<ChannelCommand>
{
    private readonly IUserSettingsService _userSettings;

    public ChannelCommandProcessor(
        ILogger<ChannelCommandProcessor> logger,
        ICommandValidator<ChannelCommand> commandValidator,
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

        var args = GetBody(message).GetArguments().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var channels = _userSettings.GetTelegramChannels(settings);
        var list = channels.Count > 0
            ? string.Join("\n", channels.Select(c => $"• {TelegramChannelUtils.ToDisplayUrl(c)}"))
            : "—";

        if (args.Length == 0)
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("channel_help", lang, list) }, token);
            return;
        }

        var action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("channel_help", lang, list) }, token);
                break;
            case "add" when args.Length >= 2:
                var raw = string.Join(' ', args.Skip(1));
                var username = TelegramChannelUtils.NormalizeUsername(raw);
                if (username == null)
                {
                    await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("channel_invalid", lang) }, token);
                    break;
                }
                await _userSettings.AddTelegramChannelAsync(userId.Value, username, token);
                await SendMessage(new Message
                {
                    ChatIds = message.ChatIds,
                    Body = L10n.T("channel_added", lang, TelegramChannelUtils.ToDisplayUrl(username))
                }, token);
                break;
            case "remove" when args.Length >= 2:
                var toRemove = TelegramChannelUtils.NormalizeUsername(string.Join(' ', args.Skip(1)));
                if (toRemove != null)
                    await _userSettings.RemoveTelegramChannelAsync(userId.Value, toRemove, token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("channel_removed", lang, args[1]) }, token);
                break;
            case "clear":
                await _userSettings.ClearTelegramChannelsAsync(userId.Value, token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("channel_cleared", lang) }, token);
                break;
            default:
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("unknown_command", lang) }, token);
                break;
        }
    }

    private static string GetBody(Message message) =>
        !string.IsNullOrWhiteSpace(message.CallbackData) ? message.CallbackData! : message.Body ?? string.Empty;
}
