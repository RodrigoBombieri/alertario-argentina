namespace AlertaRio.Application.PublicData;

public sealed record ListResponse<T>(bool Synthetic, IReadOnlyList<T> Items, string? NextCursor);

public sealed record LocationDto(string Id, string Name, string ProvinceId, string ProvinceName, bool Synthetic);

public sealed record StationDto(
    string Id, string Name, string RiverName, string LocationId, string SourceId,
    IReadOnlyList<string> SeriesIds, bool Synthetic);

public sealed record SourceDto(
    string Id, string Name, string Kind, string Attribution, string License, bool Synthetic);

public sealed record MeasurementDto(
    string SeriesId, decimal Value, string Unit, DateTimeOffset ObservedAt,
    DateTimeOffset? SourceUpdatedAt, DateTimeOffset IngestedAt,
    string Quality, string Freshness, string SourceId);

public sealed record ChangeDto(
    int WindowHours, decimal? Delta, string Unit, DateTimeOffset? ReferenceAt,
    int? ActualDurationSeconds, string Method, string Trend, string Availability,
    string? UnavailableReason);

public sealed record ThresholdDto(
    string Id, decimal ReferenceValue, string Unit, string Authority, string Datum,
    DateTimeOffset ValidFrom, string ComparisonStatus);

public sealed record NoticeDto(
    string Id, string Title, string Issuer, DateTimeOffset SentAt, DateTimeOffset ExpiresAt);

public sealed record NoticeCoverageDto(string Status, DateTimeOffset? LastSuccessfulCheckedAt);

public sealed record NoticeListDto(bool Synthetic, IReadOnlyList<NoticeDto> Items, NoticeCoverageDto Coverage);

public sealed record StationSummaryDto(
    string StationId, DateTimeOffset GeneratedAt, bool Synthetic,
    MeasurementDto? Height, MeasurementDto? Discharge, string? DischargeUnavailableReason,
    IReadOnlyList<ChangeDto> Changes, string CalculatedCondition,
    IReadOnlyList<ThresholdDto> OfficialThresholds, IReadOnlyList<NoticeDto> Notices,
    NoticeCoverageDto NoticeCoverage, string MethodologyVersion, string DataVersion);
