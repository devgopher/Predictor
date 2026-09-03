using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Predictor.Predictions;

internal sealed class PredictionStore
{
    private readonly string _root;
    private readonly string _failedRoot;
    private readonly ILogger<PredictionStore> _logger;

    public PredictionStore(
        IOptions<PredictionOptions> options,
        IHostEnvironment environment,
        ILogger<PredictionStore> logger)
    {
        _logger = logger;
        var configured = string.IsNullOrWhiteSpace(options.Value.Path)
            ? "predictions"
            : options.Value.Path.Trim();
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
        _failedRoot = Path.Combine(_root, "failed");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_failedRoot);
    }

    public string RootPath => _root;

    public bool ExistsCompleted(Guid requestId) => File.Exists(CompletedPath(requestId));

    public void ClearFailed(Guid requestId)
    {
        var path = FailedPath(requestId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public IReadOnlyList<PredictionResult> ListCompleted()
    {
        var results = new List<PredictionResult>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(path);
                var item = JsonSerializer.Deserialize<PredictionResult>(json, PredictionJson.Options);
                if (item is not null)
                    results.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping unreadable prediction file {Path}", path);
            }
        }

        return results
            .OrderByDescending(r => r.CreatedAt)
            .ToArray();
    }

    public PredictionResult? TryGetCompleted(Guid requestId)
        => TryRead<PredictionResult>(CompletedPath(requestId));

    public FailedPrediction? TryGetFailed(Guid requestId)
        => TryRead<FailedPrediction>(FailedPath(requestId));

    public async Task SaveCompletedAsync(PredictionResult result, CancellationToken token)
    {
        Directory.CreateDirectory(_root);
        await WriteAtomicallyAsync(CompletedPath(result.RequestId), result, token);
        ClearFailed(result.RequestId);
    }

    public async Task SaveFailedAsync(FailedPrediction result, CancellationToken token)
    {
        Directory.CreateDirectory(_failedRoot);
        await WriteAtomicallyAsync(FailedPath(result.RequestId), result, token);
    }

    private static async Task WriteAtomicallyAsync<T>(string dest, T value, CancellationToken token)
    {
        var temp = dest + ".tmp";
        var json = JsonSerializer.Serialize(value, PredictionJson.Options);
        try
        {
            await File.WriteAllTextAsync(temp, json, token);
            File.Move(temp, dest, overwrite: false);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private T? TryRead<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, PredictionJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read prediction file {Path}", path);
            return null;
        }
    }

    private string CompletedPath(Guid requestId)
        => Path.Combine(_root, $"{requestId:D}.json");

    private string FailedPath(Guid requestId)
        => Path.Combine(_failedRoot, $"{requestId:D}.json");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup of a leftover temp file.
        }
    }
}
