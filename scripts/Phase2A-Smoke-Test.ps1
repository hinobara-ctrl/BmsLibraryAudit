param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
$taskBase = Join-Path $taskRepository ('.artifacts\phase2a-smoke-' + [Guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'songs'
$taskSource = Join-Path $taskBase 'audit.db'
$taskDb = Join-Path $taskBase 'phase2.db'
$taskOutput = Join-Path $taskBase 'preflight'
$taskRepeat = Join-Path $taskBase 'repeat'
$taskEncoding = [Text.UTF8Encoding]::new($false)
foreach ($taskFolder in @('A', 'B')) {
    $taskDirectory = Join-Path $taskRoot $taskFolder
    New-Item -ItemType Directory -Path (Join-Path $taskDirectory 'empty') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'one.BMS'), 'synthetic-chart', $taskEncoding)
    [IO.File]::WriteAllText((Join-Path $taskDirectory '001.wav'), 'opaque non-chart content', $taskEncoding)
}
$taskScanJson = & $DotNet $taskCli scan --db $taskSource --root $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Synthetic Phase 1 scan failed.' }
$taskScan = ($taskScanJson -join "`n") | ConvertFrom-Json
$taskBefore = @((Get-ChildItem -LiteralPath $taskRoot -Recurse -File).FullName) + @($taskSource) | ForEach-Object {
    [pscustomobject]@{ Path = $_; Mtime = (Get-Item -LiteralPath $_).LastWriteTimeUtc.Ticks; Hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
}
$taskPreflightJson = & $DotNet $taskCli phase2 preflight --source-db $taskSource --source-scan $taskScan.ScanId --db $taskDb --output $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'Synthetic Phase 2A preflight failed.' }
$taskResult = ($taskPreflightJson -join "`n") | ConvertFrom-Json
& $DotNet $taskCli phase2 report --db $taskDb --session $taskResult.SessionId --output $taskRepeat
if ($LASTEXITCODE -ne 0) { throw 'Persisted preflight report failed.' }
$taskSummary = Get-Content -LiteralPath (Join-Path $taskOutput 'session_summary.json') -Raw | ConvertFrom-Json
if ($taskResult.State -ne 'COMPLETED' -or $taskResult.AdmittedComponents -ne 1 -or $taskResult.RejectedComponents -ne 0 -or
    $taskResult.ManifestSha256 -notmatch '^[0-9a-f]{64}$' -or $taskSummary.union_regular_file_count -ne 4 -or
    $taskSummary.union_directory_count -ne 4 -or $taskSummary.non_chart_payload_reads -ne 0 -or
    $taskSummary.trusted_content_evidence_scope -ne 'NONE' -or $taskSummary.phase2b_executed -ne $false) { throw 'Unexpected preflight evidence.' }
$taskNames = @('diagnostics.csv','filesystem_inventory.csv','observation_roots.csv','session_summary.json')
if ((@(Get-ChildItem -LiteralPath $taskOutput -File | Sort-Object Name | Select-Object -ExpandProperty Name) -join ',') -ne ($taskNames -join ',')) { throw 'Incorrect report set.' }
foreach ($taskName in $taskNames) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskOutput $taskName)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskRepeat $taskName)).Hash) { throw 'Reports are not deterministic.' }
}
foreach ($taskFile in $taskBefore) {
    if ((Get-Item -LiteralPath $taskFile.Path).LastWriteTimeUtc.Ticks -ne $taskFile.Mtime -or
        (Get-FileHash -LiteralPath $taskFile.Path -Algorithm SHA256).Hash -ne $taskFile.Hash) { throw "Source changed: $($taskFile.Path)" }
}
$taskBad = Join-Path $taskRoot 'unsafe.db'
$taskBadResult = & $DotNet $taskCli phase2 preflight --source-db $taskSource --source-scan $taskScan.ScanId --db $taskBad --output (Join-Path $taskBase 'bad-report') 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $taskBad)) { throw 'Unsafe destination not rejected.' }
$taskUnaccepted = & $DotNet $taskCli phase2 hash --db $taskDb 2>&1
if ($LASTEXITCODE -ne 1 -or ($taskUnaccepted -join ' ') -notmatch '--preflight is required') { throw 'Explicit Phase 2B acceptance gate failed.' }
Write-Output "Phase 2A smoke passed. Artifacts: $taskBase"
Write-Output ($taskResult | ConvertTo-Json)
Write-Output ($taskSummary | Select-Object selected_component_count,admitted_component_count,rejected_component_count,union_regular_file_count,
    union_directory_count,estimated_phase2b_read_bytes,chart_revalidation_bytes,preflight_manifest_sha256 | ConvertTo-Json)
