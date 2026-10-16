using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseSixGroupTests
{
    private const string Chart = "#WAV01 kick.wav\n#BMP01 ../external.bmp\n#00111:01";
    private sealed class Evidence : IDisposable
    {
        internal Fixture F { get; } = new();
        internal string[] Names { get; }
        internal PhaseTwoPreflightResult Content { get; private set; } = null!;
        internal Evidence(int count = 3, bool differentNames = false, bool nested = false) {
            Names = Enumerable.Range(0, count).Select(i => nested && i == 1 ? "A/Sub" : ((char)('A' + i)).ToString()).ToArray();
            foreach (var (name, i) in Names.Select((n, i) => (n, i))) F.File(name, differentNames ? $"chart-{i}.bms" : "one.bms", Chart);
        }
        internal void Seal(bool incomplete = false) {
            F.Scan(); var p = F.Run();
            using var held = incomplete ? new FileStream(F.Path("A", "kick.wav"), FileMode.Open, FileAccess.Read, FileShare.None) : null;
            Content = new PhaseTwoContentRunner().Run(new(F.Db, p.SessionId, p.ManifestSha256, Path.Combine(F.Base, "content")));
        }
        internal PhaseSixOptions Options() => new(F.Db, Content.SessionId, Content.ManifestSha256,
            (string)F.Rows("SELECT component_key FROM candidate_components WHERE outcome='ADMITTED' ORDER BY component_key").First()[0]!,
            Names.Select(n => PathPolicy.Normalize(F.Path(n))).ToArray());
        internal string PlanPath(string label = "plan") => Path.Combine(F.Base, label, "group_union_plan.json");
        internal PhaseSixGroupEnvelope Run(string label = "plan", PhaseSixGroupLimits? limits = null) => PhaseSixGroupPlanner.Plan(Options(), Path.Combine(F.Base, label), limits);
        public void Dispose() => F.Dispose();
    }
    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string[] Groups(PhaseSixGroupPlan p) => p.Candidates.Select(c => string.Join("+", c.RootIds
        .Select(id => Path.GetFileName(p.Roots.Single(r => r.Id == id).OriginalPath)).Order(StringComparer.Ordinal))).Order(StringComparer.Ordinal).ToArray();
    private static void NoAuthority(PhaseSixGroupPlan p) {
        var s = p.States; Assert.False(s.MainGroupSelected); Assert.False(s.RelocationSafe); Assert.False(s.MergeAuthority); Assert.False(s.RemovalAuthority); Assert.False(s.DependencyClosureProven);
        Assert.Equal("SEMANTIC_EVIDENCE_UNKNOWN", s.SemanticState); Assert.Equal("PACKAGE_BOUNDARY_UNKNOWN", s.PackageBoundaryState);
        Assert.Equal("RELOCATION_NOT_PROVEN", s.RelocationState); Assert.Equal("NOT_CONSTRUCTED", s.ConstructionState);
        Assert.Equal("VIRTUAL_GROUP_PLAN_CHECKED", s.VerificationState); Assert.Equal("NOT_PHYSICALLY_VERIFIED", s.PhysicalVerificationState);
        Assert.Equal("NOT_PUBLISHED", s.PublicationState); Assert.Equal("NOT_RETIRED", s.RetirementState);
        Assert.All(p.ChartObligations, o => Assert.Equal("UNKNOWN_ALL_POTENTIAL_DEPENDENCIES_RETAINED", o.State));
        foreach (var c in p.Candidates) {
            Assert.Equal("MAIN_GROUP_CANDIDATE", c.Role); Assert.Equal(p.Counts.SourceAppearances, c.Conservation.Length);
            foreach (var v in c.Variants) { Assert.Equal("PRESERVE_IN_PLACE", v.PreservationState); Assert.Equal("RELOCATION_NOT_PROVEN", v.RelocationState); }
        }
    }
    [WindowsPhaseTwoTheory]
    [InlineData(3)][InlineData(4)][InlineData(6)]
    public void IdenticalRootsHaveOneFullGroupAndExhaustiveSubsets(int count)
    {
        using var e = new Evidence(count); foreach (var n in e.Names) e.F.File(n, "kick.wav", "shared"); e.Seal();
        var result = e.Run(); var p = result.Plan; NoAuthority(p);
        Assert.Equal(0, result.ExitCode); Assert.Equal("UNIQUE_PHYSICAL_PROPOSAL_NOT_SELECTED", p.States.SelectionState);
        var c = Assert.Single(p.Candidates); Assert.Equal(count, c.RootIds.Length); Assert.Empty(c.Variants); Assert.Empty(p.Conflicts);
        Assert.Equal((1 << count) - 1, p.Subsets.Length);
        Assert.All(p.Subsets, s => Assert.Equal("UNION_PHYSICALLY_COMPATIBLE", s.PhysicalState));
        Assert.Equal(new[] { ".", "kick.wav", "one.bms" }, c.VirtualEntries.Select(t => t.RelativePath));
        Assert.All(c.VirtualEntries, t => Assert.Equal(count, t.Sources.Length));
        Assert.Equal(Sha("shared"), c.VirtualEntries.Single(t => t.RelativePath == "kick.wav").Sha256);
        Assert.Equal(count, p.ChartObligations.Length); Assert.Equal(result.PlanSha256, PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath()).PlanSha256);
    }
    [WindowsPhaseTwoFact]
    public void ComplementaryUnknownFilesDifferentChartNamesAndHardlinkNamesRemain()
    {
        using var e = new Evidence(differentNames: true);
        foreach (var n in e.Names) { e.F.File(n + "/aux", $"{n}.opaque", n); Directory.CreateDirectory(e.F.Path(n, "empty-" + n)); }
        var audio = e.F.File("A", "kick.wav", "kick"); e.F.File("B", "other.wav", "kick");
        var hardlink = e.F.Path("A", "copy.wav"); Assert.True(PhaseSevenNative.CreateHardLink(hardlink, audio, IntPtr.Zero));
        e.Seal(); var p = e.Run().Plan; var c = Assert.Single(p.Candidates); NoAuthority(p);
        Assert.Equal(new[] { ".", "aux", "aux/A.opaque", "aux/B.opaque", "aux/C.opaque", "chart-0.bms", "chart-1.bms", "chart-2.bms", "copy.wav", "empty-A", "empty-B", "empty-C", "kick.wav", "other.wav" }, c.VirtualEntries.Select(t => t.RelativePath));
        Assert.Equal(3, c.VirtualEntries.Count(t => t.Sha256 == Sha(Chart))); Assert.Equal(3, c.VirtualEntries.Count(t => t.Sha256 == Sha("kick")));
        var copies = p.Roots.SelectMany(r => r.Entries).Where(x => x.Sha256 == Sha("kick")).ToArray();
        Assert.Equal(3, copies.Length); Assert.Equal(2, copies.Select(x => x.NativeIdentity).Distinct().Count());
        Assert.All(c.VirtualEntries.Where(t => t.RelativePath.EndsWith(".opaque", StringComparison.Ordinal)), t => Assert.Single(t.Sources));
        PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath());
    }
    [WindowsPhaseTwoTheory]
    [InlineData(3, false, "A+B|C")][InlineData(3, true, "A+B|B+C")]
    [InlineData(4, true, "A+B+D|B+C+D")][InlineData(6, true, "A+B+D+E+F|B+C+D+E+F")]
    public void MaximumByInclusionAlternativesAreNotTransitivelyMerged(int count, bool bridge, string expected)
    {
        using var e = new Evidence(count); e.F.File("A", "kick.wav", "X"); e.F.File("C", "kick.wav", "Y");
        if (!bridge) e.F.File("B", "kick.wav", "X");
        e.F.File("C", "opaque", "complete excluded auxiliary"); Directory.CreateDirectory(e.F.Path("C", "empty"));
        e.Seal(); var result = e.Run(); var p = result.Plan; NoAuthority(p);
        Assert.Equal(expected.Split('|'), Groups(p)); Assert.Equal(2, result.ExitCode); Assert.Equal("HUMAN_SELECTION_REQUIRED", p.States.SelectionState);
        Assert.Equal("UNION_BLOCKED_CONFLICT", p.Subsets.Last().PhysicalState);
        Assert.All(p.Conflicts, c => { Assert.Contains("SAME_RELATIVE_PATH_DIFFERENT_BYTES", c.Types); Assert.Equal(2, c.Sources.Length); });
        Assert.Contains(p.Roots.SelectMany(r => r.Entries), x => x.Sha256 == Sha("X")); Assert.Contains(p.Roots.SelectMany(r => r.Entries), x => x.Sha256 == Sha("Y"));
        foreach (var candidate in p.Candidates) foreach (var variant in candidate.Variants) {
            var root = p.Roots.Single(r => r.Id == variant.OriginalRoot.Id); Assert.True(PhaseSixFormat.Equal(root, variant.OriginalRoot));
            Assert.Equal(root.Entries.Length, candidate.Conservation.Count(c => c.Source.RootId == root.Id));
            if (Path.GetFileName(root.OriginalPath) == "C") Assert.Contains(root.Entries, x => x.RelativePath == "empty");
        }
        PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath());
    }
    [WindowsPhaseTwoTheory]
    [InlineData("kind")][InlineData("case")]
    public void AncestorAndWindowsConflictsDoNotProduceImpossibleGroups(string kind)
    {
        using var e = new Evidence();
        if (kind == "kind") { e.F.File("A", "item", "file"); e.F.File("B/item", "child", "child"); }
        else { e.F.File("A", "kick.wav", "X"); e.F.File("B", "KICK.WAV", "X"); }
        e.Seal(); var p = e.Run().Plan;
        Assert.Equal(new[] { "A+C", "B+C" }, Groups(p));
        Assert.Contains(p.Conflicts, c => c.Types.Contains(kind == "kind" ? "FILE_DIRECTORY_KIND_CONFLICT" : "CASE_COLLISION"));
        Assert.DoesNotContain(p.Candidates, c => c.RootIds.Length == 3); PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath());
    }
    [WindowsPhaseTwoTheory]
    [InlineData("_variantes")][InlineData("_VaRiAnTeS/001/opaque")]
    public void ConditionalReservedLayoutCannotOverwriteExistingNames(string path)
    {
        using var e = new Evidence(); Directory.CreateDirectory(Path.GetDirectoryName(e.F.Path("A", path))!);
        e.F.File("A", path, "reserved original"); e.F.File("A", "kick.wav", "X"); e.F.File("C", "kick.wav", "Y"); e.Seal();
        var p = e.Run().Plan; var a = p.Roots.Single(r => Path.GetFileName(r.OriginalPath) == "A");
        var candidate = p.Candidates.Single(c => c.RootIds.Contains(a.Id));
        Assert.NotEmpty(candidate.VariantLayoutConflicts); Assert.All(candidate.VariantLayoutConflicts, c => Assert.StartsWith("VARIANT_LAYOUT_NAMESPACE_CONFLICT:", c));
        Assert.Contains(candidate.VirtualEntries, t => t.RelativePath == path); NoAuthority(p); PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath());
    }
    [WindowsPhaseTwoFact]
    public void DeterminismReadExclusionAndPhysicalPreservation()
    {
        using var e = new Evidence(); foreach (var n in e.Names) e.F.File(n, "kick.wav", n == "C" ? "Y" : "X"); e.Seal();
        var files = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Concat([e.F.Source, e.F.Db]).ToArray();
        var before = files.ToDictionary(p => p, p => (Bytes: File.ReadAllBytes(p), Facts: PhaseTwoSafety.Inspect(p)));
        var tree = Directory.GetFileSystemEntries(e.F.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var dirs = Directory.GetDirectories(e.F.Root, "*", SearchOption.AllDirectories).Append(e.F.Root).ToDictionary(p => p, PhaseTwoSafety.Inspect);
        var held = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        try {
            var p = e.Run(); var reverse = PhaseSixGroupPlanner.Plan(e.Options() with { RootKeys = e.Options().RootKeys.Reverse().ToArray() }, Path.Combine(e.F.Base, "reverse"));
            Assert.Equal(p.PlanSha256, reverse.PlanSha256); Assert.Equal(File.ReadAllBytes(e.PlanPath()), File.ReadAllBytes(e.PlanPath("reverse")));
            Assert.Equal(p.PlanSha256, PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath()).PlanSha256);
            using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); h.AppendData(Encoding.UTF8.GetBytes("BmsLibraryAudit.Phase6B.GroupVariantPlan.v1\0"));
            h.AppendData(JsonSerializer.SerializeToUtf8Bytes(p.Plan, PhaseSixFormat.Canonical)); Assert.Equal(Convert.ToHexStringLower(h.GetHashAndReset()), p.PlanSha256);
        } finally { foreach (var h in held) h.Dispose(); }
        foreach (var (path, original) in before) { Assert.Equal(original.Bytes, File.ReadAllBytes(path)); Assert.Equal(original.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var (path, original) in dirs) Assert.Equal(original, PhaseTwoSafety.Inspect(path));
        Assert.Equal(tree, Directory.GetFileSystemEntries(e.F.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var db in new[] { e.F.Source, e.F.Db }) foreach (var suffix in new[] { "-wal", "-shm", "-journal" }) Assert.False(File.Exists(db + suffix));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("groups")][InlineData("nonmaximal")][InlineData("target-hash")][InlineData("source")][InlineData("variant")][InlineData("variant-label")]
    [InlineData("variant-state")][InlineData("selected")][InlineData("relocation")][InlineData("removal")][InlineData("conflicts")][InlineData("ledger")]
    [InlineData("subsets")][InlineData("root")][InlineData("counts")][InlineData("chart")][InlineData("profile")][InlineData("budget")]
    public void FreshDigestCannotAdmitManufacturedAlternativesOrAuthority(string kind)
    {
        using var e = new Evidence(); e.F.File("A", "kick.wav", "X"); e.F.File("C", "kick.wav", "Y"); e.Seal(); var p = e.Run().Plan;
        var c = p.Candidates[0]; var t = c.VirtualEntries.ToArray(); var variants = c.Variants.ToArray();
        switch (kind) {
            case "groups": p = p with { Candidates = p.Candidates.Skip(1).ToArray() }; break;
            case "nonmaximal": c = c with { RootIds = [c.RootIds[0]] }; break;
            case "target-hash": t[0] = t[0] with { Sha256 = new string('f', 64) }; c = c with { VirtualEntries = t }; break;
            case "source": t[0] = t[0] with { Sources = [] }; c = c with { VirtualEntries = t }; break;
            case "variant": variants[0] = variants[0] with { OriginalRoot = variants[0].OriginalRoot with { Entries = [] } }; c = c with { Variants = variants }; break;
            case "variant-label": variants[0] = variants[0] with { ConditionalLabel = "_variantes/999" }; c = c with { Variants = variants }; break;
            case "variant-state": variants[0] = variants[0] with { PreservationState = "SAFE_TO_MOVE" }; c = c with { Variants = variants }; break;
            case "selected": p = p with { States = p.States with { MainGroupSelected = true } }; break;
            case "relocation": p = p with { States = p.States with { RelocationSafe = true } }; break;
            case "removal": p = p with { States = p.States with { RemovalAuthority = true } }; break;
            case "conflicts": p = p with { Conflicts = [] }; break;
            case "ledger": c = c with { Conservation = c.Conservation.Skip(1).ToArray() }; break;
            case "subsets": p = p with { Subsets = p.Subsets.Skip(1).ToArray() }; break;
            case "root": p = p with { Roots = p.Roots.Reverse().ToArray() }; break;
            case "counts": p = p with { Counts = p.Counts with { MaximalCandidates = 1 } }; break;
            case "chart": p = p with { ChartObligations = [] }; break;
            case "profile": p = p with { EvidenceProfile = "observation-pair" }; break;
            case "budget": p = p with { Limits = p.Limits with { MaximumSubsets = 1 } }; break;
        }
        if (c != p.Candidates.FirstOrDefault() && kind != "groups") { var candidates = p.Candidates.ToArray(); candidates[0] = c; p = p with { Candidates = candidates }; }
        var forged = new PhaseSixGroupEnvelope(p, PhaseSixGroupFormat.Digest(p)); File.WriteAllText(e.PlanPath(), JsonSerializer.Serialize(forged, PhaseSixFormat.Pretty));
        Assert.ThrowsAny<Exception>(() => PhaseSixGroupVerifier.Verify(e.Options(), e.PlanPath()));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("digest")][InlineData("foreign-root")][InlineData("duplicate")][InlineData("two-roots")][InlineData("seven-roots")][InlineData("component")][InlineData("profile")]
    public void InvalidSelectionsAreRefusedWithoutEvidenceWrites(string kind)
    {
        using var e = new Evidence(); e.Seal(); var o = e.Options();
        o = kind switch {
            "digest" => o with { AcceptedContentManifestSha256 = new string('0', 64) },
            "foreign-root" => o with { RootKeys = [o.RootKeys[0], o.RootKeys[1], PathPolicy.Normalize(e.F.Path("foreign"))] },
            "duplicate" => o with { RootKeys = [o.RootKeys[0], o.RootKeys[1], o.RootKeys[1]] },
            "two-roots" => o with { RootKeys = o.RootKeys.Take(2).ToArray() },
            "seven-roots" => o with { RootKeys = Enumerable.Range(0, 7).Select(i => "root" + i).ToArray() },
            "component" => o with { ComponentKey = new string('f', 64) }, _ => o };
        if (kind == "profile") { var pair = Path.Combine(e.F.Base, "pair.db"); using (var db = ObservationPairDatabase.Open(pair, true, true)) { } o = o with { DatabasePath = pair }; }
        var bytes = File.ReadAllBytes(o.DatabasePath); var facts = PhaseTwoSafety.Inspect(o.DatabasePath);
        Assert.ThrowsAny<Exception>(() => PhaseSixGroupPlanner.Plan(o, Path.Combine(e.F.Base, "refused")));
        Assert.False(Directory.Exists(Path.Combine(e.F.Base, "refused"))); Assert.Equal(bytes, File.ReadAllBytes(o.DatabasePath)); Assert.Equal(facts, PhaseTwoSafety.Inspect(o.DatabasePath));
    }
    [WindowsPhaseTwoFact]
    public void GenuineIncompleteContentAndNestedRootsAreRefused()
    {
        using (var e = new Evidence()) {
            e.F.File("A", "kick.wav", "denied"); e.Seal(incomplete: true); Assert.Equal(1, e.Content.IncompleteContentComponents);
            Assert.Throws<InvalidOperationException>(() => e.Run());
        }
        using (var e = new Evidence(nested: true)) { e.Seal(); Assert.Throws<InvalidOperationException>(() => e.Run()); }
    }
    [WindowsPhaseTwoTheory]
    [InlineData("parent")][InlineData("membership")][InlineData("hash")][InlineData("outcome")]
    public void AdulteratedHistoricalEvidenceIsRejected(string kind)
    {
        using var e = new Evidence(); e.Seal(); var options = e.Options(); var triggers = e.F.Rows("SELECT name,sql FROM sqlite_schema WHERE type='trigger'");
        foreach (var t in triggers) e.F.Sql(e.F.Db, "DROP TRIGGER \"" + t[0] + "\"");
        e.F.Sql(e.F.Db, kind switch {
            "parent" => "PRAGMA foreign_keys=OFF; UPDATE sessions SET parent_preflight_id=999999 WHERE stage='CONTENT'",
            "membership" => "PRAGMA foreign_keys=OFF; DELETE FROM observation_memberships WHERE entry_id=(SELECT min(entry_id) FROM observation_memberships)",
            "hash" => "UPDATE content_hashes SET sha256='" + new string('f',64) + "'", _ => "UPDATE content_component_outcomes SET outcome='INCOMPLETE',reason_code='REQUIRED_CONTENT_UNTRUSTED',trusted_successful_hash_count=trusted_successful_hash_count-1,failed_untrusted_file_count=1,affected_observation_root_count=1,comparison_eligible=0" });
        foreach (var t in triggers) e.F.Sql(e.F.Db, (string)t[1]!);
        var before = File.ReadAllBytes(e.F.Db); Assert.ThrowsAny<Exception>(() => PhaseSixGroupPlanner.Plan(options, Path.Combine(e.F.Base, "refused"))); Assert.Equal(before, File.ReadAllBytes(e.F.Db));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("roots")][InlineData("subsets")][InlineData("entries")][InlineData("virtual")][InlineData("appearances")][InlineData("examined")][InlineData("bytes")][InlineData("conflicts")][InlineData("invalid")]
    public void ReducedCapsNeverHideAlternativesOrPublishPartialPlans(string kind)
    {
        using var e = new Evidence(); e.F.File("A", "kick.wav", "X"); e.F.File("B", "kick.wav", "Z"); e.F.File("C", "kick.wav", "Y"); e.Seal();
        var limits = kind switch {
            "roots" => new PhaseSixGroupLimits(MaximumRoots: 2), "subsets" => new(MaximumSubsets: 1), "entries" => new(MaximumEntries: 1),
            "virtual" => new(MaximumVirtualEntries: 1), "appearances" => new(MaximumPlannedAppearances: 1), "examined" => new(MaximumExaminedEntries: 1),
            "bytes" => new(MaximumReportBytes: 1), "conflicts" => new(MaximumConflicts: 1), _ => new(MaximumSeconds: 0) };
        Assert.ThrowsAny<Exception>(() => e.Run(limits: limits)); Assert.False(File.Exists(e.PlanPath()));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("write")][InlineData("cancel")][InlineData("publish")][InlineData("foreign")]
    public void PublicationFaultsNeverImplyCompletedPlans(string kind)
    {
        using var e = new Evidence(); e.Seal(); using var cancel = new CancellationTokenSource(); var before = File.ReadAllBytes(e.F.Db);
        var output = Path.GetDirectoryName(e.PlanPath())!; var hooks = new PhaseThreePublicationTestHooks {
            AfterFirstWrite = () => { Assert.False(File.Exists(e.PlanPath())); if (kind == "write") throw new IOException("controlled write failure"); if (kind == "cancel") cancel.Cancel(); },
            BeforePublish = () => { if (kind == "publish") throw new IOException("controlled publication failure"); if (kind == "foreign") File.WriteAllText(e.PlanPath(), "foreign original"); } };
        Assert.ThrowsAny<Exception>(() => PhaseSixGroupPlanner.PlanCore(e.Options(), output, null, cancel.Token, hooks));
        if (kind == "foreign") Assert.Equal("foreign original", File.ReadAllText(e.PlanPath())); else Assert.False(File.Exists(e.PlanPath()));
        Assert.Equal(before, File.ReadAllBytes(e.F.Db));
    }
    [WindowsPhaseTwoFact]
    public void ProtectedOutputAndInputAliasesExistingPlansAndEarlyCancellationAreRefused()
    {
        using var e = new Evidence(); var inside = e.F.File("A", "pretend-plan.json", "not a plan"); e.Seal();
        foreach (var output in new[] { e.F.Root, e.F.Path("A", "reports"), e.F.Db, e.F.Base }) Assert.ThrowsAny<Exception>(() => PhaseSixGroupPlanner.Plan(e.Options(), output));
        using (var held = new FileStream(inside, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Contains("protected source", Assert.Throws<InvalidOperationException>(() => PhaseSixGroupVerifier.Verify(e.Options(), inside)).Message);
        e.Run(); var bytes = File.ReadAllBytes(e.PlanPath()); Assert.ThrowsAny<Exception>(() => e.Run()); Assert.Equal(bytes, File.ReadAllBytes(e.PlanPath()));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.Throws<OperationCanceledException>(() => PhaseSixGroupPlanner.Plan(e.Options(), Path.Combine(e.F.Base, "cancelled"), token: cancellation.Token));
        var alias = Path.Combine(e.F.Base, "alias");
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "mklink", "/J", alias, e.F.Root }, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })) { process!.WaitForExit(); Assert.Equal(0, process.ExitCode); }
        try { Assert.ThrowsAny<Exception>(() => PhaseSixGroupPlanner.Plan(e.Options(), Path.Combine(alias, "reports"))); } finally { Directory.Delete(alias); }
    }
}
