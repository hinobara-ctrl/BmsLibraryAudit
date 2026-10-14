using System.Globalization;

namespace BmsLibraryAudit.Core;

// A named, bounded nested-random model, not a simulator compatibility claim.
// Intersections are per variable; no product of random ranges is enumerated.
internal sealed class PhaseFourConditions
{
    internal const string Model = "nested-random-first-matching-branch-v1";
    internal const string FixedModel = "nested-random-fixed-value-first-matching-branch-v1";
    internal const string SwitchModel = "random-fixed-flat-switch-explicit-skip-v1";
    internal const string BmsonModel = "bmson-1.0-unconditional-resource-reference-v1";
    private sealed record Frame(int Id, string Kind, int? RandomId, HashSet<int> Labels);
    private readonly Dictionary<int, SymbolicConstraint[]> predicates = [];
    private readonly HashSet<int> unknownDecodings = [];
    private readonly ChartSyntaxResult syntax;
    private readonly Dictionary<int, int> fixedValues = [];
    private readonly Dictionary<int, int> skipLines = [];
    private readonly PhaseFourLimits limits;
    internal string AppliedModel { get; }
    internal IReadOnlyDictionary<int, int> FixedValues => fixedValues;
    internal bool Limited { get; private set; }
    internal bool Complete => unknownDecodings.Count == 0 && !Limited;

