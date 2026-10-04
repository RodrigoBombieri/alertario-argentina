using System.Globalization;
using System.Text.RegularExpressions;
using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;

namespace AlertaRio.Api.Features;

internal static class PublicDataEndpoints
{
    public static void MapPublicDataEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/v1");

        api.MapGet("/locations", (string? query, int? limit, IPublicDataReader reader,
            HttpContext context) =>
        {
            if (!reader.IsConfigured) return Unconfigured(context);
            if (query is null || query.Length is < 2 or > 80)
                return ApiProblems.Create(context, 400, "invalidQuery", "Query must have 2 to 80 characters.");
            if (limit is < 1 or > 50)
                return ApiProblems.Create(context, 400, "invalidLimit", "Limit must be between 1 and 50.");
            return Results.Ok(new ListResponse<LocationDto>(reader.IsSynthetic,
                reader.SearchLocations(query, limit ?? 20), null));
        })
        .WithName("SearchLocations")
        .Produces<ListResponse<LocationDto>>()
        .ProducesProblem(400)
        .ProducesProblem(503);

        api.MapGet("/locations/{id}/stations", (string id, IPublicDataReader reader,
            HttpContext context) =>
        {
            if (!reader.IsConfigured) return Unconfigured(context);
            if (reader.GetLocation(id) is null)
                return ApiProblems.Create(context, 404, "locationNotFound", "Location not found.");
            return Results.Ok(new ListResponse<StationDto>(reader.IsSynthetic,
                reader.GetStationsForLocation(id), null));
        })
        .WithName("GetLocationStations")
        .Produces<ListResponse<StationDto>>()
        .ProducesProblem(404)
        .ProducesProblem(503);

        api.MapGet("/stations", (int? limit, IPublicDataReader reader, HttpContext context) =>
        {
            if (!reader.IsConfigured) return Unconfigured(context);
            if (limit is < 1 or > 50)
                return ApiProblems.Create(context, 400, "invalidLimit", "Limit must be between 1 and 50.");
            return Results.Ok(new ListResponse<StationDto>(reader.IsSynthetic,
                reader.ListStations(limit ?? 20), null));
        })
        .WithName("ListStations")
        .Produces<ListResponse<StationDto>>()
        .ProducesProblem(400)
        .ProducesProblem(503);

