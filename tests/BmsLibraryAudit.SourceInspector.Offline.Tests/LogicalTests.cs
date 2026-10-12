using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

public sealed class LogicalTests
{
    [Fact, Trait("OriginalId", "T01")]
    public void ValidLogicalEvidenceRetainsOriginPending()
    {
        var result = Provenance.Validate(Fixtures.Evidence(), 1, Fixtures.Policy);
        Assert.True(result.Valid); Assert.Equal(1, result.Latest!.Id);
        Assert.Equal("DATABASE_AND_NATIVE_ORIGIN_NOT_VERIFIED", Assert.Single(result.Findings).Code);
    }
    [Theory, Trait("OriginalId", "T02-T05")]
    [InlineData(0, 1, "BmsLibraryAudit", "1", "windows-ordinal-ignore-case")]
    [InlineData(0x424D5341, 2, "BmsLibraryAudit", "1", "windows-ordinal-ignore-case")]
    [InlineData(0x424D5341, 1, "Other", "1", "windows-ordinal-ignore-case")]
    [InlineData(0x424D5341, 1, "BmsLibraryAudit", "2", "windows-ordinal-ignore-case")]
    [InlineData(0x424D5341, 1, "BmsLibraryAudit", "1", "ordinal")]
    public void IncorrectSuppliedOwnershipIsInvalid(int application, int version, string tool, string schema, string comparison) =>
        Assert.Contains(Provenance.Validate(Fixtures.Evidence() with { Ownership = new(application, version, tool, schema, comparison) }, 1, Fixtures.Policy).Findings,
            f => f.Code == "DECLARED_OWNERSHIP" && f.Kind == FindingKind.Invalid);

    [Fact, Trait("OriginalId", "T03")]
    public void MissingEvidenceRemainsUnknown() => Assert.Contains(Provenance.Validate(Fixtures.Evidence() with { Ownership = null }, 1, Fixtures.Policy).Findings,
        f => f.Code == "OWNERSHIP_EVIDENCE_MISSING" && f.Kind == FindingKind.Pending);

