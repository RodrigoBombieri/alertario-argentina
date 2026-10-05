param(
    [Parameter(Mandatory)][string]$Destination,
    [string]$ContainerName,
    [string]$Database,
    [string]$Username
)
$ErrorActionPreference = 'Stop'
# Connection and credentials use standard libpq environment variables / PGPASSFILE.
if (!$ContainerName) {
    foreach ($binary in @('pg_dump','pg_restore')) { Get-Command $binary -ErrorAction Stop | Out-Null }
} elseif (!$Database -or !$Username) { throw 'Container backup requires Database and Username.' }
if (!(Test-Path -LiteralPath $Destination -PathType Container)) {
    New-Item -ItemType Directory -Path $Destination -ErrorAction Stop | Out-Null
}
$root = (Resolve-Path -LiteralPath $Destination).Path
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$dump = Join-Path $root "alertario-$stamp.dump"
if ($ContainerName) {
    $containerFile = "/tmp/alertario-backup-$([Guid]::NewGuid().ToString('N')).dump"
    try {
        & docker exec $ContainerName pg_dump -U $Username -d $Database --format=custom --no-owner --no-acl --file $containerFile
        if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; backup is not verified.' }
        & docker exec $ContainerName pg_restore --list $containerFile | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Backup catalogue cannot be read.' }
        & docker cp "${ContainerName}:$containerFile" $dump
        if ($LASTEXITCODE -ne 0) { throw 'Backup copy failed.' }
    } finally {
        & docker exec $ContainerName rm -f $containerFile
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Temporary container backup could not be removed.' }
    }
} else {
    & pg_dump --format=custom --no-owner --no-acl --file $dump
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; backup is not verified.' }
    & pg_restore --list $dump | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Backup catalogue cannot be read.' }
}
$hash = (Get-FileHash -LiteralPath $dump -Algorithm SHA256).Hash
[ordered]@{ createdAt = [DateTimeOffset]::UtcNow.ToString('O'); sha256 = $hash; bytes = (Get-Item -LiteralPath $dump).Length } |
    ConvertTo-Json | Set-Content -LiteralPath "$dump.verified" -Encoding utf8
Write-Output "Backup created and catalogue checked: $dump"
