using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BmsLibraryAudit.Core;

public sealed record ObservationPairLimits(int SourceCharts = 512, int SourceFolders = 128,
    int Diagnostics = 1024, int Charts = 512, int Entries = 8192, int Files = 4096, int Depth = 32,
    long SourceBytes = 64L * 1024 * 1024, long FileBytes = 256L * 1024 * 1024,
    long TotalBytes = 1024L * 1024 * 1024, long AttemptBytes = 2L * 1024 * 1024 * 1024,
    int Seconds = 300, int PayloadBytes = 16 * 1024 * 1024, long DatabaseBytes = 64L * 1024 * 1024,
    int ReportBytes = 32 * 1024 * 1024, long MemoryBytes = 256L * 1024 * 1024);
public sealed record ObservationPairCaptureOptions(string SourceDatabasePath, long SourceScanId,
    string RootA, string RootB, string DatabasePath, string OutputDirectory);
public sealed record ObservationPairResult(string Stage, string State, string? ManifestSha256, string UnitId,
    string GlobalCoverage, string PairState)
{
    public int ExitCode => State == "COMPLETE" ? 0 : State == "CANCELLED" ? 130 : State is "INCOMPLETE" or "STALE" ? 2 : 1;
}
internal sealed record PairSource(string Profile, string Contract, string BuildRevision,
    ObservationPairLimits Limits, CapturedPhaseOne Capture);
internal sealed record PairSelection(string SourceDigest, string UnitId, string PairId, string Policy,
    string RootA, string RootB, FolderCandidate Discovery);
internal sealed record PairRoot(int Id, string Key, string OriginalPath, NativePathFacts Facts);
internal sealed record PairEntry(long Id, int RootId, string RelativePath, string Key, PhaseTwoEntry Facts);
internal sealed record PairChart(long EntryId, string SourceSha256, bool Direct, PhaseTwoContentRead Read);
internal sealed record PairOutcome(int RootId, string State, long Entries, long RequiredFiles, long SuccessfulFiles);
internal sealed record PairPreflight(string SourceDigest, string UnitId, string State, string StartedUtc, string EndedUtc,
    PairRoot[] Roots, PairEntry[] Entries, PairChart[] Charts, int[] VerifiedRoots, PairOutcome[] Outcomes, Diagnostic[] Diagnostics);
internal sealed record PairRead(long EntryId, PhaseTwoContentRead Read);
internal sealed record PairPhysicalComparison(long[] Counts, bool[] Flags,
    PhaseTwoContentComparison.Difference[] Differences, PhaseTwoContentComparison.Group[] Groups,
    bool TreeSubsetAInB, bool TreeSubsetBInA, PhysicalConflict[] Conflicts,
    string[] CommonChartHashes, string[] ChartHashesOnlyA, string[] ChartHashesOnlyB,
    int ChartCopiesA, int ChartCopiesB, string[] StructuralStates);
internal sealed record PairContent(string PreflightDigest, string UnitId, string State, string StartedUtc, string EndedUtc,
    PairRead[] Reads, int[] VerifiedRoots, PairOutcome[] Outcomes, PairPhysicalComparison? Comparison, Diagnostic[] Diagnostics);
internal sealed class PairStopException(string state, string code, string path = "") : IOException(code)
{
    internal string State { get; } = state;
    internal string Code { get; } = code;
    internal string AffectedPath { get; } = path;
}
internal sealed class PairBudget(ObservationPairLimits limits, CancellationToken token)
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly long memory = GC.GetTotalMemory(false);
    private long bytes;
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        if (clock.Elapsed.TotalSeconds > limits.Seconds || GC.GetTotalMemory(false) - memory > limits.MemoryBytes)
            throw new PairStopException("INCOMPLETE", "WORK_BUDGET_EXCEEDED");
    }
    internal void AddBytes(long value)
    {
        bytes = checked(bytes + value); Check();
        if (bytes > limits.AttemptBytes) throw new PairStopException("INCOMPLETE", "ATTEMPT_BYTES_EXCEEDED");
    }
}
internal static class PairJson
{
    internal static readonly JsonSerializerOptions Options = new() {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 64 };
    internal static string Text<T>(T value) => JsonSerializer.Serialize(value, Options);
    internal static T Parse<T>(string text) => JsonSerializer.Deserialize<T>(text, Options)
        ?? throw new InvalidOperationException("Missing typed observation evidence.");
    internal static bool Equal<T>(T a, T b) => Text(a) == Text(b);
}
