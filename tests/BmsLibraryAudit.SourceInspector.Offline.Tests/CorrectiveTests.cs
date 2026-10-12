using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

public sealed class CorrectiveTests
{
    internal const string ClusterHeader = "kind,cluster_id,folder_count,folder_path\n";
    [Fact]
    public void ExactBytesBindImmutableRowsWithoutAuthenticatingOrigin()
    {
        var bytes = Encoding.UTF8.GetBytes(ClusterHeader + "RELATED,RELATED_000001,2,C:\\Synthetic\\A\n");
        var first = ReportInputs.Clusters(bytes, Fixtures.Policy);
        var quoted = ReportInputs.Clusters(Encoding.UTF8.GetBytes(ClusterHeader + "RELATED,RELATED_000001,2,\"C:\\Synthetic\\A\"\n"), Fixtures.Policy);
        Assert.True(first.ParsingComplete); Assert.Equal(bytes.Length, first.BytesConsumed);
        Assert.Equal(first.Value, quoted.Value); Assert.NotEqual(first.Sha256, quoted.Sha256);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), first.Sha256);
        bytes[^2] = (byte)'B';
        Assert.NotEqual(first.Sha256, ReportInputs.Clusters(bytes, Fixtures.Policy).Sha256);
        Assert.EndsWith("A", first.Value![0].FolderPath);
        Assert.Throws<NotSupportedException>(() => ((IList<ClusterRow>)first.Value).Clear());
        Assert.False(first.FilesystemIdentityVerified); Assert.False(first.HistoricalProducerVerified); Assert.False(first.LogicalReconciliationVerified);
    }
    [Theory]
    [InlineData("RELATED,RELATED_000001,2,C:\\Synthetic\\A")]
    [InlineData("RELATED,RELATED_000001,2,\"unterminated\n")]
    [InlineData("RELATED,RELATED_000001,2,C:\\Synthetic\\A\nRELATED,RELATED_000001,2,C:\\Synthetic\\A\n")]
    public void PartialMalformedAndDuplicateReportsHaveNoCompleteFingerprint(string body)
    {
        var result = ReportInputs.Clusters(Encoding.UTF8.GetBytes(ClusterHeader + body), Fixtures.Policy);
        Assert.False(result.ParsingComplete); Assert.False(result.BytesComplete); Assert.Null(result.Value); Assert.Null(result.Sha256);
    }
    [Fact]
    public void CancellationAndSubstitutedRowsCannotCompleteReconciliation()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var stopped = ReportInputs.Clusters(Encoding.UTF8.GetBytes(ClusterHeader), Fixtures.Policy, cancel.Token);
        Assert.Equal(ParseStatus.Cancelled, stopped.Status); Assert.Null(stopped.Sha256);
        var reports = Fixtures.ReportsForSimple() with { Clusters = [new("RELATED", "RELATED_000001", 2, @"C:\Unrelated")] };
        var result = Reconciliation.Compare(Fixtures.Evidence(), reports, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.Contains(result.Findings, f => f.Code == "REPORT_ROW_BINDING");
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement);
        var unbound = Reconciliation.Compare(Fixtures.Evidence(), Fixtures.ReportsForSimple() with { Bindings = null }, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.True(unbound.LogicalAgreement); Assert.False(unbound.CompletedLogicalScope);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ValidLaterScanIsScopeChangedAndFrozenRequestIsPreserved(bool retainExpected)
    {
        var e = Fixtures.Evidence(); var latest = e.Scans[0] with { Id = 2 };
        e = e with { Scans = retainExpected ? [e.Scans[0], latest] : [latest],
            Charts = e.Charts.Select(c => c with { FirstScan = 2, LastScan = 2 }).ToArray() };
        var validation = Provenance.Validate(e, 1, Fixtures.Policy);
        Assert.Equal(FindingKind.ScopeChanged, validation.PrimaryStop);
        Assert.DoesNotContain(validation.Findings, f => f.Kind == FindingKind.Invalid);
        Assert.Equal(new ScanScopeChange(1, 2, retainExpected), validation.ScopeChange);
        Assert.True(Provenance.Validate(e, 2, Fixtures.Policy).Valid);
        var result = Reconciliation.Compare(e, Fixtures.ReportsForSimple(), 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.Equal(validation.ScopeChange, result.ScopeChange); Assert.False(result.CompletedLogicalScope); Assert.Null(result.Analysis);
        Assert.Equal("INSPECTOR_PARTIAL", result.InspectorStatus); Assert.Equal("INSPECTOR_BLOCKED", result.ScopeChange!.FutureNativeDisposition);
    }
    [Fact]
    public void InvalidHistoryAndMalformedCurrentRowsTakePrecedenceOverScopeChange()
    {
        var e = Fixtures.Evidence();
        e = e with { Scans = [e.Scans[0], e.Scans[0] with { Id = 2 }] };
        var validation = Provenance.Validate(e, 1, Fixtures.Policy);
        Assert.Equal(FindingKind.Invalid, validation.PrimaryStop);
        Assert.Contains(validation.Findings, f => f.Code == "PRESENT_PROVENANCE" && f.Kind == FindingKind.Invalid);
        var duplicate = Provenance.Validate(e with { Scans = [e.Scans[1], e.Scans[1]] }, 1, Fixtures.Policy);
        Assert.Equal(FindingKind.Invalid, duplicate.PrimaryStop);
        var empty = Provenance.Validate(e with { Scans = [] }, 1, Fixtures.Policy);
        Assert.Equal(FindingKind.Invalid, empty.PrimaryStop); Assert.Null(empty.ScopeChange);
    }
    [Theory]
    [InlineData("a", "A")]
    [InlineData("é", "É")]
    [InlineData("a\\nested", "A\\NESTED")]
    public void NonOkSpellingWinsOverOkSpellingInEitherOrder(string lower, string upper)
    {
        var bad = Fixtures.Chart(lower, 0) with { Status = "ERROR", Size = -1, Md5 = null, Sha256 = null };
        var ok = Fixtures.Chart(upper, 1);
        foreach (var charts in new[] { new[] { bad, ok }, new[] { ok, bad } })
            Assert.Equal(new[] { Fixtures.RootPath + "\\" + lower }, Relationships.Analyze(charts, [], Fixtures.Policy).IncompleteFolders);
    }
    [Fact]
    public void MultipleNonOkSpellingsRetainFirstErrorAndDiagnosticRetainsFirstPresent()
    {
        var a = Fixtures.Chart("a", 0); var b = Fixtures.Chart("A", 1); var mixed = Fixtures.Chart("a", 2);
        var badA = a with { Status = "ERROR" }; var badB = b with { Status = "UNSTABLE" };
        Assert.Equal(new[] { a.Parent }, Relationships.Analyze([badA, badB, mixed], [], Fixtures.Policy).IncompleteFolders);
        Assert.Equal(new[] { b.Parent }, Relationships.Analyze([badB, badA, mixed], [], Fixtures.Policy).IncompleteFolders);
        Diagnostic diagnostic = new(1, 1, "WARNING", "DIRECTORY_UNREADABLE", Fixtures.RootPath, "synthetic");
        Assert.Equal(new[] { a.Parent }, Relationships.Analyze([a, b, mixed], [diagnostic], Fixtures.Policy).IncompleteFolders);
        Assert.Equal(new[] { b.Parent }, Relationships.Analyze([b, a, mixed], [diagnostic], Fixtures.Policy).IncompleteFolders);
        Assert.Equal(new[] { a.Parent, Fixtures.RootPath + "\\a\\nested" },
            Relationships.Analyze([a, b, Fixtures.Chart("a\\nested", 3)], [diagnostic], Fixtures.Policy).IncompleteFolders);
    }
}
