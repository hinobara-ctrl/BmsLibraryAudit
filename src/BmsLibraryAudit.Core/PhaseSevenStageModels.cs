using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

internal sealed record PhaseSevenLimits(int MaximumFiles = 128, int MaximumDirectories = 64,
    long MaximumSourceBytes = 8 * 1024 * 1024, long MaximumCopyBytes = 8 * 1024 * 1024,
    long MaximumReadBytes = 64 * 1024 * 1024, int MaximumReceiptBytes = 2 * 1024 * 1024,
    long MaximumMemoryGrowth = 128 * 1024 * 1024, int MaximumSeconds = 60);
internal sealed record PhaseSevenSourceRead(PhaseSixSource Appearance, string OriginalPath, string Sha256,
    long BytesRead, NativePathFacts Facts);
internal sealed record PhaseSevenCopy(string RelativePath, PhaseSixSource SelectedSource, PhaseSixSource[] EquivalentSources,
    string ExpectedSha256, string ReadSha256, long BytesCopied);
internal sealed record PhaseSevenTarget(string RelativePath, string Kind, long? Size, string? Sha256, NativePathFacts Facts);
internal sealed record PhaseSevenInput(string Path, string Sha256, NativePathFacts Facts);
internal sealed record PhaseSevenReceipt(string Version, string PlanSha256, PhaseFiveProvenance? Provenance,
    string Workspace, NativePathFacts WorkspaceFacts, string Stage, NativePathFacts? StageFacts,
    string State, string[] Events, PhaseSevenInput[] Inputs, PhaseSevenSourceRead[] Sources, PhaseSevenCopy[] Copies, PhaseSevenTarget[] Targets,
    long BytesCopied, int Files, int Directories, long ElapsedMilliseconds, string[] Errors, PhaseSevenLimits Limits,
    string SemanticEvidence = "UNKNOWN", bool PackageEquivalenceProven = false, bool AllDependenciesProven = false,
    bool RelocationSafe = false, bool Published = false, bool OriginalsRemovable = false,
    string Notice = PhaseSevenFormat.Notice);
internal sealed record PhaseSevenEnvelope(PhaseSevenReceipt Receipt, string ReceiptSha256);
internal sealed record PhaseSevenResult(PhaseSevenEnvelope Envelope, string? ReceiptPath)
{
    internal bool Success => ReceiptPath is not null && Envelope.Receipt.State == "STAGING_PHYSICALLY_VERIFIED";
}
internal sealed class PhaseSevenHooks
{
    internal Action<string, string?>? At { get; init; }
    internal void Hit(string point, string? path = null) => At?.Invoke(point, path);
}
internal sealed class PhaseSevenChangedException(string message) : IOException("SOURCE_STALE_OR_CHANGED: " + message);
internal sealed class PhaseSevenLimitException(string message) : IOException("STAGING_INCOMPLETE: " + message);
internal static class PhaseSevenFormat
{
    internal const string Version = "phase7a-synthetic-stage-1";
    internal const string Notice = "Synthetic physical experiment only. File/directory counts cover independently verified targets; a failed partial tree may contain unverified entries. BytesCopied counts completed write calls including partial files. No global atomicity, universal TOCTOU protection, semantic equivalence, publication or original retirement authority.";
    internal static string Digest(PhaseSevenReceipt receipt)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("BmsLibraryAudit.Phase7A.SyntheticStageReceipt.v1\0"));
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(receipt, PhaseSixFormat.Canonical));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
internal sealed class PhaseSevenBudget(PhaseSevenLimits limits, CancellationToken token)
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private readonly long memory = GC.GetTotalMemory(false);
    private long read;
    internal long Elapsed => watch.ElapsedMilliseconds;
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        if (watch.Elapsed.TotalSeconds > limits.MaximumSeconds || GC.GetTotalMemory(false) - memory > limits.MaximumMemoryGrowth)
            throw new PhaseSevenLimitException("work/memory budget exceeded");
    }
    internal void Read(long bytes) { read = checked(read + bytes); Check(); if (read > limits.MaximumReadBytes) throw new PhaseSevenLimitException("read byte budget exceeded"); }
    internal static void Validate(PhaseSevenLimits limits)
    {
        var maximum = new PhaseSevenLimits();
        foreach (var property in typeof(PhaseSevenLimits).GetProperties()) {
            var value = Convert.ToInt64(property.GetValue(limits));
            if (value < 1 || value > Convert.ToInt64(property.GetValue(maximum))) throw new ArgumentException("Synthetic stage limits may only reduce positive hard caps.");
        }
    }
}