    [Theory, Trait("OriginalId", "T09")]
    [InlineData("RUNNING", "UNSETTLED_SCAN", FindingKind.Pending)]
    [InlineData("FAILED", "SCAN_STATUS", FindingKind.Invalid)]
    [InlineData("unknown", "SCAN_STATUS", FindingKind.Invalid)]
    public void ScanStatusesAreExplicit(string status, string code, FindingKind kind)
    {
        var e = Fixtures.Evidence();
        var extra = e.Scans[0] with { Id = 2, Status = status, CompletedUtc = null, Statistics = null };
        Assert.Contains(Provenance.Validate(e with { Scans = [e.Scans[0], extra] }, 1, Fixtures.Policy).Findings, f => f.Code == code && f.Kind == kind);
    }
    [Fact, Trait("OriginalId", "T09,T10")]
    public void MissingAndLaterScansCannotBeSubstituted()
    {
        var e = Fixtures.Evidence();
        Assert.Contains(Provenance.Validate(e with { Scans = [] }, 1, Fixtures.Policy).Findings, f => f.Code == "NO_COMPLETED_SCAN");
        Assert.Contains(Provenance.Validate(e with { Scans = [e.Scans[0], e.Scans[0] with { Id = 2 }] }, 1, Fixtures.Policy).Findings, f => f.Code == "EXPECTED_SCAN_CHANGED");
        Assert.Equal(2, Provenance.Validate(e with { Scans = [e.Scans[0] with { Id = 2 }, e.Scans[0]] }, 2, Fixtures.Policy).Latest!.Id);
    }
    [Fact, Trait("OriginalId", "T11")]
    public void RootFlagsConfigurationAndAncestorOverlapAreValidated()
    {
        var e = Fixtures.Evidence();
        Assert.Contains(Provenance.Validate(e with { Roots = [e.Roots[0] with { Active = 2 }] }, 1, Fixtures.Policy).Findings, f => f.Code == "ROOT_BOOLEAN");
        Assert.Contains(Provenance.Validate(e with { Roots = [e.Roots[0] with { Active = 0 }] }, 1, Fixtures.Policy).Findings, f => f.Code == "ROOT_CONFIGURATION");
        var paths = new[] { @"C:\A", @"C:\A-other", @"C:\A\nested" };
        var roots = paths.Select((p, i) => new Root(i + 1, p, p.ToUpperInvariant(), 1)).ToArray();
        var overlap = e with { Charts = [], Roots = roots, Scans = [e.Scans[0] with { Roots = paths, Statistics = new(0, 0, 0, 0, 0, 0, 0, 0) }] };
        Assert.Contains(Provenance.Validate(overlap, 1, Fixtures.Policy).Findings, f => f.Code == "OVERLAPPING_ROOTS");
    }
    [Theory, Trait("OriginalId", "T12")]
    [InlineData("root")][InlineData("parent")][InlineData("name")][InlineData("extension")][InlineData("hash")]
    [InlineData("boolean")][InlineData("scan")][InlineData("status")][InlineData("size")][InlineData("key")]
    public void InvalidChartProvenanceIsRejected(string mutation)
    {
        var e = Fixtures.Evidence(); var c = e.Charts[0];
        c = mutation switch
        {
            "root" => c with { RootId = 99 }, "parent" => c with { Parent = @"C:\Elsewhere" }, "name" => c with { Name = "other.bms" },
            "extension" => c with { Extension = ".wav" }, "hash" => c with { Sha256 = "not-a-hash" }, "boolean" => c with { Present = 2 },
            "scan" => c with { LastScan = 99 }, "status" => c with { Status = "UNKNOWN" }, "size" => c with { Size = -2 }, _ => c with { Key = "wrong" }
        };
        Assert.False(Provenance.Validate(e with { Charts = [c, e.Charts[1]] }, 1, Fixtures.Policy).Valid);
    }
    [Fact, Trait("OriginalId", "T12")]
    public void UnknownErrorMetadataAndOverlappingErrorCountersRemainValid()
    {
        var e = Fixtures.Evidence(); var bad = e.Charts[0] with { Status = "UNSTABLE", Size = -1, Sha256 = null, Md5 = null };
        var result = Provenance.Validate(e with { Charts = [bad, e.Charts[1]], Scans = [e.Scans[0] with { Statistics = new(2, 0, 1, 2, 0, 0, 1, 1) }] }, 1, Fixtures.Policy);
        Assert.True(result.Valid);
        Assert.False(Provenance.Validate(e with { Scans = [e.Scans[0] with { Statistics = new(2, 0, 2, 2, 0, 0, 0, 1) }] }, 1, Fixtures.Policy).Valid);
    }
    [Fact, Trait("OriginalId", "T07,T12")]
    public void SuppliedDiagnosticReferencesAndStatisticsAreChecked()
    {
        var e = Fixtures.Evidence(); var d = new Diagnostic(1, 99, "ERROR", "CHART_READ", e.Charts[0].Path, "synthetic");
        Assert.Contains(Provenance.Validate(e with { Diagnostics = [d] }, 1, Fixtures.Policy).Findings, f => f.Code == "DIAGNOSTIC_REFERENCE");
        Assert.Contains(Provenance.Validate(e with { Scans = [e.Scans[0] with { Statistics = new(long.MaxValue, long.MaxValue, 1, 0, 0, 0, 0, 0) }] }, 1, Fixtures.Policy).Findings,
            f => f.Code == "INTEGER_OVERFLOW");
        Assert.Contains(Provenance.Validate(e with { Scans = [e.Scans[0] with { Statistics = null }] }, 1, Fixtures.Policy).Findings, f => f.Code == "STATISTICS_MISSING");
    }
    [Theory, Trait("OriginalId", "T17,T23")]
    [InlineData(@"relative\file")][InlineData(@"\\server\share")][InlineData(@"\\?\C:\x")]
    [InlineData(@"C:\x\..\y")][InlineData(@"C:\x ")][InlineData(@"C:\x:stream")][InlineData(@"C:\NUL.txt")]
    public void OrdinaryWindowsNamesRejectUnsafeLexicalAliases(string path) => Assert.Throws<EvidenceException>(() => WindowsNames.Key(path));

