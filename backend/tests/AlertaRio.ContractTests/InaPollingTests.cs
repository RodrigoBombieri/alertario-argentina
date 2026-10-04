using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Providers;
using AlertaRio.Worker;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaPollingTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Collection_accepts_rights_review_without_pretending_hydrology_is_approved()
    {
        var candidate = new InaSeriesCandidate(31, 21, 4, 2, "H", "Altura", 5,
            "directa", 3, "m", new InaTimeSupport(0, 0, 0, 0, 0, 0, 0), null, null);
        var selection = new InaCollectionSelection(31, 21, 4, "H", 5, "directa", 3,
            "m", candidate.TimeSupport, InaSeriesDataKind.Observed,
            "candidate-v1", "rights-verified");

        Assert.Equal(31, InaCollectionGate.Select(candidate, selection)
            .Context.ExternalSeriesId);
        Assert.Throws<InvalidDataException>(() =>
            InaSeriesApprovalGate.Select(candidate,
                new InaSeriesApproval(31, 21, 4, "H", 5, "directa", 3,
                    "m", candidate.TimeSupport, InaSeriesDataKind.Observed,
                    "candidate-v1", "rights-verified", string.Empty)));
        Assert.Throws<InvalidDataException>(() =>
            InaCollectionGate.Select(candidate with { Unit = "cm" }, selection));
    }

    [Fact]
    public void Poll_window_starts_with_one_day_and_catches_up_without_skipping()
    {
        var first = InaPollingWindow.Plan(null, Now);
        Assert.Equal(Now.AddDays(-1), first.From);
        Assert.Equal(Now, first.To);
        Assert.False(first.CatchingUp);

        var checkpoint = new IngestionCheckpoint(Now.AddDays(-10).ToString("O"),
            "complete", 1, null);
        var catchUp = InaPollingWindow.Plan(checkpoint, Now);
        Assert.Equal(Now.AddDays(-11), catchUp.From);
        Assert.Equal(Now.AddDays(-8), catchUp.To);
        Assert.True(catchUp.CatchingUp);
        var next = InaPollingWindow.Plan(checkpoint with
        {
            Cursor = catchUp.To.ToString("O")
        }, Now);
        Assert.Equal(catchUp.To.AddDays(-1), next.From);
        Assert.Equal(catchUp.To.AddDays(2), next.To);
    }

    [Fact]
    public void Invalid_or_future_cursor_and_unacknowledged_activation_fail_closed()
    {
        Assert.Throws<InvalidDataException>(() => InaPollingWindow.Plan(
            new IngestionCheckpoint(Now.AddHours(1).ToString("O"),
                "complete", 1, null), Now));
        Assert.Throws<InvalidDataException>(() => InaPollingWindow.Plan(
            new IngestionCheckpoint("not-a-time", "complete", 1, null), Now));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["InaIngestion:Enabled"] = "true",
                ["InaIngestion:ActivationAcknowledged"] = "false"
            }).Build();
        Assert.Throws<InvalidOperationException>(() =>
            InaPollingOptions.Read(configuration));
    }
}
