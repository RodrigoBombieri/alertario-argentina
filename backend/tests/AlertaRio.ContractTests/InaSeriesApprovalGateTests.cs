using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaSeriesApprovalGateTests
{
    private static readonly InaTimeSupport Instant = new(0, 0, 0, 0, 0, 0, 0);
    private static readonly InaSeriesCandidate Candidate = new(
        31, 21, 4, 2, "H", "Altura", 5, "medición directa", 3, "m",
        Instant, null, null);
    private static readonly InaSeriesApproval Approval = new(
        31, 21, 4, "H", 5, "medición directa", 3, "m", Instant,
        InaSeriesDataKind.Observed, "synthetic-v1", "synthetic-rights-review",
        "synthetic-hydrology-review");

    [Fact]
    public void Only_an_exactly_reviewed_observed_series_can_be_selected()
    {
        var selected = InaSeriesApprovalGate.Select(Candidate, Approval);

        Assert.Equal(31, selected.Context.ExternalSeriesId);
        Assert.Equal(3, selected.Context.UnitId);
        Assert.True(selected.Context.IsObserved);
        Assert.True(selected.Context.IsInstantaneous);
    }

    [Fact]
    public void Simulated_aggregated_daily_or_changed_metadata_cannot_be_selected()
    {
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate, Approval with { DataKind = InaSeriesDataKind.Simulated }));
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate, Approval with { DataKind = InaSeriesDataKind.Aggregated }));
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate with { TimeSupport = new InaTimeSupport(0, 0, 1, 0, 0, 0, 0) },
            Approval));
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate with { UnitId = 4 }, Approval));
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate with { NetworkId = 9 }, Approval));
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate, Approval with { HydrologyDecisionId = "" }));
        Assert.Throws<InvalidDataException>(() => InaSeriesApprovalGate.Select(
            Candidate, Approval with { ApprovalVersion = "" }));
    }
}
