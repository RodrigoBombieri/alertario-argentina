using System.Net;
using System.Text;

namespace AlertaRio.Infrastructure.Providers;

public enum ProviderFetchFailure
{
    HttpStatus,
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
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<string> GetAsync(
        HttpClient client, Uri requestUri, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await client.GetAsync(
                requestUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.RequestMessage?.RequestUri is { } finalUri && finalUri != requestUri)
                throw new ProviderFetchException(ProviderFetchFailure.Network,
                    "Provider redirected the catalog request.");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new ProviderFetchException(ProviderFetchFailure.HttpStatus,
                    "Provider did not return HTTP 200.", response.StatusCode);
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType,
                    "application/json", StringComparison.OrdinalIgnoreCase))
                throw new ProviderFetchException(ProviderFetchFailure.InvalidContentType,
                    "Provider did not return JSON.");
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
                throw new ProviderFetchException(ProviderFetchFailure.TooLarge,
                    "Provider JSON response exceeds the size limit.");

            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await input.ReadAsync(buffer, timeout.Token);
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
}
