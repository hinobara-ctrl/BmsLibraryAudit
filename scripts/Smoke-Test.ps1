param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build in Release before running the smoke test.' }
$taskBase = Join-Path $taskRepository ('.artifacts\smoke-' + [Guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'songs'
$taskDb = Join-Path $taskBase 'audit.db'
$taskOutput = Join-Path $taskBase 'reports'
foreach ($taskFolder in @('A', 'B', 'C')) {
    New-Item -ItemType Directory -Path (Join-Path $taskRoot $taskFolder) -Force | Out-Null
}
$taskEncoding = [System.Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $taskRoot 'A\one.BMS'), 'chart-x', $taskEncoding)
[IO.File]::WriteAllText((Join-Path $taskRoot 'A\copy.bme'), 'chart-x', $taskEncoding)
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\one.bmson'), 'chart-x', $taskEncoding)
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\two.bml'), 'chart-y', $taskEncoding)
[IO.File]::WriteAllText((Join-Path $taskRoot 'C\two.pms'), 'chart-y', $taskEncoding)
[IO.File]::WriteAllText((Join-Path $taskRoot 'A\001.wav'), 'audio is not hashed', $taskEncoding)
$taskBefore = Get-ChildItem -LiteralPath $taskRoot -Recurse -File | ForEach-Object {
    [pscustomobject]@{ Path = $_.FullName; Mtime = $_.LastWriteTimeUtc.Ticks; Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$taskFirstJson = & $DotNet $taskCli scan --db $taskDb --root $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'First scan failed.' }
$taskFirst = ($taskFirstJson -join "`n") | ConvertFrom-Json
$taskSecondJson = & $DotNet $taskCli scan --db $taskDb --root $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Second scan failed.' }
$taskSecond = ($taskSecondJson -join "`n") | ConvertFrom-Json
if ($taskFirst.Statistics.Hashed -ne 5 -or $taskSecond.Statistics.Reused -ne 5 -or $taskSecond.Statistics.Hashed -ne 0) {
    throw 'Unexpected scan/cache statistics.'
}
& $DotNet $taskCli report --db $taskDb --output $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'Report command failed.' }
$taskSummary = Get-Content -LiteralPath (Join-Path $taskOutput 'summary.json') -Raw | ConvertFrom-Json
if ($taskSummary.current_chart_files -ne 5 -or $taskSummary.unique_sha256 -ne 2 -or
    $taskSummary.duplicate_sha256_groups -ne 2 -or $taskSummary.candidate_folder_pairs -ne 2 -or
    $taskSummary.cluster_count -ne 1 -or $taskSummary.error_count -ne 0) {
    throw 'Unexpected report summary.'
}
foreach ($taskFile in $taskBefore) {
    $taskCurrent = Get-Item -LiteralPath $taskFile.Path
    if ($taskCurrent.LastWriteTimeUtc.Ticks -ne $taskFile.Mtime -or
        (Get-FileHash -LiteralPath $taskFile.Path -Algorithm SHA256).Hash -ne $taskFile.Hash) {
        throw "Source changed: $($taskFile.Path)"
    }
}
$taskBadOutput = & $DotNet $taskCli report --db $taskDb --output (Join-Path $taskRoot 'reports') 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath (Join-Path $taskRoot 'reports'))) {
    throw 'Source-output protection failed.'
}
Write-Output "Smoke test passed. Artifacts: $taskBase"
Get-ChildItem -LiteralPath $taskOutput -File | Select-Object -ExpandProperty Name
Write-Output ($taskSummary | Select-Object current_chart_files, verified_chart_files, unique_sha256,
    duplicate_sha256_groups, candidate_folder_pairs, cluster_count, relationship_counts,
    hash_cache_statistics, warning_count, error_count | ConvertTo-Json -Depth 5)
