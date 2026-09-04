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

public class AskCommandProcessor : CommandProcessor<AskCommand>
{
    private readonly IUserSettingsService _userSettings;
    private readonly INewsAiChatService _aiChatService;

    public AskCommandProcessor(
        ILogger<AskCommandProcessor> logger,
        ICommandValidator<AskCommand> commandValidator,
        IValidator<Message> messageValidator,
        IUserSettingsService userSettings,
        INewsAiChatService aiChatService)
        : base(logger, commandValidator, messageValidator)
    {
        _userSettings = userSettings;
        _aiChatService = aiChatService;
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

        if (!_aiChatService.IsAvailable)
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("ask_ai_disabled", lang) }, token);
            return;
        }

        var question = (message.Body ?? string.Empty).GetArguments().Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("ask_help", lang) }, token);
            return;
        }

        var articles = _userSettings.GetRecentNews(settings);
        if (articles.Count == 0)
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("ask_no_context", lang) }, token);
            return;
        }

        await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("ask_thinking", lang) }, token);

        var answer = await _aiChatService.AskAboutNewsAsync(articles, question, token);
        if (string.IsNullOrWhiteSpace(answer))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("ask_failed", lang) }, token);
            return;
        }

        await SendMessage(new Message { ChatIds = message.ChatIds, Body = answer }, token);
    }
}
