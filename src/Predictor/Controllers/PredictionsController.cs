using Microsoft.AspNetCore.Mvc;
using Predictor.Models;
using Predictor.Predictions;

namespace Predictor.Controllers;

[ApiController]
[Route("api/predictions")]
public sealed class PredictionsController(
    PredictionQueue queue,
    PredictionStore store,
    ILogger<PredictionsController> logger) : ControllerBase
{
    [HttpPost]
    public IActionResult Enqueue([FromBody] EnqueuePredictionRequest request)
    {
        Guid requestId;
        string requestIdText;

        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            requestId = Guid.NewGuid();
            requestIdText = requestId.ToString("D");
        }
        else if (Guid.TryParse(request.RequestId, out requestId))
        {
            requestIdText = requestId.ToString("D");
        }
        else
        {
            return BadRequest(new
            {
                requestId = request.RequestId,
                status = "failure",
                error = "requestId must be a GUID."
            });
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new
                {
                    requestId = requestIdText,
                    status = "failure",
                    error = "text is required."
                });
            }

            if (store.ExistsCompleted(requestId) || queue.IsInFlight(requestId))
            {
                return Conflict(new
                {
                    requestId = requestIdText,
                    status = "conflict",
                    error = "A prediction with this requestId already exists."
                });
            }

            var attempt = queue.TryEnqueue(new PredictionJob(requestId, request.Text));
            switch (attempt.Status)
            {
                case EnqueueStatus.Accepted:
                    store.ClearFailed(requestId);
                    logger.LogInformation("Prediction {RequestId} queued", requestId);
                    return Ok(new { requestId = requestIdText, status = "created" });

                case EnqueueStatus.Duplicate:
                    return Conflict(new
                    {
                        requestId = requestIdText,
                        status = "conflict",
                        error = attempt.Error
                    });

                default:
                    logger.LogError(
                        "Prediction {RequestId} enqueue failed: {Error}",
                        requestId,
                        attempt.Error);
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        new
                        {
                            requestId = requestIdText,
                            status = "failure",
                            error = attempt.Error ?? "Failed to enqueue prediction."
                        });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Prediction {RequestId} enqueue failed", requestId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { requestId = requestIdText, status = "failure", error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ListCompleted()
        => Ok(store.ListCompleted());

    [HttpGet("{requestId:guid}")]
    public IActionResult GetById(Guid requestId)
    {
        var requestIdText = requestId.ToString("D");
        var completed = store.TryGetCompleted(requestId);
        if (completed is not null)
            return Ok(completed);

        if (queue.IsInFlight(requestId))
        {
            return StatusCode(
                StatusCodes.Status202Accepted,
                new { requestId = requestIdText, status = "queued" });
        }

        var failed = store.TryGetFailed(requestId);
        if (failed is not null)
        {
            return Ok(new
            {
                requestId = requestIdText,
                createdAt = failed.CreatedAt,
                status = "failed",
                error = failed.Error
            });
        }

        return NotFound(new { requestId = requestIdText, status = "not_found" });
    }
}
