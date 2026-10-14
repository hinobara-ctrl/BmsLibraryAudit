using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BmsLibraryAudit.Core;
using Xunit;
using Evidence = BmsLibraryAudit.Tests.PhaseFourTests.Evidence;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseFourSemanticTests
{
    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static PhaseFourSemanticAnalysis Analyze(string chart, string extension = ".bms", PhaseFourLimits? limits = null, string[]? files = null)
    {
        limits ??= new(); var syntax = PhaseThreeParser.Parse(Encoding.UTF8.GetBytes(chart), extension);
        var inventory = (files ?? ["a.wav", "b.wav", "default.wav", "image.png"]).Select((name, index) =>
            new ObservedResourceCandidate(index + 1, index + 1, 1, 1, name.ToUpperInvariant(), name, name, "native-" + index, Sha(name), name.Length, "")).ToArray();
        var paths = new PhaseFourPaths(inventory, limits); var conditions = new PhaseFourConditions(syntax, limits, default);
        var resolved = syntax.Declarations.Select(d => {
            var m = paths.Resolve("", d.WrittenPath);
            return new ResolvedResourceDeclaration(d.Id, d.WrittenPath, "", m.RelativePath, m.Policy, m.State, "LEGACY", conditions.For(d.Source.DecodingId, d.ConditionalContextIds), [], m.Candidates, m.Limitations);
        }).ToArray();
        return PhaseFourSemantics.Analyze(syntax, resolved, paths, limits, default);
    }
    [Theory]
    [InlineData("#WAV01 a.wav", 0)]
    [InlineData("#WAV01 a.wav\n#RANDOM 2\n#IF 3\n#00111:01\n#ENDIF\n#ENDRANDOM", 1)]
    [InlineData("#WAV01 a.wav\n#RANDOM 2\n#IF 3\n#00111:01\n#ELSEIF 4\n#00211:01\n#ENDIF\n#ENDRANDOM", 2)]
    [InlineData("#WAV01 a.wav\n#RANDOM 2\n#IF 3\n#RANDOM 2\n#IF 1\n#00111:01\n#ENDIF\n#ENDRANDOM\n#ENDIF\n#IF 1\n#RANDOM 2\n#IF 3\n#00211:01\n#ENDIF\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM", 2)]
    [InlineData("#WAV01 a.wav\n#SETRANDOM 2\n#IF 1\n#00111:01\n#ENDIF\n#ENDRANDOM", 1)]
    [InlineData("#WAV01 a.wav\n#SWITCH 2\n#CASE 3\n#00111:01\n#SKIP\n#DEF\n#ENDSW", 1)]
    [InlineData("#WAV01 a.wav\n#SETSWITCH 2\n#CASE 1\n#00111:01\n#SKIP\n#DEF\n#ENDSW", 1)]
    [InlineData("#WAV01 a.wav\n#SWITCH 2\n#CASE 1\n#SKIP\n#00111:01\n#DEF\n#ENDSW", 1)]
    public void NoSatisfiableUsageCannotPromoteVacuousCoverageToPositiveBindingProof(string chart, int usages)
    {
        var a = Analyze(chart);
        Assert.Equal("COMPLETE_WITHIN_ADMITTED_MODEL", a.ConditionalModelCoverage);
        Assert.Equal("COMPLETE", a.RelationEnumerationCoverage);
        Assert.Equal("UNIQUE_CANDIDATE_COVERAGE_WITHIN_MODEL", a.EffectiveDeclarationCoverage);
        Assert.Equal("UNIQUE_OBSERVED_LITERAL_BINDINGS", a.PhysicalBindingCoverage);
        Assert.Equal(usages, a.UsageEvidence.Length); Assert.Equal(usages, a.Relations.Length);
        Assert.All(a.UsageEvidence, u => {
            Assert.Equal("USAGE_IMPOSSIBLE_WITHIN_MODEL", u.DeclarationCoverage);
            Assert.Equal("NO_COMPATIBLE_DECLARATION_WITHIN_MODEL", u.EffectiveDeclarationState);
        });
        Assert.All(a.Relations, r => {
            Assert.Equal("IMPOSSIBLE_WITHIN_MODEL", r.UsageCondition.State);
            Assert.Equal("CANNOT_CO_OCCUR_WITHIN_MODEL", r.Compatibility);
            Assert.Single(r.PhysicalCandidateIds);
        });
        Assert.Single(a.PhysicalCandidates); Assert.Equal(Sha("a.wav"), a.PhysicalCandidates[0].Evidence.Sha256);
        Assert.Single(Assert.Single(a.ConservationObligations).PhysicalCandidateIds);
        Assert.Equal("NO_CO_OCCURRING_USE_OBSERVED_WITHIN_MODEL", a.ConservationObligations[0].State);
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        Assert.Contains("NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL", a.BoundedProof.Blockers);
        if (usages == 0) Assert.Contains(a.BoundedProof.Blockers, b => b.StartsWith("No usage bindings", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("#WAV01 a.wav\n#00111:01\n#RANDOM 2\n#IF 3\n#00211:01\n#ENDIF\n#ENDRANDOM")]
    [InlineData("#WAV01 a.wav\n#SETRANDOM 2\n#IF 2\n#00111:01\n#ENDIF\n#IF 1\n#00211:01\n#ENDIF\n#ENDRANDOM")]
    [InlineData("#WAV01 a.wav\n#SWITCH 2\n#CASE 1\n#00111:01\n#SKIP\n#CASE 3\n#00211:01\n#SKIP\n#DEF\n#ENDSW")]
    public void CoveredSatisfiableUsageStillProvesItsScopeWithImpossibleUsageRetained(string chart)
    {
        var a = Analyze(chart);
        Assert.Equal(2, a.Relations.Length); Assert.Equal(2, a.UsageEvidence.Length);
        Assert.Contains(a.Relations, r => r.UsageCondition.State is "UNCONDITIONAL_SYNTAX" or "POSSIBLE_WITHIN_MODEL");
        Assert.Contains(a.Relations, r => r.UsageCondition.State == "IMPOSSIBLE_WITHIN_MODEL");
        Assert.Equal("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", a.UsageEvidence[0].DeclarationCoverage);
        Assert.Equal("USAGE_IMPOSSIBLE_WITHIN_MODEL", a.UsageEvidence[1].DeclarationCoverage);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
        Assert.Empty(a.BoundedProof.Blockers); Assert.Single(a.ConservationObligations); Assert.Single(a.PhysicalCandidates);
    }
    [Fact]
    public void SatisfiableUsageDoesNotReplaceDeclarationCoverage()
    {
        var a = Analyze("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#ENDIF\n#ENDRANDOM\n#00111:01");
        Assert.Equal("UNCONDITIONAL_SYNTAX", Assert.Single(a.Relations).UsageCondition.State);
        var use = Assert.Single(a.UsageEvidence);
        Assert.Equal("UNBOUND_ASSIGNMENTS_WITHIN_MODEL", use.DeclarationCoverage);
        Assert.Equal(new SymbolicInterval(2, 2), Assert.Single(Assert.Single(use.UnboundWitness!).Values));
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        Assert.DoesNotContain("NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL", a.BoundedProof.Blockers);
    }
    [Theory]
    [InlineData("#WAV01 a.wav\n#IFDEF PLAYER\n#00111:01\n#ENDIF")]
    [InlineData("#WAV01 a.wav\n#SWITCH 2\n#CASE 1\n#00111:01\n#CASE 2\n#00211:01\n#ENDSW")]
    public void UnknownUsageDoesNotEstablishSatisfiableExistence(string chart)
    {
        var a = Analyze(chart);
        Assert.All(a.Relations, r => Assert.Equal("UNKNOWN", r.UsageCondition.State));
        Assert.Equal("INCOMPLETE", a.ConditionalModelCoverage); Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        Assert.Contains("NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL", a.BoundedProof.Blockers);
        Assert.Single(a.PhysicalCandidates); Assert.Single(a.ConservationObligations);
    }
    [Fact]
    public void BmsonUnconditionalUsageKeepsItsOwnModelAndAudioUncertainty()
    {
        const string chart = "{\"version\":\"1.0.0\",\"sound_channels\":[{\"name\":\"a.wav\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false}]}]}";
        var a = Analyze(chart, ".bmson", files: ["a.wav", "a.ogg"]);
        Assert.Contains(PhaseFourConditions.BmsonModel, a.Models); Assert.DoesNotContain(PhaseFourConditions.Model, a.Models);
        Assert.Equal("UNCONDITIONAL_SYNTAX", Assert.Single(a.Relations).UsageCondition.State);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
        var lookup = Assert.Single(a.BmsonAudioLookups); Assert.Equal(2, lookup.Candidates.Length);
        Assert.False(lookup.CodecCompatibilityProven); Assert.Empty(a.DomainConstraints);
    }
    [WindowsPhaseTwoFact]
    public void SatisfiableUsageWithReducedBudgetsRetainsOriginalEvidenceWithoutProof()
    {
        using var e = new Evidence("#RANDOM 2\n#IF 1\n#WAV01 kick.wav\n#ENDIF\n#IF 2\n#WAV01 kick.wav\n#ENDIF\n#ENDRANDOM\n#00111:0101"); e.Seal();
        var index = 0;
        foreach (var limits in new[] { new PhaseFourLimits(MaximumSemanticRelations: 1), new PhaseFourLimits(MaximumSemanticSymbolicTerms: 1) }) {
            var result = e.Resolve("limited-" + index++, limits: limits); var a = result.Semantics!;
            Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
            Assert.True(a.ConditionalModelCoverage == "INCOMPLETE" || a.RelationEnumerationCoverage == "INCOMPLETE");
            Assert.Equal(2, result.VerifiedSyntax.Declarations.Length); Assert.Equal(2, result.VerifiedSyntax.Usages.Length);
            Assert.Equal(2, result.Declarations.Length); Assert.Equal(2, a.ConservationObligations.Length);
            Assert.All(result.Declarations, d => Assert.Equal(Sha("known audio A"), Assert.Single(d.Candidates).Sha256));
            Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        }
    }
    [WindowsPhaseTwoFact]
    public void ImpossibleUsageEndToEndRetainsSealedPhysicalEvidenceWithoutResourceOpens()
    {
        const string chart = "#WAV01 a.wav\n#RANDOM 2\n#IF 3\n#00111:01\n#ENDIF\n#ENDRANDOM";
        const string payload = "independent impossible-usage fixture payload";
        using var e = new Evidence(chart); e.F.File("A", "a.wav", payload);
        var originals = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, p => (Sha: Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))), Facts: PhaseTwoSafety.Inspect(p)));
        e.Seal();
        var inputs = new[] { e.F.Source, e.F.Db, e.Report }
            .ToDictionary(p => p, p => (Sha: Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))), Facts: PhaseTwoSafety.Inspect(p)));
        var held = originals.Keys.Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        PhaseFourResult result;
        try { result = e.Resolve(); e.Resolve("repeat"); } finally { foreach (var stream in held) stream.Dispose(); }
        var a = Assert.IsType<PhaseFourSemanticAnalysis>(result.Semantics);
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        Assert.Contains("NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL", a.BoundedProof.Blockers);
        Assert.Equal("USAGE_IMPOSSIBLE_WITHIN_MODEL", Assert.Single(a.UsageEvidence).DeclarationCoverage);
        Assert.Equal("IMPOSSIBLE_WITHIN_MODEL", Assert.Single(a.Relations).UsageCondition.State);
        Assert.Equal("OBSERVED_PATH_MATCH", Assert.Single(result.Declarations).ResolutionState);
        Assert.Equal(Sha(payload), Assert.Single(result.Declarations[0].Candidates).Sha256);
        Assert.Equal(Sha(payload), Assert.Single(a.PhysicalCandidates).Evidence.Sha256);
        Assert.Single(Assert.Single(a.ConservationObligations).PhysicalCandidateIds);
        Assert.Equal(Sha(chart), result.Provenance.ChartSha256); Assert.Equal(e.Content.ManifestSha256, result.Provenance.ContentDigest);
        Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        Assert.Equal(File.ReadAllBytes(Path.Combine(e.F.Base, "resolved", "dependency_resolution.json")), File.ReadAllBytes(Path.Combine(e.F.Base, "repeat", "dependency_resolution.json")));
        foreach (var (path, before) in originals.Concat(inputs)) {
            Assert.Equal(before.Sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
            Assert.Equal(before.Facts, PhaseTwoSafety.Inspect(path));
        }
        foreach (var db in new[] { e.F.Source, e.F.Db }) foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(File.Exists(db + suffix));
    }
    [Fact]
    public void SanitizedV1ReportRetainsLegacyFieldsAndDefaultAdditiveLimits()
    {
        // Derived from a pre-v2 owned smoke report, sanitized for deserialization
        // only. Placeholder paths/identities/digests do not establish valid seals.
        using var input = typeof(PhaseFourSemanticTests).Assembly.GetManifestResourceStream("BmsLibraryAudit.Tests.Fixtures.phase4-v1-synthetic.json")!;
        using var bytes = new MemoryStream(); input.CopyTo(bytes);
        Assert.Equal("33f790fca0285be16ff7224cb2f24494cf753386cc6cc339708ae463ef921bb8", Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())));
        var text = Encoding.UTF8.GetString(bytes.ToArray());
        Assert.DoesNotMatch(@"(?i)(?:[a-z]:[\\/]+Users[\\/]|OneDrive|Escritorio|AppData|sk-[a-z0-9]{20,}|gh[pousr]_[a-z0-9]{20,}|github_pat_|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY)", text);
        using var json = JsonDocument.Parse(bytes.ToArray());
        var legacy = JsonSerializer.Deserialize<PhaseFourResult>(bytes.ToArray(), PhaseFourResolver.Json)!;
        Assert.Equal("phase4-conservative-1", legacy.ResultVersion); Assert.Null(legacy.Semantics); Assert.Equal(0, legacy.ExitCode);
        Assert.False(legacy.DependencyClosureProven); Assert.False(legacy.RemovalAuthority);
        Assert.Contains("Sanitized deserialization-only v1 fixture", legacy.Notice);
        Assert.Contains("seals are not valid evidence", legacy.Notice);
        Assert.Equal(new string('1', 64), legacy.Provenance.ContentDigest);
        Assert.Equal(new string('2', 64), legacy.Provenance.PreflightDigest);
        Assert.Equal(new string('3', 64), legacy.Provenance.ComponentKey);
        Assert.Equal("2000-01-01T00:00:00.0000000+00:00", legacy.Provenance.ContentStartedUtc);
        const string root = @"C:\BmsLibraryAuditSynthetic\phase4-v1\library";
        Assert.Equal(root, legacy.Provenance.SourceLibraryRoot);
        Assert.Equal(root + @"\A", legacy.Provenance.ObservationRootPath);
        Assert.Equal(legacy.Provenance.ObservationRootPath.ToUpperInvariant(), legacy.Provenance.ObservationRootKey);
        Assert.Equal(legacy.Provenance.ObservationRootPath, legacy.Provenance.ChartParent);
        Assert.All(legacy.Declarations.SelectMany(d => d.Candidates).Prepend(legacy.Provenance.Chart), c => {
            Assert.StartsWith(root + @"\A\", c.OriginalPath);
            Assert.Equal(c.OriginalPath.ToUpperInvariant(), c.EntryKey);
            Assert.StartsWith("00000000:", c.NativeIdentity);
        });
        Assert.Equal(json.RootElement.GetProperty("provenance").GetProperty("chart_sha256").GetString(), legacy.Provenance.ChartSha256);
        Assert.Equal(4, legacy.Declarations.Length); Assert.Equal(3, legacy.Usages.Length);
        Assert.Equal("NO_OBSERVED_MATCH", legacy.Declarations[3].ResolutionState);
        Assert.Equal(legacy.Declarations[0].Candidates[0].Sha256, legacy.Declarations[1].Candidates[0].Sha256);
        Assert.NotEqual(legacy.Declarations[0].Candidates[0].PhysicalFileId, legacy.Declarations[1].Candidates[0].PhysicalFileId);
        Assert.Equal(new PhaseFourLimits().MaximumSemanticRelations, legacy.Limits.MaximumSemanticRelations);
    }
    [Theory]
    [InlineData("#WAV01 a.wav\n#00111:01", "MAY_BIND_WITHIN_MODEL", "DECLARATIONS_COVER_USAGE_WITHIN_MODEL")]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#ENDIF\n#ENDRANDOM\n#00111:01", "MAY_BIND_WITHIN_MODEL", "UNBOUND_ASSIGNMENTS_WITHIN_MODEL")]
    [InlineData("#WAV01 a.wav\n#RANDOM 2\n#IF 1\n#00111:01\n#ENDIF\n#ENDRANDOM", "MAY_BIND_WITHIN_MODEL", "DECLARATIONS_COVER_USAGE_WITHIN_MODEL")]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#00111:01\n#ENDIF\n#ENDRANDOM", "MAY_BIND_WITHIN_MODEL", "DECLARATIONS_COVER_USAGE_WITHIN_MODEL")]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#ENDIF\n#IF 2\n#00111:01\n#ENDIF\n#ENDRANDOM", "CANNOT_CO_OCCUR_WITHIN_MODEL", "UNBOUND_ASSIGNMENTS_WITHIN_MODEL")]
    public void JointCompatibilityAndUsageCoverageAreSeparate(string chart, string compatibility, string coverage)
    {
        var analysis = Analyze(chart); var relation = Assert.Single(analysis.Relations);
        Assert.Equal(compatibility, relation.Compatibility); Assert.Equal(coverage, Assert.Single(analysis.UsageEvidence).DeclarationCoverage);
        Assert.Single(relation.PhysicalCandidateIds); Assert.Single(analysis.ConservationObligations);
        if (coverage == "UNBOUND_ASSIGNMENTS_WITHIN_MODEL")
        {
            var witness = Assert.Single(analysis.UsageEvidence[0].UnboundWitness!);
            Assert.Equal(0, witness.RandomContextId); Assert.Equal(new SymbolicInterval(2, 2), Assert.Single(witness.Values));
            Assert.Equal("NOT_PROVEN", analysis.BoundedProof.State);
        }
        else Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", analysis.BoundedProof.State);
    }
    [Theory]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#ENDIF\n#IF 2\n#WAV01 b.wav\n#ENDIF\n#ENDRANDOM\n#00111:01", "BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL", "CANNOT_CO_OCCUR_WITHIN_MODEL", "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE")]
    [InlineData("#WAV01 a.wav\n#WAV01 b.wav\n#00111:01", "AMBIGUOUS_EFFECTIVE_DECLARATION", "SIMULTANEOUS_DECLARATIONS_PRECEDENCE_UNKNOWN", "NOT_PROVEN")]
    [InlineData("#WAV01 default.wav\n#RANDOM 2\n#IF 1\n#WAV01 b.wav\n#ENDIF\n#ENDRANDOM\n#00111:01", "AMBIGUOUS_EFFECTIVE_DECLARATION", "POSSIBLE_CONDITIONAL_REDEFINITION_PRECEDENCE_UNKNOWN", "NOT_PROVEN")]
    public void AlternativesCoexistenceAndRedefinitionNeverSelectPrecedence(string chart, string effective, string coexistence, string proof)
    {
        var a = Analyze(chart); Assert.Equal(2, a.Relations.Length); Assert.Equal(2, a.ConservationObligations.Length);
        Assert.Equal(effective, Assert.Single(a.UsageEvidence).EffectiveDeclarationState); Assert.Equal(coexistence, Assert.Single(a.Coexistences).State);
        Assert.Equal(proof, a.BoundedProof.State); Assert.All(a.Relations, r => Assert.Single(r.PhysicalCandidateIds));
    }
    [Fact]
    public void RedefinitionOutsideTheUsageBranchDoesNotInventAmbiguity()
    {
        var a = Analyze("#WAV01 default.wav\n#RANDOM 2\n#IF 1\n#WAV01 b.wav\n#ENDIF\n#IF 2\n#00111:01\n#ENDIF\n#ENDRANDOM");
        Assert.Equal("SINGLE_CANDIDATE_WITHIN_MODEL", Assert.Single(a.UsageEvidence).EffectiveDeclarationState);
        Assert.Equal("MAY_BIND_WITHIN_MODEL", a.Relations[0].Compatibility); Assert.Equal("CANNOT_CO_OCCUR_WITHIN_MODEL", a.Relations[1].Compatibility);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
    }
    [Fact]
    public void TextAfterUseIsRetainedAndNamespacesNeverCrossBind()
    {
        var a = Analyze("#00111:01\n#WAV01 a.wav\n#BMP01 image.png\n#00104:01");
        Assert.Equal(2, a.Relations.Length); Assert.Equal("DECLARATION_AFTER_USAGE_IN_TEXT", a.Relations[0].TextualOrder);
        Assert.Equal("WAV", a.Relations[0].Namespace); Assert.Equal(0, a.Relations[0].DeclarationId);
        Assert.Equal("BMP", a.Relations[1].Namespace); Assert.Equal(1, a.Relations[1].DeclarationId);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
        var missing = Analyze("#00111:02"); Assert.Equal("UNRESOLVED_LOGICAL_REFERENCE", Assert.Single(missing.Relations).Compatibility);
        Assert.Null(missing.Relations[0].DeclarationId); Assert.Equal("NOT_PROVEN", missing.BoundedProof.State);
    }
    [Fact]
    public void FixedRandomIsQualifiedSeparatelyAndLegacyUnsupportedFieldsRemainUnchanged()
    {
        var bytes = Encoding.UTF8.GetBytes("#SETRANDOM 2\n#IF 1\n#WAV01 a.wav\n#00111:01\n#ENDIF\n#IF 2\n#WAV01 b.wav\n#00111:01\n#ENDIF\n#ENDRANDOM");
        var syntax = PhaseThreeParser.Parse(bytes, ".bms"); var legacy = new PhaseFourConditions(syntax, new(), default);
        Assert.Equal("UNKNOWN", legacy.For(0, syntax.Declarations[0].ConditionalContextIds).State);
        var a = Analyze(Encoding.UTF8.GetString(bytes)); Assert.Contains(PhaseFourConditions.FixedModel, a.Models);
        Assert.Equal("CANNOT_CO_OCCUR_WITHIN_MODEL", a.Relations[0].Compatibility);
        Assert.Equal("MAY_BIND_WITHIN_MODEL", a.Relations[^1].Compatibility);
        Assert.Contains(a.Relations[^1].Intersection.Constraints, c => c.RandomContextId == 0 && c.EqualValue == 2);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
        var outside = Analyze("#SETRANDOM 2\n#IF 1\n#WAV01 a.wav\n#ENDIF\n#IF 2\n#WAV01 b.wav\n#ENDIF\n#ENDRANDOM\n#00111:01");
        Assert.Equal("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", Assert.Single(outside.UsageEvidence).DeclarationCoverage);
        Assert.Equal(2, Assert.Single(outside.DomainConstraints).EqualValue);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", outside.BoundedProof.State);
    }
    [Theory]
    [InlineData("SWITCH", 3, "BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL")]
    [InlineData("SETSWITCH", 2, "SINGLE_CANDIDATE_WITHIN_MODEL")]
    public void FlatSwitchWithExplicitSkipDefaultAndEndHasAQualifiedModel(string directive, int range, string effective)
    {
        var chart = $"#{directive} {range}\n#CASE 1\n#WAV01 a.wav\n#SKIP\n#CASE 2\n#WAV01 b.wav\n#SKIP\n#DEF\n#WAV01 default.wav\n#ENDSW\n#00111:01";
        var a = Analyze(chart); Assert.Contains(PhaseFourConditions.SwitchModel, a.Models);
        Assert.Equal(3, a.Relations.Length); Assert.Equal(effective, Assert.Single(a.UsageEvidence).EffectiveDeclarationState);
        Assert.Equal("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", a.UsageEvidence[0].DeclarationCoverage);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
        if (directive == "SETSWITCH") Assert.Equal(new[] { "CANNOT_CO_OCCUR_WITHIN_MODEL", "MAY_BIND_WITHIN_MODEL", "CANNOT_CO_OCCUR_WITHIN_MODEL" }, a.Relations.Select(r => r.Compatibility));
        var syntax = PhaseThreeParser.Parse(Encoding.UTF8.GetBytes(chart), ".bms");
        Assert.Equal("UNKNOWN", new PhaseFourConditions(syntax, new(), default).For(0, syntax.Declarations[0].ConditionalContextIds).State);
    }
    [Fact]
    public void SkipSuppressesLaterFactsInThatSectionAndDuplicateCaseLabelsRemainRetained()
    {
        var a = Analyze("#SWITCH 2\n#CASE 1\n#WAV01 a.wav\n#SKIP\n#WAV01 b.wav\n#CASE 1\n#WAV01 default.wav\n#SKIP\n#DEF\n#WAV01 image.png\n#ENDSW\n#00111:01");
        Assert.Equal(4, a.Relations.Length); Assert.Equal("CANNOT_CO_OCCUR_WITHIN_MODEL", a.Relations[1].Compatibility);
        Assert.Equal("CANNOT_CO_OCCUR_WITHIN_MODEL", a.Relations[2].Compatibility);
        Assert.Equal("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", Assert.Single(a.UsageEvidence).DeclarationCoverage);
        Assert.Equal(4, a.ConservationObligations.Length); Assert.All(a.ConservationObligations, o => Assert.Single(o.PhysicalCandidateIds));
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
    }
    [Theory]
    [InlineData("#SWITCH 2\n#DEF\n#WAV01 a.wav\n#SKIP\n#CASE 1\n#00111:01\n#ENDSW")]
    [InlineData("#SWITCH 2\n#CASE 1\n#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#00111:01\n#ENDIF\n#ENDRANDOM\n#ENDSW")]
    [InlineData("#SWITCH 2\n#CASE 1\n#WAV01 a.wav\n#00111:01")]
    public void SwitchFormsOutsideTheExplicitSubsetStayUnknown(string chart)
    {
        var a = Analyze(chart); Assert.Equal("UNKNOWN_SEMANTICS", Assert.Single(a.Relations).Compatibility);
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State); Assert.Single(a.ConservationObligations);
    }
    [Theory]
    [InlineData("#RANDOM 2\n#IF 1\n#RANDOM 3\n#WAV01 a.wav\n#00111:01\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM")]
    [InlineData("#RANDOM 2\n#RANDOM 3\n#IF 1\n#WAV01 a.wav\n#00111:01\n#ENDIF\n#ENDRANDOM\n#ENDRANDOM")]
    [InlineData("#SETRANDOM 2\n#WAV01 a.wav\n#00111:01\n#ENDRANDOM")]
    [InlineData("#SWITCH 2\n#CASE 1\n#WAV01 a.wav\n#CASE 2\n#00111:01\n#SKIP\n#DEF\n#ENDSW")]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#00111:01")]
    [InlineData("#SETRANDOM 0\n#IF 1\n#WAV01 a.wav\n#00111:01\n#ENDIF\n#ENDRANDOM")]
    public void UnknownScopesAndFallthroughCannotBecomeJointImpossibility(string chart)
    {
        var a = Analyze(chart); Assert.Equal("UNKNOWN_SEMANTICS", Assert.Single(a.Relations).Compatibility);
        Assert.Single(a.Relations[0].PhysicalCandidateIds); Assert.Equal("INCOMPLETE", a.ConditionalModelCoverage); Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
    }
    [Fact]
    public void OuterEofAndNestedElseCoverageRemainExact()
    {
        var a = Analyze("#00111:01\n#RANDOM 2\n#IF 1\n#RANDOM 3\n#IF 1\n#WAV01 a.wav\n#ELSE\n#WAV01 b.wav\n#ENDIF\n#ENDRANDOM\n#ELSE\n#WAV01 default.wav\n#ENDIF");
        Assert.Equal(3, a.Relations.Length); Assert.All(a.Coexistences, c => Assert.Equal("CANNOT_CO_OCCUR_WITHIN_MODEL", c.State));
        Assert.Equal("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", Assert.Single(a.UsageEvidence).DeclarationCoverage);
        Assert.Equal("PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", a.BoundedProof.State);
    }
    [Fact]
    public void IntervalSubtractionMatchesIndependentSmallValueEnumerator()
    {
        // The test oracle enumerates only nine assignments; production uses boxes.
        // All subsets of a 3x3 grid exercise union, holes, overlaps and empty sets.
        var use = new ResourceReachability("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, 3, null, []), new(1, 3, null, [])], "test domain");
        for (var mask = 0; mask < 512; mask++)
        {
            var declarations = new List<ResourceReachability>();
            for (var x = 1; x <= 3; x++) for (var y = 1; y <= 3; y++)
                if ((mask & (1 << ((x - 1) * 3 + y - 1))) != 0) declarations.Add(new("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, 3, x, []), new(1, 3, y, [])], "test cell"));
            var result = PhaseFourSymbolicCoverage.Cover(use, declarations.ToArray(), new(new()), default);
            Assert.Equal(mask == 511 ? "DECLARATIONS_COVER_USAGE_WITHIN_MODEL" : "UNBOUND_ASSIGNMENTS_WITHIN_MODEL", result.State);
            if (mask != 511)
            {
                Assert.NotNull(result.Witness);
                foreach (var xRange in result.Witness[0].Values) foreach (var yRange in result.Witness[1].Values)
                    for (var x = xRange.First; x <= xRange.Last; x++) for (var y = yRange.First; y <= yRange.Last; y++)
                        Assert.Equal(0, mask & (1 << ((x - 1) * 3 + y - 1)));
            }
        }
        var huge = new ResourceReachability("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, int.MaxValue, null, [])], "large symbolic range");
        var allButTwo = new ResourceReachability("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, int.MaxValue, null, [1, int.MaxValue])], "excluded endpoints");
        var endpoints = new[] { 1, int.MaxValue }.Select(v => new ResourceReachability("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, int.MaxValue, v, [])], "endpoint"));
        Assert.Equal("DECLARATIONS_COVER_USAGE_WITHIN_MODEL", PhaseFourSymbolicCoverage.Cover(huge, [allButTwo, .. endpoints], new(new()), default).State);
    }
    [Fact]
    public void JointConstraintsMatchIndependentEnumeratorAndUnknownDominatesContradictions()
    {
        var syntax = PhaseThreeParser.Parse(Encoding.UTF8.GetBytes("#TITLE joint test"), ".bms"); var conditions = new PhaseFourConditions(syntax, new(), default);
        for (var equalA = 0; equalA <= 4; equalA++) for (var equalB = 0; equalB <= 4; equalB++) for (var mask = 0; mask < 8; mask++)
        {
            var excluded = Enumerable.Range(1, 3).Where(v => (mask & (1 << (v - 1))) != 0).ToArray();
            var a = new ResourceReachability("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, 3, equalA == 0 ? null : equalA, excluded)], "oracle input");
            var b = new ResourceReachability("POSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, 3, equalB == 0 ? null : equalB, [])], "oracle input");
            var possible = Enumerable.Range(1, 3).Any(v => (equalA == 0 || v == equalA) && (equalB == 0 || v == equalB) && !excluded.Contains(v));
            Assert.Equal(possible ? "POSSIBLE_WITHIN_MODEL" : "IMPOSSIBLE_WITHIN_MODEL", conditions.Intersect(a, b).State);
        }
        var unknown = new ResourceReachability("UNKNOWN", PhaseFourConditions.Model, [], "unknown");
        var impossible = new ResourceReachability("IMPOSSIBLE_WITHIN_MODEL", PhaseFourConditions.Model, [new(0, 2, 3, [])], "impossible");
        Assert.Equal("UNKNOWN", conditions.Intersect(unknown, impossible).State);
    }
    [Fact]
    public void SemanticRelationCandidateAndSymbolicLimitsNeverClaimProof()
    {
        const string chart = "#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#ENDIF\n#IF 2\n#WAV01 b.wav\n#ENDIF\n#ENDRANDOM\n#00111:0101";
        foreach (var limits in new[] { new PhaseFourLimits(MaximumSemanticRelations: 1), new PhaseFourLimits(MaximumSemanticCandidateLinks: 1),
            new PhaseFourLimits(MaximumSemanticSymbolicTerms: 1), new PhaseFourLimits(MaximumReferences: 1), new PhaseFourLimits(MaximumConditionalDepth: 1) })
        {
            var a = Analyze(chart, limits: limits); Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
            Assert.Equal(2, a.ConservationObligations.Length); Assert.Equal(2, a.UsageEvidence.Length);
            Assert.True(a.RelationEnumerationCoverage == "INCOMPLETE" || a.ConditionalModelCoverage == "INCOMPLETE");
        }
    }
    [Theory]
    [InlineData("stem")]
    [InlineData("stem.wav")]
    public void BmsonAudioNameSearchKeepsObservedPotentialCodecsWithoutSelecting(string name)
    {
        var chart = "{\"version\":\"1.0.0\",\"sound_channels\":[{\"name\":\"" + name + "\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false},{\"x\":0,\"y\":240,\"l\":120,\"c\":true}]}]}";
        var a = Analyze(chart, ".bmson", files: ["stem.ogg", "stem.m4a", "stem.flac", "stem.txt"]);
        Assert.Contains(PhaseFourConditions.BmsonModel, a.Models);
        var lookup = Assert.Single(a.BmsonAudioLookups); Assert.Equal(4, lookup.Candidates.Length); Assert.False(lookup.CodecCompatibilityProven);
        Assert.Equal(2, lookup.Candidates.Count(c => c.MatchPolicy == "BMSON_RECOMMENDED_FORMAT_SEARCH_POTENTIAL"));
        Assert.Equal(2, a.Relations.Length); Assert.All(a.Relations, r => Assert.Equal("MAY_BIND_WITHIN_MODEL", r.Compatibility));
        Assert.Equal("JSON_POINTERS_NO_APPLICATION_ORDER", a.Relations[0].TextualOrder); Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        Assert.Empty(Analyze("#WAV01 " + name + "\n#00111:01", files: ["stem.ogg"]).BmsonAudioLookups);
    }
    [Fact]
    public void BmsonLiteralStillRetainsUnplayableFallbackTitleImageAndDuplicateBgaStayUncertain()
    {
        const string chart = "{\"version\":\"1.0.0\",\"info\":{\"preview_music\":\"a.wav\",\"title_image\":\"image.png\"},\"sound_channels\":[{\"name\":\"a.wav\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false}]}],\"bga\":{\"bga_header\":[{\"id\":1,\"name\":\"image.png\"},{\"id\":1,\"name\":\"other.png\"}],\"bga_events\":[{\"id\":1,\"y\":0}]}}";
        var a = Analyze(chart, ".bmson", files: ["a.wav", "a.ogg", "image.png", "other.png"]);
        Assert.Equal(2, Assert.Single(a.BmsonAudioLookups).Candidates.Length);
        Assert.Contains(a.Relations, r => r.Namespace == "BMSON_INFO" && r.LogicalId == "title_image" && r.Compatibility == "UNKNOWN_SEMANTICS");
        Assert.Contains(a.UsageEvidence, u => u.EffectiveDeclarationState == "AMBIGUOUS_EFFECTIVE_DECLARATION");
        Assert.Equal("NOT_PROVEN", a.BoundedProof.State);
        var ambiguous = Analyze("#WAV01 ソ.wav\n#00111:01", files: ["ソ.wav"]);
        Assert.Equal(new[] { (0, 0, 0), (1, 1, 1) }, ambiguous.Relations.Select(r => (r.DecodingId, r.UsageId, r.DeclarationId!.Value)));
        Assert.Equal("INCOMPLETE", ambiguous.ConditionalModelCoverage); Assert.Equal("NOT_PROVEN", ambiguous.BoundedProof.State);
    }
    [WindowsPhaseTwoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndToEndBranchesOrDuplicatesPreserveAllPhysicalObligationsAndInputs(bool duplicates)
    {
        var chart = duplicates ? "#WAV01 first.wav\n#WAV01 second.wav\n#00111:01" : "#RANDOM 2\n#IF 1\n#WAV01 first.wav\n#ENDIF\n#IF 2\n#WAV01 second.wav\n#ENDIF\n#ENDRANDOM\n#00111:01";
        using var e = new Evidence(chart); e.F.File("A", "first.wav", "first payload"); e.F.File("A", "second.wav", "second payload"); e.Seal();
        var inputs = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Concat([e.F.Source, e.F.Db, e.Report]).ToDictionary(p => p, p => (Sha: Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))), Facts: PhaseTwoSafety.Inspect(p)));
        var held = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        PhaseFourResult result;
        try { result = e.Resolve(); e.Resolve("repeat"); } finally { foreach (var stream in held) stream.Dispose(); }
        Assert.Equal("phase4-dependency-2", result.ResultVersion); var graph = Assert.IsType<PhaseFourSemanticAnalysis>(result.Semantics);
        Assert.Equal(2, graph.Relations.Length); Assert.Equal(2, graph.ConservationObligations.Length);
        Assert.All(graph.Relations, r => Assert.Equal("MAY_BIND_WITHIN_MODEL", r.Compatibility));
        var expected = new[] { Sha("first payload"), Sha("second payload") };
        for (var i = 0; i < 2; i++) Assert.Equal(expected[i], graph.PhysicalCandidates[Assert.Single(graph.Relations[i].PhysicalCandidateIds)].Evidence.Sha256);
        Assert.Equal(duplicates ? "AMBIGUOUS_EFFECTIVE_DECLARATION" : "BRANCH_EXCLUSIVE_ALTERNATIVES_WITHIN_MODEL", Assert.Single(graph.UsageEvidence).EffectiveDeclarationState);
        Assert.Equal(duplicates ? "NOT_PROVEN" : "PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE", graph.BoundedProof.State);
        Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        Assert.Equal(File.ReadAllBytes(Path.Combine(e.F.Base, "resolved", "dependency_resolution.json")), File.ReadAllBytes(Path.Combine(e.F.Base, "repeat", "dependency_resolution.json")));
        foreach (var (path, before) in inputs) { Assert.Equal(before.Sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))); Assert.Equal(before.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(File.Exists(e.F.Db + suffix));
        Assert.Throws<InvalidOperationException>(() => PhaseFourResolver.Resolve(e.Options("bad") with { AcceptedContentManifestSha256 = new string('0', 64) }));
    }
    [WindowsPhaseTwoFact]
    public void VersionedReportAndExitZeroDoNotPromoteMissingPhysicalMatchToClosure()
    {
        using var e = new Evidence("#WAV01 absent.wav\n#00111:01"); e.Seal(); var result = e.Resolve();
        Assert.Equal(0, result.ExitCode); Assert.Equal("NO_OBSERVED_MATCH", Assert.Single(result.Declarations).ResolutionState);
        Assert.Equal("NOT_PROVEN", result.Semantics!.BoundedProof.State); Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(e.F.Base, "resolved", "dependency_resolution.json")));
        Assert.Equal("phase4-dependency-2", report.RootElement.GetProperty("result_version").GetString());
        foreach (var field in new[] { "provenance", "verified_syntax", "declarations", "usages", "resolution_coverage", "conditional_coverage", "dependency_closure_proven", "removal_authority" }) Assert.True(report.RootElement.TryGetProperty(field, out _));
        // A consumer using the extended public model can still read v1's old
        // fields/default limits. Existing dynamic consumers retain those fields.
        var legacyJson = JsonNode.Parse(report.RootElement.GetRawText())!.AsObject();
        legacyJson["result_version"] = "phase4-conservative-1"; legacyJson.Remove("semantics");
        var legacyLimits = legacyJson["limits"]!.AsObject();
        foreach (var field in new[] { "maximum_semantic_relations", "maximum_semantic_candidate_links", "maximum_semantic_symbolic_terms" }) legacyLimits.Remove(field);
        var legacy = JsonSerializer.Deserialize<PhaseFourResult>(legacyJson.ToJsonString(), PhaseFourResolver.Json)!;
        Assert.Null(legacy.Semantics); Assert.Equal(0, legacy.ExitCode); Assert.Equal(result.Provenance.ChartSha256, legacy.Provenance.ChartSha256);
        Assert.Equal(result.Declarations[0].ResolutionState, legacy.Declarations[0].ResolutionState);
        Assert.Equal(new PhaseFourLimits().MaximumSemanticRelations, legacy.Limits.MaximumSemanticRelations);
    }
}
