using Botticelli.Framework.Commands.Processors;
using Botticelli.Framework.Commands.Validators;
using Botticelli.Shared.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NewsBot.Logic.Data;
using NewsBot.Logic.Localization;

namespace NewsBot.Logic.Commands.Processors;

public class HelpCommandProcessor : CommandProcessor<HelpCommand>
{
    private readonly IUserSettingsService _userSettings;

    public HelpCommandProcessor(
        ILogger<HelpCommandProcessor> logger,
        ICommandValidator<HelpCommand> commandValidator,
        IValidator<Message> messageValidator,
        IUserSettingsService userSettings)
        : base(logger, commandValidator, messageValidator)
    {
        _userSettings = userSettings;
    }

    protected override async Task InnerProcess(Message message, CancellationToken token)
    {
        var userId = ProcessorHelpers.GetUserId(message);
        var lang = "en";
        if (userId != null)
        {
            var settings = await _userSettings.GetAsync(userId.Value, token);
            if (!string.IsNullOrWhiteSpace(settings?.Language))
                lang = settings.Language;
        }

        await SendMessage(new Message
        {
            ChatIds = message.ChatIds,
            Body = L10n.T("welcome_back", lang)
        }, token);
    }
}
