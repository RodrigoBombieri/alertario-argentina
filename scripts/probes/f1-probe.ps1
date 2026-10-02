param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Catalog', 'Series', 'Observations', 'GeoRef')]
    [string]$Mode,
    [int[]]$Ids = @(),
    [datetimeoffset]$FromUtc,
    [datetimeoffset]$ToUtc,
    [Parameter(Mandatory = $true)]
    [string]$OutputFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Mode -in @('Series', 'Observations')) {
    if ($Ids.Count -lt 1 -or $Ids.Count -gt 12 -or @($Ids | Where-Object { $_ -lt 1 }).Count -gt 0) {
        throw 'Provide 1-12 positive INA IDs.'
    }
}
if ($Mode -eq 'Observations') {
    if ($FromUtc -eq [datetimeoffset]::MinValue -or $ToUtc -eq [datetimeoffset]::MinValue) {
        throw 'Observations require FromUtc and ToUtc.'
    }
    if ($ToUtc -le $FromUtc -or ($ToUtc - $FromUtc).TotalDays -gt 3) {
        throw 'Observation interval must be positive and at most 3 days.'
    }
}

function Invoke-BoundedGet([string]$Uri) {
    $parsed = [uri]$Uri
    if ($parsed.Scheme -ne 'https' -or $parsed.Host -notin @('alerta.ina.gob.ar', 'apis.datos.gob.ar')) {
        throw "Host or scheme not allowed: $Uri"
    }
    $checkedAt = [datetimeoffset]::UtcNow.ToString('o')
    try {
        $response = Invoke-WebRequest -Uri $Uri -TimeoutSec 20 -MaximumRedirection 0
        $contentType = [string]$response.Headers['Content-Type']
        $bytes = [text.encoding]::UTF8.GetBytes($response.Content)
        $hash = [convert]::ToHexString([security.cryptography.sha256]::HashData($bytes)).ToLowerInvariant()
        $json = $response.Content | ConvertFrom-Json -Depth 50 -DateKind String
        return [pscustomobject]@{
            url = $Uri; checkedAt = $checkedAt; status = [int]$response.StatusCode
            contentType = $contentType; sha256Utf8Content = $hash; body = $json
        }
    }
    catch {
        return [pscustomobject]@{
            url = $Uri; checkedAt = $checkedAt; status = $null
            contentType = $null; sha256Utf8Content = $null; error = $_.Exception.Message
        }
    }
}

$requests = @()
switch ($Mode) {
    'Catalog' {
        $requests = @(
            'https://alerta.ina.gob.ar/a5/obs/puntual/estaciones?tabla=alturas_prefe&rio=URUGUAY&pagination=true&limit=30',
            'https://alerta.ina.gob.ar/a5/obs/puntual/estaciones?tabla=alturas_prefe&rio=PARANA&pagination=true&limit=30',
            'https://alerta.ina.gob.ar/a5/obs/puntual/estaciones?tabla=alturas_prefe&rio=PARANA&pagination=true&limit=30&offset=30'
        )
    }
    'Series' {
        $requests = @($Ids | Select-Object -Unique | ForEach-Object {
            "https://alerta.ina.gob.ar/a5/obs/puntual/series?estacion_id=$_"
        })
    }
    'Observations' {
        $start = [uri]::EscapeDataString($FromUtc.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))
        $end = [uri]::EscapeDataString($ToUtc.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))
        $requests = @($Ids | Select-Object -Unique | ForEach-Object {
            "https://alerta.ina.gob.ar/a5/obs/puntual/series/$_/observaciones?timestart=$start&timeend=$end"
        })
    }
    'GeoRef' {
        $requests = @(
            'https://apis.datos.gob.ar/georef/api/v2.0/localidades?nombre=Concordia&max=2&inicio=0',
            'https://apis.datos.gob.ar/georef/api/v2.0/localidades?nombre=Concordia&max=2&inicio=2',
            'https://apis.datos.gob.ar/georef/api/v2.0/localidades?nombre=Concordia&max=2&formato=geojson'
        )
    }
}

$results = @()
foreach ($uri in $requests) {
    if ($results.Count -gt 0) { Start-Sleep -Seconds 1 }
    $r = Invoke-BoundedGet $uri
    $entry = [ordered]@{
        url = $r.url; checkedAt = $r.checkedAt; status = $r.status
        contentType = $r.contentType; sha256Utf8Content = $r.sha256Utf8Content
    }
    if ($r.PSObject.Properties.Name -contains 'error') {
        $entry.error = $r.error
        $results += [pscustomobject]$entry
        continue
    }
    switch ($Mode) {
        'Catalog' {
            $entry.countReturned = @($r.body.estaciones).Count
            $entry.isLastPage = $r.body.is_last_page
            $entry.stations = @($r.body.estaciones | Where-Object { $_.public -eq $true -and $_.red.public -eq $true } | ForEach-Object {
                [pscustomobject]@{
                    id = $_.id; name = $_.nombre; river = $_.rio; province = $_.provincia
                    owner = $_.propietario; network = $_.red.nombre; coordinates = $_.geom.coordinates
                }
            })
        }
        'Series' {
            $entry.countReturned = @($r.body.rows).Count
            $entry.series = @($r.body.rows | Where-Object {
                $_.estacion.public -eq $true -and $_.estacion.red.public -eq $true
            } | ForEach-Object {
                [pscustomobject]@{
                    id = $_.id; stationId = $_.estacion.id; variable = $_.var.var
                    variableName = $_.var.nombre; procedure = $_.procedimiento.nombre
                    unit = $_.unidades.abrev; timeSupport = $_.var.timeSupport
                    dateRange = $_.date_range
                }
            })
        }
        'Observations' {
            $items = @($r.body)
            $entry.countReturned = $items.Count
            $entry.nullValues = @($items | Where-Object { $null -eq $_.valor }).Count
            $entry.observations = @($items | ForEach-Object {
                $fingerprint = [convert]::ToHexString([security.cryptography.sha256]::HashData(
                    [text.encoding]::UTF8.GetBytes(($_ | ConvertTo-Json -Depth 20 -Compress))
                )).ToLowerInvariant()
                [pscustomobject]@{
                    id = $_.id; observedStart = $_.timestart; observedEnd = $_.timeend
                    sourceUpdated = $_.timeupdate; unitId = $_.unit_id
                    valuePresent = ($null -ne $_.valor); fingerprint = $fingerprint
                }
            })
        }
        'GeoRef' {
            $keys = @($r.body.PSObject.Properties.Name)
            $entry.total = if ($keys -contains 'total') { $r.body.total } else { $null }
            $entry.countReturned = if ($keys -contains 'localidades') { @($r.body.localidades).Count } elseif ($keys -contains 'features') { @($r.body.features).Count } else { $null }
            $entry.topLevelKeys = $keys
        }
    }
    $results += [pscustomobject]$entry
}

$document = [ordered]@{
    schema = 'alertario-f1-probe-v1'
    mode = $Mode
    generatedAt = [datetimeoffset]::UtcNow.ToString('o')
    method = 'bounded sequential GET; one-second delay; no redirects; bodies omitted'
    results = $results
}
$parent = Split-Path -Parent $OutputFile
if ($parent -and -not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
$document | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $OutputFile -Encoding utf8
Write-Output "Wrote $($results.Count) request summaries to $OutputFile"
