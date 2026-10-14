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
        ObservedReportPublication.Publish(outputDirectory, "chart_inspection.json", payload,
            (temporary, empty) => RequireRoot(ValidateOutput(chartPath, sourceRoot, outputDirectory, temporary, empty), root), token, hooks);
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
}
