param([string]$DotNet = '')
. (Join-Path $PSScriptRoot 'Phase2A-Performance.Common.ps1')
$taskLauncher=Join-Path $PSScriptRoot 'Start-Phase2A-PerformanceTest.ps1'
$taskStatus=Join-Path $PSScriptRoot 'Get-Phase2A-PerformanceTest.ps1'
$taskChecks=[Collections.Generic.List[object]]::new()
foreach ($taskScript in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*Performance*.ps1') {
    $taskTokens=$null; $taskParseErrors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($taskScript.FullName,[ref]$taskTokens,[ref]$taskParseErrors)
    if ($taskParseErrors.Count) { throw "PowerShell parse errors: $taskParseErrors" }
}
# Compare non-whitespace substantive text to committed research, decoding only
# its accidental Markdown escapes/entities. This also preserves URLs and SHAs.
$taskOld=(& git -C $script:BenchmarkRepository show HEAD:docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md) -join "`n"
$taskExpected=[regex]::Replace($taskOld,'\\([#*\[\]_.\-])','$1').Replace('&#x20;',' ')
$taskNote=Get-Content -LiteralPath (Join-Path $script:BenchmarkRepository 'docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md') -Raw
if ([regex]::Replace($taskExpected,'\s','') -cne [regex]::Replace($taskNote,'\s','') -or
    $taskNote -match '\\[#*\[_\-]|&#x20;|\n\s*\n\s*\n') { throw 'Research text preservation/formatting check failed.' }
