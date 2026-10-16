using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BmsLibraryAudit.Core;

public sealed record PhaseSixGroupLimits(int MaximumRoots = 6, int MaximumSubsets = 63,
    int MaximumEntries = 20000, int MaximumConflicts = 20000, int MaximumVirtualEntries = 100000,
    int MaximumPlannedAppearances = 200000, long MaximumExaminedEntries = 1280000,
    int MaximumReportBytes = 16 * 1024 * 1024, long MaximumMemoryGrowth = 128L * 1024 * 1024, int MaximumSeconds = 60);
public sealed record PhaseSixGroupSubset(int Mask, long[] RootIds, string PhysicalState);
public sealed record PhaseSixGroupVariant(string ConditionalLabel, PhaseSixRoot OriginalRoot,
    string PreservationState = "PRESERVE_IN_PLACE", string RelocationState = "RELOCATION_NOT_PROVEN");
public sealed record PhaseSixGroupDisposition(PhaseSixSource Source, string Disposition, string RelativePath, string? ConditionalVariantLabel);
public sealed record PhaseSixGroupCandidate(string CandidateId, long[] RootIds, PhaseSixVirtualEntry[] VirtualEntries,
    PhaseSixGroupVariant[] Variants, PhaseSixGroupDisposition[] Conservation, string[] VariantLayoutConflicts,
    string Role = "MAIN_GROUP_CANDIDATE", string PhysicalState = "UNION_PHYSICALLY_COMPATIBLE");
public sealed record PhaseSixGroupStates(string SelectionState, string EnumerationState = "EXHAUSTIVE_WITHIN_SELECTED_ROOTS",
    string InventoryState = "COMPLETE_FROM_ACCEPTED_CONTENT", string ConservationState = "COMPLETE_ACCOUNTING",
    string SemanticState = "SEMANTIC_EVIDENCE_UNKNOWN", string PackageBoundaryState = "PACKAGE_BOUNDARY_UNKNOWN",
    string RelocationState = "RELOCATION_NOT_PROVEN", string ConstructionState = "NOT_CONSTRUCTED",
    string VerificationState = "VIRTUAL_GROUP_PLAN_CHECKED", string PhysicalVerificationState = "NOT_PHYSICALLY_VERIFIED",
    string PublicationState = "NOT_PUBLISHED", string RetirementState = "NOT_RETIRED",
    bool MainGroupSelected = false, bool DependencyClosureProven = false, bool RelocationSafe = false,
    bool MergeAuthority = false, bool RemovalAuthority = false);
public sealed record PhaseSixGroupCounts(int Roots, int ExaminedSubsets, int MaximalCandidates, int SourceAppearances,
    int ChartAppearances, int VirtualEntriesAcrossAlternatives, int AccountedAppearancesAcrossAlternatives, int ConflictAnnotations);
public sealed record PhaseSixGroupPlan(string PlanVersion, string EvidenceProfile, PhaseFiveProvenance Provenance,
    PhaseSixRoot[] Roots, PhaseSixConflict[] Conflicts, PhaseSixGroupSubset[] Subsets,
    PhaseSixGroupCandidate[] Candidates, PhaseSixChartObligation[] ChartObligations,
    PhaseSixGroupStates States, PhaseSixGroupCounts Counts, PhaseSixGroupLimits Limits, string[] Limitations);
public sealed record PhaseSixGroupEnvelope(PhaseSixGroupPlan Plan, string PlanSha256)
{
    [JsonIgnore] public int ExitCode => Plan.States.SelectionState == "HUMAN_SELECTION_REQUIRED" ? 2 : 0;
}
internal static class PhaseSixGroupFormat
{
    internal const string Version = "phase6b-group-variant-plan-1";
    internal const string Profile = "PREFLIGHT_AND_CONTENT/phase2ab-content-1/EXACT_ONLY";
    internal const string Filename = "group_union_plan.json";
    internal static readonly string[] Limitations = [
        "Historical selected observation roots only; complete package boundaries and current source-byte freshness are UNKNOWN.",
        "All chart appearances, opaque files, directories and unknown dependency obligations are conserved. No Phase 4 reports admitted.",
        "Candidates are alternatives, never a selected main distribution. Conditional variant labels are not authorized destinations.",
        "PRESERVE_IN_PLACE; relocation, NTFS metadata preservation and playback safety are not proved. No construction or physical destination verification.",
        "No publication, retirement, merge/removal authority or recoverable-space promise. No observation-pair adapter.",
        "Digest integrity is not authenticity. Validator revision is declared, not authenticated; UNKNOWN_DIRTY is unknown identity.",
        "Whole-session legacy verification precedes consumer budgets; cancellation/checkpoints are cooperative, not universal TOCTOU or instantaneous resource bounds." ];
    internal static string Id(int mask) => "group-" + mask.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    internal static string Digest(PhaseSixGroupPlan plan)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("BmsLibraryAudit.Phase6B.GroupVariantPlan.v1\0"));
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(plan, PhaseSixFormat.Canonical));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static void ValidateLimits(PhaseSixGroupLimits limits)
    {
        var cap = new PhaseSixGroupLimits();
        foreach (var property in typeof(PhaseSixGroupLimits).GetProperties()) {
            var n = Convert.ToInt64(property.GetValue(limits));
            if (n < 1 || n > Convert.ToInt64(property.GetValue(cap))) throw new ArgumentException("Phase 6B limits may only reduce positive hard caps.");
        }
    }
    internal static PhaseSixLimits AdmissionLimits(PhaseSixGroupLimits l) => new(MaximumEntries: l.MaximumEntries,
        MaximumConflicts: l.MaximumConflicts, MaximumReportBytes: l.MaximumReportBytes,
        MaximumMemoryGrowth: l.MaximumMemoryGrowth, MaximumSeconds: l.MaximumSeconds);
}