        api.MapGet("/stations/map", async (string? bbox, int? limit,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!TryParseBoundingBox(bbox, out var box))
                return ApiProblems.Create(context, 400, "invalidBbox",
                    "Bbox must be west,south,east,north in valid coordinates.");
            if (limit is < 1 or > 500)
                return ApiProblems.Create(context, 400, "invalidLimit",
                    "Limit must be between 1 and 500.");
            var mapReader = context.RequestServices.GetService<IPersistedStationMapReader>();
            if (mapReader is null) return Unconfigured(context);
            var stations = await mapReader.GetStationsAsync(box!, limit ?? 200,
                cancellationToken);
            return Results.Ok(new ListResponse<StationMapPointDto>(true, stations, null));
        })
        .WithName("ListStationsInMapBounds")
        .Produces<ListResponse<StationMapPointDto>>()
        .ProducesProblem(400)
        .ProducesProblem(503);

        api.MapGet("/stations/{id}", (string id, IPublicDataReader reader, HttpContext context) =>
        {
            if (!reader.IsConfigured) return Unconfigured(context);
            var station = reader.GetStation(id);
            return station is null
                ? ApiProblems.Create(context, 404, "stationNotFound", "Station not found.")
                : Results.Ok(station);
        })
        .WithName("GetStation")
        .Produces<StationDto>()
        .ProducesProblem(404)
        .ProducesProblem(503);

        api.MapGet("/stations/{id}/summary", async (string id, IPublicDataReader reader,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            var persisted = context.RequestServices.GetService<IPersistedSummaryReader>();
            if (persisted is not null)
            {
                var projected = await persisted.GetSummaryAsync(id, cancellationToken);
                return projected is null
                    ? ApiProblems.Create(context, 404, "stationNotFound", "Station not found.")
                    : Results.Ok(projected);
            }
            if (!reader.IsConfigured) return Unconfigured(context);
            var summary = reader.GetSummary(id);
            return summary is null
                ? ApiProblems.Create(context, 404, "stationNotFound", "Station not found.")
                : Results.Ok(summary);
        })
        .WithName("GetStationSummary")
        .Produces<StationSummaryDto>()
        .ProducesProblem(404)
        .ProducesProblem(503);

        api.MapGet("/series/{id}/recent", async (string id, HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var historyReader = context.RequestServices.GetService<IPersistedHistoryReader>();
            if (historyReader is null) return Unconfigured(context);
            var history = await historyReader.GetRecentAsync(id, cancellationToken);
            return history is null
                ? ApiProblems.Create(context, 404, "seriesNotFound", "Series not found.")
                : Results.Ok(history);
        })
        .WithName("GetRecentSeriesHistory")
        .Produces<SeriesHistoryDto>()
        .ProducesProblem(404)
        .ProducesProblem(503);

        api.MapGet("/series/{id}/history", async (string id, string? from,
            string? to, string? cursor, int? limit, HttpContext context, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseInstant(from, out var start) ||
                !TryParseInstant(to, out var end) || start >= end ||
                end - start > TimeSpan.FromDays(31) || end > clock.GetUtcNow())
                return ApiProblems.Create(context, 400, "invalidRange",
                    "History range must be at most 31 days with explicit UTC offsets.");
            if (limit is < 1 or > 500)
                return ApiProblems.Create(context, 400, "invalidLimit",
                    "Limit must be between 1 and 500.");
            DateTimeOffset? before = null;
            if (cursor is not null)
            {
                if (!TryParseInstant(cursor, out var parsed) ||
                    parsed <= start || parsed > end)
                    return ApiProblems.Create(context, 400, "invalidCursor",
                        "Cursor must be inside the requested range.");
                before = parsed;
            }
            var historyReader = context.RequestServices.GetService<IPersistedHistoryReader>();
            if (historyReader is null) return Unconfigured(context);
            var page = await historyReader.GetPageAsync(id, start, end, before,
                limit ?? 200, cancellationToken);
            return page is null
                ? ApiProblems.Create(context, 404, "seriesNotFound", "Series not found.")
                : Results.Ok(page);
        })
        .WithName("GetSeriesHistoryPage")
        .Produces<SeriesHistoryPageDto>()
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(503);

        api.MapGet("/notices", (string? locationId, IPublicDataReader reader,
            HttpContext context) =>
        {
            if (!reader.IsConfigured) return Unconfigured(context);
            if (string.IsNullOrWhiteSpace(locationId))
                return ApiProblems.Create(context, 400, "invalidLocationId", "Location ID is required.");
            if (reader.GetLocation(locationId) is null)
                return ApiProblems.Create(context, 404, "locationNotFound", "Location not found.");
            return Results.Ok(reader.GetNoticesForLocation(locationId));
        })
        .WithName("ListNotices")
        .Produces<NoticeListDto>()
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(503);

        api.MapGet("/sources", (IPublicDataReader reader, HttpContext context) =>
            reader.IsConfigured
                ? Results.Ok(new ListResponse<SourceDto>(reader.IsSynthetic,
                    reader.ListSources(), null))
                : Unconfigured(context))
        .WithName("ListSources")
        .Produces<ListResponse<SourceDto>>()
        .ProducesProblem(503);
    }

    private static IResult Unconfigured(HttpContext context) =>
        ApiProblems.Create(context, 503, "dataNotConfigured", "No public data source is configured.");

    private static bool TryParseBoundingBox(string? raw, out StationBoundingBox? box)
    {
        box = null;
        var parts = raw?.Split(',', StringSplitOptions.TrimEntries);
        if (parts?.Length != 4) return false;
        var coordinates = new double[4];
        for (var i = 0; i < 4; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out coordinates[i]) ||
                !double.IsFinite(coordinates[i]))
                return false;
        if (coordinates[0] is < -180 or > 180 ||
            coordinates[2] is < -180 or > 180 ||
            coordinates[1] is < -90 or > 90 ||
            coordinates[3] is < -90 or > 90 ||
            coordinates[0] >= coordinates[2] ||
            coordinates[1] >= coordinates[3] ||
            coordinates[2] - coordinates[0] > 10 ||
            coordinates[3] - coordinates[1] > 10)
            return false;
        box = new StationBoundingBox(coordinates[0], coordinates[1],
            coordinates[2], coordinates[3]);
        return true;
    }

    private static bool TryParseInstant(string? raw, out DateTimeOffset value)
    {
        value = default;
        return !string.IsNullOrWhiteSpace(raw) &&
            Regex.IsMatch(raw, @"(?:Z|[+-](?:0\d|1[0-4]):[0-5]\d)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
            DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out value);
    }
}
