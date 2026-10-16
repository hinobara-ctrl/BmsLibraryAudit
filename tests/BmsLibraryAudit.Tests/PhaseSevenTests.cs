using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Xunit;
using Xunit.Abstractions;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseSevenTests(ITestOutputHelper output)
{
    private static Dictionary<string, (byte[] Bytes, NativePathFacts Facts)> Snapshot(PhaseSevenSyntheticWorkspace w) =>
        Directory.GetFiles(w.Library, "*", SearchOption.AllDirectories).Concat([w.SourceDatabase, w.Evidence, w.PlanPath])
            .ToDictionary(p => p, p => (File.ReadAllBytes(p), PhaseTwoSafety.Inspect(p)));
    private static void Preserved(PhaseSevenSyntheticWorkspace w, Dictionary<string, (byte[] Bytes, NativePathFacts Facts)> before)
    {
        foreach (var (path, expected) in before) { Assert.Equal(expected.Bytes, File.ReadAllBytes(path)); Assert.Equal(expected.Facts, PhaseTwoSafety.Inspect(path)); }
        foreach (var db in new[] { w.SourceDatabase, w.Evidence }) foreach (var suffix in new[] { "-journal", "-wal", "-shm" }) Assert.False(File.Exists(db + suffix));
    }
    private static void NoAuthority(PhaseSevenReceipt r)
    {
        Assert.Equal("UNKNOWN", r.SemanticEvidence); Assert.False(r.PackageEquivalenceProven); Assert.False(r.AllDependenciesProven);
        Assert.False(r.RelocationSafe); Assert.False(r.Published); Assert.False(r.OriginalsRemovable);
    }
    [WindowsPhaseTwoFact]
    public void EndToEndSmoke()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w);
        var sourceTree = Directory.GetFileSystemEntries(w.Library, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var sourceDirectories = Directory.GetDirectories(w.Library, "*", SearchOption.AllDirectories).Append(w.Library).ToDictionary(p => p, PhaseTwoSafety.Inspect);
        var result = PhaseSevenSyntheticStager.Run(w); Assert.True(result.Success, string.Join(";", result.Envelope.Receipt.Errors));
        var receipt = PhaseSevenStageVerifier.Verify(w, PhaseSevenStageVerifier.ReadReceipt(w)).Receipt; NoAuthority(receipt);
        var expectedPaths = new[] { ".", "chart.bms", "chart.bmson", "chart.pms", "empty", "image.bmp", "kick-alt.wav", "kick-copy.wav", "kick.wav", "readme.txt", "sub", "sub/音.dat", "unknown" };
        Assert.Equal(expectedPaths, receipt.Targets.Select(t => t.RelativePath));
        Assert.Equal(10, receipt.Files); Assert.Equal(3, receipt.Directories); Assert.Equal(15, receipt.Sources.Length); Assert.Equal(10, receipt.Copies.Length);
        Assert.Single(receipt.Copies, c => c.RelativePath == "kick.wav"); Assert.Equal(2, Assert.Single(receipt.Copies, c => c.RelativePath == "kick.wav").EquivalentSources.Length);
        var expectedAudio = Enumerable.Range(0, 65536 + 17).Select(i => (byte)(i % 251)).ToArray();
        foreach (var name in new[] { "kick.wav", "kick-copy.wav", "kick-alt.wav" }) Assert.Equal(expectedAudio, File.ReadAllBytes(Path.Combine(w.Stage, name)));
        Assert.Equal(Encoding.UTF8.GetBytes("exclusive opaque B"), File.ReadAllBytes(Path.Combine(w.Stage, "unknown")));
        Assert.Equal(Encoding.UTF8.GetBytes("nested Unicode auxiliary"), File.ReadAllBytes(Path.Combine(w.Stage, "sub", "音.dat")));
        foreach (var name in new[] { "chart.bms", "chart.pms", "chart.bmson" }) Assert.Equal(before[Path.Combine(w.SourceA, name)].Bytes, File.ReadAllBytes(Path.Combine(w.Stage, name)));
        Assert.Equal(receipt.Copies.Sum(c => c.BytesCopied), receipt.BytesCopied);
        Assert.Equal(Directory.GetFiles(w.Stage, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length), receipt.BytesCopied);
        foreach (var copy in receipt.Copies) {
            Assert.Equal(copy.EquivalentSources.OrderBy(s => s.RootId).ThenBy(s => s.EntryId).First(), copy.SelectedSource);
            Assert.Equal(copy.ExpectedSha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(w.TargetPath(copy.RelativePath)))));
        }
        Assert.Equal(sourceTree, Directory.GetFileSystemEntries(w.Library, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var (path, facts) in sourceDirectories) Assert.Equal(facts, PhaseTwoSafety.Inspect(path));
        Preserved(w, before); Assert.False(File.Exists(Path.Combine(w.Stage, "stage_receipt.json")));
        output.WriteLine("PHASE7A_SMOKE=" + JsonSerializer.Serialize(new { Workspace = w.Root, Stage = w.Stage, Receipt = result.ReceiptPath,
            receipt.State, receipt.Files, receipt.Directories, receipt.BytesCopied, receipt.ElapsedMilliseconds, FilesPresent = receipt.Targets.Where(t => t.Kind == "REGULAR_FILE").Select(t => new { t.RelativePath, t.Size, t.Sha256 }) }));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("DifferentChartNames")][InlineData("DifferentStructure")]
    public void IndependentNamesAndStructuresPreserveEveryAppearance(string scenario)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(Enum.Parse<PhaseSevenFixture>(scenario));
        var before = Snapshot(w);
        var directories = Directory.GetDirectories(w.Library, "*", SearchOption.AllDirectories).Append(w.Library).ToDictionary(p => p, PhaseTwoSafety.Inspect);
        var result = PhaseSevenSyntheticStager.Run(w); Assert.True(result.Success, string.Join(";", result.Envelope.Receipt.Errors));
        var receipt = PhaseSevenStageVerifier.Verify(w, PhaseSevenStageVerifier.ReadReceipt(w)).Receipt; NoAuthority(receipt);
        var expected = scenario == "DifferentChartNames"
            ? new[] { ".", "alternate.bms", "kick.wav", "normal.bms" }
            : new[] { ".", "chart.bms", "kick.wav", "north", "north/opaque", "south", "south/empty", "south/other-name" };
        Assert.Equal(expected, receipt.Targets.Select(t => t.RelativePath));
        Assert.Single(receipt.Copies, c => c.RelativePath == "kick.wav");
        Assert.Equal(2, Assert.Single(receipt.Copies, c => c.RelativePath == "kick.wav").EquivalentSources.Length);
        var plan = w.AdmitPlan().Plan;
        foreach (var root in plan.Roots) foreach (var entry in root.Entries.Where(e => e.Kind == "REGULAR_FILE")) {
            var target = w.TargetPath(entry.RelativePath);
            Assert.Equal(before[entry.OriginalPath].Bytes, File.ReadAllBytes(target));
            Assert.Contains(new PhaseSixSource(root.Id, entry.EntryId), Assert.Single(receipt.Copies, c => c.RelativePath == entry.RelativePath).EquivalentSources);
        }
        var charts = plan.Roots.SelectMany(r => r.Entries).Where(e => e.IsChart).ToArray();
        Assert.Equal(2, charts.Length); Assert.Single(charts.Select(e => e.Sha256).Distinct());
        if (scenario == "DifferentChartNames") {
            Assert.Equal(new[] { "alternate.bms", "normal.bms" }, charts.Select(e => e.RelativePath).Order(StringComparer.Ordinal));
            Assert.NotEqual(Path.GetFileName(w.SourceA), Path.GetFileName(w.SourceB));
            Assert.Equal(2, receipt.Copies.Count(c => c.RelativePath.EndsWith(".bms", StringComparison.Ordinal)));
        }
        Preserved(w, before);
        foreach (var (path, facts) in directories) Assert.Equal(facts, PhaseTwoSafety.Inspect(path));
        Assert.Equal(directories.Keys.Order(StringComparer.Ordinal), Directory.GetDirectories(w.Library, "*", SearchOption.AllDirectories).Append(w.Library).Order(StringComparer.Ordinal));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("Conflict")][InlineData("CaseConflict")]
    public void ConflictingPlansNeverBuildPartialTrees(string fixture)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(Enum.Parse<PhaseSevenFixture>(fixture)); var before = Snapshot(w); var result = PhaseSevenSyntheticStager.Run(w);
        Assert.False(result.Success); Assert.Equal("STAGING_FAILED", result.Envelope.Receipt.State); Assert.False(Directory.Exists(w.Stage)); Preserved(w, before);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("A", "kick.wav")][InlineData("B", "kick.wav")][InlineData("B", "unknown")][InlineData("A", "chart.bms")]
    public void ChangedSourcesIncludingUnchosenCopiesAreRejected(string side, string name)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var path = Path.Combine(side == "A" ? w.SourceA : w.SourceB, name);
        var stamp = File.GetLastWriteTimeUtc(path); var bytes = File.ReadAllBytes(path); bytes[0] ^= 0x3f; File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, stamp);
        var before = Snapshot(w); var result = PhaseSevenSyntheticStager.Run(w);
        Assert.Equal("SOURCE_STALE_OR_CHANGED", result.Envelope.Receipt.State); Assert.False(result.Success); Assert.False(Directory.Exists(w.Stage)); Preserved(w, before);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("extra")][InlineData("missing")][InlineData("bytes")][InlineData("kind")]
    public void IndependentVerifierRejectsActualTargetTampering(string kind)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var result = PhaseSevenSyntheticStager.Run(w); Assert.True(result.Success);
        var target = Path.Combine(w.Stage, "kick.wav");
        if (kind == "extra") File.WriteAllText(Path.Combine(w.Stage, "extra.dat"), "unexpected");
        if (kind == "missing") File.Delete(target);
        if (kind == "bytes") { var bytes = File.ReadAllBytes(target); bytes[0] ^= 1; File.WriteAllBytes(target, bytes); }
        if (kind == "kind") { File.Delete(target); Directory.CreateDirectory(target); }
        Assert.ThrowsAny<Exception>(() => PhaseSevenStageVerifier.Verify(w, result.Envelope));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("hash")][InlineData("path")][InlineData("chosen")][InlineData("state")][InlineData("authority")][InlineData("source")][InlineData("count")][InlineData("digest")][InlineData("copy-budget")][InlineData("receipt-budget")]
    public void RehashedReceiptCannotOverrideEvidence(string kind)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var result = PhaseSevenSyntheticStager.Run(w); Assert.True(result.Success);
        var r = result.Envelope.Receipt; var copies = r.Copies.ToArray();
        if (kind == "hash") { copies[0] = copies[0] with { ExpectedSha256 = new string('a',64), ReadSha256 = new string('a',64) }; r = r with { Copies = copies }; }
        if (kind == "path") { copies[0] = copies[0] with { RelativePath = "other.bms" }; r = r with { Copies = copies }; }
        if (kind == "chosen") { copies[0] = copies[0] with { SelectedSource = copies[0].EquivalentSources.Last() }; r = r with { Copies = copies }; }
        if (kind == "state") r = r with { State = "PUBLISHED" };
        if (kind == "authority") r = r with { OriginalsRemovable = true };
        if (kind == "source") r = r with { Sources = r.Sources.Skip(1).ToArray() };
        if (kind == "count") r = r with { BytesCopied = r.BytesCopied + 1 };
        if (kind == "copy-budget") r = r with { Limits = r.Limits with { MaximumCopyBytes = 1 } };
        if (kind == "receipt-budget") r = r with { Limits = r.Limits with { MaximumReceiptBytes = 1 } };
        var forged = new PhaseSevenEnvelope(r, kind == "digest" ? new string('0',64) : PhaseSevenFormat.Digest(r));
        Assert.ThrowsAny<Exception>(() => PhaseSevenStageVerifier.Verify(w, forged));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("PLAN_ADMISSION")][InlineData("SOURCE_REVALIDATION")][InlineData("SOURCE_READ")][InlineData("DIRECTORY_CREATED")]
    [InlineData("COPY_SOURCE_READ")][InlineData("COPY_CHUNK")][InlineData("COPY_CLOSED")][InlineData("INDEPENDENT_VERIFICATION")][InlineData("RECEIPT_GENERATION")]
    public void InjectedFailuresPreserveSourcesAndNeverReportSuccessfulStaging(string point)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w);
        var result = PhaseSevenSyntheticStager.Run(w, hooks: new() { At = (p, _) => { if (p == point) throw new IOException("controlled failure at " + point); } });
        Assert.False(result.Success); Assert.NotEqual("STAGING_PHYSICALLY_VERIFIED", result.Envelope.Receipt.State); NoAuthority(result.Envelope.Receipt); Preserved(w, before);
        if (result.ReceiptPath is not null) Assert.NotEqual("STAGING_PHYSICALLY_VERIFIED", PhaseSevenStageVerifier.ReadReceipt(w).Receipt.State);
    }
    [WindowsPhaseTwoFact]
    public void CancellationDuringCopyIsIncompleteAndCannotResume()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w); using var cancel = new CancellationTokenSource();
        var result = PhaseSevenSyntheticStager.Run(w, token: cancel.Token, hooks: new() { At = (point, path) => { if (point == "COPY_CHUNK" && path!.EndsWith("kick.wav",StringComparison.Ordinal)) cancel.Cancel(); } });
        Assert.False(result.Success); Assert.Equal("STAGING_INCOMPLETE", result.Envelope.Receipt.State); Assert.Contains("CANCELLED", result.Envelope.Receipt.Errors);
        Assert.True(result.Envelope.Receipt.BytesCopied > 0); Assert.Equal(result.Envelope.Receipt.BytesCopied,result.Envelope.Receipt.Copies.Sum(c => c.BytesCopied));
        Assert.False(PhaseSevenSyntheticStager.Run(w).Success); Preserved(w, before);
    }
    [WindowsPhaseTwoTheory]
    [InlineData("files")][InlineData("directories")][InlineData("source-bytes")][InlineData("copy-bytes")][InlineData("read-bytes")][InlineData("receipt")]
    public void ReducedBudgetsCannotSilentlyTruncate(string kind)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var limits = kind switch {
            "files" => new PhaseSevenLimits(MaximumFiles:1), "directories" => new(MaximumDirectories:1), "source-bytes" => new(MaximumSourceBytes:1),
            "copy-bytes" => new(MaximumCopyBytes:1), "read-bytes" => new(MaximumReadBytes:1), _ => new(MaximumReceiptBytes:1) };
        var before = Snapshot(w); var result = PhaseSevenSyntheticStager.Run(w, limits); Assert.False(result.Success);
        Assert.Equal("STAGING_INCOMPLETE", result.Envelope.Receipt.State); Preserved(w, before);
    }
    [WindowsPhaseTwoFact]
    public void BuilderClaimsCannotHideAnIncorrectPhysicalTree()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w);
        var result = PhaseSevenSyntheticStager.Run(w, hooks: new() { At = (point, _) => { if (point == "INDEPENDENT_VERIFICATION") File.WriteAllText(Path.Combine(w.Stage,"unknown"), "wrong"); } });
        Assert.False(result.Success); Assert.Equal("STAGING_FAILED", result.Envelope.Receipt.State); Preserved(w, before);
    }
    [WindowsPhaseTwoFact]
    public void SourceWriteSharingAndNamespaceChangesCannotPass()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var original = File.ReadAllBytes(Path.Combine(w.SourceA,"kick.wav"));
        var result = PhaseSevenSyntheticStager.Run(w, hooks: new() { At = (point, _) => { if (point == "INDEPENDENT_VERIFICATION") {
            Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(w.SourceA,"kick.wav"), "forbidden write"));
            File.WriteAllText(Path.Combine(w.SourceB,"injected"), "new namespace entry");
        } } });
        Assert.False(result.Success); Assert.Equal("SOURCE_STALE_OR_CHANGED", result.Envelope.Receipt.State); Assert.Equal(original,File.ReadAllBytes(Path.Combine(w.SourceA,"kick.wav")));
    }
    [WindowsPhaseTwoFact]
    public void HardlinkNamesBecomeSeparateOrdinaryDestinationFiles()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(PhaseSevenFixture.Hardlinks); var before = Snapshot(w); var result = PhaseSevenSyntheticStager.Run(w);
        Assert.True(result.Success, string.Join(";", result.Envelope.Receipt.Errors)); PhaseSevenStageVerifier.Verify(w, result.Envelope);
        var a = PhaseTwoSafety.Inspect(Path.Combine(w.Stage,"kick.wav")); var b = PhaseTwoSafety.Inspect(Path.Combine(w.Stage,"hardlink.wav"));
        Assert.NotEqual(a.Identity,b.Identity); Assert.Equal((uint)1,a.LinkCount); Assert.Equal((uint)1,b.LinkCount); Preserved(w,before);
    }
    [WindowsPhaseTwoFact]
    public void WorkspaceCannotAcceptExternalOrPreexistingDestinations()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create();
        foreach (var path in new[] { w.Root, w.SourceA, w.SourceB, w.Evidence, Path.Combine(w.Library,"stage"), Path.GetDirectoryName(w.Root)! })
            Assert.ThrowsAny<Exception>(() => w.ValidateDestination(path));
        Directory.CreateDirectory(w.Stage); File.WriteAllText(Path.Combine(w.Stage,"foreign"),"retained");
        Assert.False(PhaseSevenSyntheticStager.Run(w).Success); Assert.Equal("retained",File.ReadAllText(Path.Combine(w.Stage,"foreign")));
        Assert.False(typeof(PhaseSevenSyntheticStager).IsPublic); Assert.Empty(typeof(PhaseSevenSyntheticWorkspace).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("../escape")][InlineData("/absolute")][InlineData("C:/outside")][InlineData("x\\y")][InlineData("CON")][InlineData("folder/NUL.txt")][InlineData("a/../b")][InlineData("a.")][InlineData("a ")]
    public void UnsafeRelativePathsAreRejectedWithoutRewriting(string path)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); Assert.ThrowsAny<Exception>(() => w.TargetPath(path));
    }
    [WindowsPhaseTwoTheory]
    [InlineData("destination")][InlineData("unexpected-stage-entry")]
    public void JunctionsCannotRedirectStagingOrVerification(string kind)
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w);
        var alias = kind == "destination" ? w.Stage : Path.Combine(w.Stage,"linked");
        void Junction() {
            Assert.True(PathPolicy.Contains(w.Root,alias)); Assert.True(PathPolicy.Contains(w.Root,w.SourceA));
            using var process = Process.Start(new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "mklink", "/J", alias, w.SourceA }, UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true })!;
            process.WaitForExit(); Assert.Equal(0,process.ExitCode);
        }
        try {
            if (kind == "destination") Junction();
            var result = PhaseSevenSyntheticStager.Run(w, hooks: new() { At = (point,_) => { if (kind != "destination" && point == "INDEPENDENT_VERIFICATION") Junction(); } });
            Assert.False(result.Success); Preserved(w,before);
        } finally {
            if (Directory.Exists(alias) && (File.GetAttributes(alias) & FileAttributes.ReparsePoint) != 0) Directory.Delete(alias);
        }
    }
    [WindowsPhaseTwoFact]
    public void ReceiptPublicationCannotOverwriteForeignFilesOrLeaveSuccess()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w);
        var result = PhaseSevenSyntheticStager.Run(w,hooks:new() { At = (point,_) => { if (point == "RECEIPT_GENERATION") {
            Directory.CreateDirectory(w.ReceiptDirectory); File.WriteAllText(Path.Combine(w.ReceiptDirectory,"stage_receipt.json"),"foreign");
        } } });
        Assert.False(result.Success); Assert.Null(result.ReceiptPath); Assert.Equal("STAGING_FAILED",result.Envelope.Receipt.State);
        Assert.Equal("foreign",File.ReadAllText(Path.Combine(w.ReceiptDirectory,"stage_receipt.json"))); Preserved(w,before);
    }
    [WindowsPhaseTwoFact]
    public void EvidenceAliasesAreRejectedBeforePayloadOrProviderAccess()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); using var other = PhaseSevenSyntheticWorkspace.Create();
        File.Delete(w.Evidence);
        Assert.True(PhaseSevenNative.CreateHardLink(w.Evidence,other.Evidence,IntPtr.Zero));
        var bytes = File.ReadAllBytes(other.Evidence); var facts = PhaseTwoSafety.Inspect(other.Evidence);
        using (var held = new FileStream(other.Evidence,FileMode.Open,FileAccess.Read,FileShare.None)) {
            var result = PhaseSevenSyntheticStager.Run(w);
            Assert.False(result.Success); Assert.False(Directory.Exists(w.Stage));
            Assert.Contains(result.Envelope.Receipt.Errors,error => error.Contains("factory-owned native object",StringComparison.Ordinal));
        }
        Assert.Equal(bytes,File.ReadAllBytes(other.Evidence)); Assert.Equal(facts,PhaseTwoSafety.Inspect(other.Evidence));
    }
    [WindowsPhaseTwoFact]
    public void PartialLargeFileWriteLeavesAnUnverifiedIsolatedStageWithAccurateCounters()
    {
        using var w = PhaseSevenSyntheticWorkspace.Create(); var before = Snapshot(w);
        var result = PhaseSevenSyntheticStager.Run(w,hooks:new() { At = (point,path) => {
            if (point == "COPY_CHUNK" && path!.EndsWith("kick.wav",StringComparison.Ordinal)) throw new IOException("Controlled partial large-file write failure");
        } });
        Assert.False(result.Success); Assert.Equal("STAGING_FAILED",result.Envelope.Receipt.State);
        Assert.Equal(65536,new FileInfo(Path.Combine(w.Stage,"kick.wav")).Length);
        Assert.Equal(result.Envelope.Receipt.BytesCopied,Directory.GetFiles(w.Stage,"*",SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length));
        Assert.Equal(65536,Assert.Single(result.Envelope.Receipt.Copies,c => c.RelativePath == "kick.wav").BytesCopied);
        Assert.Equal("",Assert.Single(result.Envelope.Receipt.Copies,c => c.RelativePath == "kick.wav").ReadSha256); Preserved(w,before);
    }
}
