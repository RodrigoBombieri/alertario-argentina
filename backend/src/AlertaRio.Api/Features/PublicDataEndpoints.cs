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
}
