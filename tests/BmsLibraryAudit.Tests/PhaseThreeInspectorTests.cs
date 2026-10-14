using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseThreeInspectorTests
{
    [WindowsPhaseTwoTheory]
    [InlineData("bms", "#WAV01 ../not-read.wav\n#00111:01")]
    [InlineData("bmson", "{\"version\":\"1.0.0\",\"sound_channels\":[{\"name\":\"../not-read.wav\",\"notes\":[{\"x\":1,\"y\":0,\"l\":0,\"c\":false}]}]}")]
    public void InspectionExportsDeterministicJsonAndPreservesSourceFiles(string extension, string text)
    {
        using var f = new Fixture(); var chart = f.File("A", "fixture." + extension, text); var bytes = File.ReadAllBytes(chart); var facts = PhaseTwoSafety.Inspect(chart); var parent = PhaseTwoSafety.Inspect(Path.GetDirectoryName(chart)!);
        var first = Path.Combine(f.Base, "inspection"); var repeat = Path.Combine(f.Base, "repeat-inspection");
        var a = PhaseThreeInspector.Inspect(chart, first); var b = PhaseThreeInspector.Inspect(chart, repeat);
        Assert.Equal(0, a.Analysis.ExitCode); Assert.False(a.Analysis.DependencyClosureProven);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first, "chart_inspection.json")), File.ReadAllBytes(Path.Combine(repeat, "chart_inspection.json")));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, "chart_inspection.json"))); Assert.Equal(bytes, json.RootElement.GetProperty("analysis").GetProperty("original_bytes").GetBytesFromBase64());
        Assert.Equal(bytes, File.ReadAllBytes(chart)); Assert.Equal(facts, PhaseTwoSafety.Inspect(chart)); Assert.Equal(parent, PhaseTwoSafety.Inspect(Path.GetDirectoryName(chart)!));
        Assert.Equal(new[] { chart }, Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories)); Assert.False(File.Exists(f.Source)); Assert.False(File.Exists(f.Db));
        Assert.Equal(a.Analysis.Sha256, b.Analysis.Sha256);
    }
    [WindowsPhaseTwoFact]
    public void UnsafeExistingAndOverlappingOutputsAreRefusedBeforeWrites()
    {
        using var f = new Fixture(); var chart = f.File("A", "fixture.bms", "#WAV01 a.wav"); var facts = PhaseTwoSafety.Inspect(chart); var bytes = File.ReadAllBytes(chart);
        foreach (var output in new[] { f.Path("A", "reports"), f.Root, chart, Path.Combine(f.Base, "occupied") })
        {
            if (output.EndsWith("occupied", StringComparison.Ordinal)) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "keep"), "existing"); }
            Assert.Throws<InvalidOperationException>(() => PhaseThreeInspector.Inspect(chart, output));
        }
        Assert.False(Directory.Exists(f.Path("A", "reports"))); Assert.Equal(bytes, File.ReadAllBytes(chart)); Assert.Equal(facts, PhaseTwoSafety.Inspect(chart));
    }
    [WindowsPhaseTwoFact]
    public void NativeJunctionOutputAliasAndChartAliasAreNeverFollowed()
    {
        using var f = new Fixture(); var chart = f.File("A", "fixture.bms", "#WAV01 a.wav"); var alias = Path.Combine(f.Base, "alias");
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{alias}\" \"{f.Path("A")}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        var log = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit(); Assert.True(process.ExitCode == 0, log);
        try
        {
            Assert.Throws<ReparsePointException>(() => PhaseThreeInspector.Inspect(chart, Path.Combine(alias, "out")));
            Assert.Throws<ReparsePointException>(() => PhaseThreeInspector.Inspect(Path.Combine(alias, "fixture.bms"), Path.Combine(f.Base, "out")));
            Assert.False(Directory.Exists(f.Path("A", "out"))); Assert.False(Directory.Exists(Path.Combine(f.Base, "out")));
        }
        finally { Directory.Delete(alias); }
    }
    [WindowsPhaseTwoFact]
    public void SizeLimitReportsUnobservedIdentityAndCancellationDoesNotWriteOutput()
    {
        using var f = new Fixture(); var chart = f.File("A", "fixture.bms", "#WAV01 a.wav"); var before = File.ReadAllBytes(chart); var facts = PhaseTwoSafety.Inspect(chart);
        var limited = PhaseThreeInspector.Inspect(chart, Path.Combine(f.Base, "limited"), new(MaximumBytes: 4));
        Assert.Equal(2, limited.Analysis.ExitCode); Assert.Null(limited.Analysis.Sha256); Assert.Null(limited.Analysis.OriginalBytes); Assert.Equal(before.Length, limited.Analysis.ByteLength);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => PhaseThreeInspector.Inspect(chart, Path.Combine(f.Base, "cancelled"), token: cancel.Token));
        Assert.False(Directory.Exists(Path.Combine(f.Base, "cancelled"))); Assert.Equal(before, File.ReadAllBytes(chart)); Assert.Equal(facts, PhaseTwoSafety.Inspect(chart));
    }
}
