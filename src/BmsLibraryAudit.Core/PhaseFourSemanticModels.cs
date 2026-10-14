namespace BmsLibraryAudit.Core;

public sealed record SemanticPhysicalCandidate(int Id, ObservedResourceCandidate Evidence);
public sealed record DeclarationUsageRelation(int Id, int UsageId, int? DeclarationId, int DecodingId,
    string Namespace, string? LogicalId, ResourceReachability? DeclarationCondition, ResourceReachability UsageCondition,
    ResourceReachability Intersection, string Compatibility, string TextualOrder, int[] PhysicalCandidateIds, string[] Limitations);
public sealed record DeclarationCoexistence(int Id, int LeftDeclarationId, int RightDeclarationId, int DecodingId,
    string Namespace, string LogicalId, ResourceReachability Intersection, string State, string Justification);
public sealed record SymbolicInterval(int First, int Last);
public sealed record SymbolicAxis(int RandomContextId, SymbolicInterval[] Values);
public sealed record UsageDependencyEvidence(int UsageId, int[] RelationIds, string EffectiveDeclarationState,
    string DeclarationCoverage, SymbolicAxis[]? UnboundWitness, string Justification);
public sealed record ConservationObligation(int DeclarationId, string State, int[] PhysicalCandidateIds, string Justification);
public sealed record BmsonAudioLookup(int DeclarationId, string Policy, string State, string Trigger,
    ObservedResourceCandidate[] Candidates, bool CodecCompatibilityProven, string Notice);
public sealed record BoundedDependencyProof(string Version, string State, string Scope, string[] Blockers);
public sealed record PhaseFourSemanticAnalysis(string Version, string[] Models, string PhysicalEvidenceCoverage,
    string ParserCoverage, string ConditionalModelCoverage, string RelationEnumerationCoverage, string PhysicalBindingCoverage,
    string EffectiveDeclarationCoverage, long ExpectedRelationCount, DeclarationUsageRelation[] Relations,
    DeclarationCoexistence[] Coexistences, UsageDependencyEvidence[] UsageEvidence, ConservationObligation[] ConservationObligations,
    SemanticPhysicalCandidate[] PhysicalCandidates, BmsonAudioLookup[] BmsonAudioLookups,
    BoundedDependencyProof BoundedProof, string[] Diagnostics, string[] Limitations, SymbolicConstraint[] DomainConstraints);
