using System.Net;
using System.Text;

namespace AlertaRio.Infrastructure.Providers;

public enum ProviderFetchFailure
{
    HttpStatus,
    Redirect,
    InvalidContentType,
    TooLarge,
    SchemaMismatch,
    IncompleteCatalog,
    Network,
    Timeout
}

public sealed class ProviderFetchException(
    ProviderFetchFailure failure, string message, HttpStatusCode? statusCode = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ProviderFetchFailure Failure { get; } = failure;
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

internal static class ProviderJsonTransport
{
    private const int MaxResponseBytes = 1_048_576;
    private const int MaxAttempts = 2;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<string> GetAsync(
        HttpClient client, Uri requestUri, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync(
                        requestUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if (response.RequestMessage?.RequestUri is { } finalUri && finalUri != requestUri ||
                        (int)response.StatusCode is >= 300 and < 400)
                        throw new ProviderFetchException(ProviderFetchFailure.Redirect,
                            "Provider redirected the request.", response.StatusCode);
                    if (attempt + 1 < MaxAttempts && IsTransient(response.StatusCode) &&
                        RetryDelay(response) is { } delay)
                    {
                        await Task.Delay(delay, timeout.Token);
                        continue;
                    }
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new ProviderFetchException(ProviderFetchFailure.HttpStatus,
                            "Provider did not return HTTP 200.", response.StatusCode);
                    return await ReadJsonAsync(response, timeout.Token);
                }
                catch (HttpRequestException) when (attempt + 1 < MaxAttempts)
                {
                    await Task.Delay(DefaultRetryDelay, timeout.Token);
                }
            }
            throw new InvalidOperationException("Provider retry loop ended unexpectedly.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderFetchException(ProviderFetchFailure.Timeout,
                "Provider request timed out.", innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderFetchException(ProviderFetchFailure.Network,
                "Provider request failed.", innerException: exception);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static TimeSpan? RetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ??
            (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow :
                DefaultRetryDelay);
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        return delay <= MaxRetryDelay ? delay : null;
    }

    private static async Task<string> ReadJsonAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType,
                "application/json", StringComparison.OrdinalIgnoreCase))
            throw new ProviderFetchException(ProviderFetchFailure.InvalidContentType,
                "Provider did not return JSON.");
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new ProviderFetchException(ProviderFetchFailure.TooLarge,
                "Provider JSON response exceeds the size limit.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaxResponseBytes)
                throw new ProviderFetchException(ProviderFetchFailure.TooLarge,
                    "Provider JSON response exceeds the size limit.");
            output.Write(buffer, 0, read);
        }
        try
        {
            return StrictUtf8.GetString(output.GetBuffer(), 0, (int)output.Length);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                "Provider response is not valid UTF-8.", innerException: exception);
        }
    }
}
