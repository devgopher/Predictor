using Botticelli.Framework.Commands.Processors;
using Botticelli.Framework.Commands.Validators;
using Botticelli.Shared.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Data;
using NewsBot.Logic.Localization;
using NewsBot.Logic.Services;

namespace NewsBot.Logic.Commands.Processors;

public class NewsCommandProcessor : CommandProcessor<NewsCommand>
{
    private readonly IUserSettingsService _userSettings;
    private readonly INewsService _newsService;
    private readonly INewsFormatterService _formatterService;
    private readonly INewsArchiveService _archiveService;

    public NewsCommandProcessor(
        ILogger<NewsCommandProcessor> logger,
        ICommandValidator<NewsCommand> commandValidator,
        IValidator<Message> messageValidator,
        IUserSettingsService userSettings,
        INewsService newsService,
        INewsFormatterService formatterService,
        INewsArchiveService archiveService)
        : base(logger, commandValidator, messageValidator)
    {
        _userSettings = userSettings;
        _newsService = newsService;
        _formatterService = formatterService;
        _archiveService = archiveService;
    }

    protected override async Task InnerProcess(Message message, CancellationToken token)
    {
        var userId = ProcessorHelpers.GetUserId(message);
        if (userId == null) return;

        var settings = await _userSettings.GetOrCreateAsync(userId.Value, token);
        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("news_lang_required", "en") }, token);
            return;
        }

        var lang = L10n.NormalizeLang(settings.Language);
        await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("news_fetching", lang) }, token);

        var categories = _userSettings.GetCategories(settings);
        var keywords = _userSettings.GetKeywords(settings);
        var channels = _userSettings.GetTelegramChannels(settings);
        var articles = await _newsService.FetchNewsAsync(lang, categories, keywords, channels, token);

        if (articles.Count == 0)
        {
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("news_empty", lang) }, token);
            return;
        }

        await _userSettings.SaveRecentNewsAsync(userId.Value, articles, token);
        await _archiveService.ArchiveArticlesAsync(userId.Value, articles, token);

        foreach (var article in articles)
        {
            var formatted = await _formatterService.FormatArticleAsync(article, settings.Format, token);
            await SendMessage(new Message { ChatIds = message.ChatIds, Body = formatted }, token);
        }
    }
}
