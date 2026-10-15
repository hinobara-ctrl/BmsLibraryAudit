using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseFiveTests
{
    private sealed class Evidence : IDisposable
    {
        public Fixture F { get; } = new();
        public PhaseTwoPreflightResult Content { get; private set; } = null!;
        public string Component => (string)F.Rows("SELECT c.component_key FROM candidate_components c JOIN component_memberships m ON m.component_id=c.component_id JOIN observation_roots r ON r.observation_root_id=m.observation_root_id WHERE r.root_key=$0", PathPolicy.Normalize(F.Path("A"))).Single()[0]!;
        private int reports;
        public Evidence(string chart = "#TITLE physical fixture", bool resources = false) {
            if (chart.StartsWith('{')) { F.File("A","one.bmson",chart); F.File("B","one.bmson",chart); }
            else F.Pair(bytes: chart);
            if (resources) { F.File("A", "kick.wav", "known kick"); F.File("B", "kick.wav", "known kick"); }
        }
        public void Seal() { F.Scan(); var p = F.Run(); Content = new PhaseTwoContentRunner().Run(new(F.Db, p.SessionId, p.ManifestSha256, Path.Combine(F.Base, "content"))); }
        public string Report(string root, string name = "one.BMS", PhaseFourLimits? limits = null) {
            var label = "chart-" + reports++; var syntax = Path.Combine(F.Base, label + "-syntax"); var output = Path.Combine(F.Base, label + "-resolved");
            PhaseThreeInspector.Inspect(F.Path(root, name), F.Root, syntax);
            var key = (string)F.Rows("SELECT root_key FROM observation_roots WHERE root_key=$0", PathPolicy.Normalize(F.Path(root))).Single()[0]!;
            PhaseFourResolver.Resolve(new(F.Db, Content.SessionId, Content.ManifestSha256, Path.Combine(syntax,"chart_inspection.json"), key, output), limits);
            return Path.Combine(output,"dependency_resolution.json");
        }
        public PhaseFiveOptions Options(string label = "classified", params string[] reports) => new(F.Db, Content.SessionId, Content.ManifestSha256, Component, Path.Combine(F.Base,label), reports);
        public PhaseFiveResult Run(string label = "classified", PhaseFiveLimits? limits = null, params string[] reports) => PhaseFiveClassifier.Classify(Options(label,reports),limits);
        public void Dispose() => F.Dispose();
    }
    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void NoAuthority(PhaseFiveResult r) {
        Assert.False(r.DependencyClosureProven); Assert.False(r.PackageEquivalenceProven); Assert.False(r.MergeAuthority); Assert.False(r.RemovalAuthority);
        Assert.All(r.Roots, x => Assert.Equal("PACKAGE_BOUNDARY_UNKNOWN",x.PackageBoundary));
        Assert.All(r.Relationships, x => { Assert.Equal("PACKAGE_BOUNDARY_UNKNOWN",x.PackageBoundary); Assert.DoesNotContain("SAFE_UNION_CANDIDATE",x.Labels); Assert.DoesNotContain("REDUNDANT",x.Labels); });
    }
    [WindowsPhaseTwoTheory]
    [InlineData("exact", "OBSERVATION_TREE_EXACT")]
    [InlineData("paths", "CONTENT_MULTISET_EXACT_WITH_PATH_VARIANTS")]
    [InlineData("multiplicity", "OBSERVED_TREE_SUBSET_B_IN_A")]
    [InlineData("bytes", "HARD_PATH_CONTENT_CONFLICT")]
    [InlineData("kinds", "HARD_PATH_KIND_CONFLICT")]
    [InlineData("only-a", "OBSERVED_TREE_SUBSET_B_IN_A")]
    [InlineData("only-b", "OBSERVED_TREE_SUBSET_A_IN_B")]
    [InlineData("complement", "OBSERVED_COMPLEMENTARY_CONTENT")]
    [InlineData("empty-directory", "OBSERVED_TREE_SUBSET_A_IN_B")]
    public void PhysicalDimensionsReuseVerifiedContentWithoutPhaseFour(string kind, string label)
    {
        using var e = new Evidence();
        switch(kind) {
            case "exact": e.F.File("A","kick.wav","x"); e.F.File("B","kick.wav","x"); break;
            case "paths": e.F.File("A","a.wav","x"); e.F.File("B","b.wav","x"); break;
            case "multiplicity": e.F.File("A","a.wav","x"); e.F.File("A","copy.wav","x"); e.F.File("B","a.wav","x"); break;
            case "bytes": e.F.File("A","kick.wav","x"); e.F.File("B","kick.wav","y"); break;
            case "kinds": e.F.File("A","resource","x"); Directory.CreateDirectory(e.F.Path("B","resource")); break;
            case "only-a": e.F.File("A","extra.txt","a"); break;
            case "only-b": e.F.File("B","extra.txt","b"); break;
            case "complement": e.F.File("A","audio.wav","x"); e.F.File("B","image.png","y"); break;
            case "empty-directory": Directory.CreateDirectory(e.F.Path("B","empty")); break;
        }
        e.Seal(); var r = e.Run(); var pair = Assert.Single(r.Relationships);
        Assert.Equal("PROVEN_FROM_ACCEPTED_CONTENT",pair.Trust); Assert.Contains(label,pair.Labels); Assert.Equal("CHART_SET_EXACT",pair.Charts.State);
        Assert.Equal("UNKNOWN",pair.DependencyState); Assert.All(r.Roots,x => Assert.Equal("PROVEN_FROM_ACCEPTED_CONTENT",x.PhysicalEvidenceState)); NoAuthority(r);
        Assert.Equal(e.Content.ManifestSha256,r.Provenance.ContentDigest);
        if (kind == "exact") { Assert.True(pair.ObservedTreeExact); Assert.True(pair.ContentMultisetExact); Assert.Empty(pair.Conflicts); }
        if (kind == "paths") { Assert.False(pair.ObservedTreeExact); Assert.True(pair.ContentMultisetExact); Assert.Contains(pair.ContentGroups,g => g.Sha256 == Sha("x") && g.DifferentPaths); }
        if (kind == "multiplicity") { Assert.False(pair.ContentMultisetExact); Assert.Contains(pair.ContentGroups,g => g.Sha256 == Sha("x") && g.CountA==2 && g.CountB==1); }
        if (kind == "bytes") { var c = Assert.Single(pair.Conflicts); Assert.Equal(Sha("x"),c.EntryA!.Sha256); Assert.Equal(Sha("y"),c.EntryB!.Sha256); }
        if (kind == "empty-directory") Assert.DoesNotContain("CHART_EXACT_WITH_NONCHART_DIFFERENCES",pair.Labels);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("case")][InlineData("complement")][InlineData("kind")][InlineData("case-limit")]
    public void ComplementarityRequiresCompleteConflictFreePathEvidence(string kind)
    {
        using var e = new Evidence();
        if (kind.StartsWith("case", StringComparison.Ordinal)) {
            e.F.File("A", "kick.wav", "same bytes"); e.F.File("B", "KICK.WAV", "same bytes");
        } else {
            e.F.File("A", "audio.wav", "audio"); e.F.File("B", "image.png", "image");
            if (kind == "kind") { e.F.File("A", "resource", "file"); Directory.CreateDirectory(e.F.Path("B", "resource")); }
        }
        e.Seal(); var limits = kind == "case-limit" ? new PhaseFiveLimits(MaximumConflicts: 0) : new PhaseFiveLimits();
        // The classifier can exhaust a positive global conflict budget before a pair.
        var r = e.Run(); var pair = Assert.Single(r.Relationships);
        if (kind == "case-limit") {
            var comparison = PhaseTwoContentComparison.Derive(
                r.Roots[0].Entries.Select(x => new PhaseTwoContentComparison.Node(x.EntryId, x.RelativePath, x.Kind, x.Sha256, x.IsChart)).ToArray(),
                r.Roots[1].Entries.Select(x => new PhaseTwoContentComparison.Node(x.EntryId, x.RelativePath, x.Kind, x.Sha256, x.IsChart)).ToArray(), CancellationToken.None);
            pair = PhaseFiveClassification.Pair(r.Roots[0], r.Roots[1], comparison, limits, CancellationToken.None,
                PhaseFiveClassification.ReferenceIndex(r.Roots[0], CancellationToken.None), PhaseFiveClassification.ReferenceIndex(r.Roots[1], CancellationToken.None));
        }
        Assert.Equal(kind == "complement", pair.Labels.Contains("OBSERVED_COMPLEMENTARY_CONTENT"));
        Assert.False(pair.ObservedTreeExact); Assert.Equal("PROVEN_FROM_ACCEPTED_CONTENT", pair.Trust);
        Assert.Contains(pair.Differences, d => d.Category == "FILE_ONLY_A"); Assert.Contains(pair.Differences, d => d.Category == "FILE_ONLY_B");
        if (kind.StartsWith("case", StringComparison.Ordinal)) {
            Assert.True(pair.ContentMultisetExact);
            Assert.Contains(pair.Differences, d => d.EntryA?.RelativePath == "kick.wav" && d.EntryA.Sha256 == Sha("same bytes"));
            Assert.Contains(pair.Differences, d => d.EntryB?.RelativePath == "KICK.WAV" && d.EntryB.Sha256 == Sha("same bytes"));
            if (kind == "case") Assert.Contains(pair.Conflicts, c => c.Types.Contains("CASE_COLLISION"));
            else Assert.Equal("INCOMPLETE", pair.ConflictCoverage);
        } else if (kind == "kind") Assert.Contains(pair.Conflicts, c => c.Types.Contains("FILE_DIRECTORY_KIND_CONFLICT"));
        else Assert.Empty(pair.Conflicts);
        NoAuthority(r);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("auxiliary")][InlineData("without-phase4")][InlineData("admitted")][InlineData("unreferenced")]
    [InlineData("unknown")][InlineData("unused")][InlineData("impossible")][InlineData("limited")]
    [InlineData("windows")][InlineData("alternatives")][InlineData("duplicate")]
    public void ResourceVariantLabelsRequireAdmittedCompatibleUsage(string kind)
    {
        var chart = kind switch {
            "unknown" => "#WAV01 kick.wav\n#IFDEF PLAYER\n#00111:01\n#ENDIF",
            "unused" => "#WAV01 kick.wav",
            "impossible" => "#SETRANDOM 1\n#IF 2\n#WAV01 kick.wav\n#00111:01\n#ENDIF\n#ENDRANDOM",
            "alternatives" => "#RANDOM 2\n#IF 1\n#WAV01 kick.wav\n#ELSE\n#WAV01 copy.wav\n#ENDIF\n#ENDRANDOM\n#00111:01",
            _ => "#WAV01 kick.wav\n#00111:01"
        };
        using var e = new Evidence(chart);
        e.F.File("A", "kick.wav", "kick A"); e.F.File("B", kind == "windows" ? "KICK.WAV" : "kick.wav", kind is "auxiliary" or "unreferenced" ? "kick A" : "kick B");
        if (kind == "auxiliary") e.F.File("B", "notes.txt", "auxiliary notes");
        if (kind == "unreferenced") { e.F.File("A", "orphan.wav", "orphan A"); e.F.File("B", "orphan.wav", "orphan B"); }
        if (kind == "alternatives") { e.F.File("A", "copy.wav", "copy"); e.F.File("B", "copy.wav", "copy"); }
        e.Seal();
        var reports = kind == "without-phase4" ? [] : new[] { e.Report("A"), e.Report("B", limits: kind == "limited" ? new(MaximumSemanticSymbolicTerms: 1) : null) };
        if (kind == "duplicate") reports = [reports[0], reports[0], reports[1]];
        var r = e.Run(reports: reports); var pair = Assert.Single(r.Relationships);
        Assert.Equal("phase5-conservative-classification-2", r.ResultVersion);
        Assert.Equal("CHART_SET_EXACT", pair.Charts.State); Assert.Equal("PROVEN_FROM_ACCEPTED_CONTENT", pair.Trust);
        Assert.Contains("CHART_EXACT_WITH_NONCHART_DIFFERENCES", pair.Labels);
        Assert.DoesNotContain("CHART_EXACT_RESOURCE_VARIANT", pair.Labels);
        var potential = kind is "admitted" or "windows" or "alternatives";
        Assert.Equal(potential, pair.Labels.Contains("CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT"));
        Assert.False(pair.ObservedTreeExact); NoAuthority(r);
        if (kind == "auxiliary") {
            var extra = Assert.Single(pair.Differences, d => d.RelativePath == "notes.txt");
            Assert.Equal("FILE_ONLY_B", extra.Category); Assert.Equal(Sha("auxiliary notes"), extra.EntryB!.Sha256); Assert.Empty(pair.Conflicts);
        } else if (kind == "windows") Assert.Contains(pair.Conflicts, c => c.Types.Contains("CASE_COLLISION"));
        else {
            var conflict = Assert.Single(pair.Conflicts);
            Assert.Equal(Sha(kind == "unreferenced" ? "orphan A" : "kick A"), conflict.EntryA!.Sha256);
            Assert.Equal(Sha(kind == "unreferenced" ? "orphan B" : "kick B"), conflict.EntryB!.Sha256);
            if (potential) Assert.Equal(2, conflict.References.Length);
            if (kind is "without-phase4" or "unreferenced") Assert.Empty(conflict.References);
        }
        if (kind is "unknown" or "unused" or "impossible" or "limited") {
            Assert.Equal("UNKNOWN", pair.DependencyState);
            Assert.All(r.Roots, root => Assert.NotEmpty(Assert.Single(root.Dependencies).VerifiedReport!.Declarations[0].Candidates));
        }
        if (potential) Assert.All(r.Roots, root => Assert.Contains(Assert.Single(root.Dependencies).VerifiedReport!.Semantics!.Relations,
            relation => relation.Compatibility == "MAY_BIND_WITHIN_MODEL"));
    }
    [WindowsPhaseTwoFact]
    public void RecursiveChartViewsAreStrongerThanCachedDirectChartSets()
    {
        using var e = new Evidence(); e.F.File("A/inner","two.bms","#TITLE A variant"); e.F.File("B/inner","two.bms","#TITLE B variant");
        e.F.File("A","audio.wav","same"); e.F.File("B","audio.wav","same"); e.Seal(); var r=e.Run(); var p=Assert.Single(r.Relationships);
        Assert.Equal("CHART_SET_PARTIAL_OVERLAP",p.Charts.State); Assert.True(p.NonChartContentMultisetExact); Assert.False(p.ContentMultisetExact);
        Assert.Equal(2,p.Charts.PhysicalChartsA); Assert.Single(p.Charts.Common); Assert.Single(p.Charts.OnlyA); Assert.Single(p.Charts.OnlyB);
        Assert.All(r.Roots,x => Assert.Equal("CONTAINER_DIRECTORY_CANDIDATE",x.StructuralState)); NoAuthority(r);
    }
    [WindowsPhaseTwoFact]
    public void ThreeRootsAreNotTransitivelyEquivalent()
    {
        using var e = new Evidence(); e.F.File("C","one.BMS","#TITLE physical fixture");
        e.F.File("A","kick.wav","a"); e.F.File("B","kick.wav","a"); e.F.File("B","extra.txt","b"); e.F.File("C","kick.wav","c");
        e.Seal(); var r=e.Run(); Assert.Equal(3,r.TotalRoots); Assert.Equal(3,r.Relationships.Length);
        Assert.Single(r.Relationships,p => p.Labels.Contains("OBSERVED_TREE_SUBSET_A_IN_B"));
        Assert.Equal(2,r.Relationships.Count(p=>p.Labels.Contains("HARD_PATH_CONTENT_CONFLICT"))); NoAuthority(r);
    }
    [WindowsPhaseTwoFact]
    public void DuplicateChartsNestedMembershipAndHardlinkNamesRemainVisible()
    {
        using var e = new Evidence(); e.F.File("A","copy.BMS","#TITLE physical fixture"); e.F.File("A/inner","one.BMS","#TITLE physical fixture");
        e.F.File("A","a.wav","shared"); var alias=e.F.Path("A","alias.wav");
        using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { ArgumentList={"/c","mklink","/H",alias,e.F.Path("A","a.wav")},UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true })) { process!.WaitForExit(); Assert.Equal(0,process.ExitCode); }
        e.Seal(); var r=e.Run(); var a=r.Roots.Single(x=>x.Key==PathPolicy.Normalize(e.F.Path("A")));
        Assert.Equal(3,a.Entries.Count(x=>x.IsChart)); Assert.Single(a.NestedRootIds); Assert.Equal(a.Entries.Length,a.Entries.Select(x=>x.EntryId).Distinct().Count());
        var aliases=a.Entries.Where(x=>x.RelativePath is "a.wav" or "alias.wav").ToArray(); Assert.Equal(2,aliases.Length);
        Assert.Equal(aliases[0].NativeIdentity,aliases[1].NativeIdentity); Assert.NotEqual(aliases[0].PhysicalFileId,aliases[1].PhysicalFileId);
        Assert.Contains(r.Relationships,p=>p.Labels.Contains("NESTED_OBSERVATION_ROOTS"));
        Assert.Contains(r.Relationships,p=>p.Charts.State=="CHART_SET_EXACT"&&p.Charts.PhysicalChartsA!=p.Charts.PhysicalChartsB); NoAuthority(r);
    }
    [WindowsPhaseTwoFact]
    public void CapturedSubsetAndOverlapDiscoveryNeverReceivesContentConclusions()
    {
        using var e=new Evidence(); e.F.File("C","one.BMS","#TITLE q1"); e.F.File("D","one.BMS","#TITLE q1");
        e.F.File("D","two.BMS","#TITLE q2"); e.F.File("E","one.BMS","#TITLE q2"); e.Seal(); var r=e.Run();
        Assert.NotEmpty(r.PendingDiscoveries); Assert.All(r.PendingDiscoveries,d=> { Assert.Equal("CANDIDATE_FROM_PHASE1",d.Trust); Assert.Equal("PHYSICAL_COMPARISON_NOT_ADMITTED",d.PhysicalComparisonState); });
        Assert.Equal(2,r.Roots.Length); Assert.True(Assert.Single(r.Relationships).ObservedTreeExact); NoAuthority(r);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("#WAV01 kick.wav\n#00111:01", "PROVEN_WITHIN_NAMED_MODEL")]
    [InlineData("#WAV01 KICK.WAV\n#00111:01", "UNKNOWN")]
    [InlineData("#BPM01 150\n#00108:01", "UNKNOWN")]
    [InlineData("#WAV01 kick.wav", "UNKNOWN")]
    [InlineData("#WAV01 missing.wav\n#00111:01", "UNKNOWN")]
    [InlineData("#WAV01 kick.wav\n#WAV01 kick.wav\n#00111:01", "UNKNOWN")]
    [InlineData("#WAV01 kick.wav\n#IFDEF PLAYER\n#00111:01\n#ENDIF", "UNKNOWN")]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 kick.wav\n#ELSE\n#WAV01 copy.wav\n#ENDIF\n#ENDRANDOM\n#00111:01", "PROVEN_WITHIN_NAMED_MODEL")]
    [InlineData("{\"version\":\"1.0.0\",\"sound_channels\":[{\"name\":\"stem\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false}]}]}", "UNKNOWN")]
    public void SemanticAdmissionRetainsAllObligationsWithoutASecondEngine(string chart,string trust)
    {
        using var e=new Evidence(chart,true);
        foreach(var root in new[]{"A","B"}) { e.F.File(root,"copy.wav","known kick"); e.F.File(root,"stem.ogg","opaque ogg"); e.F.File(root,"stem.m4a","opaque m4a"); }
        var name=chart.StartsWith('{') ? "one.bmson" : "one.BMS";
        e.Seal(); var ra=e.Report("A",name); var rb=e.Report("B",name); var r=e.Run(reports:[ra,rb]);
        Assert.All(r.Roots,root=> { Assert.Equal("COMPLETE_CHART_REPORT_ADMISSION",root.SemanticCoverage); Assert.Equal(trust,root.SemanticTrust); var dep=Assert.Single(root.Dependencies); Assert.Equal("ADMITTED_RECONSTRUCTED_PHASE4",dep.Admission); Assert.NotNull(dep.VerifiedReport); Assert.Equal(dep.VerifiedReport!.VerifiedSyntax.Declarations.Length,dep.VerifiedReport.Semantics!.ConservationObligations.Length); });
        Assert.True(Assert.Single(r.Relationships).ObservedTreeExact); NoAuthority(r);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("proof")][InlineData("candidate")][InlineData("condition")][InlineData("chart-bytes")][InlineData("content")]
    [InlineData("root")][InlineData("chart")][InlineData("component")][InlineData("v1-proof")][InlineData("authority")]
    [InlineData("duplicate-key")][InlineData("malformed")][InlineData("null")]
    public void UntrustedReportsCannotPromoteSemanticsOrInvalidateIndependentPhysicalEquality(string kind)
    {
        using var e=new Evidence("#WAV01 kick.wav\n#00111:01",true); e.Seal(); var ra=e.Report("A"); var rb=e.Report("B");
        var node=JsonNode.Parse(System.IO.File.ReadAllText(ra))!;
        switch(kind) {
            case "proof": node["semantics"]!["bounded_proof"]!["scope"]="forged PROVEN scope"; break;
            case "candidate": node["declarations"]![0]!["candidates"]![0]!["sha256"]=new string('a',64); break;
            case "condition": node["semantics"]!["relations"]![0]!["intersection"]!["state"]="IMPOSSIBLE_WITHIN_MODEL"; break;
            case "chart-bytes": node["verified_syntax"]!["original_bytes"]=Convert.ToBase64String(Encoding.UTF8.GetBytes("#TITLE forged")); break;
            case "content": node["provenance"]!["content_digest"]=new string('b',64); break;
            case "root": node["provenance"]!["observation_root_id"]=99999; break;
            case "chart": node["provenance"]!["chart"]!["entry_id"]=99999; break;
            case "component": node["provenance"]!["component_id"]=99999; break;
            case "v1-proof": node["semantics"]!["bounded_proof"]!["version"]="observed-logical-dependency-evidence-1"; break;
            case "authority": node["removal_authority"]=true; break;
        }
        var text=node.ToJsonString(); if(kind=="duplicate-key") text=text.Insert(1,"\"result_version\":\"phase4-dependency-2\",");
        if(kind=="malformed") text="{"; if(kind=="null") text="null"; System.IO.File.WriteAllText(ra,text);
        var r=e.Run(reports:[ra,rb]); Assert.True(Assert.Single(r.Relationships).ObservedTreeExact);
        Assert.Contains(r.Diagnostics,d=>d.StartsWith("UNVERIFIED_PHASE4_REPORT",StringComparison.Ordinal)); Assert.Contains(r.Roots,root=>root.SemanticTrust=="UNKNOWN");
        Assert.DoesNotContain(r.Roots.SelectMany(x=>x.Dependencies),d=>d.VerifiedReport?.Provenance.ObservationRootId==99999); NoAuthority(r);
    }
    [WindowsPhaseTwoFact]
    public void DuplicatesMissingChartsAndLimitedGraphsHaveExplicitSemanticCoverage()
    {
        using var e=new Evidence("#WAV01 kick.wav\n#00111:01",true); e.F.File("A","copy.BMS","#WAV01 kick.wav\n#00111:01"); e.Seal();
        var ra=e.Report("A"); var rb=e.Report("B",limits:new(MaximumSemanticSymbolicTerms:1)); var r=e.Run(reports:[ra,ra,rb]);
        Assert.True(Assert.Single(r.Relationships).ObservedTreeExact is false); Assert.Equal("UNKNOWN",Assert.Single(r.Relationships).DependencyState);
        Assert.Contains(r.Diagnostics,d=>d.StartsWith("DUPLICATE_OR_CONTRADICTORY_PHASE4_REPORTS",StringComparison.Ordinal));
        Assert.Contains(r.Roots.SelectMany(x=>x.Dependencies),d=>d.Admission=="MISSING_PHASE4_REPORT");
        Assert.Contains(r.Roots.SelectMany(x=>x.Dependencies),d=>d.VerifiedReport?.Semantics?.RelationEnumerationCoverage=="INCOMPLETE"); NoAuthority(r);
    }
    [WindowsPhaseTwoFact]
    public void AcceptedReferencesAttributeVariantsWithoutDiscardingEitherResource()
    {
        using var e=new Evidence("#WAV01 kick.wav\n#BMP01 image.png\n#00111:01\n#00104:01",true);
        e.F.File("B","kick.wav","different kick"); e.F.File("A","image.png","a image"); e.F.File("B","image.png","b image"); e.Seal();
        var r=e.Run(reports:[e.Report("A"),e.Report("B")]); var p=Assert.Single(r.Relationships);
        Assert.Contains("CHART_EXACT_WITH_NONCHART_DIFFERENCES",p.Labels);
        Assert.Contains("CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT",p.Labels); Assert.Equal(2,p.Conflicts.Length);
        Assert.All(p.Conflicts,c=> { Assert.Equal(2,c.References.Length); Assert.NotEqual(c.EntryA!.Sha256,c.EntryB!.Sha256); Assert.All(c.References,x=>Assert.Equal(Sha("#WAV01 kick.wav\n#BMP01 image.png\n#00111:01\n#00104:01"),x.ChartSha256)); }); NoAuthority(r);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("roots")][InlineData("pairs")][InlineData("entries")][InlineData("conflicts")]
    [InlineData("reports")][InlineData("input")][InlineData("semantics")][InlineData("discovery")]
    public void BudgetsNeverPromoteTruncatedCoverage(string kind)
    {
        using var e=new Evidence("#WAV01 kick.wav\n#00111:01",true); e.F.File("C","one.BMS","#WAV01 kick.wav\n#00111:01"); e.F.File("C","kick.wav","c");
        e.F.File("A","extra.txt","a"); e.F.File("B","extra.txt","b"); e.Seal(); var reports=new[]{e.Report("A"),e.Report("B"),e.Report("C")};
        var limits=kind switch {
            "roots" => new PhaseFiveLimits(MaximumRoots:1), "pairs"=>new(MaximumPairs:1), "entries"=>new(MaximumEntries:1),
            "conflicts"=>new(MaximumConflicts:1), "reports"=>new(MaximumPhaseFourReports:1), "input"=>new(MaximumInputBytes:1),
            "semantics"=>new(MaximumSemanticRelations:1), _=>new(MaximumDiscoveryCharts:1) };
        var r=e.Run(limits:limits,reports:reports); Assert.Equal("INCOMPLETE",r.Coverage); Assert.Equal(2,r.ExitCode); Assert.NotEmpty(r.Diagnostics); NoAuthority(r);
        if(kind=="conflicts") Assert.Contains(r.Relationships,p=>p.ConflictCoverage=="INCOMPLETE");
        if(kind=="entries") { Assert.Empty(r.Relationships); Assert.All(r.Roots,x=>Assert.Equal("BLOCKED",x.PhysicalEvidenceState)); }
        if(kind=="reports"||kind=="semantics"||kind=="input") Assert.All(r.Relationships,p=>Assert.Equal("PROVEN_FROM_ACCEPTED_CONTENT",p.Trust));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("accepted-digest")][InlineData("parent")][InlineData("parent-seal")][InlineData("content-seal")][InlineData("component")]
    public void InvalidEvidenceIsRefusedWithoutFurtherDatabaseWrites(string kind)
    {
        using var e=new Evidence(); e.Seal(); var options=e.Options();
        if(kind=="accepted-digest") options=options with{AcceptedContentManifestSha256=new string('0',64)};
        else if(kind=="component") options=options with{ComponentKey=new string('f',64)};
        else {
            var triggers=e.F.Rows("SELECT name,sql FROM sqlite_schema WHERE type='trigger'");
            foreach(var t in triggers) e.F.Sql(e.F.Db,"DROP TRIGGER \""+t[0]+"\"");
            e.F.Sql(e.F.Db,kind switch{
                "parent"=>"PRAGMA foreign_keys=OFF; UPDATE sessions SET parent_preflight_id=99999 WHERE stage='CONTENT'",
                "parent-seal"=>"UPDATE sessions SET final_evidence_digest='"+new string('a',64)+"' WHERE stage='PREFLIGHT'",
                _=>"UPDATE sessions SET final_evidence_digest='"+new string('b',64)+"' WHERE stage='CONTENT'"});
            foreach(var t in triggers) e.F.Sql(e.F.Db,(string)t[1]!);
        }
        var bytes=System.IO.File.ReadAllBytes(e.F.Db); var facts=PhaseTwoSafety.Inspect(e.F.Db);
        Assert.ThrowsAny<Exception>(()=>PhaseFiveClassifier.Classify(options)); Assert.False(Directory.Exists(options.OutputDirectory));
        Assert.Equal(bytes,System.IO.File.ReadAllBytes(e.F.Db)); Assert.Equal(facts,PhaseTwoSafety.Inspect(e.F.Db));
        foreach(var suffix in new[]{"-journal","-wal","-shm"}) Assert.False(System.IO.File.Exists(e.F.Db+suffix));
    }
    [WindowsPhaseTwoFact]
    public void AnIncompleteUnrelatedComponentDoesNotInvalidateTheCompleteOne()
    {
        using var e=new Evidence(); e.F.Pair("C","D","#TITLE other component"); e.F.File("C","blocked.wav","blocked");
        e.F.Scan(); var p=e.F.Run(); PhaseTwoPreflightResult content;
        using(var held=new FileStream(e.F.Path("C","blocked.wav"),FileMode.Open,FileAccess.Read,FileShare.None))
            content=new PhaseTwoContentRunner().Run(new(e.F.Db,p.SessionId,p.ManifestSha256,Path.Combine(e.F.Base,"content")));
        Assert.Equal(1,content.IncompleteContentComponents); Assert.Equal(1,content.CompleteContentComponents);
        var good=new PhaseFiveOptions(e.F.Db,content.SessionId,content.ManifestSha256,e.Component,Path.Combine(e.F.Base,"good"));
        Assert.True(Assert.Single(PhaseFiveClassifier.Classify(good).Relationships).ObservedTreeExact);
        var bad=(string)e.F.Rows("SELECT c.component_key FROM candidate_components c JOIN component_memberships m ON m.component_id=c.component_id JOIN observation_roots r ON r.observation_root_id=m.observation_root_id WHERE r.root_key=$0",PathPolicy.Normalize(e.F.Path("C"))).Single()[0]!;
        Assert.Throws<InvalidOperationException>(()=>PhaseFiveClassifier.Classify(good with{ComponentKey=bad,OutputDirectory=Path.Combine(e.F.Base,"bad")}));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("failure")][InlineData("cancel")][InlineData("user-report")][InlineData("foreign-temp")]
    public void InterruptedPublicationPreservesEvidenceAndForeignFiles(string kind)
    {
        using var e=new Evidence(); e.Seal(); using var cancel=new CancellationTokenSource(); var options=e.Options();
        var final=Path.Combine(options.OutputDirectory,"classification_report.json"); var before=System.IO.File.ReadAllBytes(e.F.Db);
        var hooks=new PhaseThreePublicationTestHooks {
            AfterFirstWrite=()=>{Assert.False(System.IO.File.Exists(final)); if(kind=="failure") throw new IOException("synthetic failure"); if(kind=="cancel") cancel.Cancel();},
            BeforePublish=()=>{if(kind=="user-report") System.IO.File.WriteAllText(final,"foreign report"); if(kind=="foreign-temp") {var temp=Directory.GetFiles(options.OutputDirectory,"*.tmp").Single(); System.IO.File.Delete(temp); System.IO.File.WriteAllText(temp,"foreign temporary");}}
        };
        Assert.ThrowsAny<Exception>(()=>PhaseFiveClassifier.ClassifyCore(options,null,cancel.Token,hooks)); Assert.Equal(before,System.IO.File.ReadAllBytes(e.F.Db));
        if(kind=="user-report") Assert.Equal("foreign report",System.IO.File.ReadAllText(final)); else Assert.False(System.IO.File.Exists(final));
        if(kind=="foreign-temp") Assert.Equal("foreign temporary",System.IO.File.ReadAllText(Directory.GetFiles(options.OutputDirectory,"*.tmp").Single()));
        else Assert.Empty(Directory.GetFiles(options.OutputDirectory,"*.tmp"));
    }
    [WindowsPhaseTwoFact]
    public void InputReportsSourceRootsAliasesAndExistingReportsAreProtected()
    {
        using var e=new Evidence("#WAV01 kick.wav\n#00111:01",true); e.Seal(); var report=e.Report("A");
        foreach(var output in new[]{e.F.Root,e.F.Path("A","out"),e.F.Base,Path.GetDirectoryName(report)!,e.F.Db})
            Assert.ThrowsAny<Exception>(()=>PhaseFiveClassifier.Classify(e.Options(reports:[report]) with{OutputDirectory=output}));
        var alias=Path.Combine(e.F.Base,"alias");
        using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { ArgumentList={"/c","mklink","/J",alias,e.F.Root},UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true })) {process!.WaitForExit(); Assert.Equal(0,process.ExitCode);}
        try{Assert.ThrowsAny<Exception>(()=>PhaseFiveClassifier.Classify(e.Options() with{OutputDirectory=Path.Combine(alias,"out")}));}finally{Directory.Delete(alias);}
        var existing=Path.Combine(e.F.Base,"existing"); Directory.CreateDirectory(existing); System.IO.File.WriteAllText(Path.Combine(existing,"classification_report.json"),"original");
        Assert.ThrowsAny<Exception>(()=>PhaseFiveClassifier.Classify(e.Options() with{OutputDirectory=existing})); Assert.Equal("original",System.IO.File.ReadAllText(Path.Combine(existing,"classification_report.json")));
        var skipped=e.Run("source-report",reports:[e.F.Path("A","one.BMS")]); Assert.Contains(skipped.Diagnostics,d=>d.StartsWith("UNVERIFIED_PHASE4_REPORT",StringComparison.Ordinal)); Assert.True(Assert.Single(skipped.Relationships).ObservedTreeExact);
    }
    [WindowsPhaseTwoFact]
    public void RepeatClassificationDeniesSourceOpensAndPreservesEveryNativeFact()
    {
        using var e=new Evidence("#WAV01 kick.wav\n#00111:01",true); e.Seal(); var reports=new[]{e.Report("A"),e.Report("B")};
        var inputs=Directory.GetFiles(e.F.Root,"*",SearchOption.AllDirectories).Concat([e.F.Source,e.F.Db]).Concat(reports)
            .ToDictionary(p=>p,p=>(Hash:SHA256.HashData(System.IO.File.ReadAllBytes(p)),Facts:PhaseTwoSafety.Inspect(p)));
        var held=Directory.GetFiles(e.F.Root,"*",SearchOption.AllDirectories).Select(p=>new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.None)).ToArray();
        PhaseFiveResult r;
        try{r=e.Run(reports:reports); e.Run("repeat",reports:reports);}finally{foreach(var stream in held)stream.Dispose();}
        Assert.Equal(0,r.ExitCode); Assert.Equal("PROVEN_WITHIN_NAMED_MODEL",Assert.Single(r.Relationships).DependencyState); NoAuthority(r);
        Assert.Equal(System.IO.File.ReadAllBytes(Path.Combine(e.F.Base,"classified","classification_report.json")),System.IO.File.ReadAllBytes(Path.Combine(e.F.Base,"repeat","classification_report.json")));
        foreach(var (path,before) in inputs){Assert.Equal(before.Hash,SHA256.HashData(System.IO.File.ReadAllBytes(path)));Assert.Equal(before.Facts,PhaseTwoSafety.Inspect(path));}
        foreach(var db in new[]{e.F.Source,e.F.Db})foreach(var suffix in new[]{"-journal","-wal","-shm"})Assert.False(System.IO.File.Exists(db+suffix));
        Assert.Throws<InvalidOperationException>(()=>e.Run("small-output",new(MaximumOutputBytes:1),reports)); Assert.False(Directory.Exists(Path.Combine(e.F.Base,"small-output")));
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel(); Assert.Throws<OperationCanceledException>(()=>PhaseFiveClassifier.Classify(e.Options("cancelled"),token:cancelled.Token));
    }
    [WindowsPhaseTwoFact]
    public void WindowsCaseVariantsKeepBothPhysicalNamesAndTheirQualifiedBindings()
    {
        using var e=new Evidence("#WAV01 kick.wav\n#00111:01"); e.F.File("A","kick.wav","same"); e.F.File("B","KICK.WAV","same"); e.Seal();
        var r=e.Run(reports:[e.Report("A"),e.Report("B")]);var p=Assert.Single(r.Relationships);
        Assert.True(p.ContentMultisetExact); Assert.False(p.ObservedTreeExact); Assert.Contains(p.Conflicts,c=>c.Types.Contains("CASE_COLLISION"));
        Assert.Contains(r.Roots.SelectMany(x=>x.Dependencies),d=>d.VerifiedReport!.Declarations[0].PathPolicy=="WINDOWS_ORDINAL_IGNORE_CASE");
        Assert.Equal("UNKNOWN",p.DependencyState); NoAuthority(r);
    }
    [WindowsPhaseTwoFact]
    public void AllChartsMustBeAdmittedAndSharedResourcesRetainEveryDeclaration()
    {
        const string chart="#WAV01 kick.wav\n#00111:01";
        using var e=new Evidence(chart,true); e.F.File("A","copy.BMS",chart); e.F.File("B","copy.BMS",chart); e.Seal();
        var reports=new[]{e.Report("A"),e.Report("A","copy.BMS"),e.Report("B"),e.Report("B","copy.BMS")};
        var r=e.Run(reports:reports); Assert.Equal("COMPLETE",r.Coverage); Assert.True(Assert.Single(r.Relationships).ObservedTreeExact);
        Assert.All(r.Roots,root=>{Assert.Equal(2,root.Dependencies.Length); Assert.Equal("PROVEN_WITHIN_NAMED_MODEL",root.SemanticTrust);
            var resources=root.Dependencies.Select(d=>d.VerifiedReport!.Declarations[0].Candidates[0]).ToArray();
            Assert.Equal(resources[0].EntryId,resources[1].EntryId); Assert.Equal(resources[0].NativeIdentity,resources[1].NativeIdentity);}); NoAuthority(r);
        var missing=e.Run("missing-chart",reports:[reports[0],reports[2]]);
        Assert.True(Assert.Single(missing.Relationships).ObservedTreeExact); Assert.All(missing.Roots,root=>Assert.Equal("INCOMPLETE",root.SemanticCoverage));
    }
}
