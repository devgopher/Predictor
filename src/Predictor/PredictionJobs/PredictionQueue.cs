using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Predictor.PredictionJobs;

public sealed class PredictionQueue
{
    private readonly Channel<PredictionJob> _channel = Channel.CreateUnbounded<PredictionJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    public bool IsInFlight(Guid requestId) => _inFlight.ContainsKey(requestId);

    public IAsyncEnumerable<PredictionJob> ReadAllAsync(CancellationToken token)
        => _channel.Reader.ReadAllAsync(token);

    /// <summary>
    /// Accepts the job if the GUID is not already queued or running.
    /// </summary>
    public EnqueueAttempt TryEnqueue(PredictionJob job)
    {
        if (!_inFlight.TryAdd(job.RequestId, 0))
            return EnqueueAttempt.Duplicate();

        try
        {
            if (_channel.Writer.TryWrite(job))
                return EnqueueAttempt.Accepted();

            _inFlight.TryRemove(job.RequestId, out _);
            return EnqueueAttempt.Failed("Prediction queue is not accepting work.");
        }
        catch (Exception ex)
        {
            _inFlight.TryRemove(job.RequestId, out _);
            return EnqueueAttempt.Failed(ex.Message);
        }
    }

    public void MarkCompleted(Guid requestId) => _inFlight.TryRemove(requestId, out _);
}

public readonly record struct EnqueueAttempt(EnqueueStatus Status, string? Error)
{
    public static EnqueueAttempt Accepted() => new(EnqueueStatus.Accepted, null);

    public static EnqueueAttempt Duplicate() => new(EnqueueStatus.Duplicate, "A prediction with this requestId already exists.");

    public static EnqueueAttempt Failed(string error) => new(EnqueueStatus.Failed, error);
}

public enum EnqueueStatus
{
    Accepted,
    Duplicate,
    Failed
}
