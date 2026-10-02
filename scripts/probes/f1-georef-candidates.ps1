param(
    [Parameter(Mandatory = $true)][string]$CatalogFile,
    [Parameter(Mandatory = $true)][string]$OutputFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ids = @(14, 19, 23, 29, 34, 36, 61, 68, 72, 74, 79, 81)
$provinces = @{
    MISIONES = 'Misiones'; CORRIENTES = 'Corrientes'; ENTRERIOS = 'Entre Ríos'
    SANTAFE = 'Santa Fe'; BUENOSAIRES = 'Buenos Aires'
}
$catalog = Get-Content -LiteralPath $CatalogFile -Raw | ConvertFrom-Json -DateKind String
if ($catalog.schema -ne 'alertario-f1-probe-v1' -or $catalog.mode -ne 'Catalog') {
    throw 'Expected a bounded F1 INA catalog probe.'
}
$stations = @($catalog.results.stations)
$results = @()
foreach ($id in $ids) {
    if ($results.Count -gt 0) { Start-Sleep -Seconds 1 }
    $station = @($stations | Where-Object { $_.id -eq $id })
    if ($station.Count -ne 1) { throw "Catalog does not identify station $id uniquely." }
    $station = $station[0]
    $province = $provinces[[string]$station.province]
    if (-not $province) { throw "Unknown province for station $id." }
    $name = [uri]::EscapeDataString([string]$station.name)
    $provinceQuery = [uri]::EscapeDataString($province)
    $uri = "https://apis.datos.gob.ar/georef/api/v2.0/localidades?nombre=$name&provincia=$provinceQuery&max=5"
    $checkedAt = [datetimeoffset]::UtcNow.ToString('o')
    try {
        $response = Invoke-WebRequest -Uri $uri -TimeoutSec 20 -MaximumRedirection 0
        $body = $response.Content | ConvertFrom-Json -DateKind String
        $results += [pscustomobject]@{
            stationId = $id; stationName = $station.name; province = $province
            stationCoordinates = $station.coordinates; url = $uri; checkedAt = $checkedAt
            status = [int]$response.StatusCode; total = $body.total
            candidates = @($body.localidades | ForEach-Object {
                [pscustomobject]@{
                    id = $_.id; name = $_.nombre; category = $_.categoria
                    province = $_.provincia.nombre; department = $_.departamento.nombre
                    centroid = $_.centroide
                }
            })
        }
    }
    catch {
        $results += [pscustomobject]@{
            stationId = $id; stationName = $station.name; province = $province
            stationCoordinates = $station.coordinates; url = $uri; checkedAt = $checkedAt
            status = $null; error = $_.Exception.Message
        }
    }
}
$document = [ordered]@{
    schema = 'alertario-f1-georef-candidates-v1'
    generatedAt = [datetimeoffset]::UtcNow.ToString('o')
    method = '12 sequential named locality GETs; one-second delay; max 5 each; candidates only'
    warning = 'Name/centroid proximity does not establish a representative station for a locality.'
    results = $results
}
$parent = Split-Path -Parent $OutputFile
if ($parent -and -not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
$document | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputFile -Encoding utf8
Write-Output "Wrote $($results.Count) locality searches to $OutputFile"
