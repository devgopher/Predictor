using Botticelli.Framework.Commands.Validators;
using Botticelli.Framework.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NewsBot.Logic.Commands;
using NewsBot.Logic.Commands.Processors;

namespace NewsBot.Logic;

public static class BotCommandRegistrationExtensions
{
    private const ServiceLifetime ProcessorLifetime = ServiceLifetime.Scoped;

    public static IServiceCollection AddNewsBotCommands(this IServiceCollection services)
    {
        services.AddBotCommand<StartCommand>()
            .AddProcessor<StartCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<StartCommand>>();

        services.AddBotCommand<LangCommand>()
            .AddProcessor<LangCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<LangCommand>>();

        services.AddBotCommand<CategoryCommand>()
            .AddProcessor<CategoryCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<CategoryCommand>>();

        services.AddBotCommand<KeywordsCommand>()
            .AddProcessor<KeywordsCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<KeywordsCommand>>();

        services.AddBotCommand<ChannelCommand>()
            .AddProcessor<ChannelCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<ChannelCommand>>();

        services.AddBotCommand<FormatCommand>()
            .AddProcessor<FormatCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<FormatCommand>>();

        services.AddBotCommand<NewsCommand>()
            .AddProcessor<NewsCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<NewsCommand>>();

        services.AddBotCommand<AskCommand>()
            .AddProcessor<AskCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<AskCommand>>();

        services.AddBotCommand<ForecastCommand>()
            .AddProcessor<ForecastCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<ForecastCommand>>();

        services.AddBotCommand<HelpCommand>()
            .AddProcessor<HelpCommandProcessor>(ProcessorLifetime)
            .AddValidator<PassValidator<HelpCommand>>();

        return services;
    }
}
