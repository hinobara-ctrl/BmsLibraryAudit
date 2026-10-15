param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 4 synthetic smoke requires PowerShell 7 or later.' }
if (-not $IsWindows) { throw 'Phase 4 requires Windows native safety checks.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build Release before running this smoke.' }
$taskBase = Join-Path $taskRepository ('.artifacts\phase4-smoke-' + [Guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'library'
$taskSource = Join-Path $taskBase 'audit.db'
$taskDb = Join-Path $taskBase 'phase2.db'
$taskUtf8 = [Text.UTF8Encoding]::new($false)
# Only this newly generated owned fixture is scanned. No real inputs or cleanup.
foreach ($taskFolder in @('A','B')) {
    $taskDirectory = Join-Path $taskRoot $taskFolder
    New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'one.bms'), "#WAV01 kick.wav`n#WAV02 copy.wav`n#BMP01 image.png`n#WAV03 absent.wav`n#00111:0102`n#00104:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'conditional.bms'), "#RANDOM 2`n#IF 1`n#WAV01 kick.wav`n#ELSEIF 2`n#WAV01 copy.wav`n#ELSE`n#WAV01 impossible.wav`n#ENDIF`n#ENDRANDOM`n#00111:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'duplicate.bms'), "#WAV01 kick.wav`n#WAV01 copy.wav`n#00111:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'fixed.bms'), "#SETRANDOM 2`n#IF 1`n#WAV01 kick.wav`n#ENDIF`n#IF 2`n#WAV01 copy.wav`n#ENDIF`n#ENDRANDOM`n#00111:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'switch.bms'), "#SWITCH 2`n#CASE 1`n#WAV01 kick.wav`n#SKIP`n#DEF`n#WAV01 copy.wav`n#ENDSW`n#00111:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'impossible.bms'), "#WAV01 kick.wav`n#RANDOM 2`n#IF 3`n#00111:01`n#ENDIF`n#ENDRANDOM", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'windows.bms'), "#WAV01 KICK.WAV`n#00111:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'timing.bms'), "#BPM01 150`n#00108:01", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'timing-unused.bms'), "#BPM01 150`n#00108:01`n#WAV01 kick.wav", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'audio.bmson'), '{"version":"1.0.0","sound_channels":[{"name":"stem","notes":[{"x":1,"y":0,"l":0,"c":false}]}]}', $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'kick.wav'), "known audio $taskFolder", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'copy.wav'), "known audio $taskFolder", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'image.png'), 'known image', $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'stem.ogg'), 'opaque candidate ogg', $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'stem.m4a'), 'opaque candidate m4a', $taskUtf8)
}
function Invoke-TaskCli([int]$Expected, [string[]]$Arguments) {
    $taskText = & $DotNet $taskCli @Arguments
    if ($LASTEXITCODE -ne $Expected) { throw "CLI status $LASTEXITCODE, expected $Expected : $($Arguments -join ' ')" }
    ($taskText -join "`n") | ConvertFrom-Json
}
$taskOriginalSources = (Get-ChildItem -LiteralPath $taskRoot -Recurse -File) | ForEach-Object {
    [pscustomobject]@{ Path = $_.FullName; Size = $_.Length; Mtime = $_.LastWriteTimeUtc.Ticks; Attributes = $_.Attributes; Hash = (Get-FileHash -LiteralPath $_.FullName).Hash }
}
$taskScan = Invoke-TaskCli 0 @('scan','--db',$taskSource,'--root',$taskRoot)
$taskPreflightOutput = Join-Path $taskBase 'preflight'
$taskPreflight = Invoke-TaskCli 0 @('phase2','preflight','--source-db',$taskSource,'--source-scan',"$($taskScan.ScanId)",'--db',$taskDb,'--output',$taskPreflightOutput)
$taskContent = Invoke-TaskCli 0 @('phase2','hash','--db',$taskDb,'--preflight',"$($taskPreflight.SessionId)",'--accept-manifest',$taskPreflight.ManifestSha256,'--output',(Join-Path $taskBase 'content'))
$taskChart = Join-Path $taskRoot 'A\one.bms'
$taskSyntax = Join-Path $taskBase 'syntax'
$null = Invoke-TaskCli 0 @('phase3','inspect','--chart',$taskChart,'--source-root',$taskRoot,'--output',$taskSyntax)
$taskReport = Join-Path $taskSyntax 'chart_inspection.json'
$taskRootKey = (Join-Path $taskRoot 'A').ToUpperInvariant()
$taskBefore = @((Get-ChildItem -LiteralPath $taskRoot -Recurse -File).FullName) + @($taskSource,$taskDb,$taskReport) | ForEach-Object {
    $taskItem = Get-Item -LiteralPath $_
    [pscustomobject]@{ Path = $_; Size = $taskItem.Length; Mtime = $taskItem.LastWriteTimeUtc.Ticks; Attributes = $taskItem.Attributes; Hash = (Get-FileHash -LiteralPath $_).Hash }
}
$taskArguments = @('phase4','resolve','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--chart-report',$taskReport,'--observation-root',$taskRootKey)
$taskOutput = Join-Path $taskBase 'resolved'
$taskResult = Invoke-TaskCli 0 ($taskArguments + @('--output',$taskOutput))
$taskResolution = Get-Content -LiteralPath $taskResult.Report -Raw | ConvertFrom-Json
if ($taskResolution.dependency_closure_proven -or $taskResolution.removal_authority -or $taskResolution.provenance.content_digest -ne $taskContent.ManifestSha256) { throw 'Incorrect authority/provenance.' }
if ($taskResolution.result_version -ne 'phase4-dependency-2' -or $taskResolution.semantics.relation_enumeration_coverage -ne 'COMPLETE' -or $taskResolution.semantics.bounded_proof.state -ne 'NOT_PROVEN') { throw 'Semantic graph or bounded proof promoted absent resource to closure.' }
$taskExpectedSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskUtf8.GetBytes('known audio A'))).ToLowerInvariant()
if ($taskResolution.declarations[0].resolution_state -ne 'OBSERVED_PATH_MATCH' -or $taskResolution.declarations[0].candidates[0].sha256 -ne $taskExpectedSha) { throw 'WAV path/hash differs from independent fixture expectation.' }
if ($taskResolution.declarations[1].candidates[0].sha256 -ne $taskExpectedSha -or $taskResolution.declarations[1].candidates[0].physical_file_id -eq $taskResolution.declarations[0].candidates[0].physical_file_id) { throw 'Multiplicity or same bytes/different name lost.' }
if ($taskResolution.declarations[3].resolution_state -ne 'NO_OBSERVED_MATCH') { throw 'No-match policy differs.' }
if ($taskResolution.provenance.chart_sha256 -ne (Get-FileHash -LiteralPath $taskChart).Hash.ToLowerInvariant()) { throw 'Chart bytes not bound independently.' }
# Deny all chart/resource opens during repeat resolution, including metadata
# handle probes. Directory-only native exclusion remains available.
$taskHeld = @()
try {
    foreach ($taskFile in (Get-ChildItem -LiteralPath $taskRoot -Recurse -File)) { $taskHeld += [IO.FileStream]::new($taskFile.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None) }
    $taskRepeat = Invoke-TaskCli 0 ($taskArguments + @('--output',(Join-Path $taskBase 'repeat')))
} finally { foreach ($taskHandle in $taskHeld) { $taskHandle.Dispose() } }
if ((Get-FileHash -LiteralPath $taskResult.Report).Hash -ne (Get-FileHash -LiteralPath $taskRepeat.Report).Hash) { throw 'Phase 4 output is not deterministic.' }
if ($taskResolution.semantics.version -ne 'phase4-semantic-graph-2' -or $taskResolution.semantics.bounded_proof.version -ne 'observed-logical-physical-ordinal-binding-evidence-2') { throw 'Reinforced proof contract is not versioned.' }
foreach ($taskContract in @(@('windows.bms','windows',0,1), @('timing.bms','timing',2,0), @('timing-unused.bms','timing-unused',2,1))) {
    $taskContractSyntax = Join-Path $taskBase ($taskContract[1] + '-syntax')
    $null = Invoke-TaskCli 0 @('phase3','inspect','--chart',(Join-Path $taskRoot ('A\' + $taskContract[0])),'--source-root',$taskRoot,'--output',$taskContractSyntax)
    $taskContractResult = Invoke-TaskCli $taskContract[2] @('phase4','resolve','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--chart-report',(Join-Path $taskContractSyntax 'chart_inspection.json'),'--observation-root',$taskRootKey,'--output',(Join-Path $taskBase $taskContract[1]))
    $taskContractReport = Get-Content -LiteralPath $taskContractResult.Report -Raw | ConvertFrom-Json
    $taskContractGraph = $taskContractReport.semantics
    if ($taskContractGraph.logical_reference_coverage.state -ne 'COMPLETE_WITHIN_ADMITTED_MODEL' -or $taskContractGraph.physical_resource_binding_coverage.physical_declaration_count -ne $taskContract[3] -or $taskContractGraph.physical_resource_binding_coverage.ordinal_binding_relation_count -ne 0 -or $taskContractGraph.bounded_proof.state -ne 'NOT_PROVEN' -or $taskContractReport.dependency_closure_proven -or $taskContractReport.removal_authority) { throw 'Logical coverage promoted Windows lookup or timing to ordinal physical proof.' }
    if ($taskContract[1] -eq 'windows') {
        if ($taskContractGraph.physical_resource_binding_coverage.observed_binding_relation_count -ne 1 -or $taskContractReport.declarations[0].resolution_state -ne 'OBSERVED_PATH_MATCH' -or $taskContractReport.declarations[0].path_policy -ne 'WINDOWS_ORDINAL_IGNORE_CASE' -or $taskContractReport.declarations[0].candidates[0].sha256 -ne $taskExpectedSha) { throw 'Windows policy correspondence or historical hash lost.' }
    } elseif ('NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE' -notin $taskContractGraph.bounded_proof.blockers) { throw 'Timing or unused physical declaration gained vacuous binding proof.' }
}
$taskConditionalSyntax = Join-Path $taskBase 'conditional-syntax'
$null = Invoke-TaskCli 2 @('phase3','inspect','--chart',(Join-Path $taskRoot 'A\conditional.bms'),'--source-root',$taskRoot,'--output',$taskConditionalSyntax)
$taskConditional = Invoke-TaskCli 2 @('phase4','resolve','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--chart-report',(Join-Path $taskConditionalSyntax 'chart_inspection.json'),'--observation-root',$taskRootKey,'--output',(Join-Path $taskBase 'conditional'))
$taskBranches = (Get-Content -LiteralPath $taskConditional.Report -Raw | ConvertFrom-Json).declarations
if (@($taskBranches).Count -ne 3 -or $taskBranches[2].reachability.state -ne 'IMPOSSIBLE_WITHIN_MODEL') { throw 'Conditional alternatives or bounded justification lost.' }
$taskGraph = (Get-Content -LiteralPath $taskConditional.Report -Raw | ConvertFrom-Json).semantics
if (@($taskGraph.relations).Count -ne 3 -or $taskGraph.usage_evidence[0].effective_declaration_state -ne 'BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL' -or $taskGraph.usage_evidence[0].declaration_coverage -ne 'DECLARATIONS_COVER_USAGE_WITHIN_MODEL') { throw 'Conditional bindings or coverage proof differ.' }
if (@($taskGraph.conservation_obligations).Count -ne 3 -or @($taskGraph.relations | Where-Object compatibility -eq 'CANNOT_CO_OCCUR_WITHIN_MODEL').Count -ne 1) { throw 'Impossible syntax or obligations lost.' }
foreach ($taskExtra in @(@('duplicate.bms','duplicate',2,'AMBIGUOUS_EFFECTIVE_DECLARATION'), @('fixed.bms','fixed',2,'SINGLE_CANDIDATE_WITHIN_MODEL'), @('switch.bms','switch',2,'BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL'), @('impossible.bms','impossible',2,'NO_COMPATIBLE_DECLARATION_WITHIN_MODEL'), @('audio.bmson','audio',0,'SINGLE_CANDIDATE_WITHIN_MODEL'))) {
    $taskExtraSyntax = Join-Path $taskBase ($taskExtra[1] + '-syntax')
    $null = Invoke-TaskCli $taskExtra[2] @('phase3','inspect','--chart',(Join-Path $taskRoot ('A\' + $taskExtra[0])),'--source-root',$taskRoot,'--output',$taskExtraSyntax)
    $taskExtraResult = Invoke-TaskCli 2 @('phase4','resolve','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--chart-report',(Join-Path $taskExtraSyntax 'chart_inspection.json'),'--observation-root',$taskRootKey,'--output',(Join-Path $taskBase $taskExtra[1]))
    $taskExtraReport = Get-Content -LiteralPath $taskExtraResult.Report -Raw | ConvertFrom-Json
    $taskExtraGraph = $taskExtraReport.semantics
    if ($taskExtraGraph.usage_evidence[0].effective_declaration_state -ne $taskExtra[3] -or $taskExtraReport.removal_authority -or $taskExtraReport.dependency_closure_proven) { throw 'Extra semantic fixture differs or grants authority.' }
    if ($taskExtra[1] -eq 'duplicate' -and ($taskExtraGraph.bounded_proof.state -ne 'NOT_PROVEN' -or @($taskExtraGraph.conservation_obligations).Count -ne 2)) { throw 'Duplicate precedence chosen or obligation lost.' }
    if ($taskExtra[1] -eq 'fixed' -and $taskExtraGraph.bounded_proof.state -ne 'PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE') { throw 'Fixed-value domain not proved for the scoped usage.' }
    if ($taskExtra[1] -eq 'switch' -and $taskExtraGraph.bounded_proof.state -ne 'PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE') { throw 'Explicit-SKIP SWITCH scope not proved.' }
    if ($taskExtra[1] -in @('fixed','switch') -and ($taskExtraGraph.physical_resource_binding_coverage.ordinal_binding_relation_count -lt 1 -or $taskExtraGraph.physical_resource_binding_coverage.state -ne 'COMPLETE_ORDINAL_OBSERVED_BINDINGS')) { throw 'Positive contract lacks a covered ordinal physical relation.' }
    if ($taskExtra[1] -eq 'impossible') {
        if ($taskExtraGraph.bounded_proof.state -ne 'NOT_PROVEN' -or 'NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL' -notin $taskExtraGraph.bounded_proof.blockers -or $taskExtraGraph.usage_evidence[0].declaration_coverage -ne 'USAGE_IMPOSSIBLE_WITHIN_MODEL') { throw 'Vacuous usage coverage promoted to positive binding proof.' }
        if (@($taskExtraGraph.relations).Count -ne 1 -or $taskExtraGraph.relations[0].usage_condition.state -ne 'IMPOSSIBLE_WITHIN_MODEL' -or @($taskExtraGraph.conservation_obligations).Count -ne 1 -or $taskExtraReport.declarations[0].resolution_state -ne 'OBSERVED_PATH_MATCH' -or $taskExtraReport.declarations[0].candidates[0].sha256 -ne $taskExpectedSha) { throw 'Impossible usage or observed physical obligation lost.' }
    }
    if ($taskExtra[1] -eq 'audio') {
        $taskLookup = $taskExtraGraph.bmson_audio_lookups[0]
        if (@($taskLookup.candidates).Count -ne 2 -or $taskLookup.codec_compatibility_proven -or $taskExtraGraph.bounded_proof.state -ne 'NOT_PROVEN') { throw 'Audio search candidates selected or usability invented.' }
        foreach ($taskCandidate in $taskLookup.candidates) {
            $taskPayload = if ($taskCandidate.relative_path -eq 'stem.ogg') { 'opaque candidate ogg' } elseif ($taskCandidate.relative_path -eq 'stem.m4a') { 'opaque candidate m4a' } else { throw 'Unexpected audio search member.' }
            $taskKnown = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskUtf8.GetBytes($taskPayload))).ToLowerInvariant()
            if ($taskCandidate.sha256 -ne $taskKnown) { throw 'Audio candidate digest differs from independent fixture bytes.' }
        }
    }
}
$taskBadOutput = Join-Path $taskBase 'bad-acceptance'
$taskBad = & $DotNet $taskCli phase4 resolve --db $taskDb --content-session $taskContent.SessionId --accept-content-manifest ('0' * 64) --chart-report $taskReport --observation-root $taskRootKey --output $taskBadOutput 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $taskBadOutput)) { throw 'Wrong accepted CONTENT digest not refused.' }
$taskUnsafe = Join-Path $taskRoot 'B\unsafe'
$taskBad = & $DotNet $taskCli @taskArguments --output $taskUnsafe 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $taskUnsafe)) { throw 'Protected source root output not refused.' }
foreach ($taskFile in (@($taskOriginalSources) + @($taskBefore))) {
    $taskCurrent = Get-Item -LiteralPath $taskFile.Path
    if ($taskCurrent.Length -ne $taskFile.Size -or $taskCurrent.LastWriteTimeUtc.Ticks -ne $taskFile.Mtime -or $taskCurrent.Attributes -ne $taskFile.Attributes -or (Get-FileHash -LiteralPath $taskFile.Path).Hash -ne $taskFile.Hash) { throw "Input changed: $($taskFile.Path)" }
}
foreach ($taskEvidenceDb in @($taskSource,$taskDb)) { foreach ($taskSuffix in @('-journal','-wal','-shm')) { if (Test-Path -LiteralPath ($taskEvidenceDb + $taskSuffix)) { throw 'Evidence database sidecar created.' } } }
Write-Output "Phase 4 synthetic end-to-end smoke passed. Artifacts: $taskBase"
