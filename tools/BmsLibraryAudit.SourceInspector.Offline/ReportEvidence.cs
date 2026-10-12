using System.Security.Cryptography;

namespace BmsLibraryAudit.SourceInspector.Offline;

public enum ReportKind { Summary, Duplicates, Candidates, Clusters }
public enum ParseStatus { Complete, Cancelled, ResourceStopped, Malformed, Unsupported }

// Only the strict parser can construct a binding. No partial fingerprint is exposed.
public sealed class ReportEvidence<T> where T : class
{
    internal ReportEvidence(ReportKind kind, long bytes, string? sha256, T? value, ParseStatus status, Finding? finding, bool endOfInputObserved = false)
    { Kind = kind; BytesConsumed = bytes; Sha256 = sha256; Value = value; Status = status; Finding = finding; EndOfSuppliedInputObserved = endOfInputObserved; }
    public ReportKind Kind { get; }
    public long BytesConsumed { get; }
    public string? Sha256 { get; }
    public T? Value { get; }
    public ParseStatus Status { get; }
    public Finding? Finding { get; }
    public int SchemaVersion => 1;
    public string ToolVersion => "0.1.0";
    public bool ParsingComplete => Status == ParseStatus.Complete;
    public bool BytesComplete => ParsingComplete;
    // EOF/captured buffer alone does not prove strict parsing or completeness of an original file.
    public bool EndOfSuppliedInputObserved { get; }
    public bool FilesystemIdentityVerified => false;
    public bool HistoricalProducerVerified => false;
    public bool LogicalReconciliationVerified => false; // A parser has no source model to reconcile.
}

public sealed record ReportBindings(ReportEvidence<Summary> Summary,
    ReportEvidence<IReadOnlyList<DuplicateRow>> Duplicates, ReportEvidence<IReadOnlyList<Candidate>> Candidates,
    ReportEvidence<IReadOnlyList<ClusterRow>> Clusters)
{
    public bool Complete => Summary.ParsingComplete && Duplicates.ParsingComplete && Candidates.ParsingComplete && Clusters.ParsingComplete;
}

public static partial class ReportInputs
{
    internal static ParseStatus Status(FindingKind kind) => kind switch
    { FindingKind.Invalid => ParseStatus.Malformed, FindingKind.ResourceLimit => ParseStatus.ResourceStopped, _ => ParseStatus.Unsupported };

    private static ReportEvidence<T> Buffer<T>(ReportKind kind, ReadOnlyMemory<byte> bytes, ProcessingPolicy policy,
        Func<ReadOnlyMemory<byte>, T> parse, CancellationToken cancellation) where T : class
    {
        long consumed = 0; var captured = false;
        try
        {
            policy.Validate(); cancellation.ThrowIfCancellationRequested();
            policy.Limit(bytes.Length > policy.MaxInputBytes, "INPUT_BYTE_LIMIT");
            // Own a bounded snapshot; caller mutation after this call cannot rebind parsed rows.
            var snapshot = bytes.ToArray(); consumed = snapshot.Length; captured = true;
            var value = parse(snapshot); cancellation.ThrowIfCancellationRequested();
            return new(kind, snapshot.Length, Convert.ToHexStringLower(SHA256.HashData(snapshot)), value, ParseStatus.Complete, null, true);
        }
        catch (EvidenceException ex) { return new(kind, consumed, null, null, Status(ex.Kind), new(ex.Kind, ex.Code, "Report parsing did not complete."), captured); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { return new(kind, consumed, null, null, ParseStatus.Cancelled, new(FindingKind.Pending, "CANCELLED", "No complete report fingerprint."), captured); }
    }
    public static ReportEvidence<Summary> Summary(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default) =>
        Buffer(ReportKind.Summary, bytes, policy, b => Freeze(Reports.ParseSummary(b, policy, cancellation)), cancellation);
    private static Summary Freeze(Summary summary) => summary with
    {
        Roots = Array.AsReadOnly(summary.Roots.ToArray()), IncompleteFolders = Array.AsReadOnly(summary.IncompleteFolders.ToArray()),
        Diagnostics = Array.AsReadOnly(summary.Diagnostics.ToArray()),
        RelationshipCounts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(summary.RelationshipCounts.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)),
        RelatedDistribution = new System.Collections.ObjectModel.ReadOnlyDictionary<int, long>(summary.RelatedDistribution.ToDictionary(p => p.Key, p => p.Value)),
        ExactDistribution = new System.Collections.ObjectModel.ReadOnlyDictionary<int, long>(summary.ExactDistribution.ToDictionary(p => p.Key, p => p.Value))
    };
    public static ReportEvidence<IReadOnlyList<DuplicateRow>> Duplicates(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default) =>
        CsvBuffer(ReportKind.Duplicates, bytes, Reports.DuplicateHeader, policy, rows => Reports.DuplicateRows(rows, cancellation), cancellation);
    public static ReportEvidence<IReadOnlyList<Candidate>> Candidates(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default) =>
        CsvBuffer(ReportKind.Candidates, bytes, Reports.CandidateHeader, policy, rows => Reports.CandidateRows(rows, cancellation), cancellation);
    public static ReportEvidence<IReadOnlyList<ClusterRow>> Clusters(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default) =>
        CsvBuffer(ReportKind.Clusters, bytes, Reports.ClusterHeader, policy, rows => Reports.ClusterRows(rows, cancellation), cancellation);

