param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 5 smoke requires PowerShell 7 or later.' }
if (-not $IsWindows) { throw 'Phase 5 requires Windows native safety checks.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build Release before smoke.' }
$taskBase = Join-Path $taskRepository ('.artifacts\phase5-smoke-' + [guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'synthetic-library'
$taskSource = Join-Path $taskBase 'audit.db'
$taskDb = Join-Path $taskBase 'phase2.db'
$taskUtf8 = [Text.UTF8Encoding]::new($false)
$taskChart = "#WAV01 kick.wav`n#BMP01 image.png`n#00111:01`n#00104:01"
foreach ($taskName in @('A','B','C')) {
    $taskFolder = Join-Path $taskRoot $taskName
    New-Item -ItemType Directory -Force -Path $taskFolder | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskFolder 'one.bms'), $taskChart, $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskFolder 'kick.wav'), $(if ($taskName -eq 'C') { 'variant audio C' } else { 'known audio AB' }), $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskFolder 'image.png'), $(if ($taskName -eq 'C') { 'variant image C' } else { 'known image AB' }), $taskUtf8)
}
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\extra.txt'), 'opaque auxiliary B', $taskUtf8)
function Invoke-TaskCli([int]$Expected,[string[]]$Arguments) {
    $taskText = & $DotNet $taskCli @Arguments
    if ($LASTEXITCODE -ne $Expected) { throw "CLI exit $LASTEXITCODE expected $Expected : $($Arguments -join ' ')" }
    ($taskText -join "`n") | ConvertFrom-Json
}
function Get-TaskStamp([string]$Path) {
    $taskItem = Get-Item -LiteralPath $Path
    [pscustomobject]@{Path=$Path;Size=$taskItem.Length;Mtime=$taskItem.LastWriteTimeUtc.Ticks;Attributes=$taskItem.Attributes;Hash=(Get-FileHash -LiteralPath $Path).Hash}
}
$taskOriginal = @((Get-ChildItem -LiteralPath $taskRoot -Recurse -File) | ForEach-Object { Get-TaskStamp $_.FullName })
$taskScan = Invoke-TaskCli 0 @('scan','--db',$taskSource,'--root',$taskRoot)
$taskPreflight = Invoke-TaskCli 0 @('phase2','preflight','--source-db',$taskSource,'--source-scan',"$($taskScan.ScanId)",'--db',$taskDb,'--output',(Join-Path $taskBase 'preflight'))
$taskContent = Invoke-TaskCli 0 @('phase2','hash','--db',$taskDb,'--preflight',"$($taskPreflight.SessionId)",'--accept-manifest',$taskPreflight.ManifestSha256,'--output',(Join-Path $taskBase 'content'))
$taskReports = @()
foreach ($taskName in @('A','B','C')) {
    $taskSyntax = Join-Path $taskBase ($taskName + '-syntax')
    $null = Invoke-TaskCli 0 @('phase3','inspect','--chart',(Join-Path $taskRoot ($taskName + '\one.bms')),'--source-root',$taskRoot,'--output',$taskSyntax)
    $taskResolved = Invoke-TaskCli 0 @('phase4','resolve','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--chart-report',(Join-Path $taskSyntax 'chart_inspection.json'),'--observation-root',(Join-Path $taskRoot $taskName).ToUpperInvariant(),'--output',(Join-Path $taskBase ($taskName + '-resolved')))
    $taskReports += $taskResolved.Report
}
$taskPhaseFour = Get-Content -LiteralPath $taskReports[0] -Raw | ConvertFrom-Json
$taskComponent = $taskPhaseFour.provenance.component_key
$taskArguments = @('phase5','classify','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--component',$taskComponent)
foreach ($taskReport in $taskReports) { $taskArguments += @('--phase4-report',$taskReport) }
$taskBefore = @($taskSource,$taskDb) + $taskReports | ForEach-Object { Get-TaskStamp $_ }
$taskHeld = @()
try {
    foreach ($taskFile in (Get-ChildItem -LiteralPath $taskRoot -Recurse -File)) { $taskHeld += [IO.FileStream]::new($taskFile.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None) }
    $taskClassification = Invoke-TaskCli 0 ($taskArguments + @('--output',(Join-Path $taskBase 'classified')))
    $taskRepeat = Invoke-TaskCli 0 ($taskArguments + @('--output',(Join-Path $taskBase 'repeat')))
} finally { foreach ($taskHandle in $taskHeld) { $taskHandle.Dispose() } }
if ((Get-FileHash -LiteralPath $taskClassification.Report).Hash -ne (Get-FileHash -LiteralPath $taskRepeat.Report).Hash) { throw 'Classification is not deterministic.' }
$taskResult = Get-Content -LiteralPath $taskClassification.Report -Raw | ConvertFrom-Json
if ($taskResult.result_version -ne 'phase5-conservative-classification-2' -or $taskResult.coverage -ne 'COMPLETE' -or @($taskResult.roots).Count -ne 3 -or @($taskResult.relationships).Count -ne 3) { throw 'Component/pair coverage incorrect.' }
if ($taskResult.dependency_closure_proven -or $taskResult.package_equivalence_proven -or $taskResult.merge_authority -or $taskResult.removal_authority) { throw 'Operational authority granted.' }
if ($taskResult.provenance.content_digest -ne $taskContent.ManifestSha256 -or $taskResult.provenance.parent_preflight_id -ne $taskPreflight.SessionId) { throw 'Accepted provenance lost.' }
$taskA = $taskResult.roots | Where-Object key -eq (Join-Path $taskRoot 'A').ToUpperInvariant()
$taskB = $taskResult.roots | Where-Object key -eq (Join-Path $taskRoot 'B').ToUpperInvariant()
$taskC = $taskResult.roots | Where-Object key -eq (Join-Path $taskRoot 'C').ToUpperInvariant()
$taskAB = $taskResult.relationships | Where-Object { $_.root_a -eq $taskA.id -and $_.root_b -eq $taskB.id }
if (-not $taskAB.tree_subset_a_in_b -or $taskAB.observed_tree_exact -or $taskAB.content_multiset_exact -or @($taskAB.conflicts).Count -ne 0 -or 'OBSERVED_TREE_SUBSET_A_IN_B' -notin $taskAB.labels) { throw 'Independent A/B subset expectation differs.' }
if ('CHART_EXACT_WITH_NONCHART_DIFFERENCES' -notin $taskAB.labels -or 'CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT' -in $taskAB.labels -or 'CHART_EXACT_RESOURCE_VARIANT' -in $taskAB.labels) { throw 'Auxiliary extra.txt was attributed to a chart resource.' }
if (@($taskResult.relationships | Where-Object { 'HARD_PATH_CONTENT_CONFLICT' -in $_.labels }).Count -ne 2) { throw 'A/C and B/C hard conflicts not retained independently.' }
foreach ($taskPair in @($taskResult.relationships | Where-Object { 'HARD_PATH_CONTENT_CONFLICT' -in $_.labels })) {
    if ('CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT' -notin $taskPair.labels -or 'OBSERVED_COMPLEMENTARY_CONTENT' -in $taskPair.labels) { throw 'Admitted potential variants or coexistence conflicts misclassified.' }
}
foreach ($taskObservedRoot in $taskResult.roots) {
    if ($taskObservedRoot.package_boundary -ne 'PACKAGE_BOUNDARY_UNKNOWN' -or $taskObservedRoot.semantic_trust -ne 'PROVEN_WITHIN_NAMED_MODEL' -or @($taskObservedRoot.dependencies).Count -ne 1) { throw 'Package boundary inferred or semantic admission lost.' }
    $taskKick = $taskObservedRoot.entries | Where-Object relative_path -eq 'kick.wav'
    $taskPayload = if ($taskObservedRoot.id -eq $taskC.id) { 'variant audio C' } else { 'known audio AB' }
    $taskExpected = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskUtf8.GetBytes($taskPayload))).ToLowerInvariant()
    if ($taskKick.sha256 -ne $taskExpected -or @($taskObservedRoot.entries | Where-Object is_chart).Count -ne 1) { throw 'Independent hash/chart membership expectation differs.' }
}
$taskPhysicalOnly = Invoke-TaskCli 2 @('phase5','classify','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--component',$taskComponent,'--output',(Join-Path $taskBase 'physical-only'))
$taskIndependent = Get-Content -LiteralPath $taskPhysicalOnly.Report -Raw | ConvertFrom-Json
if (@($taskIndependent.relationships).Count -ne 3 -or @($taskIndependent.relationships | Where-Object trust -ne 'PROVEN_FROM_ACCEPTED_CONTENT').Count) { throw 'Absent Phase 4 invalidated complete physical comparisons.' }
if (@($taskIndependent.relationships | Where-Object { 'CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT' -in $_.labels -or 'CHART_EXACT_RESOURCE_VARIANT' -in $_.labels }).Count) { throw 'Physical-only evidence gained a chart-resource variant claim.' }
foreach ($taskStamp in (@($taskOriginal) + @($taskBefore))) {
    $taskNow = Get-TaskStamp $taskStamp.Path
    if ($taskNow.Hash -ne $taskStamp.Hash -or $taskNow.Size -ne $taskStamp.Size -or $taskNow.Mtime -ne $taskStamp.Mtime -or $taskNow.Attributes -ne $taskStamp.Attributes) { throw "Input changed: $($taskStamp.Path)" }
}
foreach ($taskEvidence in @($taskSource,$taskDb)) { foreach ($taskSuffix in @('-journal','-wal','-shm')) { if (Test-Path -LiteralPath ($taskEvidence + $taskSuffix)) { throw 'Database sidecar created.' } } }
Write-Output "Phase 5 synthetic end-to-end smoke passed. Artifacts: $taskBase"
