using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BmsLibraryAudit.Core;

public static class PhaseFourResolver
{
    internal static readonly JsonSerializerOptions Json = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, MaxDepth = 96, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public const string Notice = "Historical sealed observation, not current filesystem truth or a package boundary. Phase 3 JSON is unsigned; syntax is reconstructed from CONTENT-bound included bytes. Authored resource paths are never opened. Matches do not certify simulator semantics, dependency closure, merge safety or deletion authority.";
    public static PhaseFourResult Resolve(PhaseFourOptions options, PhaseFourLimits? limits = null, CancellationToken token = default)
        => ResolveCore(options, limits, token, null);
    internal static PhaseFourResult ResolveCore(PhaseFourOptions options, PhaseFourLimits? limits, CancellationToken token, PhaseThreePublicationTestHooks? hooks)
    {
        limits ??= new(); ValidateLimits(limits); token.ThrowIfCancellationRequested(); PhaseTwoSafety.RequirePlatform();
        if (options.ContentSessionId <= 0 || !PhaseOneSourceCapture.Digest(options.AcceptedContentManifestSha256, 64) || string.IsNullOrWhiteSpace(options.ObservationRootKey))
            throw new ArgumentException("A positive CONTENT session, exact lowercase SHA-256 and explicit observation root key are required.");
        var database = PathPolicy.Absolute(options.DatabasePath); var input = PathPolicy.Absolute(options.ChartReportPath);
        var output = PathPolicy.Absolute(options.OutputDirectory);
        if (PathPolicy.Contains(output, database) || PathPolicy.Contains(output, input)) throw new InvalidOperationException("Output overlaps protected input evidence.");
        var dbFacts = PhaseTwoSafety.Inspect(database);
        using var db = PhaseTwoDatabase.Open(database, false);
        db.Begin(readOnly: true); db.CheckIntegrity();
        if ((string?)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0 AND stage='CONTENT' AND state='COMPLETED'", options.ContentSessionId) != options.AcceptedContentManifestSha256)
            throw new InvalidOperationException("Exact accepted COMPLETED CONTENT digest required.");
        PhaseTwoManifest.VerifyContent(db, options.ContentSessionId, token);
        var parent = PhaseTwoContentRunner.Parent(db, options.ContentSessionId);
        var selected = db.Rows("""
            SELECT r.observation_root_id,r.original_path,c.component_id,c.component_key,o.outcome,o.comparison_eligible,o.coherent,o.sealed
            FROM observation_roots r JOIN component_memberships m ON m.preflight_id=r.preflight_id AND m.observation_root_id=r.observation_root_id
            JOIN candidate_components c ON c.preflight_id=m.preflight_id AND c.component_id=m.component_id
            JOIN content_component_outcomes o ON o.preflight_id=c.preflight_id AND o.component_id=c.component_id
            WHERE r.preflight_id=$0 AND r.root_key=$1 AND c.outcome='ADMITTED' AND o.content_session_id=$2
            """, parent, options.ObservationRootKey, options.ContentSessionId).SingleOrDefault()
            ?? throw new InvalidOperationException("Selected root is not in an admitted CONTENT component.");
        if ((string)selected[4]! != "COMPLETE" || selected.Skip(5).Any(v => Convert.ToInt64(v) != 1))
            throw new InvalidOperationException("Selected component must be COMPLETE, comparison-eligible, coherent and sealed; no partial salvage.");
        var rootId = Convert.ToInt64(selected[0]); var rootPath = (string)selected[1]!; var componentId = Convert.ToInt64(selected[2]);
        // Protect all captured roots, not just the chosen observation root. Validate
        // their source seals before using the captured provenance for exclusion.
        foreach (var snapshot in db.Rows("SELECT snapshot_id,captured_evidence_digest,seal_flag FROM source_snapshots"))
            if (Convert.ToInt64(snapshot[2]) != 1 || (string?)snapshot[1] != PhaseTwoManifest.Source(db, Convert.ToInt64(snapshot[0])))
                throw new InvalidOperationException("Untrusted protected-root provenance.");
        var protectedRoots = db.Rows("SELECT DISTINCT original_path FROM source_library_roots ORDER BY original_path COLLATE ORDINAL").Select(r => (string)r[0]!).ToArray();
        var protectedFiles = db.Rows("SELECT DISTINCT source_db_original_path FROM source_snapshots").Select(r => (string)r[0]!).Append(database).Append(input).ToArray();
        var destination = new Destination(output, protectedRoots, protectedFiles);
        destination.Validate(null, true);
        var inputFacts = PhaseTwoSafety.Inspect(input);
        if ((inputFacts.Attributes & (uint)FileAttributes.Directory) != 0 || inputFacts.Size > limits.MaximumReportBytes)
            throw new InvalidOperationException("INCOMPLETE_REPORT_LIMIT: input must be a bounded regular Phase 3 report.");
        using var report = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
        if (PhaseTwoSafety.Inspect(report.SafeFileHandle, input) != inputFacts) throw new IOException("Phase 3 report binding changed.");
        var payload = new byte[checked((int)inputFacts.Size)]; report.ReadExactly(payload); token.ThrowIfCancellationRequested();
        if (report.ReadByte() != -1 || PhaseTwoSafety.Inspect(report.SafeFileHandle, input) != inputFacts) throw new IOException("Phase 3 report changed during read.");
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 96 });
        RejectDuplicateProperties(document.RootElement);
        CheckReportShape(document.RootElement);
        var inspection = JsonSerializer.Deserialize<PhaseThreeInspection>(payload, Json) ?? throw new InvalidOperationException("Missing Phase 3 envelope.");
        var syntax = inspection.Analysis ?? throw new InvalidOperationException("Missing Phase 3 analysis.");
        if (syntax.Limits is null || syntax.OriginalBytes is null || syntax.OriginalBytes.Length > 8 * 1024 * 1024)
            throw new InvalidOperationException("INCOMPLETE_CHART_BYTES: bounded original bytes and parser limits are required; no source reread.");
        PhaseThreeParser.ValidateLimits(syntax.Limits);
        if (string.IsNullOrWhiteSpace(inspection.ChartPath) || string.IsNullOrWhiteSpace(inspection.SourceRoot) || inspection.SourceFacts is null || inspection.SourceRootFacts is null)
            throw new InvalidOperationException("Incomplete Phase 3 path/identity provenance.");
        var chartPath = PathPolicy.Absolute(inspection.ChartPath); var sourceRoot = PathPolicy.Absolute(inspection.SourceRoot);
        if (!PhaseTwoFileSystem.IsChart(chartPath) || !PathPolicy.Contains(sourceRoot, chartPath)) throw new InvalidOperationException("Unsupported chart or declared root membership.");
        var chartRow = db.Rows("""
            SELECT e.entry_id,p.physical_file_id,e.entry_key,e.original_path,m.relative_path,e.native_identity,h.sha256,h.verified_size,
                e.mtime_ticks,e.attributes,e.extension,e.is_chart
            FROM observation_memberships m JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id
            JOIN physical_files p ON p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id
            JOIN content_hashes h ON h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
            WHERE m.preflight_id=$0 AND m.observation_root_id=$1 AND e.entry_key=$2 AND e.kind='REGULAR_FILE' AND h.content_session_id=$3 AND h.status='SUCCESS'
            """, parent, rootId, PathPolicy.Normalize(chartPath), options.ContentSessionId).SingleOrDefault()
            ?? throw new InvalidOperationException("Chart is not a SUCCESS physical member of the explicitly selected root/component.");
        var capturedRoot = (string?)db.Scalar("""
            SELECT r.original_path FROM source_charts c JOIN source_library_roots r ON r.snapshot_id=c.snapshot_id AND r.source_root_id=c.source_root_id
            WHERE c.snapshot_id=$0 AND c.normalized_path=$1 AND c.present=1 AND c.status='OK'
            """, parent, chartRow[2]);
        if (capturedRoot is null || !PathPolicy.Comparer.Equals(PathPolicy.Absolute(capturedRoot), sourceRoot))
            throw new InvalidOperationException("Declared library root differs from sealed chart source membership.");
        var facts = inspection.SourceFacts;
        var nativeRelative = Path.GetRelativePath(sourceRoot, chartPath).Replace('/', '\\').ToUpperInvariant();
        if (facts.Identity != (string)chartRow[5]! || facts.Size != Convert.ToInt64(chartRow[7]) || facts.MtimeTicks != Convert.ToInt64(chartRow[8]) ||
            facts.Attributes != Convert.ToUInt32(chartRow[9]) || facts.LinkCount < 1 ||
            facts.ResolvedPath != inspection.SourceRootFacts.ResolvedPath + "\\" + nativeRelative ||
            !string.Equals((string?)chartRow[10], Path.GetExtension(chartPath), StringComparison.OrdinalIgnoreCase) || Convert.ToInt64(chartRow[11]) != 1)
            throw new InvalidOperationException("Phase 3 physical chart identity, stamps, extension or relative native binding disagrees with CONTENT.");
        // Reconstruct, then compare every syntactic field (not just a declared SHA).
        var verified = PhaseThreeParser.Parse(syntax.OriginalBytes, Path.GetExtension(chartPath), syntax.Limits, token);
        if (verified.Sha256 != (string)chartRow[6]! || verified.ByteLength != Convert.ToInt64(chartRow[7]) ||
            !JsonElement.DeepEquals(document.RootElement.GetProperty("analysis"), JsonSerializer.SerializeToElement(verified, Json)))
            throw new InvalidOperationException("Phase 3 bytes or declarations disagree with reconstructed CONTENT-bound syntax.");
        var diagnostics = new List<string>();
        var fileCount = db.Number("""
            SELECT count(*) FROM observation_memberships m JOIN physical_files p ON p.preflight_id=m.preflight_id AND p.entry_id=m.entry_id
            WHERE m.preflight_id=$0 AND m.observation_root_id=$1
            """, parent, rootId);
        var inventory = fileCount > limits.MaximumInventoryFiles ? [] : db.Rows("""
            SELECT e.entry_id,p.physical_file_id,e.entry_key,e.original_path,m.relative_path,e.native_identity,h.sha256,h.verified_size
            FROM observation_memberships m JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id
            JOIN physical_files p ON p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id
            JOIN content_hashes h ON h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
            WHERE m.preflight_id=$0 AND m.observation_root_id=$1 AND h.content_session_id=$2 AND h.status='SUCCESS'
            ORDER BY m.relative_path_key COLLATE ORDINAL,e.entry_key COLLATE ORDINAL
            """, parent, rootId, options.ContentSessionId).Select(r => Candidate(r, rootId, componentId)).ToArray();
        var inventoryLimited = fileCount > limits.MaximumInventoryFiles;
        if (inventoryLimited) diagnostics.Add("INVENTORY_LIMIT: no path bindings inferred from a partial inventory.");
        var paths = new PhaseFourPaths(inventory, limits); var conditions = new PhaseFourConditions(verified, limits, token);
        var relativeChart = PhaseFourPaths.Slashes((string)chartRow[4]!);
        var relativeBase = relativeChart.Contains('/') ? relativeChart[..relativeChart.LastIndexOf('/')] : "";
        // Indexed logical groups; per-declaration linkage is independently capped.
        var usageGroups = verified.Usages.Where(u => u.LogicalResourceId is not null).GroupBy(u => u.LogicalResourceId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(u => u.Id).ToArray());
        var groups = verified.LogicalResources.SelectMany(g => g.DeclarationIds.Select(id => (id, g.Id))).ToDictionary(t => t.id, t => t.Id);
        var resolutions = new List<ResolvedResourceDeclaration>(); int references = 0, usageLinks = 0;
        foreach (var declaration in verified.Declarations)
        {
            token.ThrowIfCancellationRequested();
            var limited = ++references > limits.MaximumReferences || inventoryLimited;
            var match = limited ? new PhaseFourPaths.Match(null, "NONE", "UNKNOWN", [], ["REFERENCE_OR_INVENTORY_LIMIT: original evidence retained, binding not attempted."]) : paths.Resolve(relativeBase, declaration.WrittenPath);
            var reach = conditions.For(declaration.Source.DecodingId, declaration.ConditionalContextIds);
            var usageIds = groups.TryGetValue(declaration.Id, out var group) && usageGroups.TryGetValue(group, out var allUsages) ? allUsages : [];
            var retainedUses = usageIds.Take(Math.Max(0, Math.Min(limits.MaximumCandidatesPerReference, limits.MaximumCandidateLinks - usageLinks))).ToArray(); usageLinks += retainedUses.Length;
            var limitations = match.Limitations;
            if (retainedUses.Length != usageIds.Length) { diagnostics.Add("USAGE_LINK_LIMIT: verified syntax retains all usages; relation links incomplete."); limitations = [.. limitations, "Potential usage relation links truncated."]; }
            if (verified.Decodings.Length > 1) limitations = [.. limitations, "AMBIGUOUS_DECODING: this candidate is specific to one decoding; author intent is unproved."];
            resolutions.Add(new(declaration.Id, declaration.WrittenPath, relativeBase, match.RelativePath, match.Policy, match.State,
                usageIds.Length == 0 ? "DECLARED_POTENTIAL_NO_OBSERVED_USE" : "POTENTIAL_USE_RELATION", reach, retainedUses, match.Candidates, limitations));
        }
        var usages = verified.Usages.Select(u => new ResolvedResourceUsage(u.Id, conditions.For(u.Source.DecodingId, u.ConditionalContextIds), "POTENTIAL_SYNTACTIC_USE")).ToArray();
        if (references + usages.Length > limits.MaximumReferences) diagnostics.Add("REFERENCE_LIMIT: semantic relation coverage incomplete; all verified syntax retained.");
        if (paths.Limited) diagnostics.Add("CANDIDATE_LIMIT: physical relation coverage incomplete.");
        if (conditions.Limited) diagnostics.Add("SYMBOLIC_LIMIT: reachability unknown; alternatives retained.");
        if (!conditions.Complete) diagnostics.Add("CONDITIONAL_UNKNOWN: unsupported/malformed evidence prevents branch reduction.");
        var provenance = new PhaseFourProvenance(options.ContentSessionId, options.AcceptedContentManifestSha256, parent,
            (string)db.Scalar("SELECT final_evidence_digest FROM sessions WHERE session_id=$0", parent)!, componentId, (string)selected[3]!, rootId,
            options.ObservationRootKey, rootPath, capturedRoot, Path.GetDirectoryName((string)chartRow[3]!)!, null, Candidate(chartRow, rootId, componentId), verified.Sha256!,
            (string)db.Scalar("SELECT started_utc FROM sessions WHERE session_id=$0", options.ContentSessionId)!,
            (string)db.Scalar("SELECT completed_utc FROM sessions WHERE session_id=$0", options.ContentSessionId)!, ReportWriter.ToolVersion, PhaseTwoBuildProvenance.Current.EvidenceRevision);
        var resolved = resolutions.ToArray();
        var semantics = PhaseFourSemantics.Analyze(verified, resolved, paths, limits, token);
        var result = new PhaseFourResult("phase4-dependency-2", provenance, verified, resolved, usages,
            diagnostics.Count != 0 || resolutions.Any(r => r.ResolutionState is "UNKNOWN" or "UNSUPPORTED_RESOLUTION" or "AMBIGUOUS_PATH_MATCH") ? "INCOMPLETE" : "COMPLETE_OBSERVED_PATH_ANALYSIS",
            conditions.Complete && resolutions.All(r => r.Reachability.State != "UNKNOWN") && usages.All(u => u.Reachability.State != "UNKNOWN") ? "COMPLETE_BOUNDED_MODEL" : "INCOMPLETE",
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ["No package-root inference, external lookup, extension substitution, resource opening or hash-based name substitution.",
             "Phase 3 library-root native facts are unsigned hints; sealed library path membership and chart native identity/stamps/bytes are checked independently.",
             "Duplicate logical declarations and all decoding/conditional alternatives remain potential obligations; no first/last-wins or required/unused classification.",
             "Physical and semantic claims refer to a historical observation window, not a current immutable snapshot."], false, false, limits, Notice, semantics);
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        if (json.Length > limits.MaximumReportBytes) throw new InvalidOperationException("INCOMPLETE_REPORT_LIMIT: bounded result cannot be published; no complete report created.");
        void Validate(string? temporary, bool empty)
        {
            if (empty) token.ThrowIfCancellationRequested();
            if (PhaseTwoSafety.Inspect(database) != dbFacts || PhaseTwoSafety.Inspect(input) != inputFacts) throw new IOException("Input evidence binding/stamps changed.");
            destination.Validate(temporary, empty);
        }
        ObservedReportPublication.Publish(output, "dependency_resolution.json", json, Validate, token, hooks);
        return result;
    }
    private static ObservedResourceCandidate Candidate(object?[] row, long root, long component) => new(Convert.ToInt64(row[0]), Convert.ToInt64(row[1]), root, component,
        (string)row[2]!, (string)row[3]!, (string)row[4]!, (string)row[5]!, (string)row[6]!, Convert.ToInt64(row[7]), "SEALED_MEMBERSHIP");
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) throw new InvalidOperationException("Duplicate Phase 3 JSON property: " + property.Name); RejectDuplicateProperties(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
    private static void CheckReportShape(JsonElement envelope)
    {
        if (envelope.ValueKind != JsonValueKind.Object || !envelope.TryGetProperty("analysis", out var analysis) || analysis.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Missing Phase 3 analysis object.");
        foreach (var name in new[] { "declarations", "usages", "conditional_contexts", "logical_resources", "unknown_evidence", "diagnostics", "decodings" })
            if (!analysis.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > (name == "decodings" ? 2 : 50000))
                throw new InvalidOperationException("INCOMPLETE_FACT_LIMIT: missing/unbounded Phase 3 array: " + name);
        if (!analysis.TryGetProperty("original_bytes", out var bytes) || bytes.ValueKind != JsonValueKind.String || bytes.GetString()!.Length > 4L * ((8 * 1024 * 1024 + 2) / 3))
            throw new InvalidOperationException("INCOMPLETE_CHART_BYTES: bounded original bytes are required; no source reread.");
    }
    private static void ValidateLimits(PhaseFourLimits limits)
    {
        var upper = new PhaseFourLimits();
        if (limits.MaximumReportBytes is < 1 || limits.MaximumReportBytes > upper.MaximumReportBytes ||
            limits.MaximumInventoryFiles is < 1 || limits.MaximumInventoryFiles > upper.MaximumInventoryFiles ||
            limits.MaximumReferences is < 1 || limits.MaximumReferences > upper.MaximumReferences ||
            limits.MaximumCandidatesPerReference is < 1 || limits.MaximumCandidatesPerReference > upper.MaximumCandidatesPerReference ||
            limits.MaximumCandidateLinks is < 1 || limits.MaximumCandidateLinks > upper.MaximumCandidateLinks ||
            limits.MaximumConditionalDepth is < 1 || limits.MaximumConditionalDepth > upper.MaximumConditionalDepth ||
            limits.MaximumSymbolicTerms is < 1 || limits.MaximumSymbolicTerms > upper.MaximumSymbolicTerms ||
            limits.MaximumSemanticRelations is < 1 || limits.MaximumSemanticRelations > upper.MaximumSemanticRelations ||
            limits.MaximumSemanticCandidateLinks is < 1 || limits.MaximumSemanticCandidateLinks > upper.MaximumSemanticCandidateLinks ||
            limits.MaximumSemanticSymbolicTerms is < 1 || limits.MaximumSemanticSymbolicTerms > upper.MaximumSemanticSymbolicTerms) throw new ArgumentException("Phase 4 limits must be positive and may only reduce documented hard limits.");
    }
    private sealed class Destination
    {
        private sealed record Binding(string Path, string Resolved, NativePathFacts? Facts);
        private readonly string output;
        private readonly Binding[] roots, files;
        internal Destination(string output, string[] roots, string[] files)
        {
            this.output = output;
            this.roots = roots.Select(Bind).ToArray(); this.files = files.Select(Bind).ToArray();
        }
        private static Binding Bind(string path)
        {
            path = PathPolicy.Absolute(path); PathPolicy.RejectReparseAncestors(path);
            return new(path, PhaseTwoSafety.Resolve(path), PhaseTwoSafety.Exists(path) ? PhaseTwoSafety.Inspect(path) : null);
        }
        private static bool Within(string parent, string path) => path == parent || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.Ordinal);
        internal void Validate(string? temporary, bool empty)
        {
            PathPolicy.RejectReparseAncestors(output); var resolved = PhaseTwoSafety.Resolve(output);
            foreach (var binding in roots.Concat(files))
            {
                var current = Bind(binding.Path);
                if (current.Resolved != binding.Resolved || current.Facts?.Identity != binding.Facts?.Identity || current.Facts?.Attributes != binding.Facts?.Attributes)
                    throw new IOException("Protected namespace binding changed: " + binding.Path);
            }
            foreach (var root in roots)
                if (PathPolicy.Contains(root.Path, output) || PathPolicy.Contains(output, root.Path) || Within(root.Resolved, resolved) || Within(resolved, root.Resolved))
                    throw new InvalidOperationException("Output overlaps a protected source library root or native alias.");
            foreach (var file in files)
                if (PathPolicy.Contains(output, file.Path) || Within(resolved, file.Resolved)) throw new InvalidOperationException("Output overlaps protected evidence/database input.");
            if (PhaseTwoSafety.Exists(output) && (!Directory.Exists(output) || empty && Directory.EnumerateFileSystemEntries(output).Any(p => temporary is null || !PathPolicy.Comparer.Equals(PathPolicy.Absolute(p), temporary))))
                throw new InvalidOperationException("Phase 4 output must be new/empty or contain only the owned temporary.");
        }
    }
}
