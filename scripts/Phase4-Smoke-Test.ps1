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
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'kick.wav'), "known audio $taskFolder", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'copy.wav'), "known audio $taskFolder", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'image.png'), 'known image', $taskUtf8)
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
$taskConditionalSyntax = Join-Path $taskBase 'conditional-syntax'
$null = Invoke-TaskCli 2 @('phase3','inspect','--chart',(Join-Path $taskRoot 'A\conditional.bms'),'--source-root',$taskRoot,'--output',$taskConditionalSyntax)
$taskConditional = Invoke-TaskCli 2 @('phase4','resolve','--db',$taskDb,'--content-session',"$($taskContent.SessionId)",'--accept-content-manifest',$taskContent.ManifestSha256,'--chart-report',(Join-Path $taskConditionalSyntax 'chart_inspection.json'),'--observation-root',$taskRootKey,'--output',(Join-Path $taskBase 'conditional'))
$taskBranches = (Get-Content -LiteralPath $taskConditional.Report -Raw | ConvertFrom-Json).declarations
if (@($taskBranches).Count -ne 3 -or $taskBranches[2].reachability.state -ne 'IMPOSSIBLE_WITHIN_MODEL') { throw 'Conditional alternatives or bounded justification lost.' }
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
