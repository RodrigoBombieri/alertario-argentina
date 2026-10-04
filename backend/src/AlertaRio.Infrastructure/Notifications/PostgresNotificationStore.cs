using System.Data;
using AlertaRio.Core.Notifications;
using AlertaRio.Core.Trends;
using Npgsql;

namespace AlertaRio.Infrastructure.Notifications;

// Internal-only transaction boundary. No public installation or push endpoint uses it yet.
public sealed class PostgresNotificationStore(NpgsqlDataSource dataSource)
{
    public async Task<NotificationTransition> EvaluateAsync(
        Guid ruleId, NotificationSignal signal, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (ruleId == Guid.Empty || now.Offset != TimeSpan.Zero ||
            signal.ObservedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Notification evaluation requires IDs and UTC instants.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await using var ruleCommand = new NpgsqlCommand("""
            SELECT r.series_id, r.trigger, r.maximum_age_seconds,
                   r.delivery_ttl_seconds, r.version,
                   r.enabled AND i.revoked_at IS NULL,
                   ms.approved AND d.permission_status = 'approved'
            FROM notification_rules AS r
            JOIN notification_installations AS i ON i.id = r.installation_id
            JOIN measurement_series AS ms ON ms.id = r.series_id
            JOIN data_sources AS d ON d.id = ms.source_id
            WHERE r.id = $1
            FOR UPDATE OF r
            """, connection, transaction);
        ruleCommand.Parameters.Add(new NpgsqlParameter { Value = ruleId });
        Guid seriesId;
        string trigger;
        int maximumAgeSeconds;
        int deliveryTtlSeconds;
        int ruleVersion;
        bool enabled;
        bool sourceApproved;
        await using (var reader = await ruleCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException("Notification rule not found.");
            seriesId = reader.GetGuid(0);
            trigger = reader.GetString(1);
            maximumAgeSeconds = reader.GetInt32(2);
            deliveryTtlSeconds = reader.GetInt32(3);
            ruleVersion = reader.GetInt32(4);
            enabled = reader.GetBoolean(5);
            sourceApproved = reader.GetBoolean(6);
        }
        var condition = trigger switch
        {
            "followUp" => CalculatedCondition.FollowUp,
            "aboveAlertThreshold" => CalculatedCondition.AboveAlertThreshold,
            "aboveEvacuationThreshold" => CalculatedCondition.AboveEvacuationThreshold,
            _ => throw new InvalidDataException("Unknown notification trigger.")
        };
        var rule = new NotificationRule(ruleId, seriesId, condition,
            TimeSpan.FromSeconds(maximumAgeSeconds),
            TimeSpan.FromSeconds(deliveryTtlSeconds), enabled);
        await using var episodeCommand = new NpgsqlCommand("""
            SELECT id, opened_at, rule_version FROM notification_episodes
            WHERE rule_id = $1 AND closed_at IS NULL FOR UPDATE
            """, connection, transaction);
        episodeCommand.Parameters.Add(new NpgsqlParameter { Value = ruleId });
        Guid? episodeId = null;
        NotificationEpisode? active = null;
        await using (var reader = await episodeCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                episodeId = reader.GetGuid(0);
                var openedAt = reader.GetFieldValue<DateTimeOffset>(1);
                if (reader.GetInt32(2) != ruleVersion)
                    throw new InvalidOperationException(
                        "Close the previous rule version before evaluation.");
                active = new NotificationEpisode(ruleId, condition, openedAt);
            }
        }
        var checkedSignal = signal with
        {
            SourceApproved = signal.SourceApproved && sourceApproved
        };
        var decision = NotificationEpisodeEngine.Evaluate(
            rule, active, checkedSignal, now);
        if (decision.Transition == NotificationTransition.Open)
        {
            var newEpisodeId = Guid.NewGuid();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO notification_episodes
                    (id, rule_id, rule_version, opened_at, cause_observed_at, expires_at)
                VALUES ($1, $2, $3, $4, $5, $6)
                """, connection, transaction);
            insert.Parameters.Add(new NpgsqlParameter { Value = newEpisodeId });
            insert.Parameters.Add(new NpgsqlParameter { Value = ruleId });
            insert.Parameters.Add(new NpgsqlParameter { Value = ruleVersion });
            insert.Parameters.Add(new NpgsqlParameter { Value = now });
            insert.Parameters.Add(new NpgsqlParameter { Value = signal.ObservedAt });
            insert.Parameters.Add(new NpgsqlParameter { Value = decision.ExpiresAt!.Value });
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await using var outbox = new NpgsqlCommand("""
                INSERT INTO notification_outbox (id, episode_id, created_at, expires_at)
                VALUES ($1, $2, $3, $4)
                """, connection, transaction);
            outbox.Parameters.Add(new NpgsqlParameter { Value = Guid.NewGuid() });
            outbox.Parameters.Add(new NpgsqlParameter { Value = newEpisodeId });
            outbox.Parameters.Add(new NpgsqlParameter { Value = now });
            outbox.Parameters.Add(new NpgsqlParameter { Value = decision.ExpiresAt!.Value });
            await outbox.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (decision.Transition == NotificationTransition.Close)
        {
            await using var close = new NpgsqlCommand("""
                UPDATE notification_episodes SET closed_at = $2 WHERE id = $1
                """, connection, transaction);
            close.Parameters.Add(new NpgsqlParameter { Value = episodeId!.Value });
            close.Parameters.Add(new NpgsqlParameter { Value = now });
            await close.ExecuteNonQueryAsync(cancellationToken);
            await using var cancel = new NpgsqlCommand("""
                UPDATE notification_outbox SET state = 'cancelled',
                    lease_owner = NULL, lease_until = NULL
                WHERE episode_id = $1 AND state IN ('pending', 'leased')
                """, connection, transaction);
            cancel.Parameters.Add(new NpgsqlParameter { Value = episodeId.Value });
            await cancel.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return decision.Transition;
    }
}