if (@([regex]::Matches($taskNote,'(?m)^```')).Count % 2) { throw 'Unbalanced Markdown fences.' }
$taskChecks.Add([ordered]@{check='Markdown substantive preservation and syntax';passed=$true})
foreach ($taskForeign in @($script:BenchmarkRepository, (Join-Path $script:BenchmarkRoot 'not-a-run'))) {
    $taskRejected=$false
    try { $null=Get-OwnedBenchmark $taskForeign } catch { $taskRejected=$true }
    if (-not $taskRejected) { throw 'Foreign artifact directory accepted.' }
}
$taskChecks.Add([ordered]@{check='Foreign artifact directory refusal';passed=$true})
$taskQuick=& $taskLauncher -Profile QUICK -Seed 1729 -TimeoutMinutes 5 -DotNet $DotNet -NestedRoots -Synchronous
if ($taskQuick.state -ne 'SUCCESS') { throw "QUICK failed: $($taskQuick.first_failure)" }
$taskChecks.Add([ordered]@{check='QUICK nested normal completion';passed=$true;run_directory=$taskQuick.run_directory;elapsed_seconds=$taskQuick.elapsed_seconds})
# This unrelated sentinel is owned by the validation script but is deliberately
# outside every benchmark job. Timeout/cancellation must not kill it.
$taskInfo=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
$taskInfo.UseShellExecute=$false; $taskInfo.CreateNoWindow=$true
foreach ($taskArgument in @('-NoProfile','-NonInteractive','-Command','[Threading.Thread]::Sleep(120000)')) { $taskInfo.ArgumentList.Add($taskArgument) }
$taskSentinel=[Diagnostics.Process]::Start($taskInfo)
try {
    foreach ($taskMode in @('NONE','FAIL','CRASH','TIMEOUT','CANCEL')) {
        $taskLaunchClock=[Diagnostics.Stopwatch]::StartNew()
        $taskSeconds=if ($taskMode -eq 'TIMEOUT') {10} else {30}
        $taskLaunch=& $taskLauncher -Profile QUICK -Seed 1729 -TimeoutSeconds $taskSeconds -DotNet $DotNet -TestMode $taskMode
        $taskLaunchSeconds=$taskLaunchClock.Elapsed.TotalSeconds
        if ($taskLaunchSeconds -gt 5) { throw 'Detached launcher did not return promptly.' }
        $taskOwner=Get-Content -LiteralPath (Join-Path $taskLaunch.RunDirectory 'launcher.json') -Raw | ConvertFrom-Json
        $taskSupervisor=Get-BenchmarkProcess $taskOwner.supervisor_pid $taskOwner.supervisor_start_utc
        # One finite wait verifies short injected cases; there is no agent loop
        # and no observation of STANDARD or EXTENDED here.
        if ($taskSupervisor -and -not $taskSupervisor.WaitForExit(30000)) { throw 'Injected harness case exceeded its bounded validation wait.' }
        $taskObserved=& $taskStatus -RunDirectory $taskLaunch.RunDirectory
        $taskExpectedState=switch ($taskMode) { NONE {'SUCCESS'}; TIMEOUT {'TIMEOUT'}; CANCEL {'CANCELLED'}; default {'FAILED'} }
        if ($taskObserved.state -ne $taskExpectedState) { throw "Expected $taskExpectedState, observed $($taskObserved.state)" }
        if ($taskSentinel.HasExited) { throw 'Unrelated sentinel was terminated.' }
        foreach ($taskName in @('started.json','stdout.log','stderr.log','result.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $taskLaunch.RunDirectory $taskName))) { throw "Missing $taskName" }
        }
        $taskChildFile=Join-Path $taskLaunch.RunDirectory 'injected-child.json'
        if ($taskMode -in @('TIMEOUT','CANCEL')) {
            if (-not (Test-Path -LiteralPath $taskChildFile)) { throw 'Owned descendant fixture was not exercised.' }
            $taskChild=Get-Content -LiteralPath $taskChildFile -Raw | ConvertFrom-Json
            if (Get-BenchmarkProcess $taskChild.process_id $taskChild.start_utc) { throw 'Owned descendant survived job termination.' }
        }
        if ($taskMode -eq 'CRASH') {
            if (Test-Path -LiteralPath (Join-Path $taskLaunch.RunDirectory 'worker-result.json')) { throw 'Crash fixture unexpectedly had a worker final result.' }
            $taskFinalPath=Join-Path $taskLaunch.RunDirectory 'result.json'; $taskRetained=Join-Path $taskLaunch.RunDirectory 'retained-result.json'
            [IO.File]::Move($taskFinalPath,$taskRetained)
            try { if ((& $taskStatus -RunDirectory $taskLaunch.RunDirectory).state -ne 'INTERRUPTED') { throw 'Missing final result was mistaken for completion.' } }
            finally { [IO.File]::Move($taskRetained,$taskFinalPath) }
        }
        $taskChecks.Add([ordered]@{check=$taskMode;passed=$true;run_directory=$taskLaunch.RunDirectory;launch_seconds=$taskLaunchSeconds;state=$taskObserved.state})
    }
    # Launch from a disposable pwsh that exits immediately. Its supervisor must
    # survive that shell, then reach TIMEOUT with no active Codex observation.
    $taskLaunchInfo=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $taskLaunchInfo.UseShellExecute=$false; $taskLaunchInfo.CreateNoWindow=$true; $taskLaunchInfo.RedirectStandardOutput=$true; $taskLaunchInfo.RedirectStandardError=$true
    foreach ($taskArgument in @('-NoProfile','-NonInteractive','-File',$taskLauncher,'-Profile','QUICK','-TestMode','TIMEOUT','-TimeoutSeconds','10','-DotNet',$DotNet)) { $taskLaunchInfo.ArgumentList.Add($taskArgument) }
    $taskShellChild=[Diagnostics.Process]::Start($taskLaunchInfo)
    # Read the first launcher line without waiting for pipe EOF: descendants
    # may inherit otherwise-unused pipe handles beyond the launcher's lifetime.
    $taskLaunchOutput=$taskShellChild.StandardOutput.ReadLineAsync(); $taskLaunchError=$taskShellChild.StandardError.ReadToEndAsync()
    if (-not $taskShellChild.WaitForExit(5000)) { $taskShellChild.Kill($true); throw 'Disposable launcher shell did not exit promptly.' }
    $taskShellText=$taskLaunchOutput.GetAwaiter().GetResult()
    if ($taskShellChild.ExitCode -ne 0) { throw "Detached lifecycle launcher failed: $($taskLaunchError.GetAwaiter().GetResult())" }
    $taskRunMatch=[regex]::Match($taskShellText,'(?m)^Run directory: (.+)\r?$')
    if (-not $taskRunMatch.Success) { throw 'Missing full run-directory output.' }
    $taskDetachedRun=$taskRunMatch.Groups[1].Value.Trim()
    $taskDetachedOwner=Get-Content -LiteralPath (Join-Path $taskDetachedRun 'launcher.json') -Raw | ConvertFrom-Json
    $taskDetachedSupervisor=Get-BenchmarkProcess $taskDetachedOwner.supervisor_pid $taskDetachedOwner.supervisor_start_utc
    if (-not $taskDetachedSupervisor) { throw 'Supervisor did not survive launcher exit.' }
    if (-not $taskDetachedSupervisor.WaitForExit(30000)) { throw 'Detached lifecycle fixture exceeded timeout.' }
    if ((& $taskStatus -RunDirectory $taskDetachedRun).state -ne 'TIMEOUT' -or $taskSentinel.HasExited) { throw 'Detached lifecycle finalization failed.' }
    $taskShellChild.Dispose()
    $taskChecks.Add([ordered]@{check='Supervisor survives launcher shell exit';passed=$true;run_directory=$taskDetachedRun})
} finally { if (-not $taskSentinel.HasExited) { $taskSentinel.Kill(); $taskSentinel.WaitForExit() }; $taskSentinel.Dispose() }
$taskCheckPath=Join-Path $taskQuick.run_directory 'harness-validation.json'
Write-BenchmarkJson $taskCheckPath ([ordered]@{state='SUCCESS';checks=$taskChecks;unrelated_process_preserved=$true;completed_utc=[DateTime]::UtcNow.ToString('O')})
[pscustomobject]@{State='SUCCESS';ValidationFile=$taskCheckPath;QuickRun=$taskQuick.run_directory;Checks=$taskChecks.Count}
