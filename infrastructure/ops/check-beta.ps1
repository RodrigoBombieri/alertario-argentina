param(
    [Parameter(Mandatory)][uri]$BaseUrl,
    [switch]$PersistedData,
    [string]$BackupDirectory,
    [switch]$Watch,
    [ValidateRange(30,3600)][int]$IntervalSeconds = 60,
    [ValidateRange(60,86400)][int]$CooldownSeconds = 900
)
$ErrorActionPreference = 'Stop'
if ($BaseUrl.Scheme -notin @('http','https')) { throw 'HTTP(S) URL required.' }
$lastState = ''
$lastOutput = [DateTimeOffset]::MinValue
do {
    $checks = @('live','ready')
    if ($PersistedData) { $checks += @('storage','worker','data') }
    $issues = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $checks) {
        try {
            $url = [uri]::new($BaseUrl, "/health/$name")
            $response = Invoke-RestMethod -Uri $url -TimeoutSec 8
            if ($response.status -notin @('live','ready')) { $issues.Add("health/$name") }
        } catch { $issues.Add("health/$name") }
    }
    if ($PersistedData) {
        if (!$BackupDirectory -or !(Test-Path -LiteralPath $BackupDirectory -PathType Container)) {
            $issues.Add('backupMissing')
        } else {
            $latest = Get-ChildItem -LiteralPath $BackupDirectory -Filter '*.verified' -File |
                Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
            if (!$latest -or $latest.LastWriteTimeUtc -lt [DateTime]::UtcNow.AddHours(-26)) {
                $issues.Add('backupOverdue')
            } else {
                try {
                    $manifest = Get-Content -LiteralPath $latest.FullName -Raw | ConvertFrom-Json
                    $dumpPath = $latest.FullName.Substring(0, $latest.FullName.Length - '.verified'.Length)
                    $dump = Get-Item -LiteralPath $dumpPath -ErrorAction Stop
                    if ($dump.Length -ne $manifest.bytes -or
                        (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash -ne $manifest.sha256) {
                        $issues.Add('backupIntegrity')
                    }
                } catch { $issues.Add('backupIntegrity') }
            }
        }
    }
    $now = [DateTimeOffset]::UtcNow
    $currentState = $issues -join ','
    if (!$Watch -or $currentState -ne $lastState -or ($now - $lastOutput).TotalSeconds -ge $CooldownSeconds) {
        [ordered]@{ checkedAt = $now.ToString('O'); healthy = $issues.Count -eq 0; issues = @($issues) } | ConvertTo-Json -Compress
        $lastState = $currentState
        $lastOutput = $now
    }
    if ($Watch) { Start-Sleep -Seconds $IntervalSeconds }
} while ($Watch)
if ($issues.Count -gt 0) { exit 1 }
