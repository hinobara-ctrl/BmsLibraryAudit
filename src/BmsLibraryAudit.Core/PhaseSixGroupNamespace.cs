namespace BmsLibraryAudit.Core;

// Shares physical admission/comparison, never derives another content comparison.
// The full proposed namespace is also checked for every subset, not just its edges.
internal static class PhaseSixGroupNamespace
{
    internal static PhaseSixRoot[] Select(PhaseSixRoot[] roots, int mask) => roots.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
    internal static bool Compatible(PhaseSixRoot[] roots, PhaseSixConflict[] conflicts, PhaseSixBudget budget)
    {
        var ids = roots.Select(r => r.Id).ToHashSet();
        var annotated = conflicts.Any(c => c.Sources.All(s => ids.Contains(s.RootId)));
        var paths = new Dictionary<string, (string Spelling, string Kind, string? Sha)>(StringComparer.OrdinalIgnoreCase);
        var compatible = !annotated;
        foreach (var entry in roots.SelectMany(r => r.Entries)) {
            budget.Check();
            if (paths.TryGetValue(entry.RelativePath, out var other)) {
                if (other.Spelling != entry.RelativePath || other.Kind != entry.Kind || other.Sha != entry.Sha256) compatible = false;
            } else paths.Add(entry.RelativePath, (entry.RelativePath, entry.Kind, entry.Sha256));
        }
        foreach (var item in paths.Values) {
            budget.Check(); var ancestor = item.Spelling;
            while (ancestor.Contains('/')) {
                ancestor = ancestor[..ancestor.LastIndexOf('/')];
                if (!paths.TryGetValue(ancestor, out var parent) || parent.Kind != "DIRECTORY" || parent.Spelling != ancestor) compatible = false;
            }
        }
        if (!paths.TryGetValue(".", out var root) || root.Kind != "DIRECTORY") compatible = false;
        return compatible;
    }
    internal static string[] LayoutConflicts(IEnumerable<string> paths, bool hasVariants) => !hasVariants ? [] : paths
        .Where(p => p.Split('/')[0].Equals("_variantes", StringComparison.OrdinalIgnoreCase))
        .Order(StringComparer.Ordinal).Select(p => "VARIANT_LAYOUT_NAMESPACE_CONFLICT: " + p).ToArray();
    internal static PhaseSixEvidenceAdmission Admit(PhaseSixOptions options, PhaseSixGroupLimits limits, CancellationToken token)
    {
        PhaseSixGroupFormat.ValidateLimits(limits);
        if (options.RootKeys is null || options.RootKeys.Count > limits.MaximumRoots) throw new InvalidOperationException("Group root budget exceeded.");
        var admission = PhaseSixEvidenceAdmission.LoadGroup(options, PhaseSixGroupFormat.AdmissionLimits(limits), token);
        try {
            if (admission.Conflicts.Any(c => c.Types.Contains("POTENTIAL_NATIVE_ALIAS_CONFLICT", StringComparer.Ordinal)))
                throw new InvalidOperationException("Unresolved native identity conflict.");
            foreach (var root in admission.Roots)
                if (!Compatible([root], admission.Conflicts, admission.Budget)) throw new InvalidOperationException("Root has an incompatible internal namespace.");
            return admission;
        } catch { admission.Dispose(); throw; }
    }
}
