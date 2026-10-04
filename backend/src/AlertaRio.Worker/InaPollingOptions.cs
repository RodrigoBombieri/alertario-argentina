using Microsoft.Extensions.Configuration;
using AlertaRio.Infrastructure.Providers;

namespace AlertaRio.Worker;

public sealed record InaPollingOptions(
    Guid InternalSeriesId, InaCollectionSelection Selection,
    TimeSpan PollInterval)
{
    public static InaPollingOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("InaIngestion");
        if (!section.GetValue<bool>("ActivationAcknowledged"))
            throw new InvalidOperationException(
                "INA collection requires explicit activation after reviewing source rights and quota.");
        if (!Guid.TryParse(section["InternalSeriesId"], out var internalId) ||
            internalId == Guid.Empty)
            throw new InvalidOperationException("INA collection requires InternalSeriesId.");
        var approval = new InaCollectionSelection(
            RequiredInt(section, "ExternalSeriesId"),
            RequiredInt(section, "ExternalStationId"),
            RequiredInt(section, "NetworkId"),
            Required(section, "VariableCode"),
            OptionalInt(section, "ProcedureId"),
            Required(section, "ProcedureName"),
            RequiredInt(section, "UnitId"),
            Required(section, "Unit"),
            new InaTimeSupport(0, 0, 0, 0, 0, 0, 0),
            InaSeriesDataKind.Observed,
            Required(section, "SelectionVersion"),
            Required(section, "RightsDecisionId"));
        var interval = section.GetValue<int?>("PollIntervalHours") ?? 24;
        if (interval is < 1 or > 24)
            throw new InvalidOperationException("INA poll interval must be 1–24 hours.");
        return new InaPollingOptions(internalId, approval,
            TimeSpan.FromHours(interval));
    }

    private static string Required(IConfiguration section, string name) =>
        !string.IsNullOrWhiteSpace(section[name]) ? section[name]!.Trim() :
            throw new InvalidOperationException($"INA collection requires {name}.");

    private static int RequiredInt(IConfiguration section, string name) =>
        int.TryParse(section[name], out var value) && value > 0 ? value :
            throw new InvalidOperationException($"INA collection requires positive {name}.");

    private static int? OptionalInt(IConfiguration section, string name) =>
        string.IsNullOrWhiteSpace(section[name]) ? null :
            int.TryParse(section[name], out var value) && value > 0 ? value :
                throw new InvalidOperationException($"INA collection has invalid {name}.");
}
