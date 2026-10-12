namespace BmsLibraryAudit.SourceInspector.Offline;

// All fields are supplied observations. Construction has no I/O or origin attestation.
public sealed record Ownership(int? ApplicationId, int? UserVersion, string? Tool, string? SchemaVersion, string? Comparison);
public sealed record Statistics(long Discovered, long Reused, long Hashed, long New, long Changed, long Missing, long Errored, long Unstable);
public sealed record Scan(long Id, string StartedUtc, string? CompletedUtc, string Status, int RehashAll,
    IReadOnlyList<string>? Roots, Statistics? Statistics);
public sealed record Root(long Id, string Path, string Key, int Active);
public sealed record Chart(long RootId, string Path, string Key, string Parent, string Name, string Extension,
    long Size, long MtimeTicks, string? Md5, string? Sha256, long FirstScan, long LastScan, int Present, string Status);
public sealed record Diagnostic(long Id, long ScanId, string Severity, string Code, string Path, string Message);
public sealed record Evidence(Ownership? Ownership, IReadOnlyList<Scan> Scans, IReadOnlyList<Root> Roots,
    IReadOnlyList<Chart> Charts, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record Folder(string Path, int PhysicalFiles, IReadOnlyList<string> Hashes);
public sealed record DuplicateGroup(string Sha256, IReadOnlyList<Chart> Locations);
public sealed record Candidate(string FolderA, string FolderB, int PhysicalA, int PhysicalB,
    int ChartsA, int ChartsB, int Common, double CoverageA, double CoverageB, double Jaccard, string Relationship);
public sealed record Component(string Kind, string Id, IReadOnlyList<string> Members);
public sealed record Analysis(IReadOnlyList<Folder> Folders, IReadOnlyList<DuplicateGroup> Duplicates,
    IReadOnlyList<Candidate> Candidates, IReadOnlyList<Component> Components, IReadOnlyList<string> IncompleteFolders);
public enum FindingKind { Invalid, Mismatch, Pending, ResourceLimit, ScopeChanged }
public sealed record ScanScopeChange(long ExpectedScan, long ActualLatestCompletedScan, bool ExpectedScanPresent)
{
    public string FutureNativeDisposition => "INSPECTOR_BLOCKED";
}
public sealed record Finding(FindingKind Kind, string Code, string Detail);
public sealed record LogicalValidation(Scan? Latest, IReadOnlyList<Finding> Findings)
{
    public bool Valid => Findings.All(f => f.Kind == FindingKind.Pending);
    public ScanScopeChange? ScopeChange { get; init; }
    // Corrupt logical facts take precedence over a concurrent scope change.
    public FindingKind? PrimaryStop => Findings.Any(f => f.Kind == FindingKind.Invalid) ? FindingKind.Invalid :
        ScopeChange is not null ? FindingKind.ScopeChanged : Findings.Any(f => f.Kind == FindingKind.ResourceLimit) ? FindingKind.ResourceLimit : null;
}
public sealed record OfflineResult(bool CompletedLogicalScope, bool LogicalAgreement, IReadOnlyList<Finding> Findings,
    Analysis? Analysis, FormatRecognition? SummaryFormat)
{
    public string InspectorStatus => "INSPECTOR_PARTIAL";
    public bool NativeInspectionComplete => false;
    public bool Phase2ExecutionAuthorized => false;
    public ScanScopeChange? ScopeChange { get; init; }
    public ReportBindings? ReportEvidence { get; init; }
}

public sealed record ProcessingPolicy(int MaxInputBytes = 8 * 1024 * 1024, int MaxStringChars = 16 * 1024,
    int MaxJsonDepth = 32, int MaxChartRecords = 250_000, int MaxFolders = 25_000,
    long MaxPairExpansions = 250_000, int MaxFindings = 64, int MaxReportRows = 250_000,
    int MaxChunkBytes = 64 * 1024, long MaxStreamBytes = 256 * 1024 * 1024,
    int MaxDistinctHashes = 250_000, int MaxCandidatePairs = 100_000,
    long MaxRetainedReportChars = 64 * 1024 * 1024, int MaxFolderHashMemberships = 1_000_000)
{
    public void Validate()
    {
        if (MaxInputBytes <= 0 || MaxStringChars <= 0 || MaxJsonDepth <= 0 || MaxJsonDepth > 128 ||
            MaxChartRecords <= 0 || MaxFolders <= 0 || MaxPairExpansions <= 0 || MaxFindings < 2 || MaxReportRows <= 0 ||
            MaxChunkBytes <= 0 || MaxChunkBytes > 1024 * 1024 || MaxStreamBytes <= 0 || MaxDistinctHashes <= 0 || MaxCandidatePairs <= 0 ||
            MaxRetainedReportChars <= 0 || MaxFolderHashMemberships <= 0)
            throw new ArgumentOutOfRangeException(nameof(ProcessingPolicy), "Every offline budget must be positive and finite; findings need a stop marker.");
    }
    internal void Limit(bool exceeded, string name) { if (exceeded) throw new EvidenceException(FindingKind.ResourceLimit, name); }
    internal void Text(string value) => Limit(value.Length > MaxStringChars, "STRING_LIMIT");
}
public sealed class EvidenceException(FindingKind kind, string code, IReadOnlyList<Finding>? priorFindings = null) : Exception(code)
{
    public FindingKind Kind { get; } = kind;
    public string Code { get; } = code;
    public IReadOnlyList<Finding> PriorFindings { get; } = priorFindings ?? [];
}
internal sealed class Findings(ProcessingPolicy policy)
{
    private readonly List<Finding> items = [];
    public void Add(FindingKind kind, string code, string detail)
    {
        policy.Text(detail);
        if (items.Count >= policy.MaxFindings - 1)
            throw new EvidenceException(FindingKind.ResourceLimit, "FINDING_LIMIT", items.ToArray());
        items.Add(new(kind, code, detail));
    }
    public IReadOnlyList<Finding> Items => items.ToArray();
}
