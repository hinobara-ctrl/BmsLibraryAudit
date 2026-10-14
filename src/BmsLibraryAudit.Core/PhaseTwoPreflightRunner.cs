using System.Globalization;

namespace BmsLibraryAudit.Core;

public sealed class PhaseTwoPreflightRunner(IPhaseTwoFileSystem? fileSystem = null, PhaseTwoTestHooks? hooks = null)
{
    private readonly IPhaseTwoFileSystem files = fileSystem ?? new PhaseTwoFileSystem();
    private sealed record Root(long Id, long Component, string Path, string Key);
    public PhaseTwoPreflightResult Run(PhaseTwoPreflightOptions options, CancellationToken cancellationToken = default)
    {
        var startedUtc = Utc();
        cancellationToken.ThrowIfCancellationRequested();
        var source = PhaseOneSourceCapture.Capture(options, hooks, cancellationToken);
        var analysis = RelationshipAnalyzer.Analyze(source.Charts, source.Diagnostics);
        var all = analysis.Clusters.Where(c => c.Kind == "EXACT_ONLY").ToArray();
        var selection = options.Components?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (selection.Any(id => !all.Any(c => c.Id == id))) throw new ArgumentException("Unknown/non-EXACT_ONLY component selection.");
        var selected = all.Where(c => selection.Length == 0 || selection.Contains(c.Id, StringComparer.Ordinal)).ToArray();
        var forbiddenRoots = source.Roots.Select(r => r.OriginalPath).ToList();
        if (PhaseTwoSafety.Exists(options.DatabasePath))
        {
            using var probe = PhaseTwoDatabase.Open(options.DatabasePath, false);
            foreach (var captured in probe.Rows("SELECT snapshot_id,captured_evidence_digest,seal_flag FROM source_snapshots").ToArray())
                if (Convert.ToInt64(captured[2]) != 1 || (string?)captured[1] != PhaseTwoManifest.Source(probe, Convert.ToInt64(captured[0])))
                    throw new InvalidOperationException("Existing captured source provenance seal is invalid.");
            forbiddenRoots.AddRange(probe.Rows("SELECT original_path FROM source_library_roots").Select(r => (string)r[0]!));
        }
        PhaseTwoSafety.ValidateDestinations(source.OriginalPath, options.DatabasePath, options.OutputDirectory, forbiddenRoots);
        using var writer = PhaseTwoWriter.Acquire(options.DatabasePath);
        using var db = PhaseTwoDatabase.Open(options.DatabasePath, true);
        if (db.Number("SELECT count(*) FROM report_sets WHERE status='RUNNING'") > 0) throw new InvalidOperationException("An export writer is active/interrupted; external inspection required.");
        var roots = new List<Root>();
        long session = 0;
        try
        {
            db.Begin();
            foreach (var row in db.Rows("SELECT session_id FROM sessions WHERE state='RUNNING'").ToArray())
            {
                var interrupted = Convert.ToInt64(row[0]);
                db.Diagnostic(interrupted, null, null, null, "INTERRUPTED", db.Path, "Previous operation interrupted; no evidence accepted.");
                db.Execute("UPDATE sessions SET state='FAILED',completed_utc=$0 WHERE session_id=$1", Utc(), interrupted);
            }
            session = db.Insert("sessions", "stage,state,started_utc,tool_version,runtime_build_sha,source_component_count", "PREFLIGHT", "RUNNING", startedUtc, ReportWriter.ToolVersion, BuildSha(), all.Length);
            StoreSource(db, session, source);
            foreach (var component in selected)
            {
                var hashes = analysis.Folders.Single(f => PathPolicy.Comparer.Equals(f.Path, component.Members[0])).Hashes;
                var identity = PhaseTwoManifest.Component(component.Members.Select(PathPolicy.Normalize), hashes);
                var id = db.Insert("candidate_components", "preflight_id,component_key,source_component_label,distinct_chart_count,distinct_chart_set_digest,member_count,outcome,reason",
                    session, identity, component.Id, hashes.Count, PhaseTwoManifest.ChartSet(hashes), component.Members.Count, "ADMITTED", "Pending revalidation and inventory.");
                foreach (var member in component.Members.OrderBy(PathPolicy.Normalize, StringComparer.Ordinal))
                {
                    var owner = source.Roots.Single(r => r.Active && PathPolicy.Contains(r.OriginalPath, member));
                    var rootId = db.Insert("observation_roots", "preflight_id,source_root_id,original_path,root_key,chart_state,inventory_state,counts_are_lower_bounds",
                        session, owner.Id, member, PathPolicy.Normalize(member), "PENDING", "PENDING", true);
                    db.Insert("component_memberships", "preflight_id,component_id,observation_root_id", session, id, rootId);
                    roots.Add(new(rootId, id, member, PathPolicy.Normalize(member)));
                }
                foreach (var edge in analysis.Candidates.Where(e => e.Relationship == "EXACT_CHART_SET" && component.Members.Contains(e.FolderA, PathPolicy.Comparer) && component.Members.Contains(e.FolderB, PathPolicy.Comparer)))
                {
                    var a = roots.Single(r => PathPolicy.Comparer.Equals(r.Path, edge.FolderA));
                    var b = roots.Single(r => PathPolicy.Comparer.Equals(r.Path, edge.FolderB));
                    if (string.CompareOrdinal(a.Key, b.Key) > 0) (a, b) = (b, a);
                    db.Insert("source_exact_edges", "preflight_id,component_id,root_a_id,root_b_id,charts_a,charts_b,common,physical_files_a,physical_files_b",
                        session, id, a.Id, b.Id, edge.ChartsA, edge.ChartsB, edge.Common,
                        PathPolicy.Comparer.Equals(a.Path, edge.FolderA) ? edge.PhysicalFilesA : edge.PhysicalFilesB,
                        PathPolicy.Comparer.Equals(b.Path, edge.FolderB) ? edge.PhysicalFilesB : edge.PhysicalFilesA);
                }
            }
            db.Commit();
            foreach (var root in roots.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Revalidate(db, session, root, source, cancellationToken);
            }
            var eligible = roots.Where(r => (string?)db.Scalar("SELECT outcome FROM candidate_components WHERE component_id=$0", r.Component) == "ADMITTED").ToArray();
            hooks?.BeforeInventory?.Invoke();
            Inventory(db, session, eligible, cancellationToken);
            hooks?.BeforeInventoryVerification?.Invoke();
            VerifyInventory(db, session, eligible, cancellationToken);
            db.Begin();
            foreach (var root in roots)
            {
                var counts = db.Rows("SELECT COALESCE(sum(e.kind='REGULAR_FILE'),0),COALESCE(sum(e.kind='DIRECTORY'),0),COALESCE(sum(CASE WHEN e.kind='REGULAR_FILE' THEN e.size ELSE 0 END),0) FROM observation_memberships m JOIN preflight_entries e ON e.entry_id=m.entry_id AND e.preflight_id=m.preflight_id WHERE m.preflight_id=$0 AND m.observation_root_id=$1", session, root.Id).Single();
                var complete = (string?)db.Scalar("SELECT outcome FROM candidate_components WHERE component_id=$0", root.Component) == "ADMITTED";
                var inventoried = eligible.Any(r => r.Id == root.Id);
                db.Execute("UPDATE observation_roots SET regular_file_count=$0,directory_count=$1,logical_bytes=$2,inventory_state=$3,counts_are_lower_bounds=$4 WHERE observation_root_id=$5",
                    counts[0], counts[1], counts[2], complete ? "COMPLETE" : inventoried ? "INCOMPLETE" : "PENDING", !complete, root.Id);
            }
            db.Execute("UPDATE candidate_components SET reason='Direct charts and metadata manifest verified; content readability NOT_TESTED.' WHERE preflight_id=$0 AND outcome='ADMITTED'", session);
            db.Execute("UPDATE sessions SET completed_utc=$0 WHERE session_id=$1", Utc(), session);
            hooks?.BeforeSeal?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCompletion(db, session);
            var manifest = PhaseTwoManifest.Preflight(db, session);
            db.Execute("UPDATE sessions SET state='COMPLETED',final_evidence_digest=$0 WHERE session_id=$1", manifest, session);
            db.Commit();
            PhaseTwoManifest.Verify(db, session);
        }
        catch (Exception exception)
        {
            db.Rollback();
            if (session != 0 && (string?)db.Scalar("SELECT state FROM sessions WHERE session_id=$0", session) == "RUNNING")
            {
                db.Begin();
                db.Diagnostic(session, null, null, null, exception is OperationCanceledException ? "CANCELLED" : "PREFLIGHT_FAILED", db.Path, exception.Message);
                db.Execute("UPDATE sessions SET state=$0,completed_utc=$1 WHERE session_id=$2", exception is OperationCanceledException ? "CANCELLED" : "FAILED", Utc(), session);
                db.Commit();
            }
            throw;
        }
        // Export failure leaves the already completed preflight sealed.
        return PhaseTwoReportWriter.Write(options.DatabasePath, session, options.OutputDirectory, cancellationToken, hooks);
    }

