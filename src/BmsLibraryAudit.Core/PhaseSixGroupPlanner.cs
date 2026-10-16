using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class PhaseSixGroupPlanner
{
    public static PhaseSixGroupEnvelope Plan(PhaseSixOptions options, string outputDirectory,
        PhaseSixGroupLimits? limits = null, CancellationToken token = default) => PlanCore(options, outputDirectory, limits, token, null);
    internal static PhaseSixGroupEnvelope PlanCore(PhaseSixOptions options, string outputDirectory,
        PhaseSixGroupLimits? limits, CancellationToken token, PhaseThreePublicationTestHooks? hooks)
    {
        limits ??= new();
        using var admitted = PhaseSixGroupNamespace.Admit(options, limits, token);
        var budget = admitted.Budget; var roots = admitted.Roots;
        var output = PathPolicy.Absolute(outputDirectory);
        var destination = new PhaseFourResolver.Destination(output, admitted.Libraries, admitted.ProtectedFiles);
        destination.Validate(null, true);
        var end = (1 << roots.Length) - 1;
        if (end > limits.MaximumSubsets) throw new InvalidOperationException("Group subset budget cannot prove exhaustivity.");
        var subsets = new List<PhaseSixGroupSubset>(); long work = 0;
        for (var mask = 1; mask <= end; mask++) {
            budget.Check(); var selected = PhaseSixGroupNamespace.Select(roots, mask);
            work += selected.Sum(r => (long)r.Entries.Length);
            if (work > limits.MaximumExaminedEntries) throw new InvalidOperationException("Group examination budget exceeded; no partial plan.");
            subsets.Add(new(mask, selected.Select(r => r.Id).ToArray(),
                PhaseSixGroupNamespace.Compatible(selected, admitted.Conflicts, budget) ? "UNION_PHYSICALLY_COMPATIBLE" : "UNION_BLOCKED_CONFLICT"));
        }
        var compatible = subsets.Where(s => s.PhysicalState == "UNION_PHYSICALLY_COMPATIBLE").ToArray();
        var maximal = compatible.Where(s => !compatible.Any(t => t.Mask != s.Mask && (s.Mask & t.Mask) == s.Mask)).ToArray();
        var candidates = new List<PhaseSixGroupCandidate>(); long virtualCount = 0, accountedCount = 0;
        foreach (var subset in maximal) {
            budget.Check(); var ids = subset.RootIds.ToHashSet();
            var members = roots.Where(r => ids.Contains(r.Id)).ToArray();
            var targets = members.SelectMany(r => r.Entries).GroupBy(e => e.RelativePath, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new PhaseSixVirtualEntry(g.Key, g.First().Kind, g.First().Sha256,
                    g.Select(e => new PhaseSixSource(e.ObservationRootId, e.EntryId)).OrderBy(s => s.RootId).ThenBy(s => s.EntryId).ToArray())).ToArray();
            var variants = roots.Where(r => !ids.Contains(r.Id)).Select((r, i) => new PhaseSixGroupVariant(
                "_variantes/" + (i + 1).ToString("000", System.Globalization.CultureInfo.InvariantCulture), r)).ToArray();
            var labels = variants.ToDictionary(v => v.OriginalRoot.Id, v => v.ConditionalLabel);
            var conservation = roots.SelectMany(r => r.Entries).OrderBy(e => e.ObservationRootId).ThenBy(e => e.EntryId)
                .Select(e => new PhaseSixGroupDisposition(new(e.ObservationRootId, e.EntryId),
                    ids.Contains(e.ObservationRootId) ? "REPRESENTED_VIRTUALLY" : "PRESERVE_COMPLETE_VARIANT_IN_PLACE",
                    e.RelativePath, labels.GetValueOrDefault(e.ObservationRootId))).ToArray();
            virtualCount += targets.Length; accountedCount += conservation.Length;
            if (virtualCount > limits.MaximumVirtualEntries || accountedCount > limits.MaximumPlannedAppearances)
                throw new InvalidOperationException("Group alternative accounting budget exceeded; no truncation.");
            candidates.Add(new(PhaseSixGroupFormat.Id(subset.Mask), subset.RootIds, targets, variants, conservation,
                PhaseSixGroupNamespace.LayoutConflicts(targets.Select(t => t.RelativePath), variants.Length != 0)));
        }
        var entries = roots.SelectMany(r => r.Entries).ToArray();
        var obligations = entries.Where(e => e.IsChart).OrderBy(e => e.ObservationRootId).ThenBy(e => e.EntryId)
            .Select(e => new PhaseSixChartObligation(new(e.ObservationRootId, e.EntryId), e.Sha256!, Path.GetDirectoryName(e.OriginalPath)!)).ToArray();
        var plan = new PhaseSixGroupPlan(PhaseSixGroupFormat.Version, PhaseSixGroupFormat.Profile, admitted.Provenance, roots, admitted.Conflicts,
            subsets.ToArray(), candidates.ToArray(), obligations,
            new(candidates.Count > 1 ? "HUMAN_SELECTION_REQUIRED" : "UNIQUE_PHYSICAL_PROPOSAL_NOT_SELECTED"),
            new(roots.Length, subsets.Count, candidates.Count, entries.Length, obligations.Length, checked((int)virtualCount), checked((int)accountedCount), admitted.Conflicts.Length),
            limits, PhaseSixGroupFormat.Limitations);
        var envelope = new PhaseSixGroupEnvelope(plan, PhaseSixGroupFormat.Digest(plan));
        PhaseSixGroupVerifier.Validate(admitted, envelope, token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, PhaseSixFormat.Pretty).Concat(new byte[] { 10 }).ToArray();
        if (bytes.Length > limits.MaximumReportBytes) throw new InvalidOperationException("Group report budget exceeded; no partial plan.");
        void Validate(string? temporary, bool empty) { if (empty) budget.Check(); admitted.RequireBinding(); destination.Validate(temporary, empty); }
        ObservedReportPublication.Publish(output, PhaseSixGroupFormat.Filename, bytes, Validate, token, hooks);
        return envelope;
    }
}
