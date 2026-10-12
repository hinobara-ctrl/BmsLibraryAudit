using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

public sealed class ReportTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    [Theory, Trait("OriginalId", "T13.a,T13.b,T13.c")]
    [InlineData(false, SummaryProfile.CanonicalLf)][InlineData(true, SummaryProfile.WindowsLegacy)]
    public void LiteralProfilesMatchOnlySyntheticOracle(bool legacy, SummaryProfile expected)
    {
        var bytes = legacy ? Fixtures.Legacy : Fixtures.Canonical; var before = bytes.ToArray();
        var recognition = Reports.RecognizeSummary(bytes, Fixtures.Policy, Fixtures.Oracle);
        Assert.Equal(expected, recognition.Profile); Assert.True(recognition.ExactOracleMatch);
        Assert.True(recognition.ExternalProvenanceRequired); Assert.False(recognition.ProducerVerified);
        Assert.Equal(before, bytes);
        var s = Reports.ParseSummary(bytes, Fixtures.Policy);
        Assert.Equal(2, s.Current); Assert.Equal(2, s.UniqueSha256); Assert.Equal(1, s.PhysicalFolders); Assert.Empty(s.Diagnostics);
        Assert.Equal(new Statistics(2, 0, 2, 2, 0, 0, 0, 0), s.Statistics);
    }
    [Fact, Trait("OriginalId", "T13.c")]
    public void SameSemanticDataDoesNotEraseProfileDifference()
    {
        var lf = Reports.ParseSummary(Fixtures.Canonical, Fixtures.Policy); var crlf = Reports.ParseSummary(Fixtures.Legacy, Fixtures.Policy);
        Assert.Equal(lf.Current, crlf.Current); Assert.Equal(lf.ScanId, crlf.ScanId); Assert.Equal(lf.Statistics, crlf.Statistics);
        Assert.False(Fixtures.Canonical.SequenceEqual(Fixtures.Legacy));
        Assert.False(Reports.RecognizeSummary(Fixtures.Legacy, Fixtures.Policy).ExactOracleMatch);
    }
    [Theory, Trait("OriginalId", "T27.a,T27.b,T27.c,T27.d,T27.h")]
    [InlineData("mixed")][InlineData("trailing")][InlineData("missing")][InlineData("multiple")][InlineData("terminal_crlf")]
    [InlineData("bom")][InlineData("compact")][InlineData("indent")][InlineData("leading_newline")][InlineData("uniform_crlf")]
    public void UnsupportedByteLayoutsNeverBecomeAcceptedLegacy(string edit)
    {
        var lf = Encoding.UTF8.GetString(Fixtures.Canonical); var legacy = Encoding.UTF8.GetString(Fixtures.Legacy);
        var text = edit switch
        {
            "mixed" => legacy.Replace("  \"tool_version\": \"0.1.0\",\r\n", "  \"tool_version\": \"0.1.0\",\n", StringComparison.Ordinal),
            "trailing" => lf.Replace("{\n", "{ \n", StringComparison.Ordinal), "missing" => lf[..^1], "multiple" => lf + "\n",
            "terminal_crlf" => legacy[..^1] + "\r\n", "bom" => "\ufeff" + lf,
            "compact" => lf.Replace("\n", "", StringComparison.Ordinal) + "\n", "indent" => lf.Replace("  \"tool_version", "    \"tool_version", StringComparison.Ordinal),
            "leading_newline" => "\n" + lf, _ => lf.Replace("\n", "\r\n", StringComparison.Ordinal)
        };
        Assert.Equal(SummaryProfile.Unsupported, Reports.RecognizeSummary(Bytes(text), Fixtures.Policy, Fixtures.Oracle).Profile);
    }
    [Theory, Trait("OriginalId", "T27.f,T27.g,T13.e")]
    [InlineData("duplicate")][InlineData("nested_duplicate")][InlineData("unknown")][InlineData("nested_unknown")][InlineData("type")][InlineData("version")]
    public void MalformedSchemaAndDuplicateKeysHaveExplicitFailures(string mutation)
    {
        var text = Encoding.UTF8.GetString(Fixtures.Canonical);
        text = mutation switch
        {
            "duplicate" => text.Replace("  \"scan_id\": 1,", "  \"scan_id\": 1,\n  \"scan_id\": 1,", StringComparison.Ordinal),
            "nested_duplicate" => text.Replace("    \"reused\": 0,", "    \"reused\": 0,\n    \"reused\": 0,", StringComparison.Ordinal),
            "unknown" => text.Replace("{\n", "{\n  \"unexpected\": 0,", StringComparison.Ordinal),
            "nested_unknown" => text.Replace("    \"reused\": 0,", "    \"unexpected\": 0,", StringComparison.Ordinal),
            "type" => text.Replace("\"scan_id\": 1", "\"scan_id\": \"1\"", StringComparison.Ordinal),
            _ => text.Replace("\"schema_version\": 1", "\"schema_version\": 2", StringComparison.Ordinal)
        };
        Assert.Equal(SummaryProfile.Malformed, Reports.RecognizeSummary(Bytes(text), Fixtures.Policy, Fixtures.Oracle).Profile);
        Assert.Equal(FindingKind.Invalid, Assert.Throws<EvidenceException>(() => Reports.ParseSummary(Bytes(text), Fixtures.Policy)).Kind);
    }
    [Fact, Trait("OriginalId", "T27.e")]
    public void InvalidUtf8IsNeverReplaced()
    {
        byte[] bad = [0xc3, 0x28];
        Assert.Equal(SummaryProfile.Malformed, Reports.RecognizeSummary(bad, Fixtures.Policy, Fixtures.Oracle).Profile);
        Assert.Equal("INVALID_UTF8", Assert.Throws<EvidenceException>(() => Reports.ParseCsv(bad, ["h"], Fixtures.Policy)).Code);
    }
    [Theory, Trait("OriginalId", "T13.e")]
    [InlineData("report")][InlineData("tool")][InlineData("schema")][InlineData("phase")][InlineData("unproven_oracle")]
    public void OracleBindingDoesNotProveHistoricalAuthenticity(string changed)
    {
        var oracle = changed switch
        {
            "report" => Fixtures.Oracle with { ReportType = "clusters.csv" }, "tool" => Fixtures.Oracle with { ToolVersion = "other" },
            "schema" => Fixtures.Oracle with { SchemaVersion = 2 }, "phase" => Fixtures.Oracle with { Phase = "PHASE_2" },
            _ => Fixtures.Oracle with { SpecificationId = "" }
        };
        Assert.False(Reports.RecognizeSummary(Fixtures.Legacy, Fixtures.Policy, oracle).ExactOracleMatch);
        Assert.Equal(SummaryProfile.Unsupported, Reports.RecognizeSummary(Fixtures.Legacy, Fixtures.Policy, oracle).Profile);
    }
    [Fact, Trait("OriginalId", "T27")]
    public void CsvQuotingPreservesCommasQuotesEmbeddedNewlinesAndUnicode()
    {
        var rows = Reports.ParseCsv(Bytes("a,b,c\n\"日本,é\",\"say \"\"hello\"\"\",\"one\r\ntwo\"\n"), ["a", "b", "c"], Fixtures.Policy);
        Assert.Equal(new[] { "日本,é", "say \"hello\"", "one\r\ntwo" }, Assert.Single(rows));
    }
    [Theory, Trait("OriginalId", "T27")]
    [InlineData("a,b\n1,2,3\n")][InlineData("a,b\n1\n")][InlineData("a,a\n1,2\n")]
    [InlineData("a,b\n\"x\"tail,y\n")][InlineData("a,b\n\"x,y\n")][InlineData("a,b\nx\"z,y\n")]
    public void CsvMalformedHeadersFieldsAndQuotesFail(string text) => Assert.Equal(FindingKind.Invalid,
        Assert.Throws<EvidenceException>(() => Reports.ParseCsv(Bytes(text), ["a", "b"], Fixtures.Policy)).Kind);

    [Fact, Trait("OriginalId", "T13,T27")]
    public void FourReportSchemasAndDuplicateRowsAreChecked()
    {
        Assert.Throws<EvidenceException>(() => Reports.ParseCsv(Bytes("a,a\n1,2\n"), ["a", "a"], Fixtures.Policy));
        var duplicateHeader = "sha256,md5,group_paths,root_id,absolute_path,parent_folder,filename,extension,size,mtime_utc\n";
        var row = Fixtures.Hash('a') + "," + new string('a', 32) + ",2,1,C:\\Synthetic\\A\\c0.bms,C:\\Synthetic\\A,c0.bms,.bms,1,0001-01-01T00:00:00.0000000Z\n";
        Assert.Equal(@"C:\Synthetic\A\c0.bms", Assert.Single(Reports.ParseDuplicates(Bytes(duplicateHeader + row), Fixtures.Policy)).Path);
        Assert.Throws<EvidenceException>(() => Reports.ParseDuplicates(Bytes(duplicateHeader + row + row), Fixtures.Policy));
        var candidateHeader = "folder_a,folder_b,physical_files_a,physical_files_b,charts_a,charts_b,common,coverage_a,coverage_b,jaccard,relationship\n";
        var candidate = "C:\\Synthetic\\A,C:\\Synthetic\\B,3,2,2,2,2,1,1,1,EXACT_CHART_SET\n";
        Assert.Equal(Fixtures.ExpectedPairs[0], Assert.Single(Reports.ParseCandidates(Bytes(candidateHeader + candidate), Fixtures.Policy)));
        Assert.Throws<EvidenceException>(() => Reports.ParseCandidates(Bytes(candidateHeader + candidate + candidate), Fixtures.Policy));
        Assert.Throws<EvidenceException>(() => Reports.ParseCandidates(Bytes(candidateHeader + candidate.Replace(",1,1,1,", ",NaN,1,1,", StringComparison.Ordinal)), Fixtures.Policy));
        var cluster = "kind,cluster_id,folder_count,folder_path\nEXACT_ONLY,EXACT_ONLY_000001,3,C:\\Synthetic\\A\n";
        Assert.Equal(new ClusterRow("EXACT_ONLY", "EXACT_ONLY_000001", 3, @"C:\Synthetic\A"), Assert.Single(Reports.ParseClusters(Bytes(cluster), Fixtures.Policy)));
        Assert.Throws<EvidenceException>(() => Reports.ParseClusters(Bytes(cluster + "EXACT_ONLY,EXACT_ONLY_000001,3,C:\\Synthetic\\A\n"), Fixtures.Policy));
    }
    [Theory, Trait("OriginalId", "T01,T13.a,T13.b")]
    [InlineData(false)][InlineData(true)]
    public void OfflineIntegrationNeverEmitsCompleteInspectorVerification(bool legacy)
    {
        var result = Reconciliation.Compare(Fixtures.Evidence(), Fixtures.ReportsForSimple(legacy ? Fixtures.Legacy : Fixtures.Canonical), 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.True(result.LogicalAgreement); Assert.True(result.CompletedLogicalScope);
        Assert.Equal("INSPECTOR_PARTIAL", result.InspectorStatus); Assert.False(result.NativeInspectionComplete); Assert.False(result.Phase2ExecutionAuthorized);
        Assert.Contains(result.Findings, f => f.Code == "REPORT_EXTERNAL_PROVENANCE_REQUIRED");
    }
    [Theory, Trait("OriginalId", "T13,T13.d,T13.e")]
    [InlineData("count")][InlineData("date")][InlineData("roots")][InlineData("notice")][InlineData("statistics")][InlineData("unknown_raw")]
    public void MatchingAggregatesDoNotWaiveSummaryDetails(string change)
    {
        var r = Fixtures.ReportsForSimple();
        var summary = change switch
        {
            "count" => r.Summary with { Current = 3 }, "date" => r.Summary with { CompletedUtc = Fixtures.Start },
            "roots" => r.Summary with { Roots = [@"C:\Elsewhere"] }, "notice" => r.Summary with { EvidenceNotice = "different" },
            "statistics" => r.Summary with { Statistics = new(2, 2, 0, 2, 0, 0, 0, 0) }, _ => r.Summary
        };
        if (change == "unknown_raw") r = r with { SummaryBytes = Bytes(Encoding.UTF8.GetString(Fixtures.Canonical).Replace("\"current_chart_files\": 2", "\"current_chart_files\": 3", StringComparison.Ordinal)) };
        var result = Reconciliation.Compare(Fixtures.Evidence(), r with { Summary = summary }, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.False(result.LogicalAgreement); Assert.Contains(result.Findings, f => f.Kind == FindingKind.Mismatch);
    }
    [Fact, Trait("OriginalId", "T13,T34")]
    public void CompleteRowsMultiplicityAndExternalExactNeighborAreCompared()
    {
        var e = Fixtures.Evidence(Fixtures.Mathematics); var simple = Fixtures.ReportsForSimple().Summary;
        var summary = simple with
        {
            Current = 9, Verified = 9, UniqueSha256 = 3, UniqueMd5 = 3, PhysicalFolders = 4, AnalyzedFolders = 4,
            DuplicateGroups = 2, CandidatePairs = 6, RelatedCount = 1, ExactCount = 1, RelatedDistribution = new Dictionary<int, long> { [4] = 1 },
            ExactDistribution = new Dictionary<int, long> { [3] = 1 }, Statistics = new(9, 0, 9, 9, 0, 0, 0, 0),
            RelationshipCounts = new Dictionary<string, long> { ["EXACT_CHART_SET"] = 3, ["A_SUBSET_OF_B"] = 0, ["B_SUBSET_OF_A"] = 0, ["HIGH_OVERLAP"] = 0, ["MEDIUM_OVERLAP"] = 3, ["SHARED_CHARTS"] = 0 }
        };
        var rows = new[] { 0, 1, 3, 5, 7, 2, 4, 6 }.Select(i =>
        {
            var c = e.Charts[i]; return new DuplicateRow(c.Sha256!, c.Md5!, i is 2 or 4 or 6 ? 3 : 5, 1, c.Path, c.Parent, c.Name, c.Extension, 1, "0001-01-01T00:00:00.0000000Z");
        }).ToArray();
        var clusters = new[] { "A", "B", "C", "D" }.Select(f => new ClusterRow("RELATED", "RELATED_000001", 4, Fixtures.RootPath + "\\" + f))
            .Concat(new[] { "A", "B", "C" }.Select(f => new ClusterRow("EXACT_ONLY", "EXACT_ONLY_000001", 3, Fixtures.RootPath + "\\" + f))).ToArray();
        var r = new ParsedReports(summary, rows, Fixtures.ExpectedPairs, clusters, ReadOnlyMemory<byte>.Empty);
        var valid = Reconciliation.Compare(e, r, 1, Fixtures.Policy);
        Assert.True(valid.LogicalAgreement); Assert.False(valid.CompletedLogicalScope); Assert.Contains(valid.Findings, f => f.Code == "SUMMARY_BYTES_MISSING");
        Assert.False(Reconciliation.Compare(e, r with { Candidates = r.Candidates.Select((c, i) => i == 0 ? c with { PhysicalA = 2 } : c).ToArray() }, 1, Fixtures.Policy).LogicalAgreement);
        Assert.False(Reconciliation.Compare(e, r with { Duplicates = rows[..^1] }, 1, Fixtures.Policy).LogicalAgreement);
        Assert.False(Reconciliation.Compare(e, r with { Clusters = clusters[..^1] }, 1, Fixtures.Policy).LogicalAgreement);
        var selection = new Dictionary<string, IReadOnlyList<string>> { ["EXACT_ONLY_000001"] = [@"C:\Synthetic\A", @"C:\Synthetic\B"] };
        Assert.Contains(Reconciliation.Compare(e, r, 1, Fixtures.Policy, selectedExact: selection).Findings, f => f.Code == "SELECTED_EXACT_CLOSURE");
        var extra = e.Charts.Concat(new[] { Fixtures.Chart("External", 0), Fixtures.Chart("External", 1, 'b') }).ToArray();
        Assert.False(Reconciliation.Compare(Fixtures.Evidence(extra), r, 1, Fixtures.Policy).LogicalAgreement);
    }
    [Theory, Trait("OriginalId", "T13,T27")]
    [InlineData("physical")][InlineData("common")][InlineData("label")][InlineData("ratio")][InlineData("float_format")]
    public void CandidateCsvRejectsContradictoryMathAndUnsupportedNumberFormatting(string change)
    {
        const string header = "folder_a,folder_b,physical_files_a,physical_files_b,charts_a,charts_b,common,coverage_a,coverage_b,jaccard,relationship\n";
        var row = new[] { @"C:\Synthetic\A", @"C:\Synthetic\B", "3", "2", "2", "2", "2", "1", "1", "1", "EXACT_CHART_SET" };
        switch (change) { case "physical": row[2] = "1"; break; case "common": row[6] = "3"; break; case "label": row[10] = "SHARED_CHARTS"; break; case "ratio": row[7] = "0.5"; break; default: row[7] = "1.0"; break; }
        var error = Assert.Throws<EvidenceException>(() => Reports.ParseCandidates(Bytes(header + string.Join(',', row) + "\n"), Fixtures.Policy));
        Assert.Equal(change == "float_format" ? FindingKind.Pending : FindingKind.Invalid, error.Kind);
    }
    [Fact, Trait("OriginalId", "T27.e,T23")]
    public void JsonEscapesDecodeUnicodeWithoutFollowingPaths()
    {
        var text = Encoding.UTF8.GetString(Fixtures.Canonical).Replace("""C:\\Synthetic""", """X:\\NeverCreated\\\u65E5\u672C\u00E9""", StringComparison.Ordinal);
        Assert.Equal(@"X:\NeverCreated\日本é", Assert.Single(Reports.ParseSummary(Bytes(text), Fixtures.Policy).Roots));
    }
    [Theory, Trait("OriginalId", "T27.e")]
    [InlineData("\\uD800")][InlineData("\\uDC00")][InlineData("\\uD800\\u0041")]
    public void InvalidJsonSurrogatesAreNotReplaced(string escape)
    {
        var text = Encoding.UTF8.GetString(Fixtures.Canonical).Replace("""C:\\Synthetic""", """C:\\""" + escape, StringComparison.Ordinal);
        Assert.Equal("INVALID_JSON_UNICODE", Assert.Throws<EvidenceException>(() => Reports.ParseSummary(Bytes(text), Fixtures.Policy)).Code);
    }
    [Fact, Trait("OriginalId", "T27.e,T27.f")]
    public void ValidSurrogatePairsAndEscapedDuplicateKeysHaveIndependentOracles()
    {
        var text = Encoding.UTF8.GetString(Fixtures.Canonical);
        var pair = text.Replace("""C:\\Synthetic""", """C:\\\uD83D\uDE00""", StringComparison.Ordinal);
        Assert.Equal("C:\\😀", Assert.Single(Reports.ParseSummary(Bytes(pair), Fixtures.Policy).Roots));
        var duplicate = text.Replace("  \"scan_id\": 1,", "  \"scan_id\": 1,\n  \"\\u0073can_id\": 1,", StringComparison.Ordinal);
        Assert.Equal("DUPLICATE_JSON_KEY", Assert.Throws<EvidenceException>(() => Reports.ParseSummary(Bytes(duplicate), Fixtures.Policy)).Code);
    }
}
