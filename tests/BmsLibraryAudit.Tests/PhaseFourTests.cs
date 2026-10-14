using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseFourTests
{
    private sealed class Evidence : IDisposable
    {
        internal Fixture F { get; } = new();
        internal string Chart => F.Path("A", "one.BMS");
        internal string Report => Path.Combine(F.Base, "syntax", "chart_inspection.json");
        internal PhaseTwoPreflightResult Content { get; private set; } = null!;
        internal string RootKey(string root = "A") => (string)F.Rows("SELECT root_key FROM observation_roots WHERE root_key=$0", PathPolicy.Normalize(F.Path(root))).Single()[0]!;
        internal Evidence(string chart = "#WAV01 kick.wav\n#BMP01 image.png\n#00111:01\n#00104:01")
        { F.Pair(bytes: chart); F.File("A", "kick.wav", "known audio A"); F.File("B", "kick.wav", "different audio B"); F.File("A", "image.png", "known image"); }
        internal void Seal()
        {
            F.Scan(); var p = F.Run(); Content = new PhaseTwoContentRunner().Run(new(F.Db, p.SessionId, p.ManifestSha256, Path.Combine(F.Base, "content")));
            PhaseThreeInspector.Inspect(Chart, F.Root, Path.Combine(F.Base, "syntax"));
        }
        internal PhaseFourOptions Options(string label = "resolved", string root = "A", string? report = null) => new(F.Db, Content.SessionId, Content.ManifestSha256, report ?? Report, RootKey(root), Path.Combine(F.Base, label));
        internal PhaseFourResult Resolve(string label = "resolved", string root = "A", PhaseFourLimits? limits = null) => PhaseFourResolver.Resolve(Options(label, root), limits);
        public void Dispose() => F.Dispose();
    }
    private sealed record Stamp(string Sha256, NativePathFacts Facts);
    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static Dictionary<string, Stamp> Snapshot(Evidence e) => SnapshotPaths(Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Concat([e.F.Source, e.F.Db, e.Report]));
    private static Dictionary<string, Stamp> SnapshotPaths(IEnumerable<string> paths) => paths
        .ToDictionary(p => p, p => new Stamp(Sha(File.ReadAllBytes(p)), PhaseTwoSafety.Inspect(p)));
    private static void Preserved(Dictionary<string, Stamp> snapshot)
    {
        foreach (var (path, stamp) in snapshot) { Assert.Equal(stamp.Sha256, Sha(File.ReadAllBytes(path))); Assert.Equal(stamp.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var path in snapshot.Keys.Where(p => p.EndsWith(".db", StringComparison.Ordinal)))
            foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(File.Exists(path + suffix));
    }
    [WindowsPhaseTwoFact]
    public void EndToEndIndependentDigestsNamespacesMultiplicityAndReadOnlyEvidence()
    {
        using var e = new Evidence("#WAV01 kick.wav\n#WAV02 copy.wav\n#BMP01 image.png\n#WAV03 absent.wav\n#00111:0102\n#00104:01");
        e.F.File("A", "copy.wav", "known audio A");
        var originalSources = SnapshotPaths(Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories));
        e.Seal(); var before = Snapshot(e);
        var reportA = e.Resolve();
        Assert.False(reportA.DependencyClosureProven); Assert.False(reportA.RemovalAuthority); Assert.Null(reportA.Provenance.PackageRoot);
        Assert.Equal(e.Content.ManifestSha256, reportA.Provenance.ContentDigest);
        var wav = reportA.Declarations[0]; var copy = reportA.Declarations[1]; var bmp = reportA.Declarations[2]; var missing = reportA.Declarations[3];
        Assert.Equal("OBSERVED_PATH_MATCH", wav.ResolutionState); Assert.Equal("LEXICAL_ORDINAL", wav.PathPolicy);
        Assert.Equal(Sha(Encoding.UTF8.GetBytes("known audio A")), Assert.Single(wav.Candidates).Sha256);
        Assert.Equal(wav.Candidates[0].Sha256, Assert.Single(copy.Candidates).Sha256);
        Assert.NotEqual(wav.Candidates[0].PhysicalFileId, copy.Candidates[0].PhysicalFileId);
        Assert.Equal(Sha(Encoding.UTF8.GetBytes("known image")), Assert.Single(bmp.Candidates).Sha256);
        Assert.Equal("WAV", reportA.VerifiedSyntax.Declarations[0].Namespace); Assert.Equal("BMP", reportA.VerifiedSyntax.Declarations[2].Namespace);
        Assert.Equal("NO_OBSERVED_MATCH", missing.ResolutionState); Assert.Empty(missing.Candidates);
        Assert.Equal("DECLARED_POTENTIAL_NO_OBSERVED_USE", missing.SemanticState);
        // All source payload handles deny any subsequent opens. Resolver needs only
        // the accepted DB, included chart bytes and directory exclusion metadata.
        var held = Directory.GetFiles(e.F.Root, "*", SearchOption.AllDirectories).Select(p => new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        try { e.Resolve("repeat"); } finally { foreach (var stream in held) stream.Dispose(); }
        Assert.Equal(File.ReadAllBytes(Path.Combine(e.F.Base, "resolved", "dependency_resolution.json")), File.ReadAllBytes(Path.Combine(e.F.Base, "repeat", "dependency_resolution.json")));
        Preserved(before);
        Preserved(originalSources);
        // Same name in B carries another digest, and A's chart cannot be admitted
        // merely because B has a byte-identical chart elsewhere.
        Assert.Throws<InvalidOperationException>(() => e.Resolve("wrong-root", "B"));
        PhaseThreeInspector.Inspect(e.F.Path("B", "one.BMS"), e.F.Root, Path.Combine(e.F.Base, "syntaxB"));
        var inB = PhaseFourResolver.Resolve(e.Options("resolvedB", "B", Path.Combine(e.F.Base, "syntaxB", "chart_inspection.json")));
        Assert.Equal(Sha(Encoding.UTF8.GetBytes("different audio B")), Assert.Single(inB.Declarations[0].Candidates).Sha256);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("KICK.WAV", "OBSERVED_PATH_MATCH", "WINDOWS_ORDINAL_IGNORE_CASE")]
    [InlineData("./kick.wav", "OBSERVED_PATH_MATCH", "LEXICAL_ORDINAL")]
    [InlineData("sub/../kick.wav", "OBSERVED_PATH_MATCH", "LEXICAL_ORDINAL")]
    [InlineData("../kick.wav", "OUT_OF_SCOPE", "LEXICAL_RELATIVE_V1")]
    [InlineData("C:\\kick.wav", "OUT_OF_SCOPE", "NO_ABSOLUTE_OR_DEVICE_LOOKUP")]
    [InlineData("\\\\server\\share\\kick.wav", "OUT_OF_SCOPE", "NO_ABSOLUTE_OR_DEVICE_LOOKUP")]
    [InlineData("\\\\?\\C:\\kick.wav", "OUT_OF_SCOPE", "NO_ABSOLUTE_OR_DEVICE_LOOKUP")]
    [InlineData("kick.wav:stream", "UNSUPPORTED_RESOLUTION", "NO_AMBIGUOUS_WIN32_REWRITE")]
    [InlineData("CON.wav", "UNSUPPORTED_RESOLUTION", "NO_AMBIGUOUS_WIN32_REWRITE")]
    [InlineData("kick.wav.", "UNSUPPORTED_RESOLUTION", "NO_AMBIGUOUS_WIN32_REWRITE")]
    [InlineData("kick.wav ", "UNSUPPORTED_RESOLUTION", "NO_AMBIGUOUS_WIN32_REWRITE")]
    [InlineData("\"kick.wav\"", "UNSUPPORTED_RESOLUTION", "NO_AMBIGUOUS_WIN32_REWRITE")]
    [InlineData("KICK~1.WAV", "UNSUPPORTED_RESOLUTION", "NO_AMBIGUOUS_WIN32_REWRITE")]
    [InlineData("kick.ogg", "UNSUPPORTED_RESOLUTION", "ALTERNATE_EXTENSION_UNPROVEN")]
    [InlineData("kick", "UNSUPPORTED_RESOLUTION", "ALTERNATE_EXTENSION_UNPROVEN")]
    public void AuthoredPathsAreLexicalOnlyAndUnsupportedConventionsAreExplicit(string path, string state, string policy)
    {
        using var e = new Evidence("#WAV01 " + path + "\n#00111:01"); e.Seal(); var result = Assert.Single(e.Resolve().Declarations);
        Assert.Equal(path, result.WrittenPath); Assert.Equal(state, result.ResolutionState); Assert.Equal(policy, result.PathPolicy);
        if (policy == "ALTERNATE_EXTENSION_UNPROVEN") Assert.Equal(Sha(Encoding.UTF8.GetBytes("known audio A")), Assert.Single(result.Candidates).Sha256);
    }
    [WindowsPhaseTwoFact]
    public void NestedRootsSharedMembershipParentTraversalAndHardlinksStayDistinct()
    {
        using var e = new Evidence(); e.F.Pair("A/Sub", "C", "#WAV01 ../kick.wav\n#WAV02 local.wav\n#WAV03 nested\\mix.wav\n#00111:010203");
        e.F.File("A/Sub", "local.wav", "local"); e.F.File("A/Sub/nested", "mix.wav", "mixed");
        NativeLink("/H", e.F.Path("A", "linked.wav"), e.F.Path("A", "kick.wav")); e.Seal();
        PhaseThreeInspector.Inspect(e.F.Path("A/Sub", "one.BMS"), e.F.Root, Path.Combine(e.F.Base, "nestedSyntax"));
        var report = Path.Combine(e.F.Base, "nestedSyntax", "chart_inspection.json");
        var outer = PhaseFourResolver.Resolve(e.Options("outer", "A", report));
        var inner = PhaseFourResolver.Resolve(e.Options("inner", "A/Sub", report));
        Assert.Equal("OBSERVED_PATH_MATCH", outer.Declarations[0].ResolutionState); Assert.Equal("OUT_OF_SCOPE", inner.Declarations[0].ResolutionState);
        Assert.Equal("OBSERVED_PATH_MATCH", outer.Declarations[2].ResolutionState);
        Assert.Equal(Assert.Single(outer.Declarations[1].Candidates).EntryId, Assert.Single(inner.Declarations[1].Candidates).EntryId);
        Assert.NotEqual(outer.Provenance.ComponentId, inner.Provenance.ComponentId); Assert.NotEqual(outer.Provenance.ObservationRootId, inner.Provenance.ObservationRootId);
        var identities = e.F.Rows("SELECT p.physical_file_id,e.native_identity FROM physical_files p JOIN preflight_entries e ON e.entry_id=p.entry_id WHERE e.original_path IN ($0,$1)", e.F.Path("A", "kick.wav"), e.F.Path("A", "linked.wav"));
        Assert.Equal(2, identities.Length); Assert.NotEqual(identities[0][0], identities[1][0]); Assert.Equal(identities[0][1], identities[1][1]);
    }
    [WindowsPhaseTwoFact]
    public void RandomAlternativesDuplicateIdsNestedConstraintsAndImpossibleBranchRemainPresent()
    {
        const string chart = "#RANDOM 2\n#IF 1\n#WAV01 kick.wav\n#RANDOM 2\n#IF 2\n#WAV01 image.png\n#ENDIF\n#ENDRANDOM\n#ELSEIF 1\n#WAV01 duplicate.wav\n#ELSE\n#WAV01 else.wav\n#ENDIF\n#IF 3\n#WAV01 beyond.wav\n#ENDIF\n#ENDRANDOM\n#00111:01";
        using var e = new Evidence(chart); e.Seal(); var result = e.Resolve();
        Assert.Equal(5, result.Declarations.Length); Assert.Equal("POSSIBLE_WITHIN_MODEL", result.Declarations[0].Reachability.State);
        Assert.Equal(2, result.Declarations[1].Reachability.Constraints.Length);
        Assert.Equal("IMPOSSIBLE_WITHIN_MODEL", result.Declarations[2].Reachability.State);
        Assert.Equal("POSSIBLE_WITHIN_MODEL", result.Declarations[3].Reachability.State);
        Assert.Equal("IMPOSSIBLE_WITHIN_MODEL", result.Declarations[4].Reachability.State);
        Assert.All(result.Declarations, d => Assert.Single(d.PotentialUsageIds)); Assert.False(result.DependencyClosureProven);
        Assert.Equal("AMBIGUOUS", result.VerifiedSyntax.Declarations[0].State); Assert.Equal(2, result.ExitCode);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("#RANDOM 2\n", "\n#ENDRANDOM", "UNKNOWN", 1)]
    [InlineData("#RANDOM 2\n#IF 1\n#RANDOM 3\n", "\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM", "UNKNOWN", 3)]
    [InlineData("#RANDOM 2\n#IF 1\n#RANDOM 3\n#IF 2\n", "\n#ENDIF\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM", "POSSIBLE_WITHIN_MODEL", 0)]
    [InlineData("#RANDOM 2\n#IF 1\n#IF 2\n", "\n#ENDIF\n#ENDIF\n#ENDRANDOM", "IMPOSSIBLE_WITHIN_MODEL", 0)]
    [InlineData("#RANDOM 2\n#RANDOM 3\n#IF 2\n", "\n#ENDIF\n#ENDRANDOM\n#ENDRANDOM", "UNKNOWN", 1)]
    [InlineData("#RANDOM 2\n#IF 3\n#RANDOM 3\n", "\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM", "UNKNOWN", 3)]
    [InlineData("#RANDOM 2\n#IF 1\n#RANDOM 3\n#RANDOM 4\n#IF 2\n", "\n#ENDIF\n#ENDRANDOM\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM", "UNKNOWN", 3)]
    public void EveryActiveRandomRequiresItsOwnPredicateForDeclarationsAndUsages(string prefix, string suffix, string expected, int unguardedLine)
    {
        using var e = new Evidence(prefix + "#WAV01 kick.wav\n#00111:01" + suffix); e.Seal(); var before = Snapshot(e);
        var result = e.Resolve(); var declaration = Assert.Single(result.Declarations); var usage = Assert.Single(result.Usages);
        Assert.Equal(expected, declaration.Reachability.State); Assert.Equal(expected, usage.Reachability.State);
        Assert.Equal("OBSERVED_PATH_MATCH", declaration.ResolutionState);
        Assert.Equal(Sha(Encoding.UTF8.GetBytes("known audio A")), Assert.Single(declaration.Candidates).Sha256);
        Assert.Equal(usage.UsageId, Assert.Single(declaration.PotentialUsageIds));
        Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        if (unguardedLine != 0)
        {
            var random = Assert.Single(result.VerifiedSyntax.ConditionalContexts, c => c.Directive == "RANDOM" && c.Source.Line == unguardedLine);
            Assert.Contains($"RANDOM context {random.Id}", declaration.Reachability.Justification, StringComparison.Ordinal);
            Assert.Contains("no own admitted IF predicate", usage.Reachability.Justification, StringComparison.Ordinal);
            Assert.Equal("INCOMPLETE", result.ConditionalCoverage);
        }
        else Assert.Equal("COMPLETE_BOUNDED_MODEL", result.ConditionalCoverage);
        Preserved(before);
    }
    [WindowsPhaseTwoFact]
    public void NestedElseIfElseExclusionsRemainBoundToTheirOwnRandomScope()
    {
        const string chart = "#RANDOM 2\n#IF 1\n#RANDOM 3\n#IF 1\n#WAV01 kick.wav\n#00111:01\n#ELSEIF 2\n#WAV02 two.wav\n#00111:02\n#ELSE\n#WAV03 three.wav\n#00111:03\n#ENDIF\n#ENDRANDOM\n#ELSEIF 2\n#RANDOM 2\n#IF 1\n#WAV04 four.wav\n#00111:04\n#ELSE\n#WAV05 five.wav\n#00111:05\n#ENDIF\n#ENDRANDOM\n#ELSE\n#WAV06 six.wav\n#00111:06\n#ENDIF\n#ENDRANDOM";
        using var e = new Evidence(chart);
        foreach (var name in new[] { "two", "three", "four", "five", "six" }) e.F.File("A", name + ".wav", "known " + name);
        e.Seal(); var before = Snapshot(e); var result = e.Resolve();
        Assert.Equal(6, result.Declarations.Length); Assert.Equal(6, result.Usages.Length);
        for (var i = 0; i < 6; i++)
        {
            var expected = i == 5 ? "IMPOSSIBLE_WITHIN_MODEL" : "POSSIBLE_WITHIN_MODEL";
            Assert.Equal(expected, result.Declarations[i].Reachability.State); Assert.Equal(expected, result.Usages[i].Reachability.State);
            Assert.Equal("OBSERVED_PATH_MATCH", result.Declarations[i].ResolutionState); Assert.Single(result.Declarations[i].Candidates);
        }
        var innerElseIf = result.Declarations[1].Reachability.Constraints;
        Assert.Equal(2, innerElseIf.Length);
        Assert.Equal(0, innerElseIf[0].RandomContextId); Assert.Equal(2, innerElseIf[0].UpperBound);
        Assert.Equal(1, innerElseIf[0].EqualValue); Assert.Empty(innerElseIf[0].ExcludedValues);
        Assert.Equal(2, innerElseIf[1].RandomContextId); Assert.Equal(3, innerElseIf[1].UpperBound);
        Assert.Equal(2, innerElseIf[1].EqualValue); Assert.Equal([1], innerElseIf[1].ExcludedValues);
        var innerElse = result.Declarations[2].Reachability.Constraints;
        Assert.Equal(0, innerElse[0].RandomContextId); Assert.Equal(1, innerElse[0].EqualValue); Assert.Empty(innerElse[0].ExcludedValues);
        Assert.Equal(2, innerElse[1].RandomContextId); Assert.Null(innerElse[1].EqualValue); Assert.Equal([1, 2], innerElse[1].ExcludedValues);
        var secondInnerElse = result.Declarations[4].Reachability.Constraints;
        Assert.Equal(0, secondInnerElse[0].RandomContextId); Assert.Equal(2, secondInnerElse[0].EqualValue); Assert.Equal([1], secondInnerElse[0].ExcludedValues);
        Assert.Equal(9, secondInnerElse[1].RandomContextId); Assert.Null(secondInnerElse[1].EqualValue); Assert.Equal([1], secondInnerElse[1].ExcludedValues);
        var outerElse = Assert.Single(result.Declarations[5].Reachability.Constraints);
        Assert.Equal(0, outerElse.RandomContextId); Assert.Null(outerElse.EqualValue); Assert.Equal([1, 2], outerElse.ExcludedValues);
        Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority); Preserved(before);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("#RANDOM 2\n#IF 1\n#RANDOM 0\n#IF 2\n#WAV01 kick.wav\n#00111:01\n#ENDIF\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM")]
    [InlineData("#RANDOM 2\n#IF 1\n#RANDOM 3\n#IF 2\n#WAV01 kick.wav\n#00111:01")]
    [InlineData("#RANDOM 2\n#IF 1\n#SETRANDOM 3\n#IF 2\n#WAV01 kick.wav\n#00111:01\n#ENDIF\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM")]
    public void UnsupportedOrMalformedNestedRandomKeepsBothReachabilityAndBindings(string chart)
    {
        using var e = new Evidence(chart); e.Seal(); var result = e.Resolve();
        Assert.Equal("UNKNOWN", Assert.Single(result.Declarations).Reachability.State);
        Assert.Equal("UNKNOWN", Assert.Single(result.Usages).Reachability.State);
        Assert.Equal("OBSERVED_PATH_MATCH", result.Declarations[0].ResolutionState); Assert.Single(result.Declarations[0].Candidates);
        Assert.Equal("INCOMPLETE", result.ConditionalCoverage); Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
    }
    [WindowsPhaseTwoFact]
    public void NestedRandomDepthAndSymbolicLimitsRetainDeclarationAndUsageCandidates()
    {
        using var e = new Evidence("#RANDOM 2\n#IF 1\n#RANDOM 3\n#IF 2\n#WAV01 kick.wav\n#00111:01\n#ENDIF\n#ENDRANDOM\n#ENDIF\n#ENDRANDOM");
        e.Seal(); var before = Snapshot(e);
        foreach (var (label, limits) in new[] { ("depth", new PhaseFourLimits(MaximumConditionalDepth: 3)), ("terms", new PhaseFourLimits(MaximumSymbolicTerms: 2)) })
        {
            var result = e.Resolve(label, limits: limits);
            Assert.Equal("UNKNOWN", Assert.Single(result.Declarations).Reachability.State); Assert.Equal("UNKNOWN", Assert.Single(result.Usages).Reachability.State);
            Assert.Equal("OBSERVED_PATH_MATCH", result.Declarations[0].ResolutionState); Assert.Single(result.Declarations[0].Candidates);
            Assert.Equal("INCOMPLETE", result.ConditionalCoverage); Assert.Contains(result.Diagnostics, d => d.StartsWith("SYMBOLIC_LIMIT", StringComparison.Ordinal));
            Assert.False(result.DependencyClosureProven); Assert.False(result.RemovalAuthority);
        }
        Preserved(before);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("#SETRANDOM 1\n#IF 1\n#WAV01 kick.wav\n#ENDIF\n#ENDRANDOM")]
    [InlineData("#SWITCH 2\n#CASE 1\n#WAV01 kick.wav\n#SKIP\n#ENDSW")]
    [InlineData("#RANDOM 2\n#ELSE\n#WAV01 kick.wav\n#ENDRANDOM")]
    [InlineData("#RANDOM 2\n#IF 1\n#WAV01 kick.wav")]
    [InlineData("#RANDOM 2\n#WAV01 kick.wav\n#ENDRANDOM")]
    public void MalformedUnsupportedAndOrphanRandomContentNeverDiscardEvidence(string chart)
    {
        using var e = new Evidence(chart); e.Seal(); var r = e.Resolve(); Assert.Single(r.Declarations);
        Assert.Equal("UNKNOWN", r.Declarations[0].Reachability.State); Assert.Single(r.Declarations[0].Candidates);
        Assert.Equal("INCOMPLETE", r.ConditionalCoverage);
    }
    [WindowsPhaseTwoFact]
    public void UnicodeAmbiguousDecodingsAndDifferentNamesAreRetainedWithoutNormalization()
    {
        using var e = new Evidence("#WAV01 ソ.wav\n#00111:01"); e.F.File("A", "ソ.wav", "unicode"); e.Seal(); var r = e.Resolve();
        Assert.Equal(2, r.VerifiedSyntax.Decodings.Length); Assert.Equal(2, r.Declarations.Length);
        Assert.Equal("OBSERVED_PATH_MATCH", r.Declarations[0].ResolutionState); Assert.Contains(r.Declarations[0].Limitations, s => s.Contains("AMBIGUOUS_DECODING", StringComparison.Ordinal));
        Assert.NotEqual(r.Declarations[0].WrittenPath, r.Declarations[1].WrittenPath);
    }
    [WindowsPhaseTwoFact]
    public void BmsonSoundBgaPreviewAndUncertainTitleImageUseTheSameVerifiedInventory()
    {
        const string chart = "{\"version\":\"1.0.0\",\"info\":{\"preview_music\":\"kick.wav\",\"title_image\":\"image.png\"},\"sound_channels\":[{\"name\":\"kick.wav\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false}]}],\"bga\":{\"bga_header\":[{\"id\":1,\"name\":\"image.png\"}],\"bga_events\":[{\"id\":1,\"y\":0}]}}";
        using var e = new Evidence(); File.Delete(e.Chart); File.Delete(e.F.Path("B", "one.BMS")); e.F.File("A", "one.bmson", chart); e.F.File("B", "one.bmson", chart);
        e.F.Scan(); var p = e.F.Run(); var c = new PhaseTwoContentRunner().Run(new(e.F.Db, p.SessionId, p.ManifestSha256, Path.Combine(e.F.Base, "content")));
        PhaseThreeInspector.Inspect(e.F.Path("A", "one.bmson"), e.F.Root, Path.Combine(e.F.Base, "syntax"));
        var result = PhaseFourResolver.Resolve(new(e.F.Db, c.SessionId, c.ManifestSha256, e.Report, e.RootKey(), Path.Combine(e.F.Base, "resolved")));
        Assert.Equal(4, result.Declarations.Length); Assert.All(result.Declarations, d => Assert.Equal("OBSERVED_PATH_MATCH", d.ResolutionState));
        Assert.Contains(result.VerifiedSyntax.Diagnostics, d => d.Code == "TITLE_IMAGE_SPEC_DISCREPANCY"); Assert.False(result.DependencyClosureProven);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("{\"version\":\"9.0\",\"sound_channels\":[{\"name\":\"kick.wav\",\"notes\":[]}]}")]
    [InlineData("{broken")]
    public void UnknownOrMalformedBmsonCannotAcquireSemanticCompleteness(string bytes)
    {
        using var e = new Evidence(); e.F.File("A", "extra.bmson", bytes); e.F.File("B", "extra.bmson", bytes); e.Seal();
        PhaseThreeInspector.Inspect(e.F.Path("A", "extra.bmson"), e.F.Root, Path.Combine(e.F.Base, "extraSyntax"));
        var r = PhaseFourResolver.Resolve(e.Options("extra", report: Path.Combine(e.F.Base, "extraSyntax", "chart_inspection.json")));
        Assert.Equal("INCOMPLETE", r.VerifiedSyntax.SyntaxCoverage); Assert.Equal(2, r.ExitCode); Assert.False(r.DependencyClosureProven);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("declaration")]
    [InlineData("raw-bytes")]
    [InlineData("sha")]
    [InlineData("identity")]
    [InlineData("source-root")]
    [InlineData("missing-bytes")]
    [InlineData("duplicate-property")]
    public void UnsignedReportManipulationIsRefusedBeforePublication(string kind)
    {
        using var e = new Evidence(); e.Seal(); var root = JsonNode.Parse(File.ReadAllText(e.Report))!;
        switch (kind)
        {
            case "declaration": root["analysis"]!["declarations"]![0]!["written_path"] = "forged.wav"; break;
            case "raw-bytes": root["analysis"]!["original_bytes"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("#WAV01 forged.wav")); break;
            case "sha": root["analysis"]!["sha256"] = new string('0', 64); break;
            case "identity": root["source_facts"]!["identity"] = "forged"; break;
            case "source-root": root["source_root"] = e.F.Base; break;
            case "missing-bytes": root["analysis"]!["original_bytes"] = null; break;
        }
        var text = root.ToJsonString(); if (kind == "duplicate-property") text = text.Insert(1, "\"chart_path\":\"forged\","); File.WriteAllText(e.Report, text);
        Assert.Throws<InvalidOperationException>(() => e.Resolve()); Assert.False(Directory.Exists(e.Options().OutputDirectory));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("accepted-digest")]
    [InlineData("content-seal")]
    [InlineData("parent")]
    [InlineData("parent-seal")]
    [InlineData("hash")]
    public void WrongAcceptanceOrTamperedDatabaseIsRefusedUnchanged(string kind)
    {
        using var e = new Evidence(); e.Seal(); var options = e.Options();
        if (kind == "accepted-digest") options = options with { AcceptedContentManifestSha256 = new string('a', 64) };
        else
        {
            // Restore trigger text after an owned adversarial mutation so refusal
            // tests the seals/validators, not just a mismatched schema profile.
            var triggers = e.F.Rows("SELECT name,sql FROM sqlite_schema WHERE type='trigger'");
            foreach (var trigger in triggers) e.F.Sql(e.F.Db, "DROP TRIGGER \"" + trigger[0] + "\"");
            e.F.Sql(e.F.Db, kind switch {
                "parent" => "PRAGMA foreign_keys=OFF; UPDATE sessions SET parent_preflight_id=99999 WHERE stage='CONTENT'",
                "parent-seal" => "UPDATE sessions SET final_evidence_digest='" + new string('b', 64) + "' WHERE stage='PREFLIGHT'",
                "hash" => "UPDATE content_hashes SET sha256='" + new string('c', 64) + "' WHERE status='SUCCESS'",
                _ => "UPDATE sessions SET final_evidence_digest='" + new string('d', 64) + "' WHERE stage='CONTENT'" });
            foreach (var trigger in triggers) e.F.Sql(e.F.Db, (string)trigger[1]!);
        }
        var before = Snapshot(e); Assert.Throws<InvalidOperationException>(() => PhaseFourResolver.Resolve(options)); Preserved(before);
        Assert.False(Directory.Exists(options.OutputDirectory));
    }
    [WindowsPhaseTwoFact]
    public void CoherentGlobalContentAllowsOnlyItsCompleteIndependentComponent()
    {
        using var e = new Evidence(); e.F.Pair("C", "D", "#WAV01 safe.wav\n#00111:01"); e.F.File("C", "safe.wav", "safe");
        e.F.Scan(); var p = e.F.Run(); PhaseTwoPreflightResult c;
        using (var denied = new FileStream(e.F.Path("A", "kick.wav"), FileMode.Open, FileAccess.Read, FileShare.None))
            c = new PhaseTwoContentRunner().Run(new(e.F.Db, p.SessionId, p.ManifestSha256, Path.Combine(e.F.Base, "content")));
        Assert.Equal(1, c.CompleteContentComponents); Assert.Equal(1, c.IncompleteContentComponents);
        PhaseThreeInspector.Inspect(e.Chart, e.F.Root, Path.Combine(e.F.Base, "syntax"));
        var options = new PhaseFourOptions(e.F.Db, c.SessionId, c.ManifestSha256, e.Report, e.RootKey(), Path.Combine(e.F.Base, "bad"));
        Assert.Throws<InvalidOperationException>(() => PhaseFourResolver.Resolve(options));
        PhaseThreeInspector.Inspect(e.F.Path("C", "one.BMS"), e.F.Root, Path.Combine(e.F.Base, "safeSyntax"));
        var result = PhaseFourResolver.Resolve(options with { ChartReportPath = Path.Combine(e.F.Base, "safeSyntax", "chart_inspection.json"), ObservationRootKey = e.RootKey("C"), OutputDirectory = Path.Combine(e.F.Base, "safe") });
        Assert.Equal("OBSERVED_PATH_MATCH", Assert.Single(result.Declarations).ResolutionState);
    }
    [WindowsPhaseTwoFact]
    public void KnownSourceRootsEvidenceAndReparseOutputAliasesAreProtected()
    {
        using var e = new Evidence(); e.Seal();
        foreach (var destination in new[] { e.F.Path("A", "out"), e.F.Path("B", "out"), e.F.Base, e.F.Db, Path.GetDirectoryName(e.Report)! })
            Assert.ThrowsAny<Exception>(() => PhaseFourResolver.Resolve(e.Options() with { OutputDirectory = destination }));
        var alias = Path.Combine(e.F.Base, "alias"); NativeLink("/J", alias, e.F.Root);
        try { Assert.Throws<ReparsePointException>(() => PhaseFourResolver.Resolve(e.Options() with { OutputDirectory = Path.Combine(alias, "bad") })); }
        finally { Directory.Delete(alias); }
        Assert.False(Directory.Exists(e.F.Path("A", "out"))); Assert.False(Directory.Exists(e.F.Path("B", "out")));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("user-report")]
    [InlineData("foreign-temp")]
    public void InterruptedPublicationCannotExposePartialOrOverwriteForeignReports(string kind)
    {
        using var e = new Evidence(); e.Seal(); using var cancel = new CancellationTokenSource(); var output = e.Options().OutputDirectory;
        var final = Path.Combine(output, "dependency_resolution.json");
        var hooks = new PhaseThreePublicationTestHooks {
            AfterFirstWrite = () => { Assert.False(File.Exists(final)); if (kind == "failure") throw new IOException("injected"); if (kind == "cancel") cancel.Cancel(); },
            BeforePublish = () => {
                if (kind == "user-report") File.WriteAllText(final, "user report");
                if (kind == "foreign-temp") { var temp = Directory.GetFiles(output, "*.tmp").Single(); File.Delete(temp); File.WriteAllText(temp, "foreign temporary"); }
            }
        };
        Assert.ThrowsAny<Exception>(() => PhaseFourResolver.ResolveCore(e.Options(), null, cancel.Token, hooks));
        if (kind == "user-report") Assert.Equal("user report", File.ReadAllText(final)); else Assert.False(File.Exists(final));
        if (kind == "foreign-temp") Assert.Equal("foreign temporary", File.ReadAllText(Directory.GetFiles(output, "*.tmp").Single()));
        else Assert.Empty(Directory.GetFiles(output, "*.tmp"));
    }
    [WindowsPhaseTwoFact]
    public void ModerateIndexedInventoryAndAllReducedLimitsAreExplicitlyIncomplete()
    {
        var chart = string.Join('\n', Enumerable.Range(1, 200).Select(n => $"#WAV{n:X2} tone{n}.wav"));
        using var e = new Evidence(chart);
        for (var n = 1; n <= 400; n++) e.F.File("A", $"tone{n}.wav", "payload " + n);
        e.Seal(); var result = e.Resolve(); Assert.Equal(200, result.Declarations.Length); Assert.All(result.Declarations, d => Assert.Single(d.Candidates));
        Assert.Equal(Sha(Encoding.UTF8.GetBytes("payload 200")), result.Declarations[^1].Candidates[0].Sha256);
        var limited = e.Resolve("limited", limits: new(MaximumCandidateLinks: 2)); Assert.Contains(limited.Diagnostics, d => d.StartsWith("CANDIDATE_LIMIT", StringComparison.Ordinal)); Assert.Equal("UNKNOWN", limited.Declarations[^1].ResolutionState);
        var inventory = e.Resolve("inventory-limit", limits: new(MaximumInventoryFiles: 2)); Assert.All(inventory.Declarations, d => Assert.Empty(d.Candidates)); Assert.Equal("INCOMPLETE", inventory.ResolutionCoverage);
        var refs = e.Resolve("reference-limit", limits: new(MaximumReferences: 2)); Assert.Equal("UNKNOWN", refs.Declarations[^1].ResolutionState);
        Assert.Throws<InvalidOperationException>(() => e.Resolve("report-limit", limits: new(MaximumReportBytes: 100)));
        Assert.False(Directory.Exists(e.Options("report-limit").OutputDirectory));
    }
    [Fact]
    public void SymbolicLimitsAndContradictoryNestedSameVariableAreConservative()
    {
        var syntax = PhaseThreeParser.Parse(Encoding.UTF8.GetBytes("#RANDOM 2\n#IF 1\n#IF 2\n#WAV01 a.wav\n#ENDIF\n#ENDIF\n#ENDRANDOM"), ".bms");
        var conditions = new PhaseFourConditions(syntax, new(), default); var declaration = Assert.Single(syntax.Declarations);
        Assert.Equal("IMPOSSIBLE_WITHIN_MODEL", conditions.For(0, declaration.ConditionalContextIds).State);
        foreach (var limits in new[] { new PhaseFourLimits(MaximumSymbolicTerms: 1), new PhaseFourLimits(MaximumConditionalDepth: 1) })
        {
            var bounded = new PhaseFourConditions(syntax, limits, default); Assert.True(bounded.Limited); Assert.Equal("UNKNOWN", bounded.For(0, declaration.ConditionalContextIds).State);
        }
        var candidate = new ObservedResourceCandidate(1, 1, 1, 1, "a", "a", "a.wav", "native", new string('a', 64), 1, "");
        var paths = new PhaseFourPaths([candidate, candidate with { EntryId = 2, PhysicalFileId = 2, RelativePath = "A.wav" }], new());
        Assert.Equal("AMBIGUOUS_PATH_MATCH", paths.Resolve("", "a.wav").State);
        var limitedPaths = new PhaseFourPaths([candidate, candidate with { EntryId = 2, RelativePath = "A.wav" }], new(MaximumCandidatesPerReference: 1));
        Assert.Equal("UNKNOWN", limitedPaths.Resolve("", "a.wav").State); Assert.True(limitedPaths.Limited);
    }
    private static void NativeLink(string kind, string alias, string target)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink {kind} \"{alias}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true })!;
        var stdout = process.StandardOutput.ReadToEnd(); var stderr = process.StandardError.ReadToEnd(); process.WaitForExit(); Assert.True(process.ExitCode == 0, stdout + stderr);
    }
}
