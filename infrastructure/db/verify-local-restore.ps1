$ErrorActionPreference = 'Stop'

$composePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\compose.yaml')).Path
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$restoreDatabase = "alertario_restore_$suffix"
$backupPath = "/tmp/alertario_restore_$suffix.dump"
$createdDatabase = $false

function Invoke-DatabaseContainer {
    param([string[]]$CommandArgs)
    $result = & docker-compose -f $composePath exec -T db @CommandArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Database container command failed: $($CommandArgs[0])"
    }
    return $result
}

try {
    Invoke-DatabaseContainer -CommandArgs @('pg_dump', '-U', 'alertario_dev', '-d',
        'alertario_dev', '-Fc', '-f', $backupPath) | Out-Null
    Invoke-DatabaseContainer -CommandArgs @('createdb', '-U', 'alertario_dev',
        '-O', 'alertario_dev', $restoreDatabase) | Out-Null
    $createdDatabase = $true
    Invoke-DatabaseContainer -CommandArgs @('pg_restore', '-U', 'alertario_dev',
        '-d', $restoreDatabase, '--exit-on-error', $backupPath) | Out-Null

    $checkSql = @"
SELECT (SELECT string_agg(version, ',' ORDER BY version) FROM schema_migrations),
       (SELECT count(*) FROM data_sources),
       (SELECT count(*) FROM stations),
       (SELECT count(*) FROM measurement_series),
       (SELECT count(*) FROM measurements),
       (SELECT count(*) FROM quarantined_records),
       (SELECT count(*) FROM ingestion_checkpoints),
       (SELECT count(*) FROM notification_outbox)
"@
    $original = Invoke-DatabaseContainer -CommandArgs @('psql', '-U', 'alertario_dev',
        '-d', 'alertario_dev', '-At', '-c', $checkSql)
    $restored = Invoke-DatabaseContainer -CommandArgs @('psql', '-U', 'alertario_dev',
        '-d', $restoreDatabase, '-At', '-c', $checkSql)
    if ($original.Trim() -ne $restored.Trim()) {
        throw 'Restore verification failed: schema versions or row counts differ.'
    }
    Write-Host "Local restore verified in $restoreDatabase."
    Write-Host "Schema and row counts: $($restored.Trim())"
}
finally {
    if ($createdDatabase) {
        & docker-compose -f $composePath exec -T db dropdb -U alertario_dev $restoreDatabase
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not drop $restoreDatabase." }
    }
    & docker-compose -f $composePath exec -T db rm -f $backupPath
    if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove $backupPath." }
}
