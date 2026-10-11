param([Parameter(Mandatory)][string]$RunDirectory, [switch]$Cancel)
. (Join-Path $PSScriptRoot 'Phase2A-Performance.Common.ps1')
$taskStarted = Get-OwnedBenchmark $RunDirectory
$taskFinal = Join-Path $RunDirectory 'result.json'
if (Test-Path -LiteralPath $taskFinal) { Get-Content -LiteralPath $taskFinal -Raw | ConvertFrom-Json; return }
if ($Cancel) {
    $taskRequest = [IO.FileStream]::new((Join-Path $RunDirectory 'cancel.request'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $taskRequest.Dispose()
}
$taskOwnerFile = Join-Path $RunDirectory 'supervisor.json'
if (-not (Test-Path -LiteralPath $taskOwnerFile)) { $taskOwnerFile = Join-Path $RunDirectory 'launcher.json' }
$taskAlive = $null
if (Test-Path -LiteralPath $taskOwnerFile) {
    $taskOwner = Get-Content -LiteralPath $taskOwnerFile -Raw | ConvertFrom-Json
    $taskAlive = Get-BenchmarkProcess $taskOwner.supervisor_pid $taskOwner.supervisor_start_utc
}
[pscustomobject]@{run_id=$taskStarted.run_id;state=$(if ($taskAlive) { 'RUNNING' } else { 'INTERRUPTED' });
    accepted_success=$false;cancel_requested=$Cancel.IsPresent;run_directory=$RunDirectory;
    notice='No final result exists. INTERRUPTED is not success; retain logs for inspection.'}