    [Fact, Trait("OriginalId", "T23")]
    public void WindowsComparisonPreservesUnicodeAndSeparatorBoundaries()
    {
        Assert.Equal(WindowsNames.Key(@"C:\日本\A"), WindowsNames.Key("c:/日本/a"));
        Assert.False(WindowsNames.Comparer.Equals(@"C:\é", "C:\\e\u0301"));
        Assert.False(WindowsNames.Contains(@"C:\A", @"C:\AB\file.bms"));
        Assert.Equal(@"C:\", WindowsNames.Parent(@"C:\file.bms"));
        Assert.Equal(@"C:\日本\" + new string('a', 300), WindowsNames.Canonical(@"C:\日本\" + new string('a', 300)));
    }
    [Fact, Trait("OriginalId", "T01,T34")]
    public void HandCalculatedMultiplicityAndBothGraphClosuresMatch()
    {
        var a = Relationships.Analyze(Fixtures.Mathematics, [], Fixtures.Policy);
        Assert.Equal(3, a.Folders[0].PhysicalFiles); Assert.Equal(2, a.Folders[0].Hashes.Count);
        Assert.Equal(Fixtures.ExpectedPairs, a.Candidates);
        Assert.Equal(new[] { @"C:\Synthetic\A", @"C:\Synthetic\B", @"C:\Synthetic\C", @"C:\Synthetic\D" }, a.Components[0].Members);
        Assert.Equal("RELATED_000001", a.Components[0].Id);
        Assert.Equal(new[] { @"C:\Synthetic\A", @"C:\Synthetic\B", @"C:\Synthetic\C" }, a.Components[1].Members);
        Assert.Equal("EXACT_ONLY_000001", a.Components[1].Id);
        Assert.Equal(new[] { 5, 3 }, a.Duplicates.Select(g => g.Locations.Count));
        var reversed = Relationships.Analyze(Fixtures.Mathematics.Reverse().ToArray(), [], Fixtures.Policy);
        Assert.Equal(a.Candidates, reversed.Candidates);
        Assert.Equal(a.Components.Select(c => (c.Kind, c.Id, string.Join("|", c.Members))), reversed.Components.Select(c => (c.Kind, c.Id, string.Join("|", c.Members))));
    }
    [Theory, Trait("OriginalId", "T01,T34")]
    [InlineData(2, 2, 2, "EXACT_CHART_SET")][InlineData(1, 2, 1, "A_SUBSET_OF_B")][InlineData(2, 1, 1, "B_SUBSET_OF_A")]
    [InlineData(5, 5, 4, "HIGH_OVERLAP")][InlineData(6, 5, 4, "MEDIUM_OVERLAP")][InlineData(2, 2, 1, "MEDIUM_OVERLAP")]
    [InlineData(3, 4, 1, "SHARED_CHARTS")][InlineData(int.MaxValue, int.MaxValue, 1, "SHARED_CHARTS")]
    public void ExactRationalThresholdsHaveIndependentOracles(int a, int b, int common, string expected) => Assert.Equal(expected, Relationships.Classify(a, b, common));

    [Fact, Trait("OriginalId", "T12,T34")]
    public void IncompleteSubtreesRetainDuplicatesButCannotFormRelations()
    {
        var charts = Fixtures.Mathematics.Concat(new[] { Fixtures.Chart("Isolated", 0, 'd') }).ToArray();
        var d = new Diagnostic(1, 1, "ERROR", "CHART_READ", @"C:\Synthetic\A\missing.bms", "synthetic");
        var a = Relationships.Analyze(charts, [d], Fixtures.Policy);
        Assert.Contains(@"C:\Synthetic\A", a.IncompleteFolders);
        Assert.Equal(5, a.Duplicates[0].Locations.Count);
        Assert.DoesNotContain(a.Candidates, c => c.FolderA.EndsWith("\\A", StringComparison.Ordinal));
        Assert.DoesNotContain(a.Components.SelectMany(c => c.Members), p => p.EndsWith("\\Isolated", StringComparison.Ordinal));
        Assert.Empty(Relationships.Analyze(charts, [d with { Code = "DIRECTORY_UNREADABLE", Path = Fixtures.RootPath }], Fixtures.Policy).Candidates);
        Assert.Throws<EvidenceException>(() => Relationships.Analyze(charts, [d with { Code = "UNSUPPORTED" }], Fixtures.Policy));
    }
    [Fact, Trait("OriginalId", "T23,T34")]
    public void CaseEquivalentFoldersChooseOrdinalOriginalSpelling()
    {
        var lower = Fixtures.Chart("a", 0); var upper = Fixtures.Chart("A", 1, 'b');
        var a = Relationships.Analyze([lower, upper, Fixtures.Chart("日本", 0, 'd'), Fixtures.Chart("é", 0, 'e')], [], Fixtures.Policy);
        Assert.Equal(@"C:\Synthetic\A", a.Folders[0].Path); Assert.Equal(2, a.Folders[0].PhysicalFiles);
        Assert.Equal(new[] { @"C:\Synthetic\A", @"C:\Synthetic\é", @"C:\Synthetic\日本" }, a.Folders.Select(f => f.Path));
    }
    [Fact, Trait("OriginalId", "T11,T12")]
    public void HistoricalInactiveRootsRemainInLogicalEvidence()
    {
        var e = Fixtures.Evidence(); var path = @"C:\Historical";
        var old = Fixtures.Chart("Old", 0) with { RootId = 2, Path = path + @"\old.bms", Key = (path + @"\old.bms").ToUpperInvariant(), Parent = path, Name = "old.bms", Status = "INACTIVE", Present = 0 };
        var history = e with { Roots = [e.Roots[0], new(2, path, path.ToUpperInvariant(), 0)],
            Scans = [e.Scans[0] with { Roots = [path], Statistics = new(1, 0, 1, 1, 0, 0, 0, 0) }, e.Scans[0] with { Id = 2 }],
            Charts = [.. e.Charts.Select(c => c with { FirstScan = 2, LastScan = 2 }), old] };
        Assert.True(Provenance.Validate(history, 2, Fixtures.Policy).Valid);
        var bad = history with { Scans = [history.Scans[0] with { Roots = [@"C:\Uncaptured"] }, history.Scans[1]] };
        Assert.Contains(Provenance.Validate(bad, 2, Fixtures.Policy).Findings, f => f.Code == "HISTORICAL_ROOT_REFERENCE");
        Assert.Contains(Provenance.Validate(history with { Charts = [old with { FirstScan = 2 }, .. history.Charts.SkipLast(1)] }, 2, Fixtures.Policy).Findings, f => f.Code is "CHART_SCAN" or "CHART_SCAN_ROOT");
    }
    [Fact, Trait("OriginalId", "T23,T34")]
    public void IncompleteFolderDisplayIsDeterministicUnderCaseVariantOrder()
    {
        var charts = new[] { Fixtures.Chart("a", 0) with { Status = "ERROR", Size = -1, Md5 = null, Sha256 = null }, Fixtures.Chart("A", 1) };
        var first = Relationships.Analyze(charts, [], Fixtures.Policy);
        var second = Relationships.Analyze(charts.Reverse().ToArray(), [], Fixtures.Policy);
        // Corrected historical oracle: first non-OK spelling survives both orders.
        Assert.Equal(new[] { @"C:\Synthetic\a" }, first.IncompleteFolders); Assert.Equal(first.IncompleteFolders, second.IncompleteFolders);
    }
    [Theory, Trait("OriginalId", "T12")]
    [InlineData("boolean")][InlineData("duplicate")][InlineData("key")]
    public void AnalyzerCannotSilentlyFilterInvalidLocalRows(string edit)
    {
        var c = Fixtures.Chart("A", 0);
        IReadOnlyList<Chart> rows = edit switch { "boolean" => [c with { Present = 2 }], "duplicate" => [c, c], _ => [c with { Key = "wrong" }] };
        Assert.Equal(FindingKind.Invalid, Assert.Throws<EvidenceException>(() => Relationships.Analyze(rows, [], Fixtures.Policy)).Kind);
    }
    [Theory, Trait("OriginalId", "T01,T34")]
    [InlineData(5, 5, 4, "HIGH_OVERLAP")][InlineData(6, 5, 4, "MEDIUM_OVERLAP")][InlineData(2, 3, 1, "SHARED_CHARTS")]
    [InlineData(1, 3, 1, "A_SUBSET_OF_B")][InlineData(3, 1, 1, "B_SUBSET_OF_A")][InlineData(2, 2, 1, "MEDIUM_OVERLAP")]
    public void FolderSetFixturesReconstructInclusiveThresholdsAndSubsets(int countA, int countB, int common, string label)
    {
        var charts = new List<Chart>();
        void Add(string folder, int count, int uniqueOffset)
        {
            for (var i = 0; i < count; i++)
            {
                var identity = i < common ? i : uniqueOffset + i;
                charts.Add(Fixtures.Chart(folder, i) with { Sha256 = identity.ToString("x64", System.Globalization.CultureInfo.InvariantCulture), Md5 = identity.ToString("x32", System.Globalization.CultureInfo.InvariantCulture) });
            }
        }
        Add("A", countA, 100); Add("B", countB, 200);
        var pair = Assert.Single(Relationships.Analyze(charts, [], Fixtures.Policy).Candidates);
        Assert.Equal(label, pair.Relationship); Assert.Equal(common, pair.Common);
        Assert.Equal(countA, pair.PhysicalA); Assert.Equal(countB, pair.PhysicalB);
        Assert.Equal((double)common / countA, pair.CoverageA); Assert.Equal((double)common / countB, pair.CoverageB);
        Assert.Equal((double)common / (countA + countB - common), pair.Jaccard);
    }
    [Fact, Trait("OriginalId", "T34")]
    public void RelatedTransitivityDoesNotInventEndpointPairsOrExactEdges()
    {
        var charts = new[] { Fixtures.Chart("A", 0), Fixtures.Chart("B", 0), Fixtures.Chart("B", 1, 'b'), Fixtures.Chart("C", 0, 'b') };
        var result = Relationships.Analyze(charts, [], Fixtures.Policy);
        Assert.Equal(2, result.Candidates.Count);
        Assert.DoesNotContain(result.Candidates, c => c.FolderA == @"C:\Synthetic\A" && c.FolderB == @"C:\Synthetic\C");
        var component = Assert.Single(result.Components); Assert.Equal("RELATED", component.Kind); Assert.Equal(3, component.Members.Count);
    }
}
