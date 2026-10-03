using System.Net;
using System.Text.Json;
using AlertaRio.Api;
using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;
using AlertaRio.Core;
using AlertaRio.Infrastructure.Providers;
using AlertaRio.Infrastructure.Synthetic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class PublicApiTests
{
    [Fact]
    public async Task Summary_marks_every_unverified_capability_unavailable()
    {
        await using var api = await RunningApi.StartAsync(synthetic: true);
        using var response = await api.Client.GetAsync("/v1/stations/station-demo/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJson(response);
        var root = json.RootElement;
        Assert.True(root.GetProperty("synthetic").GetBoolean());
        Assert.Equal("station-demo", root.GetProperty("stationId").GetString());
        Assert.Equal("synthetic", root.GetProperty("height").GetProperty("quality").GetString());
        Assert.Equal("notApplicable", root.GetProperty("height").GetProperty("freshness").GetString());
        Assert.Equal("notApplicable", root.GetProperty("dataStatus").GetString());
        Assert.Equal("noApprovedSeries", root.GetProperty("dischargeUnavailableReason").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("discharge").ValueKind);
        Assert.Equal("notConfigured", root.GetProperty("noticeCoverage").GetProperty("status").GetString());
        Assert.Empty(root.GetProperty("notices").EnumerateArray());
        Assert.Empty(root.GetProperty("officialThresholds").EnumerateArray());
        var changes = root.GetProperty("changes").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 3, 6, 12, 24 },
            changes.Select(change => change.GetProperty("windowHours").GetInt32()));
        Assert.All(changes, change =>
        {
            Assert.Equal(JsonValueKind.Null, change.GetProperty("delta").ValueKind);
            Assert.Equal("insufficientObservations", change.GetProperty("unavailableReason").GetString());
        });
    }

    [Fact]
    public async Task Public_routes_validate_inputs_and_report_unknown_resources()
    {
        await using var api = await RunningApi.StartAsync(synthetic: true);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await api.Client.GetAsync("/v1/locations?query=x")).StatusCode);
        using var missing = await api.Client.GetAsync("/v1/stations/not-a-station/summary");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        using var problem = await ReadJson(missing);
        Assert.Equal("stationNotFound", problem.RootElement.GetProperty("code").GetString());

        using var search = await api.Client.GetAsync("/v1/locations?query=ninguna");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var empty = await ReadJson(search);
        Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());

        using var missingLocation = await api.Client.GetAsync("/v1/notices");
        Assert.Equal(HttpStatusCode.BadRequest, missingLocation.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await api.Client.GetAsync("/v1/stations/map?bbox=0,0,11,1"))
            .StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await api.Client.GetAsync("/v1/stations/map?bbox=0,0,1,1"))
            .StatusCode);
        using var notices = await api.Client.GetAsync("/v1/notices?locationId=location-demo");
        Assert.Equal(HttpStatusCode.OK, notices.StatusCode);
        using var noticeBody = await ReadJson(notices);
        Assert.Empty(noticeBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("notConfigured",
            noticeBody.RootElement.GetProperty("coverage").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Production_does_not_expose_the_synthetic_fixture()
    {
        await using var api = await RunningApi.StartAsync(synthetic: false, configured: true);
        using var response = await api.Client.GetAsync("/v1/stations/station-demo/summary");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = await ReadJson(response);
        Assert.Equal("dataNotConfigured", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await api.Client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await api.Client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Persisted_summary_preview_cannot_be_enabled_in_production()
    {
        await using var api = await RunningApi.StartAsync(
            synthetic: false, configured: false, persistedSummary: true);
        using var response = await api.Client.GetAsync("/v1/stations/any/summary");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_exposes_the_minimum_public_contract()
    {
        await using var api = await RunningApi.StartAsync(synthetic: true);
        using var response = await api.Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJson(response);
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/v1/locations", out _));
        Assert.True(paths.TryGetProperty("/v1/stations/{id}/summary", out _));
        Assert.True(paths.TryGetProperty("/v1/stations/map", out _));
        Assert.True(paths.TryGetProperty("/v1/notices", out _));
        Assert.True(paths.TryGetProperty("/v1/sources", out _));
        Assert.False(json.RootElement.TryGetProperty("servers", out _));
        using var committed = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "v1.json")));
        Assert.True(JsonElement.DeepEquals(committed.RootElement, json.RootElement),
            "The checked-in OpenAPI document differs from the running API.");
    }

    [Fact]
    public async Task Changing_an_INA_catalog_wrapper_does_not_change_the_public_station_contract()
    {
        const string observed = """
            {"estaciones":[
              {"id":701,"nombre":"Estación de ejemplo","rio":"Río de ejemplo","public":true,
               "tabla":"red-demo","id_externo":"owner-701",
               "geom":{"type":"Point","coordinates":[-58.25,-31.25]},
               "red":{"id":1,"public":true}},
              {"id":702,"nombre":"Oculta","rio":"Otro río","public":false,
               "red":{"public":true}},
              {"id":703,"nombre":"Red oculta","rio":"Otro río","public":true,
               "red":{"public":false}}
            ],"is_last_page":true}
            """;
        const string wrapped = """
            {"rows":[
              {"id":701,"nombre":"Estación de ejemplo","rio":"Río de ejemplo","public":true,
               "tabla":"red-demo","id_externo":"owner-701",
               "geom":{"type":"Point","coordinates":[-58.25,-31.25]},
               "red":{"id":1,"public":true},"extra":"ignored"},
              {"id":702,"nombre":"Oculta","rio":"Otro río","public":false,
               "red":{"public":true}},
              {"id":703,"nombre":"Red oculta","rio":"Otro río","public":true,
               "red":{"public":false}}
            ],"total":3}
            """;

        await using var first = await RunningApi.StartAsync(synthetic: true,
            reader: new CatalogFixtureReader(observed));
        await using var second = await RunningApi.StartAsync(synthetic: true,
            reader: new CatalogFixtureReader(wrapped));
        using var flatResponse = await first.Client.GetAsync("/v1/stations/station-demo");
        using var wrappedResponse = await second.Client.GetAsync("/v1/stations/station-demo");
        Assert.Equal(HttpStatusCode.OK, flatResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, wrappedResponse.StatusCode);
        using var flatBody = await ReadJson(flatResponse);
        using var wrappedBody = await ReadJson(wrappedResponse);
        Assert.True(JsonElement.DeepEquals(flatBody.RootElement, wrappedBody.RootElement));
        Assert.Equal("station-demo", flatBody.RootElement.GetProperty("id").GetString());
        Assert.True(flatBody.RootElement.GetProperty("synthetic").GetBoolean());
        Assert.DoesNotContain("701", flatBody.RootElement.GetRawText());
        Assert.DoesNotContain("Oculta", flatBody.RootElement.GetRawText());
        Assert.DoesNotContain("Red oculta", flatBody.RootElement.GetRawText());
    }

    [Fact]
    public void Core_has_no_application_or_infrastructure_dependency()
    {
        var references = typeof(DataProvenance).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();
        Assert.DoesNotContain("AlertaRio.Application", references);
        Assert.DoesNotContain("AlertaRio.Infrastructure", references);
        Assert.DoesNotContain("AlertaRio.Api", references);
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private sealed class RunningApi(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<RunningApi> StartAsync(
            bool synthetic, bool? configured = null, IPublicDataReader? reader = null,
            bool persistedSummary = false)
        {
            var environment = synthetic ? "Development" : "Production";
            var app = ApiHost.Build(new WebApplicationOptions
            {
                EnvironmentName = environment,
                ApplicationName = typeof(ApiHost).Assembly.GetName().Name
            }, builder =>
            {
                builder.Configuration["SyntheticData:Enabled"] = (configured ?? synthetic).ToString();
                builder.Configuration["PersistedSummary:Enabled"] = persistedSummary.ToString();
            },
                services =>
                {
                    if (reader is null) return;
                    services.RemoveAll<IPublicDataReader>();
                    services.AddSingleton(reader);
                });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("Kestrel did not publish an address.");
            return new RunningApi(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class CatalogFixtureReader(string json) : IPublicDataReader
    {
        private readonly SyntheticPublicDataReader fallback = new(TimeProvider.System);
        private readonly InaStationCandidate candidate =
            Assert.Single(InaStationCandidateParser.Parse(json));

        public bool IsConfigured => true;
        public bool IsSynthetic => true;
        public IReadOnlyList<LocationDto> SearchLocations(string query, int limit) =>
            fallback.SearchLocations(query, limit);
        public LocationDto? GetLocation(string id) => fallback.GetLocation(id);
        public IReadOnlyList<StationDto> GetStationsForLocation(string locationId) =>
            fallback.GetStationsForLocation(locationId);
        public IReadOnlyList<StationDto> ListStations(int limit) => fallback.ListStations(limit);
        public StationDto? GetStation(string id)
        {
            var station = fallback.GetStation(id);
            return station is null ? null : station with
            {
                Name = candidate.Name,
                RiverName = candidate.RiverName
            };
        }
        public StationSummaryDto? GetSummary(string stationId) => fallback.GetSummary(stationId);
        public NoticeListDto GetNoticesForLocation(string locationId) =>
            fallback.GetNoticesForLocation(locationId);
        public IReadOnlyList<SourceDto> ListSources() => fallback.ListSources();
    }
}
