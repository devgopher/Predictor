using Microsoft.AspNetCore.Mvc;
using NewsBot.TestApi;

namespace NewsBot.TestApi.Controllers;

[ApiController]
[Route("api/commands")]
public class CommandsController : ControllerBase
{
    private static readonly CommandDefinition[] AvailableCommands =
    [
        new("/start", "Start bot / language selection", "/start"),
        new("/lang", "Set language", "/lang ru"),
        new("/category", "Manage categories", "/category list"),
        new("/keywords", "Manage keywords", "/keywords list"),
        new("/channel", "Manage Telegram channels", "/channel list"),
        new("/format", "News text format", "/format full1000"),
        new("/news", "Fetch news", "/news"),
        new("/ask", "Ask AI about recent news", "/ask What is the main topic?"),
        new("/forecast", "Probability forecast (queued as docx)", "/forecast Probability of rain tomorrow"),
        new("/help", "Help", "/help")
    ];

    private readonly BotCommandTestService _testService;

    public CommandsController(BotCommandTestService testService)
    {
        _testService = testService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<CommandDefinition>> List() => Ok(AvailableCommands);

    [HttpPost("execute")]
    public async Task<ActionResult<CommandTestResult>> Execute(
        [FromBody] ExecuteCommandRequest request,
        CancellationToken token)
    {
        if (request.UserId <= 0)
            return BadRequest("userId must be positive.");

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("text is required.");

        var result = await _testService.ExecuteAsync(request.UserId, request.Text.Trim(), token);
        return Ok(result);
    }
}

public sealed record ExecuteCommandRequest(long UserId, string Text);

public sealed record CommandDefinition(string Name, string Description, string Example);
