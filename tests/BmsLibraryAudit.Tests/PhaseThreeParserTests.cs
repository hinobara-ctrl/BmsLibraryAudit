using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Xunit;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseThreeParserTests
{
    private static ChartSyntaxResult Text(string text, string format = "bms", PhaseThreeLimits? limits = null) => PhaseThreeParser.Parse(Encoding.UTF8.GetBytes(text), format, limits);
    private static ChartSyntaxResult Json(string text, PhaseThreeLimits? limits = null) => Text(text, "bmson", limits);
    [Theory]
    [InlineData("bms")] [InlineData("bme")] [InlineData("bml")] [InlineData("pms")]
    public void SimpleDeclarationsAndIndependentNamespacesRemainExact(string format)
    {
        var result = Text("#TITLE Fixture\n#WAV01 sub/a sound.wav\n#BMP01 image.png\n#BPM01 180\n#STOP01 192\n#00101:010100\n#00111:01\n#00104:01\n#00108:01\n#00109:01", format);
        Assert.Equal("COMPLETE_SUPPORTED_SYNTAX", result.SyntaxCoverage); Assert.False(result.DependencyClosureProven);
        Assert.Equal(new[] { "WAV", "BMP", "BPM", "STOP" }, result.Declarations.Select(d => d.Namespace));
        Assert.Equal(new[] { "sub/a sound.wav", "image.png", null, null }, result.Declarations.Select(d => d.WrittenPath));
        Assert.Equal(new[] { "WAV", "WAV", "WAV", "BMP", "BPM", "STOP" }, result.Usages.Select(u => u.Namespace));
        Assert.All(result.Usages, u => Assert.Single(result.LogicalResources[u.LogicalResourceId!.Value].DeclarationIds));
        Assert.Equal(2, result.Declarations[0].Source.Line); Assert.Equal(8, result.Usages[0].Source.Column);
        Assert.Equal("01", result.Usages[0].Source.RawText); Assert.Empty(result.Diagnostics);
    }
    [Fact]
    public void SamePathDifferentIdsAndConflictingDeclarationsNeverCollapse()
    {
        var result = Text("#WAV01 a.wav\n#WAV02 a.wav\n#WAV01 b.wav\n#00111:0102");
        Assert.Equal(new[] { "a.wav", "a.wav", "b.wav" }, result.Declarations.Select(d => d.WrittenPath));
        Assert.Equal(new[] { 0, 2 }, result.LogicalResources[result.Usages[0].LogicalResourceId!.Value].DeclarationIds);
        Assert.Equal("AMBIGUOUS", result.Usages[0].State); Assert.Equal("POTENTIAL", result.Usages[1].State);
        Assert.Contains(result.Diagnostics, d => d.Code == "CONFLICTING_DECLARATIONS"); Assert.Equal("INCOMPLETE", result.SyntaxCoverage);
    }
    [Fact]
    public void HeaderResourcesUnicodeAndAuthoredStringsRemainUnresolved()
    {
        const string text = "#STAGEFILE ../画像/ stage.png \n#BANNER banner file.png\n#BACKBMP C:\\fake\\back.png\n#PREVIEW samples/preview.ogg\n#WAV01 \"音 楽.wav\"\n#00111:01";
        var bytes = new byte[] { 239, 187, 191 }.Concat(Encoding.UTF8.GetBytes(text)).ToArray(); var result = PhaseThreeParser.Parse(bytes, "bms");
        Assert.Single(result.Decodings); Assert.Equal("UTF-8-BOM", result.Decodings[0].Encoding);
        Assert.Equal(new[] { "../画像/ stage.png ", "banner file.png", "C:\\fake\\back.png", "samples/preview.ogg", "\"音 楽.wav\"" }, result.Declarations.Select(d => d.WrittenPath));
        Assert.Equal(bytes, result.OriginalBytes); Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.Sha256);
        Assert.Equal(text, result.Decodings[0].Text); Assert.False(result.DependencyClosureProven);
    }
    [Fact]
    public void LiteralCp932FixtureDecodesStrictlyWithoutChangingBytes()
    {
        var bytes = Encoding.ASCII.GetBytes("#WAV01 ").Concat(new byte[] { 0x89, 0xb9 }).Concat(Encoding.ASCII.GetBytes(".wav\n#00111:01")).ToArray();
        var before = bytes.ToArray(); var result = PhaseThreeParser.Parse(bytes, ".bms");
        Assert.Single(result.Decodings); Assert.Equal("CP932", result.Decodings[0].Encoding); Assert.Equal("音.wav", Assert.Single(result.Declarations).WrittenPath);
        Assert.Equal(before, bytes); Assert.Equal(before, result.OriginalBytes);
    }
    [Fact]
    public void AmbiguousUtf8AndCp932BothRemainPotentialInterpretations()
    {
        var result = Text("#WAV01 é.wav\n#00111:01");
        Assert.Equal(2, result.Decodings.Length); Assert.Contains(result.Diagnostics, d => d.Code == "AMBIGUOUS_ENCODING");
        Assert.Contains(result.Declarations, d => d.WrittenPath == "é.wav"); Assert.Equal(2, result.Declarations.Length);
        Assert.NotEqual(result.Declarations[0].WrittenPath, result.Declarations[1].WrittenPath);
        Assert.Equal(new[] { 0, 1 }, result.Usages.Select(u => u.Source.DecodingId)); Assert.False(result.DependencyClosureProven);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidEncodingHasExplicitFailureAndRetainsRawBytes(bool bom)
    {
        var bytes = bom ? new byte[] { 239, 187, 191, 195 } : new byte[] { 129 };
        var result = PhaseThreeParser.Parse(bytes, "bms"); Assert.Empty(result.Decodings); Assert.Equal(bytes, result.OriginalBytes);
        Assert.Contains(result.Diagnostics, d => d.Code == "DECODING_FAILED"); Assert.Equal(2, result.ExitCode);
    }
    [Fact]
    public void AllNestedRandomAndSwitchBranchesRemainVisibleWithoutChoosingPlayback()
    {
        var result = Text("#RANDOM 2\n#IF 1\n#WAV01 a.wav\n#IF 2\n#BMP01 first.png\n#ENDIF\n#ELSEIF 2\n#WAV01 b.wav\n#ELSE\n#WAV01 c.wav\n#ENDIF\n#ENDRANDOM\n#SWITCH 2\n#CASE 1\n#WAV02 one.wav\n#SKIP\n#CASE 2\n#WAV02 two.wav\n#DEF\n#WAV02 fallback.wav\n#ENDSW\n#00111:0102");
        Assert.Equal(new[] { "a.wav", "first.png", "b.wav", "c.wav", "one.wav", "two.wav", "fallback.wav" }, result.Declarations.Select(d => d.WrittenPath));
        Assert.All(result.Declarations, d => Assert.NotEmpty(d.ConditionalContextIds));
        Assert.Equal(new[] { 0, 2, 3 }, result.LogicalResources[result.Usages[0].LogicalResourceId!.Value].DeclarationIds);
        Assert.Equal(3, result.Declarations[1].ConditionalContextIds.Length); Assert.Contains(result.ConditionalContexts, c => c.Directive == "SKIP");
        Assert.DoesNotContain(result.Diagnostics, d => d.Code.StartsWith("ORPHAN", StringComparison.Ordinal) || d.Code == "UNCLOSED_CONDITIONAL");
    }
    [Theory]
    [InlineData("#IF 1\n#WAV01 keep.wav", "UNCLOSED_CONDITIONAL")]
    [InlineData("#ELSE\n#WAV01 keep.wav", "ORPHAN_BRANCH")]
    [InlineData("#SWITCH 2\n#ENDIF\n#WAV01 keep.wav", "MISMATCHED_CONDITIONAL_END")]
    [InlineData("#RANDOM huh\n#WAV01 keep.wav", "INVALID_CONDITIONAL_ARGUMENT")]
    [InlineData("#CUSTOM_RESOURCE extra.wav\n#WAV01 keep.wav", "UNSUPPORTED_SYNTAX")]
    public void MalformedOrUnknownSyntaxPreservesAvailableResources(string text, string code)
    {
        var result = Text(text); Assert.Contains(result.Declarations, d => d.WrittenPath == "keep.wav"); Assert.Contains(result.Diagnostics, d => d.Code == code);
        Assert.Equal("INCOMPLETE", result.SyntaxCoverage); Assert.False(result.DependencyClosureProven);
    }
    [Fact]
    public void ChannelsRetainPotentialUsesWithoutMixingDamageAndTimingIds()
    {
        var result = Text("#WAV00 mine.wav\n#WAV01 note.wav\n#BMP01 poor.png\n#001D1:02\n#00131:01\n#00151:01\n#00106:01\n#00103:FF\n#001ZZ:01\n#LNOBJ 01");
        Assert.Equal(new[] { "00", "01", "01", "01", "01" }, result.Usages.Select(u => u.LogicalId));
        Assert.Equal("LANDMINE_WAV00_POTENTIAL", result.Usages[0].Kind); Assert.Equal("AMBIGUOUS", result.Usages[^1].State);
        Assert.Contains(result.UnknownEvidence, e => e.Kind == "CHANNEL_ZZ"); Assert.DoesNotContain(result.Usages, u => u.LogicalId == "FF");
    }
    [Theory]
    [InlineData("")] [InlineData("#WAV")] [InlineData("#WAV01")]
    [InlineData("#WAV!1 broken.wav\n#00111:99")]
    [InlineData("#00111:0")]
    public void EmptyMalformedAndTruncatedInputsCannotClaimCompleteness(string text)
    { var result = Text(text); Assert.Equal(2, result.ExitCode); Assert.False(result.DependencyClosureProven); Assert.Equal(Encoding.UTF8.GetBytes(text), result.OriginalBytes); }
    [Fact]
    public void LimitsPreserveUnprocessedEvidenceAndBoundContextAndFactExpansion()
    {
        var longLine = Text("#WAV01 " + new string('a', 50), limits: new(MaximumLineCharacters: 10));
        Assert.Empty(longLine.Declarations); Assert.Contains(longLine.Diagnostics, d => d.Code == "LINE_LIMIT"); Assert.Contains("#WAV01", longLine.Decodings[0].Text);
        var facts = Text("#WAV01 a.wav\n#00111:010101010101", limits: new(MaximumFacts: 3));
        Assert.Contains(facts.Diagnostics, d => d.Code == "FACT_OR_DEPTH_LIMIT"); Assert.Equal(2, facts.Usages.Length);
        var depth = Text("#RANDOM 2\n#IF 1\n#WAV01 a.wav", limits: new(MaximumConditionalDepth: 1)); Assert.Equal(2, depth.ExitCode);
        var size = Text("#WAV01 a.wav", limits: new(MaximumBytes: 4)); Assert.Contains(size.Diagnostics, d => d.Code == "SIZE_LIMIT"); Assert.Null(size.OriginalBytes); Assert.Null(size.Sha256);
        Assert.Throws<ArgumentException>(() => Text("x", limits: new(MaximumBytes: 0)));
    }
    [Fact]
    public void BmsonOneSupportsSoundsHeaderResourcesBgaAndContinuationFacts()
    {
        const string fixture = """
            {"version":"1.0.0","info":{"title":"Fixture","back_image":"../back.png","eyecatch_image":"eye.png","banner_image":"banner.png","preview_music":"preview.ogg"},
             "sound_channels":[{"name":"stem.wav","notes":[{"x":0,"y":0,"l":0,"c":false},{"x":1,"y":240,"l":120,"c":true}]}],
             "bga":{"bga_header":[{"id":1,"name":"movie.webm"}],"bga_events":[{"id":1,"y":0}],"layer_events":[{"id":1,"y":120}],"poor_events":[{"id":1,"y":240}]}}
            """;
        var result = Json(fixture); Assert.Equal("COMPLETE_SUPPORTED_SYNTAX", result.SyntaxCoverage); Assert.Equal(new[] { "1.0.0" }, result.FormatVersions);
        Assert.Equal(new[] { "../back.png", "eye.png", "banner.png", "preview.ogg", "stem.wav", "movie.webm" }, result.Declarations.Select(d => d.WrittenPath));
        var sounds = result.Usages.Where(u => u.Namespace == "BMSON_SOUND").ToArray(); Assert.Equal(new[] { "BGM_EVENT", "PLAYABLE_EVENT" }, sounds.Select(u => u.Kind));
        Assert.Equal(false, sounds[0].Continuation); Assert.Equal(true, sounds[1].Continuation); Assert.Equal(120, sounds[1].Length); Assert.Equal(240, sounds[1].Pulse);
        Assert.Equal("/sound_channels/0/notes/1", sounds[1].Source.JsonPointer); Assert.Equal(3, result.Usages.Count(u => u.Namespace == "BMSON_BGA"));
    }
    [Fact]
    public void DuplicateJsonNamesAndBgaIdsPreserveAllCandidatesWithoutLastWins()
    {
        var result = Json("""
            {"version":"1.0.0","sound_channels":[{"name":"a.wav","name":"b.wav","notes":[{"x":null,"y":0,"l":0,"c":false}]}],
             "bga":{"bga_header":[{"id":1,"name":"one.png"},{"id":1,"name":"two.png"}],"bga_events":[{"id":1,"y":0}]}}
            """);
        Assert.Equal(new[] { "a.wav", "b.wav", "one.png", "two.png" }, result.Declarations.Select(d => d.WrittenPath));
        Assert.All(result.Usages, u => Assert.Equal(2, result.LogicalResources[u.LogicalResourceId!.Value].DeclarationIds.Length));
        Assert.Contains(result.Diagnostics, d => d.Code == "DUPLICATE_JSON_PROPERTY"); Assert.All(result.Usages, u => Assert.Equal("AMBIGUOUS", u.State));
    }
    [Theory]
    [InlineData("{")]
    [InlineData("{\"version\":\"9.0.0\",\"sound_channels\":[]}")]
    [InlineData("{\"sound_channels\":[]}")]
    [InlineData("{\"version\":null,\"sound_channels\":[]}")]
    [InlineData("{\"version\":\"1.0.0\",\"sound_channels\":{}}")]
    [InlineData("{\"version\":\"1.0.0\",\"sound_channels\":[{\"name\":5,\"notes\":[{\"x\":\"custom\",\"y\":-1}]}]}")]
    public void InvalidUnsupportedAndUnexpectedBmsonShapesRemainIncomplete(string fixture)
    { var result = Json(fixture); Assert.Equal(2, result.ExitCode); Assert.False(result.DependencyClosureProven); Assert.NotEmpty(result.Diagnostics); Assert.Equal(Encoding.UTF8.GetBytes(fixture), result.OriginalBytes); }
    [Fact]
    public void UnknownJsonPropertiesAndUndeclaredBgaUsesRemainVisible()
    {
        var result = Json("""{"version":"1.0.0","sound_channels":[],"extra":{"resource":"keep.wav"},"bga":{"bga_events":[{"id":99,"y":0}]}}""");
        Assert.Contains(result.UnknownEvidence, e => e.Source.RawText.Contains("keep.wav", StringComparison.Ordinal));
        Assert.Equal("UNKNOWN", Assert.Single(result.Usages).State); Assert.Null(result.Usages[0].LogicalResourceId);
        Assert.Contains(result.Diagnostics, d => d.Code == "UNDECLARED_LOGICAL_ID");
        var deep = Json("{\"version\":\"1.0.0\",\"sound_channels\":[],\"extra\":[[[[0]]]]}", new(MaximumJsonDepth: 3));
        Assert.Contains(deep.Diagnostics, d => d.Code == "INVALID_OR_DEPTH_LIMIT_JSON");
    }
    [Fact]
    public void ResultsAreDeterministicAndCompactBindingsDoNotMultiplyDuplicateDeclarations()
    {
        var text = string.Join('\n', Enumerable.Repeat("#WAV01 a.wav", 200)) + "\n#00111:" + string.Concat(Enumerable.Repeat("01", 200));
        var a = Text(text); var b = Text(text); Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Single(a.LogicalResources); Assert.Equal(200, a.LogicalResources[0].DeclarationIds.Length); Assert.Equal(200, a.Usages.Length);
        Assert.All(a.Usages, u => Assert.Equal(0, u.LogicalResourceId)); Assert.All(a.Usages, u => Assert.Equal(2, u.Source.RawText.Length));
        Assert.True(JsonSerializer.Serialize(a).Length < 300000);
    }
    [Fact]
    public void UnsupportedIdBaseDoesNotInventCaseFoldedBindings()
    {
        var result = Text("#BASE 62\n#WAVaa first.wav\n#WAVaA second.wav\n#00111:aaaA");
        Assert.Equal(new[] { "first.wav", "second.wav" }, result.Declarations.Select(d => d.WrittenPath));
        Assert.All(result.Declarations, d => { Assert.Null(d.LogicalId); Assert.Equal("UNSUPPORTED", d.State); });
        Assert.All(result.Usages, u => Assert.Null(u.LogicalResourceId));
        Assert.Equal(new[] { "aa", "aA" }, result.Usages.Select(u => u.Source.RawText)); Assert.Equal(2, result.ExitCode);
    }
    [Fact]
    public void AmbiguousBgaHeaderIdsStayUnboundWithoutCrossProduct()
    {
        var fixture = "{\"version\":\"1.0.0\",\"sound_channels\":[],\"bga\":{\"bga_header\":[{" + string.Join(',', Enumerable.Range(1, 200).Select(i => "\"id\":" + i)) + ",\"name\":\"keep.png\"}],\"bga_events\":[{\"id\":1,\"y\":0}]}}";
        var result = Json(fixture); Assert.Single(result.Declarations); Assert.Equal("keep.png", result.Declarations[0].WrittenPath); Assert.Null(result.Declarations[0].LogicalId);
        Assert.Null(Assert.Single(result.Usages).LogicalResourceId); Assert.Contains(result.Diagnostics, d => d.Code == "AMBIGUOUS_BGA_HEADER_ID");
    }
    [Fact]
    public void SaturatedDiagnosticsStillExplainExtractionLimit()
    {
        var result = Text("#UNKNOWN first\n#UNKNOWN second", limits: new(MaximumFacts: 1));
        Assert.Single(result.Diagnostics); Assert.Equal("FACT_OR_DEPTH_LIMIT", result.Diagnostics[0].Code); Assert.Equal(2, result.ExitCode);
    }
}
