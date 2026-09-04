using Botticelli.BotData.Entities.Bot;
using Botticelli.Interfaces;
using Botticelli.Shared.API;
using Botticelli.Shared.Constants;
using Botticelli.Shared.API.Admin.Requests;
using Botticelli.Shared.API.Admin.Responses;
using Botticelli.Shared.API.Client.Requests;
using Botticelli.Shared.API.Client.Responses;
using Botticelli.Shared.Constants;
using Botticelli.Shared.ValueObjects;

namespace NewsBot.TestApi;

public class FakeBot : IBot
{
    private readonly List<CapturedBotMessage> _sentMessages = [];

    public BotType Type => BotType.Unknown;
    public string? BotUserId { get; set; } = "test-bot";

    public IReadOnlyList<CapturedBotMessage> SentMessages => _sentMessages;

    public void ClearMessages() => _sentMessages.Clear();

    public Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken token)
    {
        Capture(request);
        return Task.FromResult(SendMessageResponse.GetInstance("ok"));
    }

    public Task<SendMessageResponse> SendMessageAsync<TSendOptions>(
        SendMessageRequest request,
        ISendOptionsBuilder<TSendOptions>? optionsBuilder,
        CancellationToken token)
        where TSendOptions : class
    {
        Capture(request, optionsBuilder?.Build()?.ToString());
        return Task.FromResult(SendMessageResponse.GetInstance("ok"));
    }

    public Task<SendMessageResponse> UpdateMessageAsync(SendMessageRequest request, CancellationToken token) =>
        SendMessageAsync(request, token);

    public Task<SendMessageResponse> UpdateMessageAsync<TSendOptions>(
        SendMessageRequest request,
        ISendOptionsBuilder<TSendOptions>? optionsBuilder,
        CancellationToken token)
        where TSendOptions : class =>
        SendMessageAsync(request, optionsBuilder, token);

    public Task<RemoveMessageResponse> DeleteMessageAsync(DeleteMessageRequest request, CancellationToken token) =>
        Task.FromResult(RemoveMessageResponse.GetInstance("deleted"));

    public Task<StartBotResponse> StartBotAsync(StartBotRequest request, CancellationToken token) =>
        Task.FromResult(StartBotResponse.GetInstance(request.Uid, string.Empty, AdminCommandStatus.Ok));

    public Task<StopBotResponse> StopBotAsync(StopBotRequest request, CancellationToken token) =>
        Task.FromResult(StopBotResponse.GetInstance(request.Uid, string.Empty, AdminCommandStatus.Ok));

    public Task SetBotContext(BotData? context, CancellationToken token) =>
        Task.CompletedTask;

    private void Capture(SendMessageRequest request, string? optionsHint = null)
    {
        var message = request.Message;
        _sentMessages.Add(new CapturedBotMessage
        {
            Body = message.Body,
            Attachments = message.Attachments?
                .Select(a => a switch
                {
                    BinaryBaseAttachment binary => new CapturedAttachment(binary.Name, binary.MediaType.ToString(), binary.Data.Length),
                    _ => new CapturedAttachment("attachment", "unknown", 0)
                })
                .ToList() ?? [],
            OptionsHint = optionsHint
        });
    }
}

public sealed class CapturedBotMessage
{
    public string? Body { get; init; }
    public List<CapturedAttachment> Attachments { get; init; } = [];
    public string? OptionsHint { get; init; }
}

public sealed record CapturedAttachment(string Name, string MediaType, int SizeBytes);
