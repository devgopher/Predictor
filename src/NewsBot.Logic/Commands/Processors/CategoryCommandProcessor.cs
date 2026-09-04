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

public class CategoryCommandProcessor : CommandProcessor<CategoryCommand>
{
    private readonly IUserSettingsService _userSettings;

    public CategoryCommandProcessor(
        ILogger<CategoryCommandProcessor> logger,
        ICommandValidator<CategoryCommand> commandValidator,
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
        var categories = _userSettings.GetCategories(settings);
        var list = categories.Count > 0 ? string.Join(", ", categories) : "—";

        if (args.Length == 0)
        {
            var options = SendOptionsBuilder<InlineKeyboardMarkup>.CreateBuilder(ProcessorHelpers.CategoryKeyboard());
            await SendMessage(new SendMessageRequest
            {
                Message = new Message { ChatIds = message.ChatIds, Body = L10n.T("category_help", lang, list) }
            }, options, token);
            return;
        }

        var action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("category_help", lang, list) }, token);
                break;
            case "add" when args.Length >= 2:
                await _userSettings.AddCategoryAsync(userId.Value, args[1], token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("category_added", lang, args[1]) }, token);
                break;
            case "remove" when args.Length >= 2:
                await _userSettings.RemoveCategoryAsync(userId.Value, args[1], token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("category_removed", lang, args[1]) }, token);
                break;
            case "clear":
                await _userSettings.ClearCategoriesAsync(userId.Value, token);
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("category_cleared", lang) }, token);
                break;
            default:
                await SendMessage(new Message { ChatIds = message.ChatIds, Body = L10n.T("unknown_command", lang) }, token);
                break;
        }
    }

    private static string GetBody(Message message) =>
        !string.IsNullOrWhiteSpace(message.CallbackData) ? message.CallbackData! : message.Body ?? string.Empty;
}