    internal PhaseFourConditions(ChartSyntaxResult syntax, PhaseFourLimits limits, CancellationToken token, bool admitFixedRandom = false, bool admitFlatSwitch = false)
    {
        this.syntax = syntax; this.limits = limits; long terms = 0;
        AppliedModel = admitFixedRandom && syntax.Format == "BMSON" ? BmsonModel
            : admitFlatSwitch && syntax.ConditionalContexts.Any(c => c.Directive is "SWITCH" or "SETSWITCH") ? SwitchModel
            : admitFixedRandom && syntax.ConditionalContexts.Any(c => c.Directive == "SETRANDOM") ? FixedModel : Model;
        foreach (var decoding in syntax.Decodings)
        {
            var contexts = syntax.ConditionalContexts.Where(c => c.Source.DecodingId == decoding.Id).ToArray();
            if (syntax.Diagnostics.Any(d => d.Source?.DecodingId == decoding.Id && d.Code is
                "INVALID_CONDITIONAL_ARGUMENT" or "ORPHAN_BRANCH" or "ORPHAN_CASE" or "MISMATCHED_CONDITIONAL_END" or "UNCLOSED_CONDITIONAL") ||
                contexts.Any(c => c.Directive is not ("RANDOM" or "IF" or "ELSEIF" or "ELSE" or "ENDIF" or "ENDRANDOM") && !(admitFixedRandom && c.Directive == "SETRANDOM") &&
                    !(admitFlatSwitch && c.Directive is "SWITCH" or "SETSWITCH" or "CASE" or "DEF" or "SKIP" or "ENDSW")) ||
                admitFlatSwitch && !AdmittedSwitches(contexts) ||
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
                    case "SETRANDOM":
                        if (!Positive(context.Argument, out var range)) { unknownDecodings.Add(decoding.Id); break; }
                        if (context.Directive == "SETRANDOM") fixedValues.Add(context.Id, range);
                        ranges.Add(context.Id, range); stack.Add(new(context.Id, "RANDOM", context.Id, [])); break;
                    case "SWITCH":
                    case "SETSWITCH":
                        if (!Positive(context.Argument, out var switchRange)) { unknownDecodings.Add(decoding.Id); break; }
                        if (context.Directive == "SETSWITCH") fixedValues.Add(context.Id, switchRange);
                        ranges.Add(context.Id, switchRange); stack.Add(new(context.Id, "SWITCH", context.Id, [])); break;
                    case "CASE":
                    case "DEF":
                        if (stack.Count > 0 && stack[^1].Kind == "CASE") stack.RemoveAt(stack.Count - 1);
                        if (stack.Count == 0 || stack[^1].Kind != "SWITCH") { unknownDecodings.Add(decoding.Id); break; }
                        var controller = stack[^1]; int? caseValue = null;
                        if (context.Directive == "CASE")
                        {
                            if (!Positive(context.Argument, out var label)) { unknownDecodings.Add(decoding.Id); break; }
                            caseValue = label;
                        }
                        own = new(controller.Id, ranges[controller.Id], caseValue, controller.Labels.Order().ToArray());
                        if (caseValue is int caseLabel) controller.Labels.Add(caseLabel);
                        stack.Add(new(context.Id, "CASE", controller.Id, [])); break;
                    case "SKIP":
                        if (stack.Count == 0 || stack[^1].Kind != "CASE" || context.Source.Line is null) unknownDecodings.Add(decoding.Id);
                        else skipLines[stack[^1].Id] = context.Source.Line.Value;
                        break;
                    case "ENDSW":
                        if (stack.Count > 0 && stack[^1].Kind == "CASE") stack.RemoveAt(stack.Count - 1);
                        if (stack.Count == 0 || stack[^1].Kind != "SWITCH") unknownDecodings.Add(decoding.Id);
                        else stack.RemoveAt(stack.Count - 1);
                        break;
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
    private static bool AdmittedSwitches(ChartConditionalContext[] contexts)
    {
        for (var index = 0; index < contexts.Length; index++)
        {
            if (contexts[index].Directive is not ("SWITCH" or "SETSWITCH")) continue;
            var controller = contexts[index]; var end = index + 1;
            while (end < contexts.Length && contexts[end].Directive != "ENDSW") end++;
            if (end == contexts.Length || contexts[end].Argument.Length != 0) return false;
            var body = contexts[(index + 1)..end];
            if (body.Any(c => c.Directive is not ("CASE" or "DEF" or "SKIP"))) return false;
            var branches = body.Where(c => c.Directive is "CASE" or "DEF").ToArray();
            if (branches.Length == 0 || branches.Count(c => c.Directive == "DEF") > 1 || branches.Any(c => c.ParentId != controller.Id) ||
                branches.Any(c => c.Directive == "DEF" && (c.Id != branches[^1].Id || c.Argument.Length != 0))) return false;
            var skipsByBranch = body.Where(c => c.Directive == "SKIP").ToLookup(c => c.ParentId);
            var branchIds = branches.Select(b => b.Id).ToHashSet();
            for (var branch = 0; branch < branches.Length; branch++)
            {
                var skips = skipsByBranch[branches[branch].Id].ToArray();
                if (skips.Length > 1 || skips.Any(c => c.Argument.Length != 0 || c.Source.Line is null) || branch < branches.Length - 1 && skips.Length != 1) return false;
            }
            if (body.Any(c => c.Directive == "SKIP" && (c.ParentId is null || !branchIds.Contains(c.ParentId.Value)))) return false;
            index = end;
        }
        return true;
    }
    internal ResourceReachability For(int decoding, int[] contexts, int? sourceLine = null)
    {
        if (unknownDecodings.Contains(decoding) || Limited)
            return new("UNKNOWN", AppliedModel, [], "Unsupported, malformed or bounded-out control evidence; all alternatives retained.");
        var constraints = contexts.Length != 0 && predicates.TryGetValue(contexts[^1], out var predicate) ? predicate : [];
        // ParentId propagates predicates, but an inherited predicate only guards
        // its own RandomContextId. Every active RANDOM needs its own admitted IF
        // chain before this reference can receive a model-specific conclusion.
        var guardedRandoms = constraints.Select(c => c.RandomContextId).ToHashSet();
        foreach (var id in contexts)
            if (syntax.ConditionalContexts[id].Directive is "RANDOM" or "SETRANDOM" or "SWITCH" or "SETSWITCH" && !guardedRandoms.Contains(id))
                return new("UNKNOWN", AppliedModel, constraints, syntax.ConditionalContexts[id].Directive is "SWITCH" or "SETSWITCH"
                    ? FormattableString.Invariant($"Active SWITCH context {id} has no own admitted CASE/DEF predicate; all alternatives retained.")
                    : FormattableString.Invariant($"Active RANDOM context {id} has no own admitted IF predicate; inherited or inner predicates do not interpret this scope. All alternatives retained."));
        if (constraints.Length == 0) return new("UNCONDITIONAL_SYNTAX", AppliedModel, [], "No supported conditional predicate surrounds this syntax; necessity is not inferred.");
        // A fixed value is a domain condition, not a substitute for an IF guard.
        constraints = [.. constraints, .. constraints.Select(c => c.RandomContextId).Distinct().Order()
            .Where(fixedValues.ContainsKey).Select(id => new SymbolicConstraint(id, fixedValues[id], fixedValues[id], []))];
        foreach (var id in contexts.Where(skipLines.ContainsKey))
        {
            if (sourceLine is null) return new("UNKNOWN", AppliedModel, constraints, "SKIP requires the preserved fact position; no playback assumption.");
            if (sourceLine > skipLines[id])
            {
                var scope = syntax.ConditionalContexts[id].ParentId!.Value;
                var upper = constraints.First(c => c.RandomContextId == scope).UpperBound;
                constraints = [.. constraints, new SymbolicConstraint(scope, upper, 0, [])];
            }
        }
        return Evaluate(constraints);
    }
    internal ResourceReachability Intersect(params ResourceReachability[] operands)
    {
        // Unknown operands take precedence over contradictions in known ones.
        if (operands.Any(o => o.State == "UNKNOWN" || o.Model != AppliedModel))
            return new("UNKNOWN", AppliedModel, [], "Joint semantics are unknown; no incompatibility reduction is authorized.");
        var constraints = operands.SelectMany(o => o.Constraints).ToArray();
        if (constraints.Sum(c => 1L + c.ExcludedValues.Length) > limits.MaximumSymbolicTerms)
            return new("UNKNOWN", AppliedModel, [], "INTERSECTION_LIMIT: joint predicates exceed symbolic bounds.");
        if (constraints.Length == 0) return new("UNCONDITIONAL_SYNTAX", AppliedModel, [], "No conditional predicates in the admitted operands.");
        return Evaluate(constraints);
    }
    private ResourceReachability Evaluate(SymbolicConstraint[] constraints)
    {
        foreach (var variable in constraints.GroupBy(c => c.RandomContextId))
        {
            var bounds = variable.First().UpperBound;
            var equals = variable.Where(c => c.EqualValue is not null).Select(c => c.EqualValue!.Value).Distinct().ToArray();
            var excluded = variable.SelectMany(c => c.ExcludedValues).Where(n => n >= 1 && n <= bounds).Distinct().ToArray();
            if (equals.Length > 1 || equals.Any(n => n < 1 || n > bounds || excluded.Contains(n)) || equals.Length == 0 && excluded.Length == bounds)
                return new("IMPOSSIBLE_WITHIN_MODEL", AppliedModel, constraints, "Intersection is empty under the named model only; original declarations and physical candidates are retained.");
        }
        return new("POSSIBLE_WITHIN_MODEL", AppliedModel, constraints, "Nonempty per-random intersections; no playback selection or universal simulator semantics asserted.");
    }
}
