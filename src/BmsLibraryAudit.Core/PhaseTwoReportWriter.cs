using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class PhaseTwoReportWriter
{
    private static readonly string[] Names = ["session_summary.json", "observation_roots.csv", "filesystem_inventory.csv", "diagnostics.csv"];
    public static PhaseTwoPreflightResult Write(string databasePath, long session, string outputDirectory, CancellationToken token = default, PhaseTwoTestHooks? hooks = null)
    {
        token.ThrowIfCancellationRequested();
        using var writerLock = PhaseTwoWriter.Acquire(databasePath);
        using var read = PhaseTwoDatabase.Open(databasePath, false);
        var sessionRows = Objects(read, "SELECT * FROM sessions WHERE session_id=$0", session).ToArray();
        if (sessionRows.Length != 1 || (string)sessionRows[0]["state"]! == "RUNNING") throw new InvalidOperationException("Report requires a terminal persisted session.");
        var state = (string)sessionRows[0]["state"]!;
        var content = (string)sessionRows[0]["stage"]! == "CONTENT";
        var preflight = content ? PhaseTwoContentRunner.Parent(read, session) : session;
        void Verify() { if (content) PhaseTwoManifest.VerifyContent(read, session, token); else PhaseTwoManifest.Verify(read, session); }
        if (content) PhaseTwoContentRunner.RequirePreflight(read, preflight, (string)sessionRows[0]["accepted_manifest_sha256"]!);
        if (state == "COMPLETED") Verify();
        var names = content && state == "COMPLETED" ? Names.Concat(new[] { "folder_comparisons.csv", "content_groups.csv" }).ToArray() : Names;
        var source = Objects(read, "SELECT * FROM source_snapshots WHERE snapshot_id=$0", preflight).Single();
        foreach (var captured in read.Rows("SELECT snapshot_id,captured_evidence_digest,seal_flag FROM source_snapshots").ToArray())
            if (Convert.ToInt64(captured[2]) != 1 || (string?)captured[1] != PhaseTwoManifest.Source(read, Convert.ToInt64(captured[0])))
                throw new InvalidOperationException("Source provenance used for destination safety has an invalid seal.");
        var sourcePath = (string)source["source_db_original_path"]!;
        var knownRoots = read.Rows("SELECT original_path FROM source_library_roots ORDER BY normalized_path COLLATE ORDINAL").Select(r => (string)r[0]!).ToArray();
        // Metadata-only alias checks are allowed; no original database content
        // is opened and no library enumeration or payload reads occur here.
        PhaseTwoSafety.ValidateDestinations(sourcePath, databasePath, outputDirectory, knownRoots);
        var result = Result(read, session, state);
        long report;
        using (var receipt = PhaseTwoDatabase.Open(databasePath, true))
        {
            receipt.Begin();
            if (receipt.Number("SELECT count(*) FROM report_sets WHERE status='RUNNING'") > 0) throw new InvalidOperationException("Active/interrupted report writer requires inspection.");
            report = receipt.Insert("report_sets", "session_id,evidence_stage,evidence_state,evidence_identity,original_destination,normalized_destination,status",
                session, content ? "CONTENT" : "PREFLIGHT", state, sessionRows[0]["final_evidence_digest"], PathPolicy.Absolute(outputDirectory), PathPolicy.Normalize(outputDirectory), "RUNNING");
            receipt.Commit();
        }
        try
        {
            PhaseTwoSafety.ValidateDestinations(sourcePath, databasePath, outputDirectory, knownRoots);
            Directory.CreateDirectory(outputDirectory); PathPolicy.RejectReparseAncestors(outputDirectory);
            var outputIdentity = PhaseTwoSafety.Inspect(outputDirectory);
            StreamWriter Output(string name)
            {
                token.ThrowIfCancellationRequested(); hooks?.BeforeReportFile?.Invoke(name);
                if (PhaseTwoSafety.Inspect(outputDirectory).Identity != outputIdentity.Identity)
                    throw new IOException("Report directory identity changed.");
                return Create(outputDirectory, name);
            }
            read.Begin(readOnly: true);
            if (state == "COMPLETED") Verify();
            if (content) PhaseTwoContentReports.Write(read, session, preflight, state, Output, token);
            else
            {
            var roots = Objects(read, """
                SELECT c.component_key,c.source_component_label,c.outcome,c.reason,r.*,0 AS nested_root_count
                FROM observation_roots r JOIN component_memberships m ON m.observation_root_id=r.observation_root_id AND m.preflight_id=r.preflight_id
                JOIN candidate_components c ON c.component_id=m.component_id AND c.preflight_id=m.preflight_id
                WHERE r.preflight_id=$0 ORDER BY c.component_key COLLATE ORDINAL,r.root_key COLLATE ORDINAL
                """, session).ToArray();
            // Compute nesting from captured paths (SQL LIKE would treat '%'/'_'
            // in authored paths as wildcards); no filesystem access is involved.
            foreach (var root in roots)
                root["nested_root_count"] = roots.LongCount(other => !ReferenceEquals(root, other) &&
                    (PathPolicy.Contains((string)root["original_path"]!, (string)other["original_path"]!) || PathPolicy.Contains((string)other["original_path"]!, (string)root["original_path"]!)));
            long Count(string sql) => read.Number(sql, session);
            var unionBytes = Count("SELECT COALESCE(sum(size),0) FROM preflight_entries WHERE preflight_id=$0 AND kind='REGULAR_FILE'");
            var summary = new
            {
                contract_version = PhaseTwoDatabase.ContractVersion, tool_version = ReportWriter.ToolVersion, schema_version = 1,
                phase = "PHASE_2.READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE", operation = "PHASE_2A.READ_ONLY_PREFLIGHT",
                session_id = session, stage = "PREFLIGHT", state,
                started_utc = sessionRows[0]["started_utc"], completed_utc = sessionRows[0]["completed_utc"],
                runtime_build_sha = sessionRows[0]["runtime_build_sha"], source_provenance = source,
                source_component_count = sessionRows[0]["source_component_count"], selected_component_count = result.SelectedComponents,
                admitted_component_count = result.AdmittedComponents, rejected_component_count = result.RejectedComponents,
                observation_root_count = roots.Length,
                union_regular_file_count = Count("SELECT count(*) FROM preflight_entries WHERE preflight_id=$0 AND kind='REGULAR_FILE'"),
                union_directory_count = Count("SELECT count(*) FROM preflight_entries WHERE preflight_id=$0 AND kind='DIRECTORY'"),
                unique_normalized_path_bytes = unionBytes, union_counts_are_lower_bounds = result.RejectedComponents > 0 || state != "COMPLETED",
                estimated_phase2b_read_bytes = state == "COMPLETED" ? (long?)result.EstimatedReadBytes : null,
                retry_allowance = "At most one additional read per unstable file; excludes metadata traversal overhead.",
                chart_revalidation_bytes = Count("SELECT COALESCE(sum(bytes_read),0) FROM chart_revalidations WHERE preflight_id=$0"),
                chart_revalidation_file_count = Count("SELECT count(*) FROM chart_revalidations WHERE preflight_id=$0"),
                nested_root_pairs = roots.Sum(r => Convert.ToInt64(r["nested_root_count"])) / 2,
                shared_path_count = Count("SELECT count(*) FROM (SELECT entry_id FROM observation_memberships WHERE preflight_id=$0 GROUP BY entry_id HAVING count(*)>1)"),
                extension_distribution = Objects(read, "SELECT extension,count(*) AS files,COALESCE(sum(size),0) AS logical_bytes FROM preflight_entries WHERE preflight_id=$0 AND kind='REGULAR_FILE' GROUP BY extension ORDER BY extension COLLATE ORDINAL", session).ToArray(),
                outcome_distribution = Objects(read, "SELECT outcome,count(*) AS components FROM candidate_components WHERE preflight_id=$0 GROUP BY outcome ORDER BY outcome COLLATE ORDINAL", session).ToArray(),
                diagnostic_counts = Objects(read, "SELECT severity,code,count(*) AS count FROM diagnostics WHERE session_id=$0 GROUP BY severity,code ORDER BY severity COLLATE ORDINAL,code COLLATE ORDINAL", session).ToArray(),
                error_count = Count("SELECT count(*) FROM diagnostics WHERE session_id=$0 AND severity='ERROR'"),
                warning_count = Count("SELECT count(*) FROM diagnostics WHERE session_id=$0 AND severity='WARNING'"),
                largest_roots_by_bytes = roots.OrderByDescending(r => Convert.ToInt64(r["logical_bytes"])).ThenBy(r => (string)r["root_key"]!, StringComparer.Ordinal).Take(20).ToArray(),
                largest_roots_by_file_count = roots.OrderByDescending(r => Convert.ToInt64(r["regular_file_count"])).ThenBy(r => (string)r["root_key"]!, StringComparer.Ordinal).Take(20).ToArray(),
                preflight_manifest_sha256 = state == "COMPLETED" ? result.ManifestSha256 : null,
                trusted_content_evidence_scope = "NONE", non_chart_payload_reads = 0, phase2b_executed = false,
                complete_content_component_count = (long?)null, incomplete_content_component_count = (long?)null,
                trusted_successful_hash_count = (long?)null, failed_untrusted_content_read_count = (long?)null,
                comparison_eligible_component_count = (long?)null, excluded_content_component_count = (long?)null,
                session_result_exit_code = state == "COMPLETED" ? result.ExitCode : state == "CANCELLED" ? 130 : state == "STALE" ? 2 : 1,
                evidence_notice = "PREFLIGHT metadata and fresh direct-chart observations only. Non-chart resources were not payload-read or hashed. OBSERVATION_ROOT is not PACKAGE_ROOT. No content equality, package equivalence or mutation authority. Metadata is not content proof; observations are not a simultaneous filesystem snapshot."
            };
            using (var output = Output(Names[0]))
            { output.Write(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal)); output.Write('\n'); }
            using (var output = Output(Names[1]))
            {
                Csv(output, "session_id", "preflight_id", "source_snapshot_id", "manifest_sha256", "component_key", "source_component_label", "outcome", "reason", "root_key", "original_path", "chart_state", "inventory_state", "regular_file_count", "directory_count", "logical_bytes", "counts_are_lower_bounds", "nested_root_count", "shared_path_count", "content_component_outcome", "comparison_eligible");
                foreach (var root in roots)
                {
                    token.ThrowIfCancellationRequested();
                    var shared = read.Number("SELECT count(*) FROM observation_memberships m WHERE m.preflight_id=$0 AND m.observation_root_id=$1 AND (SELECT count(*) FROM observation_memberships n WHERE n.preflight_id=m.preflight_id AND n.entry_id=m.entry_id)>1", session, root["observation_root_id"]);
                    Csv(output, session, session, session, state == "COMPLETED" ? result.ManifestSha256 : null,
                        root["component_key"], root["source_component_label"], root["outcome"], root["reason"], root["root_key"], root["original_path"], root["chart_state"], root["inventory_state"], root["regular_file_count"], root["directory_count"], root["logical_bytes"], Convert.ToInt64(root["counts_are_lower_bounds"]) == 1, root["nested_root_count"], shared, null, null);
                }
            }
            using (var output = Output(Names[2]))
            {
                Csv(output, "session_id", "preflight_id", "source_snapshot_id", "manifest_sha256", "root_key", "entry_key", "original_path", "relative_path", "kind", "size", "mtime_ticks", "attributes", "extension", "is_chart", "inventory_status", "readability", "native_identity", "physical_file_id", "chart_revalidation_sha256", "content_sha256");
                foreach (var row in read.Rows("""
                    SELECT r.root_key,e.entry_key,e.original_path,m.relative_path,e.kind,e.size,e.mtime_ticks,e.attributes,e.extension,e.is_chart,e.inventory_status,e.readability,e.native_identity,p.physical_file_id,q.fresh_sha256
                    FROM observation_memberships m JOIN observation_roots r ON r.observation_root_id=m.observation_root_id AND r.preflight_id=m.preflight_id
                    JOIN preflight_entries e ON e.entry_id=m.entry_id AND e.preflight_id=m.preflight_id
                    LEFT JOIN physical_files p ON p.entry_id=e.entry_id AND p.preflight_id=e.preflight_id
                    LEFT JOIN chart_revalidations q ON q.preflight_id=e.preflight_id AND q.chart_key=e.entry_key
                    WHERE m.preflight_id=$0 ORDER BY r.root_key COLLATE ORDINAL,m.relative_path_key COLLATE ORDINAL
                    """, session))
                {
                    token.ThrowIfCancellationRequested();
                    if (row[9] is not null) row[9] = Convert.ToInt64(row[9]) != 0;
                    if (state != "COMPLETED") row[14] = null;
                    Csv(output, [session, session, session, state == "COMPLETED" ? result.ManifestSha256 : null, .. row, null]);
                }
            }
            using (var output = Output(Names[3]))
            {
                Csv(output, "session_id", "preflight_id", "source_snapshot_id", "manifest_sha256", "stage", "component_key", "root_key", "entry_key", "severity", "code", "original_path", "normalized_path", "attempt", "message");
                foreach (var row in read.Rows("""
                    SELECT d.gate,c.component_key,r.root_key,e.entry_key,d.severity,d.code,d.original_path,d.normalized_path,d.attempt,d.message FROM diagnostics d
                    LEFT JOIN candidate_components c ON c.component_id=d.component_id AND c.preflight_id=d.session_id
                    LEFT JOIN observation_roots r ON r.observation_root_id=d.root_id AND r.preflight_id=d.session_id
                    LEFT JOIN preflight_entries e ON e.entry_id=d.entry_id AND e.preflight_id=d.session_id
                    WHERE d.session_id=$0 ORDER BY c.component_key COLLATE ORDINAL,r.root_key COLLATE ORDINAL,d.normalized_path COLLATE ORDINAL,d.code COLLATE ORDINAL,d.attempt,d.message COLLATE ORDINAL,d.severity COLLATE ORDINAL,d.gate COLLATE ORDINAL,e.entry_key COLLATE ORDINAL,d.original_path COLLATE ORDINAL
                    """, session))
                { token.ThrowIfCancellationRequested(); Csv(output, [session, session, session, state == "COMPLETED" ? result.ManifestSha256 : null, .. row]); }
            }
            }
            read.Commit();
            using var receipt = PhaseTwoDatabase.Open(databasePath, true); receipt.Begin();
            foreach (var name in names)
            {
                token.ThrowIfCancellationRequested();
                var facts = PhaseTwoSafety.Inspect(Path.Combine(outputDirectory, name));
                if (facts.LinkCount != 1 || (facts.Attributes & (uint)FileAttributes.Directory) != 0)
                    throw new IOException("Unsafe report file identity.");
                using var file = new FileStream(Path.Combine(outputDirectory, name), FileMode.Open, FileAccess.Read, FileShare.Read);
                if (PhaseTwoSafety.Inspect(file.SafeFileHandle, Path.Combine(outputDirectory, name)).Identity != facts.Identity)
                    throw new IOException("Report file changed before receipt hashing.");
                receipt.Insert("report_files", "report_set_id,filename,byte_length,sha256,status", report, name, file.Length, Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(), "COMPLETED");
            }
            receipt.Execute("UPDATE report_sets SET status='COMPLETED',completed_utc=$0 WHERE report_set_id=$1", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), report); receipt.Commit();
            return result;
        }
        catch
        {
            read.Rollback();
            using var receipt = PhaseTwoDatabase.Open(databasePath, true); receipt.Begin();
            receipt.Execute("UPDATE report_sets SET status='FAILED',completed_utc=$0 WHERE report_set_id=$1", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), report); receipt.Commit();
            throw;
        }
    }
    private static PhaseTwoPreflightResult Result(PhaseTwoDatabase db, long session, string state)
    {
        if ((string?)db.Scalar("SELECT stage FROM sessions WHERE session_id=$0", session) == "CONTENT")
        {
            var parent = PhaseTwoContentRunner.Parent(db, session); var original = Result(db, parent, "COMPLETED");
            return original with { SessionId = session, State = state, ManifestSha256 = (string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", session) ?? "",
                ParentPreflightId = parent,
                CompleteContentComponents = state == "COMPLETED" ? db.Number("SELECT count(*) FROM content_component_outcomes WHERE content_session_id=$0 AND outcome='COMPLETE'", session) : null,
                IncompleteContentComponents = state == "COMPLETED" ? db.Number("SELECT count(*) FROM content_component_outcomes WHERE content_session_id=$0 AND outcome='INCOMPLETE'", session) : null };
        }
        return new(session, state,
        (string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", session) ?? "",
        db.Number("SELECT count(*) FROM candidate_components WHERE preflight_id=$0", session),
        db.Number("SELECT count(*) FROM candidate_components WHERE preflight_id=$0 AND outcome='ADMITTED'", session),
        db.Number("SELECT count(*) FROM candidate_components WHERE preflight_id=$0 AND outcome<>'ADMITTED'", session),
        db.Number("SELECT COALESCE(sum(e.size),0) FROM preflight_entries e WHERE e.preflight_id=$0 AND e.kind='REGULAR_FILE' AND EXISTS(SELECT 1 FROM observation_memberships m JOIN component_memberships n ON n.observation_root_id=m.observation_root_id AND n.preflight_id=m.preflight_id JOIN candidate_components c ON c.component_id=n.component_id AND c.preflight_id=n.preflight_id WHERE m.entry_id=e.entry_id AND m.preflight_id=e.preflight_id AND c.outcome='ADMITTED')", session));
    }
    internal static IEnumerable<Dictionary<string, object?>> Objects(PhaseTwoDatabase db, string sql, params object?[] values)
    {
        using var command = db.Command(sql, values); using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++) row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
            yield return row;
        }
    }
    private static StreamWriter Create(string directory, string name) => new(new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.None), new UTF8Encoding(false)) { NewLine = "\n" };
    internal static void Csv(TextWriter writer, params object?[] values) => writer.WriteLine(string.Join(',', values.Select(value =>
    {
        var text = value is null ? "" : value is bool flag ? flag ? "true" : "false" : value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString()!;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? '"' + text.Replace("\"", "\"\"") + '"' : text;
    })));
}
