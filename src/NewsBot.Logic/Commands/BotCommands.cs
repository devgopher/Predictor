using Botticelli.Shared.ValueObjects;

namespace NewsBot.Logic.Commands;

public class StartCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class LangCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class CategoryCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class KeywordsCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class ChannelCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class AskCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class ForecastCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class FormatCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class NewsCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}

public class HelpCommand : Botticelli.Framework.Commands.ICommand
{
    public Guid Id { get; }
}
