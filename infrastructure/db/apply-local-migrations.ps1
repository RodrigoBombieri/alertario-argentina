$ErrorActionPreference = 'Stop'

$composePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\compose.yaml')).Path
$migrationPath = Join-Path $PSScriptRoot 'migrations'
$files = @(Get-ChildItem -LiteralPath $migrationPath -File -Filter '*.sql' |
    Sort-Object -Property Name)
if ($files.Count -eq 0) { throw 'No migration files were found.' }

& docker-compose -f $composePath up -d db
if ($LASTEXITCODE -ne 0) { throw 'Could not start the local database.' }

$hasTable = & docker-compose -f $composePath exec -T db psql `
    -U alertario_dev -d alertario_dev -v ON_ERROR_STOP=1 -Atc `
    "SELECT to_regclass('public.schema_migrations') IS NOT NULL"
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect schema_migrations.' }
$applied = @{}
if ($hasTable.Trim() -eq 't') {
    $versions = @(& docker-compose -f $composePath exec -T db psql `
        -U alertario_dev -d alertario_dev -v ON_ERROR_STOP=1 -Atc `
        'SELECT version FROM schema_migrations')
    if ($LASTEXITCODE -ne 0) { throw 'Could not read applied migrations.' }
    foreach ($version in $versions) { $applied[$version.Trim()] = $true }
}

foreach ($file in $files) {
    if ($file.Name -notmatch '^\d{4}_[a-z0-9_]+\.sql$') {
        throw "Unexpected migration filename: $($file.Name)"
    }
    $version = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    if ($applied.ContainsKey($version)) {
        Write-Host "Already applied: $version"
        continue
    }
    & docker-compose -f $composePath exec -T db psql `
        -U alertario_dev -d alertario_dev -v ON_ERROR_STOP=1 `
        -f "/opt/alertario/migrations/$($file.Name)"
    if ($LASTEXITCODE -ne 0) { throw "Migration failed: $version" }
    $count = & docker-compose -f $composePath exec -T db psql `
        -U alertario_dev -d alertario_dev -v ON_ERROR_STOP=1 -Atc `
        "SELECT count(*) FROM schema_migrations WHERE version = '$version'"
    if ($LASTEXITCODE -ne 0 -or $count.Trim() -ne '1') {
        throw "Migration did not record its version: $version"
    }
    Write-Host "Applied: $version"
}
