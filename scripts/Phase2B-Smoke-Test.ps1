param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Synthetic Phase 2B smoke requires PowerShell 7 or later.' }
if (-not $IsWindows) { throw 'Synthetic Phase 2B smoke requires Windows.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build in Release before running the smoke test.' }
$taskBase = Join-Path $taskRepository ('.artifacts\phase2b-smoke-' + [Guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'songs'
$taskSource = Join-Path $taskBase 'audit.db'
$taskDb = Join-Path $taskBase 'phase2.db'
$taskPreflightOutput = Join-Path $taskBase 'preflight'
$taskContentOutput = Join-Path $taskBase 'content'
$taskRepeat = Join-Path $taskBase 'repeat-content'
$taskEncoding = [Text.UTF8Encoding]::new($false)
# No externally supplied roots and no cleanup: all fixture/evidence paths are
# in this unique project-owned artifact directory and remain reviewable.
foreach ($taskFolder in @('A', 'B')) {
    $taskDirectory = Join-Path $taskRoot $taskFolder
    New-Item -ItemType Directory -Path (Join-Path $taskDirectory 'empty') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'one.BMS'), 'synthetic-chart', $taskEncoding)
    [IO.File]::WriteAllText((Join-Path $taskDirectory '001.wav'), 'opaque-resource', $taskEncoding)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'no-extension'), 'opaque-auxiliary', $taskEncoding)
}
$taskScanJson = & $DotNet $taskCli scan --db $taskSource --root $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Synthetic Phase 1 scan failed.' }
$taskScan = ($taskScanJson -join "`n") | ConvertFrom-Json
$taskBefore = @((Get-ChildItem -LiteralPath $taskRoot -Recurse -File).FullName) + @($taskSource) | ForEach-Object {
    [pscustomobject]@{ Path = $_; Mtime = (Get-Item -LiteralPath $_).LastWriteTimeUtc.Ticks; Hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
}
$taskPreflightJson = & $DotNet $taskCli phase2 preflight --source-db $taskSource --source-scan $taskScan.ScanId --db $taskDb --output $taskPreflightOutput
if ($LASTEXITCODE -ne 0) { throw 'Synthetic preflight failed.' }
$taskPreflight = ($taskPreflightJson -join "`n") | ConvertFrom-Json
$taskPreflightSummary = Get-Content -LiteralPath (Join-Path $taskPreflightOutput 'session_summary.json') -Raw | ConvertFrom-Json
if ($taskPreflight.State -ne 'COMPLETED' -or $taskPreflight.AdmittedComponents -ne 1 -or $taskPreflightSummary.phase2b_executed -ne $false -or $taskPreflightSummary.non_chart_payload_reads -ne 0) { throw 'Preflight automatically ran CONTENT or failed admission.' }
$taskDatabaseBefore = (Get-FileHash -LiteralPath $taskDb).Hash
$taskBadOutput = Join-Path $taskBase 'bad-acceptance'
$taskRefusal = & $DotNet $taskCli phase2 hash --db $taskDb --preflight $taskPreflight.SessionId --accept-manifest ('0' * 64) --output $taskBadOutput 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $taskBadOutput) -or (Get-FileHash -LiteralPath $taskDb).Hash -ne $taskDatabaseBefore) { throw 'Incorrect manifest was not refused before evidence writes.' }
$taskContentJson = & $DotNet $taskCli phase2 hash --db $taskDb --preflight $taskPreflight.SessionId --accept-manifest $taskPreflight.ManifestSha256 --output $taskContentOutput
if ($LASTEXITCODE -ne 0) { throw 'Explicit accepted-manifest CONTENT failed.' }
$taskContent = ($taskContentJson -join "`n") | ConvertFrom-Json
$taskSummary = Get-Content -LiteralPath (Join-Path $taskContentOutput 'session_summary.json') -Raw | ConvertFrom-Json
if ($taskContent.State -ne 'COMPLETED' -or $taskContent.CompleteContentComponents -ne 1 -or $taskContent.IncompleteContentComponents -ne 0 -or
    $taskSummary.trusted_successful_hash_count -ne 6 -or $taskSummary.comparison_count -ne 1 -or $taskSummary.content_manifest_sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Incorrect CONTENT summary.' }
$taskComparisons = @(Import-Csv -LiteralPath (Join-Path $taskContentOutput 'folder_comparisons.csv'))
if ($taskComparisons.Count -ne 1 -or $taskComparisons[0].same_path_same_bytes -ne '3' -or $taskComparisons[0].same_path_different_bytes -ne '0' -or
    $taskComparisons[0].OBSERVED_TREE_EXACT -ne 'true' -or $taskComparisons[0].OBSERVED_CONTENT_MULTISET_EXACT -ne 'true') { throw 'Exact comparison differs from the independent fixture expectation.' }
$taskFiles = @(Import-Csv -LiteralPath (Join-Path $taskContentOutput 'filesystem_inventory.csv') | Where-Object kind -eq 'REGULAR_FILE')
if ($taskFiles.Count -ne 6) { throw 'Physical file multiplicity was lost.' }
foreach ($taskFile in $taskFiles) {
    if ($taskFile.individual_file_fact_trusted -ne 'true' -or $taskFile.comparison_eligible -ne 'true' -or
        $taskFile.content_sha256 -ne (Get-FileHash -LiteralPath $taskFile.original_path -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Fresh SHA-256 differs from independent PowerShell hashing.' }
}
& $DotNet $taskCli phase2 report --db $taskDb --session $taskContent.SessionId --output $taskRepeat
if ($LASTEXITCODE -ne 0) { throw 'Persisted CONTENT export failed.' }
$taskNames = @('content_groups.csv','diagnostics.csv','filesystem_inventory.csv','folder_comparisons.csv','observation_roots.csv','session_summary.json')
if ((@(Get-ChildItem -LiteralPath $taskContentOutput -File | Sort-Object Name | Select-Object -ExpandProperty Name) -join ',') -ne ($taskNames -join ',')) { throw 'Incorrect completed CONTENT report set.' }
foreach ($taskName in $taskNames) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskContentOutput $taskName)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskRepeat $taskName)).Hash) { throw 'CONTENT reports are not deterministic.' }
}
foreach ($taskFile in $taskBefore) {
    if ((Get-Item -LiteralPath $taskFile.Path).LastWriteTimeUtc.Ticks -ne $taskFile.Mtime -or (Get-FileHash -LiteralPath $taskFile.Path -Algorithm SHA256).Hash -ne $taskFile.Hash) { throw "Synthetic source changed: $($taskFile.Path)" }
}
foreach ($taskSuffix in @('-journal','-wal','-shm')) {
    if (Test-Path -LiteralPath ($taskSource + $taskSuffix)) { throw 'Source database sidecar created.' }
}
Write-Output "Phase 2B synthetic smoke passed. Artifacts: $taskBase"
Write-Output ($taskContent | ConvertTo-Json)
