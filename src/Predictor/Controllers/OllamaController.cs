using Microsoft.AspNetCore.Mvc;
using Predictor.Models;
using Predictor.Ollama;

namespace Predictor.Controllers;

[ApiController]
[Route("api")]
public sealed class OllamaController(IOllamaClient ollama) : ControllerBase
{
    [HttpPost("embed")]
    public async Task<IActionResult> Embed([FromBody] EmbedRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "text is required" });

        var embedding = await ollama.EmbedAsync(request.Text, token);
        return embedding is null
            ? StatusCode(StatusCodes.Status502BadGateway)
            : Ok(new { dimensions = embedding.Length, embedding });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required" });

        var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? $"Today {DateTime.UtcNow.Date} UTC."
            : $"Today {DateTime.UtcNow.Date} UTC. {request.SystemPrompt}";

        var reply = await ollama.ChatAsync(request.Message, systemPrompt, token);
        return Ok(new { reply = reply.Content, thinking = reply.Thinking });
    }

    [HttpPost("conclusions")]
    public async Task<IActionResult> Conclusions([FromBody] ConclusionsRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Context))
            return BadRequest(new { error = "context is required" });

        var conclusions = await ollama.WriteConclusionsAsync(request.Context, request.Question, token);
        return Ok(new { conclusions = conclusions.Content, thinking = conclusions.Thinking });
    }
}
