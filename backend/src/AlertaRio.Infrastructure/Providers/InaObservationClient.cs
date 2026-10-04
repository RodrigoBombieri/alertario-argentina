using System.Globalization;
using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

// Returns normalized candidates. Collection and publication have separate gates.
public sealed class InaObservationClient(HttpClient client)
{
    private static readonly Uri OfficialBase = new("https://alerta.ina.gob.ar/a5/");
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(3);
    private const int MaxObservations = 500;

    public async Task<IReadOnlyList<InaObservationResult>> FetchAsync(
        PermittedInaSeries approvedSeries, DateTimeOffset fromUtc, DateTimeOffset toUtc,
        DateTimeOffset ingestedAt, CancellationToken cancellationToken = default)
    {
        if (client.BaseAddress != OfficialBase)
            throw new InvalidOperationException("INA client must use the official A5 base URL.");
        ArgumentNullException.ThrowIfNull(approvedSeries);
        var series = approvedSeries.Context;
        if (series.ExternalSeriesId <= 0 || !series.IsObserved || !series.IsInstantaneous ||
            series.UnitId is null or <= 0 || string.IsNullOrWhiteSpace(series.Unit))
            throw new ArgumentException("An observed instantaneous series with a known unit is required.",
                nameof(series));
        if (fromUtc.Offset != TimeSpan.Zero || toUtc.Offset != TimeSpan.Zero ||
            ingestedAt.Offset != TimeSpan.Zero || fromUtc >= toUtc ||
            fromUtc.Ticks % TimeSpan.TicksPerSecond != 0 ||
            toUtc.Ticks % TimeSpan.TicksPerSecond != 0 ||
            toUtc > ingestedAt || toUtc - fromUtc > MaxWindow)
            throw new ArgumentOutOfRangeException(nameof(toUtc),
                "Use a UTC window of at most three days ending no later than ingestion.");

        var start = Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture));
        var end = Uri.EscapeDataString(toUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture));
        var uri = new Uri(OfficialBase,
            $"obs/puntual/series/{series.ExternalSeriesId}/observaciones?timestart={start}&timeend={end}");
        var json = await ProviderJsonTransport.GetAsync(client, uri, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var rows = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("rows", out var wrappedRows)
                    && wrappedRows.ValueKind == JsonValueKind.Array => wrappedRows,
                _ => throw new JsonException("Unexpected INA observation wrapper.")
            };
            if (rows.GetArrayLength() >= MaxObservations || IsIncomplete(root, rows.GetArrayLength()))
                throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                    "INA observation response may be truncated.");

            var results = InaObservationNormalizer.Normalize(json, series, ingestedAt);
            if (results.Any(result => result.Candidate is { } candidate &&
                (candidate.ObservedStartAt < fromUtc || candidate.ObservedStartAt > toUtc)))
                throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                    "INA observation is outside the requested time window.");
            return results;
        }
        catch (JsonException exception)
        {
            throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                "INA observation response schema changed.", innerException: exception);
        }
    }

    private static bool IsIncomplete(JsonElement root, int count)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("is_last_page", out var lastPage) &&
            lastPage.ValueKind == JsonValueKind.False) return true;
        if (root.TryGetProperty("next_page", out var nextPage) &&
            nextPage.ValueKind != JsonValueKind.Null) return true;
        if (root.TryGetProperty("total", out var total) &&
            (total.ValueKind != JsonValueKind.Number ||
             !total.TryGetInt32(out var expected) || expected != count)) return true;
        return false;
    }
}
