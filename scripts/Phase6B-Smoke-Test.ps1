param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 6B synthetic smoke requires PowerShell 7+.' }
if (-not $IsWindows) { throw 'Phase 6B requires Windows native safety checks.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build Release before the smoke.' }
$taskBase = Join-Path $taskRepository ('.artifacts\phase6b-smoke-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBase | Out-Null
$taskUtf8 = [Text.UTF8Encoding]::new($false)
$script:taskCall = 0
function Invoke-TaskCli([int]$Expected, [string[]]$Arguments) {
    $script:taskCall++
    $taskText = & $DotNet $taskCli @Arguments 2> (Join-Path $taskBase "$script:taskCall.stderr.log")
    $taskExit = $LASTEXITCODE
    $taskText | Set-Content -LiteralPath (Join-Path $taskBase "$script:taskCall.stdout.log")
    if ($taskExit -ne $Expected) { throw "CLI returned $taskExit, expected $Expected : $($Arguments -join ' ')" }
    ($taskText -join "`n") | ConvertFrom-Json
}
function Get-TaskStamp([string]$Path) {
    $taskItem = Get-Item -LiteralPath $Path
    [pscustomobject]@{ Path=$Path; Directory=$taskItem.PSIsContainer; Mtime=$taskItem.LastWriteTimeUtc.Ticks; Attributes=$taskItem.Attributes;
        Length=$(if ($taskItem.PSIsContainer) { $null } else { $taskItem.Length }); Sha256=$(if ($taskItem.PSIsContainer) { $null } else { (Get-FileHash -LiteralPath $Path).Hash }) }
}
foreach ($taskScenario in @('conflicting','compatible')) {
    $taskLab = Join-Path $taskBase $taskScenario
    $taskRoot = Join-Path $taskLab 'synthetic-library'
    $taskSource = Join-Path $taskLab 'audit.db'; $taskDb = Join-Path $taskLab 'phase2.db'
    $taskNames = @('SongA','AnotherFolder','Untitled-17')
    $taskCharts = @('normal.bms','alternate.bms','chart.bms')
    # Known seed maps are also the independent expected byte/path oracle.
    $taskPayloads = @(
        [ordered]@{ 'normal.bms'="#WAV01 kick.wav`n#00111:01"; 'kick.wav'='X'; 'opaque.A'='unknown A' },
        [ordered]@{ 'alternate.bms'="#WAV01 kick.wav`n#00111:01"; 'kick.wav'='X'; 'aux/note'='auxiliary B' },
        [ordered]@{ 'chart.bms'="#WAV01 kick.wav`n#00111:01"; 'kick.wav'=$(if ($taskScenario -eq 'conflicting') { 'Y' } else { 'X' }); 'opaque.C'='unknown C' }
    )
    for ($taskI = 0; $taskI -lt 3; $taskI++) {
        $taskFolder = Join-Path $taskRoot $taskNames[$taskI]
        New-Item -ItemType Directory -Path (Join-Path $taskFolder 'empty') -Force | Out-Null
        foreach ($taskPath in $taskPayloads[$taskI].Keys) {
            $taskFile = Join-Path $taskFolder $taskPath
            [IO.Directory]::CreateDirectory((Split-Path -Parent $taskFile)) | Out-Null
            [IO.File]::WriteAllText($taskFile, $taskPayloads[$taskI][$taskPath], $taskUtf8)
        }
    }
    $taskOriginals = @(Get-ChildItem -LiteralPath $taskRoot -Recurse | ForEach-Object { Get-TaskStamp $_.FullName }) + @(Get-TaskStamp $taskRoot)
    $taskScan = Invoke-TaskCli 0 @('scan','--db',$taskSource,'--root',$taskRoot)
    $taskPreflight = Invoke-TaskCli 0 @('phase2','preflight','--source-db',$taskSource,'--source-scan',"$($taskScan.ScanId)",'--db',$taskDb,'--output',(Join-Path $taskLab 'preflight'))
    $taskContent = Invoke-TaskCli 0 @('phase2','hash','--db',$taskDb,'--preflight',"$($taskPreflight.SessionId)",'--accept-manifest',$taskPreflight.ManifestSha256,'--output',(Join-Path $taskLab 'content'))
    $taskRoots = @(Import-Csv -LiteralPath (Join-Path $taskLab 'content\observation_roots.csv'))
    if ($taskRoots.Count -ne 3 -or @($taskRoots.component_key | Select-Object -Unique).Count -ne 1) { throw 'Fixture is outside one three-root EXACT_ONLY component.' }
    $taskArguments = @('--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--component',$taskRoots[0].component_key)
    foreach ($taskName in $taskNames) { $taskArguments += @('--root',(Join-Path $taskRoot $taskName).ToUpperInvariant()) }
    $taskEvidenceBefore = @($taskSource,$taskDb) | ForEach-Object { Get-TaskStamp $_ }
    $taskExpectedExit = if ($taskScenario -eq 'conflicting') { 2 } else { 0 }
    $taskHeld = @()
    try {
        foreach ($taskFile in (Get-ChildItem -LiteralPath $taskRoot -Recurse -File)) { $taskHeld += [IO.FileStream]::new($taskFile.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None) }
        $taskPlan = Invoke-TaskCli $taskExpectedExit (@('phase6','group-plan') + $taskArguments + @('--output',(Join-Path $taskLab 'plan')))
        $taskRepeat = Invoke-TaskCli $taskExpectedExit (@('phase6','group-plan') + $taskArguments + @('--output',(Join-Path $taskLab 'repeat')))
        $taskVerified = Invoke-TaskCli $taskExpectedExit (@('phase6','group-verify') + $taskArguments + @('--plan',$taskPlan.Report))
    } finally { foreach ($taskHandle in $taskHeld) { $taskHandle.Dispose() } }
    if ($taskPlan.PlanSha256 -ne $taskVerified.PlanSha256 -or (Get-FileHash $taskPlan.Report).Hash -ne (Get-FileHash $taskRepeat.Report).Hash) { throw 'Group replay or determinism failed.' }
    $taskData = (Get-Content -LiteralPath $taskPlan.Report -Raw | ConvertFrom-Json).plan
    if ($taskData.subsets.Count -ne 7 -or $taskData.counts.roots -ne 3 -or $taskData.chart_obligations.Count -ne 3) { throw 'Subset or chart appearance lost.' }
    $taskGroups = @()
    foreach ($taskCandidate in $taskData.candidates) {
        $taskMembers = @($taskData.roots | Where-Object { $taskCandidate.root_ids -contains $_.id })
        $taskGroups += (@($taskMembers | ForEach-Object { Split-Path -Leaf $_.original_path } | Sort-Object) -join '+')
        $taskExpectedFiles = [ordered]@{}
        $taskExpectedPaths = @('.','empty')
        for ($taskI = 0; $taskI -lt 3; $taskI++) {
            if ($taskMembers.original_path -contains (Join-Path $taskRoot $taskNames[$taskI])) {
                foreach ($taskPath in $taskPayloads[$taskI].Keys) { $taskExpectedFiles[$taskPath] = $taskPayloads[$taskI][$taskPath]; $taskExpectedPaths += $taskPath }
                if ($taskI -eq 1) { $taskExpectedPaths += 'aux' }
            }
        }
        $taskExpectedPaths = @($taskExpectedPaths | Sort-Object -Unique -CaseSensitive)
        if (($taskCandidate.virtual_entries.relative_path -join '|') -cne ($taskExpectedPaths -join '|')) { throw 'Independent whole-namespace oracle failed.' }
        foreach ($taskTarget in @($taskCandidate.virtual_entries | Where-Object kind -eq 'REGULAR_FILE')) {
            $taskSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskUtf8.GetBytes($taskExpectedFiles[$taskTarget.relative_path]))).ToLowerInvariant()
            if ($taskTarget.sha256 -ne $taskSha) { throw 'Known payload digest differs.' }
        }
        if ($taskCandidate.conservation.Count -ne $taskData.counts.source_appearances) { throw 'Appearance ledger incomplete.' }
        foreach ($taskVariant in $taskCandidate.variants) {
            $taskOriginalRoot = @($taskData.roots | Where-Object id -eq $taskVariant.original_root.id)[0]
            if (($taskVariant.original_root | ConvertTo-Json -Depth 25 -Compress) -cne ($taskOriginalRoot | ConvertTo-Json -Depth 25 -Compress)) { throw 'Variant is not a complete original distribution.' }
            if ($taskVariant.preservation_state -ne 'PRESERVE_IN_PLACE' -or $taskVariant.relocation_state -ne 'RELOCATION_NOT_PROVEN') { throw 'False variant relocation permission.' }
        }
    }
    $taskExpectedGroups = if ($taskScenario -eq 'conflicting') { @('AnotherFolder+SongA','Untitled-17') } else { @('AnotherFolder+SongA+Untitled-17') }
    if ((@($taskGroups | Sort-Object) -join '|') -cne ($taskExpectedGroups -join '|')) { throw 'Independent maximal-group oracle failed.' }
    $taskStates = $taskData.states
    if ($taskStates.main_group_selected -or $taskStates.relocation_safe -or $taskStates.merge_authority -or $taskStates.removal_authority -or $taskStates.dependency_closure_proven -or $taskStates.construction_state -ne 'NOT_CONSTRUCTED' -or $taskStates.physical_verification_state -ne 'NOT_PHYSICALLY_VERIFIED') { throw 'False selection, physical verification or authority.' }
    if ($taskScenario -eq 'conflicting' -and ($taskStates.selection_state -ne 'HUMAN_SELECTION_REQUIRED' -or $taskData.conflicts.Count -ne 2)) { throw 'Conflict alternatives require human selection.' }
    foreach ($taskStamp in (@($taskOriginals) + @($taskEvidenceBefore))) {
        if ((Get-TaskStamp $taskStamp.Path | ConvertTo-Json -Compress) -cne ($taskStamp | ConvertTo-Json -Compress)) { throw "Source/evidence changed: $($taskStamp.Path)" }
    }
    if (@(Get-ChildItem -LiteralPath $taskRoot -Recurse).Count + 1 -ne $taskOriginals.Count) { throw 'Tree changed or consolidation materialized.' }
    foreach ($taskDatabase in @($taskSource,$taskDb)) { foreach ($taskSuffix in @('-journal','-wal','-shm')) { if (Test-Path -LiteralPath ($taskDatabase + $taskSuffix)) { throw 'SQLite sidecar created.' } } }
}
Write-Output "Phase 6B synthetic CLI smoke passed (conflicting and compatible triples). Artifacts: $taskBase"
