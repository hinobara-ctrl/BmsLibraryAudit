using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BmsLibraryAudit.Tests;

public sealed class WindowsPhaseTwoFactAttribute : FactAttribute
{
    public WindowsPhaseTwoFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Phase 2A native safety is Windows-only; this fixture was not executed."; }
}
public sealed class WindowsPhaseTwoTheoryAttribute : TheoryAttribute
{
    public WindowsPhaseTwoTheoryAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Phase 2A native safety is Windows-only; this fixture was not executed."; }
}

public sealed class PhaseTwoPreflightTests
{
    [WindowsPhaseTwoFact]
    public void FreshChartRevalidationDoesNotReadResourceOrUnselectedNestedCharts()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        fixture.File("A/nested", "not-selected.bms", "opaque nested chart");
        fixture.File("A", "audio.wav", "opaque resource"); fixture.File("A", "extensionless", "opaque");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "A", "empty"));
        var instrument = new InstrumentedFiles();
        var result = fixture.Run(instrument);
        Assert.Equal("COMPLETED", result.State); Assert.Equal(1, result.AdmittedComponents);
        Assert.Equal(new[] { fixture.Path("A", "one.BMS"), fixture.Path("B", "one.BMS") }, instrument.Reads);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM preflight_entries WHERE kind='DIRECTORY' AND original_path LIKE '%empty'"));
        Assert.Equal(0, fixture.Number("SELECT count(*) FROM sqlite_schema WHERE name LIKE 'comparisons%'"));
        using var summary = fixture.Summary(); Assert.Equal(0, summary.RootElement.GetProperty("non_chart_payload_reads").GetInt64());
        Assert.Equal("NONE", summary.RootElement.GetProperty("trusted_content_evidence_scope").GetString());
    }

    [WindowsPhaseTwoFact]
    public void NestedRootsShareOneEntryWithIndependentRelativeMemberships()
    {
        using var fixture = new Fixture(); fixture.Pair("A", "A/B"); fixture.Scan();
        var resource = fixture.File("A/B", "sound.wav", "sample");
        var result = fixture.Run(); Assert.Equal(1, result.AdmittedComponents);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM preflight_entries WHERE entry_key=$0", PathPolicy.Normalize(resource)));
        var paths = fixture.Rows("SELECT relative_path FROM observation_memberships m JOIN preflight_entries e ON e.entry_id=m.entry_id WHERE e.entry_key=$0 ORDER BY relative_path", PathPolicy.Normalize(resource));
        Assert.Equal(new[] { "B/sound.wav", "sound.wav" }, paths.Select(r => (string)r[0]!));
        Assert.Equal(2, fixture.Number("SELECT count(*) FROM observation_memberships WHERE relative_path='.'"));
    }

    [WindowsPhaseTwoFact]
    public void ChangedChartWithPreservedSizeAndMtimeRejectsWholeComponentButIndependentComponentContinues()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Pair("C", "D", "yyyy"); fixture.Scan();
        var path = fixture.Path("A", "one.BMS"); var mtime = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "zzzz", new UTF8Encoding(false)); File.SetLastWriteTimeUtc(path, mtime);
        var instrument = new InstrumentedFiles(); var result = fixture.Run(instrument);
        Assert.Equal(2, result.ExitCode); Assert.Equal(1, result.AdmittedComponents); Assert.Equal(1, result.RejectedComponents);
        Assert.Equal(0, fixture.Number("SELECT count(*) FROM observation_memberships m JOIN observation_roots r ON r.observation_root_id=m.observation_root_id WHERE r.original_path=$0", fixture.Path("A")));
        Assert.Equal(4, instrument.Reads.Count);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM candidate_components WHERE outcome='STALE_PHASE_1_CANDIDATE'"));
    }

    [WindowsPhaseTwoTheory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("rename")]
    [InlineData("case")]
    public void DirectChartMembershipAndSpellingChangesCannotBeSalvaged(string change)
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var path = fixture.Path("A", "one.BMS");
        if (change == "add") fixture.File("A", "copy.bms", "xxxx");
        else if (change == "remove") File.Delete(path);
        else File.Move(path, fixture.Path("A", change == "case" ? "ONE.bms" : "renamed.bms"));
        var result = fixture.Run(); Assert.Equal(0, result.AdmittedComponents); Assert.Equal(2, result.ExitCode);
        Assert.Equal(0, fixture.Number("SELECT count(*) FROM preflight_entries"));
        Assert.True(fixture.Number("SELECT count(*) FROM chart_revalidations WHERE match_status<>'MATCH'") > 0);
    }

    [WindowsPhaseTwoFact]
    public void ContentOnlyReadDenialRejectsChartAndNeverInventsDigest()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        using var locked = new FileStream(fixture.Path("A", "one.BMS"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = fixture.Run(); Assert.Equal(1, result.RejectedComponents);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM chart_revalidations WHERE read_status='UNREADABLE' AND fresh_sha256 IS NULL"));
    }

    [WindowsPhaseTwoFact]
    public void TwoUnstableChartAttemptsRejectComponent()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var instrument = new InstrumentedFiles { ReadOverride = path => Path.GetFileName(Path.GetDirectoryName(path)) == "A" ? new(4, 1, 4, 2, null, 8, 2, "UNSTABLE") : null };
        var result = fixture.Run(instrument); Assert.Equal(1, result.RejectedComponents);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM chart_revalidations WHERE read_status='UNSTABLE' AND attempts=2 AND fresh_sha256 IS NULL"));
    }

    [WindowsPhaseTwoFact]
    public void MatchingBytesWithMetadataDriftWarnAndUseFreshStamp()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var path = fixture.Path("A", "one.BMS"); File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        var result = fixture.Run(); Assert.Equal(1, result.AdmittedComponents);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM diagnostics WHERE code='CHART_METADATA_DRIFT' AND severity='WARNING'"));
        Assert.Equal(File.GetLastWriteTimeUtc(path).Ticks, fixture.Number("SELECT after_mtime_ticks FROM chart_revalidations WHERE chart_key=$0", PathPolicy.Normalize(path)));
    }

    [WindowsPhaseTwoFact]
    public void ResourceMetadataFailureAndLazyEnumerationFailureRemainComponentLocal()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Pair("C", "D", "yyyy"); fixture.Scan();
        var denied = fixture.File("A", "resource.wav", "sample");
        var files = new InstrumentedFiles { MetadataFailure = p => p == denied };
        var result = fixture.Run(files); Assert.Equal(1, result.AdmittedComponents); Assert.Equal(1, result.RejectedComponents);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM preflight_entries WHERE kind='UNKNOWN' AND size IS NULL"));
        using var other = new Fixture(); other.Pair(); other.Scan();
        var unreadable = other.Path("A", "subdirectory"); Directory.CreateDirectory(unreadable);
        var partial = other.Run(new InstrumentedFiles { ChildrenFailure = p => p == unreadable });
        Assert.Equal(1, partial.RejectedComponents); Assert.Equal("COMPLETED", partial.State);
    }

    [WindowsPhaseTwoTheory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("size")]
    [InlineData("empty-dir")]
    [InlineData("chart")]
    public void SecondInventoryDetectsChangesAndPreservesFirstManifest(string change)
    {
        using var fixture = new Fixture(); fixture.Pair(); var resource = fixture.File("A", "sound.wav", "before"); fixture.Scan();
        var result = fixture.Run(hooks: new() { BeforeInventoryVerification = () =>
        {
            if (change == "add") fixture.File("A", "new.wav", "new");
            else if (change == "remove") File.Delete(resource);
            else if (change == "size") File.AppendAllText(resource, "more");
            else if (change == "chart") fixture.File("A", "new.bms", "xxxx");
            else Directory.CreateDirectory(fixture.Path("A", "empty"));
        } });
        Assert.Equal(1, result.RejectedComponents); Assert.True(fixture.Number("SELECT count(*) FROM diagnostics WHERE code='INVENTORY_CHANGED'") > 0);
        Assert.Equal(6, fixture.Number("SELECT size FROM preflight_entries WHERE entry_key=$0", PathPolicy.Normalize(resource)));
        Assert.Equal(0, fixture.Number("SELECT count(*) FROM preflight_entries WHERE original_path LIKE '%new.wav'"));
        if (change == "chart") Assert.Equal(1, fixture.Number("SELECT count(*) FROM candidate_components WHERE outcome='STALE_PHASE_1_CANDIDATE'"));
    }

    [WindowsPhaseTwoFact]
    public void ChartAddedAfterRevalidationButBeforeFirstInventoryIsStillStale()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var result = fixture.Run(hooks: new() { BeforeInventory = () => fixture.File("A", "late.bms", "xxxx") });
        Assert.Equal(1, result.RejectedComponents);
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM candidate_components WHERE outcome='STALE_PHASE_1_CANDIDATE'"));
    }

    [WindowsPhaseTwoFact]
    public void PreviouslyCapturedRootsStayProtectedAcrossDifferentSourceDatabases()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); fixture.Run();
        var otherRoot = Path.Combine(fixture.Base, "other-songs"); Directory.CreateDirectory(otherRoot);
        var otherSource = Path.Combine(fixture.Base, "other-audit.db");
        new ChartScanner().Scan(new(otherSource, [otherRoot]));
        var instrument = new InstrumentedFiles();
        Assert.Throws<InvalidOperationException>(() => new PhaseTwoPreflightRunner(instrument).Run(new(otherSource, 1, fixture.Db, fixture.Path("old-root-output"))));
        Assert.Empty(instrument.Reads); Assert.False(Directory.Exists(fixture.Path("old-root-output")));
    }

    [WindowsPhaseTwoFact]
    public void ExactEdgeConstraintRequiresNonNullPositiveEqualCountsAndContentSessionsAreDisabled()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); fixture.Run();
        var clone = Path.Combine(fixture.Base, "constraint-fixture.db"); File.Copy(fixture.Db, clone);
        fixture.Sql(clone, "DROP TRIGGER seal_source_exact_edges_UPDATE");
        Assert.Throws<SqliteException>(() => fixture.Sql(clone, "UPDATE source_exact_edges SET common=NULL"));
        Assert.Throws<SqliteException>(() => fixture.Sql(clone, "UPDATE source_exact_edges SET common=0"));
        Assert.Throws<SqliteException>(() => fixture.Sql(clone, "UPDATE source_exact_edges SET charts_a=2"));
        Assert.Throws<SqliteException>(() => fixture.Sql(fixture.Db, "INSERT INTO sessions(stage,state,parent_preflight_id,accepted_manifest_sha256,started_utc,tool_version,runtime_build_sha,source_component_count) SELECT 'CONTENT','RUNNING',1,final_evidence_digest,'x','x','x',0 FROM sessions WHERE session_id=1"));
    }

    [WindowsPhaseTwoFact]
    public void FullContractPreflightGoldenVectorBindsEmptyPopulationProvenanceAndAllFourteenGroups()
    {
        // Expected digest was computed independently with PowerShell from the
        // contract field table and these literal facts, not by this serializer.
        const string expected = "f4b2787b4700234b8dc7a433ac16b9f7e38fa2d99137c871dc003231544d75c8";
        using var fixture = new Fixture();
        foreach (var session in new[] { 17L, 99L })
        {
            using var db = PhaseTwoDatabase.Open(Path.Combine(fixture.Base, session + ".db"), true);
            db.Begin();
            db.Insert("sessions", "session_id,stage,state,started_utc,completed_utc,tool_version,runtime_build_sha,source_component_count",
                session, "PREFLIGHT", "RUNNING", "2026-10-11T00:02:00.0000000+00:00", "2026-10-11T00:03:00.0000000+00:00", "0.1.0", new string('b', 40), 0);
            db.Insert("source_snapshots", "snapshot_id,preflight_id,repository_baseline_sha,phase1_implementation_sha,source_db_original_path,source_db_normalized_path,source_db_native_identity,source_db_size,source_db_sha256,source_db_before_mtime_ticks,source_db_after_mtime_ticks,source_application_id,source_schema_version,source_comparison_mode,source_scan_id,source_scan_started_utc,source_scan_completed_utc,admission_policy,seal_flag",
                session, session, PhaseTwoDatabase.Baseline, PhaseTwoDatabase.PhaseOneBaseline, @"C:\synthetic\audit.db", @"C:\SYNTHETIC\AUDIT.DB", "00000001:0000000000000002", 4096,
                new string('a', 64), 638000000000000000L, 638000000000000000L, 0x424D5341, 1, PhaseTwoDatabase.AbsoluteMode, 1,
                "2026-10-11T00:00:00.0000000+00:00", "2026-10-11T00:01:00.0000000+00:00", "EXACT_ONLY", false);
            db.Insert("source_library_roots", "snapshot_id,source_root_id,original_path,normalized_path,active,configured", session, 1, @"C:\synthetic\songs", @"C:\SYNTHETIC\SONGS", true, true);
            Assert.Equal(expected, PhaseTwoManifest.Preflight(db, session));
            db.Execute("UPDATE source_snapshots SET source_db_sha256=$0 WHERE snapshot_id=$1", new string('c', 64), session);
            Assert.NotEqual(expected, PhaseTwoManifest.Preflight(db, session));
            db.Rollback();
        }
    }

    [WindowsPhaseTwoFact]
    public void SourceGuardPreventsConcurrentMutationAndCaptureFingerprintsMatch()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); var hash = SHA256.HashData(File.ReadAllBytes(fixture.Source));
        var denied = false;
        fixture.Run(hooks: new() { AfterSourceFingerprint = () =>
        {
            Assert.Throws<IOException>(() => new FileStream(fixture.Source, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose()); denied = true;
        } });
        Assert.True(denied); Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(fixture.Source)));
        Assert.Equal(Convert.ToHexString(hash).ToLowerInvariant(), fixture.Rows("SELECT source_db_sha256 FROM source_snapshots").Single()[0]);
    }

    [WindowsPhaseTwoFact]
    public void CapturedSourceFactsAreImmutableEvenWhilePreflightIsRunning()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var checkedGuard = false;
        var result = fixture.Run(hooks: new() { BeforeInventory = () =>
        {
            Assert.Equal("RUNNING", fixture.Rows("SELECT state FROM sessions").Single()[0]);
            Assert.Throws<SqliteException>(() => fixture.Sql(fixture.Db, "UPDATE source_library_roots SET original_path='unsafe'"));
            checkedGuard = true;
        } });
        Assert.True(checkedGuard); Assert.Equal("COMPLETED", result.State);
    }

    [WindowsPhaseTwoTheory]
    [InlineData("-journal", false)]
    [InlineData("-wal", true)]
    [InlineData("-shm", false)]
    public void SourceSidecarsAndDirectoryCollisionsAreRefusedUnchanged(string suffix, bool directory)
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); var sidecar = fixture.Source + suffix;
        if (directory) Directory.CreateDirectory(sidecar); else File.WriteAllText(sidecar, "preserve");
        Assert.Throws<InvalidOperationException>(() => fixture.Run()); Assert.False(File.Exists(fixture.Db));
        if (!directory) Assert.Equal("preserve", File.ReadAllText(sidecar));
    }

    [WindowsPhaseTwoTheory]
    [InlineData("scan")]
    [InlineData("schema")]
    [InlineData("application")]
    [InlineData("platform")]
    [InlineData("hash")]
    public void InvalidSourceProvenanceFailsBeforeDestinationWrites(string problem)
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        if (problem != "scan") fixture.Sql(fixture.Source, problem switch
        {
            "schema" => "PRAGMA user_version=999", "application" => "PRAGMA application_id=7",
            "platform" => "UPDATE schema_metadata SET value='other' WHERE key='path_comparison'",
            _ => "UPDATE chart_files SET sha256='invalid'"
        });
        var before = File.ReadAllBytes(fixture.Source);
        Assert.ThrowsAny<Exception>(() => fixture.Run(sourceScan: problem == "scan" ? 99 : 1));
        Assert.Equal(before, File.ReadAllBytes(fixture.Source)); Assert.False(File.Exists(fixture.Db));
    }

    [WindowsPhaseTwoFact]
    public void SourceWithoutCompletedScanIsRefused()
    {
        using var fixture = new Fixture(); using (AuditDatabase.Open(fixture.Source, true)) { }
        Assert.Throws<InvalidOperationException>(() => fixture.Run()); Assert.False(File.Exists(fixture.Db));
    }

    [WindowsPhaseTwoFact]
    public void MissingSourceDigestAndHistoricalRootsStayExcludedAndProtected()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        fixture.Sql(fixture.Source, "UPDATE chart_files SET status='ERROR',sha256=NULL,md5=NULL WHERE filename='one.BMS'");
        var result = fixture.Run(); Assert.Equal(0, result.AdmittedComponents); Assert.Equal(2, result.ExitCode);
        using var other = new Fixture(); other.Pair(); var oldRoot = Path.Combine(other.Base, "historical"); Directory.CreateDirectory(oldRoot);
        new ChartScanner().Scan(new(other.Source, [other.Root, oldRoot])); other.Scan();
        var files = new InstrumentedFiles();
        Assert.Throws<InvalidOperationException>(() => other.Run(files, destination: Path.Combine(oldRoot, "phase2.db"), sourceScan: 2));
        Assert.Empty(files.Reads); Assert.False(File.Exists(Path.Combine(oldRoot, "phase2.db")));
    }

    [WindowsPhaseTwoFact]
    public void ExactOnlySelectionPreservesPhysicalCopiesAndRejectsRelatedLabels()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.File("A", "copy.bme", "xxxx");
        fixture.File("Subset", "one.bms", "xxxx"); fixture.File("Subset", "extra.bms", "yyyy"); fixture.Pair("C", "D", "zzzz"); fixture.Scan();
        var result = fixture.Run(components: ["EXACT_ONLY_000001"]); Assert.Equal(1, result.SelectedComponents);
        Assert.Equal(4, fixture.Number("SELECT count(*) FROM source_charts WHERE sha256=(SELECT sha256 FROM source_charts LIMIT 1)"));
        Assert.Equal(3, fixture.Number("SELECT count(*) FROM chart_revalidations"));
        Assert.Equal(1, fixture.Number("SELECT distinct_chart_count FROM candidate_components"));
        Assert.Throws<ArgumentException>(() => fixture.Run(components: ["RELATED_000001"], destination: Path.Combine(fixture.Base, "bad.db"), output: Path.Combine(fixture.Base, "bad-reports")));
    }

    [WindowsPhaseTwoFact]
    public void RepeatedPersistedReportsAreByteIdenticalWithUnicodeAndCsvEscapingAndNoLibraryReads()
    {
        using var fixture = new Fixture(); fixture.Pair("日本語,folder", "ñ"); fixture.File("ñ", "x,y.wav", "sample"); fixture.Scan();
        var result = fixture.Run(); var repeat = Path.Combine(fixture.Base, "repeat");
        // Remove the library entirely after sealing: reporting uses persisted evidence.
        Directory.Move(fixture.Root, fixture.Root + "-removed");
        PhaseTwoReportWriter.Write(fixture.Db, result.SessionId, repeat);
        foreach (var file in Directory.GetFiles(fixture.Output))
        {
            var bytes = File.ReadAllBytes(file); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(repeat, Path.GetFileName(file))));
            Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 })); Assert.DoesNotContain((byte)13, bytes);
        }
        Assert.Contains("\"x,y.wav\"", File.ReadAllText(Path.Combine(fixture.Output, "filesystem_inventory.csv")));
        Assert.Equal(8, fixture.Number("SELECT count(*) FROM report_files"));
        Assert.Equal(2, fixture.Number("SELECT count(*) FROM report_sets WHERE status='COMPLETED'"));
        Assert.Equal(result.ManifestSha256, fixture.Rows("SELECT final_evidence_digest FROM sessions").Single()[0]);
    }

    [WindowsPhaseTwoFact]
    public void UnsafeDestinationsFailBeforeAnyChartRead()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); var files = new InstrumentedFiles();
        Assert.Throws<InvalidOperationException>(() => fixture.Run(files, destination: fixture.Source));
        Assert.Throws<InvalidOperationException>(() => fixture.Run(files, destination: fixture.Path("unsafe.db")));
        Assert.Throws<InvalidOperationException>(() => fixture.Run(files, output: fixture.Base));
        Assert.Throws<InvalidOperationException>(() => fixture.Run(files, output: fixture.Path("reports")));
        Assert.Empty(files.Reads); Assert.False(File.Exists(fixture.Db));
    }

    [WindowsPhaseTwoFact]
    public void AlternateStreamDestinationCannotModifyUnrelatedSentinel()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var sentinel = Path.Combine(fixture.Base, "sentinel.db"); File.WriteAllText(sentinel, "unrelated bytes");
        var before = File.ReadAllBytes(sentinel); var files = new InstrumentedFiles();
        Assert.Throws<ArgumentException>(() => fixture.Run(files, destination: sentinel + ":phase2.db"));
        Assert.Equal(before, File.ReadAllBytes(sentinel)); Assert.Empty(files.Reads);
    }

    [WindowsPhaseTwoTheory]
    [InlineData("empty")]
    [InlineData("foreign")]
    [InlineData("sidecar")]
    [InlineData("schema")]
    public void InvalidPhaseTwoOwnershipAndSidecarsAreNotRewritten(string problem)
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        if (problem == "empty") File.WriteAllBytes(fixture.Db, []);
        else if (problem == "foreign") fixture.Sql(fixture.Db, "CREATE TABLE song(x TEXT)");
        else if (problem == "sidecar") Directory.CreateDirectory(fixture.Db + "-wal");
        else { fixture.Run(); fixture.Sql(fixture.Db, "PRAGMA user_version=2"); }
        var before = File.Exists(fixture.Db) ? File.ReadAllBytes(fixture.Db) : null;
        Assert.ThrowsAny<Exception>(() => fixture.Run(output: Path.Combine(fixture.Base, "second")));
        if (before is not null) Assert.Equal(before, File.ReadAllBytes(fixture.Db));
    }

    [WindowsPhaseTwoFact]
    public void CancellationCannotSealOrPublishPartialEvidence()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); using var cancel = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => new PhaseTwoPreflightRunner(hooks: new() { BeforeSeal = cancel.Cancel }).Run(fixture.Options(), cancel.Token));
        Assert.Equal("CANCELLED", fixture.Rows("SELECT state FROM sessions").Single()[0]);
        Assert.Null(fixture.Rows("SELECT final_evidence_digest FROM sessions").Single()[0]); Assert.False(Directory.Exists(fixture.Output));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new PhaseTwoPreflightRunner().Run(fixture.Options(), cancelled.Token));
    }

    [WindowsPhaseTwoFact]
    public void ReportFailureHasReceiptAndDoesNotChangeCompletedSeal()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); var result = fixture.Run();
        var output = Path.Combine(fixture.Base, "failed-report");
        Assert.Throws<IOException>(() => PhaseTwoReportWriter.Write(fixture.Db, result.SessionId, output, hooks: new()
        {
            BeforeReportFile = name => { if (name == "filesystem_inventory.csv") throw new IOException("Injected report write failure"); }
        }));
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM report_sets WHERE status='FAILED'"));
        Assert.Equal(1, fixture.Number("SELECT count(*) FROM sessions WHERE state='COMPLETED'"));
        Assert.Equal(result.ManifestSha256, fixture.Rows("SELECT final_evidence_digest FROM sessions").Single()[0]);
    }

    [WindowsPhaseTwoFact]
    public void SealGuardsRejectUpdatesDeletesAndCrossSessionJoins()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); fixture.Run();
        Assert.Throws<SqliteException>(() => fixture.Sql(fixture.Db, "UPDATE preflight_entries SET size=99 WHERE kind='REGULAR_FILE'"));
        Assert.Throws<SqliteException>(() => fixture.Sql(fixture.Db, "DELETE FROM candidate_components"));
        Assert.Throws<SqliteException>(() => fixture.Sql(fixture.Db, "INSERT INTO source_exact_edges SELECT * FROM source_exact_edges"));
        Assert.Equal(0, fixture.Number("SELECT count(*) FROM pragma_foreign_key_check"));
        // The exact-edge SQL CHECK must reject NULL or an inexact triple in a running session.
        fixture.Sql(fixture.Db, "INSERT INTO sessions(stage,state,started_utc,tool_version,runtime_build_sha,source_component_count) VALUES('PREFLIGHT','RUNNING','x','x','x',0)");
        Assert.Throws<SqliteException>(() => fixture.Sql(fixture.Db, "INSERT INTO component_memberships VALUES(2,1,1)"));
    }

    [WindowsPhaseTwoFact]
    public void CanonicalPreflightIsIndependentOfCultureAndInsertionIdsAndDetectsTampering()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan(); var result = fixture.Run();
        using (var db = PhaseTwoDatabase.Open(fixture.Db, false))
        {
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Assert.Equal(result.ManifestSha256, PhaseTwoManifest.Preflight(db, result.SessionId)); }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        var clone = Path.Combine(fixture.Base, "clone.db"); File.Copy(fixture.Db, clone);
        fixture.Sql(clone, "DROP TRIGGER seal_source_diagnostics_INSERT; DROP TRIGGER seal_preflight_entries_UPDATE; DROP TRIGGER seal_physical_files_UPDATE; DROP TRIGGER seal_observation_memberships_UPDATE; DROP TRIGGER seal_diagnostics_UPDATE;");
        fixture.Sql(clone, "PRAGMA foreign_keys=OFF; UPDATE preflight_entries SET entry_id=entry_id+10000; UPDATE physical_files SET entry_id=entry_id+10000; UPDATE observation_memberships SET entry_id=entry_id+10000; UPDATE diagnostics SET entry_id=entry_id+10000;");
        using (var connection = PhaseTwoDatabase.Connect(clone, SqliteOpenMode.ReadOnly))
        {
            // Bypass normal ownership validation ONLY in this deliberate tampering fixture.
            using var db = (PhaseTwoDatabase)Activator.CreateInstance(typeof(PhaseTwoDatabase), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [clone, connection], CultureInfo.InvariantCulture)!;
            Assert.Equal(result.ManifestSha256, PhaseTwoManifest.Preflight(db, result.SessionId));
        }
        fixture.Sql(clone, "UPDATE preflight_entries SET original_path=original_path || 'x' WHERE entry_id=(SELECT min(entry_id) FROM preflight_entries)");
        Assert.Throws<InvalidOperationException>(() => PhaseTwoReportWriter.Write(clone, result.SessionId, Path.Combine(fixture.Base, "tampered")));
    }

    [WindowsPhaseTwoFact]
    public void NativeHardlinkAndSubstAliasesCannotBypassDestinationSafety()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var alias = Path.Combine(fixture.Base, "alias.db"); NativeCommand("cmd.exe", $"/c mklink /H \"{alias}\" \"{fixture.Source}\"");
        Assert.Throws<InvalidOperationException>(() => fixture.Run(destination: alias));
        var drive = Enumerable.Range('P', 10).Select(i => ((char)i) + ":").First(d => !Directory.Exists(d + "\\"));
        NativeCommand("subst.exe", $"{drive} \"{fixture.Root}\"");
        try { Assert.Throws<InvalidOperationException>(() => fixture.Run(destination: drive + "\\unsafe.db")); }
        finally { NativeCommand("subst.exe", drive + " /D"); }
        Assert.False(File.Exists(fixture.Path("unsafe.db")));
    }

    [WindowsPhaseTwoFact]
    public void ReparseDestinationAliasIsRejectedBeforeWritesAndNativeVolumeIdentityIsCanonical()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var alias = Path.Combine(fixture.Base, "library-alias"); NativeCommand("cmd.exe", $"/c mklink /J \"{alias}\" \"{fixture.Root}\"");
        try
        {
            var files = new InstrumentedFiles();
            Assert.Throws<ReparsePointException>(() => fixture.Run(files, destination: Path.Combine(alias, "unsafe.db")));
            Assert.Empty(files.Reads); Assert.False(File.Exists(fixture.Path("unsafe.db")));
            Assert.StartsWith(@"\\?\VOLUME{", PhaseTwoSafety.Resolve(fixture.Root));
        }
        finally { Directory.Delete(alias); }
    }

    [WindowsPhaseTwoFact]
    public void GlobalWriterExclusionRefusesConcurrentPreflightAndExistingOwnedHardlinkAliases()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        using (PhaseTwoWriter.Acquire(fixture.Db))
        {
            var files = new InstrumentedFiles();
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => fixture.Run(files))).GetAwaiter().GetResult();
            Assert.Empty(files.Reads); Assert.False(File.Exists(fixture.Db));
        }
        fixture.Run();
        var alias = Path.Combine(fixture.Base, "owned-alias.db"); NativeCommand("cmd.exe", $"/c mklink /H \"{alias}\" \"{fixture.Db}\"");
        Assert.Throws<InvalidOperationException>(() => PhaseTwoReportWriter.Write(fixture.Db, 1, Path.Combine(fixture.Base, "aliased-report")));
    }

    [WindowsPhaseTwoFact]
    public void ReparseDirectoryAndChartTargetsAreNeverFollowed()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.Scan();
        var target = Path.Combine(fixture.Base, "external"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "secret.bms"), "not authorized");
        var junction = fixture.Path("A", "linked"); NativeCommand("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"");
        try
        {
            var instrument = new InstrumentedFiles(); var result = fixture.Run(instrument); Assert.Equal(1, result.RejectedComponents);
            Assert.DoesNotContain(instrument.Reads, p => PathPolicy.Contains(target, p)); Assert.Equal(1, fixture.Number("SELECT count(*) FROM preflight_entries WHERE kind='REPARSE'"));
        }
        finally { Directory.Delete(junction); }
        using var chartFixture = new Fixture(); chartFixture.Pair(); chartFixture.Scan();
        var chartPath = chartFixture.Path("A", "one.BMS"); File.Delete(chartPath);
        // A directory junction with a chart filename is still an unsafe direct entry.
        NativeCommand("cmd.exe", $"/c mklink /J \"{chartPath}\" \"{target}\"");
        try { Assert.Equal(1, chartFixture.Run().RejectedComponents); Assert.Equal(1, chartFixture.Number("SELECT count(*) FROM chart_revalidations WHERE read_status='REPARSE' AND fresh_sha256 IS NULL")); }
        finally { Directory.Delete(chartPath); }
    }

    [WindowsPhaseTwoFact]
    public void SourceBytesAndMtimesIncludingDatabaseRemainUnchanged()
    {
        using var fixture = new Fixture(); fixture.Pair(); fixture.File("A", "bga.zip", "opaque archive"); fixture.Scan();
        var before = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Append(fixture.Source)
            .ToDictionary(p => p, p => (SHA256.HashData(File.ReadAllBytes(p)), File.GetLastWriteTimeUtc(p)));
        fixture.Run();
        foreach (var (path, facts) in before) { Assert.Equal(facts.Item1, SHA256.HashData(File.ReadAllBytes(path))); Assert.Equal(facts.Item2, File.GetLastWriteTimeUtc(path)); }
        Assert.False(File.Exists(fixture.Source + "-journal")); Assert.False(File.Exists(fixture.Source + "-wal"));
    }

    [Fact]
    public void CanonicalTokenGoldenVectorHasBigEndianLengthsNullAndUnicode()
    {
        // Independent byte specification: d; group g; one row; three fields;
        // names a/b/c; NULL; empty string; U+00E9 as C3 A9.
        var hex = "0000000164000000016700000001310000000133000000016100000001620000000163ffffffff0000000000000002c3a9";
        var expected = Convert.ToHexString(SHA256.HashData(Convert.FromHexString(hex))).ToLowerInvariant();
        var actual = PhaseTwoCanonicalEncoding.Hash("d", [("g", ["a", "b", "c"], new[] { new string?[] { null, "", "é" } })]);
        Assert.Equal(expected, actual);
    }

    private static void NativeCommand(string executable, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true })!;
        process.WaitForExit(); Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd());
    }
    private sealed class InstrumentedFiles : IPhaseTwoFileSystem
    {
        private readonly PhaseTwoFileSystem real = new();
        public List<string> Reads { get; } = [];
        public Func<string, bool>? MetadataFailure { get; init; }
        public Func<string, bool>? ChildrenFailure { get; init; }
        public Func<string, PhaseTwoChartRead?>? ReadOverride { get; init; }
        public PhaseTwoEntry Metadata(string path) => MetadataFailure?.Invoke(path) == true ? throw new IOException("Injected metadata denial") : real.Metadata(path);
        public IEnumerable<string> Children(string path)
        {
            if (ChildrenFailure?.Invoke(path) == true) { yield return Path.Combine(path, "partial.wav"); throw new IOException("Injected lazy enumeration denial"); }
            foreach (var item in real.Children(path)) yield return item;
        }
        public PhaseTwoChartRead ReadChart(string path, CancellationToken token) { Reads.Add(path); return ReadOverride?.Invoke(path) ?? real.ReadChart(path, token); }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string parent = System.IO.Path.Combine(AppContext.BaseDirectory, "phase2-fixtures");
        public string Base { get; }
        public string Root { get; }
        public string Source => System.IO.Path.Combine(Base, "audit.db");
        public string Db => System.IO.Path.Combine(Base, "phase2.db");
        public string Output => System.IO.Path.Combine(Base, "reports");
        public Fixture() { Base = System.IO.Path.Combine(parent, Guid.NewGuid().ToString("N")); Root = System.IO.Path.Combine(Base, "songs"); Directory.CreateDirectory(Root); }
        public string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);
        public string File(string folder, string name, string bytes) { Directory.CreateDirectory(Path(folder)); var path = Path(folder, name); System.IO.File.WriteAllText(path, bytes, new UTF8Encoding(false)); return path; }
        public void Pair(string a = "A", string b = "B", string bytes = "xxxx") { File(a, "one.BMS", bytes); File(b, "one.BMS", bytes); }
        public void Scan() => new ChartScanner().Scan(new(Source, [Root]));
        public PhaseTwoPreflightOptions Options(long sourceScan = 1, string? destination = null, string? output = null, string[]? components = null) => new(Source, sourceScan, destination ?? Db, output ?? Output, components);
        public PhaseTwoPreflightResult Run(IPhaseTwoFileSystem? files = null, PhaseTwoTestHooks? hooks = null, long sourceScan = 1, string? destination = null, string? output = null, string[]? components = null) => new PhaseTwoPreflightRunner(files, hooks).Run(Options(sourceScan, destination, output, components));
        public void Sql(string db, string sql) { using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString()); connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
        public object?[][] Rows(string sql, params object?[] values) { using var connection = PhaseTwoDatabase.Connect(Db, SqliteOpenMode.ReadOnly); using var command = connection.CreateCommand(); command.CommandText = sql; for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$" + i, values[i] ?? DBNull.Value); using var reader = command.ExecuteReader(); var rows = new List<object?[]>(); while (reader.Read()) rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray()); return rows.ToArray(); }
        public long Number(string sql, params object?[] values) => Convert.ToInt64(Rows(sql, values).Single()[0]);
        public JsonDocument Summary() => JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Output, "session_summary.json")));
        public void Dispose() { if (!PathPolicy.Contains(parent, Base) || Base == parent) throw new InvalidOperationException("Unsafe fixture cleanup"); Directory.Delete(Base, true); }
    }
}
