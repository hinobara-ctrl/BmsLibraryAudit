param(
    [ValidateSet('QUICK','STANDARD','EXTENDED')][string]$Profile = 'QUICK',
    [int]$Seed = 1729,
    [ValidateRange(1,120)][int]$TimeoutMinutes = 60,
    [ValidateRange(1,7200)][int]$TimeoutSeconds = 0,
    [string]$DotNet = '',
    [switch]$NestedRoots,
    [switch]$Synchronous,
    [ValidateSet('NONE','FAIL','TIMEOUT','CRASH','CANCEL')][string]$TestMode = 'NONE'
)
. (Join-Path $PSScriptRoot 'Phase2A-Performance.Common.ps1')
if ($TestMode -ne 'NONE' -and $Profile -ne 'QUICK') { throw 'Failure injection is QUICK-only.' }
if ($Synchronous -and $Profile -ne 'QUICK') { throw 'Long profiles must run detached.' }
Initialize-BenchmarkRoot
if (-not $DotNet) { $DotNet = Join-Path $script:BenchmarkRepository '.tools/dotnet/dotnet.exe' }
$DotNet = (Get-Command -Name $DotNet -CommandType Application -ErrorAction Stop).Source
$taskCli = Join-Path $script:BenchmarkRepository 'src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll'
if (-not [IO.File]::Exists($taskCli)) { throw 'Build Release before benchmarking.' }
Test-BenchmarkAncestors (Split-Path -Parent $taskCli)
Test-BenchmarkAncestors (Split-Path -Parent $DotNet)
$taskRunId = 'run-' + [Guid]::NewGuid().ToString('N')
$taskRun = Join-Path $script:BenchmarkRoot $taskRunId
if (Test-Path -LiteralPath $taskRun) { throw 'Run directory collision.' }
$null = [IO.Directory]::CreateDirectory($taskRun)
$taskFacts = [Phase2BenchmarkNative]::DirectoryFacts($taskRun)
$taskSeconds = if ($TimeoutSeconds) { $TimeoutSeconds } else { $TimeoutMinutes * 60 }
$taskGitHead = & git -C $script:BenchmarkRepository rev-parse HEAD 2>$null
$taskGitStatus = @(& git -C $script:BenchmarkRepository status --porcelain --untracked-files=normal 2>$null)
$taskInput = [ordered]@{kind='BmsLibraryAudit.Phase2A.PerformanceRun.v1';run_id=$taskRunId;profile=$Profile;seed=$Seed;
    requested_utc=[DateTime]::UtcNow.ToString('O');timeout_seconds=$taskSeconds;parameters=(Get-BenchmarkProfile $Profile $NestedRoots.IsPresent);
    directory_identity=$taskFacts[1];resolved_directory=$taskFacts[0];dotnet=$DotNet;cli=$taskCli;
    executable_sha256=(Get-FileHash -LiteralPath $taskCli -Algorithm SHA256).Hash.ToLowerInvariant();
    core_sha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $taskCli) 'BmsLibraryAudit.Core.dll') -Algorithm SHA256).Hash.ToLowerInvariant();
    core_informational_version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path (Split-Path -Parent $taskCli) 'BmsLibraryAudit.Core.dll')).ProductVersion;
    powershell_version=$PSVersionTable.PSVersion.ToString();dotnet_host_version=[Diagnostics.FileVersionInfo]::GetVersionInfo($DotNet).ProductVersion;
    launch_repository_head=($taskGitHead -join '');launch_repository_dirty=($taskGitStatus.Count -gt 0);test_mode=$TestMode}
Write-BenchmarkJson (Join-Path $taskRun 'started.json') $taskInput
$taskSupervisor = Join-Path $PSScriptRoot 'Phase2A-Performance-Test.ps1'
if ($Synchronous) {
    & $taskSupervisor -RunDirectory $taskRun -Mode Supervisor
} else {
    $taskShell = (Get-Process -Id $PID).Path
    # Explicit paths, no shell command strings or background jobs. A separate
    # pwsh process owns its Job Object and survives the launching shell's exit.
    $taskArguments = @('-NoLogo','-NoProfile','-NonInteractive','-File', ('"' + $taskSupervisor + '"'), '-Mode','Supervisor','-RunDirectory',('"' + $taskRun + '"'))
    # The top-level supervisor gets an independent hidden Windows console,
    # avoiding inherited launcher pipe lifetimes. It owns worker stdout/stderr
    # capture itself, and persists its own errors in the atomic final result.
    $taskProcess = Start-Process -FilePath $taskShell -ArgumentList $taskArguments -WindowStyle Hidden -PassThru -WorkingDirectory $script:BenchmarkRepository
    Write-BenchmarkJson (Join-Path $taskRun 'launcher.json') ([ordered]@{supervisor_pid=$taskProcess.Id;supervisor_start_utc=$taskProcess.StartTime.ToUniversalTime().ToString('O');launched_utc=[DateTime]::UtcNow.ToString('O')})
    Write-Host "Run directory: $taskRun"
    Write-Host "Supervisor PID: $($taskProcess.Id); timeout: $taskSeconds seconds"
    [pscustomobject]@{RunDirectory=$taskRun;SupervisorPid=$taskProcess.Id;State='PENDING';TimeoutSeconds=$taskSeconds}
}
