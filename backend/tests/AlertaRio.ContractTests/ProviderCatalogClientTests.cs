using System.Net;
using System.Text;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class ProviderCatalogClientTests
{
    [Fact]
    public async Task INA_reconstructs_page_urls_and_never_follows_next_page()
    {
        const string first = """
            {"estaciones":[
              {"id":701,"tabla":"red-demo","nombre":"Estación A","rio":"Río de ejemplo",
               "public":true,"red":{"id":1,"public":true},
               "geom":{"type":"Point","coordinates":[-58.25,-31.25]}}
            ],"is_last_page":false,"next_page":"https://example.invalid/steal"}
            """;
        const string second = """
            {"estaciones":[
              {"id":702,"tabla":"red-demo","nombre":"Estación B","rio":"Río de ejemplo",
               "public":true,"red":{"id":1,"public":true},
               "geom":{"type":"Point","coordinates":[-58.5,-31.5]}}
            ],"is_last_page":true}
            """;
        using var handler = new QueueHandler(Json(first), Json(second));
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://alerta.ina.gob.ar/a5/")
        };

        var result = await new InaStationCatalogClient(http).FetchAsync("red-demo", "RIO");

        Assert.Equal(new[] { 701, 702 }, result.Select(candidate => candidate.ExternalId));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, uri => Assert.Equal("alerta.ina.gob.ar", uri.Host));
        Assert.Contains("offset=0", handler.Requests[0].Query);
        Assert.Contains("offset=1", handler.Requests[1].Query);
        Assert.DoesNotContain("example.invalid", handler.Requests[1].AbsoluteUri);
    }

    [Fact]
    public async Task INA_rejects_a_partial_or_non_json_catalog()
    {
        const string incomplete = """
            {"estaciones":[],"is_last_page":false}
            """;
        using var partialHandler = new QueueHandler(Json(incomplete));
        using var partialHttp = InaHttp(partialHandler);
        var partial = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaStationCatalogClient(partialHttp).FetchAsync("red-demo", "RIO"));
        Assert.Equal(ProviderFetchFailure.IncompleteCatalog, partial.Failure);

        using var htmlHandler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>not json</html>", Encoding.UTF8, "text/html")
        });
        using var htmlHttp = InaHttp(htmlHandler);
        var html = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaStationCatalogClient(htmlHttp).FetchAsync("red-demo", "RIO"));
        Assert.Equal(ProviderFetchFailure.InvalidContentType, html.Failure);
    }

    [Fact]
    public async Task GeoRef_only_returns_a_complete_snapshot_of_text_ids()
    {
        const string first = """
            {"cantidad":1,"inicio":0,"total":2,"localidades":[
              {"id":"001","nombre":"Villa Ejemplo","categoria":"Localidad simple",
               "provincia":{"id":"01","nombre":"Provincia de ejemplo"},
               "centroide":{"lat":-31.25,"lon":-58.25}}
            ]}
            """;
        const string second = """
            {"cantidad":1,"inicio":1,"total":2,"localidades":[
              {"id":"002","nombre":"Villa Ejemplo","categoria":"Entidad",
               "provincia":{"id":"01","nombre":"Provincia de ejemplo"},
               "centroide":{"lat":-31.3,"lon":-58.3}}
            ]}
            """;
        using var handler = new QueueHandler(Json(first), Json(second));
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apis.datos.gob.ar/georef/api/v2.0/")
        };

        var snapshot = await new GeoRefCatalogClient(http).FetchAsync();

        Assert.Equal(new[] { "001", "002" },
            snapshot.Localities.Select(locality => locality.ExternalId));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, uri => Assert.Equal("apis.datos.gob.ar", uri.Host));
        Assert.Contains("inicio=0", handler.Requests[0].Query);
        Assert.Contains("inicio=1", handler.Requests[1].Query);
    }

    [Fact]
    public async Task GeoRef_does_not_return_an_incomplete_catalog()
    {
        const string emptyPage = """
            {"cantidad":0,"inicio":0,"total":2,"localidades":[]}
            """;
        using var handler = new QueueHandler(Json(emptyPage));
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apis.datos.gob.ar/georef/api/v2.0/")
        };

        var exception = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new GeoRefCatalogClient(http).FetchAsync());
        Assert.Equal(ProviderFetchFailure.IncompleteCatalog, exception.Failure);
    }

    [Fact]
    public async Task Provider_status_and_response_size_fail_with_typed_errors()
    {
        using var rateHandler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var rateHttp = InaHttp(rateHandler);
        var rate = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaStationCatalogClient(rateHttp).FetchAsync("red-demo", "RIO"));
        Assert.Equal(ProviderFetchFailure.HttpStatus, rate.Failure);
        Assert.Equal(HttpStatusCode.TooManyRequests, rate.StatusCode);
        Assert.Equal(2, rateHandler.Requests.Count);

        using var large = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[1_048_577])
        };
        large.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        using var largeHandler = new QueueHandler(large);
        using var largeHttp = InaHttp(largeHandler);
        var size = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaStationCatalogClient(largeHttp).FetchAsync("red-demo", "RIO"));
        Assert.Equal(ProviderFetchFailure.TooLarge, size.Failure);
    }

    [Fact]
    public async Task Transient_status_is_retried_once_with_the_same_url()
    {
        var unavailable = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        unavailable.Headers.RetryAfter =
            new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
        const string complete = """
            {"estaciones":[
              {"id":701,"tabla":"red-demo","nombre":"Estación A","rio":"Río de ejemplo",
               "public":true,"red":{"id":1,"public":true},
               "geom":{"type":"Point","coordinates":[-58.25,-31.25]}}
            ],"is_last_page":true}
            """;
        using var handler = new QueueHandler(unavailable, Json(complete));
        using var http = InaHttp(handler);

        var result = await new InaStationCatalogClient(http).FetchAsync("red-demo", "RIO");

        Assert.Single(result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0], handler.Requests[1]);
    }

    [Fact]
    public async Task Long_retry_after_and_redirects_are_not_followed()
    {
        var rateLimited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimited.Headers.RetryAfter =
            new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
        using var rateHandler = new QueueHandler(rateLimited);
        using var rateHttp = InaHttp(rateHandler);
        var rate = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaStationCatalogClient(rateHttp).FetchAsync("red-demo", "RIO"));
        Assert.Equal(ProviderFetchFailure.HttpStatus, rate.Failure);
        Assert.Single(rateHandler.Requests);

        var redirect = new HttpResponseMessage(HttpStatusCode.Found);
        redirect.Headers.Location = new Uri("https://example.invalid/other");
        using var redirectHandler = new QueueHandler(redirect);
        using var redirectHttp = InaHttp(redirectHandler);
        var moved = await Assert.ThrowsAsync<ProviderFetchException>(() =>
            new InaStationCatalogClient(redirectHttp).FetchAsync("red-demo", "RIO"));
        Assert.Equal(ProviderFetchFailure.Redirect, moved.Failure);
        Assert.Single(redirectHandler.Requests);
    }

    [Fact]
    public async Task A_transient_network_error_is_retried_once()
    {
        using var handler = new FailOnceHandler();
        using var http = InaHttp(handler);

        var result = await new InaSeriesCatalogClient(http).FetchAsync(21);

        Assert.Empty(result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task A_non_official_base_is_rejected_before_any_request()
    {
        using var handler = new QueueHandler(Json("{}"));
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.invalid/a5/")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new InaStationCatalogClient(http).FetchAsync("red-demo", "RIO"));
        Assert.Empty(handler.Requests);
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

    private sealed class FailOnceHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
                throw new HttpRequestException("Temporary network error.");
            return Task.FromResult(Json("{\"rows\":[]}"));
        }
    }
}