    public static ReportEvidence<IReadOnlyList<DuplicateRow>> Duplicates(Stream input, ProcessingPolicy policy, CancellationToken cancellation = default, long? expectedByteCount = null) =>
        Csv(ReportKind.Duplicates, input, Reports.DuplicateHeader, policy, rows => Reports.DuplicateRows(rows, cancellation), cancellation, expectedByteCount);
    public static ReportEvidence<IReadOnlyList<Candidate>> Candidates(Stream input, ProcessingPolicy policy, CancellationToken cancellation = default, long? expectedByteCount = null) =>
        Csv(ReportKind.Candidates, input, Reports.CandidateHeader, policy, rows => Reports.CandidateRows(rows, cancellation), cancellation, expectedByteCount);
    public static ReportEvidence<IReadOnlyList<ClusterRow>> Clusters(Stream input, ProcessingPolicy policy, CancellationToken cancellation = default, long? expectedByteCount = null) =>
        Csv(ReportKind.Clusters, input, Reports.ClusterHeader, policy, rows => Reports.ClusterRows(rows, cancellation), cancellation, expectedByteCount);

    private static ReportEvidence<T> CsvBuffer<T>(ReportKind kind, ReadOnlyMemory<byte> bytes, IReadOnlyList<string> header,
        ProcessingPolicy policy, Func<IEnumerable<string[]>, T> parse, CancellationToken cancellation) where T : class
    {
        policy.Validate();
        if (bytes.Length > policy.MaxInputBytes)
            return new(kind, 0, null, null, ParseStatus.ResourceStopped, new(FindingKind.ResourceLimit, "INPUT_BYTE_LIMIT", "Buffer budget exceeded; no parsing occurred."));
        using var input = new MemoryInput(bytes);
        return Csv(kind, input, header, policy, parse, cancellation, bytes.Length);
    }
    private static ReportEvidence<T> Csv<T>(ReportKind kind, Stream input, IReadOnlyList<string> header,
        ProcessingPolicy policy, Func<IEnumerable<string[]>, T> parse, CancellationToken cancellation, long? expectedByteCount) where T : class
    {
        ArgumentNullException.ThrowIfNull(input); policy.Validate();
        if (expectedByteCount < 0) throw new ArgumentOutOfRangeException(nameof(expectedByteCount));
        using var reader = new IncrementalCsv(input, policy, cancellation);
        try
        {
            policy.Limit(expectedByteCount > policy.MaxStreamBytes, "STREAM_BYTE_LIMIT");
            var value = parse(reader.Rows(header)); cancellation.ThrowIfCancellationRequested();
            if (expectedByteCount is not null && reader.BytesConsumed != expectedByteCount)
                throw new EvidenceException(FindingKind.Pending, "INPUT_LENGTH_MISMATCH");
            if (reader.CompleteDigest is null) throw new EvidenceException(FindingKind.Pending, "CSV_NOT_FULLY_CONSUMED");
            return new(kind, reader.BytesConsumed, reader.CompleteDigest, value, ParseStatus.Complete, null, reader.EndOfInputObserved);
        }
        catch (EvidenceException ex) { return new(kind, reader.BytesConsumed, null, null, Status(ex.Kind), new(ex.Kind, ex.Code, "Report parsing did not complete."), reader.EndOfInputObserved); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { return new(kind, reader.BytesConsumed, null, null, ParseStatus.Cancelled, new(FindingKind.Pending, "CANCELLED", "No complete report fingerprint."), reader.EndOfInputObserved); }
        catch (IOException)
        { return new(kind, reader.BytesConsumed, null, null, ParseStatus.Unsupported, new(FindingKind.Pending, "INPUT_READ_FAILED", "Caller stream failed; report consumption incomplete."), reader.EndOfInputObserved); }
    }
}
