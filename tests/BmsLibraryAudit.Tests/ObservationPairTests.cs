using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BmsLibraryAudit.Tests;

public sealed class ObservationPairTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly PhaseTwoPreflightTests.Fixture F = new();
        internal string Db => Path.Combine(F.Base, "pair.db");
        internal string Output(string name) => Path.Combine(F.Base, name);
        internal string? Source, Preflight, Content;
        internal Fixture(bool inverse = false)
        {
            F.Pair(bytes: "chart-one"); F.File("A", "two.bmson", "chart-two"); F.File("B", "two.bmson", "chart-two");
            F.File(inverse ? "A" : "B", "three.bms", "exclusive-three");
        }
        internal ObservationPairResult Capture(bool reverse = false, ObservationPairLimits? limits = null)
        {
            F.Scan(); var r = ObservationPairObserver.Capture(new(F.Source, 1, F.Path(reverse ? "B" : "A"), F.Path(reverse ? "A" : "B"), Db, Output("capture")), limits); Source = r.ManifestSha256; return r;
        }
        internal ObservationPairResult Pf(IPhaseTwoContentFileSystem? files = null, ObservationPairObserver.Hooks? hooks = null, CancellationToken token = default)
        { var r = ObservationPairObserver.PreflightCore(Db, Source!, Output("preflight"), files ?? new PhaseTwoFileSystem(), hooks, token); Preflight = r.ManifestSha256; return r; }
        internal ObservationPairResult Hash(IPhaseTwoContentFileSystem? files = null, ObservationPairObserver.Hooks? hooks = null, CancellationToken token = default)
        { var r = ObservationPairObserver.HashCore(Db, Preflight!, Output("content"), files ?? new PhaseTwoFileSystem(), hooks, token); Content = r.ManifestSha256; return r; }
        internal void All() { Capture(); Assert.Equal("COMPLETE", Pf().State); Assert.Equal("COMPLETE", Hash().State); }
        internal PairContent C() { using var db = ObservationPairDatabase.Open(Db); return PairJson.Parse<PairContent>(db.Read("CONTENT")!.Payload); }
        internal PairPreflight P() { using var db = ObservationPairDatabase.Open(Db); return PairJson.Parse<PairPreflight>(db.Read("PREFLIGHT")!.Payload); }
        public void Dispose() => F.Dispose();
    }
    private sealed class Files : IPhaseTwoContentFileSystem
    {
        private readonly PhaseTwoFileSystem real = new();
        internal readonly List<string> Reads = [];
        internal Func<string, bool>? DenyDirectory;
        internal Func<PhaseTwoEntry, PhaseTwoContentRead?>? Override;
        internal bool Reverse;
        public PhaseTwoEntry Metadata(string path) => real.Metadata(path);
        public IEnumerable<string> Children(string path) => DenyDirectory?.Invoke(path) == true ? throw new UnauthorizedAccessException("Synthetic directory denial") : Reverse ? real.Children(path).Reverse() : real.Children(path);
        public PhaseTwoChartRead ReadChart(string path, CancellationToken token) => throw new InvalidOperationException("Native-bound shared content reader required.");
        public PhaseTwoContentRead ReadContent(PhaseTwoEntry expected, byte[] buffer, CancellationToken token)
        { Reads.Add(expected.OriginalPath); return Override?.Invoke(expected) ?? real.ReadContent(expected, buffer, token); }
    }
    [WindowsPhaseTwoTheory]
    [InlineData(false)] [InlineData(true)]
    public void StrictSubsetDirectionAndPhysicalInclusionHaveIndependentOracles(bool inverse)
    {
        using var f = new Fixture(inverse); f.F.File("A", "same.wav", "known"); f.F.File("B", "same.wav", "known");
        f.Capture(); f.Pf(); f.Hash(); var c = f.C();
        Assert.Equal(inverse, c.Comparison!.TreeSubsetBInA); Assert.Equal(!inverse, c.Comparison.TreeSubsetAInB);
        Assert.False(c.Comparison.Flags[0]); Assert.False(c.Comparison.Flags[1]); Assert.Equal(2, c.Comparison.CommonChartHashes.Length);
        Assert.True(c.Comparison.Flags[2]); Assert.True(c.Comparison.Flags[3]);
        Assert.Equal(inverse ? 3 : 2, c.Comparison.ChartCopiesA); Assert.Equal(inverse ? 2 : 3, c.Comparison.ChartCopiesB);
        var expected = Convert.ToHexStringLower(SHA256.HashData("known"u8));
        Assert.Contains(c.Comparison.Groups, g => g.Digest == expected && g.CountA == 1 && g.CountB == 1);
        Assert.Equal("COMPLETE", ObservationPairObserver.Verify(f.Db, "CONTENT", f.Content!).PairState);
    }
    [WindowsPhaseTwoFact]
    public void RecursiveChartsDuplicatesAndUnknownFilesAreAllFreshlyRead()
    {
        using var f = new Fixture(); f.F.File("A", "copy.bms", "chart-one"); f.F.File("A/nested", "inner.bms", "nested");
        f.F.File("A", "archive.zip", "archive"); f.F.File("B", "opaque.custom", "unknown"); Directory.CreateDirectory(f.F.Path("B", "empty"));
        f.Capture(); var pre = new Files(); f.Pf(pre); var p = f.P();
        Assert.Equal(7, pre.Reads.Count); Assert.DoesNotContain(pre.Reads, p => p.EndsWith(".zip", StringComparison.Ordinal));
        Assert.Single(p.Charts, c => !c.Direct); Assert.Equal(7, p.Charts.Length);
        var all = new Files(); f.Hash(all); Assert.Equal(9, all.Reads.Count); Assert.Equal(9, all.Reads.Distinct().Count());
        Assert.Equal(4, f.C().Comparison!.ChartCopiesA); Assert.Contains(f.C().Comparison!.StructuralStates, s => s == "CONTAINER_DIRECTORY_CANDIDATE");
        Assert.Contains(f.C().Comparison!.Differences, d => d.Path == "empty" && d.Category == "DIRECTORY_ONLY_B");
    }
    [WindowsPhaseTwoTheory]
    [InlineData("content")] [InlineData("kind")] [InlineData("case")] [InlineData("complement")]
    [InlineData("paths")] [InlineData("empty")]
    public void PhysicalConflictsAndExclusivePathsRemainIndependent(string scenario)
    {
        using var f = new Fixture();
        if (scenario == "content") { f.F.File("A", "kick.wav", "aaaa"); f.F.File("B", "kick.wav", "bbbb"); }
        if (scenario == "kind") { f.F.File("A", "kick.wav", "aaaa"); Directory.CreateDirectory(f.F.Path("B", "kick.wav")); }
        if (scenario == "case") { f.F.File("A", "kick.wav", "same"); f.F.File("B", "KICK.WAV", "same"); }
        if (scenario == "complement") { f.F.File("A", "audio.wav", "audio"); f.F.File("B", "image.png", "image"); }
        if (scenario == "paths") { f.F.File("A", "kick.wav", "same"); f.F.File("B/nested", "renamed.wav", "same"); }
        if (scenario == "empty") Directory.CreateDirectory(f.F.Path("A", "empty"));
        f.All(); var c = f.C().Comparison!; Assert.False(c.TreeSubsetAInB); Assert.False(c.TreeSubsetBInA);
        if (scenario == "content") Assert.Contains(c.Conflicts, x => x.Kinds.Contains("SAME_RELATIVE_PATH_DIFFERENT_BYTES"));
        if (scenario == "kind") Assert.Contains(c.Conflicts, x => x.Kinds.Contains("FILE_DIRECTORY_KIND_CONFLICT"));
        if (scenario == "case") Assert.Contains(c.Conflicts, x => x.Kinds.Contains("CASE_COLLISION"));
        if (scenario == "complement") { Assert.Contains(c.Differences, d => d.Path == "audio.wav" && d.Category == "FILE_ONLY_A"); Assert.Contains(c.Differences, d => d.Path == "image.png" && d.Category == "FILE_ONLY_B"); }
        if (scenario == "paths") Assert.Contains(c.Groups, g => g.Projection == "NON_CHART" && g.CountA == 1 && g.CountB == 1 && g.DifferentPaths);
        if (scenario == "empty") Assert.Contains(c.Differences, d => d.Path == "empty" && d.Category == "DIRECTORY_ONLY_A");
    }
    [WindowsPhaseTwoTheory]
    [InlineData("bytes")] [InlineData("new")] [InlineData("missing")] [InlineData("nested")]
    public void PreflightRejectsChangedChartsIncludingExclusiveAndNested(string mode)
    {
        using var f = new Fixture(); if (mode == "nested") f.F.File("B/inside", "hidden.bms", "nested"); f.Capture();
        var path = mode == "nested" ? f.F.Path("B", "inside", "hidden.bms") : f.F.Path("B", "three.bms");
        if (mode == "missing") File.Delete(path);
        else if (mode == "new") f.F.File("A", "new.bms", "new");
        else { var stamp = File.GetLastWriteTimeUtc(path); File.WriteAllBytes(path, Enumerable.Repeat((byte)'X', (int)new FileInfo(path).Length).ToArray()); File.SetLastWriteTimeUtc(path, stamp); }
        Assert.Equal("STALE", f.Pf().State); Assert.Null(f.Preflight); Assert.Null(f.P().Outcomes.Single(r => r.RootId == 1).State == "COMPLETE" ? "bad" : null);
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Hash(f.Db, new string('0', 64), f.Output("forbidden")));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("exact")] [InlineData("overlap")] [InlineData("disjoint")] [InlineData("no-charts")] [InlineData("nested")]
    [InlineData("high")] [InlineData("shared")]
    public void NonSubsetAndOverlappingSelectionsAreBlocked(string mode)
    {
        using var f = new Fixture();
        if (mode == "exact") f.F.File("A", "three.bms", "exclusive-three");
        if (mode == "overlap") f.F.File("A", "different.bms", "unique-A");
        if (mode == "high") {
            foreach (var side in new[] { "A", "B" }) { f.F.File(side, "common-three.bms", "common-three"); f.F.File(side, "common-four.bms", "common-four"); }
            f.F.File("A", "unique.bms", "unique-A");
        }
        if (mode == "shared") { f.F.File("A", "unique.bms", "unique-A"); f.F.File("B", "two.bmson", "different-two"); }
        if (mode == "disjoint") { f.F.File("B", "one.BMS", "different-one"); f.F.File("B", "two.bmson", "different-two"); }
        f.F.Scan(); var b = mode == "no-charts" ? f.F.Path("empty") : mode == "nested" ? f.F.Path("A", "nested") : f.F.Path("B"); Directory.CreateDirectory(b);
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Capture(new(f.F.Source, 1, f.F.Path("A"), b, f.Db, f.Output("capture")))); Assert.False(File.Exists(f.Db));
    }
    [WindowsPhaseTwoFact]
    public void HistoricalRecursiveDiagnosticBlocksButUnrelatedDiagnosticDoesNotErasePair()
    {
        using var bad = new Fixture(); bad.F.Scan();
        InsertDiagnostic(bad.F, bad.F.Path("A", "inner"));
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Capture(new(bad.F.Source, 1, bad.F.Path("A"), bad.F.Path("B"), bad.Db, bad.Output("capture"))));
        using var good = new Fixture(); good.F.Scan(); InsertDiagnostic(good.F, good.F.Path("unrelated"));
        var source = ObservationPairObserver.Capture(new(good.F.Source, 1, good.F.Path("A"), good.F.Path("B"), good.Db, good.Output("capture"))); good.Source = source.ManifestSha256;
        good.Pf(); var result = good.Hash(); Assert.Equal("INCOMPLETE", result.GlobalCoverage); Assert.Equal("COMPLETE", result.PairState);
    }
    private static void InsertDiagnostic(PhaseTwoPreflightTests.Fixture f, string path)
    {
        using var db = AuditDatabase.Open(f.Source, true); db.Execute("INSERT INTO diagnostics(scan_id,severity,code,path,message) VALUES(1,'ERROR','DIRECTORY_UNREADABLE',$path,'synthetic coverage denial')", ("$path", path));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("new")] [InlineData("missing")] [InlineData("identity")] [InlineData("chart-bytes")]
    public void ContentDriftIsStaleAndDoesNotWriteAnIndependentUnit(string mode)
    {
        using var other = new Fixture(); other.All(); var original = File.ReadAllBytes(other.Db);
        using var f = new Fixture(); f.F.File("A", "resource.wav", "audio"); f.Capture(); f.Pf();
        if (mode == "new") f.F.File("A", "new.txt", "new");
        else if (mode == "missing") File.Delete(f.F.Path("A", "resource.wav"));
        else if (mode == "identity") {
            var path = f.F.Path("A", "resource.wav"); var time = File.GetLastWriteTimeUtc(path);
            File.Move(path, f.Output("held-resource")); File.WriteAllText(path, "audio"); File.SetLastWriteTimeUtc(path, time);
        } else { var path = f.F.Path("B", "three.bms"); var stamp = File.GetLastWriteTimeUtc(path); File.WriteAllText(path, new string('x', (int)new FileInfo(path).Length)); File.SetLastWriteTimeUtc(path, stamp); }
        Assert.Equal("STALE", f.Hash().State); Assert.Null(f.Content); Assert.Null(f.C().Comparison);
        Assert.Equal(original, File.ReadAllBytes(other.Db)); Assert.Equal("COMPLETE", ObservationPairObserver.Verify(other.Db, "CONTENT", other.Content!).State);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("ERROR")] [InlineData("UNSTABLE")]
    public void FailedOrShortReadCannotProduceSuccessOrPartialPair(string status)
    {
        using var f = new Fixture(); f.F.File("A", "resource.wav", "payload"); f.Capture(); f.Pf();
        var files = new Files { Override = e => !e.OriginalPath.EndsWith(".wav", StringComparison.Ordinal) ? null :
            new(e, e, null, status == "ERROR" ? 0 : e.Size!.Value - 1, status == "ERROR" ? 1 : 2, status, DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.ToString("O")) };
        Assert.Equal("INCOMPLETE", f.Hash(files).State); var c = f.C(); Assert.Null(c.Comparison); Assert.Null(f.Content);
        Assert.Equal("INCOMPLETE", c.Outcomes[0].State); Assert.Equal("COMPLETE", c.Outcomes[1].State);
        Assert.Null(c.Reads.Single(r => r.Read.Status == status).Read.Sha256);
    }
    [WindowsPhaseTwoFact]
    public void UnreadableDirectoryAndReparseAreNotCompleteOrTraversed()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.F.Path("A", "blocked")); f.Capture();
        Assert.Equal("INCOMPLETE", f.Pf(new Files { DenyDirectory = p => p.EndsWith("blocked", StringComparison.Ordinal) }).State);
        using var g = new Fixture(); g.Capture(); var target = g.Output("external"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "secret.bms"), "external");
        var junction = g.F.Path("A", "link"); Native("/J", junction, target);
        try { var files = new Files(); Assert.Equal("INCOMPLETE", g.Pf(files).State); Assert.DoesNotContain(files.Reads, p => PathPolicy.Contains(target, p)); }
        finally { Directory.Delete(junction); }
    }
    [WindowsPhaseTwoFact]
    public void HardlinkNamesAreSeparateFreshReadsAndMultiplicities()
    {
        using var f = new Fixture(); var path = f.F.File("A", "kick.wav", "hardlink"); Native("/H", f.F.Path("A", "copy.wav"), path); f.Capture(); f.Pf(); var files = new Files(); f.Hash(files);
        Assert.Contains(path, files.Reads); Assert.Contains(f.F.Path("A", "copy.wav"), files.Reads);
        var p = f.P(); Assert.Equal(p.Entries.Single(e => e.RelativePath == "kick.wav").Facts.NativeIdentity, p.Entries.Single(e => e.RelativePath == "copy.wav").Facts.NativeIdentity);
        Assert.Contains(f.C().Comparison!.Groups, g => g.Projection == "FULL" && g.CountA == 2 && g.CountB == 0);
    }
    private static void Native(string kind, string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "/c", "mklink", kind, link, target }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; process.WaitForExit(); Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd());
    }
    [WindowsPhaseTwoTheory]
    [InlineData("preflight")] [InlineData("content")]
    public void CancellationPersistsAuditWithoutManifest(string stage)
    {
        using var f = new Fixture(); f.Capture(); using var token = new CancellationTokenSource();
        if (stage == "content") f.Pf();
        var hooks = new ObservationPairObserver.Hooks { BeforeSeal = token.Cancel };
        Assert.Throws<OperationCanceledException>(() => { if (stage == "preflight") f.Pf(hooks: hooks, token: token.Token); else f.Hash(hooks: hooks, token: token.Token); });
        using var db = ObservationPairDatabase.Open(f.Db); var record = db.Read(stage.ToUpperInvariant())!; Assert.Equal("CANCELLED", record.State); Assert.Null(record.Digest);
    }
    [WindowsPhaseTwoFact]
    public void WrongAcceptedDigestDoesNotReadPayloadAndProfilesMutuallyReject()
    {
        using var f = new Fixture(); f.Capture(); var files = new Files();
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.PreflightCore(f.Db, new string('0', 64), f.Output("bad"), files, null, default)); Assert.Empty(files.Reads);
        Assert.Throws<InvalidOperationException>(() => PhaseTwoDatabase.Open(f.Db, false));
        using var legacy = new PhaseTwoPreflightTests.Fixture(); legacy.Pair(); legacy.Scan(); var old = legacy.Run(); var before = File.ReadAllBytes(legacy.Db);
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Verify(legacy.Db, "PREFLIGHT", old.ManifestSha256));
        using (var db = PhaseTwoDatabase.Open(legacy.Db, false)) Assert.True(PhaseTwoManifest.Preflight(db, 1) == old.ManifestSha256);
        Assert.Equal(before, File.ReadAllBytes(legacy.Db));
        Assert.Throws<InvalidOperationException>(() => PhaseFiveClassifier.Classify(new(f.Db, 1, f.Source!, new string('a', 64), f.Output("forbidden"))));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("source-digest")] [InlineData("preflight-digest")] [InlineData("content-digest")] [InlineData("pair-id")]
    [InlineData("root-id")] [InlineData("membership")] [InlineData("sha")] [InlineData("success")]
    [InlineData("flags")] [InlineData("difference")] [InlineData("group")] [InlineData("terminal")]
    [InlineData("profile")]
    public void TamperedEvidenceIsRejectedEvenWithRecomputedContentSeal(string attack)
    {
        using var f = new Fixture(); f.All(); var accepted = f.Content!;
        var name = attack == "source-digest" ? "SOURCE" : attack == "preflight-digest" || attack is "root-id" or "membership" ? "PREFLIGHT" : attack == "pair-id" ? "SELECTION" : "CONTENT";
        accepted = Mutate(f.Db, name, row => {
            var node = JsonNode.Parse(row.Payload)!;
            if (attack.EndsWith("digest", StringComparison.Ordinal)) return row with { Digest = new string('0', 64) };
            switch (attack) {
                case "pair-id": node["PairId"] = new string('0', 64); break;
                case "root-id": node["Roots"]![0]!["Facts"]!["Identity"] = "forged-native-root"; break;
                case "membership": node["Entries"]![0]!["RootId"] = 2; break;
                case "sha": node["Reads"]![0]!["Read"]!["Sha256"] = new string('a', 64); break;
                case "success": node["Reads"]![0]!["Read"]!["BytesRead"] = 0; break;
                case "flags": node["Comparison"]!["Flags"]![0] = true; break;
                case "difference": node["Comparison"]!["Differences"]![0]!["Category"] = "FILE_ONLY_A"; break;
                case "group": node["Comparison"]!["Groups"]![0]!["CountA"] = 99; break;
                case "terminal": node["State"] = "INCOMPLETE"; return row with { State = "INCOMPLETE", Digest = null, Payload = node.ToJsonString() };
                case "profile": node["UnitId"] = "foreign-profile-unit"; break;
            }
            var payload = node.ToJsonString(); return row with { Payload = payload, Digest = name == "CONTENT" ? ObservationPairDatabase.Seal("CONTENT", payload) : row.Digest };
        }) ?? accepted;
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Verify(f.Db, "CONTENT", attack is "sha" or "success" or "flags" or "difference" or "group" or "profile" ? accepted : f.Content!));
    }
    private static string? Mutate(string path, string name, Func<ObservationPairDatabase.Record, ObservationPairDatabase.Record> mutate)
    {
        ObservationPairDatabase.Record old; using (var db = ObservationPairDatabase.Open(path)) old = db.Read(name)!;
        var changed = mutate(old); using var connection = PhaseTwoDatabase.Connect(path, SqliteOpenMode.ReadWrite);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT sql FROM sqlite_schema WHERE name='records_no_update'"; var trigger = (string)command.ExecuteScalar()!;
        command.CommandText = "DROP TRIGGER records_no_update"; command.ExecuteNonQuery();
        command.CommandText = "UPDATE records SET state=$s,parent_digest=$parent,digest=$d,payload=$p WHERE name=$n"; command.Parameters.AddWithValue("$s", changed.State); command.Parameters.AddWithValue("$parent", (object?)changed.Parent ?? DBNull.Value); command.Parameters.AddWithValue("$d", (object?)changed.Digest ?? DBNull.Value); command.Parameters.AddWithValue("$p", changed.Payload); command.Parameters.AddWithValue("$n", name); command.ExecuteNonQuery();
        command.Parameters.Clear(); command.CommandText = trigger; command.ExecuteNonQuery(); return changed.Digest;
    }
    [WindowsPhaseTwoFact]
    public void ExportIsByteDeterministicWithOriginalOpensDeniedAndPreservesAllInputs()
    {
        using var f = new Fixture(); f.All(); var before = Snapshot(f.F); var evidence = File.ReadAllBytes(f.Db); var native = PhaseTwoSafety.Inspect(f.Db);
        var handles = Directory.GetFiles(f.F.Root, "*", SearchOption.AllDirectories).Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        try {
            ObservationPairObserver.Export(f.Db, "CONTENT", f.Content!, f.Output("repeat"));
            Assert.Equal(File.ReadAllBytes(Path.Combine(f.Output("content"), "observation_pair.json")), File.ReadAllBytes(Path.Combine(f.Output("repeat"), "observation_pair.json")));
            Assert.Equal("COMPLETE", ObservationPairObserver.Verify(f.Db, "CONTENT", f.Content!).State);
        } finally { foreach (var h in handles) h.Dispose(); }
        Assert.Equal(before, Snapshot(f.F)); Assert.Equal(evidence, File.ReadAllBytes(f.Db)); Assert.Equal(native, PhaseTwoSafety.Inspect(f.Db));
        using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Output("repeat"), "observation_pair.json")));
        foreach (var flag in new[] { "DependencyClosureProven", "PackageEquivalenceProven", "MergeAuthority", "RemovalAuthority" }) Assert.False(report.RootElement.GetProperty(flag).GetBoolean());
        Assert.Equal("SEMANTIC_EVIDENCE_NOT_EVALUATED", report.RootElement.GetProperty("Semantics").GetString());
        foreach (var db in new[] { f.F.Source, f.Db }) foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(File.Exists(db + suffix));
    }
    private static string Snapshot(PhaseTwoPreflightTests.Fixture f) => string.Join('\n', Directory.GetFileSystemEntries(f.Root, "*", SearchOption.AllDirectories).Append(f.Root).Append(f.Source).Order(StringComparer.Ordinal).Select(p =>
        PairJson.Text(new { Path = p, Facts = PhaseTwoSafety.Inspect(p), Hash = File.Exists(p) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))) : null })));
    [WindowsPhaseTwoFact]
    public void ReverseArgumentsAndEnumerationHaveStableUnitAndLogicalComparison()
    {
        using var f = new Fixture(); f.Capture();
        var secondDb = f.Output("second.db"); var reverse = ObservationPairObserver.Capture(new(f.F.Source, 1, f.F.Path("B"), f.F.Path("A"), secondDb, f.Output("capture-reverse")));
        var initial = ObservationPairObserver.Verify(f.Db, "SOURCE", f.Source!); Assert.Equal(initial.UnitId, reverse.UnitId); Assert.Equal(initial.ManifestSha256, reverse.ManifestSha256);
        f.Pf(new Files { Reverse = true }); f.Hash(new Files { Reverse = true });
        var pf = ObservationPairObserver.Preflight(secondDb, reverse.ManifestSha256!, f.Output("preflight-reverse"));
        ObservationPairObserver.Hash(secondDb, pf.ManifestSha256!, f.Output("content-reverse"));
        using var db = ObservationPairDatabase.Open(secondDb); var c = PairJson.Parse<PairContent>(db.Read("CONTENT")!.Payload);
        Assert.True(PairJson.Equal(f.C().Comparison, c.Comparison));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("inside")] [InlineData("existing")] [InlineData("junction")] [InlineData("hardlink")]
    public void UnsafeDestinationsAreRejectedWithoutWrites(string mode)
    {
        using var f = new Fixture(); f.All(); var output = mode == "inside" ? f.F.Path("A", "reports") : f.Output("foreign");
        if (mode == "existing") { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "foreign.tmp"), "foreign"); }
        if (mode == "junction") Native("/J", output, f.F.Path("A"));
        if (mode == "hardlink") { Directory.CreateDirectory(output); Native("/H", Path.Combine(output, "foreign.db"), f.Db); }
        try { Assert.ThrowsAny<Exception>(() => ObservationPairObserver.Export(f.Db, "CONTENT", f.Content!, output)); }
        finally { if (mode == "junction") Directory.Delete(output); }
        if (mode == "existing") Assert.Equal("foreign", File.ReadAllText(Path.Combine(output, "foreign.tmp")));
        Assert.False(File.Exists(Path.Combine(output, "observation_pair.json")));
    }
    [WindowsPhaseTwoFact]
    public void BudgetsAndMalformedSuccessNeverYieldACompletePair()
    {
        using var f = new Fixture(); Assert.Throws<ArgumentException>(() => f.Capture(limits: new(Entries: 0)));
        using var g = new Fixture(); g.Capture(limits: new(Entries: 2)); Assert.Equal("INCOMPLETE", g.Pf().State);
        using var h = new Fixture(); h.Capture(); h.Pf(); var files = new Files { Override = e => new(e, e, new string('a', 64), 0, 1, "SUCCESS", DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.ToString("O")) };
        Assert.Equal("FAILED", h.Hash(files).State); Assert.Null(h.C().Comparison);
    }
    [WindowsPhaseTwoFact]
    public void ActualResourceReadDenialPreservesMetadataAndIndependentRoot()
    {
        using var f = new Fixture(); var resource = f.F.File("A", "denied.wav", "known"); f.Capture(); f.Pf();
        var before = PhaseTwoSafety.Inspect(resource);
        using (new FileStream(resource, FileMode.Open, FileAccess.Read, FileShare.None)) Assert.Equal("INCOMPLETE", f.Hash().State);
        var c = f.C(); Assert.Null(c.Comparison); Assert.Equal("ERROR", c.Reads.Single(r => r.Read.Status != "SUCCESS").Read.Status);
        Assert.Equal("COMPLETE", c.Outcomes[1].State); Assert.Equal(before, PhaseTwoSafety.Inspect(resource));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("write")] [InlineData("cancel")] [InlineData("foreign")]
    public void PublicationFailurePreservesSealedContentAndForeignFiles(string mode)
    {
        using var f = new Fixture(); f.Capture(); f.Pf(); using var cancellation = new CancellationTokenSource();
        var foreign = Path.Combine(f.Output("content"), "foreign.tmp");
        var hooks = new ObservationPairObserver.Hooks { Publication = new() { AfterFirstWrite = () => {
            if (mode == "cancel") cancellation.Cancel();
            else { if (mode == "foreign") File.WriteAllText(foreign, "untouched"); throw new IOException("Synthetic publication failure"); }
        } } };
        if (mode == "cancel") Assert.Throws<OperationCanceledException>(() => f.Hash(hooks: hooks, token: cancellation.Token));
        else Assert.Throws<IOException>(() => f.Hash(hooks: hooks));
        using (var db = ObservationPairDatabase.Open(f.Db)) { var row = db.Read("CONTENT")!; Assert.Equal("COMPLETE", row.State); f.Content = row.Digest; }
        Assert.False(File.Exists(Path.Combine(f.Output("content"), "observation_pair.json")));
        if (mode == "foreign") Assert.Equal("untouched", File.ReadAllText(foreign));
        Assert.Equal(mode == "foreign" ? 1 : 0, Directory.GetFiles(f.Output("content")).Length);
        Assert.Equal("COMPLETE", ObservationPairObserver.Export(f.Db, "CONTENT", f.Content!, f.Output("new-report")).State);
    }
    [WindowsPhaseTwoFact]
    public void WrongPreflightDigestAndMixedUnitsAreRejectedBeforeSourceOpens()
    {
        using var a = new Fixture(); a.Capture(); a.Pf(); var files = new Files();
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.HashCore(a.Db, new string('0', 64), a.Output("wrong"), files, null, default)); Assert.Empty(files.Reads);
        using var b = new Fixture(); b.All();
        a.Hash(); ObservationPairDatabase.Record record;
        using (var db = ObservationPairDatabase.Open(b.Db)) record = db.Read("CONTENT")!;
        var digest = Mutate(a.Db, "CONTENT", _ => record);
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Verify(a.Db, "CONTENT", digest!));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("application")] [InlineData("schema")] [InlineData("header")] [InlineData("version")]
    public void CorruptOrForeignProfileIsRejectedUnchanged(string mode)
    {
        using var f = new Fixture(); f.All();
        if (mode == "header") { var bytes = File.ReadAllBytes(f.Db); bytes[18] = 2; bytes[19] = 2; File.WriteAllBytes(f.Db, bytes); }
        else f.F.Sql(f.Db, mode == "application" ? "PRAGMA application_id=111" : mode == "version" ? "PRAGMA user_version=2" : "CREATE TABLE foreign_extension(value TEXT)");
        var before = File.ReadAllBytes(f.Db); Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Verify(f.Db, "CONTENT", f.Content!)); Assert.Equal(before, File.ReadAllBytes(f.Db));
        foreach (var suffix in new[] { "-wal", "-journal", "-shm" }) Assert.False(File.Exists(f.Db + suffix));
    }
    [WindowsPhaseTwoFact]
    public void RowStorageOrderDoesNotChangeSealsOrExportBytes()
    {
        using var f = new Fixture(); f.All(); var before = File.ReadAllBytes(Path.Combine(f.Output("content"), "observation_pair.json"));
        using (var connection = PhaseTwoDatabase.Connect(f.Db, SqliteOpenMode.ReadWrite)) {
            using var command = connection.CreateCommand(); command.CommandText = "SELECT sql FROM sqlite_schema WHERE name='records_no_update'"; var trigger = (string)command.ExecuteScalar()!;
            command.CommandText = "DROP TRIGGER records_no_update; UPDATE records SET rowid=100-rowid; " + trigger; command.ExecuteNonQuery();
        }
        ObservationPairObserver.Export(f.Db, "CONTENT", f.Content!, f.Output("row-order")); Assert.Equal(before, File.ReadAllBytes(Path.Combine(f.Output("row-order"), "observation_pair.json")));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("root")] [InlineData("membership")]
    public void PreflightBindingsAreReconstructedAfterAllDescendantDigestsAreRecomputed(string field)
    {
        using var f = new Fixture(); f.All();
        var parent = Mutate(f.Db, "PREFLIGHT", row => {
            var node = JsonNode.Parse(row.Payload)!;
            if (field == "root") node["Roots"]![0]!["Facts"]!["Identity"] = "forged-root";
            else node["Entries"]![1]!["RootId"] = 2;
            var payload = node.ToJsonString(); return row with { Payload = payload, Digest = ObservationPairDatabase.Seal("PREFLIGHT", payload) };
        });
        var content = Mutate(f.Db, "CONTENT", row => {
            var node = JsonNode.Parse(row.Payload)!; node["PreflightDigest"] = parent; var payload = node.ToJsonString();
            return row with { Parent = parent, Payload = payload, Digest = ObservationPairDatabase.Seal("CONTENT", payload) };
        });
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Verify(f.Db, "CONTENT", content!));
    }
    [WindowsPhaseTwoFact]
    public void TerminalRecordsRejectUpdatesDeletesAndRepeatAcquisition()
    {
        using var f = new Fixture(); f.All(); var before = File.ReadAllBytes(f.Db);
        Assert.Throws<SqliteException>(() => f.F.Sql(f.Db, "UPDATE records SET payload='{}' WHERE name='SOURCE'"));
        Assert.Throws<SqliteException>(() => f.F.Sql(f.Db, "DELETE FROM records WHERE name='CONTENT'"));
        Assert.Throws<SqliteException>(() => f.F.Sql(f.Db, "INSERT OR REPLACE INTO records SELECT * FROM records WHERE name='CONTENT'"));
        Assert.Throws<SqliteException>(() => f.F.Sql(f.Db, "UPDATE metadata SET value='foreign' WHERE key='profile'"));
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Preflight(f.Db, f.Source!, f.Output("repeat-pf")));
        Assert.Throws<InvalidOperationException>(() => ObservationPairObserver.Hash(f.Db, f.Preflight!, f.Output("repeat-hash")));
        Assert.Equal(before, File.ReadAllBytes(f.Db));
    }
    [Fact]
    public void SharedPhysicalAnnotationsKeepRootContextForRepeatedLegacyEntryIds()
    {
        var a = new PhysicalPath(7, 1, "nested/kick.wav", "REGULAR_FILE", new string('a', 64), "same-object", false);
        var b = new PhysicalPath(7, 2, "kick.wav", "REGULAR_FILE", new string('b', 64), "same-object", false);
        var physical = PhaseTwoContentComparison.Derive([new(a.EntryId, a.RelativePath, a.Kind, a.Sha256, false)], [new(b.EntryId, b.RelativePath, b.Kind, b.Sha256, false)]);
        var conflict = Assert.Single(PhysicalConflictAnnotations.Derive([a], [b], physical.Entries, default));
        Assert.Contains("POTENTIAL_NATIVE_ALIAS_CONFLICT", conflict.Kinds); Assert.Equal(1, conflict.RootA); Assert.Equal(2, conflict.RootB);
        Assert.Equal(7, conflict.EntryA); Assert.Equal(7, conflict.EntryB);
    }
}
