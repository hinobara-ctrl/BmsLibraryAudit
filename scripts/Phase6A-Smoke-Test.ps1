param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 6A synthetic smoke requires PowerShell 7+.' }
if (-not $IsWindows) { throw 'Phase 6A requires Windows native safety checks.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build Release before the smoke.' }
$taskBase = Join-Path $taskRepository ('.artifacts\phase6a-smoke-' + [guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'synthetic-library'
$taskSource = Join-Path $taskBase 'audit.db'
$taskDb = Join-Path $taskBase 'phase2.db'
$taskUtf8 = [Text.UTF8Encoding]::new($false)
foreach ($taskName in @('A','B','C')) {
    $taskFolder = Join-Path $taskRoot $taskName
    New-Item -ItemType Directory -Path (Join-Path $taskFolder 'empty') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskFolder 'one.bms'), "#WAV01 kick.wav`n#00111:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskFolder 'kick.wav'), $(if ($taskName -eq 'C') { 'conflicting C' } else { 'shared AB' }), $taskUtf8)
}
[IO.File]::WriteAllText((Join-Path $taskRoot 'A\opaque'), 'auxiliary A', $taskUtf8)
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\exclusive.txt'), 'auxiliary B', $taskUtf8)
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\same-other-name.wav'), 'shared AB', $taskUtf8)
function Invoke-TaskCli([int]$Expected, [string[]]$Arguments) {
    $taskText = & $DotNet $taskCli @Arguments
    if ($LASTEXITCODE -ne $Expected) { throw "CLI returned $LASTEXITCODE, expected $Expected : $($Arguments -join ' ')" }
    ($taskText -join "`n") | ConvertFrom-Json
}
function Get-TaskStamp([string]$Path) {
    $taskItem = Get-Item -LiteralPath $Path
    [pscustomobject]@{Path=$Path;Length=$taskItem.Length;Mtime=$taskItem.LastWriteTimeUtc.Ticks;Attributes=$taskItem.Attributes;Sha256=(Get-FileHash -LiteralPath $Path).Hash}
}
$taskOriginals = @((Get-ChildItem -LiteralPath $taskRoot -Recurse -File) | ForEach-Object { Get-TaskStamp $_.FullName })
$taskTree = @(Get-ChildItem -LiteralPath $taskRoot -Recurse | Sort-Object FullName | Select-Object -ExpandProperty FullName)
$taskScan = Invoke-TaskCli 0 @('scan','--db',$taskSource,'--root',$taskRoot)
$taskPreflight = Invoke-TaskCli 0 @('phase2','preflight','--source-db',$taskSource,'--source-scan',"$($taskScan.ScanId)",'--db',$taskDb,'--output',(Join-Path $taskBase 'preflight'))
$taskContent = Invoke-TaskCli 0 @('phase2','hash','--db',$taskDb,'--preflight',"$($taskPreflight.SessionId)",'--accept-manifest',$taskPreflight.ManifestSha256,'--output',(Join-Path $taskBase 'content'))
$taskRoots = @(Import-Csv -LiteralPath (Join-Path $taskBase 'content\observation_roots.csv'))
$taskComponent = $taskRoots[0].component_key
$taskArguments = @('--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--component',$taskComponent,'--root',(Join-Path $taskRoot 'A').ToUpperInvariant(),'--root',(Join-Path $taskRoot 'B').ToUpperInvariant())
$taskConflicting = @('--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--component',$taskComponent,'--root',(Join-Path $taskRoot 'A').ToUpperInvariant(),'--root',(Join-Path $taskRoot 'C').ToUpperInvariant())
$taskEvidenceBefore = @($taskSource,$taskDb) | ForEach-Object { Get-TaskStamp $_ }
$taskHeld = @()
try {
    foreach ($taskFile in (Get-ChildItem -LiteralPath $taskRoot -Recurse -File)) { $taskHeld += [IO.FileStream]::new($taskFile.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None) }
    $taskPlan = Invoke-TaskCli 0 (@('phase6','plan') + $taskArguments + @('--output',(Join-Path $taskBase 'plan')))
    $taskRepeat = Invoke-TaskCli 0 (@('phase6','plan') + $taskArguments + @('--output',(Join-Path $taskBase 'repeat')))
    $taskVerified = Invoke-TaskCli 0 (@('phase6','verify') + $taskArguments + @('--plan',$taskPlan.Report))
    $taskConflict = Invoke-TaskCli 2 (@('phase6','plan') + $taskConflicting + @('--output',(Join-Path $taskBase 'conflict')))
    $taskConflictVerified = Invoke-TaskCli 2 (@('phase6','verify') + $taskConflicting + @('--plan',$taskConflict.Report))
} finally { foreach ($taskHandle in $taskHeld) { $taskHandle.Dispose() } }
if ((Get-FileHash -LiteralPath $taskPlan.Report).Hash -ne (Get-FileHash -LiteralPath $taskRepeat.Report).Hash -or $taskPlan.PlanSha256 -ne $taskVerified.PlanSha256) { throw 'Deterministic generation or verification failed.' }
$taskResult = (Get-Content -LiteralPath $taskPlan.Report -Raw | ConvertFrom-Json).plan
$taskExpectedPaths = @('.','empty','exclusive.txt','kick.wav','one.bms','opaque','same-other-name.wav')
if ((@($taskResult.virtual_entries.relative_path) -join '|') -cne ($taskExpectedPaths -join '|')) { throw 'Independent virtual-tree expectation failed.' }
if ($taskResult.counts.source_appearances -ne 11 -or $taskResult.counts.virtual_entries -ne 7 -or $taskResult.counts.chart_appearances -ne 2 -or @($taskResult.chart_obligations).Count -ne 2) { throw 'Appearance or chart accounting differs.' }
$taskShared = @($taskResult.virtual_entries | Where-Object relative_path -eq 'kick.wav')[0]
$taskExpectedSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskUtf8.GetBytes('shared AB'))).ToLowerInvariant()
if (@($taskShared.sources).Count -ne 2 -or $taskShared.sha256 -ne $taskExpectedSha -or @($taskResult.virtual_entries | Where-Object sha256 -eq $taskExpectedSha).Count -ne 2) { throw 'Hash/path multiplicity or shared provenance lost.' }
foreach ($taskReport in @($taskPlan.Report,$taskConflict.Report)) {
    $taskData = (Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json).plan
    if ($taskData.states.merge_authority -or $taskData.states.removal_authority -or $taskData.states.dependency_closure_proven -or $taskData.states.semantic_evidence_state -ne 'SEMANTIC_EVIDENCE_UNKNOWN' -or $taskData.states.construction_state -ne 'NOT_CONSTRUCTED' -or $taskData.states.relocation_state -ne 'RELOCATION_NOT_PROVEN') { throw 'False operational authority or semantic coverage.' }
    if (@($taskData.conservation).Count -ne $taskData.counts.source_appearances) { throw 'Source occurrence disappeared.' }
}
$taskBlocked = (Get-Content -LiteralPath $taskConflict.Report -Raw | ConvertFrom-Json).plan
if ($taskBlocked.states.physical_union_state -ne 'UNION_BLOCKED_CONFLICT' -or @($taskBlocked.conflicts).Count -ne 1 -or @($taskBlocked.virtual_entries | Where-Object relative_path -eq 'kick.wav').Count -ne 0 -or $taskBlocked.counts.blocked_appearances -ne 2) { throw 'Independent conflict expectation failed.' }
foreach ($taskStamp in (@($taskOriginals) + @($taskEvidenceBefore))) {
    $taskNow = Get-TaskStamp $taskStamp.Path
    if ($taskNow.Sha256 -ne $taskStamp.Sha256 -or $taskNow.Length -ne $taskStamp.Length -or $taskNow.Mtime -ne $taskStamp.Mtime -or $taskNow.Attributes -ne $taskStamp.Attributes) { throw "Input changed: $($taskStamp.Path)" }
}
if ((@(Get-ChildItem -LiteralPath $taskRoot -Recurse | Sort-Object FullName | Select-Object -ExpandProperty FullName) -join '|') -cne ($taskTree -join '|')) { throw 'Source tree changed or consolidation materialized.' }
foreach ($taskDatabase in @($taskSource,$taskDb)) { foreach ($taskSuffix in @('-journal','-wal','-shm')) { if (Test-Path -LiteralPath ($taskDatabase + $taskSuffix)) { throw 'SQLite sidecar created.' } } }
Write-Output "Phase 6A synthetic virtual-union smoke passed. Artifacts: $taskBase"
