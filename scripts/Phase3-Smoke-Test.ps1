param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 3 synthetic smoke requires PowerShell 7 or later.' }
if (-not $IsWindows) { throw 'Phase 3 file inspection requires Windows native identity checks.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build Release before running this smoke.' }
$taskBase = Join-Path $taskRepository ('.artifacts\phase3-smoke-' + [Guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'library'
$taskSources = Join-Path $taskRoot 'SongA'
New-Item -ItemType Directory -Path $taskSources | Out-Null
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'SongB') | Out-Null
$taskBms = Join-Path $taskSources 'simple.bms'
$taskConditional = Join-Path $taskSources 'conditional.bms'
$taskBmson = Join-Path $taskSources 'sound.bmson'
$taskBom = [Text.UTF8Encoding]::new($true)
$taskUtf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($taskBms, "#TITLE Fixture`n#WAV01 音 楽.wav`n#BMP01 image.png`n#00111:0101`n#00104:01", $taskBom)
[IO.File]::WriteAllText($taskConditional, "#RANDOM 2`n#IF 1`n#WAV01 a.wav`n#ENDIF`n#IF 2`n#WAV01 b.wav`n#ENDIF`n#ENDRANDOM`n#00111:01", $taskUtf8)
[IO.File]::WriteAllText($taskBmson, '{"version":"1.0.0","sound_channels":[{"name":"stem.wav","notes":[{"x":0,"y":0,"l":0,"c":false},{"x":1,"y":240,"l":120,"c":true}]}],"bga":{"bga_header":[{"id":1,"name":"movie.webm"}],"bga_events":[{"id":1,"y":0}]}}', $taskUtf8)
# All input/output paths are generated under this unique owned directory.
# No databases, recursive library scan or cleanup is performed.
$taskBefore = @(Get-ChildItem -LiteralPath $taskSources -File | ForEach-Object {
    [pscustomobject]@{ Path = $_.FullName; Size = $_.Length; Mtime = $_.LastWriteTimeUtc.Ticks; Attributes = $_.Attributes; Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
foreach ($taskCase in @(@($taskBms,'bms',0,2,3), @($taskConditional,'conditional',2,2,1), @($taskBmson,'bmson',0,2,3))) {
    $taskChart = $taskCase[0]; $taskLabel = $taskCase[1]; $taskOutput = Join-Path $taskBase $taskLabel
    $taskJson = & $DotNet $taskCli phase3 inspect --chart $taskChart --source-root $taskRoot --output $taskOutput
    if ($LASTEXITCODE -ne $taskCase[2]) { throw "Unexpected inspection status: $taskLabel" }
    $taskResult = ($taskJson -join "`n") | ConvertFrom-Json
    if ($taskResult.Declarations -ne $taskCase[3] -or $taskResult.Usages -ne $taskCase[4] -or $taskResult.DependencyClosureProven -ne $false) { throw "Incorrect extraction: $taskLabel" }
    $taskReport = Join-Path $taskOutput 'chart_inspection.json'
    $taskAnalysis = (Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json).analysis
    if ($taskAnalysis.sha256 -ne (Get-FileHash -LiteralPath $taskChart -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Byte identity differs from independent PowerShell hashing.' }
    $taskOriginal = [Convert]::FromBase64String($taskAnalysis.original_bytes)
    if ([Convert]::ToBase64String($taskOriginal) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($taskChart))) { throw 'Retained original bytes differ.' }
    if ($taskLabel -eq 'bms' -and $taskAnalysis.declarations[0].written_path -ne '音 楽.wav') { throw 'Unicode path changed.' }
    if ($taskLabel -eq 'conditional' -and (@($taskAnalysis.declarations.written_path) -join ',') -ne 'a.wav,b.wav') { throw 'A potential branch was discarded.' }
    if ($taskLabel -eq 'bmson' -and ($taskAnalysis.usages[0].kind -ne 'BGM_EVENT' -or $taskAnalysis.usages[1].continuation -ne $true)) { throw 'BMSON sound semantics were lost.' }
    $taskRepeat = Join-Path $taskBase ($taskLabel + '-repeat')
    & $DotNet $taskCli phase3 inspect --chart $taskChart --source-root $taskRoot --output $taskRepeat | Out-Null
    if ($LASTEXITCODE -ne $taskCase[2] -or (Get-FileHash -LiteralPath $taskReport).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskRepeat 'chart_inspection.json')).Hash) { throw 'Non-deterministic inspection report.' }
    Write-Output ($taskResult | ConvertTo-Json)
}
$taskUnsafe = Join-Path $taskSources 'unsafe-output'
$taskRefusal = & $DotNet $taskCli phase3 inspect --chart $taskBms --source-root $taskRoot --output $taskUnsafe 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $taskUnsafe)) { throw 'Unsafe destination was not refused before writes.' }
$taskSibling = Join-Path $taskRoot 'SongB\reports'
$taskRefusal = & $DotNet $taskCli phase3 inspect --chart $taskBms --source-root $taskRoot --output $taskSibling 2>&1
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $taskSibling)) { throw 'Sibling destination under protected root was accepted or created.' }
$taskMissingRoot = Join-Path $taskBase 'missing-root-output'
$taskRefusal = & $DotNet $taskCli phase3 inspect --chart $taskBms --output $taskMissingRoot 2>&1
if ($LASTEXITCODE -ne 1 -or ($taskRefusal -join ' ') -notmatch '--source-root is required' -or (Test-Path -LiteralPath $taskMissingRoot)) { throw 'Omitted source root was not refused.' }
foreach ($taskFile in $taskBefore) {
    $taskCurrent = Get-Item -LiteralPath $taskFile.Path
    if ($taskCurrent.Length -ne $taskFile.Size -or $taskCurrent.LastWriteTimeUtc.Ticks -ne $taskFile.Mtime -or $taskCurrent.Attributes -ne $taskFile.Attributes -or (Get-FileHash -LiteralPath $taskFile.Path -Algorithm SHA256).Hash -ne $taskFile.Hash) { throw "Synthetic chart modified: $($taskFile.Path)" }
}
if (@(Get-ChildItem -LiteralPath $taskSources -File).Count -ne 3) { throw 'Unexpected source-directory output.' }
Write-Output "Phase 3 synthetic smoke passed. Artifacts: $taskBase"
