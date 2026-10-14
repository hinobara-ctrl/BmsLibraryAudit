using System.Globalization;

namespace BmsLibraryAudit.Core;

// A named, bounded nested-random model, not a simulator compatibility claim.
// Intersections are per variable; no product of random ranges is enumerated.
internal sealed class PhaseFourConditions
{
    internal const string Model = "nested-random-first-matching-branch-v1";
    private sealed record Frame(int Id, string Kind, int? RandomId, HashSet<int> Labels);
    private readonly Dictionary<int, SymbolicConstraint[]> predicates = [];
    private readonly HashSet<int> unknownDecodings = [];
    private readonly ChartSyntaxResult syntax;
    internal bool Limited { get; private set; }
    internal bool Complete => unknownDecodings.Count == 0 && !Limited;

    internal PhaseFourConditions(ChartSyntaxResult syntax, PhaseFourLimits limits, CancellationToken token)
    {
        this.syntax = syntax; long terms = 0;
        foreach (var decoding in syntax.Decodings)
        {
            var contexts = syntax.ConditionalContexts.Where(c => c.Source.DecodingId == decoding.Id).ToArray();
            if (syntax.Diagnostics.Any(d => d.Source?.DecodingId == decoding.Id && d.Code is
                "INVALID_CONDITIONAL_ARGUMENT" or "ORPHAN_BRANCH" or "ORPHAN_CASE" or "MISMATCHED_CONDITIONAL_END" or "UNCLOSED_CONDITIONAL") ||
                contexts.Any(c => c.Directive is not ("RANDOM" or "IF" or "ELSEIF" or "ELSE" or "ENDIF" or "ENDRANDOM")) ||
                syntax.UnknownEvidence.Any(u => u.Source.DecodingId == decoding.Id) ||
                syntax.Diagnostics.Any(d => d.Code is "FACT_OR_DEPTH_LIMIT" or "LINE_LIMIT"))
            { unknownDecodings.Add(decoding.Id); continue; }
            var stack = new List<Frame>(); var ranges = new Dictionary<int, int>();
            foreach (var context in contexts)
            {
                token.ThrowIfCancellationRequested();
                var inherited = context.ParentId is int parent && predicates.TryGetValue(parent, out var previous) ? previous : [];
                SymbolicConstraint? own = null;
                switch (context.Directive)
                {
                    case "RANDOM":
                        if (!Positive(context.Argument, out var range)) { unknownDecodings.Add(decoding.Id); break; }
                        ranges.Add(context.Id, range); stack.Add(new(context.Id, "RANDOM", context.Id, [])); break;
                    case "IF":
                        var random = stack.LastOrDefault(f => f.Kind == "RANDOM");
                        if (random is null || !Positive(context.Argument, out var value)) { unknownDecodings.Add(decoding.Id); break; }
                        own = new(random.Id, ranges[random.Id], value, []);
                        stack.Add(new(context.Id, "IF", random.Id, [value])); break;
                    case "ELSEIF":
                    case "ELSE":
                        if (stack.Count == 0 || stack[^1].Kind != "IF") { unknownDecodings.Add(decoding.Id); break; }
                        var chain = stack[^1]; stack.RemoveAt(stack.Count - 1);
                        int? equal = null;
                        if (context.Directive == "ELSEIF")
                        {
                            if (!Positive(context.Argument, out var label)) { unknownDecodings.Add(decoding.Id); break; }
                            equal = label;
                        }
                        else if (context.Argument.Length != 0) { unknownDecodings.Add(decoding.Id); break; }
                        own = new(chain.RandomId!.Value, ranges[chain.RandomId.Value], equal, chain.Labels.Order().ToArray());
                        if (equal is int next) chain.Labels.Add(next);
                        stack.Add(new(context.Id, equal is null ? "ELSE" : "IF", chain.RandomId, chain.Labels)); break;
                    case "ENDIF":
                        if (context.Argument.Length != 0 || stack.Count == 0 || stack[^1].Kind is not ("IF" or "ELSE")) unknownDecodings.Add(decoding.Id);
                        else stack.RemoveAt(stack.Count - 1); break;
                    case "ENDRANDOM":
                        if (context.Argument.Length != 0 || stack.Count == 0 || stack[^1].Kind != "RANDOM") unknownDecodings.Add(decoding.Id);
                        else stack.RemoveAt(stack.Count - 1); break;
                }
                // Charge inherited and excluded terms, rather than random ranges.
                terms += inherited.Sum(c => 1L + c.ExcludedValues.Length) + (own is null ? 0 : 1L + own.ExcludedValues.Length);
                if (stack.Count > limits.MaximumConditionalDepth || terms > limits.MaximumSymbolicTerms)
                { Limited = true; unknownDecodings.Add(decoding.Id); break; }
                predicates[context.Id] = own is null ? inherited : [.. inherited, own];
            }
            if (stack.Count > 1 || stack.Any(f => f.Kind != "RANDOM")) unknownDecodings.Add(decoding.Id);
        }
    }
    private static bool Positive(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    internal ResourceReachability For(int decoding, int[] contexts)
    {
        if (unknownDecodings.Contains(decoding) || Limited)
            return new("UNKNOWN", Model, [], "Unsupported, malformed or bounded-out control evidence; all alternatives retained.");
        var constraints = contexts.Length != 0 && predicates.TryGetValue(contexts[^1], out var predicate) ? predicate : [];
        // ParentId propagates predicates, but an inherited predicate only guards
        // its own RandomContextId. Every active RANDOM needs its own admitted IF
        // chain before this reference can receive a model-specific conclusion.
        var guardedRandoms = constraints.Select(c => c.RandomContextId).ToHashSet();
        foreach (var id in contexts)
            if (syntax.ConditionalContexts[id].Directive == "RANDOM" && !guardedRandoms.Contains(id))
                return new("UNKNOWN", Model, constraints, FormattableString.Invariant($"Active RANDOM context {id} has no own admitted IF predicate; inherited or inner predicates do not interpret this scope. All alternatives retained."));
        if (constraints.Length == 0) return new("UNCONDITIONAL_SYNTAX", Model, [], "No supported conditional predicate surrounds this syntax; necessity is not inferred.");
        foreach (var variable in constraints.GroupBy(c => c.RandomContextId))
        {
            var bounds = variable.First().UpperBound;
            var equals = variable.Where(c => c.EqualValue is not null).Select(c => c.EqualValue!.Value).Distinct().ToArray();
            var excluded = variable.SelectMany(c => c.ExcludedValues).Where(n => n >= 1 && n <= bounds).Distinct().ToArray();
            if (equals.Length > 1 || equals.Any(n => n < 1 || n > bounds || excluded.Contains(n)) || equals.Length == 0 && excluded.Length == bounds)
                return new("IMPOSSIBLE_WITHIN_MODEL", Model, constraints, "Intersection is empty under the named model only; original declarations and physical candidates are retained.");
        }
        return new("POSSIBLE_WITHIN_MODEL", Model, constraints, "Nonempty per-random intersections; no playback selection or universal simulator semantics asserted.");
    }
}
