namespace BmsLibraryAudit.Core;

internal sealed class SemanticBudget(PhaseFourLimits limits)
{
    private long terms;
    internal bool Limited { get; private set; }
    internal bool Charge(long count)
    {
        terms += count;
        if (terms > Math.Min(limits.MaximumSymbolicTerms, limits.MaximumSemanticSymbolicTerms)) Limited = true;
        return !Limited;
    }
}

// Exact finite interval-set boxes, never enumeration of random values/products.
// Subtracting declaration boxes from a usage box proves whether the candidates
// cover every assignment in that named model, separately from path availability.
internal static class PhaseFourSymbolicCoverage
{
    internal sealed record Result(string State, SymbolicAxis[]? Witness);
    internal static Result Cover(ResourceReachability usage, ResourceReachability[] declarations, SemanticBudget budget, CancellationToken token,
        IReadOnlyDictionary<int, int>? fixedValues = null)
    {
        token.ThrowIfCancellationRequested();
        if (usage.State == "UNKNOWN" || declarations.Any(d => d.State == "UNKNOWN")) return new("UNKNOWN_SEMANTICS", null);
        if (usage.State == "IMPOSSIBLE_WITHIN_MODEL") return new("USAGE_IMPOSSIBLE_WITHIN_MODEL", null);
        var all = usage.Constraints.Concat(declarations.SelectMany(d => d.Constraints)).ToArray();
        if (!budget.Charge(all.Sum(c => 1L + c.ExcludedValues.Length))) return new("INCOMPLETE_SYMBOLIC_COVERAGE", null);
        var domains = all.GroupBy(c => c.RandomContextId).OrderBy(g => g.Key).Select(g => (Id: g.Key, Upper: g.First().UpperBound)).ToArray();
        var initial = Box(usage.Constraints, domains, fixedValues);
        var uncovered = new List<SymbolicAxis[]> { initial };
        foreach (var declaration in declarations)
        {
            token.ThrowIfCancellationRequested();
            if (declaration.State == "IMPOSSIBLE_WITHIN_MODEL") continue;
            var covered = Box(declaration.Constraints, domains, fixedValues); var next = new List<SymbolicAxis[]>();
            foreach (var box in uncovered)
            {
                token.ThrowIfCancellationRequested();
                if (!budget.Charge(1 + box.Sum(a => 1L + a.Values.Length) + covered.Sum(a => 1L + a.Values.Length))) return new("INCOMPLETE_SYMBOLIC_COVERAGE", null);
                var overlap = box.Select((a, i) => new SymbolicAxis(a.RandomContextId, Intersect(a.Values, covered[i].Values))).ToArray();
                if (overlap.Any(a => a.Values.Length == 0)) { next.Add(box); continue; }
                for (var axis = 0; axis < box.Length; axis++)
                {
                    var difference = Difference(box[axis].Values, covered[axis].Values);
                    if (difference.Length == 0) continue;
                    var remainder = box.Select((a, i) => i < axis ? overlap[i] : i == axis ? a with { Values = difference } : a).ToArray();
                    if (!budget.Charge(remainder.Sum(a => 1L + a.Values.Length) + 1)) return new("INCOMPLETE_SYMBOLIC_COVERAGE", null);
                    next.Add(remainder);
                }
                // The intersection itself is covered and is not retained.
            }
            uncovered = next;
            if (uncovered.Count == 0) return new("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", null);
        }
        return new("UNBOUND_ASSIGNMENTS_WITHIN_MODEL", uncovered.FirstOrDefault());
    }
    private static SymbolicAxis[] Box(SymbolicConstraint[] constraints, (int Id, int Upper)[] domains, IReadOnlyDictionary<int, int>? fixedValues)
    {
        var groups = constraints.GroupBy(c => c.RandomContextId).ToDictionary(g => g.Key, g => g.ToArray());
        return domains.Select(d => {
            SymbolicInterval[] values = fixedValues is not null && fixedValues.TryGetValue(d.Id, out var fixedValue) ? [new(fixedValue, fixedValue)] : [new(1, d.Upper)];
            foreach (var constraint in groups.GetValueOrDefault(d.Id) ?? [])
            {
                if (constraint.EqualValue is int equal) values = Intersect(values, [new(equal, equal)]);
                values = Difference(values, constraint.ExcludedValues.Distinct().Order().Select(v => new SymbolicInterval(v, v)).ToArray());
            }
            return new SymbolicAxis(d.Id, values);
        }).ToArray();
    }
    private static SymbolicInterval[] Intersect(SymbolicInterval[] left, SymbolicInterval[] right)
    {
        var result = new List<SymbolicInterval>(); var i = 0; var j = 0;
        while (i < left.Length && j < right.Length)
        {
            var first = Math.Max(left[i].First, right[j].First); var last = Math.Min(left[i].Last, right[j].Last);
            if (first <= last) result.Add(new(first, last));
            if (left[i].Last < right[j].Last) i++; else j++;
        }
        return result.ToArray();
    }
    private static SymbolicInterval[] Difference(SymbolicInterval[] left, SymbolicInterval[] right)
    {
        var result = new List<SymbolicInterval>();
        foreach (var interval in left)
        {
            long cursor = interval.First;
            foreach (var removed in right)
            {
                if (removed.Last < cursor) continue;
                if (removed.First > interval.Last) break;
                if (removed.First > cursor) result.Add(new((int)cursor, Math.Min(interval.Last, removed.First - 1)));
                cursor = Math.Max(cursor, (long)removed.Last + 1);
                if (cursor > interval.Last) break;
            }
            if (cursor <= interval.Last) result.Add(new((int)cursor, interval.Last));
        }
        return result.ToArray();
    }
}
