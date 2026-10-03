using System.Net;
using System.Text;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaDataClientTests
{
    private static readonly DateTimeOffset IngestedAt =
        new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset From =
        new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To =
        new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly ApprovedInaSeries Series = InaSeriesApprovalGate.Select(
        new InaSeriesCandidate(31, 21, 4, 2, "H", "Altura", 5,
            "medición directa", 3, "m", new InaTimeSupport(0, 0, 0, 0, 0, 0, 0), null, null),
        new InaSeriesApproval(31, 21, 4, "H", 5, "medición directa", 3, "m",
            new InaTimeSupport(0, 0, 0, 0, 0, 0, 0), InaSeriesDataKind.Observed,
            "synthetic-v1", "synthetic-rights-review", "synthetic-hydrology-review"));

    [Fact]
    public async Task Series_are_fetched_once_from_the_filtered_official_endpoint()
    {
        const string json = """
            {"rows":[{"id":31,"tipo":"puntual",
              "estacion":{"id":21,"public":true,"red":{"id":4,"public":true}},
              "var":{"var":"H","timeSupport":{"years":0,"months":0,"days":0,
                    "hours":0,"minutes":0,"seconds":0,"milliseconds":0}},
              "procedimiento":{"nombre":"medición directa"},
              "unidades":{"id":3,"abrev":"m"}}],"total":1}
            """;
        using var handler = new QueueHandler(Json(json));
        using var http = InaHttp(handler);

        var result = await new InaSeriesCatalogClient(http).FetchAsync(21);

        Assert.Equal(31, Assert.Single(result).ExternalSeriesId);
        Assert.Equal("https://alerta.ina.gob.ar/a5/obs/puntual/series?estacion_id=21",
            Assert.Single(handler.Requests).AbsoluteUri);
    }

    [Theory]
    [InlineData("{\"rows\":[],\"total\":2}", ProviderFetchFailure.IncompleteCatalog)]
    [InlineData("{\"rows\":[],\"is_last_page\":false}", ProviderFetchFailure.IncompleteCatalog)]
    [InlineData("{\"rows\":[],\"next_page\":\"https://example.invalid/next\"}", ProviderFetchFailure.IncompleteCatalog)]
    [InlineData("{\"items\":[]}", ProviderFetchFailure.SchemaMismatch)]
    public async Task Series_reject_incomplete_or_unknown_responses(
        string json, ProviderFetchFailure expected)
    {
        using var handler = new QueueHandler(Json(json));
        using var http = InaHttp(handler);

        var error = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaSeriesCatalogClient(http).FetchAsync(21));

        Assert.Equal(expected, error.Failure);
    }

    [Fact]
    public async Task Series_rejects_another_station_and_invalid_requests_without_network()
    {
        const string wrongStation = """
            [{"id":31,"tipo":"puntual",
              "estacion":{"id":22,"public":true,"red":{"id":4,"public":true}},
              "var":{"var":"H","timeSupport":{"years":0,"months":0,"days":0,
                    "hours":0,"minutes":0,"seconds":0,"milliseconds":0}},
              "procedimiento":{"nombre":"medición directa"},
              "unidades":{"id":3,"abrev":"m"}}]
            """;
        using var handler = new QueueHandler(Json(wrongStation));
        using var http = InaHttp(handler);
        var client = new InaSeriesCatalogClient(http);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.FetchAsync(0));
        Assert.Empty(handler.Requests);
        var error = await Assert.ThrowsAsync<ProviderFetchException>(() => client.FetchAsync(21));
        Assert.Equal(ProviderFetchFailure.SchemaMismatch, error.Failure);
    }

    [Fact]
    public async Task Observations_use_a_bounded_utc_range_and_preserve_missing_values()
    {
        const string json = """
            {"rows":[
              {"id":10,"series_id":31,"unit_id":null,"valor":7.48,
               "timestart":"2026-10-01T03:00:00-03:00"},
              {"id":11,"series_id":31,"unit_id":3,"valor":null,
               "timestart":"2026-10-01T07:00:00Z"}],"total":2}
            """;
        using var handler = new QueueHandler(Json(json));
        using var http = InaHttp(handler);

        var results = await new InaObservationClient(http)
            .FetchAsync(Series, From, To, IngestedAt);

        Assert.Equal(new[] { InaObservationStatus.Candidate, InaObservationStatus.Missing },
            results.Select(result => result.Status));
        Assert.Equal(7.48m, results[0].Candidate!.Value);
        Assert.Equal("m", results[0].Candidate!.Unit);
        Assert.Equal("https://alerta.ina.gob.ar/a5/obs/puntual/series/31/observaciones" +
            "?timestart=2026-10-01T00%3A00%3A00Z&timeend=2026-10-02T00%3A00%3A00Z",
            Assert.Single(handler.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task Observations_reject_invalid_windows_before_network()
    {
        using var handler = new QueueHandler();
        using var http = InaHttp(handler);
        var client = new InaObservationClient(http);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.FetchAsync(Series, From, From.AddDays(4), IngestedAt.AddDays(5)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.FetchAsync(Series, From.ToOffset(TimeSpan.FromHours(-3)), To, IngestedAt));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.FetchAsync(Series, From.AddMilliseconds(1), To, IngestedAt));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.FetchAsync(null!, From, To, IngestedAt));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("{\"rows\":[],\"total\":1}", ProviderFetchFailure.IncompleteCatalog)]
    [InlineData("{\"rows\":[],\"is_last_page\":false}", ProviderFetchFailure.IncompleteCatalog)]
    [InlineData("{\"rows\":[],\"next_page\":\"https://example.invalid/next\"}", ProviderFetchFailure.IncompleteCatalog)]
    [InlineData("{\"items\":[]}", ProviderFetchFailure.SchemaMismatch)]
    [InlineData("[{\"id\":10,\"series_id\":31,\"unit_id\":3,\"valor\":1,\"timestart\":\"2026-09-30T00:00:00Z\"}]", ProviderFetchFailure.SchemaMismatch)]
    public async Task Observations_reject_partial_unknown_or_out_of_window_responses(
        string json, ProviderFetchFailure expected)
    {
        using var handler = new QueueHandler(Json(json));
        using var http = InaHttp(handler);

        var error = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaObservationClient(http).FetchAsync(Series, From, To, IngestedAt));

        Assert.Equal(expected, error.Failure);
    }

    private static HttpClient InaHttp(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://alerta.ina.gob.ar/a5/")
    };

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> queue = new(responses);
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri ?? throw new InvalidOperationException());
            if (queue.Count == 0) throw new InvalidOperationException("Unexpected provider call.");
            return Task.FromResult(queue.Dequeue());
        }
    }
}
