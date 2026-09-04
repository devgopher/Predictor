using Botticelli.Framework.Commands.Processors;
using Botticelli.Framework.Commands.Utils;
using Botticelli.Framework.Commands.Validators;
using Botticelli.Shared.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Data;
using NewsBot.Logic.Localization;

namespace NewsBot.Logic.Commands.Processors;

public class KeywordsCommandProcessor : CommandProcessor<KeywordsCommand>
{
    private readonly IUserSettingsService _userSettings;

    public KeywordsCommandProcessor(
        ILogger<KeywordsCommandProcessor> logger,
        ICommandValidator<KeywordsCommand> commandValidator,
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

        var args = (message.Body ?? string.Empty).GetArguments().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keywords = _userSettings.GetKeywords(settings);
        var list = keywords.Count > 0 ? string.Join(", ", keywords) : "—";

        if (args.Length == 0)
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("keywords_help", lang, list) }, token);
            return;
        }

        var action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("keywords_help", lang, list) }, token);
                break;
            case "add" when args.Length >= 2:
                var keyword = string.Join(' ', args.Skip(1));
                await _userSettings.AddKeywordAsync(userId.Value, keyword, token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("keyword_added", lang, keyword) }, token);
                break;
            case "remove" when args.Length >= 2:
                var toRemove = string.Join(' ', args.Skip(1));
                await _userSettings.RemoveKeywordAsync(userId.Value, toRemove, token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("keyword_removed", lang, toRemove) }, token);
                break;
            case "clear":
                await _userSettings.ClearKeywordsAsync(userId.Value, token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("keywords_cleared", lang) }, token);
                break;
            default:
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("unknown_command", lang) }, token);
                break;
        }
    }
}
