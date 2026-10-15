using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BmsLibraryAudit.Core;
using Xunit;
using Evidence = BmsLibraryAudit.Tests.PhaseFourTests.Evidence;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseFourContractTests
{
    [Theory]
    [InlineData("#WAV01 kick.wav\n#00111:01", "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE")]
    [InlineData("#WAV01 KICK.WAV\n#00111:01", "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01", "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#WAV01 kick.wav", "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#RANDOM 2\n#IF 3\n#WAV01 kick.wav\n#00211:01\n#ENDIF\n#ENDRANDOM", "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#RANDOM 2\n#IF 3\n#WAV01 kick.wav\n#ENDIF\n#ENDRANDOM", "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#WAV01 kick.wav\n#00111:01", "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE")]
    [InlineData("#WAV01 kick.wav\n#RANDOM 2\n#IF 3\n#00111:01\n#ENDIF\n#ENDRANDOM", "NOT_PROVEN")]
    public void OrdinalProofRequiresASatisfiablePhysicalRelation(string chart, string proof)
    {
        var a = PhaseFourSemanticTests.Analyze(chart, files: ["kick.wav"]);
        Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", a.ConditionalModelCoverage);
        Assert.Equal("COMPLETE", a.RelationEnumerationCoverage);
        Assert.Equal("UNIQUE_CANDIDATE_COVERAGE_WITHIN_MODEL", a.EffectiveDeclarationCoverage);
        Assert.Equal("UNIQUE_OBSERVED_LITERAL_BINDINGS", a.PhysicalBindingCoverage);
        Assert.Equal(proof, a.BoundedProof.State);
        Assert.Equal(a.Relations.Length, a.UsageEvidence.Length);
        Assert.Equal("phase4-semantic-graph-2", a.Version);
        Assert.Equal("observed-logical-physical-ordinal-binding-evidence-2", a.BoundedProof.Version);
        var logical = Assert.IsType<LogicalReferenceCoverage>(a.LogicalReferenceCoverage);
        var physical = Assert.IsType<PhysicalResourceBindingCoverage>(a.PhysicalResourceBindingCoverage);
        Assert.Equal(a.UsageEvidence.Length, logical.SatisfiableUsageCount + logical.ImpossibleUsageCount + logical.UnknownUsageCount);
        if (logical.SatisfiableUsageCount == 0) Assert.Contains("NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL", a.BoundedProof.Blockers);
        else Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", logical.State);
        if (physical.SatisfiableRelationCount == 0) Assert.Contains("NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE", a.BoundedProof.Blockers);
        if (proof == "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE") {
            Assert.Equal(1, physical.OrdinalBindingRelationCount); Assert.Equal("COMPLETE_ORDINAL_OBSERVED_BINDINGS", physical.State);
        }
        if (chart.Contains("KICK.WAV", StringComparison.Ordinal)) {
            Assert.Equal(1, physical.ObservedBindingRelationCount); Assert.Equal(0, physical.OrdinalBindingRelationCount);
            Assert.Equal("WINDOWS_QUALIFIED_OBSERVED_MATCH", Assert.Single(physical.Declarations).State);
            Assert.DoesNotContain("NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE", a.BoundedProof.Blockers);
        }
    }
    [Theory]
    [InlineData("kick.wav", "ORDINAL_EXACT_OBSERVED_MATCH", "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE")]
    [InlineData("KICK.WAV", "WINDOWS_QUALIFIED_OBSERVED_MATCH", "NOT_PROVEN")]
    [InlineData("missing.wav", "NO_OBSERVED_MATCH", "NOT_PROVEN")]
    [InlineData("../kick.wav", "OUT_OF_SCOPE", "NOT_PROVEN")]
    [InlineData("\"kick.wav\"", "UNKNOWN_OR_UNSUPPORTED", "NOT_PROVEN")]
    [InlineData("kick", "ALTERNATE_EXTENSION_PROPOSALS_ONLY", "NOT_PROVEN")]
    public void PerDeclarationCoverageKeepsLexicalPoliciesAndNeverUsesHashSubstitution(string path, string state, string proof)
    {
        var a = PhaseFourSemanticTests.Analyze("#WAV01 " + path + "\n#00111:01", files: ["kick.wav"]);
        Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", a.LogicalReferenceCoverage!.State);
        Assert.Equal(state, Assert.Single(a.PhysicalResourceBindingCoverage!.Declarations).State);
        Assert.Equal(1, a.PhysicalResourceBindingCoverage.SatisfiableRelationCount);
        Assert.Equal(proof, a.BoundedProof.State);
        if (path == "KICK.WAV") {
            Assert.Equal("WINDOWS_ORDINAL_IGNORE_CASE", a.PhysicalResourceBindingCoverage.Declarations[0].PathPolicy);
            Assert.Single(a.PhysicalCandidates); Assert.Single(Assert.Single(a.Relations).PhysicalCandidateIds);
        }
        if (path == "missing.wav") Assert.Empty(a.PhysicalCandidates);
    }
    [Fact]
    public void CaseCollisionKeepsEvenTheExactCandidateAndBlocksOrdinalProof()
    {
        // Model distinct entries; ordinary Windows directories need not support
        // these two spellings as separate physical files.
        var a = PhaseFourSemanticTests.Analyze("#WAV01 kick.wav\n#00111:01", files: ["kick.wav", "KICK.WAV"]);
        Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", a.LogicalReferenceCoverage!.State);
        Assert.Equal("AMBIGUOUS_OBSERVED_BINDINGS", a.PhysicalResourceBindingCoverage!.State);
        Assert.Equal("AMBIGUOUS_OBSERVED_MATCH", Assert.Single(a.PhysicalResourceBindingCoverage.Declarations).State);
        Assert.Equal(2, a.PhysicalResourceBindingCoverage.Declarations[0].CandidateCount);
        Assert.Equal(2, Assert.Single(a.Relations).PhysicalCandidateIds.Length);
        Assert.Equal(new[] { "kick.wav", "KICK.WAV" }, a.PhysicalCandidates.Select(c => c.Evidence.RelativePath));
        Assert.Equal(0, a.PhysicalResourceBindingCoverage.OrdinalBindingRelationCount);
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        Assert.Single(a.ConservationObligations);
    }
    [Fact]
    public void BmsonAudioProposalsAreSeparateFromOrdinalBindingsAndCodecProof()
    {
        const string chart = "{\"version\":\"1.0.0\",\"sound_channels\":[{\"name\":\"stem\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false}]}]}";
        var a = PhaseFourSemanticTests.Analyze(chart, ".bmson", files: ["stem.ogg", "stem.m4a"]);
        Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", a.LogicalReferenceCoverage!.State);
        var physical = a.PhysicalResourceBindingCoverage!;
        Assert.Equal("BMSON_SEARCH_PROPOSALS_ONLY", Assert.Single(physical.Declarations).State);
        Assert.Equal(2, physical.Declarations[0].BmsonSearchCandidateCount);
        Assert.Equal(0, physical.OrdinalBindingRelationCount); Assert.Equal(0, physical.ObservedBindingRelationCount);
        var lookup = Assert.Single(a.BmsonAudioLookups); Assert.Equal(2, lookup.Candidates.Length); Assert.False(lookup.CodecCompatibilityProven);
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
    }
    [Fact]
    public void UnknownDuplicateAndLimitedEvidenceCannotSupplyPhysicalProof()
    {
        var unknown = PhaseFourSemanticTests.Analyze("#WAV01 kick.wav\n#IFDEF PLAYER\n#00111:01\n#ENDIF", files: ["kick.wav"]);
        Assert.Equal(1, unknown.LogicalReferenceCoverage!.UnknownUsageCount);
        Assert.Equal(0, unknown.LogicalReferenceCoverage.SatisfiableUsageCount);
        Assert.Single(unknown.PhysicalCandidates); Assert.Single(unknown.ConservationObligations);
        Assert.Equal("NOT_PROVEN", unknown.BoundedProof.State);
        var duplicate = PhaseFourSemanticTests.Analyze("#WAV01 kick.wav\n#WAV01 kick.wav\n#00111:01", files: ["kick.wav"]);
        Assert.Equal("INCOMPLETE_OR_AMBIGUOUS", duplicate.LogicalReferenceCoverage!.State);
        Assert.Equal(2, duplicate.PhysicalResourceBindingCoverage!.SatisfiableRelationCount);
        Assert.Equal(0, duplicate.PhysicalResourceBindingCoverage.OrdinalBindingRelationCount);
        Assert.Equal("NOT_PROVEN", duplicate.BoundedProof.State); Assert.Equal(2, duplicate.ConservationObligations.Length);
        foreach (var limits in new[] { new PhaseFourLimits(MaximumCandidatesPerReference: 1), new PhaseFourLimits(MaximumSemanticRelations: 1), new PhaseFourLimits(MaximumSemanticSymbolicTerms: 1) }) {
            var a = PhaseFourSemanticTests.Analyze("#WAV01 kick.wav\n#00111:0101", limits: limits, files: ["kick.wav", "KICK.WAV"]);
            Assert.Equal("NOT_PROVEN", a.BoundedProof.State); Assert.Equal("INCOMPLETE", a.PhysicalResourceBindingCoverage!.State);
            Assert.Single(a.ConservationObligations); Assert.Equal(2, a.UsageEvidence.Length); Assert.NotEmpty(a.PhysicalCandidates);
        }
    }
    [WindowsPhaseTwoTheory]
    [InlineData("#WAV01 kick.wav\n#00111:01", "LEXICAL_ORDINAL", 1, "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE")]
    [InlineData("#WAV01 KICK.WAV\n#00111:01", "WINDOWS_ORDINAL_IGNORE_CASE", 0, "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01", "NONE", 0, "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#WAV01 kick.wav", "LEXICAL_ORDINAL", 0, "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#RANDOM 2\n#IF 3\n#WAV01 kick.wav\n#00211:01\n#ENDIF\n#ENDRANDOM", "LEXICAL_ORDINAL", 0, "NOT_PROVEN")]
    [InlineData("#BPM01 150\n#00108:01\n#WAV01 kick.wav\n#00111:01", "LEXICAL_ORDINAL", 1, "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE")]
    public void SealedPipelineKeepsPolicyTimingAndSourceEvidenceWithoutPayloadOpens(string chart, string policy, int ordinalRelations, string proof)
    {
        using var e = new Evidence(chart); e.Seal();
        var inputs = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Concat([e.F.Source, e.F.Db, e.Report])
            .ToDictionary(p => p, p => (Hash: SHA256.HashData(File.ReadAllBytes(p)), Facts: PhaseTwoSafety.Inspect(p)));
        var held = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        PhaseFourResult result;
        try { result = e.Resolve(); e.Resolve("repeat"); } finally { foreach (var handle in held) handle.Dispose(); }
        var a = result.Semantics!; Assert.Equal(proof, a.BoundedProof.State);
        Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", a.LogicalReferenceCoverage!.State);
        Assert.Equal(ordinalRelations, a.PhysicalResourceBindingCoverage!.OrdinalBindingRelationCount);
        Assert.Equal(e.Content.ManifestSha256, result.Provenance.ContentDigest);
        Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        var resources = result.Declarations.Where(d => d.WrittenPath is not null).ToArray();
        if (resources.Length == 0) {
            Assert.Equal("NO_PHYSICAL_RESOURCE_DECLARATIONS_IN_SCOPE", a.PhysicalResourceBindingCoverage.State);
            Assert.Equal(0, a.PhysicalResourceBindingCoverage.SatisfiableRelationCount);
            Assert.Empty(a.PhysicalCandidates); Assert.Contains("NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE", a.BoundedProof.Blockers);
        } else {
            var d = Assert.Single(resources); Assert.Equal(policy, d.PathPolicy); Assert.Equal("OBSERVED_PATH_MATCH", d.ResolutionState);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("known audio A"))), Assert.Single(d.Candidates).Sha256);
            if (ordinalRelations == 0 && policy == "LEXICAL_ORDINAL") Assert.Contains("NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE", a.BoundedProof.Blockers);
        }
        if (chart.Contains("#BPM01", StringComparison.Ordinal)) Assert.Contains(result.VerifiedSyntax.Declarations, d => d.Namespace == "BPM" && d.WrittenPath is null);
        else Assert.Equal(0, result.ExitCode); // Windows match remains successful observed analysis.
        Assert.Equal(File.ReadAllBytes(Path.Combine(e.F.Base, "resolved", "dependency_resolution.json")), File.ReadAllBytes(Path.Combine(e.F.Base, "repeat", "dependency_resolution.json")));
        foreach (var (path, before) in inputs) { Assert.Equal(before.Hash, SHA256.HashData(File.ReadAllBytes(path))); Assert.Equal(before.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var db in new[] { e.F.Source, e.F.Db }) foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(File.Exists(db + suffix));
    }
    [WindowsPhaseTwoFact]
    public void RootMembershipAndSameHashDifferentNamesStayIndependent()
    {
        using var e = new Evidence("#WAV01 a.wav\n#WAV02 b.wav\n#00111:0102");
        foreach (var name in new[] { "a.wav", "b.wav" }) { e.F.File("A", name, "root A bytes"); e.F.File("B", name, "root B bytes"); }
        e.Seal(); PhaseThreeInspector.Inspect(e.F.Path("B", "one.BMS"), e.F.Root, Path.Combine(e.F.Base, "syntaxB"));
        var a = e.Resolve(); var b = PhaseFourResolver.Resolve(e.Options("resolvedB", "B", Path.Combine(e.F.Base, "syntaxB", "chart_inspection.json")));
        foreach (var result in new[] { a, b }) {
            Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", result.Semantics!.BoundedProof.State);
            Assert.Equal(2, result.Semantics.PhysicalResourceBindingCoverage!.OrdinalBindingRelationCount);
            var candidates = result.Declarations.Select(d => Assert.Single(d.Candidates)).ToArray();
            Assert.Equal(candidates[0].Sha256, candidates[1].Sha256); Assert.NotEqual(candidates[0].PhysicalFileId, candidates[1].PhysicalFileId);
            Assert.NotEqual(candidates[0].RelativePath, candidates[1].RelativePath);
            Assert.All(candidates, c => Assert.Equal(result.Provenance.ObservationRootId, c.ObservationRootId));
            Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        }
        Assert.NotEqual(a.Declarations[0].Candidates[0].Sha256, b.Declarations[0].Candidates[0].Sha256);
        Assert.Throws<InvalidOperationException>(() => e.Resolve("wrong-root", "B"));
    }
    [WindowsPhaseTwoFact]
    public void AdditiveCoverageReadsOldV2WithoutInventingNewOrdinalEvidence()
    {
        using var e = new Evidence("#WAV01 KICK.WAV\n#00111:01"); e.Seal(); var current = e.Resolve();
        var text = File.ReadAllText(Path.Combine(e.F.Base, "resolved", "dependency_resolution.json"));
        var reread = JsonSerializer.Deserialize<PhaseFourResult>(text, PhaseFourResolver.Json)!;
        Assert.Equal("phase4-dependency-2", reread.ResultVersion); Assert.Equal("phase4-semantic-graph-2", reread.Semantics!.Version);
        Assert.Equal("QUALIFIED_WINDOWS_OBSERVED_BINDINGS", reread.Semantics.PhysicalResourceBindingCoverage!.State);
        Assert.Equal("NOT_PROVEN", reread.Semantics.BoundedProof.State); Assert.Equal(0, reread.ExitCode);
        // Model an old v2 shape; this is structural compatibility, not historical
        // seal authenticity or promotion of its older proof to the new contract.
        var old = JsonNode.Parse(text)!; var semantics = old["semantics"]!.AsObject();
        semantics.Remove("logical_reference_coverage"); semantics.Remove("physical_resource_binding_coverage");
        semantics["version"] = "phase4-semantic-graph-1";
        semantics["bounded_proof"]!["version"] = "observed-logical-dependency-evidence-1";
        semantics["bounded_proof"]!["state"] = "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE";
        var legacy = JsonSerializer.Deserialize<PhaseFourResult>(old.ToJsonString(), PhaseFourResolver.Json)!;
        Assert.Null(legacy.Semantics!.LogicalReferenceCoverage); Assert.Null(legacy.Semantics.PhysicalResourceBindingCoverage);
        Assert.Equal("phase4-semantic-graph-1", legacy.Semantics.Version);
        Assert.Equal("observed-logical-dependency-evidence-1", legacy.Semantics.BoundedProof.Version);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", legacy.Semantics.BoundedProof.State);
        Assert.Equal("WINDOWS_ORDINAL_IGNORE_CASE", legacy.Declarations[0].PathPolicy);
        Assert.Equal(current.Declarations[0].Candidates[0].Sha256, legacy.Declarations[0].Candidates[0].Sha256);
        Assert.False(legacy.DependencyClosureProven); Assert.False(legacy.RemovalAuthority);
    }
}
