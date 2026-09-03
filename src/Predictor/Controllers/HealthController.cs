using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Predictor.Ollama.Options;

namespace Predictor.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Get([FromServices] IOptions<OllamaOptions> ollama)
    {
        var options = ollama.Value;
        return Ok(new
        {
            status = "ok",
            ollama = new
            {
                embedding = new
                {
                    baseUrl = options.Embedding.ResolvedBaseUrl,
                    model = options.Embedding.ResolvedModel
                },
                chat = new
                {
                    baseUrl = options.Chat.ResolvedBaseUrl,
                    model = options.Chat.ResolvedModel,
                    allowUrlFetch = options.Chat.AllowUrlFetch
                }
            }
        });
    }
}
