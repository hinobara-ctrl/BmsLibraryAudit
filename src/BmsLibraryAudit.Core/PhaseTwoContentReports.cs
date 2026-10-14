using System.Text.Json;

namespace BmsLibraryAudit.Core;

internal static class PhaseTwoContentReports
{
    internal const string Notice = "Fresh stable reads in this session's observation window only; not a simultaneous immutable filesystem snapshot. Metadata-preserving edits after a read may escape final metadata checks. OBSERVATION_ROOT is not PACKAGE_ROOT. Byte/tree/multiset equality grants no merge, removal or mutation authority; dependency analysis and fresh independent revalidation remain required.";
    internal static void Write(PhaseTwoDatabase db, long session, long preflight, string state, Func<string, StreamWriter> output, CancellationToken token)
    {
        var completed = state == "COMPLETED";
        var identity = completed ? (string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", session) : null;
        long Count(string sql) => db.Number(sql, session);
        var complete = Count("SELECT count(*) FROM content_component_outcomes WHERE content_session_id=$0 AND outcome='COMPLETE'");
        var incomplete = Count("SELECT count(*) FROM content_component_outcomes WHERE content_session_id=$0 AND outcome='INCOMPLETE'");
        var summary = new
        {
            contract_version = PhaseTwoDatabase.ContractVersion, tool_version = ReportWriter.ToolVersion, schema_version = 1,
            capabilities = PhaseTwoDatabase.Capabilities, schema_revision = PhaseTwoDatabase.SchemaRevision,
            phase = "PHASE_2.READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE", operation = "PHASE_2B.EXACT_CONTENT_EVIDENCE",
            session_id = session, stage = "CONTENT", state, preflight_id = preflight,
            execution = PhaseTwoReportWriter.Objects(db, "SELECT started_utc,completed_utc,runtime_build_sha,accepted_manifest_sha256 FROM sessions WHERE session_id=$0", session).Single(),
            source_provenance = PhaseTwoReportWriter.Objects(db, "SELECT * FROM source_snapshots WHERE snapshot_id=$0", preflight).Single(),
            preflight_manifest_sha256 = db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", preflight), content_manifest_sha256 = identity,
            admitted_component_count = db.Number("SELECT count(*) FROM candidate_components WHERE preflight_id=$0 AND outcome='ADMITTED'", preflight),
            complete_content_component_count = completed ? (long?)complete : null, incomplete_content_component_count = completed ? (long?)incomplete : null,
            comparison_eligible_component_count = completed ? complete : 0, excluded_content_component_count = completed ? (long?)incomplete : null,
            trusted_successful_hash_count = completed ? (long?)Count("SELECT count(*) FROM content_hashes WHERE content_session_id=$0 AND status='SUCCESS'") : null,
            failed_untrusted_content_read_count = completed ? (long?)Count("SELECT count(*) FROM content_hashes WHERE content_session_id=$0 AND status IN ('ERROR','UNSTABLE')") : null,
            provisional_processed_files = completed ? (long?)null : Count("SELECT count(*) FROM content_hashes WHERE content_session_id=$0"),
            observed_read_bytes = Count("SELECT COALESCE(sum(bytes_read),0) FROM content_hashes WHERE content_session_id=$0"),
            byte_counter_scope = "Persisted returned file-read bytes, including retries; excludes metadata, exports and unreturned/in-progress interrupted bytes. Not OS I/O telemetry.",
            comparison_count = completed ? Count("SELECT count(*) FROM comparisons WHERE content_session_id=$0") : 0,
            component_outcomes = completed ? PhaseTwoReportWriter.Objects(db, "SELECT c.component_key,o.outcome,o.reason_code,o.reason,o.required_physical_file_count,o.trusted_successful_hash_count,o.failed_untrusted_file_count,o.affected_observation_root_count,o.comparison_eligible FROM content_component_outcomes o JOIN candidate_components c ON c.preflight_id=o.preflight_id AND c.component_id=o.component_id WHERE o.content_session_id=$0 ORDER BY c.component_key COLLATE ORDINAL", session).ToArray() : [],
            diagnostic_counts = PhaseTwoReportWriter.Objects(db, "SELECT severity,code,count(*) AS count FROM diagnostics WHERE session_id=$0 GROUP BY severity,code ORDER BY severity COLLATE ORDINAL,code COLLATE ORDINAL", session).ToArray(),
            trusted_content_evidence_scope = completed ? "COMPLETE_COMPONENTS_ONLY" : "NONE", phase2b_executed = true,
            session_result_exit_code = completed ? incomplete > 0 ? 2 : 0 : state == "CANCELLED" ? 130 : state == "STALE" ? 2 : 1,
            evidence_notice = Notice, comparison_notice = completed && complete == 0 ? "No trusted component comparisons exist; all required outcomes are INCOMPLETE." : Notice
        };
        using (var writer = output("session_summary.json"))
        { writer.Write(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal)); writer.Write('\n'); }
        using (var writer = output("observation_roots.csv"))
        {
            Csv(writer, "session_id", "preflight_id", "source_snapshot_id", "content_manifest_sha256", "component_key", "root_key", "original_path", "chart_state", "inventory_state", "content_component_outcome", "content_reason_code", "content_reason", "root_content_affected", "failed_dependency_count", "comparison_eligible");
            foreach (var r in db.Rows("""
                SELECT c.component_key,r.root_key,r.original_path,r.chart_state,r.inventory_state,o.outcome,o.reason_code,o.reason,o.comparison_eligible,
                  (SELECT count(*) FROM observation_memberships m JOIN physical_files p ON p.preflight_id=m.preflight_id AND p.entry_id=m.entry_id
                   JOIN content_hashes h ON h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id AND h.content_session_id=$0
                   WHERE m.preflight_id=r.preflight_id AND m.observation_root_id=r.observation_root_id AND h.status IN ('ERROR','UNSTABLE'))
                FROM observation_roots r JOIN component_memberships cm ON cm.preflight_id=r.preflight_id AND cm.observation_root_id=r.observation_root_id
                JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
                LEFT JOIN content_component_outcomes o ON o.content_session_id=$0 AND o.component_id=c.component_id
                WHERE r.preflight_id=$1 AND c.outcome='ADMITTED' ORDER BY c.component_key COLLATE ORDINAL,r.root_key COLLATE ORDINAL
                """, session, preflight))
            {
                token.ThrowIfCancellationRequested();
                Csv(writer, session, preflight, preflight, identity, r[0], r[1], r[2], r[3], r[4], completed ? r[5] : null,
                    completed ? r[6] : null, completed ? r[7] : null, completed ? Convert.ToInt64(r[9]) > 0 : null,
                    completed ? r[9] : null, completed ? Convert.ToInt64(r[8]) != 0 : null);
            }
        }
        using (var writer = output("filesystem_inventory.csv"))
        {
            Csv(writer, "session_id", "preflight_id", "source_snapshot_id", "content_manifest_sha256", "component_key", "root_key", "entry_key", "original_path", "relative_path", "kind", "size", "mtime_ticks", "attributes", "is_chart", "native_identity", "physical_file_id", "content_read_status", "content_reason_code", "content_sha256", "individual_file_fact_trusted", "content_component_outcome", "comparison_eligible");
            foreach (var r in db.Rows("""
                SELECT c.component_key,r.root_key,e.entry_key,e.original_path,m.relative_path,e.kind,e.size,e.mtime_ticks,e.attributes,e.is_chart,e.native_identity,p.physical_file_id,h.status,h.reason_code,h.sha256,o.outcome,o.comparison_eligible
                FROM observation_memberships m JOIN observation_roots r ON r.preflight_id=m.preflight_id AND r.observation_root_id=m.observation_root_id
                JOIN component_memberships cm ON cm.preflight_id=m.preflight_id AND cm.observation_root_id=m.observation_root_id
                JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
                JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id
                LEFT JOIN physical_files p ON p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id
                LEFT JOIN content_hashes h ON h.content_session_id=$0 AND h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
                LEFT JOIN content_component_outcomes o ON o.content_session_id=$0 AND o.component_id=c.component_id
                WHERE m.preflight_id=$1 AND c.outcome='ADMITTED' ORDER BY r.root_key COLLATE ORDINAL,m.relative_path_key COLLATE ORDINAL
                """, session, preflight))
            {
                token.ThrowIfCancellationRequested(); var trusted = completed && (string?)r[12] == "SUCCESS";
                if (r[9] is not null) r[9] = Convert.ToInt64(r[9]) != 0;
                Csv(writer, [session, preflight, preflight, identity, .. r.Take(14), trusted ? r[14] : null, trusted, completed ? r[15] : null, completed ? Convert.ToInt64(r[16]) != 0 : null]);
            }
        }
        using (var writer = output("diagnostics.csv"))
        {
            Csv(writer, "session_id", "preflight_id", "source_snapshot_id", "content_manifest_sha256", "component_key", "root_key", "entry_key", "physical_path_key", "stage", "severity", "code", "original_path", "normalized_path", "attempt", "message");
            foreach (var r in db.Rows("""
                SELECT c.component_key,r.root_key,e.entry_key,pe.entry_key,d.gate,d.severity,d.code,d.original_path,d.normalized_path,d.attempt,d.message
                FROM diagnostics d LEFT JOIN candidate_components c ON c.preflight_id=d.preflight_id AND c.component_id=d.component_id
                LEFT JOIN observation_roots r ON r.preflight_id=d.preflight_id AND r.observation_root_id=d.root_id
                LEFT JOIN preflight_entries e ON e.preflight_id=d.preflight_id AND e.entry_id=d.entry_id
                LEFT JOIN physical_files p ON p.preflight_id=d.preflight_id AND p.physical_file_id=d.physical_file_id
                LEFT JOIN preflight_entries pe ON pe.preflight_id=p.preflight_id AND pe.entry_id=p.entry_id
                WHERE d.session_id=$0 ORDER BY c.component_key COLLATE ORDINAL,r.root_key COLLATE ORDINAL,d.normalized_path COLLATE ORDINAL,d.code COLLATE ORDINAL,d.attempt,d.message COLLATE ORDINAL,d.severity COLLATE ORDINAL,d.gate COLLATE ORDINAL,e.entry_key COLLATE ORDINAL,pe.entry_key COLLATE ORDINAL,d.original_path COLLATE ORDINAL
                """, session)) { token.ThrowIfCancellationRequested(); Csv(writer, [session, preflight, preflight, identity, .. r]); }
        }
        if (!completed) return;
        using (var writer = output("folder_comparisons.csv"))
        {
            Csv(writer, ["session_id", "preflight_id", "source_snapshot_id", "content_manifest_sha256", "component_key", "root_a_key", "root_b_key",
                .. PhaseTwoContentComparison.ValueColumns.Split(',').Take(7), "OBSERVED_TREE_EXACT", "OBSERVED_CONTENT_MULTISET_EXACT", "OBSERVED_NON_CHART_TREE_EXACT", "OBSERVED_NON_CHART_CONTENT_MULTISET_EXACT", "OBSERVED_PATH_CONFLICT", "OBSERVED_FILESET_VARIANT", "entry_differences_json"]);
            foreach (var r in db.Rows("SELECT c.component_key,a.root_key,b.root_key,q.root_a_id,q.root_b_id," + string.Join(',', PhaseTwoContentComparison.ValueColumns.Split(',').Select(x => "q." + x)) + " FROM comparisons q JOIN candidate_components c ON c.preflight_id=q.preflight_id AND c.component_id=q.component_id JOIN observation_roots a ON a.preflight_id=q.preflight_id AND a.observation_root_id=q.root_a_id JOIN observation_roots b ON b.preflight_id=q.preflight_id AND b.observation_root_id=q.root_b_id WHERE q.content_session_id=$0 ORDER BY c.component_key COLLATE ORDINAL,a.root_key COLLATE ORDINAL,b.root_key COLLATE ORDINAL", session))
            {
                token.ThrowIfCancellationRequested();
                for (var i = 12; i < 18; i++) r[i] = Convert.ToInt64(r[i]) != 0;
                var differences = PhaseTwoReportWriter.Objects(db, "SELECT relative_path_key,category,sha256_a,sha256_b FROM comparison_entries WHERE content_session_id=$0 AND root_a_id=$1 AND root_b_id=$2 AND category NOT IN ('SAME_FILE','SAME_DIRECTORY') ORDER BY relative_path_key COLLATE ORDINAL", session, r[3], r[4]).ToArray();
                Csv(writer, [session, preflight, preflight, identity, .. r.Take(3), .. r.Skip(5), JsonSerializer.Serialize(differences)]);
            }
        }
        using (var writer = output("content_groups.csv"))
        {
            Csv(writer, "session_id", "preflight_id", "source_snapshot_id", "content_manifest_sha256", "component_key", "root_a_key", "root_b_key", "projection", "sha256", "count_a", "count_b", "differing_path_sets", "side", "relative_path", "entry_key");
            foreach (var g in db.Rows("SELECT c.component_key,a.root_key,b.root_key,g.root_a_id,g.root_b_id,g.projection,g.sha256,g.count_a,g.count_b,g.differing_path_sets FROM comparison_content_groups g JOIN candidate_components c ON c.preflight_id=g.preflight_id AND c.component_id=g.component_id JOIN observation_roots a ON a.preflight_id=g.preflight_id AND a.observation_root_id=g.root_a_id JOIN observation_roots b ON b.preflight_id=g.preflight_id AND b.observation_root_id=g.root_b_id WHERE g.content_session_id=$0 ORDER BY c.component_key COLLATE ORDINAL,a.root_key COLLATE ORDINAL,b.root_key COLLATE ORDINAL,g.projection COLLATE ORDINAL,g.sha256 COLLATE ORDINAL", session))
                foreach (var member in db.Rows("""
                    SELECT CASE WHEN m.observation_root_id=$2 THEN 'A' ELSE 'B' END AS side,m.relative_path,e.entry_key
                    FROM observation_memberships m JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id
                    JOIN physical_files p ON p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id
                    JOIN content_hashes h ON h.content_session_id=$0 AND h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
                    WHERE m.preflight_id=$1 AND m.observation_root_id IN ($2,$3) AND h.sha256=$4 AND ($5='FULL' OR e.is_chart=0)
                    ORDER BY side COLLATE ORDINAL,m.relative_path_key COLLATE ORDINAL
                    """, session, preflight, g[3], g[4], g[6], g[5]))
                {
                    token.ThrowIfCancellationRequested(); Csv(writer, session, preflight, preflight, identity, g[0], g[1], g[2], g[5], g[6], g[7], g[8], Convert.ToInt64(g[9]) != 0, member[0], member[1], member[2]);
                }
        }
    }
    private static void Csv(TextWriter writer, params object?[] values) => PhaseTwoReportWriter.Csv(writer, values);
}
