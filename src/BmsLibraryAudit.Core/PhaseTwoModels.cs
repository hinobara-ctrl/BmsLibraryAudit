namespace BmsLibraryAudit.Core;

public sealed record PhaseTwoContentOptions(string DatabasePath, long PreflightSessionId,
    string AcceptedManifestSha256, string OutputDirectory);
public sealed record PhaseTwoContentRead(PhaseTwoEntry? Before, PhaseTwoEntry? After, string? Sha256,
    long BytesRead, int Attempts, string Status, string StartedUtc, string EndedUtc);
public interface IPhaseTwoContentFileSystem : IPhaseTwoFileSystem
{
    PhaseTwoContentRead ReadContent(PhaseTwoEntry expected, byte[] buffer, CancellationToken cancellationToken);
}
public sealed class PhaseTwoContentTestHooks
{
    public Action? AfterPrevalidation { get; init; }
    public Action<string>? AfterFile { get; init; }
    public Action? BeforeFinalVerification { get; init; }
    public Action? BeforeSeal { get; init; }
}
internal sealed class PhaseTwoStaleException(string path, string code = "STALE_PREFLIGHT") : IOException(code + ": " + path)
{
    internal string Path { get; } = path;
    internal string Code { get; } = code;
}

public sealed record PhaseTwoPreflightOptions(string SourceDatabasePath, long SourceScanId,
    string DatabasePath, string OutputDirectory, IReadOnlyList<string>? Components = null);
public sealed record PhaseTwoPreflightResult(long SessionId, string State, string ManifestSha256,
    long SelectedComponents, long AdmittedComponents, long RejectedComponents, long EstimatedReadBytes)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? CompleteContentComponents { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? IncompleteContentComponents { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? ParentPreflightId { get; init; }
    public int ExitCode => ParentPreflightId is not null && State != "COMPLETED" ? State == "CANCELLED" ? 130 : State == "STALE" ? 2 : 1 :
        IncompleteContentComponents is not null ? IncompleteContentComponents > 0 ? 2 : 0 :
        RejectedComponents > 0 || AdmittedComponents == 0 ? 2 : 0;
}
public sealed record PhaseTwoEntry(string OriginalPath, string Kind, long? Size, long? MtimeTicks,
    long? Attributes, string? Extension, bool? IsChart, string Status, string Readability, string? NativeIdentity)
{
    public string Key => PathPolicy.Normalize(OriginalPath);
}
public sealed record PhaseTwoChartRead(long? BeforeSize, long? BeforeMtime, long? AfterSize,
    long? AfterMtime, string? Sha256, long BytesRead, int Attempts, string Status);

// Phase 2A metadata/direct-chart surface, reused by the explicit CONTENT
// extension. Tests can instrument reads and deterministic failures.
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
