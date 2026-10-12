using System.Security.Cryptography;
using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

public sealed class StreamingTests
{
    [Fact]
    public void FrozenByteCountDetectsTruncationAtAnOtherwiseValidRowBoundary()
    {
        var full = Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader +
            "RELATED,RELATED_000001,2,C:\\Synthetic\\A\nRELATED,RELATED_000001,2,C:\\Synthetic\\B\n");
        // Construct the exact complete-row prefix; do not guess a byte-slice length.
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader + "RELATED,RELATED_000001,2,C:\\Synthetic\\A\n"));
        var result = ReportInputs.Clusters(input, Fixtures.Policy, expectedByteCount: full.Length);
        Assert.False(result.BytesComplete); Assert.Null(result.Sha256); Assert.Null(result.Value);
        Assert.Equal("INPUT_LENGTH_MISMATCH", result.Finding!.Code);
        Assert.True(result.EndOfSuppliedInputObserved); Assert.False(result.ParsingComplete);
    }
    [Fact]
    public void SummaryBindingsCannotBeMutatedThroughCollectionCasts()
    {
        var result = ReportInputs.Summary(Fixtures.Canonical, Fixtures.Policy);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Value!.Roots).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, long>)result.Value!.RelationshipCounts).Clear());
    }
    [Fact]
    public void DifferentValidSummaryBytesCannotRetainTheOriginalBinding()
    {
        var reports = Fixtures.ReportsForSimple() with { SummaryBytes = Fixtures.Legacy };
        var result = Reconciliation.Compare(Fixtures.Evidence(), reports, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.Contains(result.Findings, f => f.Code == "SUMMARY_BYTE_BINDING" && f.Kind == FindingKind.Mismatch);
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement);
    }
    [Fact]
    public void ResourceStopPreservesAlreadyKnownOriginFindings()
    {
        var result = Reconciliation.Compare(Fixtures.Evidence(Fixtures.Mathematics), Fixtures.ReportsForSimple(), 1,
            Fixtures.Policy with { MaxCandidatePairs = 5 });
        Assert.Contains(result.Findings, f => f.Code == "DATABASE_AND_NATIVE_ORIGIN_NOT_VERIFIED");
        Assert.Contains(result.Findings, f => f.Code == "CANDIDATE_PAIR_LIMIT" && f.Kind == FindingKind.ResourceLimit);
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement);
    }
    [Fact]
    public void FailedParserBindingCannotPassReconciliationEvenWithMatchingSuppliedModels()
    {
        var reports = Fixtures.ReportsForSimple();
        var stopped = ReportInputs.Clusters(Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader + "broken\n"), Fixtures.Policy);
        reports = reports with { Bindings = reports.Bindings! with { Clusters = stopped } };
        var result = Reconciliation.Compare(Fixtures.Evidence(), reports, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.False(result.LogicalAgreement); Assert.False(result.CompletedLogicalScope); Assert.Equal(stopped, result.ReportEvidence!.Clusters);
        Assert.Contains(result.Findings, f => f.Kind == FindingKind.Invalid && f.Code == "CSV_FIELD_COUNT");
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(7)] [InlineData(16)] [InlineData(65536)]
    public void Utf8QuotesEmbeddedCrLfAndExactDigestSurviveEveryChunk(int chunk)
    {
        var bytes = Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader +
            "RELATED,RELATED_000001,2,\"C:\\Synthetic\\é😀,\"\"quoted\"\"\r\nend\"\n" +
            "RELATED,RELATED_000001,2,C:\\Synthetic\\B\n");
        using var input = new MemoryStream(bytes);
        var result = ReportInputs.Clusters(input, Fixtures.Policy with { MaxChunkBytes = chunk });
        Assert.Equal(ParseStatus.Complete, result.Status); Assert.Equal(bytes.Length, result.BytesConsumed);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.Sha256);
        Assert.Equal(2, result.Value!.Count); Assert.Equal("C:\\Synthetic\\é😀,\"quoted\"\r\nend", result.Value[0].FolderPath);
        Assert.Equal(@"C:\Synthetic\B", result.Value[1].FolderPath); Assert.True(input.CanRead);
    }
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(31)]
    public void LateMalformedAndInvalidUtf8HaveNoCompleteFingerprint(int chunk)
    {
        var valid = CorrectiveTests.ClusterHeader + "RELATED,RELATED_000001,2,C:\\Synthetic\\A\n";
        foreach (var bytes in new[]
        {
            Encoding.UTF8.GetBytes(valid + "RELATED,RELATED_000001,2,\"open\n"),
            Encoding.UTF8.GetBytes(valid + "RELATED,RELATED_000001,2,C:\\Synthetic\\A\n"),
            Encoding.UTF8.GetBytes(valid).Concat(new byte[] { 0xf0, 0x9f, 0x98 }).ToArray(),
            Encoding.UTF8.GetBytes(valid).Concat(new byte[] { 0xc3, 0x28 }).ToArray()
        })
        {
            using var input = new MemoryStream(bytes);
            var result = ReportInputs.Clusters(input, Fixtures.Policy with { MaxChunkBytes = chunk });
            Assert.Equal(ParseStatus.Malformed, result.Status); Assert.Null(result.Sha256); Assert.Null(result.Value); Assert.False(result.BytesComplete);
        }
    }
    [Fact]
    public void CancellationAtEofCannotPublishAnOtherwiseCompleteDigest()
    {
        var bytes = Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader);
        using var cancel = new CancellationTokenSource();
        using var input = new HookStream(bytes, () => cancel.Cancel(), eofOnly: true);
        var result = ReportInputs.Clusters(input, Fixtures.Policy, cancel.Token);
        Assert.Equal(ParseStatus.Cancelled, result.Status); Assert.Equal(bytes.Length, result.BytesConsumed);
        Assert.Null(result.Sha256); Assert.Null(result.Value);
    }
    [Theory]
    [InlineData("bytes")] [InlineData("chars")] [InlineData("rows")]
    public void StreamStopsAreResourceFindingsAndNeverCorruption(string budget)
    {
        var bytes = Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader +
            "RELATED,RELATED_000001,2,C:\\Synthetic\\A\nRELATED,RELATED_000001,2,C:\\Synthetic\\B\n");
        var policy = Fixtures.Policy with { MaxChunkBytes = 1 };
        policy = budget switch { "bytes" => policy with { MaxStreamBytes = bytes.Length - 1 },
            "chars" => policy with { MaxRetainedReportChars = 1 }, _ => policy with { MaxReportRows = 1 } };
        using var input = new MemoryStream(bytes);
        var result = ReportInputs.Clusters(input, policy);
        Assert.Equal(ParseStatus.ResourceStopped, result.Status); Assert.Equal(FindingKind.ResourceLimit, result.Finding!.Kind);
        Assert.Null(result.Sha256); Assert.Null(result.Value); Assert.False(result.ParsingComplete);
    }
    [Theory]
    [InlineData(5, false)] [InlineData(6, true)] [InlineData(7, true)]
    public void MaterializedPairsHaveASeparateExactBudget(int limit, bool fits)
    {
        var policy = Fixtures.Policy with { MaxCandidatePairs = limit };
        if (fits) Assert.Equal(Fixtures.ExpectedPairs, Relationships.Analyze(Fixtures.Mathematics, [], policy).Candidates);
        else Assert.Equal("CANDIDATE_PAIR_LIMIT", Assert.Throws<EvidenceException>(() => Relationships.Analyze(Fixtures.Mathematics, [], policy)).Code);
    }
    [Theory]
    [InlineData(2, false)] [InlineData(3, true)] [InlineData(4, true)]
    public void DistinctHashBudgetStopsWithoutInventingInvalidEvidence(int limit, bool fits)
    {
        var policy = Fixtures.Policy with { MaxDistinctHashes = limit };
        if (fits) Assert.Equal(4, Relationships.Analyze(Fixtures.Mathematics, [], policy).Folders.Count);
        else Assert.Equal("HASH_LIMIT", Assert.Throws<EvidenceException>(() => Relationships.Analyze(Fixtures.Mathematics, [], policy)).Code);
    }
    [Theory]
    [InlineData(7, false)] [InlineData(8, true)] [InlineData(9, true)]
    public void MembershipBudgetCountsDistinctHashesPerFolder(int limit, bool fits)
    {
        var policy = Fixtures.Policy with { MaxFolderHashMemberships = limit };
        if (fits) Assert.Equal(4, Relationships.Analyze(Fixtures.Mathematics, [], policy).Folders.Count);
        else Assert.Equal("FOLDER_HASH_LIMIT", Assert.Throws<EvidenceException>(() => Relationships.Analyze(Fixtures.Mathematics, [], policy)).Code);
    }
    [Fact]
    public void AllTypedCsvStreamsMatchHandAuthoredRowsAndPreserveOrdering()
    {
        const string candidateHeader = "folder_a,folder_b,physical_files_a,physical_files_b,charts_a,charts_b,common,coverage_a,coverage_b,jaccard,relationship\n";
        using var pairs = new MemoryStream(Encoding.UTF8.GetBytes(candidateHeader + "C:\\Synthetic\\A,C:\\Synthetic\\B,3,2,2,2,2,1,1,1,EXACT_CHART_SET\n"));
        Assert.Equal(Fixtures.ExpectedPairs[0], Assert.Single(ReportInputs.Candidates(pairs, Fixtures.Policy with { MaxChunkBytes = 1 }).Value!));
        var hash = new string('a', 64); var md5 = new string('b', 32);
        const string duplicateHeader = "sha256,md5,group_paths,root_id,absolute_path,parent_folder,filename,extension,size,mtime_utc\n";
        string Row(string name) => $"{hash},{md5},2,1,C:\\Synthetic\\A\\{name},C:\\Synthetic\\A,{name},.bms,1,2020-01-01T00:00:00.0000000+00:00\n";
        using var duplicates = new MemoryStream(Encoding.UTF8.GetBytes(duplicateHeader + Row("b.bms") + Row("a.bms")));
        var parsed = ReportInputs.Duplicates(duplicates, Fixtures.Policy with { MaxChunkBytes = 3 });
        Assert.Equal(2, parsed.Value!.Count); Assert.Equal("b.bms", parsed.Value[0].Filename); Assert.Equal("a.bms", parsed.Value[1].Filename);
        Assert.All(parsed.Value, r => Assert.Equal(2, r.GroupPaths));
    }
    internal sealed class HookStream(byte[] bytes, Action hook, bool eofOnly = false) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, Math.Min(count, 16));
            if (!eofOnly || read == 0) hook(); return read;
        }
    }
}
