param([Parameter(Mandatory)][string]$RunDirectory, [ValidateSet('Supervisor','Worker')][string]$Mode = 'Supervisor')
. (Join-Path $PSScriptRoot 'Phase2A-Performance.Common.ps1')
$taskStarted = Get-OwnedBenchmark $RunDirectory
$taskStart = [DateTime]::UtcNow
$taskClock = [Diagnostics.Stopwatch]::StartNew()
if ($Mode -eq 'Supervisor') {
    # A run has exactly one supervisor; retry via a fresh launcher/run directory.
    $taskClaim=[IO.FileStream]::new((Join-Path $RunDirectory 'supervisor.claim'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $taskClaim.Dispose()
    $taskJob = [IntPtr]::Zero; $taskWorker = $null; $taskState = 'FAILED'; $taskFailure = $null; $taskExit = $null
    try {
        Write-BenchmarkJson (Join-Path $RunDirectory 'supervisor.json') ([ordered]@{supervisor_pid=$PID;supervisor_start_utc=(Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('O');started_utc=$taskStart.ToString('O')})
        $taskJob = [Phase2BenchmarkNative]::NewJob()
        $taskShell = (Get-Process -Id $PID).Path
        $taskArguments = @('-NoLogo','-NoProfile','-NonInteractive','-File',('"' + $PSCommandPath + '"'),'-Mode','Worker','-RunDirectory',('"' + $RunDirectory + '"'))
        $taskWorker = Start-Process -FilePath $taskShell -ArgumentList $taskArguments -WindowStyle Hidden -PassThru -WorkingDirectory $script:BenchmarkRepository `
            -RedirectStandardOutput (Join-Path $RunDirectory 'stdout.log') -RedirectStandardError (Join-Path $RunDirectory 'stderr.log')
        # Worker cannot launch native descendants or generate data until assigned.
        [Phase2BenchmarkNative]::Assign($taskJob, $taskWorker.Handle)
        Write-BenchmarkJson (Join-Path $RunDirectory 'worker.json') ([ordered]@{worker_pid=$taskWorker.Id;worker_start_utc=$taskWorker.StartTime.ToUniversalTime().ToString('O')})
        [IO.File]::WriteAllText((Join-Path $RunDirectory 'contained.signal'), 'owned job assigned', $script:BenchmarkEncoding)
        while (-not $taskWorker.WaitForExit(200)) {
            if (Test-Path -LiteralPath (Join-Path $RunDirectory 'cancel.request')) { $taskState='CANCELLED'; $taskFailure='Explicit cancellation request'; break }
            if ($taskClock.Elapsed.TotalSeconds -ge $taskStarted.timeout_seconds) { $taskState='TIMEOUT'; $taskFailure='Finite supervisor timeout reached'; break }
        }
        if ($taskWorker.HasExited) {
            $taskExit = $taskWorker.ExitCode
            $taskResultPath = Join-Path $RunDirectory 'worker-result.json'
            if (Test-Path -LiteralPath $taskResultPath) {
                $taskResult = Get-Content -LiteralPath $taskResultPath -Raw | ConvertFrom-Json
                if ($taskExit -eq 0 -and $taskResult.state -eq 'SUCCESS' -and $taskResult.validated -eq $true -and $taskResult.run_id -eq $taskStarted.run_id) { $taskState='SUCCESS' }
                else { $taskFailure=$taskResult.first_failure }
            } else { $taskFailure="Worker exited without validated final evidence (exit $taskExit)" }
        }
    } catch { $taskFailure=$_.Exception.Message }
    finally {
        # Closing this private Job Object kills only its worker and descendants,
        # including a native CLI still alive after worker crash/timeout.
        if ($taskJob -ne [IntPtr]::Zero) { $null = [Phase2BenchmarkNative]::CloseHandle($taskJob) }
        if ($taskWorker -and -not $taskWorker.HasExited) {
            # Assignment failure is before the worker gate opens. This process
            # object is the just-created child, never a user-provided PID.
            $taskWorker.Kill($true); $null=$taskWorker.WaitForExit(5000)
        }
    }
    $taskMetrics = $null
    if (Test-Path -LiteralPath (Join-Path $RunDirectory 'metrics.json')) { $taskMetrics=Get-Content -LiteralPath (Join-Path $RunDirectory 'metrics.json') -Raw | ConvertFrom-Json }
    $taskFinal = [ordered]@{run_id=$taskStarted.run_id;profile=$taskStarted.profile;seed=$taskStarted.seed;state=$taskState;
        started_utc=$taskStart.ToString('O');finished_utc=[DateTime]::UtcNow.ToString('O');elapsed_seconds=$taskClock.Elapsed.TotalSeconds;
        timeout_seconds=$taskStarted.timeout_seconds;worker_exit_code=$taskExit;first_failure=$taskFailure;metrics=$taskMetrics;
        executable_identity=$taskStarted.executable_sha256;run_directory=$RunDirectory}
    Write-BenchmarkJson (Join-Path $RunDirectory 'result.json') $taskFinal
    [pscustomobject]$taskFinal
    return
}

# Worker. Only this process generates the synthetic fixture and invokes the CLI.
$taskMetrics = [ordered]@{run_id=$taskStarted.run_id;profile=$taskStarted.profile;seed=$taskStarted.seed;phases=[ordered]@{};
    generated_files=0;generated_directories=0;generated_chart_files=0;generated_non_chart_files=0;fixture_bytes=0;
    selected_components=$null;admitted_components=$null;rejected_components=$null;observation_roots=$null;
    estimated_phase2b_read_bytes=$null;phase1_db_bytes=$null;phase2_db_bytes=$null;preflight_manifest_sha256=$null;
    runtime_build_sha=$null;worker_peak_working_set_bytes=0;phase_exit_codes=[ordered]@{};first_failure=$null;diagnostic_summary=$null}
$taskValidated = $false; $taskFailure = $null
function Save-Metrics {
    $taskMetrics.worker_peak_working_set_bytes = (Get-Process -Id $PID).PeakWorkingSet64
    Write-BenchmarkJson (Join-Path $RunDirectory 'metrics.json') $taskMetrics
}
function Invoke-MeasuredCli([string]$Name, [string[]]$Arguments) {
    Write-Output ("Starting {0} at {1}" -f $Name, [DateTime]::UtcNow.ToString('O'))
    $taskPhaseClock = [Diagnostics.Stopwatch]::StartNew()
    $taskInfo = [Diagnostics.ProcessStartInfo]::new($taskStarted.dotnet)
    $taskInfo.UseShellExecute=$false; $taskInfo.CreateNoWindow=$true; $taskInfo.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $taskInfo.RedirectStandardOutput=$true; $taskInfo.RedirectStandardError=$true; $taskInfo.WorkingDirectory=$script:BenchmarkRepository
    $taskInfo.ArgumentList.Add($taskStarted.cli); foreach ($taskArgument in $Arguments) { $taskInfo.ArgumentList.Add($taskArgument) }
    $taskInfo.Environment['DOTNET_CLI_HOME']=Join-Path $script:BenchmarkRepository '.tools/cli-home'
    $taskInfo.Environment['NUGET_PACKAGES']=Join-Path $script:BenchmarkRepository '.tools/nuget'
    $taskProcess = [Diagnostics.Process]::Start($taskInfo)
    $taskOut = [IO.FileStream]::new((Join-Path $RunDirectory "$Name.stdout.log"),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $taskErr = [IO.FileStream]::new((Join-Path $RunDirectory "$Name.stderr.log"),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $taskOutCopy=$taskProcess.StandardOutput.BaseStream.CopyToAsync($taskOut); $taskErrCopy=$taskProcess.StandardError.BaseStream.CopyToAsync($taskErr)
    $taskPeak = 0L; $taskCpu = $null
    try {
        while (-not $taskProcess.WaitForExit(100)) { $taskProcess.Refresh(); $taskPeak=[Math]::Max($taskPeak,$taskProcess.PeakWorkingSet64) }
        $null=$taskOutCopy.GetAwaiter().GetResult(); $null=$taskErrCopy.GetAwaiter().GetResult()
        $taskCpu=$taskProcess.TotalProcessorTime.TotalSeconds
        $taskCode=$taskProcess.ExitCode
    } finally { $taskOut.Dispose(); $taskErr.Dispose(); $taskProcess.Dispose() }
    $taskMetrics.phases[$Name]=[ordered]@{elapsed_seconds=$taskPhaseClock.Elapsed.TotalSeconds;sampled_peak_working_set_bytes=$taskPeak;cpu_seconds=$taskCpu}
    $taskMetrics.phase_exit_codes[$Name]=$taskCode; Save-Metrics
    Write-Output ("Finished {0}: exit {1}, {2:N3}s" -f $Name,$taskCode,$taskPhaseClock.Elapsed.TotalSeconds)
    if ($taskCode -ne 0) { throw "$Name failed (exit $taskCode): $(Get-Content -LiteralPath (Join-Path $RunDirectory "$Name.stderr.log") -Raw)" }
}
try {
    $taskGateClock=[Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath (Join-Path $RunDirectory 'contained.signal'))) {
        if ($taskGateClock.Elapsed.TotalSeconds -gt 15) { throw 'No process containment gate; worker refused execution.' }
        [Threading.Thread]::Sleep(50)
    }
    Save-Metrics
    if ($taskStarted.test_mode -eq 'FAIL') { throw 'Injected worker failure' }
    if ($taskStarted.test_mode -eq 'CRASH') { [Environment]::Exit(23) }
    if ($taskStarted.test_mode -in @('TIMEOUT','CANCEL')) {
        $taskSleepInfo=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
        $taskSleepInfo.UseShellExecute=$false; $taskSleepInfo.CreateNoWindow=$true
        foreach ($taskArgument in @('-NoProfile','-NonInteractive','-Command','[Threading.Thread]::Sleep(120000)')) { $taskSleepInfo.ArgumentList.Add($taskArgument) }
        $taskSleepChild=[Diagnostics.Process]::Start($taskSleepInfo)
        Write-BenchmarkJson (Join-Path $RunDirectory 'injected-child.json') ([ordered]@{process_id=$taskSleepChild.Id;start_utc=$taskSleepChild.StartTime.ToUniversalTime().ToString('O')})
        if ($taskStarted.test_mode -eq 'CANCEL') { [IO.File]::WriteAllText((Join-Path $RunDirectory 'cancel.request'), 'injected cancellation', $script:BenchmarkEncoding) }
        [Threading.Thread]::Sleep(120000)
    }
    if ((Get-FileHash -LiteralPath $taskStarted.cli).Hash.ToLowerInvariant() -ne $taskStarted.executable_sha256 -or
        (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $taskStarted.cli) 'BmsLibraryAudit.Core.dll')).Hash.ToLowerInvariant() -ne $taskStarted.core_sha256) { throw 'Executable changed after launch.' }
    $taskFixtureClock=[Diagnostics.Stopwatch]::StartNew()
    $taskSongs=Join-Path $RunDirectory 'fixture/songs'
    $null=[IO.Directory]::CreateDirectory($taskSongs)
    $taskRandom=[Random]::new($taskStarted.seed)
    $taskFacts=[Collections.Generic.List[object]]::new()
    $taskTime=[DateTime]::new(2026,1,1,0,0,0,[DateTimeKind]::Utc)
    $taskSpec=$taskStarted.parameters
    if ($taskSpec.chart_files + $taskSpec.non_chart_files -gt $taskSpec.max_files -or
        14*$taskSpec.components+1 -gt $taskSpec.max_directories -or
        128*($taskSpec.chart_files+$taskSpec.non_chart_files) -gt $taskSpec.max_fixture_bytes) { throw 'Profile exceeds generation upper bounds.' }
    function New-Placeholder([string]$Path,[string]$Text,[bool]$Chart) {
        if ($taskFacts.Count -ge $taskSpec.max_files) { throw 'Fixture file upper bound reached.' }
        $taskBytes=$script:BenchmarkEncoding.GetBytes($Text)
        if ($taskMetrics.fixture_bytes + $taskBytes.Length -gt $taskSpec.max_fixture_bytes) { throw 'Fixture byte upper bound reached.' }
        Test-BenchmarkAncestors $Path
        $null=[IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
        $taskFile=[IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $taskFile.Write($taskBytes) } finally { $taskFile.Dispose() }
        [IO.File]::SetLastWriteTimeUtc($Path,$taskTime)
        $taskHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskBytes)).ToLowerInvariant()
        $taskFacts.Add([pscustomobject]@{path=$Path;sha256=$taskHash;mtime=$taskTime.Ticks})
        $taskMetrics.fixture_bytes += $taskBytes.Length
        if ($Chart) { $taskMetrics.generated_chart_files++ } else { $taskMetrics.generated_non_chart_files++ }
    }
    for ($taskComponent=0; $taskComponent -lt $taskSpec.components; $taskComponent++) {
        $taskContainer=Join-Path $taskSongs ('component-{0:D4}' -f $taskComponent)
        $taskA=Join-Path $taskContainer 'A'; $taskB=if ($taskSpec.nested_roots) { Join-Path $taskA 'B' } else { Join-Path $taskContainer 'B' }
        $taskNonce=$taskRandom.Next()
        foreach ($taskView in @($taskA,$taskB)) {
            $null=[IO.Directory]::CreateDirectory((Join-Path $taskView 'empty'))
            New-Placeholder (Join-Path $taskView 'direct.BMS') "synthetic-component=$taskComponent;mode=bms;seed=$taskNonce" $true
            New-Placeholder (Join-Path $taskView 'direct.pms') "synthetic-component=$taskComponent;mode=pms;seed=$taskNonce" $true
            for ($taskResource=0; $taskResource -lt $taskSpec.resources_per_root; $taskResource++) {
                $taskExtension=switch ($taskResource % 5) { 0 { '' }; 1 { '.wav' }; 2 { '.png' }; 3 { '.zip' }; 4 { '.unknown' } }
                $taskRelative=('assets/level-{0}/resource-{1:D4}{2}' -f ($taskResource%3),$taskResource,$taskExtension)
                New-Placeholder (Join-Path $taskView $taskRelative) "placeholder:$taskComponent/$taskResource/$($taskRandom.Next())" $false
            }
        }
    }
    $taskDirectories=@([IO.Directory]::EnumerateDirectories($taskSongs,'*',[IO.SearchOption]::AllDirectories))
    if ($taskDirectories.Count+1 -gt $taskSpec.max_directories) { throw 'Fixture directory upper bound reached.' }
    foreach ($taskDirectory in $taskDirectories) { [IO.Directory]::SetLastWriteTimeUtc($taskDirectory,$taskTime) }
    [IO.Directory]::SetLastWriteTimeUtc($taskSongs,$taskTime)
    $taskMetrics.generated_files=$taskFacts.Count; $taskMetrics.generated_directories=$taskDirectories.Count+1
    $taskMetrics.phases.fixture_generation=[ordered]@{elapsed_seconds=$taskFixtureClock.Elapsed.TotalSeconds}
    $taskRelativeFacts=@($taskFacts | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($taskSongs,$_.path).Replace('\','/');sha256=$_.sha256;mtime_ticks=$_.mtime} })
    Write-BenchmarkJson (Join-Path $RunDirectory 'fixture-manifest.json') $taskRelativeFacts
    Save-Metrics
    $taskSource=Join-Path $RunDirectory 'fixture/audit.db'; $taskDb=Join-Path $RunDirectory 'fixture/phase2.db'
    Invoke-MeasuredCli 'phase1_scan' @('scan','--db',$taskSource,'--root',$taskSongs)
    $taskScan=Get-Content -LiteralPath (Join-Path $RunDirectory 'phase1_scan.stdout.log') -Raw | ConvertFrom-Json
    $taskSourceHash=(Get-FileHash -LiteralPath $taskSource).Hash; $taskSourceMtime=[IO.File]::GetLastWriteTimeUtc($taskSource)
    Invoke-MeasuredCli 'phase2a_preflight_and_initial_export' @('phase2','preflight','--source-db',$taskSource,'--source-scan',([string]$taskScan.ScanId),'--db',$taskDb,'--output',(Join-Path $RunDirectory 'preflight'))
    $taskPreflight=Get-Content -LiteralPath (Join-Path $RunDirectory 'phase2a_preflight_and_initial_export.stdout.log') -Raw | ConvertFrom-Json
    Invoke-MeasuredCli 'persisted_report' @('phase2','report','--db',$taskDb,'--session',([string]$taskPreflight.SessionId),'--output',(Join-Path $RunDirectory 'repeat'))
    $taskSummary=Get-Content -LiteralPath (Join-Path $RunDirectory 'preflight/session_summary.json') -Raw | ConvertFrom-Json
    $taskMetrics.selected_components=$taskSummary.selected_component_count; $taskMetrics.admitted_components=$taskSummary.admitted_component_count
    $taskMetrics.rejected_components=$taskSummary.rejected_component_count; $taskMetrics.observation_roots=$taskSummary.observation_root_count
    $taskMetrics.estimated_phase2b_read_bytes=$taskSummary.estimated_phase2b_read_bytes; $taskMetrics.preflight_manifest_sha256=$taskSummary.preflight_manifest_sha256
    $taskMetrics.runtime_build_sha=$taskSummary.runtime_build_sha; $taskMetrics.phase1_db_bytes=(Get-Item -LiteralPath $taskSource).Length
    $taskMetrics.phase2_db_bytes=(Get-Item -LiteralPath $taskDb).Length
    $taskMetrics.diagnostic_summary=[ordered]@{errors=$taskSummary.error_count;warnings=$taskSummary.warning_count;distribution=$taskSummary.diagnostic_counts}
    $taskValidationClock=[Diagnostics.Stopwatch]::StartNew()
    $taskMetrics['union_directory_count']=$taskSummary.union_directory_count
    $taskMetrics['shared_path_count']=$taskSummary.shared_path_count
    $taskMetrics['nested_root_pairs']=$taskSummary.nested_root_pairs
    $taskMembershipRows=-1L
    foreach ($taskLine in [IO.File]::ReadLines((Join-Path $RunDirectory 'preflight/filesystem_inventory.csv'))) { $taskMembershipRows++ }
    $taskMetrics['observation_membership_rows']=$taskMembershipRows
    if ($taskPreflight.State -ne 'COMPLETED' -or $taskSummary.selected_component_count -ne $taskSpec.components -or
        $taskSummary.admitted_component_count -ne $taskSpec.components -or $taskSummary.rejected_component_count -ne 0 -or
        $taskSummary.observation_root_count -ne $taskSpec.roots -or $taskSummary.union_regular_file_count -ne $taskFacts.Count -or
        $taskSummary.union_directory_count -ne $taskMetrics.generated_directories-$taskSpec.components-1 -or
        $taskSummary.estimated_phase2b_read_bytes -ne $taskMetrics.fixture_bytes -or $taskSummary.non_chart_payload_reads -ne 0 -or
        $taskSummary.phase2b_executed -ne $false -or $taskSummary.preflight_manifest_sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Unexpected synthetic preflight evidence.' }
    $taskReportHashes=[ordered]@{}
    foreach ($taskName in @('session_summary.json','observation_roots.csv','filesystem_inventory.csv','diagnostics.csv')) {
        $taskHash=(Get-FileHash -LiteralPath (Join-Path $RunDirectory "preflight/$taskName")).Hash
        if ($taskHash -ne (Get-FileHash -LiteralPath (Join-Path $RunDirectory "repeat/$taskName")).Hash) { throw 'Repeated reports differ.' }
        $taskReportHashes[$taskName]=$taskHash.ToLowerInvariant()
    }
    if ($taskSourceHash -ne (Get-FileHash -LiteralPath $taskSource).Hash -or $taskSourceMtime -ne [IO.File]::GetLastWriteTimeUtc($taskSource)) { throw 'Synthetic source database changed.' }
    foreach ($taskFact in $taskFacts) {
        if ((Get-FileHash -LiteralPath $taskFact.path).Hash.ToLowerInvariant() -ne $taskFact.sha256 -or [IO.File]::GetLastWriteTimeUtc($taskFact.path).Ticks -ne $taskFact.mtime) { throw 'Synthetic source file changed.' }
    }
    $taskMetrics.phases.validation=[ordered]@{elapsed_seconds=$taskValidationClock.Elapsed.TotalSeconds}
    $taskMetrics['report_sha256']=$taskReportHashes
    $taskMetrics['fixture_manifest_sha256']=(Get-FileHash -LiteralPath (Join-Path $RunDirectory 'fixture-manifest.json')).Hash.ToLowerInvariant()
    $taskValidated=$true
} catch { $taskFailure=$_.Exception.Message; $taskMetrics.first_failure=$taskFailure; Write-Output "FAILED: $taskFailure" }
Save-Metrics
Write-BenchmarkJson (Join-Path $RunDirectory 'worker-result.json') ([ordered]@{run_id=$taskStarted.run_id;state=$(if ($taskValidated) {'SUCCESS'} else {'FAILED'});
    validated=$taskValidated;started_utc=$taskStart.ToString('O');finished_utc=[DateTime]::UtcNow.ToString('O');first_failure=$taskFailure})
if (-not $taskValidated) { exit 1 }
