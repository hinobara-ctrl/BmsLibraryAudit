using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseSixTests
{
    private sealed class Evidence : IDisposable
    {
        internal Fixture F { get; } = new();
        internal PhaseTwoPreflightResult Content { get; private set; } = null!;
        internal Evidence() => F.Pair(bytes: "#WAV01 kick.wav\n#00111:01");
        internal void Seal() {
            F.Scan(); var p = F.Run(); Content = new PhaseTwoContentRunner().Run(new(F.Db, p.SessionId, p.ManifestSha256, Path.Combine(F.Base, "content")));
        }
        internal PhaseSixOptions Options() => new(F.Db, Content.SessionId, Content.ManifestSha256,
            (string)F.Rows("SELECT component_key FROM candidate_components WHERE component_id=(SELECT component_id FROM component_memberships m JOIN observation_roots r ON r.observation_root_id=m.observation_root_id WHERE r.root_key=$0)", PathPolicy.Normalize(F.Path("A"))).Single()[0]!,
            [PathPolicy.Normalize(F.Path("A")), PathPolicy.Normalize(F.Path("B"))]);
        internal PhaseSixEnvelope Run(string label = "plan", PhaseSixLimits? limits = null) => PhaseSixVirtualUnionPlanner.Plan(Options(), Path.Combine(F.Base, label), limits);
        internal string PlanPath(string label = "plan") => Path.Combine(F.Base, label, "virtual_union_plan.json");
        public void Dispose() => F.Dispose();
    }
    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void NoAuthority(PhaseSixPlan p) {
        Assert.Equal("SEMANTIC_EVIDENCE_UNKNOWN", p.States.SemanticEvidenceState);
        Assert.Equal("PACKAGE_BOUNDARY_UNKNOWN", p.States.PackageBoundaryState); Assert.Equal("RELOCATION_NOT_PROVEN", p.States.RelocationState);
        Assert.Equal("NOT_CONSTRUCTED", p.States.ConstructionState); Assert.Equal("NOT_PUBLISHED", p.States.PublicationState); Assert.Equal("NOT_RETIRED", p.States.RetirementState);
        Assert.False(p.States.MergeAuthority); Assert.False(p.States.RemovalAuthority); Assert.False(p.States.DependencyClosureProven);
    }
    [WindowsPhaseTwoFact]
    public void ExactPathsAndExclusivesKeepAllAppearancesWithoutReadingPayloads()
    {
        using var e = new Evidence();
        e.F.File("A", "kick.wav", "kick"); e.F.File("B", "kick.wav", "kick");
        e.F.File("A", "copy.BMS", "#WAV01 kick.wav\n#00111:01"); e.F.File("B", "copy.BMS", "#WAV01 kick.wav\n#00111:01");
        e.F.File("A", "unknown", "aux A"); e.F.File("B", "readme.txt", "aux B");
        e.F.File("A", "same.wav", "kick"); Directory.CreateDirectory(e.F.Path("B", "empty")); e.Seal();
        var paths = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Concat([e.F.Source, e.F.Db]).ToArray();
        var before = paths.ToDictionary(p => p, p => (Bytes: File.ReadAllBytes(p), Facts: PhaseTwoSafety.Inspect(p)));
        var tree = Directory.GetFileSystemEntries(e.F.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var directoryFacts = Directory.GetDirectories(e.F.Root, "*", SearchOption.AllDirectories).Append(e.F.Root).ToDictionary(p => p, PhaseTwoSafety.Inspect);
        var held = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        PhaseSixEnvelope result;
        try {
            result = e.Run(); var verified = PhaseSixPlanVerifier.Verify(e.Options(), e.PlanPath());
            Assert.Equal(result.PlanSha256, verified.PlanSha256); e.Run("repeat");
        } finally { foreach (var h in held) h.Dispose(); }
        var p = result.Plan; Assert.Equal(0, result.ExitCode); Assert.Equal("UNION_PHYSICALLY_COMPATIBLE", p.States.PhysicalUnionState); NoAuthority(p);
        Assert.Empty(p.Conflicts);
        Assert.Equal(new[] { ".", "copy.BMS", "empty", "kick.wav", "one.BMS", "readme.txt", "same.wav", "unknown" }, p.VirtualEntries.Select(v => v.RelativePath));
        Assert.Equal(4, p.Counts.ChartAppearances); Assert.Equal(4, p.VirtualEntries.Where(v => v.RelativePath.EndsWith(".BMS", StringComparison.Ordinal)).Sum(v => v.Sources.Length));
        Assert.Equal(Sha("kick"), Assert.Single(p.VirtualEntries, v => v.RelativePath == "kick.wav").Sha256);
        Assert.Equal(Sha("kick"), Assert.Single(p.VirtualEntries, v => v.RelativePath == "same.wav").Sha256);
        Assert.Equal(2, Assert.Single(p.VirtualEntries, v => v.RelativePath == "kick.wav").Sources.Length);
        Assert.Single(Assert.Single(p.VirtualEntries, v => v.RelativePath == "same.wav").Sources);
        Assert.Equal(p.Roots.Sum(r => r.Entries.Length), p.Conservation.Length); Assert.All(p.Conservation, c => Assert.Equal("REPRESENTED_VIRTUALLY", c.Disposition));
        Assert.Equal(File.ReadAllBytes(e.PlanPath()), File.ReadAllBytes(e.PlanPath("repeat")));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("BmsLibraryAudit.Phase6A.VirtualUnionPlan.v1\0")); hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(p, PhaseSixFormat.Canonical));
        Assert.Equal(Convert.ToHexStringLower(hash.GetHashAndReset()), result.PlanSha256);
        foreach (var (path, snapshot) in before) { Assert.Equal(snapshot.Bytes, File.ReadAllBytes(path)); Assert.Equal(snapshot.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var (path, facts) in directoryFacts) Assert.Equal(facts, PhaseTwoSafety.Inspect(path));
        Assert.Equal(tree, Directory.GetFileSystemEntries(e.F.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var db in new[] { e.F.Source, e.F.Db }) foreach (var suffix in new[] { "-wal", "-shm", "-journal" }) Assert.False(File.Exists(db + suffix));
        var reverse = PhaseSixVirtualUnionPlanner.Plan(e.Options() with { RootKeys = e.Options().RootKeys.Reverse().ToArray() }, Path.Combine(e.F.Base, "reverse"));
        Assert.Equal(result.PlanSha256, reverse.PlanSha256);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("bytes")][InlineData("kind")][InlineData("case")]
    public void ConflictsKeepEverySourceAndBlockAncestorsWithoutFalseVirtualEntries(string kind)
    {
        using var e = new Evidence();
        if (kind == "kind") { e.F.File("A", "item", "file"); e.F.File("B/item", "child.dat", "child"); }
        else { e.F.File("A", "kick.wav", "A"); e.F.File("B", kind == "case" ? "KICK.WAV" : "kick.wav", kind == "case" ? "A" : "B"); }
        e.Seal(); var result = e.Run(); Assert.Equal(2, result.ExitCode); NoAuthority(result.Plan);
        Assert.Equal("UNION_BLOCKED_CONFLICT", result.Plan.States.PhysicalUnionState);
        Assert.NotEmpty(result.Plan.Conflicts); Assert.Equal(result.Plan.Counts.SourceAppearances, result.Plan.Conservation.Length);
        if (kind == "kind") {
            Assert.DoesNotContain(result.Plan.VirtualEntries, v => v.RelativePath.StartsWith("item", StringComparison.Ordinal));
            Assert.Equal(3, result.Plan.Counts.BlockedAppearances);
        } else { Assert.DoesNotContain(result.Plan.VirtualEntries, v => v.RelativePath.Equals("kick.wav", StringComparison.OrdinalIgnoreCase)); Assert.Equal(2, result.Plan.Counts.BlockedAppearances); }
        Assert.Equal(2, PhaseSixPlanVerifier.Verify(e.Options(), e.PlanPath()).ExitCode);
        Assert.Equal(2, result.Plan.Counts.ChartAppearances); // Identical charts keep independent contexts, including conflicting resources.
    }
    [WindowsPhaseTwoTheory]
    [InlineData("digest")][InlineData("root")][InlineData("component")][InlineData("duplicate")][InlineData("session")][InlineData("subset-profile")]
    public void InvalidAdmissionIsRefusedWithoutChangingEvidence(string kind)
    {
        using var e = new Evidence(); e.Seal(); var o = e.Options();
        if (kind == "digest") o = o with { AcceptedContentManifestSha256 = new string('a', 64) };
        if (kind == "root") o = o with { RootKeys = [o.RootKeys[0], PathPolicy.Normalize(e.F.Path("foreign"))] };
        if (kind == "component") o = o with { ComponentKey = new string('f', 64) };
        if (kind == "duplicate") o = o with { RootKeys = [o.RootKeys[0], o.RootKeys[0]] };
        if (kind == "session") o = o with { ContentSessionId = 1 };
        if (kind == "subset-profile") {
            // A genuine independently created profile, not a renamed legacy DB.
            var pair = Path.Combine(e.F.Base, "pair.db");
            using (var db = ObservationPairDatabase.Open(pair, true, true)) { }
            o = o with { DatabasePath = pair };
        }
        var bytes = File.ReadAllBytes(o.DatabasePath); var facts = PhaseTwoSafety.Inspect(o.DatabasePath);
        Assert.ThrowsAny<Exception>(() => PhaseSixVirtualUnionPlanner.Plan(o, Path.Combine(e.F.Base, "refused")));
        Assert.False(Directory.Exists(Path.Combine(e.F.Base, "refused"))); Assert.Equal(bytes, File.ReadAllBytes(o.DatabasePath)); Assert.Equal(facts, PhaseTwoSafety.Inspect(o.DatabasePath));
    }
    [WindowsPhaseTwoFact]
    public void NestedAdmittedRootsAreNotPlannerPairs()
    {
        using var f = new Fixture(); f.Pair("A", "A/Sub"); f.Scan(); var p = f.Run();
        var c = new PhaseTwoContentRunner().Run(new(f.Db, p.SessionId, p.ManifestSha256, Path.Combine(f.Base, "content")));
        var component = (string)f.Rows("SELECT component_key FROM candidate_components WHERE outcome='ADMITTED'").Single()[0]!;
        Assert.Throws<InvalidOperationException>(() => PhaseSixVirtualUnionPlanner.Plan(new(f.Db, c.SessionId, c.ManifestSha256, component,
            [PathPolicy.Normalize(f.Path("A")), PathPolicy.Normalize(f.Path("A/Sub"))]), Path.Combine(f.Base, "plan")));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("sha")][InlineData("path")][InlineData("source")][InlineData("count")][InlineData("state")][InlineData("authority")]
    [InlineData("root")][InlineData("digest")][InlineData("conservation")][InlineData("limits")][InlineData("unknown-semantic")][InlineData("chart-obligation")]
    public void RecomputedDigestDoesNotAuthenticateTamperedPlan(string kind)
    {
        using var e = new Evidence(); e.Seal(); var envelope = e.Run(); var p = envelope.Plan;
        var targets = p.VirtualEntries.ToArray(); var entry = targets.First(v => v.Kind == "REGULAR_FILE"); var index = Array.IndexOf(targets, entry);
        if (kind == "sha") { targets[index] = entry with { Sha256 = new string('f', 64) }; p = p with { VirtualEntries = targets }; }
        if (kind == "path") { targets[index] = entry with { RelativePath = "other.bms" }; p = p with { VirtualEntries = targets }; }
        if (kind == "source") { targets[index] = entry with { Sources = [entry.Sources[0]] }; p = p with { VirtualEntries = targets }; }
        if (kind == "count") p = p with { Counts = p.Counts with { ChartAppearances = 1 } };
        if (kind == "state") p = p with { States = p.States with { PhysicalUnionState = "UNION_BLOCKED_CONFLICT" } };
        if (kind == "authority") p = p with { States = p.States with { RemovalAuthority = true } };
        if (kind == "unknown-semantic") p = p with { States = p.States with { SemanticEvidenceState = "PROVEN", RelocationState = "SAFE" } };
        if (kind == "root") p = p with { Roots = p.Roots.Reverse().ToArray() };
        if (kind == "conservation") p = p with { Conservation = p.Conservation.Skip(1).ToArray() };
        if (kind == "limits") p = p with { Limits = p.Limits with { MaximumEntries = int.MaxValue } };
        if (kind == "chart-obligation") p = p with { ChartObligations = [] };
        var changed = new PhaseSixEnvelope(p, kind == "digest" ? new string('0', 64) : PhaseSixFormat.Digest(p));
        File.WriteAllText(e.PlanPath(), JsonSerializer.Serialize(changed, PhaseSixFormat.Pretty));
        Assert.ThrowsAny<Exception>(() => PhaseSixPlanVerifier.Verify(e.Options(), e.PlanPath()));
    }
    [WindowsPhaseTwoFact]
    public void ConflictErasureWithFreshDigestIsRefused()
    {
        using var e = new Evidence(); e.F.File("A", "kick.wav", "A"); e.F.File("B", "kick.wav", "B"); e.Seal();
        var envelope = e.Run(); var plan = envelope.Plan with { Conflicts = [], States = new("UNION_PHYSICALLY_COMPATIBLE") };
        File.WriteAllText(e.PlanPath(), JsonSerializer.Serialize(new PhaseSixEnvelope(plan, PhaseSixFormat.Digest(plan)), PhaseSixFormat.Pretty));
        Assert.Throws<InvalidOperationException>(() => PhaseSixPlanVerifier.Verify(e.Options(), e.PlanPath()));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("entries")][InlineData("virtual")][InlineData("conflicts")][InlineData("bytes")][InlineData("invalid")]
    public void BudgetsRefuseRatherThanTruncate(string kind)
    {
        using var e = new Evidence(); e.F.File("A", "kick.wav", "A"); e.F.File("B", "kick.wav", "B");
        e.F.File("A", "bga.png", "A"); e.F.File("B", "bga.png", "B"); e.Seal();
        var limits = kind switch {
            "entries" => new PhaseSixLimits(MaximumEntries: 1), "virtual" => new(MaximumVirtualEntries: 1),
            "conflicts" => new(MaximumConflicts: 1), "bytes" => new(MaximumReportBytes: 1), _ => new(MaximumSeconds: 0) };
        Assert.ThrowsAny<Exception>(() => e.Run("limited", limits)); Assert.False(File.Exists(e.PlanPath("limited")));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("write")][InlineData("cancel")][InlineData("before-publish")][InlineData("foreign-report")][InlineData("foreign-temp")]
    public void PublicationFailuresCannotPublishPartialPlansOrDeleteForeignFiles(string kind)
    {
        using var e = new Evidence(); e.Seal(); using var cancel = new CancellationTokenSource(); var output = Path.Combine(e.F.Base, "failed");
        var final = Path.Combine(output, "virtual_union_plan.json"); var before = File.ReadAllBytes(e.F.Db);
        var hooks = new PhaseThreePublicationTestHooks {
            AfterFirstWrite = () => { Assert.False(File.Exists(final)); if (kind == "write") throw new IOException("synthetic write failure"); if (kind == "cancel") cancel.Cancel(); },
            BeforePublish = () => { if (kind == "before-publish") throw new IOException("synthetic publish failure"); if (kind == "foreign-report") File.WriteAllText(final, "foreign");
                if (kind == "foreign-temp") { var temporary = Directory.GetFiles(output, "*.tmp").Single(); File.Delete(temporary); File.WriteAllText(temporary, "foreign temporary"); } }
        };
        Assert.ThrowsAny<Exception>(() => PhaseSixVirtualUnionPlanner.PlanCore(e.Options(), output, null, cancel.Token, hooks));
        Assert.Equal(before, File.ReadAllBytes(e.F.Db));
        if (kind == "foreign-report") Assert.Equal("foreign", File.ReadAllText(final)); else Assert.False(File.Exists(final));
        if (kind == "foreign-temp") Assert.Equal("foreign temporary", File.ReadAllText(Directory.GetFiles(output, "*.tmp").Single()));
    }
    [WindowsPhaseTwoFact]
    public void UnsafeAndExistingOutputAndCancelledCallsPreserveSources()
    {
        using var e = new Evidence(); e.Seal();
        foreach (var output in new[] { e.F.Root, e.F.Path("A", "reports"), e.F.Base, e.F.Db })
            Assert.ThrowsAny<Exception>(() => PhaseSixVirtualUnionPlanner.Plan(e.Options(), output));
        var existing = Path.Combine(e.F.Base, "existing"); Directory.CreateDirectory(existing); File.WriteAllText(Path.Combine(existing, "virtual_union_plan.json"), "original");
        Assert.Throws<InvalidOperationException>(() => PhaseSixVirtualUnionPlanner.Plan(e.Options(), existing)); Assert.Equal("original", File.ReadAllText(Path.Combine(existing, "virtual_union_plan.json")));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => PhaseSixVirtualUnionPlanner.Plan(e.Options(), Path.Combine(e.F.Base, "cancelled"), token: cancel.Token));
        var alias = Path.Combine(e.F.Base, "alias");
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "mklink", "/J", alias, e.F.Root }, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })) { process!.WaitForExit(); Assert.Equal(0, process.ExitCode); }
        try { Assert.ThrowsAny<Exception>(() => PhaseSixVirtualUnionPlanner.Plan(e.Options(), Path.Combine(alias, "reports"))); } finally { Directory.Delete(alias); }
    }
    [WindowsPhaseTwoFact]
    public void AllegedPlanInsideSourceIsRejectedBeforePayloadRead()
    {
        using var e = new Evidence(); var path = e.F.File("A", "foreign.json", "{\"plan\":\"not evidence\"}"); e.Seal();
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var error = Assert.Throws<InvalidOperationException>(() => PhaseSixPlanVerifier.Verify(e.Options(), path));
        Assert.Contains("protected source", error.Message);
    }
    [WindowsPhaseTwoFact]
    public void HardlinkNamesKeepIndependentPathMemberships()
    {
        using var e = new Evidence(); var original = e.F.File("A", "kick.wav", "kick"); var copy = e.F.Path("A", "copy.wav");
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "mklink", "/H", copy, original }, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })) { process!.WaitForExit(); Assert.Equal(0, process.ExitCode); }
        e.Seal(); var plan = e.Run().Plan; var entries = plan.Roots.SelectMany(r => r.Entries).Where(x => x.Sha256 == Sha("kick")).ToArray();
        Assert.Equal(2, entries.Length); Assert.Equal(entries[0].NativeIdentity, entries[1].NativeIdentity); Assert.NotEqual(entries[0].PhysicalFileId, entries[1].PhysicalFileId);
        Assert.Contains(plan.VirtualEntries, v => v.RelativePath == "copy.wav"); Assert.Contains(plan.VirtualEntries, v => v.RelativePath == "kick.wav");
        PhaseSixPlanVerifier.Verify(e.Options(), e.PlanPath());
    }
    [WindowsPhaseTwoTheory]
    [InlineData("comparison")][InlineData("outcome")][InlineData("parent")][InlineData("source-seal")]
    public void TamperedSealedContentCannotBecomeAPlan(string kind)
    {
        using var e = new Evidence(); e.Seal(); var options = e.Options();
        var triggers = e.F.Rows("SELECT name,sql FROM sqlite_schema WHERE type='trigger'");
        foreach (var trigger in triggers) e.F.Sql(e.F.Db, "DROP TRIGGER \"" + trigger[0] + "\"");
        e.F.Sql(e.F.Db, kind switch {
            "comparison" => "UPDATE comparisons SET tree_exact=0", "outcome" => "UPDATE content_component_outcomes SET outcome='INCOMPLETE',reason_code='REQUIRED_CONTENT_UNTRUSTED',trusted_successful_hash_count=trusted_successful_hash_count-1,failed_untrusted_file_count=1,affected_observation_root_count=1,comparison_eligible=0",
            "parent" => "PRAGMA foreign_keys=OFF; UPDATE sessions SET parent_preflight_id=999999 WHERE stage='CONTENT'",
            _ => "UPDATE source_snapshots SET captured_evidence_digest='" + new string('0',64) + "'" });
        foreach (var trigger in triggers) e.F.Sql(e.F.Db, (string)trigger[1]!);
        var bytes = File.ReadAllBytes(e.F.Db);
        Assert.ThrowsAny<Exception>(() => PhaseSixVirtualUnionPlanner.Plan(options, Path.Combine(e.F.Base, "refused")));
        Assert.Equal(bytes, File.ReadAllBytes(e.F.Db));
    }
}