    private static string Utc() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private static string BuildSha() => PhaseTwoBuildProvenance.Current.EvidenceRevision;
    private static void StoreSource(PhaseTwoDatabase db, long session, CapturedPhaseOne source)
    {
        db.Insert("source_snapshots", "snapshot_id,preflight_id,repository_baseline_sha,phase1_implementation_sha,source_db_original_path,source_db_normalized_path,source_db_native_identity,source_db_size,source_db_sha256,source_db_before_mtime_ticks,source_db_after_mtime_ticks,source_application_id,source_schema_version,source_comparison_mode,source_scan_id,source_scan_started_utc,source_scan_completed_utc,admission_policy,seal_flag",
            session, session, PhaseTwoDatabase.Baseline, PhaseTwoDatabase.PhaseOneBaseline, source.OriginalPath, PathPolicy.Normalize(source.OriginalPath), source.Facts.Identity,
            source.Facts.Size, source.Sha256, source.Facts.MtimeTicks, source.Facts.MtimeTicks, 0x424D5341, 1, PhaseTwoDatabase.AbsoluteMode, source.ScanId, source.StartedUtc, source.CompletedUtc, "EXACT_ONLY", false);
        foreach (var root in source.Roots)
            db.Insert("source_library_roots", "snapshot_id,source_root_id,original_path,normalized_path,active,configured", session, root.Id, root.OriginalPath, root.Key, root.Active, root.Configured);
        foreach (var chart in source.Charts)
            db.Insert("source_charts", "snapshot_id,normalized_path,source_root_id,original_path,parent_folder,filename,extension,size,mtime_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status",
                session, chart.NormalizedPath, chart.RootId, chart.AbsolutePath, chart.ParentFolder, chart.Filename, chart.Extension, chart.Size, chart.MtimeUtcTicks, chart.Md5, chart.Sha256, chart.FirstSeenScan, chart.LastSeenScan, chart.Present, chart.Status);
        var ordinal = 0;
        foreach (var diagnostic in source.Diagnostics)
            db.Insert("source_diagnostics", "snapshot_id,ordinal,severity,code,path,message", session, ordinal++, diagnostic.Severity, diagnostic.Code, diagnostic.Path, diagnostic.Message);
        db.Execute("UPDATE source_snapshots SET captured_evidence_digest=$0,seal_flag=1 WHERE snapshot_id=$1", PhaseTwoManifest.Source(db, session), session);
    }

