using System.Globalization;
using AlertaRio.Infrastructure.Ingestion;

namespace AlertaRio.Worker;

public sealed record InaPollingWindow(
    DateTimeOffset From, DateTimeOffset To, bool CatchingUp)
{
    public static InaPollingWindow Plan(
        IngestionCheckpoint? checkpoint, DateTimeOffset now)
    {
        var end = new DateTimeOffset(now.ToUniversalTime().Ticks /
            TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var start = end.AddDays(-1);
        if (checkpoint?.Cursor is { } cursor)
        {
            if (!DateTimeOffset.TryParse(cursor, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed) ||
                parsed.Offset != TimeSpan.Zero || parsed > end)
                throw new InvalidDataException("INA checkpoint cursor is invalid.");
            start = parsed.AddDays(-1);
        }
        var to = start.AddDays(3) < end ? start.AddDays(3) : end;
        return new InaPollingWindow(start, to, to < end);
    }
}
