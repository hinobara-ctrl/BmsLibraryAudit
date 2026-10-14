using System.Globalization;

namespace BmsLibraryAudit.Core;

public sealed class PhaseTwoContentRunner(IPhaseTwoContentFileSystem? fileSystem = null, PhaseTwoContentTestHooks? hooks = null)
{
    private readonly IPhaseTwoContentFileSystem files = fileSystem ?? new PhaseTwoFileSystem();
    internal const string Scope = """
        EXISTS(SELECT 1 FROM observation_memberships m JOIN component_memberships cm ON cm.preflight_id=m.preflight_id AND cm.observation_root_id=m.observation_root_id
        JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
        WHERE m.preflight_id=e.preflight_id AND m.entry_id=e.entry_id AND c.outcome='ADMITTED')
        """;
    internal static long Parent(PhaseTwoDatabase db, long session) => Convert.ToInt64(db.Scalar("SELECT parent_preflight_id FROM sessions WHERE session_id=$0 AND stage='CONTENT'", session)
        ?? throw new InvalidOperationException("CONTENT session required."));
    internal static PhaseTwoEntry Entry(object?[] r, int offset = 0) => new((string)r[offset]!, (string)r[offset + 1]!,
        Long(r[offset + 2]), Long(r[offset + 3]), Long(r[offset + 4]), (string?)r[offset + 5],
        r[offset + 6] is null ? null : Convert.ToInt64(r[offset + 6]) != 0, (string)r[offset + 7]!, (string)r[offset + 8]!, (string?)r[offset + 9]);
    private static long? Long(object? value) => value is null ? null : Convert.ToInt64(value);
    private static string Utc() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    public PhaseTwoPreflightResult Run(PhaseTwoContentOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); PhaseTwoSafety.RequirePlatform();
        if (options.PreflightSessionId <= 0 || !PhaseOneSourceCapture.Digest(options.AcceptedManifestSha256, 64))
            throw new ArgumentException("Positive --preflight and lowercase 64-character --accept-manifest are required.");
        using var writer = PhaseTwoWriter.Acquire(options.DatabasePath);
        var preflight = options.PreflightSessionId;
        using (var probe = PhaseTwoDatabase.Open(options.DatabasePath, false))
        {
            RequirePreflight(probe, preflight, options.AcceptedManifestSha256);
            ValidateScope(probe, preflight);
            ValidateDestinations(probe, preflight, options.DatabasePath, options.OutputDirectory);
        }
        using var db = PhaseTwoDatabase.Open(options.DatabasePath, true);
        RequirePreflight(db, preflight, options.AcceptedManifestSha256);
        if (db.Number("SELECT count(*) FROM sessions WHERE state='RUNNING'") != 0 || db.Number("SELECT count(*) FROM report_sets WHERE status='RUNNING'") != 0)
            throw new InvalidOperationException("Interrupted/active evidence requires separate inspection; CONTENT never resumes or repairs it.");
        db.Begin();
        var session = db.Insert("sessions", "stage,parent_preflight_id,accepted_manifest_sha256,state,started_utc,tool_version,runtime_build_sha,source_component_count",
            "CONTENT", preflight, options.AcceptedManifestSha256, "RUNNING", Utc(), ReportWriter.ToolVersion,
            PhaseTwoBuildProvenance.Current.EvidenceRevision, db.Scalar("SELECT source_component_count FROM sessions WHERE session_id=$0", preflight));
        db.Commit();
        try
        {
            VerifyInventory(db, preflight, token);
            hooks?.AfterPrevalidation?.Invoke();
            var buffer = new byte[64 * 1024];
            foreach (var row in db.Rows($"""
                SELECT p.physical_file_id,e.entry_id,e.original_path,e.kind,e.size,e.mtime_ticks,e.attributes,e.extension,e.is_chart,e.inventory_status,e.readability,e.native_identity,e.entry_key
                FROM physical_files p JOIN preflight_entries e ON e.preflight_id=p.preflight_id AND e.entry_id=p.entry_id
                WHERE e.preflight_id=$0 AND {Scope} ORDER BY e.entry_key COLLATE ORDINAL
                """, preflight))
            {
                token.ThrowIfCancellationRequested();
                var physical = Convert.ToInt64(row[0]); var entryId = Convert.ToInt64(row[1]); var expected = Entry(row, 2);
                var observed = files.ReadContent(expected, buffer, token);
                if (observed.Status is not ("SUCCESS" or "ERROR" or "UNSTABLE") || observed.Before != expected || observed.After != expected ||
                    observed.Attempts is < 1 or > 2 || observed.BytesRead < 0 ||
                    observed.Status == "SUCCESS" && (!PhaseOneSourceCapture.Digest(observed.Sha256, 64) || observed.BytesRead < expected.Size) ||
                    observed.Status != "SUCCESS" && observed.Sha256 is not null || observed.Status == "UNSTABLE" && observed.Attempts != 2)
                    throw new InvalidOperationException("Invalid/incomplete content-read evidence: " + expected.OriginalPath);
                var chartDigest = (string?)db.Scalar("SELECT fresh_sha256 FROM chart_revalidations WHERE preflight_id=$0 AND chart_key=$1 AND match_status='MATCH'", preflight, row[12]);
                if (observed.Status == "SUCCESS" && chartDigest is not null && observed.Sha256 != chartDigest)
                    throw new PhaseTwoStaleException(expected.OriginalPath, "STALE_PHASE_1_CANDIDATE");
                db.Begin();
                var reason = observed.Status == "SUCCESS" ? "CONTENT_READ_SUCCESS" : observed.Status == "ERROR" ? "FILE_READ_ERROR" : "FILE_UNSTABLE";
                db.Insert("content_hashes", "content_session_id,preflight_id,physical_file_id,status,reason_code,sha256,verified_size,before_size,before_mtime_ticks,before_native_identity,after_size,after_mtime_ticks,after_native_identity,bytes_read,attempts,read_started_utc,read_ended_utc",
                    session, preflight, physical, observed.Status, reason, observed.Sha256, observed.Status == "SUCCESS" ? expected.Size : null,
                    observed.Before?.Size, observed.Before?.MtimeTicks, observed.Before?.NativeIdentity,
                    observed.After?.Size, observed.After?.MtimeTicks, observed.After?.NativeIdentity, observed.BytesRead, observed.Attempts, observed.StartedUtc, observed.EndedUtc);
                if (observed.Status != "SUCCESS")
                    foreach (var dependency in db.Rows("""
                        SELECT cm.component_id,m.observation_root_id FROM observation_memberships m
                        JOIN component_memberships cm ON cm.preflight_id=m.preflight_id AND cm.observation_root_id=m.observation_root_id
                        JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
                        WHERE m.preflight_id=$0 AND m.entry_id=$1 AND c.outcome='ADMITTED' ORDER BY cm.component_id,m.observation_root_id
                        """, preflight, entryId))
                        db.Diagnostic(session, Convert.ToInt64(dependency[0]), Convert.ToInt64(dependency[1]), entryId, reason,
                            expected.OriginalPath, "Required content has no trusted digest; every dependent component is comparison-ineligible.", attempt: observed.Attempts, physical: physical);
                db.Commit();
                hooks?.AfterFile?.Invoke(expected.OriginalPath);
            }
            hooks?.BeforeFinalVerification?.Invoke();
            VerifyInventory(db, preflight, token);
            ValidateDestinations(db, preflight, options.DatabasePath, options.OutputDirectory);
            RequirePreflight(db, preflight, options.AcceptedManifestSha256);
            db.Begin();
            foreach (var component in db.Rows("SELECT component_id FROM candidate_components WHERE preflight_id=$0 AND outcome='ADMITTED' ORDER BY component_key COLLATE ORDINAL", preflight))
            {
                token.ThrowIfCancellationRequested(); var id = Convert.ToInt64(component[0]);
                var (required, successful, failed, affected) = OutcomeCounts(db, session, preflight, id);
                db.Insert("content_component_outcomes", "content_session_id,preflight_id,component_id,outcome,reason_code,reason,required_physical_file_count,trusted_successful_hash_count,failed_untrusted_file_count,affected_observation_root_count,comparison_eligible,coherent,sealed",
                    session, preflight, id, failed == 0 ? "COMPLETE" : "INCOMPLETE", failed == 0 ? "CONTENT_COMPLETE" : "REQUIRED_CONTENT_UNTRUSTED",
                    failed == 0 ? "Every required physical path has a fresh verified digest." : "Required content is unreadable or unstable; no component comparisons are trusted.",
                    required, successful, failed, affected, failed == 0, true, true);
                if (failed == 0) PhaseTwoContentComparison.Store(db, session, preflight, id, token);
            }
            db.Execute("UPDATE sessions SET completed_utc=$0 WHERE session_id=$1", Utc(), session);
            hooks?.BeforeSeal?.Invoke(); token.ThrowIfCancellationRequested();
            ValidateEvidence(db, session, token);
            var digest = PhaseTwoManifest.Content(db, session);
            db.Execute("UPDATE sessions SET state='COMPLETED',final_evidence_digest=$0 WHERE session_id=$1", digest, session);
            db.Commit();
            PhaseTwoManifest.VerifyContent(db, session, token);
        }
        catch (Exception exception)
        {
            db.Rollback();
            if ((string?)db.Scalar("SELECT state FROM sessions WHERE session_id=$0", session) != "RUNNING") throw;
            db.Begin();
            var stale = exception as PhaseTwoStaleException;
            db.Diagnostic(session, null, null, null, stale?.Code ?? (exception is OperationCanceledException ? "CANCELLED" : "CONTENT_FAILED"),
                stale?.Path ?? options.DatabasePath, exception.Message);
            if (stale?.Code == "STALE_PHASE_1_CANDIDATE") db.Diagnostic(session, null, null, null, "STALE_PREFLIGHT", stale.Path, "Fresh direct-chart digest differs from accepted Phase 2A.");
            db.Execute("UPDATE sessions SET state=$0,completed_utc=$1 WHERE session_id=$2", stale is not null ? "STALE" : exception is OperationCanceledException ? "CANCELLED" : "FAILED", Utc(), session);
            db.Commit();
            if (stale is null) throw;
        }
        return PhaseTwoReportWriter.Write(options.DatabasePath, session, options.OutputDirectory, token);
    }

    internal static void RequirePreflight(PhaseTwoDatabase db, long preflight, string accepted)
    {
        if ((string?)db.Scalar("SELECT stage FROM sessions WHERE session_id=$0", preflight) != "PREFLIGHT" ||
            (string?)db.Scalar("SELECT state FROM sessions WHERE session_id=$0", preflight) != "COMPLETED" ||
            (string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", preflight) != accepted ||
            db.Number("SELECT count(*) FROM candidate_components WHERE preflight_id=$0 AND outcome='ADMITTED'", preflight) == 0)
            throw new InvalidOperationException("Accepted completed PREFLIGHT with admitted components and exact digest required.");
        PhaseTwoManifest.Verify(db, preflight);
    }
    internal static void ValidateDestinations(PhaseTwoDatabase db, long preflight, string database, string output)
    {
        foreach (var captured in db.Rows("SELECT snapshot_id,captured_evidence_digest,seal_flag FROM source_snapshots"))
            if (Convert.ToInt64(captured[2]) != 1 || (string?)captured[1] != PhaseTwoManifest.Source(db, Convert.ToInt64(captured[0])))
                throw new InvalidOperationException("Untrusted protected-root provenance.");
        var source = (string)db.Scalar("SELECT source_db_original_path FROM source_snapshots WHERE snapshot_id=$0", preflight)!;
        var roots = db.Rows("SELECT original_path FROM source_library_roots").Select(r => (string)r[0]!).ToArray();
        PhaseTwoSafety.ValidateDestinations(source, database, output, roots);
    }
    internal static void ValidateScope(PhaseTwoDatabase db, long preflight)
    {
        var charts = db.Rows("SELECT source_root_id,original_path,normalized_path,parent_folder,filename,extension,size,mtime_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status FROM source_charts WHERE snapshot_id=$0 ORDER BY normalized_path COLLATE ORDINAL", preflight)
            .Select(r => new ChartRecord(Convert.ToInt64(r[0]), (string)r[1]!, (string)r[2]!, (string)r[3]!, (string)r[4]!, (string)r[5]!, Convert.ToInt64(r[6]), Convert.ToInt64(r[7]), (string?)r[8], (string?)r[9], Convert.ToInt64(r[10]), Convert.ToInt64(r[11]), Convert.ToInt64(r[12]) != 0, (string)r[13]!)).ToArray();
        var diagnostics = db.Rows("SELECT severity,code,path,message FROM source_diagnostics WHERE snapshot_id=$0", preflight)
            .Select(r => new Diagnostic((string)r[0]!, (string)r[1]!, (string)r[2]!, (string)r[3]!)).ToArray();
        var analysis = RelationshipAnalyzer.Analyze(charts, diagnostics);
        foreach (var row in db.Rows("SELECT component_id,source_component_label,component_key,member_count,outcome,distinct_chart_count,distinct_chart_set_digest FROM candidate_components WHERE preflight_id=$0", preflight))
        {
            var component = analysis.Clusters.SingleOrDefault(c => c.Kind == "EXACT_ONLY" && c.Id == (string)row[1]!);
            var members = db.Rows("SELECT r.original_path FROM component_memberships m JOIN observation_roots r ON r.preflight_id=m.preflight_id AND r.observation_root_id=m.observation_root_id WHERE m.preflight_id=$0 AND m.component_id=$1 ORDER BY r.original_path COLLATE ORDINAL", preflight, row[0]).Select(r => (string)r[0]!).ToArray();
            if (component is null || members.Length != Convert.ToInt64(row[3]) || !members.SequenceEqual(component.Members, StringComparer.Ordinal) ||
                PhaseTwoManifest.Component(members.Select(PathPolicy.Normalize), analysis.Folders.Single(f => PathPolicy.Comparer.Equals(f.Path, members[0])).Hashes) != (string)row[2]!)
                throw new InvalidOperationException("Incomplete or inconsistent source EXACT_ONLY closure.");
            var hashes = analysis.Folders.Single(f => PathPolicy.Comparer.Equals(f.Path, members[0])).Hashes;
            if (hashes.Count != Convert.ToInt64(row[5]) || PhaseTwoManifest.ChartSet(hashes) != (string)row[6]!)
                throw new InvalidOperationException("Inconsistent source chart set.");
            var expectedEdges = analysis.Candidates.Where(c => c.Relationship == "EXACT_CHART_SET" && members.Contains(c.FolderA, PathPolicy.Comparer) && members.Contains(c.FolderB, PathPolicy.Comparer))
                .Select(c => (PathPolicy.Normalize(c.FolderA), PathPolicy.Normalize(c.FolderB), (long)c.ChartsA, (long)c.ChartsB, (long)c.Common, (long)c.PhysicalFilesA, (long)c.PhysicalFilesB)).Order().ToArray();
            var actualEdges = db.Rows("SELECT a.root_key,b.root_key,e.charts_a,e.charts_b,e.common,e.physical_files_a,e.physical_files_b FROM source_exact_edges e JOIN observation_roots a ON a.preflight_id=e.preflight_id AND a.observation_root_id=e.root_a_id JOIN observation_roots b ON b.preflight_id=e.preflight_id AND b.observation_root_id=e.root_b_id WHERE e.preflight_id=$0 AND e.component_id=$1", preflight, row[0])
                .Select(e => ((string)e[0]!, (string)e[1]!, Convert.ToInt64(e[2]), Convert.ToInt64(e[3]), Convert.ToInt64(e[4]), Convert.ToInt64(e[5]), Convert.ToInt64(e[6]))).Order().ToArray();
            if (!expectedEdges.SequenceEqual(actualEdges)) throw new InvalidOperationException("Incomplete source EXACT_ONLY edges.");
        }
        if (db.Number("SELECT count(*) FROM observation_roots r JOIN component_memberships m ON m.preflight_id=r.preflight_id AND m.observation_root_id=r.observation_root_id JOIN candidate_components c ON c.preflight_id=m.preflight_id AND c.component_id=m.component_id WHERE r.preflight_id=$0 AND c.outcome='ADMITTED' AND (r.chart_state<>'CURRENT' OR r.inventory_state<>'COMPLETE' OR r.counts_are_lower_bounds<>0 OR NOT EXISTS(SELECT 1 FROM observation_memberships n WHERE n.preflight_id=r.preflight_id AND n.observation_root_id=r.observation_root_id AND n.relative_path='.'))", preflight) != 0)
            throw new InvalidOperationException("Incomplete admitted observation surface.");
        foreach (var row in db.Rows($"SELECT e.entry_key,e.original_path,e.kind,e.inventory_status,e.size,e.mtime_ticks,e.attributes,e.native_identity FROM preflight_entries e WHERE e.preflight_id=$0 AND {Scope}", preflight))
            if ((string)row[0]! != PathPolicy.Normalize((string)row[1]!) || (string)row[2]! is not ("DIRECTORY" or "REGULAR_FILE") || (string)row[3]! != "OK" || row[5] is null || row[6] is null || row[7] is null || (string)row[2]! == "REGULAR_FILE" && row[4] is null)
                throw new InvalidOperationException("Unsafe accepted entry.");
        if (db.Number($"SELECT count(*) FROM preflight_entries e WHERE e.preflight_id=$0 AND {Scope} AND e.kind='REGULAR_FILE' AND NOT EXISTS(SELECT 1 FROM physical_files p WHERE p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id AND p.native_identity IS e.native_identity)", preflight) != 0)
            throw new InvalidOperationException("Incomplete physical path coverage.");
        foreach (var row in db.Rows("SELECT r.original_path,e.original_path,m.relative_path,m.relative_path_key FROM observation_memberships m JOIN observation_roots r ON r.preflight_id=m.preflight_id AND r.observation_root_id=m.observation_root_id JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id WHERE m.preflight_id=$0", preflight))
        {
            var root = (string)row[0]!; var path = (string)row[1]!; var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!PathPolicy.Contains(root, path) || relative != (string)row[2]! || relative != (string)row[3]!) throw new InvalidOperationException("Invalid observation membership.");
        }
    }
    private void VerifyInventory(PhaseTwoDatabase db, long preflight, CancellationToken token)
    {
        var roots = db.Rows("SELECT r.original_path FROM observation_roots r JOIN component_memberships m ON m.preflight_id=r.preflight_id AND m.observation_root_id=r.observation_root_id JOIN candidate_components c ON c.preflight_id=m.preflight_id AND c.component_id=m.component_id WHERE r.preflight_id=$0 AND c.outcome='ADMITTED' ORDER BY r.root_key COLLATE ORDINAL", preflight).Select(r => (string)r[0]!).ToArray();
        db.Execute("CREATE TEMP TABLE IF NOT EXISTS content_seen(entry_key TEXT PRIMARY KEY)"); db.Execute("DELETE FROM content_seen");
        foreach (var (observed, error) in PhaseTwoPreflightRunner.Walk(files, roots, token))
        {
            token.ThrowIfCancellationRequested();
            var row = db.Rows($"SELECT e.original_path,e.kind,e.size,e.mtime_ticks,e.attributes,e.extension,e.is_chart,e.inventory_status,e.readability,e.native_identity FROM preflight_entries e WHERE e.preflight_id=$0 AND e.entry_key=$1 AND {Scope}", preflight, observed.Key).SingleOrDefault();
            if (error is not null || row is null || observed != Entry(row) || observed.Kind is not ("DIRECTORY" or "REGULAR_FILE")) throw new PhaseTwoStaleException(observed.OriginalPath);
            if (db.Number("SELECT count(*) FROM content_seen WHERE entry_key=$0", observed.Key) != 0) throw new PhaseTwoStaleException(observed.OriginalPath);
            db.Execute("INSERT INTO content_seen VALUES($0)", observed.Key);
        }
        var missing = db.Rows($"SELECT e.original_path FROM preflight_entries e WHERE e.preflight_id=$0 AND {Scope} AND NOT EXISTS(SELECT 1 FROM content_seen s WHERE s.entry_key=e.entry_key)", preflight).FirstOrDefault();
        if (missing is not null) throw new PhaseTwoStaleException((string)missing[0]!);
    }
    internal static (long Required, long Success, long Failed, long Affected) OutcomeCounts(PhaseTwoDatabase db, long session, long preflight, long component)
    {
        var row = db.Rows("""
            SELECT count(DISTINCT p.physical_file_id),count(DISTINCT CASE WHEN h.status='SUCCESS' THEN p.physical_file_id END),
              count(DISTINCT CASE WHEN h.status IN ('ERROR','UNSTABLE') THEN p.physical_file_id END),
              count(DISTINCT CASE WHEN h.status IN ('ERROR','UNSTABLE') THEN cm.observation_root_id END)
            FROM component_memberships cm JOIN observation_memberships m ON m.preflight_id=cm.preflight_id AND m.observation_root_id=cm.observation_root_id
            JOIN physical_files p ON p.preflight_id=m.preflight_id AND p.entry_id=m.entry_id
            LEFT JOIN content_hashes h ON h.content_session_id=$0 AND h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
            WHERE cm.preflight_id=$1 AND cm.component_id=$2
            """, session, preflight, component).Single();
        return (Convert.ToInt64(row[0]), Convert.ToInt64(row[1]), Convert.ToInt64(row[2]), Convert.ToInt64(row[3]));
    }
    internal static void ValidateEvidence(PhaseTwoDatabase db, long session, CancellationToken token = default)
    {
        var preflight = Parent(db, session); db.CheckIntegrity();
        RequirePreflight(db, preflight, (string)db.Scalar("SELECT accepted_manifest_sha256 FROM sessions WHERE session_id=$0", session)!);
        ValidateScope(db, preflight);
        foreach (var row in db.Rows("""
            SELECT h.status,h.sha256,h.verified_size,h.before_size,h.after_size,h.before_mtime_ticks,h.after_mtime_ticks,
                h.before_native_identity,h.after_native_identity,h.read_started_utc,h.read_ended_utc,e.size,e.mtime_ticks,e.native_identity,q.fresh_sha256
            FROM content_hashes h JOIN physical_files p ON p.preflight_id=h.preflight_id AND p.physical_file_id=h.physical_file_id
            JOIN preflight_entries e ON e.preflight_id=p.preflight_id AND e.entry_id=p.entry_id
            LEFT JOIN chart_revalidations q ON q.preflight_id=e.preflight_id AND q.chart_key=e.entry_key AND q.match_status='MATCH'
            WHERE h.content_session_id=$0
            """, session))
        {
            if (row[3] is null || row[4] is null || row[5] is null || row[6] is null || row[7] is null || row[8] is null ||
                !Equals(row[3], row[11]) || !Equals(row[4], row[11]) || !Equals(row[5], row[12]) || !Equals(row[6], row[12]) ||
                !Equals(row[7], row[13]) || !Equals(row[8], row[13]) ||
                !DateTimeOffset.TryParseExact((string?)row[9], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !DateTimeOffset.TryParseExact((string?)row[10], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) ||
                start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero || end < start ||
                (string)row[0]! == "SUCCESS" && (!Equals(row[2], row[11]) || !PhaseOneSourceCapture.Digest((string?)row[1], 64) || row[14] is not null && !Equals(row[1], row[14])))
                throw new InvalidOperationException("EVIDENCE_INTEGRITY_ERROR: invalid content stamps, timing or chart digest.");
        }
        var required = db.Number($"SELECT count(*) FROM physical_files p JOIN preflight_entries e ON e.preflight_id=p.preflight_id AND e.entry_id=p.entry_id WHERE e.preflight_id=$0 AND {Scope}", preflight);
        if (required != db.Number("SELECT count(*) FROM content_hashes WHERE content_session_id=$0 AND status<>'PENDING'", session) ||
            db.Number("SELECT count(*) FROM content_hashes WHERE content_session_id=$0 AND status='PENDING'", session) != 0 ||
            db.Number("SELECT count(*) FROM (SELECT sha256 FROM content_hashes WHERE content_session_id=$0 AND status='SUCCESS' GROUP BY sha256 HAVING min(verified_size)<>max(verified_size))", session) != 0)
            throw new InvalidOperationException("EVIDENCE_INTEGRITY_ERROR: content coverage or digest/size mismatch.");
        var componentCount = db.Number("SELECT count(*) FROM candidate_components WHERE preflight_id=$0 AND outcome='ADMITTED'", preflight);
        if (componentCount != db.Number("SELECT count(*) FROM content_component_outcomes WHERE content_session_id=$0", session)) throw new InvalidOperationException("Missing content outcomes.");
        foreach (var row in db.Rows("SELECT component_id,outcome,required_physical_file_count,trusted_successful_hash_count,failed_untrusted_file_count,affected_observation_root_count FROM content_component_outcomes WHERE content_session_id=$0", session))
        {
            token.ThrowIfCancellationRequested(); var component = Convert.ToInt64(row[0]); var counts = OutcomeCounts(db, session, preflight, component);
            if (counts.Required != Convert.ToInt64(row[2]) || counts.Success != Convert.ToInt64(row[3]) || counts.Failed != Convert.ToInt64(row[4]) || counts.Affected != Convert.ToInt64(row[5]) || counts.Required != counts.Success + counts.Failed || (string)row[1]! != (counts.Failed == 0 ? "COMPLETE" : "INCOMPLETE"))
                throw new InvalidOperationException("Content outcome counters disagree with complete dependency graph.");
            PhaseTwoContentComparison.Verify(db, session, preflight, component, counts.Failed == 0, token);
        }
        if (db.Number("""
            SELECT count(*) FROM content_hashes h JOIN physical_files p ON p.preflight_id=h.preflight_id AND p.physical_file_id=h.physical_file_id
            JOIN observation_memberships m ON m.preflight_id=p.preflight_id AND m.entry_id=p.entry_id
            JOIN component_memberships cm ON cm.preflight_id=m.preflight_id AND cm.observation_root_id=m.observation_root_id
            JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
            WHERE h.content_session_id=$0 AND c.outcome='ADMITTED' AND h.status IN ('ERROR','UNSTABLE') AND NOT EXISTS(
                SELECT 1 FROM diagnostics d WHERE d.session_id=h.content_session_id AND d.component_id=cm.component_id AND d.root_id=m.observation_root_id
                AND d.entry_id=p.entry_id AND d.physical_file_id=p.physical_file_id AND d.code=h.reason_code)
            """, session) != 0) throw new InvalidOperationException("Missing failure dependency diagnostics.");
    }
}
