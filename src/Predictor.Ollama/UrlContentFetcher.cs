using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Predictor.Ollama.Options;

namespace Predictor.Ollama;

public sealed class UrlContentFetcher(IHttpClientFactory httpClientFactory, ILogger<UrlContentFetcher> logger)
{
    public const string HttpClientName = "OllamaUrlFetch";
    public const string ToolName = "fetch_url";
    private const int DefaultMaxBytes = 1048576;
    private const int DefaultTimeoutSeconds = 15;
    private const int MaxRedirects = 5;

    public async Task<string> FetchAsync(
        string? url,
        int? requestedMaxBytes,
        OllamaAgentOptions agent,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Error: url is required.";

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return "Error: url is not a valid absolute URI.";

        var validation = await ValidateAsync(uri, agent.AllowLocalhostUrlFetch, token);
        if (validation is not null)
            return validation;

        var maxBytes = agent.UrlFetchMaxBytes > 0 ? agent.UrlFetchMaxBytes : DefaultMaxBytes;
        if (requestedMaxBytes is > 0)
            maxBytes = Math.Min(maxBytes, requestedMaxBytes.Value);

        var timeoutSec = agent.UrlFetchTimeoutSeconds > 0 ? agent.UrlFetchTimeoutSeconds : DefaultTimeoutSeconds;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        var client = httpClientFactory.CreateClient(HttpClientName);
        var current = uri;
        try
        {
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token);

                if (IsRedirect(response.StatusCode))
                {
                    var location = response.Headers.Location;
                    if (location is null)
                        return $"Error: redirect without Location from {current}.";

                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    var redirectCheck = await ValidateAsync(next, agent.AllowLocalhostUrlFetch, cts.Token);
                    if (redirectCheck is not null)
                        return redirectCheck;

                    current = next;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error: HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) fetching {current}.";
                }

                var text = await ReadLimitedTextAsync(response, maxBytes, cts.Token);
                logger.LogInformation("Fetched {Url} ({Length} chars)", current, text.Length);
                return text;
            }

            return "Error: too many redirects.";
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return $"Error: timed out fetching {current} after {timeoutSec}s.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "fetch_url failed for {Url}", current);
            return $"Error: failed to fetch {current}: {ex.Message}";
        }
    }

    private static async Task<string?> ValidateAsync(Uri uri, bool allowLocalhost, CancellationToken token)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return "Error: only http and https URLs are allowed.";

        if (allowLocalhost)
            return null;

        var host = uri.IdnHost;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "metadata.google.internal", StringComparison.OrdinalIgnoreCase))
        {
            return "Error: localhost and metadata URLs are blocked. Set AllowLocalhostUrlFetch to allow them.";
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var parsed))
        {
            addresses = [parsed];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, token);
            }
            catch (Exception ex)
            {
                return $"Error: could not resolve host '{host}': {ex.Message}";
            }
        }

        if (addresses.Length == 0)
            return $"Error: could not resolve host '{host}'.";

        return addresses.Any(IsRestrictedAddress) ? "Error: private, loopback, and link-local addresses are blocked. Set AllowLocalhostUrlFetch to allow them." : null;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static bool IsRestrictedAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
                return true;
            return false;
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return true;

        var b = ip.GetAddressBytes();
        if (b[0] == 0 || b[0] == 10 || b[0] == 127)
            return true;
        if (b[0] == 169 && b[1] == 254)
            return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return true;
        if (b[0] == 192 && b[1] == 168)
            return true;

        return false;
    }

    private static async Task<string> ReadLimitedTextAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken token)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var buffer = new byte[8192];
        var remaining = maxBytes;
        var truncated = false;

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), token);
            if (read == 0)
                break;

            memory.Write(buffer, 0, read);
            remaining -= read;
        }

        if (remaining == 0)
        {
            var extra = await stream.ReadAsync(buffer.AsMemory(0, 1), token);
            truncated = extra > 0;
        }

        var text = Encoding.UTF8.GetString(memory.ToArray());
        if (truncated)
            text += "\n[truncated]";

        return text;
    }
}
