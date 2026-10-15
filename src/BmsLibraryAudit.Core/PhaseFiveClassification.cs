namespace BmsLibraryAudit.Core;

internal static class PhaseFiveClassification
{
    internal static PhaseFivePair Pair(PhaseFiveRoot a, PhaseFiveRoot b, PhaseTwoContentComparison.Evidence comparison,
        PhaseFiveLimits limits, CancellationToken token, IReadOnlyDictionary<long, PhaseFiveReference[]> referencesA,
        IReadOnlyDictionary<long, PhaseFiveReference[]> referencesB)
    {
        var ai = a.Entries.ToDictionary(e => e.EntryId); var bi = b.Entries.ToDictionary(e => e.EntryId);
        var differences = comparison.Entries.Select(e => new PhaseFiveDifference(e.Path, e.Category,
            e.EntryA is long x ? ai[x] : null, e.EntryB is long y ? bi[y] : null)).ToArray();
        var ca = a.Entries.Where(e => e.IsChart).ToArray(); var cb = b.Entries.Where(e => e.IsChart).ToArray();
        var ha = ca.Select(e => e.Sha256!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var hb = cb.Select(e => e.Sha256!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var common = ha.Intersect(hb, StringComparer.Ordinal).ToArray();
        var onlyA = ha.Except(hb, StringComparer.Ordinal).ToArray(); var onlyB = hb.Except(ha, StringComparer.Ordinal).ToArray();
        var chartState = onlyA.Length == 0 && onlyB.Length == 0 ? "CHART_SET_EXACT" : ha.Length == 0 || hb.Length == 0 ? "CHART_SET_DISJOINT"
            : onlyA.Length == 0 ? "CHART_SET_SUBSET_A_IN_B" : onlyB.Length == 0 ? "CHART_SET_SUBSET_B_IN_A"
            : common.Length > 0 ? "CHART_SET_PARTIAL_OVERLAP" : "CHART_SET_DISJOINT";
        var charts = new PhaseFiveChartRelation(chartState, "PROVEN_FROM_ACCEPTED_CONTENT", ca.Length, cb.Length, ha, hb, common, onlyA, onlyB);
        var groups = comparison.Groups.Select(g => new PhaseFiveContentGroup(g.Projection, g.Digest, g.CountA, g.CountB, g.DifferentPaths)).ToArray();
        bool Subset(bool left) => differences.All(d => d.Category is "SAME_FILE" or "SAME_DIRECTORY" || d.Category == (left ? "FILE_ONLY_B" : "FILE_ONLY_A") ||
            d.Category == (left ? "DIRECTORY_ONLY_B" : "DIRECTORY_ONLY_A"));
        var conflicts = new List<PhaseFiveConflict>(); var truncated = false;
        PhaseFiveReference[] Refs(PhaseFiveEntry? e) => e is null ? [] :
            (e.ObservationRootId == a.Id ? referencesA : referencesB).GetValueOrDefault(e.EntryId) ?? [];
        void Conflict(string path, string[] kinds, PhaseFiveEntry? x, PhaseFiveEntry? y)
        {
            if (conflicts.Count == limits.MaximumConflicts) { truncated = true; return; }
            conflicts.Add(new(path, kinds, x, y, Refs(x).Concat(Refs(y)).ToArray()));
        }
        foreach (var d in differences)
        {
            token.ThrowIfCancellationRequested();
            if (d.Category == "CONTENT_CONFLICT") Conflict(d.RelativePath,
                d.EntryA!.IsChart != d.EntryB!.IsChart ? ["SAME_RELATIVE_PATH_DIFFERENT_BYTES", "CHART_RESOURCE_PATH_CONFLICT"]
                : !d.EntryA.IsChart ? ["SAME_RELATIVE_PATH_DIFFERENT_BYTES", "RESOURCE_VARIANT"] : ["SAME_RELATIVE_PATH_DIFFERENT_BYTES"], d.EntryA, d.EntryB);
            if (d.Category == "KIND_CONFLICT") Conflict(d.RelativePath, ["FILE_DIRECTORY_KIND_CONFLICT"], d.EntryA, d.EntryB);
        }
        foreach (var group in a.Entries.Concat(b.Entries).GroupBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (group.Select(e => e.RelativePath).Distinct(StringComparer.Ordinal).Skip(1).Any()) {
                var entries = group.OrderBy(e => e.ObservationRootId).ThenBy(e => e.RelativePath, StringComparer.Ordinal).ToArray();
                Conflict(group.Key, ["CASE_COLLISION"], entries[0], entries.First(e => e.RelativePath != entries[0].RelativePath));
            }
        }
        foreach (var group in a.Entries.Concat(b.Entries).Where(e => e.Kind == "REGULAR_FILE" && e.NativeIdentity is not null).GroupBy(e => e.NativeIdentity, StringComparer.Ordinal))
            if (group.Select(e => e.Sha256).Distinct(StringComparer.Ordinal).Skip(1).Any()) {
                var entries = group.ToArray(); Conflict(entries[0].RelativePath, ["POTENTIAL_NATIVE_ALIAS_CONFLICT"], entries[0], entries.First(e => e.Sha256 != entries[0].Sha256));
            }
        var labels = new List<string>();
        if (comparison.Flags[0]) labels.Add("OBSERVATION_TREE_EXACT");
        if (comparison.Flags[1] && !comparison.Flags[0]) labels.Add("CONTENT_MULTISET_EXACT_WITH_PATH_VARIANTS");
        if (chartState == "CHART_SET_EXACT" && differences.Any(d => d.Category is not ("SAME_FILE" or "SAME_DIRECTORY") &&
            (d.EntryA is { Kind: "REGULAR_FILE", IsChart: false } || d.EntryB is { Kind: "REGULAR_FILE", IsChart: false }))) {
            labels.Add("CHART_EXACT_WITH_NONCHART_DIFFERENCES");
            var bindingsA = PotentialResourceBindings(a, token); var bindingsB = PotentialResourceBindings(b, token);
            if (bindingsA.Any(p => bindingsB.TryGetValue(p.Key, out var other) && !p.Value.SetEquals(other)))
                labels.Add("CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT");
        }
        if (!comparison.Flags[0] && Subset(true)) labels.Add("OBSERVED_TREE_SUBSET_A_IN_B");
        if (!comparison.Flags[0] && Subset(false)) labels.Add("OBSERVED_TREE_SUBSET_B_IN_A");
        if (comparison.Counts[2] > 0 && comparison.Counts[3] > 0 && !comparison.Flags[4] && conflicts.Count == 0 && !truncated)
            labels.Add("OBSERVED_COMPLEMENTARY_CONTENT");
        if (comparison.Counts[1] > 0) labels.Add("HARD_PATH_CONTENT_CONFLICT");
        if (comparison.Counts[6] > 0) labels.Add("HARD_PATH_KIND_CONFLICT");
        if (a.NestedRootIds.Contains(b.Id) || b.NestedRootIds.Contains(a.Id)) labels.Add("NESTED_OBSERVATION_ROOTS");
        if (truncated) labels.Add("CONFLICT_ENUMERATION_INCOMPLETE");
        var supporting = new List<string> { "CONTENT_REPRODUCED_TREE", "CONTENT_HASH_MULTIPLICITY", "CONTENT_CHART_MEMBERSHIP" };
        if (labels.Contains("CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT")) supporting.Add("ADMITTED_PHASE4_POTENTIAL_BINDINGS");
        return new(a.Id, b.Id, "PROVEN_FROM_ACCEPTED_CONTENT", charts, comparison.Flags[0], comparison.Flags[2], comparison.Flags[1], comparison.Flags[3],
            Subset(true), Subset(false), labels.ToArray(), supporting.ToArray(),
            differences, groups, conflicts.ToArray(), truncated ? "INCOMPLETE" : "COMPLETE",
            a.SemanticTrust == "PROVEN_WITHIN_NAMED_MODEL" && b.SemanticTrust == "PROVEN_WITHIN_NAMED_MODEL" ? "PROVEN_WITHIN_NAMED_MODEL" : "UNKNOWN",
            "PACKAGE_BOUNDARY_UNKNOWN", ["Historical observation dimensions only; no transitive package equivalence or authority.",
                "Referenced-resource variants are potential admitted model bindings, not simulator-selected resources or playback differences.",
                "Known/admitted explicit references only; UNKNOWN and absent reports retain all obligations."]);
    }
    private static Dictionary<(string Chart, int Declaration, int Usage, string Model), HashSet<(string Path, string Sha256)>>
        PotentialResourceBindings(PhaseFiveRoot root, CancellationToken token)
    {
        var bindings = new Dictionary<(string Chart, int Declaration, int Usage, string Model), HashSet<(string Path, string Sha256)>>();
        var entries = root.Entries.ToDictionary(e => e.EntryId);
        foreach (var chart in root.Dependencies.Where(d => d.Admission == "ADMITTED_RECONSTRUCTED_PHASE4" && d.VerifiedReport is not null))
        {
            var report = chart.VerifiedReport!; var graph = report.Semantics!;
            // Phase 3 can mark retained conditional/duplicate syntax incomplete
            // while the admitted Phase 4 model proves its coverage explicitly.
            if (graph.ConditionalModelCoverage != "COMPLETE_WITHIN_ADMITTED_MODEL" || graph.RelationEnumerationCoverage != "COMPLETE") continue;
            var declarations = report.Declarations.ToDictionary(d => d.DeclarationId);
            var coverage = graph.PhysicalResourceBindingCoverage!.Declarations.ToDictionary(d => d.DeclarationId);
            var usages = graph.UsageEvidence.ToDictionary(u => u.UsageId);
            var candidates = graph.PhysicalCandidates.ToDictionary(c => c.Id, c => c.Evidence);
            foreach (var relation in graph.Relations)
            {
                token.ThrowIfCancellationRequested();
                if (relation.DeclarationId is not int id || relation.Compatibility != "MAY_BIND_WITHIN_MODEL" ||
                    relation.Intersection.State is not ("UNCONDITIONAL_SYNTAX" or "POSSIBLE_WITHIN_MODEL") ||
                    !graph.Models.Contains(relation.Intersection.Model, StringComparer.Ordinal) ||
                    !usages.TryGetValue(relation.UsageId, out var usage) || usage.DeclarationCoverage != "DECLARATIONS_COVER_USAGE_WITHIN_MODEL" ||
                    usage.EffectiveDeclarationState is not ("SINGLE_CANDIDATE_WITHIN_MODEL" or "BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL") ||
                    !declarations.TryGetValue(id, out var declaration) || declaration.ResolutionState != "OBSERVED_PATH_MATCH" ||
                    declaration.Candidates.Length != 1 || !coverage.TryGetValue(id, out var physical) ||
                    physical.State is not ("ORDINAL_EXACT_OBSERVED_MATCH" or "WINDOWS_QUALIFIED_OBSERVED_MATCH")) continue;
                var candidate = declaration.Candidates[0];
                if (!relation.PhysicalCandidateIds.Any(c => candidates[c].EntryId == candidate.EntryId) ||
                    candidate.ObservationRootId != root.Id || candidate.ComponentId != report.Provenance.ComponentId ||
                    !entries.TryGetValue(candidate.EntryId, out var entry) || entry.IsChart || entry.Kind != "REGULAR_FILE" ||
                    entry.Sha256 != candidate.Sha256 || entry.ComponentId != candidate.ComponentId) continue;
                var key = (chart.ChartSha256, id, relation.UsageId, relation.Intersection.Model);
                if (!bindings.TryGetValue(key, out var observed)) bindings.Add(key, observed = []);
                observed.Add((entry.RelativePath, entry.Sha256!));
            }
        }
        return bindings;
    }
    internal static Dictionary<long, PhaseFiveReference[]> ReferenceIndex(PhaseFiveRoot root, CancellationToken token)
    {
        var index = new Dictionary<long, List<PhaseFiveReference>>();
        foreach (var chart in root.Dependencies.Where(d => d.VerifiedReport is not null))
        {
            var report = chart.VerifiedReport!; var declarations = report.VerifiedSyntax.Declarations.ToDictionary(d => d.Id);
            var relations = report.Semantics!.Relations.Where(r => r.DeclarationId is not null).ToLookup(r => r.DeclarationId!.Value);
            var proposals = report.Semantics.BmsonAudioLookups.ToDictionary(l => l.DeclarationId, l => l.Candidates);
            foreach (var declaration in report.Declarations) {
                token.ThrowIfCancellationRequested(); var original = declarations[declaration.DeclarationId];
                var relation = relations[declaration.DeclarationId].FirstOrDefault();
                var condition = relation?.DeclarationCondition ?? declaration.Reachability;
                var reference = new PhaseFiveReference(root.Id, chart.ChartEntryId, chart.ChartSha256, declaration.DeclarationId, original.LogicalId, declaration.WrittenPath,
                    declaration.PathPolicy, condition.Model, condition.State, relations[declaration.DeclarationId].Select(r => r.UsageId).ToArray());
                foreach (var candidate in declaration.Candidates.Concat(proposals.GetValueOrDefault(declaration.DeclarationId) ?? []).DistinctBy(c => c.EntryId)) {
                    if (!index.TryGetValue(candidate.EntryId, out var refs)) index.Add(candidate.EntryId, refs = []);
                    refs.Add(reference);
                }
            }
        }
        return index.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }
}
