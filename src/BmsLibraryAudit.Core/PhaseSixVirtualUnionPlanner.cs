using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class PhaseSixVirtualUnionPlanner
{
    public static PhaseSixEnvelope Plan(PhaseSixOptions options, string outputDirectory,
        PhaseSixLimits? limits = null, CancellationToken token = default) => PlanCore(options, outputDirectory, limits, token, null);
    internal static PhaseSixEnvelope PlanCore(PhaseSixOptions options, string outputDirectory, PhaseSixLimits? limits,
        CancellationToken token, PhaseThreePublicationTestHooks? hooks)
    {
        limits ??= new();
        using var admitted = PhaseSixEvidenceAdmission.Load(options, limits, token);
        var budget = admitted.Budget; budget.Check();
        var output = PathPolicy.Absolute(outputDirectory);
        var destination = new PhaseFourResolver.Destination(output, admitted.Libraries, admitted.ProtectedFiles);
        destination.Validate(null, true);
        var entries = admitted.Roots.SelectMany(r => r.Entries).ToArray();
        var index = entries.ToDictionary(e => new PhaseSixSource(e.ObservationRootId, e.EntryId));
        var blockedPaths = admitted.Conflicts.SelectMany(c => c.Sources).Select(s => index[s].RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Materialize affected paths once; avoid a full conflict scan per entry.
        var blockedEntries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries) {
            budget.Check(); var parts = entry.RelativePath.Split('/'); var prefix = "";
            foreach (var part in parts) {
                prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                if (blockedPaths.Contains(prefix)) { blockedEntries.Add(entry.RelativePath); break; }
            }
        }
        var targets = new List<PhaseSixVirtualEntry>(); var conservation = new List<PhaseSixConservation>();
        foreach (var group in entries.GroupBy(e => e.RelativePath, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)) {
            budget.Check(); var sources = group.OrderBy(e => e.ObservationRootId).ThenBy(e => e.EntryId).ToArray();
            var blocked = blockedEntries.Contains(group.Key);
            if (!blocked) {
                if (targets.Count == limits.MaximumVirtualEntries) throw new InvalidOperationException("UNION_NEEDS_EVIDENCE: virtual entry budget exceeded.");
                if (sources.Select(e => (e.Kind, e.Sha256)).Distinct().Count() != 1) throw new InvalidOperationException("Unannotated physical incompatibility.");
                targets.Add(new(group.Key, sources[0].Kind, sources[0].Sha256, sources.Select(e => new PhaseSixSource(e.ObservationRootId, e.EntryId)).ToArray()));
            }
            foreach (var source in sources) conservation.Add(new(new(source.ObservationRootId, source.EntryId),
                blocked ? "BLOCKED_CONFLICT" : "REPRESENTED_VIRTUALLY", source.RelativePath,
                blocked ? "Physical conflict at this path or an ancestor; preserve source in place." : "Original occurrence retained in virtual target provenance; no copy source or removal selected."));
        }
        var states = new PhaseSixStates(admitted.Conflicts.Length == 0 ? "UNION_PHYSICALLY_COMPATIBLE" : "UNION_BLOCKED_CONFLICT");
        var plan = new PhaseSixPlan(PhaseSixFormat.Version, admitted.Provenance, admitted.Roots, targets.ToArray(), admitted.Conflicts,
            conservation.OrderBy(c => c.Source.RootId).ThenBy(c => c.Source.EntryId).ToArray(),
            entries.Where(e => e.IsChart).OrderBy(e => e.ObservationRootId).ThenBy(e => e.EntryId)
                .Select(e => new PhaseSixChartObligation(new(e.ObservationRootId, e.EntryId), e.Sha256!, Path.GetDirectoryName(e.OriginalPath)!)).ToArray(), states,
            new(entries.Length, entries.Count(e => e.IsChart), targets.Count, targets.Count(t => t.Sources.Length == 2),
                targets.Count(t => t.Sources.Length == 1), conservation.Count(c => c.Disposition == "BLOCKED_CONFLICT"), admitted.Conflicts.Length), limits, PhaseSixFormat.Limitations);
        var envelope = new PhaseSixEnvelope(plan, PhaseSixFormat.Digest(plan)); budget.Check();
        PhaseSixPlanVerifier.Validate(admitted, envelope, budget, token);
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, PhaseSixFormat.Pretty) + "\n"); budget.Check();
        if (payload.Length > limits.MaximumReportBytes) throw new InvalidOperationException("UNION_NEEDS_EVIDENCE: output byte budget exceeded; no partial plan.");
        void Validate(string? temporary, bool empty) {
            if (empty) budget.Check(); admitted.RequireBinding(); destination.Validate(temporary, empty);
        }
        ObservedReportPublication.Publish(output, "virtual_union_plan.json", payload, Validate, token, hooks);
        return envelope;
    }
}
