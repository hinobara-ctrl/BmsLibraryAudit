namespace BmsLibraryAudit.Core;

internal static class PhaseFourSemantics
{
    private sealed record Key(int Decoding, string Namespace, string? LogicalId);
    internal static PhaseFourSemanticAnalysis Analyze(ChartSyntaxResult syntax, ResolvedResourceDeclaration[] resolved,
        PhaseFourPaths paths, PhaseFourLimits limits, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var conditions = new PhaseFourConditions(syntax, limits, token, admitFixedRandom: true, admitFlatSwitch: true);
        var symbols = new SemanticBudget(limits); var diagnostics = new HashSet<string>(StringComparer.Ordinal);
        var declarations = syntax.Declarations.ToDictionary(d => d.Id);
        var groups = syntax.Declarations.Where(d => d.LogicalId is not null).GroupBy(d => new Key(d.Source.DecodingId, d.Namespace, d.LogicalId))
            .ToDictionary(g => g.Key, g => g.ToArray());
        bool Supported(string state) => state is not ("UNKNOWN" or "UNSUPPORTED");
        ResourceReachability FactCondition(string state, ChartSourcePosition source, int[] contexts) => Supported(state)
            ? conditions.For(source.DecodingId, contexts, source.Line) : new("UNKNOWN", conditions.AppliedModel, [], "Unsupported/invalid source fact; syntax retained.");
        var declarationConditions = syntax.Declarations.ToDictionary(d => d.Id, d => FactCondition(
            d.Namespace == "BMSON_INFO" && d.LogicalId == "title_image" ? "UNKNOWN" : d.State, d.Source, d.ConditionalContextIds));
        var usageConditions = syntax.Usages.ToDictionary(u => u.Id, u => FactCondition(
            u.Namespace == "BMSON_INFO" && u.LogicalId == "title_image" ? "UNKNOWN" : u.State, u.Source, u.ConditionalContextIds));
        var relations = new List<DeclarationUsageRelation>(); var collisions = new List<DeclarationCoexistence>();
        var usageEvidence = new List<UsageDependencyEvidence>(); var catalog = new List<SemanticPhysicalCandidate>();
        var catalogIds = new Dictionary<(long Entry, string Policy), int>(); var candidateIds = new Dictionary<int, int[]>();
        var lookups = new List<BmsonAudioLookup>(); var obligations = new List<ConservationObligation>();
        var referenceLimited = syntax.Declarations.Length + syntax.Usages.Length > limits.MaximumReferences;
        var candidateLimited = false; var workLimited = referenceLimited; long work = 0, candidateLinks = 0;
        bool Work()
        {
            if (workLimited || ++work > limits.MaximumSemanticRelations) { workLimited = true; diagnostics.Add("SEMANTIC_RELATION_LIMIT"); return false; }
            return true;
        }
        ResourceReachability Joint(params ResourceReachability[] facts)
        {
            token.ThrowIfCancellationRequested();
            if (!symbols.Charge(1 + facts.Sum(f => f.Constraints.Sum(c => 1L + c.ExcludedValues.Length))))
                return new("UNKNOWN", conditions.AppliedModel, [], "SEMANTIC_SYMBOLIC_LIMIT: joint analysis incomplete; all source facts retained.");
            return conditions.Intersect(facts);
        }
        foreach (var resolution in resolved)
        {
            token.ThrowIfCancellationRequested(); var declaration = declarations[resolution.DeclarationId];
            var candidates = resolution.Candidates.AsEnumerable();
            if (syntax.Format == "BMSON" && syntax.FormatVersions.SequenceEqual(["1.0.0"]) && declaration.Namespace == "BMSON_SOUND")
            {
                var lookup = paths.BmsonAudioSearch(declaration.Id, resolution.CandidateRelativePath, resolution.ResolutionState);
                lookups.Add(lookup); candidates = candidates.Concat(lookup.Candidates);
            }
            var ids = new List<int>();
            foreach (var candidate in candidates.DistinctBy(c => (c.EntryId, c.MatchPolicy)))
            {
                if (++candidateLinks > limits.MaximumSemanticCandidateLinks) { candidateLimited = true; break; }
                var key = (candidate.EntryId, candidate.MatchPolicy);
                if (!catalogIds.TryGetValue(key, out var id)) { id = catalog.Count; catalogIds.Add(key, id); catalog.Add(new(id, candidate)); }
                ids.Add(id);
            }
            candidateIds.Add(declaration.Id, ids.ToArray());
        }
        // Diagnose declarations that may coexist without inventing first/last-wins.
        foreach (var group in groups.Values)
        {
            for (var i = 0; i < group.Length && !workLimited; i++)
                for (var j = i + 1; j < group.Length && !workLimited; j++)
                {
                    if (!Work()) break;
                    var a = group[i]; var b = group[j]; var intersection = Joint(declarationConditions[a.Id], declarationConditions[b.Id]);
                    var state = intersection.State == "UNKNOWN" ? "UNKNOWN_DECLARATION_COEXISTENCE" : intersection.State == "IMPOSSIBLE_WITHIN_MODEL"
                        ? "CANNOT_CO_OCCUR_WITHIN_MODEL" : a.ConditionalContextIds.Length == 0 && b.ConditionalContextIds.Length == 0
                        ? "SIMULTANEOUS_DECLARATIONS_PRECEDENCE_UNKNOWN" : (a.ConditionalContextIds.Length == 0) != (b.ConditionalContextIds.Length == 0)
                        ? "POSSIBLE_CONDITIONAL_REDEFINITION_PRECEDENCE_UNKNOWN" : "COEXISTING_DECLARATIONS_PRECEDENCE_UNKNOWN";
                    collisions.Add(new(collisions.Count, a.Id, b.Id, a.Source.DecodingId, a.Namespace, a.LogicalId!, intersection, state,
                        "Coexistence is qualified by the named predicate model. Textual position and equal bytes do not prove an effective precedence rule."));
                }
        }
        var collisionGroups = collisions.GroupBy(c => new Key(c.DecodingId, c.Namespace, c.LogicalId)).ToDictionary(g => g.Key, g => g.ToArray());
        long expected = 0;
        foreach (var usage in syntax.Usages)
        {
            token.ThrowIfCancellationRequested();
            var key = new Key(usage.Source.DecodingId, usage.Namespace, usage.LogicalId);
            var choices = usage.LogicalId is null ? [] : groups.GetValueOrDefault(key) ?? [];
            expected += Math.Max(1, choices.Length);
            var ownRelations = new List<int>(); var use = usageConditions[usage.Id];
            if (choices.Length == 0 && Work())
            {
                ownRelations.Add(relations.Count); relations.Add(new(relations.Count, usage.Id, null, usage.Source.DecodingId, usage.Namespace, usage.LogicalId,
                    null, use, use, "UNRESOLVED_LOGICAL_REFERENCE", "NO_DECLARATION", [], ["No declaration for the admitted logical identity; no physical absence assertion."]));
            }
            foreach (var declaration in choices)
            {
                if (!Work()) break;
                var intersection = Joint(declarationConditions[declaration.Id], use);
                var compatibility = intersection.State == "UNKNOWN" ? "UNKNOWN_SEMANTICS" : intersection.State == "IMPOSSIBLE_WITHIN_MODEL"
                    ? "CANNOT_CO_OCCUR_WITHIN_MODEL" : "MAY_BIND_WITHIN_MODEL";
                var physical = candidateIds[declaration.Id];
                var available = (int)Math.Max(0, Math.Min(physical.Length, limits.MaximumSemanticCandidateLinks - candidateLinks));
                candidateLinks += available;
                if (available != physical.Length) candidateLimited = true;
                ownRelations.Add(relations.Count); relations.Add(new(relations.Count, usage.Id, declaration.Id, usage.Source.DecodingId, usage.Namespace, usage.LogicalId,
                    declarationConditions[declaration.Id], use, intersection, compatibility, TextualOrder(declaration.Source, usage.Source), physical.Take(available).ToArray(),
                    ["Joint predicates describe a candidate, not simulator-effective assignment. All declaration/path evidence remains separately retained."]));
            }
            var effectiveUnknown = workLimited || ownRelations.Any(id => relations[id].Compatibility == "UNKNOWN_SEMANTICS");
            var overlap = false;
            foreach (var collision in collisionGroups.GetValueOrDefault(key) ?? [])
            {
                if (collision.Intersection.State == "IMPOSSIBLE_WITHIN_MODEL") continue;
                if (!Work()) { effectiveUnknown = true; break; }
                var joint = Joint(collision.Intersection, use);
                if (joint.State == "UNKNOWN") effectiveUnknown = true;
                else if (joint.State != "IMPOSSIBLE_WITHIN_MODEL") overlap = true;
            }
            var possible = ownRelations.Count(id => relations[id].Compatibility == "MAY_BIND_WITHIN_MODEL");
            var effective = effectiveUnknown ? "UNKNOWN_EFFECTIVE_DECLARATION" : choices.Length == 0 ? "UNRESOLVED_LOGICAL_REFERENCE"
                : overlap ? "AMBIGUOUS_EFFECTIVE_DECLARATION" : possible == 0 ? "NO_COMPATIBLE_DECLARATION_WITHIN_MODEL"
                : possible == 1 ? "SINGLE_CANDIDATE_WITHIN_MODEL" : "BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL";
            var coverage = workLimited ? new PhaseFourSymbolicCoverage.Result("INCOMPLETE_RELATION_COVERAGE", null)
                : PhaseFourSymbolicCoverage.Cover(use, choices.Select(d => declarationConditions[d.Id]).ToArray(), symbols, token, conditions.FixedValues);
            usageEvidence.Add(new(usage.Id, ownRelations.ToArray(), effective, coverage.State, coverage.Witness,
                "Coverage subtracts candidate predicate regions from the usage region. A single reachable candidate does not by itself cover all usage assignments; text order does not select a declaration."));
        }
        var relationsByDeclaration = relations.Where(r => r.DeclarationId is not null).GroupBy(r => r.DeclarationId!.Value).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var declaration in syntax.Declarations)
        {
            var attached = relationsByDeclaration.GetValueOrDefault(declaration.Id) ?? [];
            var state = workLimited || declarationConditions[declaration.Id].State == "UNKNOWN" || attached.Any(r => r.Compatibility == "UNKNOWN_SEMANTICS")
                ? "POTENTIAL_OBLIGATION_UNKNOWN_SEMANTICS" : attached.Any(r => r.Compatibility == "MAY_BIND_WITHIN_MODEL")
                ? declaration.ConditionalContextIds.Length == 0 ? "POTENTIAL_OBSERVED_USE" : "POTENTIAL_CONDITIONED_USE"
                : "NO_CO_OCCURRING_USE_OBSERVED_WITHIN_MODEL";
            obligations.Add(new(declaration.Id, state, candidateIds[declaration.Id], "Retain authored declaration and all observed candidates. No observed compatible use is not unused/removable proof, even for a model-impossible declaration."));
        }
        if (referenceLimited) diagnostics.Add("SEMANTIC_REFERENCE_LIMIT");
        if (candidateLimited || paths.AudioLimited) diagnostics.Add("SEMANTIC_CANDIDATE_LINK_LIMIT");
        if (symbols.Limited || conditions.Limited) diagnostics.Add("SEMANTIC_SYMBOLIC_LIMIT");
        var admittedSyntax = syntax.Decodings.Length == 1 && syntax.UnknownEvidence.Length == 0 &&
            syntax.Diagnostics.All(d => d.Code is "CONDITIONAL_SEMANTICS_DEFERRED" or "DUPLICATE_DECLARATION" or "CONFLICTING_DECLARATIONS");
        var modelComplete = admittedSyntax && conditions.Complete && !symbols.Limited &&
            declarationConditions.Values.Concat(usageConditions.Values).All(p => p.State != "UNKNOWN");
        var enumerationComplete = !workLimited && !candidateLimited && !paths.AudioLimited && !symbols.Limited && relations.Count == expected;
        var effectiveComplete = enumerationComplete && modelComplete && usageEvidence.All(u => u.DeclarationCoverage is "DECLARATIONS_COVER_USAGE_WITHIN_MODEL" or "USAGE_IMPOSSIBLE_WITHIN_MODEL" &&
            u.EffectiveDeclarationState is not ("AMBIGUOUS_EFFECTIVE_DECLARATION" or "UNKNOWN_EFFECTIVE_DECLARATION" or "UNRESOLVED_LOGICAL_REFERENCE"));
        // Complete coverage may be vacuous. Only a proven nonempty usage region
        // supplies positive binding evidence; UNKNOWN does not establish one.
        var hasSatisfiableUsage = usageConditions.Values.Any(p => p.State is "UNCONDITIONAL_SYNTAX" or "POSSIBLE_WITHIN_MODEL");
        var logicalCoverage = new LogicalReferenceCoverage(effectiveComplete && hasSatisfiableUsage ? "COMPLETE_WITHIN_ADMITTED_MODEL"
            : !hasSatisfiableUsage ? "NO_PROVEN_SATISFIABLE_USAGE" : "INCOMPLETE_OR_AMBIGUOUS",
            usageConditions.Values.Count(p => p.State is "UNCONDITIONAL_SYNTAX" or "POSSIBLE_WITHIN_MODEL"),
            usageConditions.Values.Count(p => p.State == "IMPOSSIBLE_WITHIN_MODEL"), usageConditions.Values.Count(p => p.State == "UNKNOWN"));
        var physicalDeclarations = resolved.Where(r => declarations[r.DeclarationId].WrittenPath is not null).ToArray();
        var resolutionsById = physicalDeclarations.ToDictionary(r => r.DeclarationId);
        bool Ordinal(ResolvedResourceDeclaration r) => r.WrittenPath is { Length: > 0 } && r.CandidateRelativePath is not null &&
            r.ResolutionState == "OBSERVED_PATH_MATCH" && r.PathPolicy == "LEXICAL_ORDINAL" && r.Candidates.Length == 1 &&
            r.Candidates[0].MatchPolicy == "LEXICAL_ORDINAL" &&
            string.Equals(PhaseFourPaths.Slashes(r.Candidates[0].RelativePath), r.CandidateRelativePath, StringComparison.Ordinal);
        var physicalRelations = relations.Where(r => r.DeclarationId is int id && resolutionsById.ContainsKey(id) && r.Compatibility == "MAY_BIND_WITHIN_MODEL").ToArray();
        var usagesById = usageEvidence.ToDictionary(u => u.UsageId);
        var coveredPhysicalRelations = physicalRelations.Where(r => usagesById[r.UsageId].DeclarationCoverage == "DECLARATIONS_COVER_USAGE_WITHIN_MODEL" &&
            usagesById[r.UsageId].EffectiveDeclarationState is "SINGLE_CANDIDATE_WITHIN_MODEL" or "BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL").ToArray();
        var observedRelationCount = coveredPhysicalRelations.Count(r => resolutionsById[r.DeclarationId!.Value].ResolutionState == "OBSERVED_PATH_MATCH" &&
            resolutionsById[r.DeclarationId.Value].Candidates.Length == 1);
        var ordinalRelationCount = coveredPhysicalRelations.Count(r => Ordinal(resolutionsById[r.DeclarationId!.Value]));
        var lookupCounts = lookups.ToDictionary(l => l.DeclarationId, l => l.Candidates.Length);
        var physicalEvidence = physicalDeclarations.Select(r => new PhysicalResourceBindingEvidence(r.DeclarationId,
            Ordinal(r) ? "ORDINAL_EXACT_OBSERVED_MATCH"
            : r.ResolutionState == "OBSERVED_PATH_MATCH" && r.PathPolicy == "WINDOWS_ORDINAL_IGNORE_CASE" && r.Candidates.Length == 1 ? "WINDOWS_QUALIFIED_OBSERVED_MATCH"
            : r.ResolutionState == "AMBIGUOUS_PATH_MATCH" ? "AMBIGUOUS_OBSERVED_MATCH"
            : r.ResolutionState is "NO_OBSERVED_MATCH" or "UNSUPPORTED_RESOLUTION" && lookupCounts.GetValueOrDefault(r.DeclarationId) > 0 ? "BMSON_SEARCH_PROPOSALS_ONLY"
            : r.PathPolicy == "ALTERNATE_EXTENSION_UNPROVEN" ? "ALTERNATE_EXTENSION_PROPOSALS_ONLY"
            : r.ResolutionState == "NO_OBSERVED_MATCH" ? "NO_OBSERVED_MATCH"
            : r.ResolutionState == "OUT_OF_SCOPE" ? "OUT_OF_SCOPE" : "UNKNOWN_OR_UNSUPPORTED",
            r.PathPolicy, r.Candidates.Length, lookupCounts.GetValueOrDefault(r.DeclarationId))).ToArray();
        var physicalLimited = paths.Limited || !enumerationComplete || physicalDeclarations.Any(r => r.Limitations.Any(l => l.Contains("LIMIT", StringComparison.Ordinal)));
        var ordinalBindings = physicalDeclarations.Length > 0 && !physicalLimited && physicalDeclarations.All(Ordinal);
        var physicalState = physicalDeclarations.Length == 0 ? "NO_PHYSICAL_RESOURCE_DECLARATIONS_IN_SCOPE" : physicalLimited ? "INCOMPLETE"
            : physicalEvidence.Any(p => p.State == "AMBIGUOUS_OBSERVED_MATCH") ? "AMBIGUOUS_OBSERVED_BINDINGS"
            : ordinalBindings ? ordinalRelationCount > 0 ? "COMPLETE_ORDINAL_OBSERVED_BINDINGS" : "ORDINAL_PATHS_WITHOUT_ADMITTED_USAGE_BINDING"
            : physicalEvidence.All(p => p.State is "ORDINAL_EXACT_OBSERVED_MATCH" or "WINDOWS_QUALIFIED_OBSERVED_MATCH") ? "QUALIFIED_WINDOWS_OBSERVED_BINDINGS"
            : physicalEvidence.Any(p => p.State == "BMSON_SEARCH_PROPOSALS_ONLY") ? "BMSON_SEARCH_PROPOSALS_ONLY_OR_MIXED"
            : physicalEvidence.Any(p => p.State == "ALTERNATE_EXTENSION_PROPOSALS_ONLY") ? "ALTERNATE_EXTENSION_PROPOSALS_ONLY_OR_MIXED" : "INCOMPLETE_OR_NO_OBSERVED_BINDINGS";
        var physicalCoverage = new PhysicalResourceBindingCoverage(physicalState, physicalDeclarations.Length, physicalRelations.Length,
            observedRelationCount, ordinalRelationCount, physicalEvidence);
        // Preserve the coarse graph-v1 field for readers; only the new ordinal
        // coverage and satisfiable physical relations authorize graph-v2 proof.
        var observedBindings = resolved.Where(r => declarations[r.DeclarationId].WrittenPath is not null).All(r => r.ResolutionState == "OBSERVED_PATH_MATCH");
        var blockers = new List<string>();
        if (!admittedSyntax) blockers.Add("Syntax/format/decoding uncertainty outside the admitted graph rules.");
        if (!modelComplete) blockers.Add("Conditional or semantic predicates incomplete/unknown.");
        if (!enumerationComplete) blockers.Add("Relation/candidate/symbolic enumeration limited.");
        if (!effectiveComplete) blockers.Add("Unbound usage assignments or unresolved effective precedence/logical identity.");
        if (!observedBindings) blockers.Add("Not every authored path has a unique historical literal binding.");
        if (syntax.Usages.Length == 0) blockers.Add("No usage bindings to prove; empty graph is not dependency closure.");
        if (!hasSatisfiableUsage) blockers.Add("NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL");
        if (!ordinalBindings) blockers.Add("ORDINAL_EXACT_PHYSICAL_BINDING_COVERAGE_NOT_PROVEN");
        if (physicalRelations.Length == 0) blockers.Add("NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE");
        else if (coveredPhysicalRelations.Length == 0) blockers.Add("NO_COVERED_UNAMBIGUOUS_PHYSICAL_USAGE_BINDING");
        else if (ordinalRelationCount == 0) blockers.Add("NO_ORDINAL_EXACT_PHYSICAL_USAGE_BINDING");
        return new("phase4-semantic-graph-2", [conditions.AppliedModel, "symbolic-interval-box-coverage-v1", "candidate-precedence-unselected-v1"],
            "VALIDATED_COMPLETE_CONTENT_COMPONENT", syntax.SyntaxCoverage, modelComplete ? "COMPLETE_WITHIN_ADMITTED_MODEL" : "INCOMPLETE",
            enumerationComplete ? "COMPLETE" : "INCOMPLETE", observedBindings ? "UNIQUE_OBSERVED_LITERAL_BINDINGS" : "INCOMPLETE_OR_NO_OBSERVED_MATCH",
            effectiveComplete ? "UNIQUE_CANDIDATE_COVERAGE_WITHIN_MODEL" : "INCOMPLETE_OR_AMBIGUOUS", expected, relations.ToArray(), collisions.ToArray(), usageEvidence.ToArray(), obligations.ToArray(), catalog.ToArray(), lookups.ToArray(),
            new("observed-logical-physical-ordinal-binding-evidence-2", blockers.Count == 0 ? "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE" : "NOT_PROVEN",
                "At least one satisfiable physical declaration/usage relation has complete unambiguous logical coverage and a unique ordinal-exact historical CONTENT path binding. All satisfiable logical usage assignments have unique declaration candidates within the named predicate model; every authored physical path has a unique LEXICAL_ORDINAL observed binding without truncation. Excludes Windows-only case matches, playback, codec usability/fallback, simulator portability, implicit dependencies, package closure and deletion authority.", blockers.ToArray()),
            diagnostics.Order(StringComparer.Ordinal).ToArray(),
            ["Legacy potential_usage_ids and reachability keep their v1 syntactic meanings; this separate graph evaluates semantic intersections.",
             "Flat SWITCH/SETSWITCH requires explicit SKIP between branches, DEF last and ENDSW; fallthrough/nested controls remain UNKNOWN. Legacy v1 reachability is unchanged. SETRANDOM uses a separately named fixed-value model.",
             "BMSON compatible-name lookup proposals are specific to sound_channels; no codec is tested, preferred order or equivalence asserted.",
             "Legacy physical_binding_coverage is coarse graph-v1 observation, accepts Windows case matches and can be vacuous; consult versioned logical_reference_coverage, physical_resource_binding_coverage and bounded_proof."],
            conditions.FixedValues.OrderBy(v => v.Key).Select(v => new SymbolicConstraint(v.Key, v.Value, v.Value, [])).ToArray(), logicalCoverage, physicalCoverage);
    }
    private static string TextualOrder(ChartSourcePosition declaration, ChartSourcePosition usage)
    {
        if (declaration.DecodingId != usage.DecodingId) return "DIFFERENT_DECODINGS";
        if (declaration.Line is not int left || usage.Line is not int right) return "JSON_POINTERS_NO_APPLICATION_ORDER";
        return left < right || left == right && declaration.Column < usage.Column ? "DECLARATION_BEFORE_USAGE_IN_TEXT" : "DECLARATION_AFTER_USAGE_IN_TEXT";
    }
}
