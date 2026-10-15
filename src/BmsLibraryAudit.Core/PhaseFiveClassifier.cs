using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class PhaseFiveClassifier
{
    public static PhaseFiveResult Classify(PhaseFiveOptions options, PhaseFiveLimits? limits = null, CancellationToken token = default)
        => ClassifyCore(options, limits, token, null);
    internal static PhaseFiveResult ClassifyCore(PhaseFiveOptions options, PhaseFiveLimits? limits, CancellationToken token, PhaseThreePublicationTestHooks? hooks)
    {
        limits ??= new(); ValidateLimits(limits); token.ThrowIfCancellationRequested(); PhaseTwoSafety.RequirePlatform();
        if (options.ContentSessionId <= 0 || !PhaseOneSourceCapture.Digest(options.AcceptedContentManifestSha256, 64) ||
            !PhaseOneSourceCapture.Digest(options.ComponentKey, 64)) throw new ArgumentException("Exact CONTENT digest, positive session and component key required.");
        var database = PathPolicy.Absolute(options.DatabasePath); var output = PathPolicy.Absolute(options.OutputDirectory);
        var inputs = (options.PhaseFourReports ?? []).Select(PathPolicy.Absolute).ToArray();
        if (inputs.Any(p => PathPolicy.Contains(output, p)) || PathPolicy.Contains(output, database)) throw new InvalidOperationException("Output overlaps input evidence.");
        var before = PhaseTwoSafety.Inspect(database);
        using var db = PhaseTwoDatabase.Open(database, false); db.Begin(readOnly: true); db.CheckIntegrity();
        if ((string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0 AND stage='CONTENT' AND state='COMPLETED'", options.ContentSessionId) != options.AcceptedContentManifestSha256)
            throw new InvalidOperationException("Exact accepted COMPLETED CONTENT digest required.");
        PhaseTwoManifest.VerifyContent(db, options.ContentSessionId, token);
        var parent = PhaseTwoContentRunner.Parent(db, options.ContentSessionId);
        var component = db.Rows("""
            SELECT c.component_id,c.source_component_label,c.outcome,o.outcome,o.comparison_eligible,o.coherent,o.sealed
            FROM candidate_components c JOIN content_component_outcomes o ON o.preflight_id=c.preflight_id AND o.component_id=c.component_id
            WHERE c.preflight_id=$0 AND c.component_key=$1 AND o.content_session_id=$2
            """, parent, options.ComponentKey, options.ContentSessionId).SingleOrDefault();
        if (component is null || (string)component[2]! != "ADMITTED" || (string)component[3]! != "COMPLETE" ||
            component.Skip(4).Any(v => Convert.ToInt64(v) != 1)) throw new InvalidOperationException("Only ADMITTED EXACT_ONLY, COMPLETE comparison-eligible components may be classified.");
        var id = Convert.ToInt64(component[0]);
        // Source provenance and EXACT_ONLY graph have already been reproduced by
        // VerifyContent/its parent validator. Explicitly retain the source seals.
        foreach (var snapshot in db.Rows("SELECT snapshot_id,captured_evidence_digest,seal_flag FROM source_snapshots"))
            if (Convert.ToInt64(snapshot[2]) != 1 || (string?)snapshot[1] != PhaseTwoManifest.Source(db, Convert.ToInt64(snapshot[0])))
                throw new InvalidOperationException("Untrusted protected-root provenance.");
        var libraries = db.Rows("SELECT DISTINCT original_path FROM source_library_roots ORDER BY original_path COLLATE ORDINAL").Select(r => (string)r[0]!).ToArray();
        var files = db.Rows("SELECT DISTINCT source_db_original_path FROM source_snapshots").Select(r => (string)r[0]!).Append(database).ToArray();
        var destination = new PhaseFourResolver.Destination(output, libraries, files); destination.Validate(null, true);
        var provenance = new PhaseFiveProvenance(options.ContentSessionId, options.AcceptedContentManifestSha256, parent,
            (string)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", parent)!, id, options.ComponentKey,
            (string)db.Scalar("SELECT captured_evidence_digest FROM source_snapshots WHERE snapshot_id=$0", parent)!,
            (string)db.Scalar("SELECT started_utc FROM sessions WHERE session_id=$0", options.ContentSessionId)!,
            (string)db.Scalar("SELECT completed_utc FROM sessions WHERE session_id=$0", options.ContentSessionId)!, ReportWriter.ToolVersion,
            PhaseTwoBuildProvenance.Current.EvidenceRevision);
        var diagnostics = new List<string>();
        var totalRoots = db.Number("SELECT count(*) FROM component_memberships WHERE preflight_id=$0 AND component_id=$1", parent, id);
        var rootRows = db.Rows("""
            SELECT r.observation_root_id,r.root_key,r.original_path FROM component_memberships m JOIN observation_roots r
            ON r.preflight_id=m.preflight_id AND r.observation_root_id=m.observation_root_id
            WHERE m.preflight_id=$0 AND m.component_id=$1 ORDER BY r.root_key COLLATE ORDINAL LIMIT $2
            """, parent, id, limits.MaximumRoots);
        if (totalRoots > limits.MaximumRoots) diagnostics.Add("ROOT_LIMIT: excess observation roots remain unclassified in original evidence.");
        var roots = new List<PhaseFiveRoot>(); var remaining = limits.MaximumEntries;
        foreach (var row in rootRows.Take(limits.MaximumRoots))
        {
            token.ThrowIfCancellationRequested(); var root = Convert.ToInt64(row[0]);
            var count = db.Number("SELECT count(*) FROM observation_memberships WHERE preflight_id=$0 AND observation_root_id=$1", parent, root);
            PhaseFiveEntry[] entries = [];
            if (count > remaining) diagnostics.Add("ENTRY_LIMIT: root " + root + " unclassified; no partial tree promoted.");
            else { entries = Entries(db, options.ContentSessionId, parent, id, root); remaining -= entries.Length; }
            var nested = rootRows.Where(r => Convert.ToInt64(r[0]) != root && PathPolicy.Contains((string)row[2]!, (string)r[2]!)).Select(r => Convert.ToInt64(r[0])).ToArray();
            var parents = entries.Where(e => e.IsChart).Select(e => Path.GetDirectoryName(e.OriginalPath)!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            roots.Add(new(root, (string)row[1]!, (string)row[2]!, libraries.Where(p => PathPolicy.Contains(p, (string)row[2]!)).ToArray(), parents, nested,
                parents.Length > 1 || nested.Length > 0 ? "CONTAINER_DIRECTORY_CANDIDATE" : "OBSERVATION_ROOT_ONLY", "PACKAGE_BOUNDARY_UNKNOWN",
                entries.LongLength == count ? "PROVEN_FROM_ACCEPTED_CONTENT" : "BLOCKED", "MISSING_CHART_REPORTS", "UNKNOWN", entries, []));
        }
        var inputStamps = new Dictionary<string, NativePathFacts>(PathPolicy.Comparer);
        var admitted = new List<PhaseFiveDependency>(); long bytesRead = 0, semanticWork = 0;
        if (inputs.Length > limits.MaximumPhaseFourReports) diagnostics.Add("PHASE4_REPORT_COUNT_LIMIT");
        foreach (var input in inputs.Take(limits.MaximumPhaseFourReports))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                PathPolicy.RejectReparseAncestors(input);
                if (libraries.Any(p => PathPolicy.Contains(p, input)) || !string.Equals(Path.GetExtension(input), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Report must be JSON outside protected source libraries.");
                var stamp = PhaseTwoSafety.Inspect(input);
                if ((stamp.Attributes & (uint)FileAttributes.Directory) != 0 || stamp.LinkCount != 1 ||
                    db.Number("SELECT count(*) FROM preflight_entries WHERE kind='REGULAR_FILE' AND native_identity=$0", stamp.Identity) != 0)
                    throw new InvalidOperationException("Report aliases source/evidence payload or is not an ordinary input.");
                if (stamp.Size > limits.MaximumInputBytes - bytesRead) { diagnostics.Add("PHASE4_INPUT_BYTE_LIMIT: " + input); continue; }
                bytesRead += stamp.Size;
                using var stream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, input) != stamp) throw new IOException("Report changed before read.");
                var bytes = new byte[checked((int)stamp.Size)]; stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1 || PhaseTwoSafety.Inspect(stream.SafeFileHandle, input) != stamp || PhaseTwoSafety.Inspect(input) != stamp) throw new IOException("Report changed during read.");
                inputStamps[input] = stamp;
                using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 96 }); PhaseFourResolver.RejectDuplicateProperties(document.RootElement);
                var report = JsonSerializer.Deserialize<PhaseFourResult>(bytes, PhaseFourResolver.Json) ?? throw new InvalidOperationException("Missing Phase 4 result.");
                var root = roots.SingleOrDefault(r => r.Id == report.Provenance.ObservationRootId && r.PhysicalEvidenceState == "PROVEN_FROM_ACCEPTED_CONTENT")
                    ?? throw new InvalidOperationException("Report is outside admitted roots/component or root budget.");
                var chart = root.Entries.SingleOrDefault(e => e.EntryId == report.Provenance.Chart.EntryId && e.IsChart)
                    ?? throw new InvalidOperationException("Report chart outside admitted membership.");
                var sourceRoot = (string?)db.Scalar("""
                    SELECT r.original_path FROM source_charts c JOIN source_library_roots r ON r.snapshot_id=c.snapshot_id AND r.source_root_id=c.source_root_id
                    WHERE c.snapshot_id=$0 AND c.normalized_path=$1 AND c.present=1 AND c.status='OK'
                    """, parent, chart.EntryKey) ?? throw new InvalidOperationException("Chart source root cannot be admitted.");
                var work = report.Semantics?.Relations.LongLength ?? 0;
                if (work > limits.MaximumSemanticRelations - semanticWork) { diagnostics.Add("SEMANTIC_RELATION_LIMIT: " + input); continue; }
                semanticWork += work;
                var verified = PhaseFiveAdmission.Verify(report, provenance, root, sourceRoot, token);
                admitted.Add(new(root.Id, chart.EntryId, chart.Sha256!, "ADMITTED_RECONSTRUCTED_PHASE4",
                    verified.Semantics!.BoundedProof.State == "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE" ? "PROVEN_WITHIN_NAMED_MODEL" : "UNKNOWN", [],
                    Convert.ToHexStringLower(SHA256.HashData(bytes)), verified, PhaseFiveAdmission.ResolverBuildRevisionState));
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or NullReferenceException or KeyNotFoundException or OverflowException)
            { diagnostics.Add("UNVERIFIED_PHASE4_REPORT: " + input + ": " + e.Message); }
        }
        for (var i = 0; i < roots.Count; i++)
        {
            var root = roots[i]; var dependencies = new List<PhaseFiveDependency>();
            foreach (var chart in root.Entries.Where(e => e.IsChart)) {
                var matches = admitted.Where(d => d.RootId == root.Id && d.ChartEntryId == chart.EntryId).ToArray();
                if (matches.Length != 1) {
                    var reason = matches.Length == 0 ? "MISSING_PHASE4_REPORT" : "DUPLICATE_OR_CONTRADICTORY_PHASE4_REPORTS";
                    dependencies.Add(new(root.Id, chart.EntryId, chart.Sha256!, reason, "UNKNOWN", [reason], null, null));
                    diagnostics.Add(reason + ": root " + root.Id + " chart " + chart.EntryId);
                } else dependencies.Add(matches[0]);
            }
            var all = dependencies.Count > 0 && dependencies.All(d => d.VerifiedReport is not null);
            var proven = all && dependencies.All(d => d.VerifiedReport!.Semantics!.BoundedProof.State == "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE");
            if (all && !proven) diagnostics.Add("DEPENDENCY_COVERAGE_NOT_PROVEN: root " + root.Id + "; admitted obligations/uncertainties retained.");
            roots[i] = root with { Dependencies = dependencies.ToArray(), SemanticCoverage = all ? "COMPLETE_CHART_REPORT_ADMISSION" : "INCOMPLETE",
                SemanticTrust = proven ? "PROVEN_WITHIN_NAMED_MODEL" : "UNKNOWN" };
        }
        var pairs = new List<PhaseFivePair>(); var expected = checked(totalRoots * (totalRoots - 1) / 2);
        var pairEntryBudget = limits.MaximumEntries; var conflictBudget = limits.MaximumConflicts;
        var referenceIndexes = roots.ToDictionary(r => r.Id, r => PhaseFiveClassification.ReferenceIndex(r, token));
        for (var i = 0; i < roots.Count; i++) for (var j = i + 1; j < roots.Count; j++)
        {
            token.ThrowIfCancellationRequested();
            if (pairs.Count == limits.MaximumPairs) continue;
            var a = roots[i]; var b = roots[j];
            if (a.PhysicalEvidenceState != "PROVEN_FROM_ACCEPTED_CONTENT" || b.PhysicalEvidenceState != "PROVEN_FROM_ACCEPTED_CONTENT") continue;
            if (a.Entries.Length + b.Entries.Length > pairEntryBudget) { diagnostics.Add("PAIR_ENTRY_BUDGET_LIMIT"); continue; }
            pairEntryBudget -= a.Entries.Length + b.Entries.Length;
            var comparison = PhaseTwoContentComparison.Derive(PhaseTwoContentComparison.View(db, options.ContentSessionId, parent, a.Id),
                PhaseTwoContentComparison.View(db, options.ContentSessionId, parent, b.Id), token);
            var persisted = db.Rows("SELECT " + PhaseTwoContentComparison.ValueColumns + " FROM comparisons WHERE content_session_id=$0 AND component_id=$1 AND root_a_id=$2 AND root_b_id=$3",
                options.ContentSessionId, id, a.Id, b.Id).SingleOrDefault();
            if (persisted is null || !persisted.Select(Convert.ToInt64).SequenceEqual(comparison.Counts.Concat(comparison.Flags.Select(f => f ? 1L : 0L)))) {
                diagnostics.Add("BLOCKED_COMPARISON: " + a.Id + ":" + b.Id); continue;
            }
            var pair = PhaseFiveClassification.Pair(a, b, comparison, limits with { MaximumConflicts = conflictBudget }, token,
                referenceIndexes[a.Id], referenceIndexes[b.Id]);
            conflictBudget -= pair.Conflicts.Length; pairs.Add(pair);
        }
        if (pairs.Count != expected) diagnostics.Add("PAIR_COVERAGE_INCOMPLETE: unclassified pairs remain in original CONTENT evidence.");
        if (pairs.Any(p => p.ConflictCoverage != "COMPLETE")) diagnostics.Add("CONFLICT_LIMIT");
        var discovery = Discover(db, parent, roots, limits, diagnostics, token);
        var result = new PhaseFiveResult("phase5-conservative-classification-2", provenance, diagnostics.Count == 0 ? "COMPLETE" : "INCOMPLETE", totalRoots, expected,
            roots.ToArray(), pairs.ToArray(), discovery, diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ["EXACT_ONLY admitted COMPLETE components only; observations are historical, not simultaneous immutable snapshots or current filesystem truth.",
             "Observation roots/containers are not proven packages. No transitive equivalence, source mutation, deletion list or recoverable-byte claim.",
             "Semantic admission recomputes the current Phase 4 graph from CONTENT-bound included bytes; absent/unverified reports leave dependencies UNKNOWN without invalidating physical dimensions.",
             "Phase 4 resolver_build_revision is a declared historical value, not authenticated producer-build identity; current semantic reconstruction does not authenticate that claim.",
             "Existing CONTENT/source seal verification has scope-sized cost before Phase 5 budgets. This synthetic increment is not large-library acceptance.",
             "Native report exclusion reduces namespace risks, not universal race freedom or destructive safety."], limits);
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result, PhaseFourResolver.Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        if (json.Length > limits.MaximumOutputBytes) throw new InvalidOperationException("INCOMPLETE_OUTPUT_LIMIT: no partial report published.");
        var publicationDestination = new PhaseFourResolver.Destination(output, libraries, files.Concat(inputStamps.Keys).ToArray());
        void Validate(string? temporary, bool empty) {
            if (empty) token.ThrowIfCancellationRequested();
            if (PhaseTwoSafety.Inspect(database) != before) throw new IOException("Evidence DB binding changed.");
            foreach (var stamp in inputStamps) if (PhaseTwoSafety.Inspect(stamp.Key) != stamp.Value) throw new IOException("Input report changed before publication.");
            foreach (var input in inputs) if (PathPolicy.Contains(output, input)) throw new IOException("Protected input overlap.");
            publicationDestination.Validate(temporary, empty);
        }
        ObservedReportPublication.Publish(output, "classification_report.json", json, Validate, token, hooks);
        return result;
    }
    internal static PhaseFiveEntry[] Entries(PhaseTwoDatabase db, long session, long parent, long component, long root) => db.Rows("""
        SELECT e.entry_id,p.physical_file_id,e.entry_key,e.original_path,m.relative_path,e.kind,e.native_identity,h.sha256,e.size,e.mtime_ticks,e.attributes,e.is_chart,
          (SELECT c.sha256 FROM source_charts c WHERE c.snapshot_id=$1 AND c.normalized_path=e.entry_key AND c.present=1 AND c.status='OK')
        FROM observation_memberships m JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id
        LEFT JOIN physical_files p ON p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id
        LEFT JOIN content_hashes h ON h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id AND h.content_session_id=$0
        WHERE m.preflight_id=$1 AND m.observation_root_id=$2 ORDER BY m.relative_path_key COLLATE ORDINAL,e.entry_key COLLATE ORDINAL
        """, session, parent, root).Select(r => new PhaseFiveEntry(Convert.ToInt64(r[0]), r[1] is null ? null : Convert.ToInt64(r[1]), root, component,
            (string)r[2]!, (string)r[3]!, (string)r[4]!, (string)r[5]!, (string?)r[6], (string?)r[7], r[8] is null ? null : Convert.ToInt64(r[8]),
            r[9] is null ? null : Convert.ToInt64(r[9]), r[10] is null ? null : Convert.ToInt64(r[10]), r[11] is not null && Convert.ToInt64(r[11]) != 0, (string?)r[12])).ToArray();
    private static PhaseFiveDiscovery[] Discover(PhaseTwoDatabase db, long parent, List<PhaseFiveRoot> roots, PhaseFiveLimits limits, List<string> diagnostics, CancellationToken token)
    {
        if (db.Number("SELECT count(*) FROM source_charts WHERE snapshot_id=$0", parent) > limits.MaximumDiscoveryCharts ||
            db.Number("SELECT count(DISTINCT parent_folder) FROM source_charts WHERE snapshot_id=$0 AND present=1 AND status='OK'", parent) > limits.MaximumDiscoveryFolders) {
            diagnostics.Add("PHASE1_DISCOVERY_LIMIT: captured source retained, discovery unclassified."); return [];
        }
        var charts = db.Rows("SELECT source_root_id,original_path,normalized_path,parent_folder,filename,extension,size,mtime_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status FROM source_charts WHERE snapshot_id=$0 ORDER BY normalized_path COLLATE ORDINAL", parent)
            .Select(r => new ChartRecord(Convert.ToInt64(r[0]), (string)r[1]!, (string)r[2]!, (string)r[3]!, (string)r[4]!, (string)r[5]!, Convert.ToInt64(r[6]), Convert.ToInt64(r[7]),
                (string?)r[8], (string?)r[9], Convert.ToInt64(r[10]), Convert.ToInt64(r[11]), Convert.ToInt64(r[12]) != 0, (string)r[13]!)).ToArray();
        var capturedDiagnostics = db.Rows("SELECT severity,code,path,message FROM source_diagnostics WHERE snapshot_id=$0 ORDER BY code COLLATE ORDINAL,path COLLATE ORDINAL,severity COLLATE ORDINAL,message COLLATE ORDINAL", parent)
            .Select(r => new Diagnostic((string)r[0]!, (string)r[1]!, (string)r[2]!, (string)r[3]!)).ToArray();
        token.ThrowIfCancellationRequested(); var analysis = RelationshipAnalyzer.Analyze(charts, capturedDiagnostics);
        foreach (var folder in analysis.IncompleteFolders)
            diagnostics.Add("PHASE1_DISCOVERY_INCOMPLETE: " + folder + "; excluded by captured coverage evidence; relationships remain UNKNOWN, not proven absent.");
        var pending = analysis.Candidates.Where(c => !roots.Any(a => a.PhysicalEvidenceState == "PROVEN_FROM_ACCEPTED_CONTENT" && a.Key == PathPolicy.Normalize(c.FolderA)) ||
            !roots.Any(b => b.PhysicalEvidenceState == "PROVEN_FROM_ACCEPTED_CONTENT" && b.Key == PathPolicy.Normalize(c.FolderB))).ToArray();
        if (pending.Length > limits.MaximumDiscoveryPairs) diagnostics.Add("PHASE1_DISCOVERY_PAIR_LIMIT");
        return pending.Take(limits.MaximumDiscoveryPairs).Select(c => new PhaseFiveDiscovery(c.FolderA, c.FolderB, c.Relationship, c.ChartsA, c.ChartsB, c.Common,
            "CANDIDATE_FROM_PHASE1", "PHYSICAL_COMPARISON_NOT_ADMITTED")).ToArray();
    }
    private static void ValidateLimits(PhaseFiveLimits limits)
    {
        var max = new PhaseFiveLimits();
        foreach (var property in typeof(PhaseFiveLimits).GetProperties()) {
            var value = (int)property.GetValue(limits)!; if (value < 1 || value > (int)property.GetValue(max)!) throw new ArgumentException("Phase 5 budgets must be positive and may only reduce hard caps.");
        }
    }
}
