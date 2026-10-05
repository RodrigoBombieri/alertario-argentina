using Microsoft.Extensions.Configuration;

namespace AlertaRio.Worker;

public sealed record GeoRefPollingOptions(
    Guid SourceId, string RightsDecisionId, TimeSpan PollInterval)
{
    public static GeoRefPollingOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("GeoRefIngestion");
        if (!section.GetValue<bool>("ActivationAcknowledged"))
            throw new InvalidOperationException(
                "GeoRef import requires explicit activation after source-rights review.");
        if (!Guid.TryParse(section["SourceId"], out var sourceId) ||
            sourceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(section["RightsDecisionId"]))
            throw new InvalidOperationException(
                "GeoRef import requires SourceId and RightsDecisionId.");
        var days = section.GetValue<int?>("PollIntervalDays") ?? 7;
        if (days is < 1 or > 30)
            throw new InvalidOperationException("GeoRef poll interval must be 1–30 days.");
        return new GeoRefPollingOptions(sourceId,
            section["RightsDecisionId"]!.Trim(), TimeSpan.FromDays(days));
    }
}
