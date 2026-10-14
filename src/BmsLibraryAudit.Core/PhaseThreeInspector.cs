using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

public sealed record PhaseThreeInspection(string ChartPath, string SourceRoot, NativePathFacts SourceRootFacts,
    NativePathFacts SourceFacts, ChartSyntaxResult Analysis, string Notice);
internal sealed class PhaseThreePublicationTestHooks
{
    internal Action? AfterTemporaryCreated { get; init; }
    internal Action? AfterFirstWrite { get; init; }
    internal Action? BeforePublish { get; init; }
}
public static class PhaseThreeInspector
{
    public const string Notice = "Phase 3 is observed syntactic evidence only. Supported syntax coverage is not full chart validation. All declared paths remain potential conservation obligations. Only the explicitly declared source root is protected; other library roots are not inferred. Dependency closure, reachability, physical path resolution and removal/merge authority are not established. No Phase 2B provenance join is implemented.";
    public static PhaseThreeInspection Inspect(string chartPath, string sourceRoot, string outputDirectory, PhaseThreeLimits? limits = null, CancellationToken token = default)
        => InspectCore(chartPath, sourceRoot, outputDirectory, limits, token, null);
    internal static PhaseThreeInspection InspectCore(string chartPath, string sourceRoot, string outputDirectory, PhaseThreeLimits? limits,
        CancellationToken token, PhaseThreePublicationTestHooks? hooks)
    {
        limits ??= new(); PhaseThreeParser.ValidateLimits(limits); token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Phase 3 file inspection requires Windows native identity/alias support; the pure parser API is portable.");
        if (string.IsNullOrWhiteSpace(sourceRoot)) throw new ArgumentException("An explicit, non-empty source root is required.", nameof(sourceRoot));
        chartPath = PathPolicy.Absolute(chartPath); sourceRoot = PathPolicy.Absolute(sourceRoot); outputDirectory = PathPolicy.Absolute(outputDirectory);
        if (!PhaseTwoFileSystem.IsChart(chartPath)) throw new ArgumentException("Phase 3 inspect requires one BMS/BME/BML/PMS/BMSON chart.");
        var root = ValidateOutput(chartPath, sourceRoot, outputDirectory);
        var before = PhaseTwoSafety.Inspect(chartPath);
        if ((before.Attributes & (uint)FileAttributes.Directory) != 0) throw new ArgumentException("Chart must be an ordinary regular file.");
        using var source = new FileStream(chartPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
        if (PhaseTwoSafety.Inspect(source.SafeFileHandle, chartPath) != before) throw new IOException("Chart changed before its read-only handle was bound.");
        ChartSyntaxResult result;
        if (before.Size > limits.MaximumBytes)
        {
            var b = new ChartSyntaxBuilder(Path.GetExtension(chartPath).TrimStart('.').ToUpperInvariant(), before.Size, limits, token);
            b.Diagnostic("SIZE_LIMIT", "UNKNOWN", "Chart exceeds byte limit; payload was not read, hashed or decoded."); result = b.Finish();
        }
        else
        {
            var bytes = new byte[checked((int)before.Size)]; source.ReadExactly(bytes); token.ThrowIfCancellationRequested();
            if (source.ReadByte() != -1) throw new IOException("Chart length changed during read.");
            result = PhaseThreeParser.Parse(bytes, Path.GetExtension(chartPath), limits, token);
        }
        if (PhaseTwoSafety.Inspect(source.SafeFileHandle, chartPath) != before || PhaseTwoSafety.Inspect(chartPath) != before)
            throw new IOException("Chart changed during inspection; no trustworthy report is exported.");
        RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory), root); token.ThrowIfCancellationRequested();
        var inspection = new PhaseThreeInspection(chartPath, sourceRoot, root, before, result, Notice);
        var json = JsonSerializer.Serialize(inspection, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }).Replace("\r\n", "\n", StringComparison.Ordinal);
        var payload = new UTF8Encoding(false).GetBytes(json + "\n"); token.ThrowIfCancellationRequested();
        RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory), root);
        Directory.CreateDirectory(outputDirectory); var parent = PhaseTwoSafety.Inspect(outputDirectory);
        PathPolicy.RejectReparseAncestors(outputDirectory);
        var report = Path.Combine(outputDirectory, "chart_inspection.json");
        var temporary = Path.Combine(outputDirectory, ".chart_inspection." + Guid.NewGuid().ToString("N") + ".tmp");
        NativePathFacts? ownedTemporary = null; var published = false;
        try
        {
            RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory), root); RequireParent(outputDirectory, parent);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownedTemporary = PhaseTwoSafety.Inspect(stream.SafeFileHandle, temporary);
                RequireParent(outputDirectory, parent); RequireTemporaryPath(ownedTemporary, parent, temporary);
                hooks?.AfterTemporaryCreated?.Invoke(); token.ThrowIfCancellationRequested();
                RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory, temporary), root); RequireParent(outputDirectory, parent);
                RequireTemporaryPath(PhaseTwoSafety.Inspect(stream.SafeFileHandle, temporary), parent, temporary);
                // A bounded first chunk supplies a precise internal fault-injection
                // point while the final filename remains absent.
                var first = Math.Min(payload.Length / 2, 65536); stream.Write(payload.AsSpan(0, first)); stream.Flush();
                hooks?.AfterFirstWrite?.Invoke(); token.ThrowIfCancellationRequested();
                RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory, temporary), root); RequireParent(outputDirectory, parent);
                RequireTemporaryPath(PhaseTwoSafety.Inspect(stream.SafeFileHandle, temporary), parent, temporary);
                stream.Write(payload.AsSpan(first)); stream.Flush(flushToDisk: true);
                ownedTemporary = PhaseTwoSafety.Inspect(stream.SafeFileHandle, temporary);
                if (ownedTemporary.Size != payload.Length) throw new IOException("Incomplete staged report.");
            }
            var completedTemporary = PhaseTwoSafety.Inspect(temporary);
            if (completedTemporary.Identity != ownedTemporary.Identity || completedTemporary.Size != payload.Length)
                throw new IOException("Staged report changed after its write handle closed.");
            ownedTemporary = completedTemporary;
            hooks?.BeforePublish?.Invoke(); token.ThrowIfCancellationRequested();
            RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory, temporary), root); RequireParent(outputDirectory, parent);
            using (var staged = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if (PhaseTwoSafety.Inspect(staged.SafeFileHandle, temporary) != ownedTemporary || PhaseTwoSafety.Inspect(temporary) != ownedTemporary)
                    throw new IOException("Staged report identity/stamp changed before publication.");
                RequireTemporaryPath(ownedTemporary, parent, temporary); token.ThrowIfCancellationRequested();
                File.Move(temporary, report, overwrite: false); published = true;
            }
        }
        finally
        {
            if (!published && ownedTemporary is not null) CleanupTemporary(chartPath, sourceRoot, outputDirectory, temporary, root, parent, ownedTemporary);
        }
        return inspection;
    }
    private static NativePathFacts ValidateOutput(string chart, string sourceRoot, string output, string? temporary = null, bool requireEmpty = true)
    {
        PathPolicy.RejectReparseAncestors(sourceRoot); PathPolicy.RejectReparseAncestors(chart); PathPolicy.RejectReparseAncestors(output);
        var root = PhaseTwoSafety.Inspect(sourceRoot);
        if ((root.Attributes & (uint)FileAttributes.Directory) == 0) throw new ArgumentException("Source root must be an existing ordinary directory.");
        if (!PathPolicy.Contains(sourceRoot, chart)) throw new InvalidOperationException("Chart is outside its declared source root.");
        if (PathPolicy.Contains(sourceRoot, output) || PathPolicy.Contains(output, sourceRoot)) throw new InvalidOperationException("Output overlaps the declared source root.");
        var destination = PhaseTwoSafety.Resolve(output);
        if (!Within(root.ResolvedPath, PhaseTwoSafety.Resolve(chart))) throw new InvalidOperationException("Chart is outside the native source root.");
        if (Within(root.ResolvedPath, destination) || Within(destination, root.ResolvedPath)) throw new InvalidOperationException("Native output/source-root alias or overlap.");
        if (PhaseTwoSafety.Exists(output) && (!Directory.Exists(output) || requireEmpty && Directory.EnumerateFileSystemEntries(output).Any(p => temporary is null || !PathPolicy.Comparer.Equals(PathPolicy.Absolute(p), temporary))))
            throw new InvalidOperationException("Phase 3 output must be new/empty or contain only this operation's exclusive temporary.");
        return root;
    }
    private static bool Within(string parent, string path) => path == parent || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.Ordinal);
    private static void RequireRoot(NativePathFacts current, NativePathFacts expected)
    {
        if (current.Identity != expected.Identity || current.ResolvedPath != expected.ResolvedPath || current.Attributes != expected.Attributes)
            throw new IOException("Declared source root identity changed during inspection.");
    }
    private static void RequireParent(string path, NativePathFacts expected)
    {
        var current = PhaseTwoSafety.Inspect(path);
        if (current.Identity != expected.Identity || current.ResolvedPath != expected.ResolvedPath || current.Attributes != expected.Attributes)
            throw new IOException("Report parent identity changed during publication.");
    }
    private static void RequireTemporaryPath(NativePathFacts facts, NativePathFacts parent, string path)
    {
        if (facts.LinkCount != 1 || facts.ResolvedPath != parent.ResolvedPath + "\\" + Path.GetFileName(path).ToUpperInvariant())
            throw new IOException("Unsafe temporary report binding.");
    }
    private static void CleanupTemporary(string chart, string rootPath, string output, string temporary, NativePathFacts root, NativePathFacts parent, NativePathFacts owned)
    {
        try
        {
            // Cleanup never follows a changed namespace or deletes a replacement
            // file, pre-existing report, or any directory. A lost binding is left
            // untouched for inspection rather than guessed/recovered.
            RequireRoot(ValidateOutput(chart, rootPath, output, temporary, requireEmpty: false), root); RequireParent(output, parent);
            if (!PhaseTwoSafety.Exists(temporary)) return;
            var current = PhaseTwoSafety.Inspect(temporary); RequireTemporaryPath(current, parent, temporary);
            if (current.Identity == owned.Identity) File.Delete(temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
    }
}
