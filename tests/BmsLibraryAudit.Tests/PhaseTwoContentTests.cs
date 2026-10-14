using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseTwoContentTests
{
    private static PhaseTwoContentOptions Options(Fixture f, PhaseTwoPreflightResult p, string output = "content") =>
        new(f.Db, p.SessionId, p.ManifestSha256, Path.Combine(f.Base, output));
    private static PhaseTwoPreflightResult Hash(Fixture f, PhaseTwoPreflightResult p, IPhaseTwoContentFileSystem? files = null, PhaseTwoContentTestHooks? hooks = null) =>
        new PhaseTwoContentRunner(files, hooks).Run(Options(f, p));

    [WindowsPhaseTwoTheory]
    [InlineData("identical", true, true, true, true, false, 0, 0, 0)]
    [InlineData("conflict", false, false, false, false, true, 0, 0, 0)]
    [InlineData("same-stamps-conflict", false, false, false, false, true, 0, 0, 0)]
    [InlineData("different-names", false, true, false, true, false, 1, 1, 0)]
    [InlineData("multiplicity", false, false, false, false, false, 1, 0, 0)]
    [InlineData("empty-directory", false, true, false, true, false, 0, 0, 0)]
    [InlineData("exclusive", false, false, false, false, false, 1, 0, 0)]
    [InlineData("kind-conflict", false, false, false, false, true, 1, 0, 1)]
    public void IndependentContentExamplesHaveExactDimensions(string scenario, bool tree, bool multiset, bool nonChartTree,
        bool nonChartMultiset, bool conflict, long onlyA, long onlyB, long kindConflicts)
    {
        using var f = new Fixture(); f.Pair();
        switch (scenario)
        {
            case "identical": f.File("A", "01.wav", "aaaa"); f.File("B", "01.wav", "aaaa"); break;
            case "conflict":
            case "same-stamps-conflict":
                var a = f.File("A", "01.wav", "aaaa"); var b = f.File("B", "01.wav", "bbbb");
                if (scenario == "same-stamps-conflict") { var time = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc); File.SetLastWriteTimeUtc(a, time); File.SetLastWriteTimeUtc(b, time); }
                break;
            case "different-names": f.File("A", "a.wav", "aaaa"); f.File("B", "b.wav", "aaaa"); break;
            case "multiplicity": f.File("A", "kick.wav", "aaaa"); f.File("A", "001.wav", "aaaa"); f.File("B", "kick.wav", "aaaa"); break;
            case "empty-directory": Directory.CreateDirectory(f.Path("A", "empty")); break;
            case "exclusive": f.File("A", "only.txt", "aaaa"); break;
            case "kind-conflict": f.File("A", "collision", "aaaa"); Directory.CreateDirectory(f.Path("B", "collision")); break;
        }
        f.Scan(); var p = f.Run(); var before = Snapshot(f); var files = new CountedFiles(); var result = Hash(f, p, files);
        Assert.Equal("COMPLETED", result.State); Assert.Equal(0, result.ExitCode); Assert.Equal(1, result.CompleteContentComponents);
        var q = f.Rows("SELECT tree_exact,content_multiset_exact,non_chart_tree_exact,non_chart_multiset_exact,path_conflict,fileset_variant,files_only_a,files_only_b,kind_conflicts FROM comparisons").Single();
        Assert.Equal(new[] { tree, multiset, nonChartTree, nonChartMultiset, conflict, !tree }, q.Take(6).Select(x => Convert.ToInt64(x) != 0));
        Assert.Equal(onlyA, Convert.ToInt64(q[6])); Assert.Equal(onlyB, Convert.ToInt64(q[7])); Assert.Equal(kindConflicts, Convert.ToInt64(q[8]));
        Assert.Equal(files.Reads.Count, files.Reads.Distinct(PathPolicy.Comparer).Count());
        Assert.Equal(f.Number("SELECT count(*) FROM physical_files"), files.Reads.Count);
        Assert.Equal(p.ManifestSha256, (string)f.Rows("SELECT final_evidence_digest FROM sessions WHERE stage='PREFLIGHT'").Single()[0]!);
        if (scenario == "different-names") Assert.Equal(1, f.Number("SELECT count(*) FROM comparison_content_groups WHERE projection='NON_CHART' AND count_a=1 AND count_b=1 AND differing_path_sets=1"));
        if (scenario == "multiplicity") Assert.Equal(1, f.Number("SELECT count(*) FROM comparison_content_groups WHERE projection='NON_CHART' AND count_a=2 AND count_b=1"));
        AssertPreserved(f, before);
    }

    [WindowsPhaseTwoFact]
    public void MixedOpaqueFilesAndMultipleChartsAreAllFreshlyHashed()
    {
        using var f = new Fixture(); f.Pair();
        var names = new[] { "two.BMSON", "01.wav", "02.ogg", "03.flac", "04.mp3", "video.mp4", "image.png", "banner.jpg", "readme.txt", "pack.zip", "pack.rar", "opaque.xyz", "extensionless", "日本語,音.wav" };
        foreach (var name in names) { f.File("A", name, "opaque-" + name); f.File("B", name, "opaque-" + name); }
        f.Scan(); var p = f.Run(); var before = Snapshot(f); var files = new CountedFiles(); var result = Hash(f, p, files);
        Assert.Equal(30, files.Reads.Count); Assert.Equal(30, f.Number("SELECT count(*) FROM content_hashes WHERE status='SUCCESS'"));
        foreach (var row in f.Rows("SELECT e.original_path,h.sha256,h.bytes_read,h.verified_size FROM content_hashes h JOIN physical_files p ON p.preflight_id=h.preflight_id AND p.physical_file_id=h.physical_file_id JOIN preflight_entries e ON e.preflight_id=p.preflight_id AND e.entry_id=p.entry_id"))
        { var bytes = File.ReadAllBytes((string)row[0]!); Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), row[1]); Assert.Equal(bytes.LongLength, Convert.ToInt64(row[2])); Assert.Equal(bytes.LongLength, Convert.ToInt64(row[3])); }
        Assert.Equal(1, f.Number("SELECT count(*) FROM comparisons WHERE tree_exact=1 AND non_chart_tree_exact=1"));
        Assert.Equal(1, result.CompleteContentComponents); AssertPreserved(f, before);
    }

    [WindowsPhaseTwoTheory]
    [InlineData("resource", "COMPLETED")]
    [InlineData("chart", "STALE")]
    public void PreservedMetadataEditsAreFreshContentNotCacheProof(string kind, string state)
    {
        using var f = new Fixture(); f.Pair(); f.File("A", "01.wav", "aaaa"); f.File("B", "01.wav", "aaaa"); f.Scan(); var p = f.Run();
        var path = f.Path("A", kind == "chart" ? "one.BMS" : "01.wav"); var time = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "yyyy", new UTF8Encoding(false)); File.SetLastWriteTimeUtc(path, time); var before = Snapshot(f);
        var result = Hash(f, p); Assert.Equal(state, result.State);
        if (kind == "chart") { Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons")); Assert.Equal(1, f.Number("SELECT count(*) FROM diagnostics WHERE code='STALE_PHASE_1_CANDIDATE'")); Assert.False(File.Exists(Path.Combine(f.Base, "content", "folder_comparisons.csv"))); }
        else Assert.Equal(1, f.Number("SELECT count(*) FROM comparisons WHERE same_path_different_bytes=1 AND path_conflict=1"));
        AssertPreserved(f, before);
    }

    [WindowsPhaseTwoTheory]
    [InlineData("add")] [InlineData("remove")] [InlineData("rename")] [InlineData("size")] [InlineData("mtime")]
    [InlineData("attributes")] [InlineData("directory-attributes")] [InlineData("directory-add")] [InlineData("directory-remove")] [InlineData("directory-rename")]
    public void ChangedAcceptedInventoryIsGloballyStaleBeforePayload(string change)
    {
        using var f = new Fixture(); f.Pair(); var path = f.File("A", "01.wav", "aaaa"); Directory.CreateDirectory(f.Path("A", "empty")); f.Scan(); var p = f.Run();
        switch (change)
        {
            case "add": f.File("A", "new", "aaaa"); break;
            case "remove": File.Delete(path); break;
            case "rename": File.Move(path, f.Path("A", "renamed.wav")); break;
            case "size": File.WriteAllText(path, "longer", new UTF8Encoding(false)); break;
            case "mtime": File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2)); break;
            case "attributes": File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden); break;
            case "directory-attributes": File.SetAttributes(f.Path("A", "empty"), File.GetAttributes(f.Path("A", "empty")) | FileAttributes.Hidden); break;
            case "directory-add": Directory.CreateDirectory(f.Path("B", "new")); break;
            case "directory-remove": Directory.Delete(f.Path("A", "empty")); break;
            case "directory-rename": Directory.Move(f.Path("A", "empty"), f.Path("A", "renamed")); break;
        }
        var before = Snapshot(f); var files = new CountedFiles(); var result = Hash(f, p, files);
        Assert.Equal("STALE", result.State); Assert.Equal(2, result.ExitCode); Assert.Empty(files.Reads); Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons"));
        Assert.False(File.Exists(Path.Combine(f.Base, "content", "content_groups.csv"))); AssertPreserved(f, before);
    }

    [WindowsPhaseTwoFact]
    public void MutationAfterPrevalidationOrBeforeFinalVerificationCannotComplete()
    {
        foreach (var early in new[] { true, false })
        {
            using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); Action change = () => f.File("A", "unexpected", "aaaa");
            var hooks = early ? new PhaseTwoContentTestHooks { AfterPrevalidation = change } : new PhaseTwoContentTestHooks { BeforeFinalVerification = change };
            Assert.Equal("STALE", Hash(f, p, hooks: hooks).State); Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons"));
            Assert.Null(f.Rows("SELECT final_evidence_digest FROM sessions WHERE stage='CONTENT'").Single()[0]);
        }
    }

    [WindowsPhaseTwoTheory]
    [InlineData("syntax")] [InlineData("digest")] [InlineData("preflight-id")] [InlineData("tamper")]
    public void InvalidAcceptanceNeverCreatesContentOrReadsPayload(string problem)
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); var request = Options(f, p);
        if (problem == "syntax") request = request with { AcceptedManifestSha256 = "invalid" };
        if (problem == "digest") request = request with { AcceptedManifestSha256 = new string('a', 64) };
        if (problem == "preflight-id") request = request with { PreflightSessionId = 99 };
        if (problem == "tamper") f.Sql(f.Db, "DROP TRIGGER sealed_session_update; UPDATE sessions SET final_evidence_digest=printf('%064d',0)");
        var before = File.ReadAllBytes(f.Db); var files = new CountedFiles();
        Assert.ThrowsAny<Exception>(() => new PhaseTwoContentRunner(files).Run(request)); Assert.Empty(files.Reads);
        Assert.Equal(before, File.ReadAllBytes(f.Db)); Assert.Equal(0, f.Number("SELECT count(*) FROM sessions WHERE stage='CONTENT'")); Assert.False(Directory.Exists(request.OutputDirectory));
    }

    [WindowsPhaseTwoFact]
    public void OnlyAdmittedComponentsAreProcessedAndEmptyAdmissionIsRefused()
    {
        using var f = new Fixture(); f.Pair(); f.Pair("C", "D", "yyyy"); f.Scan();
        f.File("A", "one.BMS", "changed"); var p = f.Run(); var files = new CountedFiles(); var result = Hash(f, p, files);
        Assert.Equal(1, p.RejectedComponents); Assert.Equal("COMPLETED", result.State); Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { f.Path("C", "one.BMS"), f.Path("D", "one.BMS") }, files.Reads);
        using var other = new Fixture(); other.Pair(); other.Scan(); other.File("A", "one.BMS", "changed"); var rejected = other.Run();
        Assert.Throws<InvalidOperationException>(() => Hash(other, rejected)); Assert.Equal(0, other.Number("SELECT count(*) FROM sessions WHERE stage='CONTENT'"));
    }

    [WindowsPhaseTwoFact]
    public void CancelledPreflightCannotBecomeContent()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); using var cancel = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => new PhaseTwoPreflightRunner(hooks: new() { BeforeSeal = cancel.Cancel }).Run(f.Options(), cancel.Token));
        Assert.Throws<InvalidOperationException>(() => new PhaseTwoContentRunner().Run(new(f.Db, 1, new string('a', 64), Path.Combine(f.Base, "content"))));
        Assert.Equal(0, f.Number("SELECT count(*) FROM sessions WHERE stage='CONTENT'"));
    }

    [WindowsPhaseTwoFact]
    public void SharedPhysicalFailureAffectsEveryDependentComponentWithoutPairSalvage()
    {
        using var f = new Fixture(); f.Pair(); f.Pair("A/Sub", "C", "yyyy"); f.Pair("D", "E", "zzzz");
        var shared = f.File("A/Sub", "shared.wav", "aaaa"); f.File("C", "shared.wav", "aaaa"); f.Scan(); var p = f.Run(); var before = Snapshot(f);
        var files = new CountedFiles { Failure = path => PathPolicy.Normalize(path) == PathPolicy.Normalize(shared) ? "ERROR" : null }; var result = Hash(f, p, files);
        Assert.Equal("COMPLETED", result.State); Assert.Equal(2, result.ExitCode); Assert.Equal(2, result.IncompleteContentComponents); Assert.Equal(1, result.CompleteContentComponents);
        Assert.Equal(1, files.Reads.Count(path => PathPolicy.Normalize(path) == PathPolicy.Normalize(shared))); Assert.Equal(2, f.Number("SELECT count(*) FROM diagnostics WHERE code='FILE_READ_ERROR'"));
        Assert.Equal(1, f.Number("SELECT count(*) FROM comparisons")); Assert.Equal(f.Number("SELECT count(*) FROM physical_files"), files.Reads.Count);
        Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons q JOIN content_component_outcomes o ON o.content_session_id=q.content_session_id AND o.component_id=q.component_id WHERE o.outcome='INCOMPLETE'"));
        var rows = File.ReadAllText(Path.Combine(f.Base, "content", "filesystem_inventory.csv")); Assert.Contains("INCOMPLETE,false", rows); Assert.Contains("true,INCOMPLETE,false", rows);
        AssertPreserved(f, before);
    }

    [WindowsPhaseTwoFact]
    public void ActualContentOnlyReadDenialKeepsIndependentComponentComplete()
    {
        using var f = new Fixture(); f.Pair(); f.Pair("C", "D", "yyyy"); var denied = f.File("A", "01.wav", "aaaa"); f.Scan(); var p = f.Run(); var before = Snapshot(f);
        using (var held = new FileStream(denied, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = Hash(f, p); Assert.Equal("COMPLETED", result.State); Assert.Equal(2, result.ExitCode);
            Assert.Equal(1, result.IncompleteContentComponents); Assert.Equal(1, result.CompleteContentComponents);
            Assert.Equal(1, f.Number("SELECT count(*) FROM content_hashes WHERE status='ERROR' AND sha256 IS NULL")); Assert.Equal(1, f.Number("SELECT count(*) FROM comparisons"));
        }
        AssertPreserved(f, before);
    }

    [WindowsPhaseTwoFact]
    public void UnstableRequiredFileMakesAllIncompleteButNotAFalseGlobalFailure()
    {
        using var f = new Fixture(); f.Pair(); var path = f.File("A", "01.wav", "aaaa"); f.Scan(); var p = f.Run();
        var result = Hash(f, p, new CountedFiles { Failure = s => s == path ? "UNSTABLE" : null });
        Assert.Equal("COMPLETED", result.State); Assert.Equal(2, result.ExitCode); Assert.Equal(0, result.CompleteContentComponents);
        Assert.Equal(1, result.IncompleteContentComponents); Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons"));
        Assert.Single(File.ReadAllLines(Path.Combine(f.Base, "content", "folder_comparisons.csv")));
        Assert.Single(File.ReadAllLines(Path.Combine(f.Base, "content", "content_groups.csv")));
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Base, "content", "session_summary.json")));
        Assert.Contains("No trusted", summary.RootElement.GetProperty("comparison_notice").GetString());
    }

    [WindowsPhaseTwoFact]
    public void CancellationAndInterruptedSessionsCannotExposeTrustedComparisons()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); using var cancel = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => new PhaseTwoContentRunner(hooks: new() { AfterFile = _ => cancel.Cancel() }).Run(Options(f, p), cancel.Token));
        Assert.Equal("CANCELLED", f.Rows("SELECT state FROM sessions WHERE stage='CONTENT'").Single()[0]); Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons"));
        PhaseTwoReportWriter.Write(f.Db, 2, Path.Combine(f.Base, "cancel-report"));
        Assert.False(File.Exists(Path.Combine(f.Base, "cancel-report", "folder_comparisons.csv")));
        Assert.DoesNotContain("true,COMPLETE,true", File.ReadAllText(Path.Combine(f.Base, "cancel-report", "filesystem_inventory.csv")));
        using var db = PhaseTwoDatabase.Open(f.Db, true); var interrupted = Start(db, p);
        Assert.Throws<InvalidOperationException>(() => PhaseTwoReportWriter.Write(f.Db, interrupted, Path.Combine(f.Base, "interrupted-report")));
        Assert.Throws<InvalidOperationException>(() => new PhaseTwoContentRunner().Run(Options(f, p, "new-content")));
    }

    [WindowsPhaseTwoFact]
    public void SourceAndOutputAliasesAreRefusedBeforeContentWrites()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); var before = File.ReadAllBytes(f.Db);
        Assert.Throws<InvalidOperationException>(() => new PhaseTwoContentRunner().Run(Options(f, p) with { OutputDirectory = f.Path("unsafe-output") }));
        Assert.Equal(before, File.ReadAllBytes(f.Db)); Assert.False(Directory.Exists(f.Path("unsafe-output")));
    }

    [WindowsPhaseTwoFact]
    public void LegacyCapabilityAndForeignDatabasesAreRejectedUnchanged()
    {
        using var f = new Fixture(); var legacy = Path.Combine(f.Base, "legacy.db");
        f.Sql(legacy, "PRAGMA application_id=1112363826; PRAGMA user_version=1; CREATE TABLE schema_metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL); INSERT INTO schema_metadata VALUES('tool','BmsLibraryAudit.Phase2'),('contract_version','1.0-draft'),('absolute_comparison_mode','windows-ordinal-ignore-case'),('relative_comparison_mode','relative-ordinal-spelling-v1'),('capabilities','PREFLIGHT_ONLY'),('schema_revision','phase2a-hardening-1');");
        foreach (var foreign in new[] { false, true })
        {
            if (foreign) f.Sql(legacy, "PRAGMA application_id=7"); var bytes = File.ReadAllBytes(legacy); var mtime = File.GetLastWriteTimeUtc(legacy);
            Assert.Throws<InvalidOperationException>(() => PhaseTwoDatabase.Open(legacy, true)); Assert.Equal(bytes, File.ReadAllBytes(legacy)); Assert.Equal(mtime, File.GetLastWriteTimeUtc(legacy));
        }
    }

    [WindowsPhaseTwoFact]
    public void DeterministicContentExportsPreserveSealsAndWriteSeparateReceipts()
    {
        using var f = new Fixture(); f.Pair("日本語,A", "B"); f.File("日本語,A", "x.wav", "aaaa"); f.File("B", "y.wav", "aaaa"); f.Scan(); var p = f.Run(); var result = Hash(f, p);
        var first = Path.Combine(f.Base, "content"); var repeat = Path.Combine(f.Base, "repeat-content"); var before = Snapshot(f);
        PhaseTwoReportWriter.Write(f.Db, result.SessionId, repeat);
        var expected = new[] { "content_groups.csv", "diagnostics.csv", "filesystem_inventory.csv", "folder_comparisons.csv", "observation_roots.csv", "session_summary.json" };
        Assert.Equal(expected, Directory.GetFiles(first).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var name in expected) { var bytes = File.ReadAllBytes(Path.Combine(first, name)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(repeat, name))); Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 })); Assert.DoesNotContain((byte)13, bytes); }
        Assert.Equal(2, f.Number("SELECT count(*) FROM report_sets WHERE session_id=$0 AND status='COMPLETED'", result.SessionId));
        Assert.Equal(12, f.Number("SELECT count(*) FROM report_files rf JOIN report_sets rs ON rs.report_set_id=rf.report_set_id WHERE rs.session_id=$0", result.SessionId));
        Assert.Equal(result.ManifestSha256, (string)f.Rows("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", result.SessionId).Single()[0]!);
        AssertPreserved(f, before);
    }

    [WindowsPhaseTwoFact]
    public void SealedContentAndCrossPreflightReferencesAreEnforced()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); var result = Hash(f, p);
        foreach (var table in new[] { "content_hashes", "content_component_outcomes", "comparisons", "comparison_entries", "comparison_content_groups" })
        {
            Assert.Throws<SqliteException>(() => f.Sql(f.Db, "DELETE FROM " + table));
            Assert.Throws<SqliteException>(() => f.Sql(f.Db, "UPDATE " + table + " SET content_session_id=content_session_id"));
        }
        var second = f.Run(output: Path.Combine(f.Base, "second-preflight"));
        using var db = PhaseTwoDatabase.Open(f.Db, true); var child = Start(db, p); var physical = db.Scalar("SELECT physical_file_id FROM physical_files WHERE preflight_id=$0 LIMIT 1", second.SessionId);
        Assert.Throws<SqliteException>(() => db.Insert("content_hashes", "content_session_id,preflight_id,physical_file_id,status,reason_code,bytes_read,attempts", child, second.SessionId, physical, "PENDING", "PENDING", 0, 0));
        var badComponent = db.Scalar("SELECT component_id FROM candidate_components WHERE preflight_id=$0", second.SessionId);
        Assert.Throws<SqliteException>(() => db.Insert("content_component_outcomes", "content_session_id,preflight_id,component_id,outcome,reason_code,reason,required_physical_file_count,trusted_successful_hash_count,failed_untrusted_file_count,affected_observation_root_count,comparison_eligible,coherent,sealed", child, p.SessionId, badComponent, "COMPLETE", "CONTENT_COMPLETE", "x", 2, 2, 0, 0, true, true, true));
        Assert.Throws<SqliteException>(() => db.Execute("UPDATE sessions SET state='COMPLETED',completed_utc='x',final_evidence_digest=$0 WHERE session_id=$1", new string('a', 64), child));
        Assert.Throws<InvalidOperationException>(() => PhaseTwoContentRunner.ValidateEvidence(db, child));
        Assert.Equal(result.ManifestSha256, db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", result.SessionId));
    }

    [Fact]
    public void HandAuthoredMultiplicityAndKindCollisionOracleDoesNotCollapsePaths()
    {
        var sha = new string('a', 64);
        PhaseTwoContentComparison.Node[] a = [new(1, ".", "DIRECTORY", null, false), new(2, "kick.wav", "REGULAR_FILE", sha, false), new(3, "001.wav", "REGULAR_FILE", sha, false)];
        PhaseTwoContentComparison.Node[] b = [new(4, ".", "DIRECTORY", null, false), new(5, "kick.wav", "REGULAR_FILE", sha, false), new(6, "001.wav", "DIRECTORY", null, false)];
        var evidence = PhaseTwoContentComparison.Derive(a, b);
        Assert.Equal(new long[] { 1, 0, 1, 0, 0, 1, 1 }, evidence.Counts); Assert.Equal(new[] { false, false, false, false, true, true }, evidence.Flags);
        Assert.Equal(new PhaseTwoContentComparison.Group("FULL", sha, 2, 1, true), evidence.Groups[0]);
        Assert.Equal("KIND_CONFLICT", evidence.Entries.Single(e => e.Path == "001.wav").Category);
    }

    private static long Start(PhaseTwoDatabase db, PhaseTwoPreflightResult p) => db.Insert("sessions", "stage,parent_preflight_id,accepted_manifest_sha256,state,started_utc,tool_version,runtime_build_sha,source_component_count",
        "CONTENT", p.SessionId, p.ManifestSha256, "RUNNING", "2026-10-14T00:00:00.0000000+00:00", "0.1.0", "UNKNOWN_DIRTY", db.Scalar("SELECT source_component_count FROM sessions WHERE session_id=$0", p.SessionId));

    [WindowsPhaseTwoFact]
    public void NestedOccurrencesAreReadOnceWhileHardlinkPathsAreEachFreshlyRead()
    {
        using var f = new Fixture(); f.Pair("A", "A/Sub"); var sound = f.File("A/Sub", "sound.wav", "aaaa");
        var hardlink = f.Path("A/Sub", "copy.wav"); NativeLink("/H", hardlink, sound); f.Scan(); var p = f.Run(); var before = Snapshot(f);
        var files = new CountedFiles(); var result = Hash(f, p, files);
        Assert.Equal(0, result.ExitCode); Assert.Equal(4, files.Reads.Count); Assert.Equal(4, files.Reads.Distinct(PathPolicy.Comparer).Count());
        Assert.Equal(PhaseTwoSafety.Inspect(sound).Identity, PhaseTwoSafety.Inspect(hardlink).Identity);
        Assert.Equal(2, f.Number("SELECT count(*) FROM content_hashes h JOIN physical_files p ON p.physical_file_id=h.physical_file_id WHERE p.native_identity=$0", PhaseTwoSafety.Inspect(sound).Identity));
        Assert.Equal(4, f.Number("SELECT count(*) FROM observation_memberships m JOIN physical_files p ON p.entry_id=m.entry_id WHERE p.native_identity=$0", PhaseTwoSafety.Inspect(sound).Identity));
        AssertPreserved(f, before);
    }

    [WindowsPhaseTwoFact]
    public void ReparseInsertedAfterAcceptanceIsStaleAndItsTargetIsNotPayloadRead()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run();
        var target = Path.Combine(f.Base, "external-owned"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "secret.wav"), "opaque");
        var alias = f.Path("A", "linked"); NativeLink("/J", alias, target);
        try { var files = new CountedFiles(); var result = Hash(f, p, files); Assert.Equal("STALE", result.State); Assert.Empty(files.Reads); Assert.Equal("opaque", File.ReadAllText(Path.Combine(target, "secret.wav"))); }
        finally { Directory.Delete(alias); }
    }

    [WindowsPhaseTwoFact]
    public void ReplacedNativeIdentityWithSameSizeAndTimeIsRejectedBeforePayload()
    {
        using var f = new Fixture(); f.Pair(); var path = f.File("A", "sound.wav", "aaaa"); f.Scan(); var p = f.Run();
        var facts = PhaseTwoSafety.Inspect(path); var replacement = Path.Combine(f.Base, "replacement.wav"); File.WriteAllText(replacement, "bbbb", new UTF8Encoding(false)); File.SetLastWriteTimeUtc(replacement, new DateTime(facts.MtimeTicks, DateTimeKind.Utc));
        File.Move(replacement, path, true); Assert.NotEqual(facts.Identity, PhaseTwoSafety.Inspect(path).Identity);
        var files = new CountedFiles(); Assert.Equal("STALE", Hash(f, p, files).State); Assert.Empty(files.Reads);
    }

    [WindowsPhaseTwoFact]
    public void FinalCoverageTamperingFailsAndDoesNotPublishComparisonRows()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run();
        Assert.Throws<SqliteException>(() => Hash(f, p, hooks: new() { BeforeFinalVerification = () => f.Sql(f.Db, "DELETE FROM content_hashes WHERE physical_file_id=(SELECT min(physical_file_id) FROM content_hashes)") }));
        Assert.Equal("FAILED", f.Rows("SELECT state FROM sessions WHERE stage='CONTENT'").Single()[0]); Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons"));
        PhaseTwoReportWriter.Write(f.Db, 2, Path.Combine(f.Base, "failed-report"));
        var inventory = File.ReadAllText(Path.Combine(f.Base, "failed-report", "filesystem_inventory.csv")); Assert.DoesNotContain(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("xxxx"))), inventory);
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Base, "failed-report", "session_summary.json")));
        Assert.Equal("NONE", summary.RootElement.GetProperty("trusted_content_evidence_scope").GetString()); Assert.Equal(0, summary.RootElement.GetProperty("comparison_count").GetInt64());
    }

    [WindowsPhaseTwoFact]
    public void CancellationBeforeSealRollsBackComparisonPublication()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); using var cancel = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => new PhaseTwoContentRunner(hooks: new() { BeforeSeal = cancel.Cancel }).Run(Options(f, p), cancel.Token));
        Assert.Equal("CANCELLED", f.Rows("SELECT state FROM sessions WHERE stage='CONTENT'").Single()[0]); Assert.Equal(0, f.Number("SELECT count(*) FROM comparisons")); Assert.Equal(0, f.Number("SELECT count(*) FROM content_component_outcomes"));
    }

    [WindowsPhaseTwoFact]
    public void ExportFailureLeavesSealedContentAndFailedReceiptThenAllowsNewExport()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); var result = Hash(f, p);
        Assert.Throws<IOException>(() => PhaseTwoReportWriter.Write(f.Db, result.SessionId, Path.Combine(f.Base, "failed-export"), hooks: new() { BeforeReportFile = _ => throw new IOException("synthetic export interruption") }));
        Assert.Equal(1, f.Number("SELECT count(*) FROM report_sets WHERE session_id=$0 AND status='FAILED'", result.SessionId));
        PhaseTwoReportWriter.Write(f.Db, result.SessionId, Path.Combine(f.Base, "recovered-export"));
        Assert.Equal(result.ManifestSha256, f.Rows("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", result.SessionId).Single()[0]);
    }

    private static void NativeLink(string kind, string alias, string target)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink {kind} \"{alias}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit(); Assert.True(process.ExitCode == 0, output + error);
    }

    [WindowsPhaseTwoFact]
    public void RejectedInventoryPathsCannotReceiveContentRowsOrDiagnostics()
    {
        using var f = new Fixture(); f.Pair(); f.Pair("C", "D", "yyyy"); f.Scan();
        var p = f.Run(hooks: new() { BeforeInventoryVerification = () => f.File("A", "extra.bms", "zzzz") });
        Assert.Equal(1, p.AdmittedComponents); Assert.Equal(1, p.RejectedComponents);
        using var db = PhaseTwoDatabase.Open(f.Db, true); var child = Start(db, p);
        var physical = db.Number("SELECT p.physical_file_id FROM physical_files p JOIN preflight_entries e ON e.entry_id=p.entry_id WHERE e.entry_key=$0", PathPolicy.Normalize(f.Path("A", "one.BMS")));
        Assert.Throws<SqliteException>(() => db.Insert("content_hashes", "content_session_id,preflight_id,physical_file_id,status,reason_code,bytes_read,attempts", child, p.SessionId, physical, "PENDING", "PENDING", 0, 0));
        var rejected = db.Number("SELECT component_id FROM candidate_components WHERE outcome<>'ADMITTED'");
        Assert.Throws<SqliteException>(() => db.Diagnostic(child, rejected, null, null, "TEST", f.Db, "outside scope"));
    }

    [WindowsPhaseTwoFact]
    public void EmptyAndLargeOpaqueFilesUseOneReusedBoundedBufferAndIndependentDigests()
    {
        using var f = new Fixture(); f.Pair(); var path = f.File("A", "large.dat", ""); var empty = f.File("B", "empty.dat", "");
        var payload = Enumerable.Range(0, 1024 * 1024 + 13).Select(i => (byte)(i % 251)).ToArray(); File.WriteAllBytes(path, payload);
        f.Scan(); var p = f.Run(); var files = new CountedFiles(); var before = Snapshot(f); var result = Hash(f, p, files);
        Assert.Equal(0, result.ExitCode); Assert.Equal(4, files.Reads.Count);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), f.Rows("SELECT h.sha256 FROM content_hashes h JOIN physical_files p ON p.physical_file_id=h.physical_file_id JOIN preflight_entries e ON e.entry_id=p.entry_id WHERE e.entry_key=$0", PathPolicy.Normalize(path)).Single()[0]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>())), f.Rows("SELECT h.sha256 FROM content_hashes h JOIN physical_files p ON p.physical_file_id=h.physical_file_id JOIN preflight_entries e ON e.entry_id=p.entry_id WHERE e.entry_key=$0", PathPolicy.Normalize(empty)).Single()[0]);
        Assert.Equal(payload.Length + 8, f.Number("SELECT sum(bytes_read) FROM content_hashes")); AssertPreserved(f, before);
    }
    private static Dictionary<string, (byte[] Bytes, NativePathFacts Facts)> Snapshot(Fixture f) => Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).Append(f.Source)
        .ToDictionary(p => p, p => (File.ReadAllBytes(p), PhaseTwoSafety.Inspect(p)), PathPolicy.Comparer);
    private static void AssertPreserved(Fixture f, Dictionary<string, (byte[] Bytes, NativePathFacts Facts)> before)
    {
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).Append(f.Source).Order(StringComparer.Ordinal));
        foreach (var (path, facts) in before) { Assert.Equal(facts.Bytes, File.ReadAllBytes(path)); Assert.Equal(facts.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(PhaseTwoSafety.Exists(f.Source + suffix));
    }
    private sealed class CountedFiles : IPhaseTwoContentFileSystem
    {
        private readonly PhaseTwoFileSystem real = new();
        private byte[]? observedBuffer;
        public List<string> Reads { get; } = [];
        public Func<string, string?>? Failure { get; init; }
        public PhaseTwoEntry Metadata(string path) => real.Metadata(path);
        public IEnumerable<string> Children(string path) => real.Children(path);
        public PhaseTwoChartRead ReadChart(string path, CancellationToken token) => real.ReadChart(path, token);
        public PhaseTwoContentRead ReadContent(PhaseTwoEntry expected, byte[] buffer, CancellationToken token)
        {
            Assert.Equal(64 * 1024, buffer.Length); if (observedBuffer is not null) Assert.Same(observedBuffer, buffer); observedBuffer = buffer;
            Reads.Add(expected.OriginalPath); var status = Failure?.Invoke(expected.OriginalPath);
            if (status is not null) return new(expected, expected, null, 0, status == "UNSTABLE" ? 2 : 1, status,
                "2026-10-14T00:00:00.0000000+00:00", "2026-10-14T00:00:01.0000000+00:00");
            return real.ReadContent(expected, buffer, token);
        }
    }
}
