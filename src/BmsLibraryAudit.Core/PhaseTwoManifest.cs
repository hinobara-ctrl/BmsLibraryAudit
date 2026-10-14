using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BmsLibraryAudit.Core;

// Explicit contract field/group order. SQLite supplies external sorting; no
// inventory-sized arrays or JSON serialization are used to build the seal.
public static class PhaseTwoCanonicalEncoding
{
    public static string Hash(string domain, IEnumerable<(string Name, string[] Fields, IEnumerable<string?[]> Rows)> groups)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Token(domain);
        foreach (var group in groups)
        {
            Token(group.Name); Token(group.Rows.LongCount().ToString(CultureInfo.InvariantCulture));
            Token(group.Fields.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var field in group.Fields) Token(field);
            foreach (var row in group.Rows)
            {
                if (row.Length != group.Fields.Length) throw new InvalidOperationException("Canonical field count mismatch.");
                foreach (var value in row) Token(value);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        void Token(string? value)
        {
            Span<byte> prefix = stackalloc byte[4];
            var bytes = value is null ? null : Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteUInt32BigEndian(prefix, bytes is null ? uint.MaxValue : checked((uint)bytes.Length));
            hash.AppendData(prefix); if (bytes is not null) hash.AppendData(bytes);
        }
    }
}

internal static class PhaseTwoManifest
{
    private sealed record Group(string Name, string Fields, string Select, string Order);
    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal)
        { "active", "configured", "present", "counts_are_lower_bounds", "captured_member", "observed_member", "is_chart", "comparison_eligible", "coherent", "sealed" };
    private static readonly Group[] Groups =
    [
        new("header", "contract_version,phase2_schema_version,tool_version,runtime_build_sha,stage,state,started_utc,completed_utc,absolute_comparison_mode,relative_comparison_mode",
            "SELECT '1.0-draft',1,tool_version,runtime_build_sha,stage,'COMPLETED',started_utc,completed_utc,'windows-ordinal-ignore-case','relative-ordinal-spelling-v1' FROM sessions WHERE session_id=$0", ""),
        new("source_provenance", "repository_baseline_sha,phase1_implementation_sha,source_db_original_path,source_db_normalized_path,source_db_native_identity,source_db_size,source_db_sha256,source_db_before_mtime_ticks,source_db_after_mtime_ticks,source_application_id,source_schema_version,source_comparison_mode,source_scan_id,source_scan_started_utc,source_scan_completed_utc,admission_policy",
            "SELECT repository_baseline_sha,phase1_implementation_sha,source_db_original_path,source_db_normalized_path,source_db_native_identity,source_db_size,source_db_sha256,source_db_before_mtime_ticks,source_db_after_mtime_ticks,source_application_id,source_schema_version,source_comparison_mode,source_scan_id,source_scan_started_utc,source_scan_completed_utc,admission_policy FROM source_snapshots WHERE snapshot_id=$0", ""),
        new("source_roots", "source_root_id,original_path,normalized_path,active,configured", "SELECT source_root_id,original_path,normalized_path,active,configured FROM source_library_roots WHERE snapshot_id=$0", "normalized_path"),
        new("source_charts", "normalized_path,source_root_id,original_path,parent_folder,filename,extension,size,mtime_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status",
            "SELECT normalized_path,source_root_id,original_path,parent_folder,filename,extension,size,mtime_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status FROM source_charts WHERE snapshot_id=$0", "normalized_path"),
        new("source_diagnostics", "severity,code,path,message", "SELECT severity,code,path,message FROM source_diagnostics WHERE snapshot_id=$0", "path,code,message,severity"),
        new("selected_components", "component_key,source_component_label,distinct_chart_count,distinct_chart_set_digest,member_count,outcome,reason",
            "SELECT component_key,source_component_label,distinct_chart_count,distinct_chart_set_digest,member_count,outcome,reason FROM candidate_components WHERE preflight_id=$0", "component_key"),
        new("observation_roots", "root_key,original_path,source_root_id,chart_state,inventory_state,regular_file_count,directory_count,logical_bytes,counts_are_lower_bounds",
            "SELECT root_key,original_path,source_root_id,chart_state,inventory_state,regular_file_count,directory_count,logical_bytes,counts_are_lower_bounds FROM observation_roots WHERE preflight_id=$0", "root_key"),
        new("exact_edges", "component_key,root_a_key,root_b_key,charts_a,charts_b,common,physical_files_a,physical_files_b",
            "SELECT c.component_key,a.root_key AS root_a_key,b.root_key AS root_b_key,e.charts_a,e.charts_b,e.common,e.physical_files_a,e.physical_files_b FROM source_exact_edges e JOIN candidate_components c ON c.component_id=e.component_id AND c.preflight_id=e.preflight_id JOIN observation_roots a ON a.observation_root_id=e.root_a_id AND a.preflight_id=e.preflight_id JOIN observation_roots b ON b.observation_root_id=e.root_b_id AND b.preflight_id=e.preflight_id WHERE e.preflight_id=$0", "component_key,root_a_key,root_b_key"),
        new("component_memberships", "component_key,root_key", "SELECT c.component_key,r.root_key FROM component_memberships m JOIN candidate_components c ON c.component_id=m.component_id AND c.preflight_id=m.preflight_id JOIN observation_roots r ON r.observation_root_id=m.observation_root_id AND r.preflight_id=m.preflight_id WHERE m.preflight_id=$0", "component_key,root_key"),
        new("chart_revalidations", "chart_key,observed_filename,before_size,before_mtime_ticks,after_size,after_mtime_ticks,fresh_sha256,bytes_read,attempts,read_status,match_status",
            "SELECT chart_key,observed_filename,before_size,before_mtime_ticks,after_size,after_mtime_ticks,fresh_sha256,bytes_read,attempts,read_status,match_status FROM chart_revalidations WHERE preflight_id=$0", "chart_key"),
        new("direct_chart_memberships", "root_key,chart_key,captured_member,observed_member", "SELECT r.root_key,m.chart_key,m.captured_member,m.observed_member FROM direct_chart_memberships m JOIN observation_roots r ON r.observation_root_id=m.observation_root_id AND r.preflight_id=m.preflight_id WHERE m.preflight_id=$0", "root_key,chart_key"),
        new("entries", "entry_key,original_path,kind,size,mtime_ticks,attributes,extension,is_chart,inventory_status,readability,native_identity", "SELECT entry_key,original_path,kind,size,mtime_ticks,attributes,extension,is_chart,inventory_status,readability,native_identity FROM preflight_entries WHERE preflight_id=$0", "entry_key"),
        new("observation_memberships", "root_key,entry_key,relative_path,relative_path_key", "SELECT r.root_key,e.entry_key,m.relative_path,m.relative_path_key FROM observation_memberships m JOIN observation_roots r ON r.observation_root_id=m.observation_root_id AND r.preflight_id=m.preflight_id JOIN preflight_entries e ON e.entry_id=m.entry_id AND e.preflight_id=m.preflight_id WHERE m.preflight_id=$0", "root_key,relative_path_key"),
        new("preflight_diagnostics", "component_key,root_key,entry_key,gate,severity,code,original_path,normalized_path,attempt,message",
            "SELECT c.component_key,r.root_key,e.entry_key,d.gate,d.severity,d.code,d.original_path,d.normalized_path,d.attempt,d.message FROM diagnostics d LEFT JOIN candidate_components c ON c.component_id=d.component_id AND c.preflight_id=d.session_id LEFT JOIN observation_roots r ON r.observation_root_id=d.root_id AND r.preflight_id=d.session_id LEFT JOIN preflight_entries e ON e.entry_id=d.entry_id AND e.preflight_id=d.session_id WHERE d.session_id=$0", "component_key,root_key,normalized_path,code,attempt,message,severity,gate,entry_key,original_path")
    ];
    private static readonly Group[] ContentGroups =
    [
        new("content_header", "accepted_preflight_sha256,contract_version,phase2_schema_version,tool_version,runtime_build_sha,stage,state,started_utc,completed_utc,absolute_comparison_mode,relative_comparison_mode",
            "SELECT accepted_manifest_sha256,'1.0-draft',1,tool_version,runtime_build_sha,stage,'COMPLETED',started_utc,completed_utc,'windows-ordinal-ignore-case','relative-ordinal-spelling-v1' FROM sessions WHERE session_id=$0", ""),
        new("content_component_outcomes", "component_key,outcome,reason_code,reason,required_physical_file_count,trusted_successful_hash_count,failed_untrusted_file_count,affected_observation_root_count,comparison_eligible,coherent,sealed",
            "SELECT c.component_key,o.outcome,o.reason_code,o.reason,o.required_physical_file_count,o.trusted_successful_hash_count,o.failed_untrusted_file_count,o.affected_observation_root_count,o.comparison_eligible,o.coherent,o.sealed FROM content_component_outcomes o JOIN candidate_components c ON c.preflight_id=o.preflight_id AND c.component_id=o.component_id WHERE o.content_session_id=$0", "component_key"),
        new("content_file_results", "physical_path_key,status,reason_code,sha256,verified_size,before_size,before_mtime_ticks,before_native_identity,after_size,after_mtime_ticks,after_native_identity,bytes_read,attempts,read_started_utc,read_ended_utc",
            "SELECT e.entry_key AS physical_path_key,h.status,h.reason_code,h.sha256,h.verified_size,h.before_size,h.before_mtime_ticks,h.before_native_identity,h.after_size,h.after_mtime_ticks,h.after_native_identity,h.bytes_read,h.attempts,h.read_started_utc,h.read_ended_utc FROM content_hashes h JOIN physical_files p ON p.preflight_id=h.preflight_id AND p.physical_file_id=h.physical_file_id JOIN preflight_entries e ON e.preflight_id=p.preflight_id AND e.entry_id=p.entry_id WHERE h.content_session_id=$0", "physical_path_key"),
        new("content_diagnostics", "component_key,root_key,entry_key,physical_path_key,gate,severity,code,original_path,normalized_path,attempt,message",
            "SELECT c.component_key,r.root_key,e.entry_key,pe.entry_key AS physical_path_key,d.gate,d.severity,d.code,d.original_path,d.normalized_path,d.attempt,d.message FROM diagnostics d LEFT JOIN candidate_components c ON c.preflight_id=d.preflight_id AND c.component_id=d.component_id LEFT JOIN observation_roots r ON r.preflight_id=d.preflight_id AND r.observation_root_id=d.root_id LEFT JOIN preflight_entries e ON e.preflight_id=d.preflight_id AND e.entry_id=d.entry_id LEFT JOIN physical_files p ON p.preflight_id=d.preflight_id AND p.physical_file_id=d.physical_file_id LEFT JOIN preflight_entries pe ON pe.preflight_id=p.preflight_id AND pe.entry_id=p.entry_id WHERE d.session_id=$0", "component_key,root_key,normalized_path,code,attempt,message,severity,gate,entry_key,physical_path_key,original_path")
    ];
    internal static string Component(IEnumerable<string> roots, IEnumerable<string> hashes) => PhaseTwoCanonicalEncoding.Hash(
        "BmsLibraryAudit.Phase2.Component.v1",
        [("member_roots", ["normalized_path"], roots.Order(StringComparer.Ordinal).Select(p => new string?[] { p })),
         ("chart_hashes", ["sha256"], hashes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(h => new string?[] { h }))]);
    internal static string ChartSet(IEnumerable<string> hashes) => PhaseTwoCanonicalEncoding.Hash("BmsLibraryAudit.Phase2.ChartSet.v1",
        [("chart_hashes", ["sha256"], hashes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(h => new string?[] { h }))]);
    internal static string Source(PhaseTwoDatabase db, long session) => Compute(db, session, "BmsLibraryAudit.Phase2.Source.v1", Groups.Skip(1).Take(4));
    internal static string Preflight(PhaseTwoDatabase db, long session) => Compute(db, session, "BmsLibraryAudit.Phase2.Preflight.v1", Groups);
    internal static string Content(PhaseTwoDatabase db, long session) => Compute(db, session, "BmsLibraryAudit.Phase2.Content.v1", ContentGroups);
    internal static void VerifyContent(PhaseTwoDatabase db, long session, CancellationToken token = default)
    {
        if ((string?)db.Scalar("SELECT state FROM sessions WHERE session_id=$0", session) != "COMPLETED" ||
            (string?)db.Scalar("SELECT stage FROM sessions WHERE session_id=$0", session) != "CONTENT" ||
            (string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", session) != Content(db, session))
            throw new InvalidOperationException("EVIDENCE_INTEGRITY_ERROR: content seal mismatch.");
        PhaseTwoContentRunner.ValidateEvidence(db, session, token);
    }
    private static string Compute(PhaseTwoDatabase db, long session, string domain, IEnumerable<Group> groups) =>
        PhaseTwoCanonicalEncoding.Hash(domain, groups.Select(g => (g.Name, g.Fields.Split(','), CanonicalRows(db, session, g))));
    private static IEnumerable<string?[]> CanonicalRows(PhaseTwoDatabase db, long session, Group group)
    {
        var fields = group.Fields.Split(',');
        var order = group.Order.Length == 0 ? "" : " ORDER BY " + string.Join(',', group.Order.Split(',').Select(f => f + " COLLATE ORDINAL"));
        foreach (var row in db.Rows("SELECT * FROM (" + group.Select + ")" + order, session))
            yield return row.Select((v, i) => v is null ? null : Flags.Contains(fields[i]) ? Convert.ToInt64(v) == 0 ? "false" : "true" :
                v is IFormattable value ? value.ToString(null, CultureInfo.InvariantCulture) : v.ToString()).ToArray();
    }
    internal static void Verify(PhaseTwoDatabase db, long session)
    {
        if ((string?)db.Scalar("SELECT stage FROM sessions WHERE session_id=$0", session) != "PREFLIGHT" ||
            (string?)db.Scalar("SELECT state FROM sessions WHERE session_id=$0", session) != "COMPLETED" ||
            (string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", session) != Preflight(db, session) ||
            (string?)db.Scalar("SELECT captured_evidence_digest FROM source_snapshots WHERE snapshot_id=$0", session) != Source(db, session))
            throw new InvalidOperationException("EVIDENCE_INTEGRITY_ERROR: preflight seal mismatch.");
    }
}
