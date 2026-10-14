namespace BmsLibraryAudit.Core;

public sealed record PhaseFourOptions(string DatabasePath, long ContentSessionId, string AcceptedContentManifestSha256,
    string ChartReportPath, string ObservationRootKey, string OutputDirectory);
public sealed record PhaseFourLimits(int MaximumReportBytes = 64 * 1024 * 1024, int MaximumInventoryFiles = 100000,
    int MaximumReferences = 50000, int MaximumCandidatesPerReference = 64, int MaximumCandidateLinks = 100000,
    int MaximumConditionalDepth = 64, int MaximumSymbolicTerms = 100000);
public sealed record ObservedResourceCandidate(long EntryId, long PhysicalFileId, long ObservationRootId,
    long ComponentId, string EntryKey, string OriginalPath, string RelativePath, string NativeIdentity,
    string Sha256, long Size, string MatchPolicy);
public sealed record SymbolicConstraint(int RandomContextId, int UpperBound, int? EqualValue, int[] ExcludedValues);
public sealed record ResourceReachability(string State, string Model, SymbolicConstraint[] Constraints, string Justification);
public sealed record ResolvedResourceDeclaration(int DeclarationId, string? WrittenPath, string RelativeBase,
    string? CandidateRelativePath, string PathPolicy, string ResolutionState, string SemanticState,
    ResourceReachability Reachability, int[] PotentialUsageIds, ObservedResourceCandidate[] Candidates, string[] Limitations);
public sealed record ResolvedResourceUsage(int UsageId, ResourceReachability Reachability, string SemanticState);
public sealed record PhaseFourProvenance(long ContentSessionId, string ContentDigest, long ParentPreflightId,
    string PreflightDigest, long ComponentId, string ComponentKey, long ObservationRootId, string ObservationRootKey,
    string ObservationRootPath, string SourceLibraryRoot, string ChartParent, string? PackageRoot,
    ObservedResourceCandidate Chart, string ChartSha256, string ContentStartedUtc, string ContentCompletedUtc,
    string ToolVersion, string ResolverBuildRevision);
public sealed record PhaseFourResult(string ResultVersion, PhaseFourProvenance Provenance, ChartSyntaxResult VerifiedSyntax,
    ResolvedResourceDeclaration[] Declarations, ResolvedResourceUsage[] Usages, string ResolutionCoverage,
    string ConditionalCoverage, string[] Diagnostics, string[] Limitations, bool DependencyClosureProven,
    bool RemovalAuthority, PhaseFourLimits Limits, string Notice)
{
    public int ExitCode => ResolutionCoverage == "COMPLETE_OBSERVED_PATH_ANALYSIS" &&
        ConditionalCoverage == "COMPLETE_BOUNDED_MODEL" && VerifiedSyntax.SyntaxCoverage == "COMPLETE_SUPPORTED_SYNTAX" ? 0 : 2;
}