    private void Revalidate(PhaseTwoDatabase db, long session, Root root, CapturedPhaseOne source, CancellationToken token)
    {
        var expected = source.Charts.Where(c => c.Present && PathPolicy.Comparer.Equals(c.ParentFolder, root.Path)).ToDictionary(c => c.NormalizedPath, StringComparer.Ordinal);
        var observed = new Dictionary<string, string>(StringComparer.Ordinal);
        var safe = true;
        db.Begin();
        try
        {
            if (files.Metadata(root.Path).Kind != "DIRECTORY") throw new IOException("Observation root is not an ordinary directory.");
            foreach (var path in files.Children(root.Path))
            {
                token.ThrowIfCancellationRequested();
                if (!PhaseTwoFileSystem.IsChart(path)) continue;
                var entry = files.Metadata(path);
                if (entry.Kind == "DIRECTORY") continue;
                if (!observed.TryAdd(PathPolicy.Normalize(path), path)) throw new IOException("Ambiguous normalized direct-chart path.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            safe = false; db.Diagnostic(session, root.Component, root.Id, null, "STALE_PHASE_1_CANDIDATE", root.Path, "Direct enumeration/root safety failed: " + exception.Message);
        }
        foreach (var key in expected.Keys.Concat(observed.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            expected.TryGetValue(key, out var captured); observed.TryGetValue(key, out var path);
            var result = path is null ? new PhaseTwoChartRead(null, null, null, null, null, 0, 0, safe ? "MISSING" : "UNREADABLE") : files.ReadChart(path, token);
            var match = captured is null ? "UNEXPECTED" : path is null ? "MISSING" : result.Status != "SUCCESS" ? "UNKNOWN" :
                result.Sha256 == captured.Sha256 && Path.GetFileName(path) == captured.Filename ? "MATCH" : "CHANGED";
            var chartPath = path ?? captured!.AbsolutePath;
            db.Insert("chart_revalidations", "preflight_id,chart_key,captured_chart_key,observed_filename,before_size,before_mtime_ticks,after_size,after_mtime_ticks,fresh_sha256,bytes_read,attempts,read_status,match_status",
                session, key, captured?.NormalizedPath, Path.GetFileName(chartPath), result.BeforeSize, result.BeforeMtime, result.AfterSize, result.AfterMtime, result.Sha256, result.BytesRead, result.Attempts, result.Status, match);
            db.Insert("direct_chart_memberships", "preflight_id,observation_root_id,chart_key,captured_member,observed_member", session, root.Id, key, captured is not null, path is not null);
            if (match != "MATCH")
            {
                safe = false; db.Diagnostic(session, root.Component, root.Id, null, "STALE_PHASE_1_CANDIDATE", chartPath, "Direct chart read/membership/spelling/digest differs: " + result.Status + "/" + match, attempt: result.Attempts);
            }
            else if (result.AfterMtime != captured!.MtimeUtcTicks || result.AfterSize != captured.Size)
                db.Diagnostic(session, root.Component, root.Id, null, "CHART_METADATA_DRIFT", chartPath, "Fresh bytes match; metadata drift retained as preflight stamp.", "WARNING");
        }
        db.Execute("UPDATE observation_roots SET chart_state=$0 WHERE observation_root_id=$1", safe ? "CURRENT" : "STALE", root.Id);
        if (!safe) Reject(db, root.Component, "STALE_PHASE_1_CANDIDATE", "At least one member failed fresh direct-chart revalidation; new Phase 1/2A required.");
        db.Commit();
    }

    private static void Reject(PhaseTwoDatabase db, long component, string outcome, string reason) =>
        db.Execute("UPDATE candidate_components SET outcome=$0,reason=$1 WHERE component_id=$2 AND outcome<>'STALE_PHASE_1_CANDIDATE'", outcome, reason, component);

    internal static IEnumerable<(PhaseTwoEntry Entry, string? Error)> Walk(IPhaseTwoFileSystem files, IReadOnlyList<string> roots, CancellationToken token)
    {
        var stack = new Stack<IEnumerator<string>>();
        var top = roots.Where(r => !roots.Any(o => o != r && PathPolicy.Contains(o, r))).OrderBy(PathPolicy.Normalize, StringComparer.Ordinal);
        stack.Push(top.GetEnumerator());
        try
        {
            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var iterator = stack.Peek();
                bool more;
                string? enumerationError = null;
                try { more = iterator.MoveNext(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { more = false; enumerationError = exception.Message; }
                if (enumerationError is not null && iterator is DirectoryIterator directoryIterator)
                {
                    var failedPath = directoryIterator.Directory;
                    yield return (new(failedPath, "UNKNOWN", null, null, null, null, null, "ENUMERATION_ERROR", "UNKNOWN", null), enumerationError);
                }
                if (!more) { stack.Pop().Dispose(); continue; }
                var path = iterator.Current;
                PhaseTwoEntry entry;
                string? error = null;
                try { entry = files.Metadata(path); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { entry = new(path, "UNKNOWN", null, null, null, null, null, "METADATA_ERROR", "UNKNOWN", null); error = exception.Message; }
                yield return (entry, error);
                if (entry.Kind == "DIRECTORY")
                {
                    IEnumerator<string>? children = null;
                    try { children = files.Children(path).GetEnumerator(); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { error = exception.Message; }
                    if (children is not null) stack.Push(new DirectoryIterator(children, path));
                    else yield return (entry with { Status = "ENUMERATION_ERROR" }, error);
                }
            }
        }
        finally { while (stack.TryPop(out var iterator)) iterator.Dispose(); }
    }
    // Retains the directory path when lazy enumeration fails, allowing a local
    // incomplete view instead of losing all context or following a new target.
    private sealed class DirectoryIterator(IEnumerator<string> inner, string directory) : IEnumerator<string>
    {
        public string Directory => directory;
        public string Current => inner.Current;
        object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext()
        {
            try { return inner.MoveNext(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { throw new IOException("Directory enumeration failed: " + directory, exception); }
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => inner.Dispose();
    }
    private void Inventory(PhaseTwoDatabase db, long session, Root[] roots, CancellationToken token)
    {
        var batch = 0; db.Begin();
        foreach (var (entry, error) in Walk(files, roots.Select(r => r.Path).ToArray(), token))
        {
            var existing = db.Scalar("SELECT entry_id FROM preflight_entries WHERE preflight_id=$0 AND entry_key=$1", session, entry.Key);
            long id;
            if (existing is not null)
            {
                id = Convert.ToInt64(existing);
                if (entry.Status == "ENUMERATION_ERROR") db.Execute("UPDATE preflight_entries SET inventory_status='ENUMERATION_ERROR' WHERE entry_id=$0", id);
                else { Affect(db, session, roots, entry, id, "AMBIGUOUS_ENTRY", "Duplicate normalized entry identity."); continue; }
            }
            else
            {
                id = db.Insert("preflight_entries", "preflight_id,entry_key,original_path,kind,size,mtime_ticks,attributes,extension,is_chart,inventory_status,readability,native_identity",
                    session, entry.Key, entry.OriginalPath, entry.Kind, entry.Size, entry.MtimeTicks, entry.Attributes, entry.Extension, entry.IsChart, entry.Status, entry.Readability, entry.NativeIdentity);
                if (entry.Kind == "REGULAR_FILE") db.Insert("physical_files", "preflight_id,entry_id,native_identity", session, id, entry.NativeIdentity);
                foreach (var root in roots.Where(r => PathPolicy.Contains(r.Path, entry.OriginalPath)))
                {
                    var relative = Path.GetRelativePath(root.Path, entry.OriginalPath).Replace('\\', '/');
                    if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new InvalidOperationException("Invalid observation membership.");
                    db.Insert("observation_memberships", "preflight_id,observation_root_id,entry_id,relative_path,relative_path_key", session, root.Id, id, relative, relative);
                }
            }
            if (entry.Status != "OK" || entry.Kind is not ("DIRECTORY" or "REGULAR_FILE"))
                Affect(db, session, roots, entry, id, "OBSERVED_SUBTREE_INCOMPLETE", error ?? "Unsafe/nonregular entry; target not followed.");
            if (++batch == 256) { db.Commit(); db.Begin(); batch = 0; }
        }
        db.Commit();
    }
    private static void Affect(PhaseTwoDatabase db, long session, Root[] roots, PhaseTwoEntry entry, long? entryId, string code, string message)
    {
        foreach (var root in roots.Where(r => PathPolicy.Contains(r.Path, entry.OriginalPath)))
        {
            db.Diagnostic(session, root.Component, root.Id, entryId, code, entry.OriginalPath, message);
            Reject(db, root.Component, "OBSERVED_SUBTREE_INCOMPLETE", "Observed subtree incomplete/changed; new Phase 2A required.");
        }
    }
    private void VerifyInventory(PhaseTwoDatabase db, long session, Root[] roots, CancellationToken token)
    {
        db.Execute("CREATE TEMP TABLE verified_entries(entry_key TEXT PRIMARY KEY)");
        var batch = 0; db.Begin();
        foreach (var (entry, error) in Walk(files, roots.Select(r => r.Path).ToArray(), token))
        {
            var rows = db.Rows("SELECT entry_id,original_path,kind,size,mtime_ticks,attributes,extension,is_chart,inventory_status,readability,native_identity FROM preflight_entries WHERE preflight_id=$0 AND entry_key=$1", session, entry.Key).ToArray();
            db.Execute("INSERT OR IGNORE INTO verified_entries VALUES($0)", entry.Key);
            var different = rows.Length != 1;
            if (!different)
            {
                var row = rows[0];
                var before = new PhaseTwoEntry((string)row[1]!, (string)row[2]!, Long(row[3]), Long(row[4]), Long(row[5]), (string?)row[6], row[7] is null ? null : Convert.ToInt64(row[7]) != 0, (string)row[8]!, (string)row[9]!, (string?)row[10]);
                different = before != entry;
            }
            if (different || error is not null || entry.Status != "OK")
            {
                Affect(db, session, roots, entry, rows.Length == 1 ? Convert.ToInt64(rows[0][0]) : null, "INVENTORY_CHANGED", "Second enumeration does not verify the first manifest.");
                if (PhaseTwoFileSystem.IsChart(entry.OriginalPath)) DirectDrift(db, session, roots, entry.OriginalPath);
            }
            if (++batch == 256) { db.Commit(); db.Begin(); batch = 0; }
        }
        foreach (var missing in db.Rows("SELECT entry_id,original_path FROM preflight_entries WHERE preflight_id=$0 AND entry_key NOT IN (SELECT entry_key FROM verified_entries)", session))
        {
            var path = (string)missing[1]!;
            Affect(db, session, roots, new(path, "UNKNOWN", null, null, null, null, null, "MISSING", "UNKNOWN", null), Convert.ToInt64(missing[0]), "INVENTORY_CHANGED", "Entry missing during final manifest verification.");
            if (PhaseTwoFileSystem.IsChart(path)) DirectDrift(db, session, roots, path);
        }
        // Direct-chart fresh read stamps must agree with the FIRST inventory too.
        foreach (var drift in db.Rows("SELECT r.original_path,q.chart_key FROM chart_revalidations q JOIN direct_chart_memberships m ON m.preflight_id=q.preflight_id AND m.chart_key=q.chart_key JOIN observation_roots r ON r.observation_root_id=m.observation_root_id AND r.preflight_id=m.preflight_id LEFT JOIN preflight_entries e ON e.preflight_id=q.preflight_id AND e.entry_key=q.chart_key WHERE q.preflight_id=$0 AND q.match_status='MATCH' AND (e.entry_id IS NULL OR e.kind<>'REGULAR_FILE' OR e.size<>q.after_size OR e.mtime_ticks<>q.after_mtime_ticks)", session))
            DirectDrift(db, session, roots, (string)db.Scalar("SELECT original_path FROM source_charts WHERE snapshot_id=$0 AND normalized_path=$1", session, drift[1])!);
        foreach (var unexpected in db.Rows("""
            SELECT e.original_path FROM observation_memberships m JOIN preflight_entries e ON e.entry_id=m.entry_id AND e.preflight_id=m.preflight_id
            WHERE m.preflight_id=$0 AND e.kind='REGULAR_FILE' AND e.is_chart=1 AND instr(m.relative_path,'/')=0
            AND NOT EXISTS(SELECT 1 FROM direct_chart_memberships d WHERE d.preflight_id=m.preflight_id AND d.observation_root_id=m.observation_root_id AND d.chart_key=e.entry_key AND d.observed_member=1)
            """, session)) DirectDrift(db, session, roots, (string)unexpected[0]!);
        db.Commit();
        db.Execute("DROP TABLE verified_entries");
    }
    private static long? Long(object? value) => value is null ? null : Convert.ToInt64(value);
    private static void DirectDrift(PhaseTwoDatabase db, long session, Root[] roots, string path)
    {
        foreach (var root in roots.Where(r => PathPolicy.Comparer.Equals(r.Path, Path.GetDirectoryName(path))))
        {
            Reject(db, root.Component, "STALE_PHASE_1_CANDIDATE", "Direct-chart manifest changed; new Phase 1 and Phase 2A required.");
            db.Execute("UPDATE observation_roots SET chart_state='STALE' WHERE observation_root_id=$0", root.Id);
            db.Diagnostic(session, root.Component, root.Id, null, "STALE_PHASE_1_CANDIDATE", path, "Direct-chart membership/stamp differs at final verification.");
        }
    }
    private static void ValidateCompletion(PhaseTwoDatabase db, long session)
    {
        db.CheckIntegrity();
        if (db.Number("SELECT count(*) FROM candidate_components c WHERE c.preflight_id=$0 AND c.member_count<>(SELECT count(*) FROM component_memberships m WHERE m.component_id=c.component_id AND m.preflight_id=c.preflight_id)", session) != 0 ||
            db.Number("SELECT count(*) FROM observation_roots r JOIN component_memberships m ON m.observation_root_id=r.observation_root_id AND m.preflight_id=r.preflight_id JOIN candidate_components c ON c.component_id=m.component_id AND c.preflight_id=m.preflight_id WHERE r.preflight_id=$0 AND c.outcome='ADMITTED' AND (r.chart_state<>'CURRENT' OR r.inventory_state<>'COMPLETE' OR NOT EXISTS(SELECT 1 FROM observation_memberships n WHERE n.preflight_id=r.preflight_id AND n.observation_root_id=r.observation_root_id AND n.relative_path='.'))", session) != 0)
            throw new InvalidOperationException("Incomplete admission coverage.");
    }
}
