namespace AlertaRio.Infrastructure.Providers;

public enum InaSeriesDataKind
{
    Observed,
    Simulated,
    Aggregated
}

// Approval entries must come from a reviewed, versioned allowlist; none ship with the app.
public sealed record InaSeriesApproval(
    int ExternalSeriesId, int ExternalStationId, int NetworkId,
    string VariableCode, int? ProcedureId, string ProcedureName,
    int UnitId, string Unit, InaTimeSupport TimeSupport, InaSeriesDataKind DataKind,
    string ApprovalVersion, string RightsDecisionId, string HydrologyDecisionId);

public sealed record InaCollectionSelection(
    int ExternalSeriesId, int ExternalStationId, int NetworkId,
    string VariableCode, int? ProcedureId, string ProcedureName,
    int UnitId, string Unit, InaTimeSupport TimeSupport, InaSeriesDataKind DataKind,
    string SelectionVersion, string RightsDecisionId);

public class PermittedInaSeries
{
    internal PermittedInaSeries(InaSeriesContext context) => Context = context;

    public InaSeriesContext Context { get; }
}

public sealed class ApprovedInaSeries(InaSeriesContext context) : PermittedInaSeries(context)
{
}

// Collection requires documented source rights and an exact public-series match.
// Hydrological approval remains a separate gate for publication and calculations.
public static class InaCollectionGate
{
    public static PermittedInaSeries Select(
        InaSeriesCandidate candidate, InaCollectionSelection approval)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(approval);
        if (string.IsNullOrWhiteSpace(approval.SelectionVersion) ||
            string.IsNullOrWhiteSpace(approval.RightsDecisionId) ||
            approval.DataKind != InaSeriesDataKind.Observed ||
            !candidate.IsInstantaneous || !approval.TimeSupport.IsInstantaneous ||
            candidate.ExternalSeriesId <= 0 || candidate.ExternalStationId <= 0 ||
            candidate.NetworkId <= 0 || candidate.UnitId <= 0 ||
            candidate.ExternalSeriesId != approval.ExternalSeriesId ||
            candidate.ExternalStationId != approval.ExternalStationId ||
            candidate.NetworkId != approval.NetworkId ||
            candidate.VariableCode != approval.VariableCode ||
            candidate.ProcedureId != approval.ProcedureId ||
            candidate.ProcedureName != approval.ProcedureName ||
            candidate.UnitId != approval.UnitId || candidate.Unit != approval.Unit ||
            candidate.TimeSupport != approval.TimeSupport)
            throw new InvalidDataException(
                "INA series is not an unchanged, rights-reviewed observed instantaneous series.");

        return new PermittedInaSeries(new InaSeriesContext(
            candidate.ExternalSeriesId, candidate.UnitId, candidate.Unit,
            IsObserved: true, IsInstantaneous: true));
    }
}

public static class InaSeriesApprovalGate
{
    public static ApprovedInaSeries Select(
        InaSeriesCandidate candidate, InaSeriesApproval approval)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(approval);
        if (string.IsNullOrWhiteSpace(approval.HydrologyDecisionId))
            throw new InvalidDataException(
                "INA series is not an approved, unchanged observed instantaneous series.");
        var permitted = InaCollectionGate.Select(candidate,
            new InaCollectionSelection(approval.ExternalSeriesId,
                approval.ExternalStationId, approval.NetworkId,
                approval.VariableCode, approval.ProcedureId,
                approval.ProcedureName, approval.UnitId, approval.Unit,
                approval.TimeSupport, approval.DataKind,
                approval.ApprovalVersion, approval.RightsDecisionId));
        return new ApprovedInaSeries(permitted.Context);
    }
}
