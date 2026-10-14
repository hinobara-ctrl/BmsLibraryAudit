using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseTwoContentIntegrityTests
{
    // Transcribed from the normative four-group CONTENT field table. This
    // oracle uses a whole byte vector and an independent network-order writer.
    private static readonly string[] Names = ["content_header", "content_component_outcomes", "content_file_results", "content_diagnostics"];
    private static readonly string[][] Fields = [
        "accepted_preflight_sha256,contract_version,phase2_schema_version,tool_version,runtime_build_sha,stage,state,started_utc,completed_utc,absolute_comparison_mode,relative_comparison_mode".Split(','),
        "component_key,outcome,reason_code,reason,required_physical_file_count,trusted_successful_hash_count,failed_untrusted_file_count,affected_observation_root_count,comparison_eligible,coherent,sealed".Split(','),
        "physical_path_key,status,reason_code,sha256,verified_size,before_size,before_mtime_ticks,before_native_identity,after_size,after_mtime_ticks,after_native_identity,bytes_read,attempts,read_started_utc,read_ended_utc".Split(','),
        "component_key,root_key,entry_key,physical_path_key,gate,severity,code,original_path,normalized_path,attempt,message".Split(',')];

    [Fact]
    public void NormativeContentGoldenVectorIncludesAllGroupsNullAndUtf8()
    {
        string?[][][] rows = [
            [[new string('a', 64), "1.0-draft", "1", "0.1.0", new string('b', 40), "CONTENT", "COMPLETED", "2026-10-14T00:00:00.0000000+00:00", "2026-10-14T00:00:01.0000000+00:00", "windows-ordinal-ignore-case", "relative-ordinal-spelling-v1"]],
            [[new string('c', 64), "COMPLETE", "CONTENT_COMPLETE", "all read", "2", "2", "0", "0", "true", "true", "true"]],
            [[@"C:\FIXTURE\A\chart.bms", "SUCCESS", "CONTENT_READ_SUCCESS", new string('d', 64), "4", "4", "123", "native", "4", "123", "native", "4", "1", "2026-10-14T00:00:00.0000000+00:00", "2026-10-14T00:00:01.0000000+00:00"]],
            [[new string('c', 64), null, null, @"C:\FIXTURE\A\chart.bms", "CONTENT", "INFO", "VERIFIED", @"C:\fixture\A\chart.bms", @"C:\FIXTURE\A\chart.bms", "1", "verified, ñ"]]];
        // Frozen result from the independent PowerShell byte-vector generator.
        const string expected = "8c4173f9c94786cbb6c90b2b688c6b953cea675f586d4124fb5bf213356a65eb";
        Assert.Equal(expected, IndependentDigest(rows));
        Assert.Equal(expected, PhaseTwoCanonicalEncoding.Hash("BmsLibraryAudit.Phase2.Content.v1", Names.Select((name, i) => (name, Fields[i], (IEnumerable<string?[]>)rows[i]))));
    }

    [WindowsPhaseTwoFact]
    public void ActualContentSealMatchesIndependentOracleAndReorderedInsertions()
    {
        using var f = new Fixture(); f.Pair(); f.File("A", "x.wav", "aaaa"); f.File("B", "y.wav", "aaaa"); f.Scan(); var p = f.Run();
        var checkedOrder = false;
        var result = new PhaseTwoContentRunner(hooks: new() { BeforeFinalVerification = () => {
            using var db = PhaseTwoDatabase.Open(f.Db, true); var before = PhaseTwoManifest.Content(db, 2);
            db.Begin(); db.Execute("CREATE TEMP TABLE reverse_hashes AS SELECT * FROM content_hashes WHERE content_session_id=2 ORDER BY physical_file_id DESC");
            db.Execute("DELETE FROM content_hashes WHERE content_session_id=2"); db.Execute("INSERT INTO content_hashes SELECT * FROM reverse_hashes ORDER BY physical_file_id DESC");
            Assert.Equal(before, PhaseTwoManifest.Content(db, 2)); db.Commit(); checkedOrder = true;
        } }).Run(new(f.Db, p.SessionId, p.ManifestSha256!, Path.Combine(f.Base, "content")));
        Assert.True(checkedOrder);
        using var read = PhaseTwoDatabase.Open(f.Db, false);
        Assert.Equal(result.ManifestSha256, IndependentDigest(ObservedRows(read, result.SessionId)));
    }

    [WindowsPhaseTwoFact]
    public void ResultAndDiagnosticTamperingChangeSealAndComparisonsAreIndependentlyChecked()
    {
        using var f = new Fixture(); f.Pair(); f.Scan(); var p = f.Run(); var result = new PhaseTwoContentRunner().Run(new(f.Db, p.SessionId, p.ManifestSha256!, Path.Combine(f.Base, "content")));
        var clone = Path.Combine(f.Base, "tampered.db"); File.Copy(f.Db, clone);
        f.Sql(clone, "DROP TRIGGER seal_content_hashes_UPDATE; DROP TRIGGER seal_diagnostics_INSERT; DROP TRIGGER seal_comparisons_UPDATE;");
        // Deliberately bypass schema validation only in this owned corruption fixture.
        using var connection = PhaseTwoDatabase.Connect(clone, SqliteOpenMode.ReadWrite);
        using var db = (PhaseTwoDatabase)Activator.CreateInstance(typeof(PhaseTwoDatabase), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [clone, connection], CultureInfo.InvariantCulture)!;
        var seal = PhaseTwoManifest.Content(db, result.SessionId);
        db.Execute("UPDATE content_hashes SET sha256=$0 WHERE physical_file_id=(SELECT min(physical_file_id) FROM content_hashes)", new string('a', 64));
        Assert.NotEqual(seal, PhaseTwoManifest.Content(db, result.SessionId)); Assert.Throws<InvalidOperationException>(() => PhaseTwoManifest.VerifyContent(db, result.SessionId));
        var changed = PhaseTwoManifest.Content(db, result.SessionId); db.Diagnostic(result.SessionId, null, null, null, "TEST_TAMPER", f.Db, "changed diagnostic");
        Assert.NotEqual(changed, PhaseTwoManifest.Content(db, result.SessionId));
        db.Execute("UPDATE comparisons SET same_path_same_bytes=same_path_same_bytes+1");
        var component = db.Number("SELECT component_id FROM candidate_components");
        Assert.Throws<InvalidOperationException>(() => PhaseTwoContentComparison.Verify(db, result.SessionId, p.SessionId, component, true, default));
    }

    private static string?[][][] ObservedRows(PhaseTwoDatabase db, long session)
    {
        var queries = new[] {
            "SELECT accepted_manifest_sha256,'1.0-draft',1,tool_version,runtime_build_sha,stage,state,started_utc,completed_utc,'windows-ordinal-ignore-case','relative-ordinal-spelling-v1' FROM sessions WHERE session_id=$0",
            "SELECT c.component_key,o.outcome,o.reason_code,o.reason,o.required_physical_file_count,o.trusted_successful_hash_count,o.failed_untrusted_file_count,o.affected_observation_root_count,o.comparison_eligible,o.coherent,o.sealed FROM content_component_outcomes o JOIN candidate_components c ON c.component_id=o.component_id WHERE o.content_session_id=$0 ORDER BY c.component_key COLLATE ORDINAL",
            "SELECT e.entry_key,h.status,h.reason_code,h.sha256,h.verified_size,h.before_size,h.before_mtime_ticks,h.before_native_identity,h.after_size,h.after_mtime_ticks,h.after_native_identity,h.bytes_read,h.attempts,h.read_started_utc,h.read_ended_utc FROM content_hashes h JOIN physical_files p ON p.physical_file_id=h.physical_file_id JOIN preflight_entries e ON e.entry_id=p.entry_id WHERE h.content_session_id=$0 ORDER BY e.entry_key COLLATE ORDINAL",
            "SELECT c.component_key,r.root_key,e.entry_key,pe.entry_key,d.gate,d.severity,d.code,d.original_path,d.normalized_path,d.attempt,d.message FROM diagnostics d LEFT JOIN candidate_components c ON c.component_id=d.component_id LEFT JOIN observation_roots r ON r.observation_root_id=d.root_id LEFT JOIN preflight_entries e ON e.entry_id=d.entry_id LEFT JOIN physical_files p ON p.physical_file_id=d.physical_file_id LEFT JOIN preflight_entries pe ON pe.entry_id=p.entry_id WHERE d.session_id=$0 ORDER BY c.component_key COLLATE ORDINAL,r.root_key COLLATE ORDINAL,d.normalized_path COLLATE ORDINAL,d.code COLLATE ORDINAL,d.attempt COLLATE ORDINAL,d.message COLLATE ORDINAL,d.severity COLLATE ORDINAL,d.gate COLLATE ORDINAL,e.entry_key COLLATE ORDINAL,pe.entry_key COLLATE ORDINAL,d.original_path COLLATE ORDINAL" };
        return queries.Select((sql, g) => db.Rows(sql, session).Select(row => row.Select((v, i) => v is null ? null : g == 1 && i >= 8 ? Convert.ToInt64(v) == 0 ? "false" : "true" : Convert.ToString(v, CultureInfo.InvariantCulture)).ToArray()).ToArray()).ToArray();
    }
    private static string IndependentDigest(string?[][][] rows)
    {
        using var bytes = new MemoryStream(); Token("BmsLibraryAudit.Phase2.Content.v1");
        for (var g = 0; g < Names.Length; g++)
        {
            Token(Names[g]); Token(rows[g].Length.ToString(CultureInfo.InvariantCulture)); Token(Fields[g].Length.ToString(CultureInfo.InvariantCulture));
            foreach (var field in Fields[g]) Token(field);
            foreach (var row in rows[g]) { Assert.Equal(Fields[g].Length, row.Length); foreach (var value in row) Token(value); }
        }
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
        void Token(string? value)
        {
            if (value is null) { bytes.Write([255, 255, 255, 255]); return; }
            var payload = Encoding.UTF8.GetBytes(value); var length = (uint)payload.Length;
            bytes.Write([(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length]); bytes.Write(payload);
        }
    }
}
