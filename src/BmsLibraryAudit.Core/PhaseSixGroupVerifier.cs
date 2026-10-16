using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class PhaseSixGroupVerifier
{
    public static PhaseSixGroupEnvelope Verify(PhaseSixOptions options, string planPath, CancellationToken token = default)
    {
        // Admission and protected-path exclusion precede all alleged plan payload reads.
        using var admitted = PhaseSixGroupNamespace.Admit(options, new(), token);
        var path = PathPolicy.Absolute(planPath); PathPolicy.RejectReparseAncestors(path); var facts = PhaseTwoSafety.Inspect(path);
        if ((facts.Attributes & (uint)FileAttributes.Directory) != 0 || facts.LinkCount != 1 || facts.Size > new PhaseSixGroupLimits().MaximumReportBytes)
            throw new InvalidOperationException("Bounded single-link group plan required.");
        if (admitted.Libraries.Any(r => PathPolicy.Contains(r, path)) || admitted.Roots.SelectMany(r => r.Entries).Any(e => e.NativeIdentity == facts.Identity) ||
            admitted.ProtectedFiles.Any(f => PhaseTwoSafety.Exists(f) && PhaseTwoSafety.Inspect(f).Identity == facts.Identity))
            throw new InvalidOperationException("Group plan overlaps protected source or evidence input.");
        new PhaseFourResolver.Destination(Path.GetDirectoryName(path)!, admitted.Libraries, admitted.ProtectedFiles).Validate(null, false);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts) throw new IOException("Group plan changed before reading.");
        var bytes = new byte[checked((int)facts.Size)]; stream.ReadExactly(bytes); token.ThrowIfCancellationRequested();
        if (stream.ReadByte() != -1 || PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts || PhaseTwoSafety.Inspect(path) != facts)
            throw new IOException("Group plan changed during reading.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        PhaseFourResolver.RejectDuplicateProperties(document.RootElement);
        PhaseSixGroupEnvelope envelope;
        try {
            envelope = JsonSerializer.Deserialize<PhaseSixGroupEnvelope>(bytes, PhaseSixFormat.Canonical) ?? throw new InvalidOperationException("Missing group plan.");
            PhaseSixGroupFormat.ValidateLimits(envelope.Plan.Limits);
            if (bytes.Length > envelope.Plan.Limits.MaximumReportBytes) throw new InvalidOperationException("Group plan exceeds declared byte budget.");
            admitted.Budget.Reduce(PhaseSixGroupFormat.AdmissionLimits(envelope.Plan.Limits));
            Validate(admitted, envelope, token);
        } catch (Exception e) when (e is JsonException or NullReferenceException or ArgumentException or KeyNotFoundException or OverflowException) {
            throw new InvalidOperationException("Malformed or inconsistent group plan.", e);
        }
        admitted.RequireBinding();
        if (PhaseTwoSafety.Inspect(path) != facts) throw new IOException("Group plan binding changed.");
        return envelope;
    }

    // Independent accounting and maximality reconstruction, never calls Plan.
    // Shared physical namespace/evidence kernels do not provide these expectations.
    internal static void Validate(PhaseSixEvidenceAdmission admitted, PhaseSixGroupEnvelope envelope, CancellationToken token)
    {
        void Require(bool ok, string why) { if (!ok) throw new InvalidOperationException("INVALID_GROUP_PLAN: " + why); }
        var plan = envelope.Plan; var l = plan.Limits; var budget = admitted.Budget; PhaseSixGroupFormat.ValidateLimits(l); budget.Check();
        Require(plan.PlanVersion == PhaseSixGroupFormat.Version && plan.EvidenceProfile == PhaseSixGroupFormat.Profile && envelope.PlanSha256 == PhaseSixGroupFormat.Digest(plan), "version/profile/digest");
        Require(PhaseSixFormat.Equal(plan.Provenance, admitted.Provenance) && PhaseSixFormat.Equal(plan.Roots, admitted.Roots), "provenance and full inventories");
        Require(PhaseSixFormat.Equal(plan.Conflicts, admitted.Conflicts) && plan.Conflicts.Length <= l.MaximumConflicts, "conflict witnesses");
        Require(plan.Limitations.SequenceEqual(PhaseSixGroupFormat.Limitations, StringComparer.Ordinal), "limitations");
        var roots = admitted.Roots; var end = (1 << roots.Length) - 1;
        var input = roots.SelectMany(r => r.Entries).ToDictionary(e => new PhaseSixSource(e.ObservationRootId, e.EntryId));
        Require(roots.Length <= l.MaximumRoots && end <= l.MaximumSubsets && input.Count <= l.MaximumEntries, "selection/entry/subset caps");
        Require(plan.Subsets.Length == end, "every nonempty subset examined");
        var good = new HashSet<int>(); long work = 0;
        for (var mask = 1; mask <= end; mask++) {
            budget.Check(); var selected = PhaseSixGroupNamespace.Select(roots, mask);
            work += selected.Sum(r => (long)r.Entries.Length); Require(work <= l.MaximumExaminedEntries, "examination budget");
            var ok = PhaseSixGroupNamespace.Compatible(selected, admitted.Conflicts, budget);
            Require(PhaseSixFormat.Equal(plan.Subsets[mask - 1], new PhaseSixGroupSubset(mask, selected.Select(r => r.Id).ToArray(),
                ok ? "UNION_PHYSICALLY_COMPATIBLE" : "UNION_BLOCKED_CONFLICT")), "subset membership/global namespace");
            if (ok) good.Add(mask);
        }
        // A hereditary compatible set is maximal iff no missing root can be added.
        var expected = good.Where(mask => !Enumerable.Range(0, roots.Length).Any(i => (mask & (1 << i)) == 0 && good.Contains(mask | (1 << i)))).Order().ToArray();
        Require(plan.Candidates.Select(c => c.CandidateId).SequenceEqual(expected.Select(PhaseSixGroupFormat.Id)), "all and only maximal alternatives, in canonical order");
        var states = new PhaseSixGroupStates(expected.Length > 1 ? "HUMAN_SELECTION_REQUIRED" : "UNIQUE_PHYSICAL_PROPOSAL_NOT_SELECTED");
        Require(PhaseSixFormat.Equal(plan.States, states), "independent states and no manufactured human selection/authority");
        long virtualCount = 0, accounted = 0;
        for (var i = 0; i < expected.Length; i++) {
            budget.Check(); var candidate = plan.Candidates[i]; var mask = expected[i];
            var members = PhaseSixGroupNamespace.Select(roots, mask); var ids = members.Select(r => r.Id).ToHashSet();
            Require(candidate.RootIds.SequenceEqual(members.Select(r => r.Id)) && candidate.Role == "MAIN_GROUP_CANDIDATE" && candidate.PhysicalState == "UNION_PHYSICALLY_COMPATIBLE", "candidate membership/role");
            var outside = roots.Where(r => !ids.Contains(r.Id)).ToArray();
            Require(candidate.Variants.Length == outside.Length, "whole excluded distributions");
            var labels = new Dictionary<long, string>();
            for (var v = 0; v < outside.Length; v++) {
                var label = "_variantes/" + (v + 1).ToString("000", System.Globalization.CultureInfo.InvariantCulture);
                Require(PhaseSixFormat.Equal(candidate.Variants[v], new PhaseSixGroupVariant(label, outside[v])), "complete variant inventory, provenance, numbering and in-place state");
                labels.Add(outside[v].Id, label);
            }
            var remaining = input.Where(p => ids.Contains(p.Key.RootId)).ToDictionary(p => p.Key, p => p.Value);
            var byPath = new Dictionary<string, List<KeyValuePair<PhaseSixSource, PhaseFiveEntry>>>(StringComparer.Ordinal);
            foreach (var item in remaining.OrderBy(p => p.Key.RootId).ThenBy(p => p.Key.EntryId)) {
                budget.Check();
                if (!byPath.TryGetValue(item.Value.RelativePath, out var bucket)) byPath.Add(item.Value.RelativePath, bucket = []);
                bucket.Add(item);
            }
            var expectedPaths = byPath.Keys.Order(StringComparer.Ordinal).ToArray();
            Require(candidate.VirtualEntries.Select(t => t.RelativePath).SequenceEqual(expectedPaths, StringComparer.Ordinal), "exact virtual namespace");
            foreach (var target in candidate.VirtualEntries) {
                budget.Check(); var origins = byPath[target.RelativePath].ToArray();
                Require(target.Sources.SequenceEqual(origins.Select(p => p.Key)) && origins.Length > 0, "every and only contributing appearance");
                foreach (var (source, entry) in origins) {
                    Require(target.Kind == entry.Kind && target.Sha256 == entry.Sha256, "target type/digest"); remaining.Remove(source);
                }
            }
            Require(remaining.Count == 0, "all main appearances accounted");
            Require(candidate.Conservation.Length == input.Count, "one ledger row per original appearance");
            var ordered = input.OrderBy(p => p.Key.RootId).ThenBy(p => p.Key.EntryId).ToArray();
            for (var row = 0; row < ordered.Length; row++) {
                budget.Check(); var (source, entry) = ordered[row]; var main = ids.Contains(source.RootId);
                Require(candidate.Conservation[row] == new PhaseSixGroupDisposition(source,
                    main ? "REPRESENTED_VIRTUALLY" : "PRESERVE_COMPLETE_VARIANT_IN_PLACE", entry.RelativePath, main ? null : labels[source.RootId]), "independent complete conservation ledger");
            }
            Require(candidate.VariantLayoutConflicts.SequenceEqual(PhaseSixGroupNamespace.LayoutConflicts(expectedPaths, outside.Length > 0), StringComparer.Ordinal), "reserved layout conflicts");
            virtualCount += candidate.VirtualEntries.Length; accounted += candidate.Conservation.Length;
            Require(virtualCount <= l.MaximumVirtualEntries && accounted <= l.MaximumPlannedAppearances, "aggregate alternatives budgets");
        }
        var obligations = input.Where(p => p.Value.IsChart).OrderBy(p => p.Key.RootId).ThenBy(p => p.Key.EntryId)
            .Select(p => new PhaseSixChartObligation(p.Key, p.Value.Sha256!, Path.GetDirectoryName(p.Value.OriginalPath)!)).ToArray();
        Require(PhaseSixFormat.Equal(plan.ChartObligations, obligations), "all chart contexts remain UNKNOWN");
        Require(plan.Counts == new PhaseSixGroupCounts(roots.Length, end, expected.Length, input.Count, obligations.Length,
            checked((int)virtualCount), checked((int)accounted), admitted.Conflicts.Length), "independent counts");
        budget.Check(); token.ThrowIfCancellationRequested();
    }
}
