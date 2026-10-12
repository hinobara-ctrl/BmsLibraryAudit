using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

public sealed class ResourceTests
{
    [Theory, Trait("OriginalId", "T27,T33")]
    [InlineData(1, false)][InlineData(2, true)][InlineData(3, true)]
    public void InputBytesAreMeasuredAtBelowAndAboveLimit(int limit, bool fits)
    {
        var policy = Fixtures.Policy with { MaxInputBytes = limit }; var bytes = Encoding.UTF8.GetBytes("h\n");
        if (fits) Assert.Empty(Reports.ParseCsv(bytes, ["h"], policy));
        else Assert.Equal(FindingKind.ResourceLimit, Assert.Throws<EvidenceException>(() => Reports.ParseCsv(bytes, ["h"], policy)).Kind);
    }
    [Theory, Trait("OriginalId", "T27,T33")]
    [InlineData(2, false)][InlineData(3, true)][InlineData(4, true)]
    public void FieldCharsAreMeasuredAtBelowAndAboveLimit(int limit, bool fits)
    {
        var bytes = Encoding.UTF8.GetBytes("h\nabc\n"); var policy = Fixtures.Policy with { MaxStringChars = limit };
        if (fits) Assert.Equal("abc", Assert.Single(Reports.ParseCsv(bytes, ["h"], policy))[0]);
        else Assert.Equal("FIELD_LIMIT", Assert.Throws<EvidenceException>(() => Reports.ParseCsv(bytes, ["h"], policy)).Code);
    }
    [Theory, Trait("OriginalId", "T33")]
    [InlineData(8, false)][InlineData(9, true)][InlineData(10, true)]
    public void ChartRecordLimitCannotSilentlyDropRows(int limit, bool fits)
    {
        var policy = Fixtures.Policy with { MaxChartRecords = limit };
        if (fits) Assert.Equal(4, Relationships.Analyze(Fixtures.Mathematics, [], policy).Folders.Count);
        else Assert.Equal(FindingKind.ResourceLimit, Assert.Throws<EvidenceException>(() => Relationships.Analyze(Fixtures.Mathematics, [], policy)).Kind);
    }
    [Theory, Trait("OriginalId", "T33")]
    [InlineData(3, false)][InlineData(4, true)][InlineData(5, true)]
    public void FolderCountLimitStopsBeforeGraphExpansion(int limit, bool fits)
    {
        var policy = Fixtures.Policy with { MaxFolders = limit };
        if (fits) Assert.Equal(4, Relationships.Analyze(Fixtures.Mathematics, [], policy).Folders.Count);
        else Assert.Equal("FOLDER_LIMIT", Assert.Throws<EvidenceException>(() => Relationships.Analyze(Fixtures.Mathematics, [], policy)).Code);
    }
    [Theory, Trait("OriginalId", "T33")]
    [InlineData(8, false)][InlineData(9, true)][InlineData(10, true)]
    public void HandCountedPairExpansionLimitPrecedesAllocation(int limit, bool fits)
    {
        // x appears in 4 folders => 6 expansions; y in 3 => 3; z in 1 => 0. Total 9, six distinct pairs.
        var policy = Fixtures.Policy with { MaxPairExpansions = limit };
        if (fits) Assert.Equal(6, Relationships.Analyze(Fixtures.Mathematics, [], policy).Candidates.Count);
        else Assert.Equal("PAIR_EXPANSION_LIMIT", Assert.Throws<EvidenceException>(() => Relationships.Analyze(Fixtures.Mathematics, [], policy)).Code);
    }
    [Theory, Trait("OriginalId", "T27,T33")]
    [InlineData(1, false)][InlineData(2, true)][InlineData(3, true)]
    public void ReportRowLimitStopsWithoutTruncating(int limit, bool fits)
    {
        var bytes = Encoding.UTF8.GetBytes("h\na\nb\n"); var policy = Fixtures.Policy with { MaxReportRows = limit };
        if (fits) Assert.Equal(2, Reports.ParseCsv(bytes, ["h"], policy).Count);
        else Assert.Equal(FindingKind.ResourceLimit, Assert.Throws<EvidenceException>(() => Reports.ParseCsv(bytes, ["h"], policy)).Kind);
    }
    [Theory, Trait("OriginalId", "T27,T33")]
    [InlineData(2, false)][InlineData(3, true)][InlineData(4, true)]
    public void SummaryDepthLimitIsResourceExhaustion(int depth, bool fits)
    {
        // summary object -> roots array and maps: maximum depth 2 for this literal fixture.
        var policy = Fixtures.Policy with { MaxJsonDepth = depth - 1 };
        if (fits) Assert.Equal(2, Reports.ParseSummary(Fixtures.Canonical, policy).Current);
        else Assert.Equal(FindingKind.ResourceLimit, Assert.Throws<EvidenceException>(() => Reports.ParseSummary(Fixtures.Canonical, policy)).Kind);
    }
    [Fact, Trait("OriginalId", "T33,T36")]
    public void FindingBudgetReturnsExplicitIncompleteStop()
    {
        var e = Fixtures.Evidence(); var invalid = e with { Charts = e.Charts.Select(c => c with { RootId = 99, LastScan = 99, Status = "UNKNOWN" }).ToArray() };
        var result = Reconciliation.Compare(invalid, Fixtures.ReportsForSimple(), 1, Fixtures.Policy with { MaxFindings = 2 }, Fixtures.Oracle);
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement);
        Assert.Equal(2, result.Findings.Count); Assert.Equal(FindingKind.Invalid, result.Findings[0].Kind);
        Assert.Equal("FINDING_LIMIT", result.Findings[1].Code);
        Assert.Equal(FindingKind.ResourceLimit, result.Findings[1].Kind);
    }
    [Fact, Trait("OriginalId", "T20")]
    public void CancellationLeavesNoFalseLogicalOrNativeSuccess()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => Provenance.Validate(Fixtures.Evidence(), 1, Fixtures.Policy, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => Relationships.Analyze(Fixtures.Mathematics, [], Fixtures.Policy, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => Reports.ParseSummary(Fixtures.Canonical, Fixtures.Policy, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => Reports.ParseCsv(Encoding.UTF8.GetBytes("h\n"), ["h"], Fixtures.Policy, cancel.Token));
        var result = Reconciliation.Compare(Fixtures.Evidence(), Fixtures.ReportsForSimple(), 1, Fixtures.Policy, cancellation: cancel.Token);
        Assert.Equal("CANCELLED", Assert.Single(result.Findings).Code); Assert.False(result.CompletedLogicalScope); Assert.False(result.NativeInspectionComplete);
    }
    [Fact, Trait("OriginalId", "T26")]
    public void AssemblyClosureHasNoProductionOrSqliteReferences()
    {
        var assembly = typeof(Evidence).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), n => n.Name!.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
            n.Name.Contains("SQLitePCL", StringComparison.Ordinal) || n.Name.Contains("BmsLibraryAudit.Core", StringComparison.Ordinal) || n.Name.Contains("BmsLibraryAudit.Cli", StringComparison.Ordinal));
        Assert.Null(assembly.EntryPoint);
    }
    [Theory, Trait("OriginalId", "T33")]
    [InlineData(0)][InlineData(-1)]
    public void UnlimitedPoliciesAreRejected(int count) => Assert.Throws<ArgumentOutOfRangeException>(() => (Fixtures.Policy with { MaxChartRecords = count }).Validate());
    [Theory, Trait("OriginalId", "T33,T36")]
    [InlineData(2, false)][InlineData(3, true)][InlineData(4, true)]
    public void FindingCapacityReservesOneExplicitStopMarker(int limit, bool fits)
    {
        var result = Reconciliation.Compare(Fixtures.Evidence(), Fixtures.ReportsForSimple(), 1, Fixtures.Policy with { MaxFindings = limit }, Fixtures.Oracle);
        Assert.Equal(fits, result.LogicalAgreement);
        if (fits) Assert.Equal(2, result.Findings.Count); else Assert.Equal("FINDING_LIMIT", result.Findings[^1].Code);
    }
    [Fact, Trait("OriginalId", "T20")]
    public void CancellationDuringChartEnumerationStopsAtTheNextCheckpoint()
    {
        using var cancel = new CancellationTokenSource();
        var rows = new CancelDuringEnumeration(Fixtures.Mathematics, cancel);
        Assert.Throws<OperationCanceledException>(() => Relationships.Analyze(rows, [], Fixtures.Policy, cancel.Token));
        Assert.Equal(2, rows.Visited);
    }
    private sealed class CancelDuringEnumeration(Chart[] rows, CancellationTokenSource cancel) : IReadOnlyList<Chart>
    {
        public int Visited { get; private set; }
        public int Count => rows.Length;
        public Chart this[int index] => rows[index];
        public IEnumerator<Chart> GetEnumerator()
        {
            foreach (var row in rows) { Visited++; if (Visited == 2) cancel.Cancel(); yield return row; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
