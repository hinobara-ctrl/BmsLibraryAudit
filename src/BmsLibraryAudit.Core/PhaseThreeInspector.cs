using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

public sealed record PhaseThreeInspection(string ChartPath, NativePathFacts SourceFacts, ChartSyntaxResult Analysis, string Notice);
public static class PhaseThreeInspector
{
    public const string Notice = "Phase 3 is observed syntactic evidence only. Supported syntax coverage is not full chart validation. All declared paths remain potential conservation obligations. Dependency closure, reachability, physical path resolution and removal/merge authority are not established. No Phase 2B provenance join is implemented.";
    public static PhaseThreeInspection Inspect(string chartPath, string outputDirectory, PhaseThreeLimits? limits = null, CancellationToken token = default)
    {
        limits ??= new(); PhaseThreeParser.ValidateLimits(limits); token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Phase 3 file inspection requires Windows native identity/alias support; the pure parser API is portable.");
        chartPath = PathPolicy.Absolute(chartPath); outputDirectory = PathPolicy.Absolute(outputDirectory);
        if (!PhaseTwoFileSystem.IsChart(chartPath)) throw new ArgumentException("Phase 3 inspect requires one BMS/BME/BML/PMS/BMSON chart.");
        ValidateOutput(chartPath, outputDirectory);
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
        ValidateOutput(chartPath, outputDirectory); token.ThrowIfCancellationRequested();
        var inspection = new PhaseThreeInspection(chartPath, before, result, Notice);
        Directory.CreateDirectory(outputDirectory); var parent = PhaseTwoSafety.Inspect(outputDirectory);
        PathPolicy.RejectReparseAncestors(outputDirectory);
        var report = Path.Combine(outputDirectory, "chart_inspection.json");
        using (var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var currentParent = PhaseTwoSafety.Inspect(outputDirectory);
            if (currentParent.Identity != parent.Identity || currentParent.ResolvedPath != parent.ResolvedPath || currentParent.Attributes != parent.Attributes ||
                PhaseTwoSafety.Inspect(stream.SafeFileHandle, report).ResolvedPath != parent.ResolvedPath + "\\CHART_INSPECTION.JSON")
                throw new IOException("Report parent/identity changed before export.");
            var json = JsonSerializer.Serialize(inspection, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }).Replace("\r\n", "\n", StringComparison.Ordinal);
            var payload = new UTF8Encoding(false).GetBytes(json + "\n"); token.ThrowIfCancellationRequested(); stream.Write(payload);
        }
        return inspection;
    }
    private static void ValidateOutput(string chart, string output)
    {
        var folder = Path.GetDirectoryName(chart)!; PathPolicy.RejectReparseAncestors(chart); PathPolicy.RejectReparseAncestors(output);
        if (PathPolicy.Contains(folder, output) || PathPolicy.Contains(output, folder)) throw new InvalidOperationException("Output overlaps the chart's source directory.");
        var source = PhaseTwoSafety.Resolve(folder); var destination = PhaseTwoSafety.Resolve(output);
        if (Within(source, destination) || Within(destination, source)) throw new InvalidOperationException("Native output/source alias or overlap.");
        if (PhaseTwoSafety.Exists(output) && (!Directory.Exists(output) || Directory.EnumerateFileSystemEntries(output).Any())) throw new InvalidOperationException("Phase 3 output must be a new or empty ordinary directory.");
        static bool Within(string parent, string path) => path == parent || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.Ordinal);
    }
}
