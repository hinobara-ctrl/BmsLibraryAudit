param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Subset-pair smoke requires PowerShell 7+.' }
if (-not $IsWindows) { throw 'Subset-pair acquisition requires Windows native checks.' }
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepository 'src\BmsLibraryAudit.Cli\bin\Release\net10.0\bms-audit.dll'
if (-not (Test-Path -LiteralPath $taskCli)) { throw 'Build Release first.' }
$taskBase = Join-Path $taskRepository ('.artifacts\observation-pair-smoke-' + [guid]::NewGuid().ToString('N'))
$taskRoot = Join-Path $taskBase 'synthetic-library'
$taskSource = Join-Path $taskBase 'audit.db'
$taskDb = Join-Path $taskBase 'pair.db'
$taskUtf8 = [Text.UTF8Encoding]::new($false)
# Independent native preservation oracle; this does not call application acquisition.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class SubsetSmokeNative {
 [StructLayout(LayoutKind.Sequential)] struct Info {
  public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh,
   WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
 }
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
 static extern SafeFileHandle CreateFileW(string p,uint a,uint s,IntPtr sec,uint d,uint f,IntPtr t);
 [DllImport("kernel32.dll", SetLastError=true)]
 static extern bool GetFileInformationByHandle(SafeFileHandle h,out Info i);
 public static string Facts(string p) {
  using(var h=CreateFileW(p,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)) {
   Info i; if(h.IsInvalid || !GetFileInformationByHandle(h,out i)) throw new InvalidOperationException("Native preservation oracle failed");
   return String.Format("{0:x8}:{1:x8}{2:x8}/{3}/{4}/{5}:{6}/{7}:{8}",i.Volume,i.IndexHigh,i.IndexLow,i.Links,i.Attributes,i.SizeHigh,i.SizeLow,i.WriteHigh,i.WriteLow);
  }
 }
}
'@
function Invoke-TaskCli([int]$Expected, [string[]]$Arguments) {
    $taskText = & $DotNet $taskCli @Arguments
    if ($LASTEXITCODE -ne $Expected) { throw "Unexpected CLI exit $LASTEXITCODE expected $Expected for $($Arguments[0..1] -join ' ')" }
    if ($Expected -eq 0) { return ($taskText -join "`n") | ConvertFrom-Json }
}
function Get-TaskSnapshot([string[]]$Paths) {
    @($Paths | Sort-Object -CaseSensitive | ForEach-Object {
        $taskItem = Get-Item -LiteralPath $_
        [pscustomobject]@{ Path=$_; Native=[SubsetSmokeNative]::Facts($_); Hash=$(if (-not $taskItem.PSIsContainer) { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } else { $null }) }
    }) | ConvertTo-Json -Depth 5 -Compress
}
foreach ($taskName in @('A','B')) {
    $taskFolder = Join-Path $taskRoot $taskName
    New-Item -ItemType Directory -Path $taskFolder | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskFolder 'one.bms'), "#TITLE synthetic-one`n#WAV01 kick.wav", $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskFolder 'two.bmson'), '{"version":"1.0.0","info":{"title":"synthetic-two"}}', $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskFolder 'kick.wav'), $(if ($taskName -eq 'A') { 'audio-A' } else { 'audio-B' }), $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskFolder 'shared.opaque'), 'same-known-payload', $taskUtf8)
}
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\three.bms'), '#TITLE exclusive-three', $taskUtf8)
[IO.File]::WriteAllText((Join-Path $taskRoot 'A\exclusive.zip'), 'opaque-A', $taskUtf8)
[IO.File]::WriteAllText((Join-Path $taskRoot 'B\exclusive.png'), 'opaque-B', $taskUtf8)
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'B\empty') | Out-Null
$taskPaths = @($taskRoot) + @((Get-ChildItem -LiteralPath $taskRoot -Recurse -Force).FullName)
$taskBefore = Get-TaskSnapshot $taskPaths
$taskScan = Invoke-TaskCli 0 @('scan','--db',$taskSource,'--root',$taskRoot,'--rehash-all')
$taskSourceBefore = Get-TaskSnapshot @($taskSource)
$taskA = Join-Path $taskRoot 'A'; $taskB = Join-Path $taskRoot 'B'
$taskCapture = Invoke-TaskCli 0 @('observation-pair','capture','--source-db',$taskSource,'--source-scan',"$($taskScan.ScanId)",'--root',$taskA,'--root',$taskB,'--db',$taskDb,'--output',(Join-Path $taskBase 'source'))
$taskPf = Invoke-TaskCli 0 @('observation-pair','preflight','--db',$taskDb,'--accept-source',$taskCapture.ManifestSha256,'--output',(Join-Path $taskBase 'preflight'))
$taskContent = Invoke-TaskCli 0 @('observation-pair','hash','--db',$taskDb,'--accept-manifest',$taskPf.ManifestSha256,'--output',(Join-Path $taskBase 'content'))
$taskEvidenceBefore = Get-TaskSnapshot @($taskDb)
$taskVerified = Invoke-TaskCli 0 @('observation-pair','verify','--db',$taskDb,'--stage','CONTENT','--accept-manifest',$taskContent.ManifestSha256)
$taskReport = Get-Content -LiteralPath (Join-Path $taskBase 'content\observation_pair.json') -Raw | ConvertFrom-Json -Depth 64
if ($taskReport.Profile -ne 'explicit-subset-pair-1' -or $taskReport.Selection.Discovery.Relationship -ne 'A_SUBSET_OF_B' -or $taskVerified.PairState -ne 'COMPLETE') { throw 'Selected pair/admission differs.' }
$taskComparison = $taskReport.Content.Comparison
if ($taskComparison.Flags[0] -or $taskComparison.TreeSubsetAInB -or $taskComparison.TreeSubsetBInA) { throw 'Chart subset was confused with physical inclusion.' }
if (@($taskComparison.CommonChartHashes).Count -ne 2 -or $taskComparison.ChartCopiesA -ne 2 -or $taskComparison.ChartCopiesB -ne 3) { throw 'Chart identities/copies differ.' }
if (@($taskComparison.Conflicts | Where-Object { $_.RelativePath -eq 'kick.wav' -and 'SAME_RELATIVE_PATH_DIFFERENT_BYTES' -in $_.Kinds }).Count -ne 1) { throw 'Known byte conflict lost.' }
foreach ($taskSide in @('A','B')) {
    $taskId = if ($taskSide -eq 'A') { 1 } else { 2 }
    $taskEntry = $taskReport.Preflight.Entries | Where-Object { $_.RootId -eq $taskId -and $_.RelativePath -eq 'kick.wav' }
    $taskRead = $taskReport.Content.Reads | Where-Object EntryId -eq $taskEntry.Id
    $taskExpected = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskUtf8.GetBytes('audio-' + $taskSide))).ToLowerInvariant()
    if ($taskRead.Read.Sha256 -ne $taskExpected -or $taskRead.Read.BytesRead -ne 7) { throw 'Independent payload/byte oracle differs.' }
}
if (@($taskReport.Preflight.Charts).Count -ne 5 -or @($taskReport.Content.Reads).Count -ne 11 -or @($taskReport.Preflight.Entries).Count -ne 14) { throw 'Complete namespace counts differ.' }
foreach ($taskFlag in @('DependencyClosureProven','PackageEquivalenceProven','MergeAuthority','RemovalAuthority')) { if ($taskReport.$taskFlag) { throw 'Operational authority granted.' } }
if ($taskReport.Semantics -ne 'SEMANTIC_EVIDENCE_NOT_EVALUATED' -or $taskReport.PackageBoundary -ne 'PACKAGE_BOUNDARY_UNKNOWN') { throw 'Semantic/package authority inferred.' }
$taskHeld = @()
try {
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskRoot -Recurse -File) { $taskHeld += [IO.FileStream]::new($taskFile.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None) }
    $null = Invoke-TaskCli 0 @('observation-pair','report','--db',$taskDb,'--stage','CONTENT','--accept-manifest',$taskContent.ManifestSha256,'--output',(Join-Path $taskBase 'repeat'))
    $null = Invoke-TaskCli 0 @('observation-pair','verify','--db',$taskDb,'--stage','CONTENT','--accept-manifest',$taskContent.ManifestSha256)
} finally { foreach ($taskHandle in $taskHeld) { $taskHandle.Dispose() } }
if ((Get-FileHash (Join-Path $taskBase 'content\observation_pair.json')).Hash -ne (Get-FileHash (Join-Path $taskBase 'repeat\observation_pair.json')).Hash) { throw 'Reexport bytes differ.' }
$null = Invoke-TaskCli 1 @('observation-pair','verify','--db',$taskDb,'--stage','CONTENT','--accept-manifest',('0' * 64))
$taskTampered = Join-Path $taskBase 'tampered.db'; Copy-Item -LiteralPath $taskDb -Destination $taskTampered
$taskRaw = [IO.File]::ReadAllBytes($taskTampered); $taskRaw[68] = 0; $taskRaw[69] = 0; $taskRaw[70] = 0; $taskRaw[71] = 0; [IO.File]::WriteAllBytes($taskTampered,$taskRaw)
$null = Invoke-TaskCli 1 @('observation-pair','verify','--db',$taskTampered,'--stage','CONTENT','--accept-manifest',$taskContent.ManifestSha256)
if ($taskBefore -cne (Get-TaskSnapshot $taskPaths) -or $taskSourceBefore -cne (Get-TaskSnapshot @($taskSource)) -or $taskEvidenceBefore -cne (Get-TaskSnapshot @($taskDb))) { throw 'Source/evidence preservation differs.' }
$taskAfterPaths = @($taskRoot) + @((Get-ChildItem -LiteralPath $taskRoot -Recurse -Force).FullName)
if (@(Compare-Object ($taskPaths | Sort-Object) ($taskAfterPaths | Sort-Object)).Count) { throw 'Source directory entries changed.' }
foreach ($taskEvidence in @($taskSource,$taskDb)) { foreach ($taskSuffix in @('-journal','-wal','-shm')) { if (Test-Path -LiteralPath ($taskEvidence + $taskSuffix)) { throw 'Persistent evidence sidecar created.' } } }
Write-Output "Explicit subset-pair synthetic smoke passed. Artifacts: $taskBase"
