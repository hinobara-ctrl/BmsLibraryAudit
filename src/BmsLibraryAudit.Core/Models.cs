namespace BmsLibraryAudit.Core;

public sealed record ScanOptions(string DatabasePath, IReadOnlyList<string> Roots, bool RehashAll = false);
public sealed class ScanStatistics
{
    public long Discovered { get; set; }
    public long Reused { get; set; }
    public long Hashed { get; set; }
    public long New { get; set; }
    public long Changed { get; set; }
    public long Missing { get; set; }
    public long Errored { get; set; }
    public long Unstable { get; set; }
}
public sealed record Diagnostic(string Severity, string Code, string Path, string Message);
public sealed record ScanResult(long ScanId, ScanStatistics Statistics, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record ChartRecord(long RootId, string AbsolutePath, string NormalizedPath,
    string ParentFolder, string Filename, string Extension, long Size, long MtimeUtcTicks,
    string? Md5, string? Sha256, long FirstSeenScan, long LastSeenScan, bool Present, string Status);
public sealed record ScanSnapshot(long ScanId, string StartedUtc, string CompletedUtc,
    IReadOnlyList<string> Roots, IReadOnlyList<ChartRecord> Charts,
    ScanStatistics Statistics, IReadOnlyList<Diagnostic> Diagnostics, long UnverifiedChartFiles = 0);
public sealed record FolderEvidence(string Path, int PhysicalChartFiles, IReadOnlySet<string> Hashes);
public sealed record FolderCandidate(string FolderA, string FolderB, int PhysicalFilesA,
    int PhysicalFilesB, int ChartsA, int ChartsB, int Common, double CoverageA,
    double CoverageB, double Jaccard, string Relationship);
public sealed record FolderCluster(string Kind, string Id, IReadOnlyList<string> Members);
public sealed record AnalysisResult(IReadOnlyList<FolderEvidence> Folders,
    IReadOnlyList<IReadOnlyList<ChartRecord>> DuplicateGroups,
    IReadOnlyList<FolderCandidate> Candidates, IReadOnlyList<FolderCluster> Clusters,
    IReadOnlyList<string> IncompleteFolders);
