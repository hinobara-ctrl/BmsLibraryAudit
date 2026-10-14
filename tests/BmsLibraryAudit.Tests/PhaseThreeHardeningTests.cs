using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseThreeHardeningTests
{
    [WindowsPhaseTwoTheory]
    [InlineData("chart-folder")] [InlineData("sibling-folder")] [InlineData("root")]
    [InlineData("ancestor")] [InlineData("chart-outside")] [InlineData("missing-root")]
    [InlineData("empty-root")] [InlineData("root-file")] [InlineData("occupied")]
    public void ExplicitRootAndDestinationFailuresNeverCreateOutputs(string kind)
    {
        using var f = new Fixture(); var chart = f.File("SongA", "chart.bms", "#WAV01 a.wav\n#00111:01"); Directory.CreateDirectory(f.Path("SongB"));
        var bytes = File.ReadAllBytes(chart); var facts = PhaseTwoSafety.Inspect(chart); var root = f.Root; var output = Path.Combine(f.Base, "external-output");
        switch (kind)
        {
            case "chart-folder": output = f.Path("SongA", "reports"); break;
            case "sibling-folder": output = f.Path("SongB", "reports"); break;
            case "root": output = f.Root; break;
            case "ancestor": output = f.Base; break;
            case "chart-outside": root = f.Path("SongB"); break;
            case "missing-root": root = Path.Combine(f.Base, "does-not-exist"); break;
            case "empty-root": root = ""; break;
            case "root-file": root = chart; break;
            case "occupied": Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "user-file"), "retain"); break;
        }
        var directories = Directory.GetDirectories(f.Base, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var files = Directory.GetFiles(f.Base, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var error = Record.Exception(() => PhaseThreeInspector.Inspect(chart, root, output)); Assert.NotNull(error);
        Assert.True(error is ArgumentException or IOException or InvalidOperationException, error.ToString());
        Assert.Equal(directories, Directory.GetDirectories(f.Base, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(files, Directory.GetFiles(f.Base, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(bytes, File.ReadAllBytes(chart)); Assert.Equal(facts, PhaseTwoSafety.Inspect(chart));
        if (kind == "occupied") Assert.Equal("retain", File.ReadAllText(Path.Combine(output, "user-file")));
    }

    [WindowsPhaseTwoFact]
    public void DeclaredRootJunctionIsRejectedBeforeCreatingDestination()
    {
        using var f = new Fixture(); var chart = f.File("SongA", "chart.bms", "#WAV01 a.wav"); var alias = Path.Combine(f.Base, "root-alias");
        Link(alias, f.Root);
        try { Assert.Throws<ReparsePointException>(() => PhaseThreeInspector.Inspect(Path.Combine(alias, "SongA", "chart.bms"), alias, Path.Combine(f.Base, "out"))); Assert.False(Directory.Exists(Path.Combine(f.Base, "out"))); }
        finally { Directory.Delete(alias); }
    }

    [WindowsPhaseTwoTheory]
    [InlineData("preparation")] [InlineData("write")] [InlineData("cancel")]
    public void FailedOrCancelledPublicationCleansOnlyItsTemporary(string stage)
    {
        using var f = new Fixture(); var chart = f.File("A", "chart.bms", "#WAV01 a.wav\n#00111:01"); var bytes = File.ReadAllBytes(chart); var facts = PhaseTwoSafety.Inspect(chart);
        var output = Path.Combine(f.Base, "publication"); using var cancel = new CancellationTokenSource(); var reached = false;
        void CheckTemporary()
        {
            reached = true; Assert.False(File.Exists(Path.Combine(output, "chart_inspection.json"))); Assert.Single(Directory.GetFiles(output, ".chart_inspection.*.tmp"));
        }
        var hooks = stage == "preparation" ? new PhaseThreePublicationTestHooks { AfterTemporaryCreated = () => { CheckTemporary(); throw new IOException("synthetic preparation failure"); } } :
            stage == "write" ? new PhaseThreePublicationTestHooks { AfterFirstWrite = () => { CheckTemporary(); Assert.True(new FileInfo(Directory.GetFiles(output).Single()).Length > 0); throw new IOException("synthetic partial write failure"); } } :
            new PhaseThreePublicationTestHooks { BeforePublish = () => { CheckTemporary(); cancel.Cancel(); } };
        if (stage == "cancel") Assert.Throws<OperationCanceledException>(() => PhaseThreeInspector.InspectCore(chart, f.Root, output, null, cancel.Token, hooks));
        else Assert.Throws<IOException>(() => PhaseThreeInspector.InspectCore(chart, f.Root, output, null, cancel.Token, hooks));
        Assert.True(reached); Assert.True(Directory.Exists(output)); Assert.Empty(Directory.GetFileSystemEntries(output));
        Assert.Equal(bytes, File.ReadAllBytes(chart)); Assert.Equal(facts, PhaseTwoSafety.Inspect(chart));
    }

    [WindowsPhaseTwoFact]
    public void LateExistingReportIsPreservedAndOwnTemporaryIsRemoved()
    {
        using var f = new Fixture(); var chart = f.File("A", "chart.bms", "#WAV01 a.wav"); var output = Path.Combine(f.Base, "publication");
        var report = Path.Combine(output, "chart_inspection.json");
        Assert.Throws<InvalidOperationException>(() => PhaseThreeInspector.InspectCore(chart, f.Root, output, null, default,
            new() { BeforePublish = () => File.WriteAllText(report, "pre-existing user content") }));
        Assert.Equal("pre-existing user content", File.ReadAllText(report)); Assert.Equal(new[] { report }, Directory.GetFiles(output));
    }

    [WindowsPhaseTwoFact]
    public void ReplacementTemporaryIsNotPublishedOrDeletedAsOwned()
    {
        using var f = new Fixture(); var chart = f.File("A", "chart.bms", "#WAV01 a.wav"); var output = Path.Combine(f.Base, "publication"); string? stagedPath = null; NativePathFacts? foreign = null;
        Assert.Throws<IOException>(() => PhaseThreeInspector.InspectCore(chart, f.Root, output, null, default, new() { BeforePublish = () => {
            stagedPath = Directory.GetFiles(output).Single(); var replacement = Path.Combine(f.Base, "foreign-temporary"); File.WriteAllText(replacement, "user replacement");
            foreign = PhaseTwoSafety.Inspect(replacement); File.Move(replacement, stagedPath, true);
        } }));
        Assert.False(File.Exists(Path.Combine(output, "chart_inspection.json"))); Assert.Equal("user replacement", File.ReadAllText(stagedPath!));
        Assert.Equal(foreign!.Identity, PhaseTwoSafety.Inspect(stagedPath!).Identity);
    }

    [WindowsPhaseTwoFact]
    public void OutputAliasIntroducedBeforePublicationCannotWriteInsideProtectedRoot()
    {
        using var f = new Fixture(); var chart = f.File("A", "chart.bms", "#WAV01 a.wav"); var bytes = File.ReadAllBytes(chart); var facts = PhaseTwoSafety.Inspect(chart);
        var output = Path.Combine(f.Base, "publication"); var moved = Path.Combine(f.Base, "displaced-owned-output");
        try
        {
            Assert.Throws<ReparsePointException>(() => PhaseThreeInspector.InspectCore(chart, f.Root, output, null, default, new() { BeforePublish = () => {
                if (!PathPolicy.Contains(f.Base, moved) || PathPolicy.Contains(f.Root, moved) || !PathPolicy.Contains(f.Base, output)) throw new InvalidOperationException("Unsafe owned fixture move");
                Directory.Move(output, moved); Link(output, f.Root);
            } }));
            Assert.Equal(new[] { chart }, Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories)); Assert.False(File.Exists(Path.Combine(moved, "chart_inspection.json")));
            Assert.Single(Directory.GetFiles(moved, ".chart_inspection.*.tmp")); Assert.Equal(bytes, File.ReadAllBytes(chart)); Assert.Equal(facts, PhaseTwoSafety.Inspect(chart));
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output); }
    }

    [WindowsPhaseTwoFact]
    public void NormalPublicationContainsOneCompleteJsonAndExplicitRootProvenance()
    {
        using var f = new Fixture(); var chart = f.File("A", "chart.bms", "#WAV01 a.wav\n#00111:01"); var output = Path.Combine(f.Base, "publication");
        var result = PhaseThreeInspector.Inspect(chart, f.Root, output); var report = Path.Combine(output, "chart_inspection.json"); Assert.Equal(new[] { report }, Directory.GetFiles(output));
        using var json = JsonDocument.Parse(File.ReadAllBytes(report)); Assert.Equal(f.Root, json.RootElement.GetProperty("source_root").GetString()); Assert.Equal(f.Root, result.SourceRoot);
        Assert.Equal(PhaseTwoSafety.Inspect(f.Root).Identity, result.SourceRootFacts.Identity); Assert.Equal(File.ReadAllBytes(chart), result.Analysis.OriginalBytes);
        Assert.Throws<InvalidOperationException>(() => PhaseThreeInspector.Inspect(chart, f.Root, output));
    }

    [WindowsPhaseTwoFact]
    public void ExclusiveTemporaryHandlePreventsParentMoveDuringWriting()
    {
        using var f = new Fixture(); var chart = f.File("A", "chart.bms", "#WAV01 a.wav"); var output = Path.Combine(f.Base, "publication"); var displaced = Path.Combine(f.Base, "displaced-owned-output");
        long written = 0;
        Assert.Throws<IOException>(() => PhaseThreeInspector.InspectCore(chart, f.Root, output, null, default, new() { AfterFirstWrite = () => {
            written = new FileInfo(Directory.GetFiles(output).Single()).Length;
            if (!PathPolicy.Contains(f.Base, displaced) || PathPolicy.Contains(f.Root, displaced) || !PathPolicy.Contains(f.Base, output)) throw new InvalidOperationException("Unsafe owned fixture move");
            Assert.ThrowsAny<IOException>(() => Directory.Move(output, displaced));
            throw new IOException("synthetic abort after blocked parent move");
        } }));
        Assert.True(written > 0); Assert.False(Directory.Exists(displaced)); Assert.Empty(Directory.GetFileSystemEntries(output));
        Assert.Equal(new[] { chart }, Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("cover.png")] [InlineData("画像/表紙 image.png")] [InlineData("../images/cover.png ")] [InlineData("")]
    public void TitleImageIsAnExplicitPreservedPotentialVisualResource(string path)
    {
        var text = JsonSerializer.Serialize(new { version = "1.0.0", info = new { title_image = path }, sound_channels = Array.Empty<object>() });
        var bytes = Encoding.UTF8.GetBytes(text); var result = PhaseThreeParser.Parse(bytes, "bmson"); var declaration = Assert.Single(result.Declarations);
        Assert.Equal("BMSON_INFO", declaration.Namespace); Assert.Equal("title_image", declaration.LogicalId); Assert.Equal("VISUAL", declaration.ResourceKind);
        Assert.Equal(path, declaration.WrittenPath); Assert.Equal("POTENTIAL", declaration.State); Assert.Equal("/info/title_image", declaration.Source.JsonPointer);
        Assert.Equal(path, JsonSerializer.Deserialize<string>(declaration.Source.RawText)); Assert.Equal(bytes, result.OriginalBytes);
        Assert.Contains(result.Diagnostics, d => d.Code == "TITLE_IMAGE_SPEC_DISCREPANCY" && d.State == "AMBIGUOUS"); Assert.False(result.DependencyClosureProven); Assert.Equal(2, result.ExitCode);
        Assert.DoesNotContain(result.UnknownEvidence, e => e.Source.JsonPointer == "/info/title_image");
        if (path.Length == 0) Assert.Contains(result.Diagnostics, d => d.Code == "EMPTY_RESOURCE_PATH");
    }

    [Theory]
    [InlineData("null", "TITLE_IMAGE_NULL")]
    [InlineData("17", "RESOURCE_PATH_TYPE")]
    [InlineData("{}", "RESOURCE_PATH_TYPE")]
    public void NullAndInvalidTitleImagesRemainExplicitWithoutInventedPaths(string value, string diagnostic)
    {
        var text = "{\"version\":\"1.0.0\",\"info\":{\"title_image\":" + value + "},\"sound_channels\":[]}";
        var result = PhaseThreeParser.Parse(Encoding.UTF8.GetBytes(text), "bmson"); Assert.Empty(result.Declarations);
        Assert.Contains(result.Diagnostics, d => d.Code == diagnostic && d.Source!.JsonPointer == "/info/title_image" && d.Source.RawText == value);
        Assert.Contains(result.Diagnostics, d => d.Code == "TITLE_IMAGE_SPEC_DISCREPANCY"); Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void DuplicateTitleImagesAndOtherHeaderResourcesRemainDistinct()
    {
        const string text = """{"version":"1.0.0","info":{"back_image":"back.png","title_image":"first.png","title_image":"second.png","banner_image":"banner.png","preview_music":"preview.ogg"},"sound_channels":[]}""";
        var result = PhaseThreeParser.Parse(Encoding.UTF8.GetBytes(text), "bmson");
        Assert.Equal(new[] { "back.png", "first.png", "second.png", "banner.png", "preview.ogg" }, result.Declarations.Select(d => d.WrittenPath));
        var titles = result.Declarations.Where(d => d.LogicalId == "title_image").ToArray(); Assert.Equal(2, titles.Length); Assert.All(titles, d => Assert.Equal("AMBIGUOUS", d.State));
        var logical = result.LogicalResources.Single(r => r.LogicalId == "title_image"); Assert.Equal(titles.Select(d => d.Id), logical.DeclarationIds);
        Assert.Contains(result.Diagnostics, d => d.Code == "DUPLICATE_JSON_PROPERTY"); Assert.Contains(result.Diagnostics, d => d.Code == "CONFLICTING_DECLARATIONS");
    }

    private static void Link(string alias, string target)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{alias}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        var log = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit(); Assert.True(process.ExitCode == 0, log);
    }
}
