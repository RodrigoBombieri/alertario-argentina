using System.Diagnostics;
using System.Data;
using System.Net;
using System.Text.Json;
using AlertaRio.Api;
using AlertaRio.Core.Trends;
using AlertaRio.Core.Notifications;
using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Notifications;
using AlertaRio.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace AlertaRio.DbIntegrationTests;

public sealed class IngestionRoundTripTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("ALERTARIO_TEST_POSTGRES") ??
        "Host=127.0.0.1;Port=5433;Database=alertario_dev;" +
        "Username=alertario_dev;Password=local_only_change_me";

    [Fact]
    public async Task Storage_health_rejects_a_schema_without_migrations()
    {
        var connection = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            SearchPath = $"missing_{Guid.NewGuid():N}"
        };
        await using var app = ApiHost.Build(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(ApiHost).Assembly.GetName().Name
        }, builder =>
        {
            builder.Configuration["PersistedSummary:Enabled"] = "true";
            builder.Configuration["ConnectionStrings:Ingestion"] = connection.ConnectionString;
        });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("Kestrel did not publish an address.");
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var response = await client.GetAsync("/health/storage");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("schemaIncompatible",
                body.RootElement.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync("/health/live")).StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Notification_episode_and_outbox_survive_replay_and_cancel_pending_delivery()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            var ingestor = new PostgresIngestionStore(dataSource);
            var stream = seriesId.ToString("D");
            var observedAt = now.AddMinutes(-5);
            Assert.True(await ingestor.ClaimLeaseAsync("integration", stream,
                "notification-worker", TimeSpan.FromMinutes(1)));
            Assert.Equal(1, (await ingestor.CommitAsync(new IngestionBatch(
                "integration", stream, "notification-worker", "notification-cursor",
                "complete", now,
                [Record(seriesId, observedAt, 12m, observedAt.AddMinutes(1), 'a')])))
                .Inserted);
            DateTimeOffset persistedObservedAt;
            long persistedLatestVersion;
            await using (var latest = dataSource.CreateCommand("""
                SELECT m.observed_end_at, l.version FROM series_latest AS l
                JOIN measurements AS m ON m.id = l.measurement_id
                WHERE l.series_id = $1
                """))
            {
                latest.Parameters.Add(new NpgsqlParameter { Value = seriesId });
                await using var reader = await latest.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                persistedObservedAt = reader.GetFieldValue<DateTimeOffset>(0);
                persistedLatestVersion = reader.GetInt64(1);
            }
            await using (var seed = dataSource.CreateBatch())
            {
                Add(seed, """
                    INSERT INTO notification_installations
                        (id, credential_hash, platform, consent_version)
                    VALUES ($1, $2, 'android', 'synthetic-v1')
                    """, installationId, Enumerable.Repeat((byte)0xAB, 32).ToArray());
                Add(seed, """
                    INSERT INTO notification_rules
                        (id, installation_id, series_id, trigger,
                         maximum_age_seconds, delivery_ttl_seconds)
                    VALUES ($1, $2, $3, 'aboveAlertThreshold', 3600, 900)
                    """, ruleId, installationId, seriesId);
                await seed.ExecuteNonQueryAsync();
            }
            var store = new PostgresNotificationStore(dataSource);
            var signal = new NotificationSignal(seriesId, persistedObservedAt,
                DataStatus.Current, CalculatedCondition.AboveAlertThreshold,
                true, true, false, persistedLatestVersion);
            Assert.Equal(NotificationTransition.None,
                await store.EvaluateAsync(ruleId,
                    signal with { LatestVersion = persistedLatestVersion + 1 }, now));
            Assert.Equal(NotificationTransition.None,
                await store.EvaluateAsync(ruleId,
                    signal with { ObservedAt = now.AddMinutes(-6) }, now));
            var competingEvaluations = await Task.WhenAll(
                store.EvaluateAsync(ruleId, signal, now),
                store.EvaluateAsync(ruleId, signal, now));
            Assert.Single(competingEvaluations,
                transition => transition == NotificationTransition.Open);
            Assert.Single(competingEvaluations,
                transition => transition == NotificationTransition.None);
            Assert.Equal(NotificationTransition.None,
                await store.EvaluateAsync(ruleId, signal, now));
            await using (var count = dataSource.CreateCommand("""
                SELECT count(*) FROM notification_outbox AS o
                JOIN notification_episodes AS e ON e.id = o.episode_id
                WHERE e.rule_id = $1 AND o.state = 'pending'
                """))
            {
                count.Parameters.Add(new NpgsqlParameter { Value = ruleId });
                Assert.Equal(1L, await count.ExecuteScalarAsync());
            }
            Assert.Equal(NotificationTransition.Close,
                await store.EvaluateAsync(ruleId,
                    signal with { Condition = CalculatedCondition.NoNotableChange }, now));
            Assert.Equal(NotificationTransition.None,
                await store.EvaluateAsync(ruleId,
                    signal with { IsBackfill = true }, now));
            await using (var cancelled = dataSource.CreateCommand("""
                SELECT count(*) FROM notification_outbox AS o
                JOIN notification_episodes AS e ON e.id = o.episode_id
                WHERE e.rule_id = $1 AND o.state = 'cancelled'
                """))
            {
                cancelled.Parameters.Add(new NpgsqlParameter { Value = ruleId });
                Assert.Equal(1L, await cancelled.ExecuteScalarAsync());
            }
            await RevokeSourceAsync(dataSource, sourceId);
            Assert.Equal(NotificationTransition.None,
                await store.EvaluateAsync(ruleId, signal, now));
        }
        finally
        {
            await using var cleanup = dataSource.CreateBatch();
            Add(cleanup, "DELETE FROM notification_outbox WHERE episode_id IN " +
                "(SELECT id FROM notification_episodes WHERE rule_id = $1)", ruleId);
            Add(cleanup, "DELETE FROM notification_episodes WHERE rule_id = $1", ruleId);
            Add(cleanup, "DELETE FROM notification_rules WHERE id = $1", ruleId);
            Add(cleanup, "DELETE FROM notification_installations WHERE id = $1",
                installationId);
            await cleanup.ExecuteNonQueryAsync();
            await CleanupAsync(dataSource, sourceId, stationId, seriesId,
                seriesId.ToString("D"));
        }
    }

    [Fact]
    public async Task Writer_and_trend_reader_survive_replay_correction_and_failed_batch()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var stream = seriesId.ToString("D");
        var now = DateTimeOffset.UtcNow;
        var currentAt = now.AddMinutes(-10);
        var oldAt = currentAt.AddHours(-6);
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            var store = new PostgresIngestionStore(dataSource);
            var trendReader = new PostgresTrendReader(dataSource);
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "worker-a",
                TimeSpan.FromMinutes(1)));
            Assert.False(await store.ClaimLeaseAsync("integration", stream, "worker-b",
                TimeSpan.FromMinutes(1)));

            var first = Record(seriesId, oldAt, 7.20m, oldAt.AddMinutes(1), 'a');
            var second = Record(seriesId, currentAt, 7.50m, currentAt.AddMinutes(1), 'b');
            var batch = new IngestionBatch("integration", stream, "worker-a", "cursor-1",
                "complete", now, [first, second]);
            var inserted = await store.CommitAsync(batch);
            Assert.Equal(2, inserted.Inserted);
            Assert.Equal(2, (await store.CommitAsync(batch with { Cursor = "cursor-2" })).Unchanged);
            var configuration = await new PostgresTrendPolicyReader(dataSource)
                .ReadAsync(seriesId, now);
            Assert.NotNull(configuration);
            Assert.Single(configuration.Thresholds);
            var initialTrend = await trendReader.ReadAsync(seriesId, configuration.Policy, now);
            Assert.NotNull(initialTrend);
            Assert.Equal(0.30m, Assert.Single(initialTrend.Windows,
                window => window.WindowHours == 6).Delta);
            var level = new CurrentLevel(seriesId, initialTrend.LatestObservedAt!.Value, 7.50m,
                "m", "integration-datum", 1, true);
            var followUp = CalculatedStateEngine.Evaluate(initialTrend, level,
                configuration.Thresholds,
                new FollowUpRule(6, 0.20m, true), [], NoticeCoverage.Unavailable, now);
            Assert.Equal(CalculatedCondition.FollowUp, followUp.Condition);

            var corrected = Record(seriesId, oldAt, 7.30m, oldAt.AddMinutes(2), 'c');
            var revision = await store.CommitAsync(batch with
            {
                Cursor = "cursor-3",
                Records = [corrected]
            });
            Assert.Equal(1, revision.Revised);
            var changedTrend = await trendReader.ReadAsync(seriesId, configuration.Policy, now);
            Assert.NotNull(changedTrend);
            Assert.Equal(0.20m, Assert.Single(changedTrend.Windows,
                window => window.WindowHours == 6).Delta);

            await Assert.ThrowsAnyAsync<Exception>(() => store.CommitAsync(batch with
            {
                Cursor = "bad-cursor",
                Records = [Record(seriesId, oldAt, 7.40m,
                    oldAt.AddMinutes(3), 'd'), first with { PayloadHash = "bad" }]
            }));
            Assert.Equal("cursor-3", (await store.ReadCheckpointAsync("integration", stream))?.Cursor);
            Assert.Equal(0.20m, Assert.Single((await trendReader.ReadAsync(
                seriesId, configuration.Policy, now))!
                .Windows, window => window.WindowHours == 6).Delta);

            await ExpireLeaseAsync(dataSource, stream);
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "worker-b",
                TimeSpan.FromMinutes(1)));
            Assert.Equal(1, (await store.CommitAsync(batch with
            {
                LeaseOwner = "worker-b",
                Cursor = "cursor-4",
                Records = [corrected]
            })).Unchanged);
            Assert.Equal(4, (await store.ReadCheckpointAsync("integration", stream))?.Version);

            var missing = new IngestionRecord(seriesId, currentAt.AddMinutes(1),
                currentAt.AddMinutes(1), null, currentAt.AddMinutes(2),
                "missing-observation", new string('e', 64));
            var rejected = new RejectedIngestionRecord("bad-observation",
                "normalizer:unitConflict", new string('f', 64));
            var partial = batch with
            {
                LeaseOwner = "worker-b",
                Cursor = "cursor-5",
                Coverage = "partial",
                Records = [missing],
                RejectedRecords = [rejected]
            };
            var partialResult = await store.CommitAsync(partial);
            Assert.Equal(1, partialResult.Inserted);
            Assert.Equal(1, partialResult.Quarantined);
            var replayResult = await store.CommitAsync(partial with { Cursor = "cursor-6" });
            Assert.Equal(1, replayResult.Unchanged);
            Assert.Equal(0, replayResult.Quarantined);
            Assert.Equal(1, await CountMissingAsync(dataSource, seriesId));
            Assert.Equal(1, await CountQuarantinedAsync(dataSource, stream));
            Assert.Equal("partial", (await store.ReadCheckpointAsync("integration", stream))?.Coverage);

            var beforeRejectedRollback = await CountMeasurementsAsync(dataSource, seriesId);
            await Assert.ThrowsAnyAsync<Exception>(() => store.CommitAsync(partial with
            {
                Cursor = "rejected-invalid",
                Records = [Record(seriesId, currentAt.AddMinutes(2), 8.00m,
                    currentAt.AddMinutes(3), '8')],
                RejectedRecords = [new RejectedIngestionRecord("bad-evidence",
                    "unqualifiedReason", new string('f', 64))]
            }));
            Assert.Equal(beforeRejectedRollback,
                await CountMeasurementsAsync(dataSource, seriesId));
            Assert.Equal("cursor-6", (await store.ReadCheckpointAsync("integration", stream))?.Cursor);
            Assert.Equal(1, await CountQuarantinedAsync(dataSource, stream));

            await using var app = ApiHost.Build(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ApplicationName = typeof(ApiHost).Assembly.GetName().Name
            }, builder =>
            {
                builder.Configuration["PersistedSummary:Enabled"] = "true";
                builder.Configuration["ConnectionStrings:Ingestion"] = ConnectionString;
            });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            try
            {
                var server = app.Services.GetRequiredService<IServer>();
                var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                    ?? throw new InvalidOperationException("Kestrel did not publish an address.");
                using var client = new HttpClient { BaseAddress = new Uri(address) };
                using var storage = await client.GetAsync("/health/storage");
                Assert.Equal(HttpStatusCode.OK, storage.StatusCode);
                using var storageBody = JsonDocument.Parse(
                    await storage.Content.ReadAsStringAsync());
                Assert.Equal("ready", storageBody.RootElement.GetProperty("status").GetString());
                using var response = await client.GetAsync($"/v1/stations/{stationId:D}/summary");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("synthetic").GetBoolean());
                Assert.Equal(7.50m,
                    body.RootElement.GetProperty("height").GetProperty("value").GetDecimal());
                var sixHours = Assert.Single(body.RootElement.GetProperty("changes")
                    .EnumerateArray(), change =>
                        change.GetProperty("windowHours").GetInt32() == 6);
                Assert.Equal(0.20m, sixHours.GetProperty("delta").GetDecimal());
                using var historyResponse = await client.GetAsync(
                    $"/v1/series/{seriesId:D}/recent");
                Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
                using var history = JsonDocument.Parse(
                    await historyResponse.Content.ReadAsStringAsync());
                var points = history.RootElement.GetProperty("points").EnumerateArray()
                    .ToArray();
                Assert.Equal(2, points.Length);
                Assert.Equal(7.30m, points[0].GetProperty("value").GetDecimal());
                Assert.Equal(7.50m, points[1].GetProperty("value").GetDecimal());
                var range = $"from={Uri.EscapeDataString(oldAt.AddMinutes(-1).ToString("O"))}" +
                    $"&to={Uri.EscapeDataString(now.ToString("O"))}&limit=1";
                using var pageOneResponse = await client.GetAsync(
                    $"/v1/series/{seriesId:D}/history?{range}");
                Assert.Equal(HttpStatusCode.OK, pageOneResponse.StatusCode);
                using var pageOne = JsonDocument.Parse(
                    await pageOneResponse.Content.ReadAsStringAsync());
                Assert.Equal(7.50m, Assert.Single(pageOne.RootElement
                    .GetProperty("points").EnumerateArray()).GetProperty("value").GetDecimal());
                var newestPoint = Assert.Single(pageOne.RootElement
                    .GetProperty("points").EnumerateArray());
                Assert.Equal(1, newestPoint.GetProperty("revision").GetInt32());
                Assert.NotEqual(JsonValueKind.Null,
                    newestPoint.GetProperty("sourceUpdatedAt").ValueKind);
                Assert.NotEqual(JsonValueKind.Null,
                    newestPoint.GetProperty("ingestedAt").ValueKind);
                var cursor = pageOne.RootElement.GetProperty("nextCursor").GetString();
                Assert.NotNull(cursor);
                using var pageTwoResponse = await client.GetAsync(
                    $"/v1/series/{seriesId:D}/history?{range}&cursor={Uri.EscapeDataString(cursor)}");
                using var pageTwo = JsonDocument.Parse(
                    await pageTwoResponse.Content.ReadAsStringAsync());
                Assert.Equal(7.30m, Assert.Single(pageTwo.RootElement
                    .GetProperty("points").EnumerateArray()).GetProperty("value").GetDecimal());
                Assert.Equal(JsonValueKind.Null, pageTwo.RootElement
                    .GetProperty("nextCursor").ValueKind);
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.GetAsync($"/v1/series/{seriesId:D}/history?" +
                        "from=2026-01-01T00:00:00&to=2026-01-02T00:00:00Z")).StatusCode);
                Assert.Equal("notConfigured", body.RootElement.GetProperty("noticeCoverage")
                    .GetProperty("status").GetString());
                Assert.Empty(body.RootElement.GetProperty("notices").EnumerateArray());
                Assert.Equal(HttpStatusCode.ServiceUnavailable,
                    (await client.GetAsync("/v1/stations")).StatusCode);
                Assert.Equal(HttpStatusCode.ServiceUnavailable,
                    (await client.GetAsync("/health/ready")).StatusCode);

                var high = Record(seriesId, currentAt.AddMinutes(5), 12.50m,
                    currentAt.AddMinutes(6), '9');
                Assert.Equal(1, (await store.CommitAsync(batch with
                {
                    LeaseOwner = "worker-b",
                    Cursor = "cursor-7",
                    Coverage = "partial",
                    Records = [high]
                })).Inserted);
                using var highResponse = await client.GetAsync(
                    $"/v1/stations/{stationId:D}/summary");
                Assert.Equal(HttpStatusCode.OK, highResponse.StatusCode);
                using var highBody = JsonDocument.Parse(
                    await highResponse.Content.ReadAsStringAsync());
                Assert.Equal("unavailable", highBody.RootElement
                    .GetProperty("calculatedCondition").GetString());
                Assert.Equal("qualityReview", highBody.RootElement
                    .GetProperty("dataStatus").GetString());
                Assert.Equal("unavailable", Assert.Single(highBody.RootElement
                    .GetProperty("officialThresholds").EnumerateArray())
                    .GetProperty("comparisonStatus").GetString());
                Assert.Empty(highBody.RootElement.GetProperty("notices").EnumerateArray());
                Assert.Equal("notConfigured", highBody.RootElement
                    .GetProperty("noticeCoverage").GetProperty("status").GetString());

                await using (var quarantineIdCommand = dataSource.CreateCommand(
                    "SELECT id FROM quarantined_records " +
                    "WHERE provider = $1 AND stream_key = $2 AND review_status = 'open'"))
                {
                    quarantineIdCommand.Parameters.Add(new NpgsqlParameter { Value = "integration" });
                    quarantineIdCommand.Parameters.Add(new NpgsqlParameter { Value = stream });
                    var quarantineId = (long)(await quarantineIdCommand.ExecuteScalarAsync()
                        ?? throw new InvalidOperationException("Missing quarantine fixture."));
                    await using var reviewCommand = dataSource.CreateCommand(
                        "SELECT review_quarantined_record($1, $2, $3, $4)");
                    reviewCommand.Parameters.Add(new NpgsqlParameter { Value = quarantineId });
                    reviewCommand.Parameters.Add(new NpgsqlParameter { Value = "dismissed" });
                    reviewCommand.Parameters.Add(new NpgsqlParameter { Value = "integration-test" });
                    reviewCommand.Parameters.Add(new NpgsqlParameter
                    {
                        Value = "Synthetic rejected fixture inspected"
                    });
                    Assert.True((bool)(await reviewCommand.ExecuteScalarAsync() ?? false));
                    Assert.False((bool)(await reviewCommand.ExecuteScalarAsync() ?? false));
                }
                using var stillPartialResponse = await client.GetAsync(
                    $"/v1/stations/{stationId:D}/summary");
                using var stillPartialBody = JsonDocument.Parse(
                    await stillPartialResponse.Content.ReadAsStringAsync());
                Assert.Equal("qualityReview", stillPartialBody.RootElement
                    .GetProperty("dataStatus").GetString());

                Assert.Equal(1, (await store.CommitAsync(batch with
                {
                    LeaseOwner = "worker-b",
                    Cursor = "cursor-8",
                    Coverage = "complete",
                    Records = [high]
                })).Unchanged);
                using var reviewedResponse = await client.GetAsync(
                    $"/v1/stations/{stationId:D}/summary");
                Assert.Equal(HttpStatusCode.OK, reviewedResponse.StatusCode);
                using var reviewedBody = JsonDocument.Parse(
                    await reviewedResponse.Content.ReadAsStringAsync());
                Assert.NotEqual("qualityReview", reviewedBody.RootElement
                    .GetProperty("dataStatus").GetString());

                await RevokeSourceAsync(dataSource, sourceId);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await client.GetAsync($"/v1/stations/{stationId:D}/summary")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await client.GetAsync($"/v1/series/{seriesId:D}/recent"))
                    .StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await client.GetAsync($"/v1/series/{seriesId:D}/history?{range}"))
                    .StatusCode);
            }
            finally
            {
                await app.StopAsync();
            }

            Assert.Null(await trendReader.ReadAsync(seriesId, configuration.Policy, now));
            Assert.Null(await new PostgresTrendPolicyReader(dataSource)
                .ReadAsync(seriesId, now));
        }
        finally
        {
            await CleanupAsync(dataSource, sourceId, stationId, seriesId, stream);
        }
    }

    [Fact]
    public async Task Concurrent_workers_cannot_claim_the_same_live_stream()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresIngestionStore(dataSource);
        var stream = $"race-{Guid.NewGuid():N}";
        try
        {
            var claims = await Task.WhenAll(
                store.ClaimLeaseAsync("integration", stream, "racer-a", TimeSpan.FromMinutes(1)),
                store.ClaimLeaseAsync("integration", stream, "racer-b", TimeSpan.FromMinutes(1)));
            Assert.Single(claims, claimed => claimed);
            var checkpoint = await store.ReadCheckpointAsync("integration", stream);
            Assert.NotNull(checkpoint);
            Assert.Equal(0, checkpoint.Version);
        }
        finally
        {
            await using var command = dataSource.CreateCommand(
                "DELETE FROM ingestion_checkpoints WHERE provider = 'integration' AND stream_key = $1");
            command.Parameters.Add(new NpgsqlParameter { Value = stream });
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Renewal_requires_a_live_lease_owned_by_the_same_worker()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresIngestionStore(dataSource);
        var stream = $"renew-{Guid.NewGuid():N}";
        try
        {
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "worker-a",
                TimeSpan.FromSeconds(5)));
            var firstUntil = (await store.ReadCheckpointAsync("integration", stream))?.LeaseUntil;
            Assert.NotNull(firstUntil);
            Assert.False(await store.RenewLeaseAsync("integration", stream, "worker-b",
                TimeSpan.FromMinutes(1)));
            Assert.True(await store.RenewLeaseAsync("integration", stream, "worker-a",
                TimeSpan.FromMinutes(1)));
            Assert.True((await store.ReadCheckpointAsync("integration", stream))?.LeaseUntil
                > firstUntil);
            Assert.False(await store.ClaimLeaseAsync("integration", stream, "worker-b",
                TimeSpan.FromMinutes(1)));

            await ExpireLeaseAsync(dataSource, stream);
            Assert.False(await store.RenewLeaseAsync("integration", stream, "worker-a",
                TimeSpan.FromMinutes(1)));
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "worker-b",
                TimeSpan.FromMinutes(1)));
            Assert.False(await store.RenewLeaseAsync("integration", stream, "worker-a",
                TimeSpan.FromMinutes(1)));
        }
        finally
        {
            await using var command = dataSource.CreateCommand(
                "DELETE FROM ingestion_checkpoints WHERE provider = 'integration' AND stream_key = $1");
            command.Parameters.Add(new NpgsqlParameter { Value = stream });
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Trend_readers_share_a_stable_snapshot_during_a_concurrent_revision()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var stream = seriesId.ToString("D");
        var now = DateTimeOffset.UtcNow;
        var currentAt = now.AddMinutes(-5);
        var oldAt = currentAt.AddHours(-6);
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            var store = new PostgresIngestionStore(dataSource);
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "snapshot-worker",
                TimeSpan.FromMinutes(1)));
            var batch = new IngestionBatch("integration", stream, "snapshot-worker",
                "initial", "complete", now,
                [Record(seriesId, oldAt, 7.20m, oldAt.AddMinutes(1), 'a'),
                 Record(seriesId, currentAt, 7.50m, currentAt.AddMinutes(1), 'b')]);
            Assert.Equal(2, (await store.CommitAsync(batch)).Inserted);

            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead);
            var configuration = await PostgresTrendPolicyReader.ReadAsync(
                connection, transaction, seriesId, now);
            Assert.NotNull(configuration);

            var correction = Record(seriesId, oldAt, 7.30m,
                oldAt.AddMinutes(2), 'c');
            Assert.Equal(1, (await store.CommitAsync(batch with
            {
                Cursor = "corrected",
                Records = [correction]
            })).Revised);

            var withinSnapshot = await PostgresTrendReader.ReadAsync(
                connection, transaction, seriesId, configuration.Policy, now);
            Assert.NotNull(withinSnapshot);
            Assert.Equal(0.30m, Assert.Single(withinSnapshot.Windows,
                window => window.WindowHours == 6).Delta);
            await transaction.CommitAsync();

            var afterSnapshot = await new PostgresTrendReader(dataSource)
                .ReadAsync(seriesId, configuration.Policy, now);
            Assert.NotNull(afterSnapshot);
            Assert.Equal(0.20m, Assert.Single(afterSnapshot.Windows,
                window => window.WindowHours == 6).Delta);
        }
        finally
        {
            await CleanupAsync(dataSource, sourceId, stationId, seriesId, stream);
        }
    }

    [Fact]
    public async Task Map_bbox_returns_only_approved_stations_inside_the_bounds()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var stream = seriesId.ToString("D");
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            await using (var point = dataSource.CreateCommand("""
                UPDATE stations
                SET point = ST_SetSRID(ST_MakePoint(-58.25, -31.25), 4326)::geography
                WHERE id = $1
                """))
            {
                point.Parameters.Add(new NpgsqlParameter { Value = stationId });
                await point.ExecuteNonQueryAsync();
            }
            await using (var catalog = dataSource.CreateBatch())
            {
                Add(catalog, """
                    INSERT INTO catalog_snapshots (source_id, version, status, row_count)
                    VALUES ($1, 'map-test', 'complete', 1)
                    """, sourceId);
                Add(catalog, """
                    INSERT INTO locations (id, source_id, external_id, catalog_version,
                        name, normalized_name, category, province_id, province_name)
                    VALUES ($1, $2, 'fixture-location', 'map-test', 'Fixture locality',
                        'fixture locality', 'localidad', 'ER', 'Entre Ríos')
                    """, locationId, sourceId);
                Add(catalog, """
                    INSERT INTO active_catalogs (source_id, catalog_version)
                    VALUES ($1, 'map-test')
                    """, sourceId);
                Add(catalog, """
                    INSERT INTO location_station_associations
                        (id, location_id, station_id, valid_from, status, reason, reviewed_by)
                    VALUES ($1, $2, $3, now() - interval '1 day', 'approved',
                        'Synthetic map association', 'integration-test')
                    """, Guid.NewGuid(), locationId, stationId);
                await catalog.ExecuteNonQueryAsync();
            }
            await using var app = ApiHost.Build(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ApplicationName = typeof(ApiHost).Assembly.GetName().Name
            }, builder =>
            {
                builder.Configuration["PersistedSummary:Enabled"] = "true";
                builder.Configuration["ConnectionStrings:Ingestion"] = ConnectionString;
            });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            try
            {
                var server = app.Services.GetRequiredService<IServer>();
                var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                    ?? throw new InvalidOperationException("Kestrel did not publish an address.");
                using var client = new HttpClient { BaseAddress = new Uri(address) };
                using var response = await client.GetAsync(
                    "/v1/stations/map?bbox=-59,-32,-58,-31");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var station = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
                Assert.Equal(stationId.ToString("D"), station.GetProperty("id").GetString());
                Assert.Equal(-58.25, station.GetProperty("longitude").GetDouble());
                Assert.Equal(-31.25, station.GetProperty("latitude").GetDouble());
                Assert.Equal("Entre Ríos", Assert.Single(station
                    .GetProperty("provinceNames").EnumerateArray()).GetString());
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.GetAsync("/v1/stations/map?bbox=-180,-90,180,90"))
                    .StatusCode);
                using var outside = await client.GetAsync(
                    "/v1/stations/map?bbox=-61,-34,-60,-33");
                Assert.Equal(HttpStatusCode.OK, outside.StatusCode);
                using var empty = JsonDocument.Parse(await outside.Content.ReadAsStringAsync());
                Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());

                await RevokeSourceAsync(dataSource, sourceId);
                using var revoked = await client.GetAsync(
                    "/v1/stations/map?bbox=-59,-32,-58,-31");
                using var revokedBody = JsonDocument.Parse(
                    await revoked.Content.ReadAsStringAsync());
                Assert.Empty(revokedBody.RootElement.GetProperty("items").EnumerateArray());
            }
            finally
            {
                await app.StopAsync();
            }
        }
        finally
        {
            await using var catalogCleanup = dataSource.CreateBatch();
            Add(catalogCleanup, "DELETE FROM location_station_associations " +
                "WHERE station_id = $1", stationId);
            Add(catalogCleanup, "DELETE FROM active_catalogs WHERE source_id = $1", sourceId);
            Add(catalogCleanup, "DELETE FROM locations WHERE source_id = $1", sourceId);
            Add(catalogCleanup, "DELETE FROM catalog_snapshots WHERE source_id = $1", sourceId);
            await catalogCleanup.ExecuteNonQueryAsync();
            await CleanupAsync(dataSource, sourceId, stationId, seriesId, stream);
        }
    }

    [Fact]
    public async Task Lost_connection_before_commit_rolls_back_measurement_and_cursor()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var stream = seriesId.ToString("D");
        var now = DateTimeOffset.UtcNow;
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            var store = new PostgresIngestionStore(dataSource);
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "lost-worker",
                TimeSpan.FromMinutes(1)));
            var record = Record(seriesId, now.AddMinutes(-5), 7.50m,
                now.AddMinutes(-4), 'a');
            var batch = new IngestionBatch("integration", stream, "lost-worker",
                "after-write", "complete", now, [record]);

            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var transaction = await connection.BeginTransactionAsync();
                await using var command = new NpgsqlCommand("""
                    SELECT inserted FROM commit_ingestion_outcomes(
                        $1, $2, $3, $4, $5, $6, $7, $8)
                    """, connection, transaction);
                command.Parameters.Add(new NpgsqlParameter { Value = batch.Provider });
                command.Parameters.Add(new NpgsqlParameter { Value = batch.StreamKey });
                command.Parameters.Add(new NpgsqlParameter { Value = batch.LeaseOwner });
                command.Parameters.Add(new NpgsqlParameter { Value = batch.Cursor! });
                command.Parameters.Add(new NpgsqlParameter { Value = batch.Coverage });
                command.Parameters.Add(new NpgsqlParameter { Value = batch.TransportSucceededAt });
                command.Parameters.Add(new NpgsqlParameter
                {
                    Value = JsonSerializer.Serialize(batch.Records,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    NpgsqlDbType = NpgsqlDbType.Jsonb
                });
                command.Parameters.Add(new NpgsqlParameter
                {
                    Value = "[]",
                    NpgsqlDbType = NpgsqlDbType.Jsonb
                });
                Assert.Equal(1, (int)(await command.ExecuteScalarAsync())!);
                await connection.CloseAsync();
            }

            Assert.Equal(0, await CountMeasurementsAsync(dataSource, seriesId));
            Assert.Null((await store.ReadCheckpointAsync("integration", stream))?.Cursor);
            Assert.Equal(1, (await store.CommitAsync(batch)).Inserted);
            Assert.Equal("after-write",
                (await store.ReadCheckpointAsync("integration", stream))?.Cursor);
        }
        finally
        {
            await CleanupAsync(dataSource, sourceId, stationId, seriesId, stream);
        }
    }

    [Fact]
    public async Task Clean_persisted_stream_can_compare_a_threshold_without_inventing_a_notice()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var stream = seriesId.ToString("D");
        var now = DateTimeOffset.UtcNow;
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            var store = new PostgresIngestionStore(dataSource);
            Assert.True(await store.ClaimLeaseAsync("integration", stream, "clean-worker",
                TimeSpan.FromMinutes(1)));
            var currentAt = now.AddMinutes(-5);
            var batch = new IngestionBatch("integration", stream, "clean-worker",
                "clean-cursor", "complete", now,
                [Record(seriesId, currentAt.AddHours(-6), 10.50m,
                    currentAt.AddHours(-6).AddMinutes(1), 'a'),
                 Record(seriesId, currentAt, 12.50m,
                    currentAt.AddMinutes(1), 'b')]);
            Assert.Equal(2, (await store.CommitAsync(batch)).Inserted);
            var summary = await new PostgresSummaryReader(dataSource, TimeProvider.System)
                .GetSummaryAsync(stationId.ToString("D"));
            Assert.NotNull(summary);
            Assert.Equal("aboveAlertThreshold", summary.CalculatedCondition);
            Assert.Equal("current", summary.DataStatus);
            Assert.Empty(summary.Notices);
            Assert.Equal("notConfigured", summary.NoticeCoverage.Status);
            Assert.Equal("above", Assert.Single(summary.OfficialThresholds).ComparisonStatus);
        }
        finally
        {
            await CleanupAsync(dataSource, sourceId, stationId, seriesId, stream);
        }
    }

    [Fact]
    public async Task Development_worker_commits_and_replays_a_synthetic_observation()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var sourceId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var stream = seriesId.ToString("D");
        var observedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        try
        {
            await SeedAsync(dataSource, sourceId, stationId, seriesId);
            await RunWorkerAsync(seriesId, observedAt);
            Assert.Equal(1, await CountMeasurementsAsync(dataSource, seriesId));
            await ExpireLeaseAsync(dataSource, stream, "synthetic-worker");
            await RunWorkerAsync(seriesId, observedAt);
            Assert.Equal(1, await CountMeasurementsAsync(dataSource, seriesId));
            var checkpoint = await new PostgresIngestionStore(dataSource)
                .ReadCheckpointAsync("synthetic-worker", stream);
            Assert.Equal(2, checkpoint?.Version);
        }
        finally
        {
            await CleanupAsync(dataSource, sourceId, stationId, seriesId,
                stream, "synthetic-worker");
        }
    }

    private static async Task RunWorkerAsync(Guid seriesId, DateTimeOffset observedAt)
    {
        var workerAssembly = typeof(SyntheticIngestionWorker).Assembly.Location;
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.ArgumentList.Add(workerAssembly);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        process.StartInfo.Environment["SyntheticIngestion__Enabled"] = "true";
        process.StartInfo.Environment["SyntheticIngestion__SeriesId"] = seriesId.ToString("D");
        process.StartInfo.Environment["SyntheticIngestion__ObservedAt"] = observedAt.ToString("O");
        process.StartInfo.Environment["SyntheticIngestion__Value"] = "7.50";
        process.StartInfo.Environment["ConnectionStrings__Ingestion"] = ConnectionString;
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        Assert.True(process.ExitCode == 0,
            $"Synthetic worker failed: {await output} {await error}");
    }

    private static async Task<long> CountMeasurementsAsync(
        NpgsqlDataSource dataSource, Guid seriesId)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM measurements WHERE series_id = $1");
        command.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountMissingAsync(
        NpgsqlDataSource dataSource, Guid seriesId)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM measurements WHERE series_id = $1 AND quality = 'missing'");
        command.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountQuarantinedAsync(
        NpgsqlDataSource dataSource, string stream)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM quarantined_records " +
            "WHERE provider = 'integration' AND stream_key = $1");
        command.Parameters.Add(new NpgsqlParameter { Value = stream });
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static IngestionRecord Record(
        Guid seriesId, DateTimeOffset observedAt, decimal value,
        DateTimeOffset sourceUpdatedAt, char hashCharacter) =>
        new(seriesId, observedAt, observedAt, value, sourceUpdatedAt,
            observedAt.ToString("O"), new string(hashCharacter, 64));

    private static async Task SeedAsync(
        NpgsqlDataSource dataSource, Guid sourceId, Guid stationId, Guid seriesId)
    {
        await using var batch = dataSource.CreateBatch();
        Add(batch, """
            INSERT INTO data_sources (id, code, name, permission_status,
                rights_decision_id, reviewed_at)
            VALUES ($1, $2, 'Integration fixture', 'approved', 'synthetic-rights', now())
            """, sourceId, $"integration-{seriesId:N}");
        Add(batch, "INSERT INTO stations (id, name) VALUES ($1, 'Integration station')",
            stationId);
        Add(batch, """
            INSERT INTO measurement_series (id, station_id, source_id, external_id,
                variable_code, unit_id, unit, procedure_name, support_seconds,
                data_kind, datum_ref, cadence_seconds, allowed_lag_seconds,
                approval_version, rights_decision_id, hydrology_decision_id, approved)
            VALUES ($1, $2, $3, $4, 'H', 3, 'm', 'synthetic-direct', 0,
                'observed', 'integration-datum', 3600, 1800, 'integration-v1',
                'synthetic-rights', 'synthetic-hydrology', true)
            """, seriesId, stationId, sourceId, $"integration-{seriesId:N}");
        Add(batch, """
            INSERT INTO series_trend_policies (series_id, version, cadence_seconds,
                allowed_lag_seconds, epsilon, methodology_version, datum_ref,
                epoch, active, hydrology_decision_id, reviewed_at)
            VALUES ($1, 'integration-v1', 3600, 1800, 0.02, 'integration-v1',
                'integration-datum', 1, true, 'synthetic-hydrology', now())
            """, seriesId);
        Add(batch, """
            INSERT INTO official_thresholds (id, series_id, version, kind, value,
                unit, datum_ref, epoch, authority, title, source_url, valid_from,
                approved, rights_decision_id, hydrology_decision_id, reviewed_at)
            VALUES ($1, $2, 'integration-v1', 'alert', 11, 'm',
                'integration-datum', 1, 'Synthetic authority',
                'Synthetic threshold', 'https://example.invalid/', now() - interval '1 day',
                true, 'synthetic-rights', 'synthetic-hydrology', now())
            """, Guid.NewGuid(), seriesId);
        await batch.ExecuteNonQueryAsync();
    }

    private static async Task RevokeSourceAsync(NpgsqlDataSource dataSource, Guid sourceId)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE data_sources SET permission_status = 'denied' WHERE id = $1");
        command.Parameters.Add(new NpgsqlParameter { Value = sourceId });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExpireLeaseAsync(
        NpgsqlDataSource dataSource, string stream, string provider = "integration")
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE ingestion_checkpoints SET lease_until = clock_timestamp() - interval '1 second' " +
            "WHERE provider = $1 AND stream_key = $2");
        command.Parameters.Add(new NpgsqlParameter { Value = provider });
        command.Parameters.Add(new NpgsqlParameter { Value = stream });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(
        NpgsqlDataSource dataSource, Guid sourceId, Guid stationId, Guid seriesId,
        string stream, string provider = "integration")
    {
        await using var batch = dataSource.CreateBatch();
        Add(batch, "DELETE FROM series_latest WHERE series_id = $1", seriesId);
        Add(batch, "DELETE FROM measurement_revisions WHERE measurement_id IN " +
            "(SELECT id FROM measurements WHERE series_id = $1)", seriesId);
        Add(batch, "DELETE FROM measurements WHERE series_id = $1", seriesId);
        Add(batch, "DELETE FROM quarantined_records WHERE provider = $1 " +
            "AND stream_key = $2", provider, stream);
        Add(batch, "DELETE FROM ingestion_checkpoints WHERE provider = $1 " +
            "AND stream_key = $2", provider, stream);
        Add(batch, "DELETE FROM official_thresholds WHERE series_id = $1", seriesId);
        Add(batch, "DELETE FROM series_trend_policies WHERE series_id = $1", seriesId);
        Add(batch, "DELETE FROM measurement_series WHERE id = $1", seriesId);
        Add(batch, "DELETE FROM stations WHERE id = $1", stationId);
        Add(batch, "DELETE FROM data_sources WHERE id = $1", sourceId);
        await batch.ExecuteNonQueryAsync();
    }

    private static void Add(NpgsqlBatch batch, string sql, params object[] values)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in values)
            command.Parameters.Add(new NpgsqlParameter { Value = value });
        batch.BatchCommands.Add(command);
    }
}
