namespace BmsLibraryAudit.Core;

public sealed record PhaseFiveOptions(string DatabasePath, long ContentSessionId, string AcceptedContentManifestSha256,
    string ComponentKey, string OutputDirectory, IReadOnlyList<string>? PhaseFourReports = null);
public sealed record PhaseFiveLimits(int MaximumRoots = 32, int MaximumPairs = 256, int MaximumEntries = 100000,
    int MaximumPhaseFourReports = 64, int MaximumInputBytes = 64 * 1024 * 1024, int MaximumSemanticRelations = 100000,
    int MaximumConflicts = 100000, int MaximumOutputBytes = 64 * 1024 * 1024, int MaximumDiscoveryCharts = 100000,
    int MaximumDiscoveryFolders = 128, int MaximumDiscoveryPairs = 1024);
public sealed record PhaseFiveEntry(long EntryId, long? PhysicalFileId, long ObservationRootId, long ComponentId,
    string EntryKey, string OriginalPath, string RelativePath, string Kind, string? NativeIdentity, string? Sha256,
    long? Size, long? MtimeTicks, long? Attributes, bool IsChart, string? CapturedPhaseOneSha256);
public sealed record PhaseFiveDependency(long RootId, long ChartEntryId, string ChartSha256, string Admission,
    string Trust, string[] Diagnostics, string? ReportSha256, PhaseFourResult? VerifiedReport,
    string? ResolverBuildRevisionState = null);
public sealed record PhaseFiveRoot(long Id, string Key, string OriginalPath, string[] LibraryRoots,
    string[] ChartParents, long[] NestedRootIds, string StructuralState, string PackageBoundary,
    string PhysicalEvidenceState, string SemanticCoverage, string SemanticTrust,
    PhaseFiveEntry[] Entries, PhaseFiveDependency[] Dependencies);
public sealed record PhaseFiveChartRelation(string State, string Trust, int PhysicalChartsA, int PhysicalChartsB,
    string[] HashesA, string[] HashesB, string[] Common, string[] OnlyA, string[] OnlyB);
public sealed record PhaseFiveDifference(string RelativePath, string Category, PhaseFiveEntry? EntryA, PhaseFiveEntry? EntryB);
public sealed record PhaseFiveContentGroup(string Projection, string Sha256, long CountA, long CountB, bool DifferentPaths);
public sealed record PhaseFiveReference(long RootId, long ChartEntryId, string ChartSha256, int DeclarationId,
    string? LogicalId, string? WrittenPath, string PathPolicy, string Model, string Reachability, int[] UsageIds,
    string? CandidateMatchPolicy = null, string? CandidateOrigin = null, long? CandidateEntryId = null,
    string? CandidateRelativePath = null);
public sealed record PhaseFiveConflict(string RelativePath, string[] Types, PhaseFiveEntry? EntryA, PhaseFiveEntry? EntryB,
    PhaseFiveReference[] References);
public sealed record PhaseFivePair(long RootA, long RootB, string Trust, PhaseFiveChartRelation Charts,
    bool? ObservedTreeExact, bool? ObservedNonChartTreeExact, bool? ContentMultisetExact,
    bool? NonChartContentMultisetExact, bool? TreeSubsetAInB, bool? TreeSubsetBInA,
    string[] Labels, string[] SupportingDimensions, PhaseFiveDifference[] Differences,
    PhaseFiveContentGroup[] ContentGroups, PhaseFiveConflict[] Conflicts, string ConflictCoverage,
    string DependencyState, string PackageBoundary, string[] Limitations);
public sealed record PhaseFiveDiscovery(string FolderA, string FolderB, string Relationship, int ChartsA,
    int ChartsB, int Common, string Trust, string PhysicalComparisonState);
public sealed record PhaseFiveProvenance(long ContentSessionId, string ContentDigest, long ParentPreflightId,
    string PreflightDigest, long ComponentId, string ComponentKey, string SourceSnapshotDigest,
    string ContentStartedUtc, string ContentCompletedUtc, string ToolVersion, string BuildRevision);
public sealed record PhaseFiveResult(string ResultVersion, PhaseFiveProvenance Provenance, string Coverage,
    long TotalRoots, long ExpectedPairs, PhaseFiveRoot[] Roots, PhaseFivePair[] Relationships,
    PhaseFiveDiscovery[] PendingDiscoveries, string[] Diagnostics, string[] Limitations, PhaseFiveLimits Limits,
    bool DependencyClosureProven = false, bool PackageEquivalenceProven = false,
    bool MergeAuthority = false, bool RemovalAuthority = false)
{
    public int ExitCode => Coverage == "COMPLETE" ? 0 : 2;
}
