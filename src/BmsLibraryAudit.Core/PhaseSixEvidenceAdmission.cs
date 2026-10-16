namespace BmsLibraryAudit.Core;

// Owns the existing read-only DB guard/transaction until validation/publication ends.
internal sealed class PhaseSixEvidenceAdmission : IDisposable
{
    internal PhaseTwoDatabase Database { get; }
    internal PhaseFiveProvenance Provenance { get; private set; } = null!;
    internal PhaseSixRoot[] Roots { get; private set; } = [];
    internal PhaseSixConflict[] Conflicts { get; private set; } = [];
    internal PhaseSixBudget Budget { get; private set; } = null!;
    internal string[] Libraries { get; private set; } = [];
    internal string[] ProtectedFiles { get; private set; } = [];
    private readonly string path;
    private readonly NativePathFacts initial;
    private PhaseSixEvidenceAdmission(string path) {
        this.path = path; initial = PhaseTwoSafety.Inspect(path);
        Database = PhaseTwoDatabase.Open(path, false);
    }
    internal static PhaseSixEvidenceAdmission Load(PhaseSixOptions options, PhaseSixLimits limits, CancellationToken token)
        => LoadCore(options, limits, token, 2, 2);
    // The historical two-root entry point remains exact; only the new group consumer
    // can request a wider bounded selection through this shared admission kernel.
    internal static PhaseSixEvidenceAdmission LoadGroup(PhaseSixOptions options, PhaseSixLimits limits, CancellationToken token)
        => LoadCore(options, limits, token, 3, 6);
    private static PhaseSixEvidenceAdmission LoadCore(PhaseSixOptions options, PhaseSixLimits limits, CancellationToken token, int minimum, int maximum)
    {
        PhaseSixFormat.ValidateLimits(limits); token.ThrowIfCancellationRequested(); PhaseTwoSafety.RequirePlatform();
        if (options.ContentSessionId < 1 || !PhaseOneSourceCapture.Digest(options.AcceptedContentManifestSha256, 64) ||
            !PhaseOneSourceCapture.Digest(options.ComponentKey, 64) || options.RootKeys is null || options.RootKeys.Count < minimum || options.RootKeys.Count > maximum ||
            options.RootKeys.Any(string.IsNullOrWhiteSpace) || options.RootKeys.Distinct(StringComparer.Ordinal).Count() != options.RootKeys.Count)
            throw new ArgumentException(minimum == 2 ? "Exact accepted CONTENT/component keys and exactly two distinct sealed root keys required."
                : "Exact accepted CONTENT/component keys and three to six distinct sealed root keys required.");
        var admitted = new PhaseSixEvidenceAdmission(PathPolicy.Absolute(options.DatabasePath));
        try { admitted.Read(options, limits, token); return admitted; }
        catch { admitted.Dispose(); throw; }
    }
    private void Read(PhaseSixOptions options, PhaseSixLimits limits, CancellationToken token)
    {
        var db = Database; db.Begin(readOnly: true); db.CheckIntegrity();
        if ((string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0 AND stage='CONTENT' AND state='COMPLETED'", options.ContentSessionId) != options.AcceptedContentManifestSha256)
            throw new InvalidOperationException("Exact accepted COMPLETED CONTENT required.");
        PhaseTwoManifest.VerifyContent(db, options.ContentSessionId, token);
        var budget = Budget = new PhaseSixBudget(limits, token); budget.Check();
        var parent = PhaseTwoContentRunner.Parent(db, options.ContentSessionId);
        var component = db.Rows("""
            SELECT c.component_id,c.outcome,o.outcome,o.comparison_eligible,o.coherent,o.sealed
            FROM candidate_components c JOIN content_component_outcomes o ON o.preflight_id=c.preflight_id AND o.component_id=c.component_id
            WHERE c.preflight_id=$0 AND c.component_key=$1 AND o.content_session_id=$2
            """, parent, options.ComponentKey, options.ContentSessionId).SingleOrDefault();
        if (component is null || (string)component[1]! != "ADMITTED" || (string)component[2]! != "COMPLETE" || component.Skip(3).Any(x => Convert.ToInt64(x) != 1))
            throw new InvalidOperationException("Only ADMITTED COMPLETE coherent sealed comparison-eligible EXACT_ONLY components are allowed.");
        var id = Convert.ToInt64(component[0]);
        Libraries = db.Rows("SELECT DISTINCT original_path FROM source_library_roots ORDER BY original_path COLLATE ORDINAL").Select(r => (string)r[0]!).ToArray();
        ProtectedFiles = db.Rows("SELECT DISTINCT source_db_original_path FROM source_snapshots").Select(r => (string)r[0]!).Append(path).ToArray();
        foreach (var snapshot in db.Rows("SELECT snapshot_id,captured_evidence_digest,seal_flag FROM source_snapshots"))
            if (Convert.ToInt64(snapshot[2]) != 1 || (string?)snapshot[1] != PhaseTwoManifest.Source(db, Convert.ToInt64(snapshot[0])))
                throw new InvalidOperationException("Protected-root source seal mismatch.");
        var roots = new List<PhaseSixRoot>(); var facts = new List<NativePathFacts>(); long count = 0;
        foreach (var key in options.RootKeys.Order(StringComparer.Ordinal)) {
            budget.Check();
            var row = db.Rows("""
                SELECT r.observation_root_id,r.original_path FROM observation_roots r JOIN component_memberships m
                ON m.preflight_id=r.preflight_id AND m.observation_root_id=r.observation_root_id
                WHERE r.preflight_id=$0 AND m.component_id=$1 AND r.root_key=$2
                """, parent, id, key).SingleOrDefault() ?? throw new InvalidOperationException("Root is outside selected sealed component.");
            var root = Convert.ToInt64(row[0]);
            count += db.Number("SELECT count(*) FROM observation_memberships WHERE preflight_id=$0 AND observation_root_id=$1", parent, root);
            if (count > limits.MaximumEntries) throw new InvalidOperationException("UNION_NEEDS_EVIDENCE: source entry budget exceeded.");
            var entries = PhaseFiveClassifier.Entries(db, options.ContentSessionId, parent, id, root);
            if (entries.Any(e => string.IsNullOrEmpty(e.NativeIdentity) || e.Kind is not ("DIRECTORY" or "REGULAR_FILE") ||
                e.Kind == "REGULAR_FILE" && (!PhaseOneSourceCapture.Digest(e.Sha256, 64) || e.PhysicalFileId is null)))
                throw new InvalidOperationException("Complete entry identity and successful hashes required.");
            var rootEntry = entries.SingleOrDefault(e => e.RelativePath == "." && e.Kind == "DIRECTORY")
                ?? throw new InvalidOperationException("Missing root directory membership.");
            var native = PhaseTwoSafety.Inspect((string)row[1]!);
            if (native.Identity != rootEntry.NativeIdentity) throw new InvalidOperationException("Root native binding changed since observation.");
            roots.Add(new(root, key, (string)row[1]!, entries)); facts.Add(native);
        }
        bool Within(string a, string b) => a == b || b.StartsWith(a.TrimEnd('\\') + "\\", StringComparison.Ordinal);
        for (var a = 0; a < roots.Count; a++) for (var b = a + 1; b < roots.Count; b++)
        if (PathPolicy.Contains(roots[a].OriginalPath, roots[b].OriginalPath) || PathPolicy.Contains(roots[b].OriginalPath, roots[a].OriginalPath) ||
            facts[a].Identity == facts[b].Identity || Within(facts[a].ResolvedPath, facts[b].ResolvedPath) || Within(facts[b].ResolvedPath, facts[a].ResolvedPath))
            throw new InvalidOperationException("Observation roots must be physically disjoint, nonnested and nonaliased.");
        Roots = roots.ToArray();
        Provenance = new(options.ContentSessionId, options.AcceptedContentManifestSha256, parent,
            (string)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", parent)!, id, options.ComponentKey,
            (string)db.Scalar("SELECT captured_evidence_digest FROM source_snapshots WHERE snapshot_id=$0", parent)!,
            (string)db.Scalar("SELECT started_utc FROM sessions WHERE session_id=$0", options.ContentSessionId)!,
            (string)db.Scalar("SELECT completed_utc FROM sessions WHERE session_id=$0", options.ContentSessionId)!, ReportWriter.ToolVersion,
            PhaseTwoBuildProvenance.Current.EvidenceRevision);
        PhysicalPath Physical(PhaseFiveEntry e) => new(e.EntryId, e.ObservationRootId, e.RelativePath, e.Kind, e.Sha256, e.NativeIdentity, e.IsChart);
        var conflicts = new List<PhaseSixConflict>();
        for (var a = 0; a < Roots.Length; a++) for (var b = a + 1; b < Roots.Length; b++) {
            var comparison = PhaseTwoContentComparison.Derive(PhaseTwoContentComparison.View(db, options.ContentSessionId, parent, Roots[a].Id),
                PhaseTwoContentComparison.View(db, options.ContentSessionId, parent, Roots[b].Id), token);
            foreach (var conflict in PhysicalConflictAnnotations.Derive(Roots[a].Entries.Select(Physical).ToArray(), Roots[b].Entries.Select(Physical).ToArray(), comparison.Entries, token)) {
                budget.Check();
                if (conflicts.Count == limits.MaximumConflicts) throw new InvalidOperationException("UNION_NEEDS_EVIDENCE: conflict budget exceeded.");
                conflicts.Add(new(conflict.RelativePath, conflict.Kinds, new[] {
                    conflict.EntryA is long entryA ? new PhaseSixSource(conflict.RootA!.Value, entryA) : null,
                    conflict.EntryB is long entryB ? new PhaseSixSource(conflict.RootB!.Value, entryB) : null }.OfType<PhaseSixSource>().OrderBy(s => s.RootId).ThenBy(s => s.EntryId).ToArray()));
            }
        }
        Conflicts = conflicts.OrderBy(c => c.RelativePath, StringComparer.Ordinal).ThenBy(c => string.Join("|", c.Types), StringComparer.Ordinal)
            .ThenBy(c => c.Sources[0].RootId).ThenBy(c => c.Sources[0].EntryId).ToArray();
        budget.Check(); RequireBinding();
    }
    internal void RequireBinding() {
        if (PhaseTwoSafety.Inspect(path) != initial) throw new IOException("Evidence database binding/stamps changed.");
    }
    public void Dispose() => Database.Dispose();
}
