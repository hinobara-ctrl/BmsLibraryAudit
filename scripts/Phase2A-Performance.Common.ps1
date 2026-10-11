# Shared ownership checks, atomic records and Windows process containment.
# PowerShell 7 is required; no external data, network or cleanup is supported.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Phase 2A benchmarks require PowerShell 7 or later; Windows PowerShell 5.1 is not supported.' }
if (-not $IsWindows) { throw 'Phase 2A benchmarks require Windows and PowerShell 7 or later.' }
$script:BenchmarkRepository = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$script:BenchmarkRoot = Join-Path $script:BenchmarkRepository '.artifacts/phase2a-performance'
$script:BenchmarkEncoding = [Text.UTF8Encoding]::new($false)
if (-not ('Phase2BenchmarkNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class Phase2BenchmarkNative {
    [StructLayout(LayoutKind.Sequential)] struct Info { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential)] struct Limits { public long UserTime, JobTime; public uint Flags; public UIntPtr Min, Max; public uint Active; public UIntPtr Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct Extended { public Limits Basic; public Io IO; public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info info);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int kind,out uint flags,uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder value,uint size,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObjectW(IntPtr security,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int kind,ref Extended limits,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll",SetLastError=true)] public static extern bool CloseHandle(IntPtr handle);
    static Exception Error() => new Win32Exception(Marshal.GetLastWin32Error());
    public static string[] DirectoryFacts(string path) {
        using var handle=CreateFileW(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
        if(handle.IsInvalid || !GetFileInformationByHandle(handle,out var i)) throw Error();
        if((i.Attributes & 0x400)!=0 || (i.Attributes & 0x10)==0) throw new IOException("Unsafe benchmark directory");
        if(!GetFileInformationByHandleEx(handle,23,out var flags,4)) throw Error();
        if((flags & 1)!=0) throw new IOException("Case-sensitive benchmark directory unsupported");
        var sb=new StringBuilder(32768); var n=GetFinalPathNameByHandleW(handle,sb,32768,1);
        if(n==0 || n>=32768) throw Error();
        return new[]{sb.ToString().TrimEnd('\\').ToUpperInvariant(), $"{i.Volume:x8}:{i.IndexHigh:x8}{i.IndexLow:x8}"};
    }
    public static IntPtr NewJob() {
        var job=CreateJobObjectW(IntPtr.Zero,null); if(job==IntPtr.Zero) throw Error();
        var limits=new Extended(); limits.Basic.Flags=0x2000; // KILL_ON_JOB_CLOSE
        if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<Extended>())) { CloseHandle(job); throw Error(); }
        return job;
    }
    public static void Assign(IntPtr job,IntPtr process) { if(!AssignProcessToJobObject(job,process)) throw Error(); }
}
'@
}
function Test-BenchmarkAncestors([string]$Path) {
    $taskPath = [IO.Path]::GetFullPath($Path)
    if ($taskPath.Substring(([IO.Path]::GetPathRoot($taskPath)).Length).Contains(':')) { throw 'Alternate streams are forbidden.' }
    while ($taskPath) {
        if ([IO.Directory]::Exists($taskPath) -or [IO.File]::Exists($taskPath)) {
            if (([IO.File]::GetAttributes($taskPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse ancestor: $taskPath" }
            if ([IO.Directory]::Exists($taskPath)) { $null = [Phase2BenchmarkNative]::DirectoryFacts($taskPath) }
        }
        # The existing user-selected workspace is the authorization anchor.
        # Its native final path and identity are checked, and every component
        # below it is inspected. No arbitrary artifact-root argument exists.
        # Ancestors above the workspace (e.g. a cloud Desktop) are not targets
        # of benchmark creation or enumeration. Production safety is unchanged.
        if ($taskPath -eq $script:BenchmarkRepository) { break }
        $taskPath = [IO.Path]::GetDirectoryName($taskPath)
    }
}
function Write-BenchmarkJson([string]$Path, $Value) {
    Test-BenchmarkAncestors (Split-Path -Parent $Path)
    $taskTemporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $taskBytes = $script:BenchmarkEncoding.GetBytes(($Value | ConvertTo-Json -Depth 20) + "`n")
    $taskStream = [IO.FileStream]::new($taskTemporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $taskStream.Write($taskBytes); $taskStream.Flush($true) } finally { $taskStream.Dispose() }
    [IO.File]::Move($taskTemporary, $Path, $true)
}
function Get-BenchmarkProfile([string]$Profile, [bool]$Nested) {
    $taskCount = switch ($Profile) { QUICK { 10 }; STANDARD { 100 }; EXTENDED { 500 }; default { throw 'Unknown profile.' } }
    $taskResources = if ($Profile -eq 'QUICK') { 10 } else { 20 }
    [ordered]@{ components=$taskCount; roots=2*$taskCount; chart_files=4*$taskCount; non_chart_files=2*$taskCount*$taskResources;
        resources_per_root=$taskResources; nested_roots=$Nested; max_files=25000; max_directories=10000; max_fixture_bytes=16777216 }
}
function Initialize-BenchmarkRoot {
    Test-BenchmarkAncestors $script:BenchmarkRoot
    $taskRepoFacts = [Phase2BenchmarkNative]::DirectoryFacts($script:BenchmarkRepository)
    $taskOwnerPath = Join-Path $script:BenchmarkRoot 'owner.json'
    if ([IO.Directory]::Exists($script:BenchmarkRoot)) {
        if (-not [IO.File]::Exists($taskOwnerPath)) { throw 'Existing performance directory has no benchmark ownership marker.' }
        Test-BenchmarkAncestors $taskOwnerPath
        $taskOwner = Get-Content -LiteralPath $taskOwnerPath -Raw | ConvertFrom-Json
        if ($taskOwner.kind -ne 'BmsLibraryAudit.Phase2A.PerformanceRoot.v1' -or $taskOwner.repository_identity -ne $taskRepoFacts[1]) { throw 'Foreign benchmark root.' }
    } else {
        $null = [IO.Directory]::CreateDirectory($script:BenchmarkRoot)
        Write-BenchmarkJson $taskOwnerPath ([ordered]@{kind='BmsLibraryAudit.Phase2A.PerformanceRoot.v1';repository_identity=$taskRepoFacts[1]})
    }
    $taskRootFacts = [Phase2BenchmarkNative]::DirectoryFacts($script:BenchmarkRoot)
    if (-not $taskRootFacts[0].StartsWith($taskRepoFacts[0] + '\', [StringComparison]::Ordinal)) { throw 'Benchmark root escapes the repository.' }
}
function Get-OwnedBenchmark([string]$RunDirectory) {
    Initialize-BenchmarkRoot
    $taskRun = [IO.Path]::GetFullPath($RunDirectory)
    if ([IO.Path]::GetDirectoryName($taskRun) -ne [IO.Path]::GetFullPath($script:BenchmarkRoot) -or
        [IO.Path]::GetFileName($taskRun) -notmatch '^run-[0-9a-f]{32}$') { throw 'Expected a direct, owned benchmark run directory.' }
    Test-BenchmarkAncestors $taskRun
    Test-BenchmarkAncestors (Join-Path $taskRun 'started.json')
    $taskStarted = Get-Content -LiteralPath (Join-Path $taskRun 'started.json') -Raw | ConvertFrom-Json
    $taskFacts = [Phase2BenchmarkNative]::DirectoryFacts($taskRun)
    if ($taskStarted.kind -ne 'BmsLibraryAudit.Phase2A.PerformanceRun.v1' -or $taskStarted.run_id -ne [IO.Path]::GetFileName($taskRun) -or
        $taskStarted.directory_identity -ne $taskFacts[1] -or $taskStarted.resolved_directory -ne $taskFacts[0]) { throw 'Run ownership/identity differs.' }
    return $taskStarted
}
function Get-BenchmarkProcess([int]$ProcessId, $StartUtc) {
    try {
        $taskProcess = Get-Process -Id $ProcessId -ErrorAction Stop
        # Recent PowerShell versions deserialize ISO JSON dates as DateTime.
        # Compare exact UTC ticks instead of culture-dependent string coercion.
        $taskExpectedStart = if ($StartUtc -is [DateTime]) { $StartUtc } else { [DateTime]::Parse($StartUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind) }
        if ($taskProcess.StartTime.ToUniversalTime().Ticks -eq $taskExpectedStart.ToUniversalTime().Ticks) { return $taskProcess }
    } catch { }
    return $null
}
