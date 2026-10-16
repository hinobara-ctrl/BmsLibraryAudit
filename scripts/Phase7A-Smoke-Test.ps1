param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 7A synthetic smoke requires PowerShell 7+.' }
if (-not $IsWindows) { throw 'Phase 7A synthetic smoke requires Windows.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskResults = Join-Path $taskRepository ('.artifacts\phase7a-smoke-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskResults | Out-Null
# The only entry point is a fixed test. No source/plan/destination path is accepted.
& $DotNet test (Join-Path $taskRepository 'tests\BmsLibraryAudit.Tests') -c Release --no-build `
    --filter 'FullyQualifiedName=BmsLibraryAudit.Tests.PhaseSevenTests.EndToEndSmoke' `
    --logger 'trx;LogFileName=phase7a-smoke.trx' --results-directory $taskResults 2>&1 |
    Tee-Object -FilePath (Join-Path $taskResults 'test.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Internal synthetic Phase 7A harness failed.' }
[xml]$taskTrx = Get-Content -LiteralPath (Join-Path $taskResults 'phase7a-smoke.trx') -Raw
$taskCases = @($taskTrx.GetElementsByTagName('UnitTestResult'))
if ($taskCases.Count -ne 1 -or $taskCases[0].outcome -ne 'Passed') { throw 'Expected exactly one successful fixed harness case.' }
$taskCaseOutput = $taskCases[0].GetElementsByTagName('StdOut')[0].InnerText
$taskMatch = [regex]::Match($taskCaseOutput, 'PHASE7A_SMOKE=(\{[^\r\n]+\})')
if (-not $taskMatch.Success) { throw 'Harness artifact record missing.' }
$taskRecord = $taskMatch.Groups[1].Value | ConvertFrom-Json
if ($taskRecord.State -ne 'STAGING_PHYSICALLY_VERIFIED' -or $taskRecord.Files -ne 10 -or $taskRecord.Directories -ne 3) { throw 'Unexpected independent fixture counts or state.' }
$taskFileList = @(Get-ChildItem -LiteralPath $taskRecord.Stage -Recurse -File)
if ($taskFileList.Count -ne 10) { throw 'Actual stage contains an incorrect number of files.' }
foreach ($taskExpected in $taskRecord.FilesPresent) {
    $taskPath = Join-Path $taskRecord.Stage $taskExpected.RelativePath
    if ((Get-FileHash -LiteralPath $taskPath).Hash.ToLowerInvariant() -ne $taskExpected.Sha256 -or (Get-Item -LiteralPath $taskPath).Length -ne $taskExpected.Size) { throw 'Independent PowerShell target verification failed.' }
}
if (($taskFileList | Measure-Object Length -Sum).Sum -ne $taskRecord.BytesCopied) { throw 'Copied-byte accounting differs from physical file lengths.' }
$taskRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskResults 'artifact-record.json') -Encoding utf8
Write-Output ('Constructed synthetic stage: ' + $taskRecord.Stage)
Write-Output ('Receipt outside stage: ' + $taskRecord.Receipt)
Write-Output ('Copied bytes: ' + $taskRecord.BytesCopied + '; elapsed staging milliseconds: ' + $taskRecord.ElapsedMilliseconds)
$taskRecord.FilesPresent | Select-Object RelativePath,Size | Format-Table -AutoSize | Out-Host
Write-Output ('Phase 7A synthetic staging smoke passed. Logs: ' + $taskResults)
