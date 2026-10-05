param(
    [uri]$BaseUrl = 'http://127.0.0.1:5182',
    [ValidateRange(1,1000)][int]$Requests = 100,
    [ValidateRange(1,20)][int]$Concurrency = 5
)
$ErrorActionPreference = 'Stop'
if (!$BaseUrl.IsLoopback) { throw 'This smoke load test is restricted to your local API.' }
$target = [uri]::new($BaseUrl, '/v1/stations/station-demo/summary').AbsoluteUri
# Warm the app before measuring. Synthetic API only; this never queries organisms.
$warm = Invoke-RestMethod -Uri $target -TimeoutSec 10
if (!$warm.synthetic) { throw 'Only the synthetic demo may be load-tested by this script.' }
$measurements = 1..$Requests | ForEach-Object -Parallel {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-RestMethod -Uri $using:target -TimeoutSec 10
        if (!$response.synthetic -or $response.stationId -ne 'station-demo') { throw 'Unexpected payload.' }
        [pscustomobject]@{ ms = $watch.Elapsed.TotalMilliseconds; ok = $true }
    } catch { [pscustomobject]@{ ms = $watch.Elapsed.TotalMilliseconds; ok = $false } }
} -ThrottleLimit $Concurrency
$times = @($measurements.ms | Sort-Object)
$failures = @($measurements | Where-Object { !$_.ok }).Count
[ordered]@{
    mode = 'localSyntheticOnly'; requests = $Requests; concurrency = $Concurrency
    failures = $failures; p95Ms = [math]::Round($times[[math]::Ceiling($times.Count * 0.95) - 1], 2)
    maxMs = [math]::Round($times[-1], 2)
} | ConvertTo-Json -Compress
if ($failures -gt 0) { exit 1 }
