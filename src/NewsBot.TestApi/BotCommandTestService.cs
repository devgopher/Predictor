using Botticelli.Framework.Extensions.Processors;
using Botticelli.Shared.ValueObjects;

namespace NewsBot.TestApi;

public class BotCommandTestService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FakeBot _fakeBot;

    public BotCommandTestService(IServiceProvider serviceProvider, FakeBot fakeBot)
    {
        _serviceProvider = serviceProvider;
        _fakeBot = fakeBot;
    }

    public async Task<CommandTestResult> ExecuteAsync(long userId, string text, CancellationToken token)
    {
        _fakeBot.ClearMessages();

        var message = BuildMessage(userId, text);
        var factory = ProcessorFactoryBuilder.Build(_serviceProvider);
        var tasks = factory.GetProcessors()
            .Select(p => p.ProcessAsync(message, token))
            .Concat(factory.GetCommandChainProcessors().Select(p => p.ProcessAsync(message, token)))
            .ToArray();

        await Task.WhenAll(tasks);

        return new CommandTestResult
        {
            UserId = userId,
            Input = text,
            Responses = _fakeBot.SentMessages
                .Select(m => new BotResponseDto
                {
                    Body = m.Body,
                    Attachments = m.Attachments
                        .Select(a => new BotAttachmentDto(a.Name, a.MediaType, a.SizeBytes))
                        .ToList(),
                    OptionsHint = m.OptionsHint
                })
                .ToList()
        };
    }

    private static Message BuildMessage(long userId, string text)
    {
        var chatId = userId.ToString();
        return new Message
        {
            Body = text,
            ChatIds = [chatId],
            From = new User
            {
                Id = chatId,
                Name = "Test",
                NickName = "test_user"
            }
        };
    }
}

public sealed class CommandTestResult
{
    public long UserId { get; init; }
    public string Input { get; init; } = string.Empty;
    public List<BotResponseDto> Responses { get; init; } = [];
}

public sealed class BotResponseDto
{
    public string? Body { get; init; }
    public List<BotAttachmentDto> Attachments { get; init; } = [];
    public string? OptionsHint { get; init; }
}

public sealed record BotAttachmentDto(string Name, string MediaType, int SizeBytes);
