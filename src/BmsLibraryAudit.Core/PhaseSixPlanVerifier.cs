using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class PhaseSixPlanVerifier
{
    public static PhaseSixEnvelope Verify(PhaseSixOptions options, string planPath, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); PhaseTwoSafety.RequirePlatform();
        // Admit/exclude protected inputs before reading any alleged plan payload.
        using var admitted = PhaseSixEvidenceAdmission.Load(options, new(), token);
        var path = PathPolicy.Absolute(planPath); PathPolicy.RejectReparseAncestors(path);
        var facts = PhaseTwoSafety.Inspect(path);
        if ((facts.Attributes & (uint)FileAttributes.Directory) != 0 || facts.LinkCount != 1 || facts.Size > new PhaseSixLimits().MaximumReportBytes)
            throw new InvalidOperationException("Plan must be a bounded ordinary single-link input.");
        if (admitted.Libraries.Any(r => PathPolicy.Contains(r, path)) || admitted.Roots.SelectMany(r => r.Entries).Any(e => e.NativeIdentity == facts.Identity) ||
            admitted.ProtectedFiles.Any(f => PhaseTwoSafety.Exists(f) && PhaseTwoSafety.Inspect(f).Identity == facts.Identity))
            throw new InvalidOperationException("Plan input overlaps/aliases protected source or evidence.");
        new PhaseFourResolver.Destination(Path.GetDirectoryName(path)!, admitted.Libraries, admitted.ProtectedFiles).Validate(null, false);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts) throw new IOException("Plan binding changed before read.");
        var bytes = new byte[checked((int)facts.Size)]; stream.ReadExactly(bytes); token.ThrowIfCancellationRequested();
        if (stream.ReadByte() != -1 || PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts || PhaseTwoSafety.Inspect(path) != facts)
            throw new IOException("Plan changed during read.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        PhaseFourResolver.RejectDuplicateProperties(document.RootElement);
        PhaseSixEnvelope envelope;
        try {
            envelope = JsonSerializer.Deserialize<PhaseSixEnvelope>(bytes, PhaseSixFormat.Canonical) ?? throw new InvalidOperationException("Missing plan envelope.");
            PhaseSixFormat.ValidateLimits(envelope.Plan.Limits);
        } catch (Exception e) when (e is JsonException or NullReferenceException or ArgumentException) {
            throw new InvalidOperationException("Malformed Phase 6A plan.", e);
        }
        if (bytes.Length > envelope.Plan.Limits.MaximumReportBytes) throw new InvalidOperationException("Plan exceeds its declared byte budget.");
        admitted.Budget.Reduce(envelope.Plan.Limits);
        try { Validate(admitted, envelope, admitted.Budget, token); }
        catch (Exception e) when (e is NullReferenceException or KeyNotFoundException or ArgumentException or OverflowException) {
            throw new InvalidOperationException("Malformed or inconsistent plan accounting.", e);
        }
        admitted.RequireBinding();
        if (PhaseTwoSafety.Inspect(path) != facts) throw new IOException("Plan changed before verification completion.");
        return envelope;
    }

    // Independent consumer: never calls the planner or compares its output with a
    // second invocation of that planner. Evidence admission/physical kernels are shared.
    internal static void Validate(PhaseSixEvidenceAdmission admitted, PhaseSixEnvelope envelope, PhaseSixBudget budget, CancellationToken token)
    {
        void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException("INVALID_VIRTUAL_PLAN: " + reason); }
        var plan = envelope.Plan; budget.Check();
        Require(plan.PlanVersion == PhaseSixFormat.Version && envelope.PlanSha256 == PhaseSixFormat.Digest(plan), "version/digest");
        PhaseSixFormat.ValidateLimits(plan.Limits);
        Require(PhaseSixFormat.Equal(plan.Provenance, admitted.Provenance) && PhaseSixFormat.Equal(plan.Roots, admitted.Roots), "input provenance/root inventories");
        Require(PhaseSixFormat.Equal(plan.Conflicts, admitted.Conflicts), "conflict witnesses and canonical order");
        Require(PhaseSixFormat.Equal(plan.States, new PhaseSixStates(admitted.Conflicts.Length == 0 ? "UNION_PHYSICALLY_COMPATIBLE" : "UNION_BLOCKED_CONFLICT")), "states/authority");
        Require(plan.Limitations.SequenceEqual(PhaseSixFormat.Limitations, StringComparer.Ordinal), "preservation limitations");
        var input = admitted.Roots.SelectMany(r => r.Entries).ToDictionary(e => new PhaseSixSource(e.ObservationRootId, e.EntryId));
        var chartObligations = input.Where(p => p.Value.IsChart).OrderBy(p => p.Key.RootId).ThenBy(p => p.Key.EntryId)
            .Select(p => new PhaseSixChartObligation(p.Key, p.Value.Sha256!, Path.GetDirectoryName(p.Value.OriginalPath)!)).ToArray();
        Require(PhaseSixFormat.Equal(plan.ChartObligations, chartObligations), "per-appearance unknown dependency obligations");
        Require(input.Count <= plan.Limits.MaximumEntries && plan.VirtualEntries.Length <= plan.Limits.MaximumVirtualEntries && plan.Conflicts.Length <= plan.Limits.MaximumConflicts, "budgets");
        var witnessPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in admitted.Conflicts) foreach (var source in c.Sources) witnessPaths.Add(input[source].RelativePath);
        // A different traversal from construction: check each occurrence and its
        // ancestors, then require exact path groups and a bijective accounting ledger.
        var blocked = new HashSet<PhaseSixSource>();
        foreach (var (source, entry) in input) {
            budget.Check(); var ancestor = entry.RelativePath;
            while (true) {
                if (witnessPaths.Contains(ancestor)) { blocked.Add(source); break; }
                var slash = ancestor.LastIndexOf('/'); if (slash < 0) break; ancestor = ancestor[..slash];
            }
        }
        Require(plan.Conservation.Length == input.Count, "one disposition per source appearance");
        var ledger = plan.Conservation.ToDictionary(c => c.Source);
        Require(ledger.Keys.ToHashSet().SetEquals(input.Keys), "missing/foreign conservation sources");
        var expectedPaths = input.Where(p => !blocked.Contains(p.Key)).GroupBy(p => p.Value.RelativePath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Key).OrderBy(s => s.RootId).ThenBy(s => s.EntryId).ToArray(), StringComparer.Ordinal);
        Require(plan.VirtualEntries.Select(t => t.RelativePath).SequenceEqual(expectedPaths.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal), "exact virtual path set/order");
        var consumed = new HashSet<PhaseSixSource>(); var namespaceKinds = plan.VirtualEntries.ToDictionary(t => t.RelativePath, t => t.Kind, StringComparer.OrdinalIgnoreCase);
        foreach (var target in plan.VirtualEntries) {
            budget.Check(); Require(target.Sources.SequenceEqual(expectedPaths[target.RelativePath]), "all and only contributing occurrences");
            foreach (var source in target.Sources) {
                var original = input[source];
                Require(consumed.Add(source) && original.Kind == target.Kind && original.Sha256 == target.Sha256 && original.RelativePath == target.RelativePath, "target kind/bytes/path/multiplicity");
            }
            var ancestor = target.RelativePath;
            while (ancestor.Contains('/')) {
                ancestor = ancestor[..ancestor.LastIndexOf('/')];
                Require(namespaceKinds.TryGetValue(ancestor, out var kind) && kind == "DIRECTORY", "target ancestor compatibility");
            }
        }
        foreach (var (source, original) in input) {
            budget.Check(); var disposition = ledger[source]; var isBlocked = blocked.Contains(source);
            Require(disposition.RelativePath == original.RelativePath && disposition.Disposition == (isBlocked ? "BLOCKED_CONFLICT" : "REPRESENTED_VIRTUALLY"), "source disposition/path");
            Require(disposition.Reason == (isBlocked ? "Physical conflict at this path or an ancestor; preserve source in place." : "Original occurrence retained in virtual target provenance; no copy source or removal selected."), "source reason");
            Require(isBlocked != consumed.Contains(source), "conservation partition");
        }
        Require(plan.Conservation.Select(c => c.Source).SequenceEqual(input.Keys.OrderBy(s => s.RootId).ThenBy(s => s.EntryId)), "ledger order");
        var counts = new PhaseSixCounts(input.Count, input.Values.Count(e => e.IsChart), expectedPaths.Count,
            expectedPaths.Values.Count(s => s.Length == 2), expectedPaths.Values.Count(s => s.Length == 1), blocked.Count, admitted.Conflicts.Length);
        Require(plan.Counts == counts, "counts independently reconstructed"); budget.Check(); token.ThrowIfCancellationRequested();
    }
}
