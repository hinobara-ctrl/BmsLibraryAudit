namespace BmsLibraryAudit.Core;

public sealed record PhaseTwoPreflightOptions(string SourceDatabasePath, long SourceScanId,
    string DatabasePath, string OutputDirectory, IReadOnlyList<string>? Components = null);
public sealed record PhaseTwoPreflightResult(long SessionId, string State, string ManifestSha256,
    long SelectedComponents, long AdmittedComponents, long RejectedComponents, long EstimatedReadBytes)
{
    public int ExitCode => RejectedComponents > 0 || AdmittedComponents == 0 ? 2 : 0;
}
public sealed record PhaseTwoEntry(string OriginalPath, string Kind, long? Size, long? MtimeTicks,
    long? Attributes, string? Extension, bool? IsChart, string Status, string Readability, string? NativeIdentity)
{
    public string Key => PathPolicy.Normalize(OriginalPath);
}
public sealed record PhaseTwoChartRead(long? BeforeSize, long? BeforeMtime, long? AfterSize,
    long? AfterMtime, string? Sha256, long BytesRead, int Attempts, string Status);

// The production implementation has one payload entry point, used only for
// selected direct charts. Tests can instrument reads and deterministic failures.
public interface IPhaseTwoFileSystem
{
    PhaseTwoEntry Metadata(string path);
    IEnumerable<string> Children(string directory);
    PhaseTwoChartRead ReadChart(string path, CancellationToken cancellationToken);
}
public sealed class PhaseTwoTestHooks
{
    public Action? AfterSourceFingerprint { get; init; }
    public Action? BeforeInventoryVerification { get; init; }
    public Action? BeforeInventory { get; init; }
    public Action? BeforeSeal { get; init; }
    public Action<string>? BeforeReportFile { get; init; }
}
