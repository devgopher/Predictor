using NewsBot.Logic.Localization;
using Botticelli.Shared.ValueObjects;
using Telegram.Bot.Types.ReplyMarkups;

namespace NewsBot.Logic.Commands.Processors;

internal static class ProcessorHelpers
{
    public static long? GetUserId(Message message) =>
        long.TryParse(message.From?.Id, out var id) ? id : null;

    public static InlineKeyboardMarkup LanguageKeyboard()
    {
        var rows = L10n.SupportedLanguages
            .Select(l => new[] { InlineKeyboardButton.WithCallbackData(l.Label, $"/lang {l.Code}") })
            .ToArray();
        return new(rows);
    }

    public static InlineKeyboardMarkup CategoryKeyboard()
    {
        var rows = L10n.NewsCategories
            .Chunk(2)
            .Select(chunk => chunk
                .Select(c => InlineKeyboardButton.WithCallbackData(c.LabelEn, $"/category add {c.Code}"))
                .ToArray())
            .ToArray();
        return new(rows);
    }

    public static InlineKeyboardMarkup FormatKeyboard() =>
        new([
            [
                InlineKeyboardButton.WithCallbackData("full1000", "/format full1000"),
                InlineKeyboardButton.WithCallbackData("medium500_1000", "/format medium500_1000")
            ],
            [
                InlineKeyboardButton.WithCallbackData("short50_100", "/format short50_100"),
                InlineKeyboardButton.WithCallbackData("tiny50", "/format tiny50")
            ]
        ]);
}
