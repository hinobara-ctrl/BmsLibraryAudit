using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BmsLibraryAudit.Core;

public sealed record PhaseSixOptions(string DatabasePath, long ContentSessionId, string AcceptedContentManifestSha256,
    string ComponentKey, IReadOnlyList<string> RootKeys);
public sealed record PhaseSixLimits(int MaximumEntries = 20000, int MaximumVirtualEntries = 20000,
    int MaximumConflicts = 20000, int MaximumReportBytes = 16 * 1024 * 1024,
    long MaximumMemoryGrowth = 128L * 1024 * 1024, int MaximumSeconds = 60);
public sealed record PhaseSixSource(long RootId, long EntryId);
public sealed record PhaseSixRoot(long Id, string Key, string OriginalPath, PhaseFiveEntry[] Entries);
public sealed record PhaseSixVirtualEntry(string RelativePath, string Kind, string? Sha256, PhaseSixSource[] Sources);
public sealed record PhaseSixConflict(string RelativePath, string[] Types, PhaseSixSource[] Sources);
public sealed record PhaseSixConservation(PhaseSixSource Source, string Disposition, string RelativePath, string Reason);
public sealed record PhaseSixChartObligation(PhaseSixSource Appearance, string ChartSha256, string OriginalParent,
    string State = "UNKNOWN_ALL_POTENTIAL_DEPENDENCIES_RETAINED");
public sealed record PhaseSixStates(string PhysicalUnionState, string ConservationState = "COMPLETE_ACCOUNTING",
    string SemanticEvidenceState = "SEMANTIC_EVIDENCE_UNKNOWN", string PackageBoundaryState = "PACKAGE_BOUNDARY_UNKNOWN",
    string RelocationState = "RELOCATION_NOT_PROVEN", string ConstructionState = "NOT_CONSTRUCTED",
    string VerificationState = "VIRTUAL_PLAN_CHECKED", string PublicationState = "NOT_PUBLISHED", string RetirementState = "NOT_RETIRED",
    bool DependencyClosureProven = false, bool MergeAuthority = false, bool RemovalAuthority = false);
public sealed record PhaseSixCounts(int SourceAppearances, int ChartAppearances, int VirtualEntries,
    int SharedVirtualEntries, int ExclusiveVirtualEntries, int BlockedAppearances, int ConflictAnnotations);
public sealed record PhaseSixPlan(string PlanVersion, PhaseFiveProvenance Provenance, PhaseSixRoot[] Roots,
    PhaseSixVirtualEntry[] VirtualEntries, PhaseSixConflict[] Conflicts, PhaseSixConservation[] Conservation, PhaseSixChartObligation[] ChartObligations,
    PhaseSixStates States, PhaseSixCounts Counts, PhaseSixLimits Limits, string[] Limitations);
public sealed record PhaseSixEnvelope(PhaseSixPlan Plan, string PlanSha256)
{
    [JsonIgnore] public int ExitCode => Plan.States.PhysicalUnionState == "UNION_PHYSICALLY_COMPATIBLE" ? 0 : 2;
}
internal static class PhaseSixFormat
{
    internal const string Version = "phase6a-virtual-union-1";
    internal const string Domain = "BmsLibraryAudit.Phase6A.VirtualUnionPlan.v1";
    internal static readonly JsonSerializerOptions Canonical = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };
    internal static readonly string[] Limitations = [
        "Historical virtual observation union only; not current source-byte freshness or a simultaneous snapshot.",
        "Every chart appearance and all inventory are retained; dependencies remain UNKNOWN. Phase 4 inputs are not accepted in v1.",
        "Package boundary and relocation are unproved; no construction, destination verification, publication, retirement or operational authority.",
        "Digests establish accepted consistency, not external authenticity. No removal list or promised recoverable bytes.",
        "Whole-session legacy verification precedes consumer budgets. Native guards/cooperative checkpoints do not prove universal race freedom or hard resource bounds." ];
    internal static string Digest(PhaseSixPlan plan)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Domain)); hash.AppendData([0]);
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(plan, Canonical));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static bool Equal<T>(T a, T b) => JsonElement.DeepEquals(
        JsonSerializer.SerializeToElement(a, Canonical), JsonSerializer.SerializeToElement(b, Canonical));
    internal static void ValidateLimits(PhaseSixLimits limits)
    {
        var maximum = new PhaseSixLimits();
        foreach (var p in typeof(PhaseSixLimits).GetProperties()) {
            var value = Convert.ToInt64(p.GetValue(limits));
            if (value < 1 || value > Convert.ToInt64(p.GetValue(maximum))) throw new ArgumentException("Phase 6A limits may only reduce positive hard caps.");
        }
    }
}
internal sealed class PhaseSixBudget(PhaseSixLimits limits, CancellationToken token)
{
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly long memory = GC.GetTotalMemory(false);
    internal void Reduce(PhaseSixLimits narrowed) { PhaseSixFormat.ValidateLimits(narrowed); limits = narrowed; Check(); }
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        if (clock.Elapsed.TotalSeconds > limits.MaximumSeconds || GC.GetTotalMemory(false) - memory > limits.MaximumMemoryGrowth)
            throw new InvalidOperationException("UNION_NEEDS_EVIDENCE: consumer work/memory budget exceeded; no partial plan.");
    }
}
