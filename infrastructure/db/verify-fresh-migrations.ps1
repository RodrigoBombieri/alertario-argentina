$ErrorActionPreference = 'Stop'

$composePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\compose.yaml')).Path
$migrationPath = Join-Path $PSScriptRoot 'migrations'
$testPath = Join-Path $PSScriptRoot 'tests'
$migrations = @(Get-ChildItem -LiteralPath $migrationPath -File -Filter '*.sql' |
    Sort-Object -Property Name)
$tests = @(Get-ChildItem -LiteralPath $testPath -File -Filter '*.sql' |
    Sort-Object -Property Name)
if ($migrations.Count -eq 0 -or $tests.Count -eq 0) {
    throw 'Migration and smoke files are required.'
}
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$testDatabase = "alertario_fresh_$suffix"
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
    Invoke-DatabaseContainer -CommandArgs @('createdb', '-U', 'alertario_dev',
        '-O', 'alertario_dev', $testDatabase) | Out-Null
    $createdDatabase = $true
    foreach ($file in $migrations) {
        if ($file.Name -notmatch '^\d{4}_[a-z0-9_]+\.sql$') {
            throw "Unexpected migration filename: $($file.Name)"
        }
        Invoke-DatabaseContainer -CommandArgs @('psql', '-U', 'alertario_dev',
            '-d', $testDatabase, '-v', 'ON_ERROR_STOP=1',
            '-f', "/opt/alertario/migrations/$($file.Name)") | Out-Null
    }
    $actualVersions = @(Invoke-DatabaseContainer -CommandArgs @('psql',
        '-U', 'alertario_dev', '-d', $testDatabase, '-At',
        '-c', 'SELECT version FROM schema_migrations ORDER BY version'))
    $expectedVersions = @($migrations | ForEach-Object {
        [IO.Path]::GetFileNameWithoutExtension($_.Name)
    })
    if (($actualVersions -join ',') -ne ($expectedVersions -join ',')) {
        throw 'Fresh database schema versions do not match migration files.'
    }
    foreach ($file in $tests) {
        Invoke-DatabaseContainer -CommandArgs @('psql', '-U', 'alertario_dev',
            '-d', $testDatabase, '-v', 'ON_ERROR_STOP=1',
            '-f', "/opt/alertario/tests/$($file.Name)") | Out-Null
    }
    Write-Host "Fresh migration and SQL smoke verification passed ($($migrations.Count) migrations, $($tests.Count) smokes)."
}
finally {
    if ($createdDatabase) {
        & docker-compose -f $composePath exec -T db dropdb -U alertario_dev $testDatabase
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not drop $testDatabase." }
    }
}
