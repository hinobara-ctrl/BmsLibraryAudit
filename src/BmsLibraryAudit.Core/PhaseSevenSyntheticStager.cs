using System.Security.Cryptography;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

internal static class PhaseSevenSyntheticStager
{
    internal static PhaseSevenResult Run(PhaseSevenSyntheticWorkspace workspace, PhaseSevenLimits? limits = null,
        CancellationToken token = default, PhaseSevenHooks? hooks = null)
    {
        limits ??= new(); PhaseSevenBudget.Validate(limits); var budget = new PhaseSevenBudget(limits, token);
        var events = new List<string>(); var copies = new List<PhaseSevenCopy>(); var errors = new List<string>();
        PhaseSixEnvelope? plan = null; PhaseSevenSourceRead[] sources = []; PhaseSevenTarget[] targets = []; PhaseSevenInput[] inputs = [];
        var state = "STAGING_FAILED"; long bytesWritten = 0;
        try {
            budget.Check(); workspace.RequireBinding(); hooks?.Hit("PLAN_ADMISSION");
            using var inputLease = new PhaseSevenInputLease(workspace, budget); inputs = inputLease.Records;
            plan = workspace.AdmitPlan(); events.Add("PLAN_VERIFIED");
            using var evidence = PhaseSixEvidenceAdmission.Load(workspace.Options, new(), token);
            hooks?.Hit("SOURCE_REVALIDATION");
            using var lease = PhaseSevenSourceLease.Open(workspace, plan.Plan, limits, budget, hooks);
            sources = lease.Records; events.Add("SOURCE_REVALIDATION_PASSED"); budget.Check();
            var expectedCopyBytes = plan.Plan.VirtualEntries.Where(v => v.Kind == "REGULAR_FILE")
                .Sum(v => lease.Entries[v.Sources[0]].Size!.Value);
            if (expectedCopyBytes > limits.MaximumCopyBytes) throw new PhaseSevenLimitException("copy byte budget exceeded");
            workspace.BeginStage(); hooks?.Hit("DIRECTORY_CREATED", workspace.Stage);
            foreach (var directory in plan.Plan.VirtualEntries.Where(v => v.Kind == "DIRECTORY" && v.RelativePath != ".")
                .OrderBy(v => v.RelativePath.Count(c => c == '/')).ThenBy(v => v.RelativePath, StringComparer.Ordinal)) {
                budget.Check(); workspace.RequireStage(); var path = workspace.TargetPath(directory.RelativePath);
                PathPolicy.RejectReparseAncestors(path); PhaseSevenNative.CreateDirectory(path); workspace.GuardStageDirectory(path); hooks?.Hit("DIRECTORY_CREATED", path);
            }
            var buffer = new byte[65536];
            foreach (var target in plan.Plan.VirtualEntries.Where(v => v.Kind == "REGULAR_FILE")) {
                budget.Check(); workspace.RequireStage();
                var selected = target.Sources.OrderBy(s => s.RootId).ThenBy(s => s.EntryId).First(); var original = lease.Entries[selected];
                var source = lease.Streams[selected]; lease.RequireFile(selected); source.Position = 0;
                var path = workspace.TargetPath(target.RelativePath); PathPolicy.RejectReparseAncestors(path); hooks?.Hit("COPY_SOURCE_READ", original.OriginalPath);
                var recordIndex = copies.Count; copies.Add(new(target.RelativePath, selected, target.Sources, target.Sha256!, "", 0));
                long copied = 0; string digest; NativePathFacts created;
                using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.SequentialScan)) {
                    created = PhaseTwoSafety.Inspect(destination.SafeFileHandle, path);
                    if (created.LinkCount != 1 || !created.ResolvedPath.StartsWith(workspace.StageIdentity!.ResolvedPath + "\\", StringComparison.Ordinal)) throw new IOException("Unsafe created file binding.");
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); int count;
                    while ((count = source.Read(buffer)) != 0) {
                        budget.Read(count); if (copied + count > original.Size) throw new PhaseSevenChangedException(original.OriginalPath);
                        destination.Write(buffer, 0, count); hash.AppendData(buffer, 0, count); copied += count;
                        bytesWritten += count; copies[recordIndex] = copies[recordIndex] with { BytesCopied = copied };
                        hooks?.Hit("COPY_CHUNK", path); budget.Check(); workspace.RequireStage();
                        if (PhaseTwoSafety.Inspect(destination.SafeFileHandle, path).Identity != created.Identity || PhaseTwoSafety.Inspect(path).Identity != created.Identity) throw new IOException("Destination identity changed during copy.");
                    }
                    destination.Flush(true); digest = Convert.ToHexStringLower(hash.GetHashAndReset());
                    copies[recordIndex] = copies[recordIndex] with { ReadSha256 = digest };
                    if (copied != original.Size || digest != target.Sha256 || PhaseTwoSafety.Inspect(destination.SafeFileHandle, path).Size != copied) throw new PhaseSevenChangedException(original.OriginalPath);
                }
                hooks?.Hit("COPY_CLOSED", path); lease.RequireFile(selected);
                if (PhaseTwoSafety.Inspect(path).Identity != created.Identity || PhaseTwoSafety.Inspect(path).LinkCount != 1) throw new IOException("Destination binding changed after close.");
            }
            events.Add("STAGING_CONSTRUCTED"); hooks?.Hit("INDEPENDENT_VERIFICATION");
            targets = PhaseSevenStageVerifier.Inspect(workspace, plan, lease, limits, budget);
            inputLease.RequireUnchanged(); evidence.RequireBinding(); events.Add("STAGING_PHYSICALLY_VERIFIED"); state = "STAGING_PHYSICALLY_VERIFIED";
            hooks?.Hit("RECEIPT_GENERATION"); budget.Check();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or OperationCanceledException or System.Text.Json.JsonException)
        {
            state = e is PhaseSevenChangedException ? "SOURCE_STALE_OR_CHANGED" : e is OperationCanceledException or PhaseSevenLimitException ? "STAGING_INCOMPLETE" : "STAGING_FAILED";
            errors.Add(e is OperationCanceledException ? "CANCELLED" : e.GetType().Name + ": " + e.Message);
        }
        PhaseSevenReceipt Receipt() => new(PhaseSevenFormat.Version, plan?.PlanSha256 ?? "", plan?.Plan.Provenance,
            workspace.Root, workspace.Facts, workspace.Stage, workspace.StageIdentity, state, events.ToArray(), inputs, sources, copies.ToArray(), targets,
            bytesWritten, targets.Count(t => t.Kind == "REGULAR_FILE"), targets.Count(t => t.Kind == "DIRECTORY"), budget.Elapsed, errors.ToArray(), limits);
        var receipt = Receipt(); var envelope = new PhaseSevenEnvelope(receipt, PhaseSevenFormat.Digest(receipt));
        try {
            workspace.RequireBinding();
            var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, PhaseSixFormat.Pretty).Concat(new byte[] { 10 }).ToArray();
            if (payload.Length > limits.MaximumReceiptBytes) throw new PhaseSevenLimitException("receipt byte budget exceeded");
            var destination = new PhaseFourResolver.Destination(workspace.ReceiptDirectory, [workspace.Library, workspace.Stage], [workspace.Evidence, workspace.SourceDatabase, workspace.PlanPath]);
            void Validate(string? temporary, bool empty) { workspace.RequireBinding(); destination.Validate(temporary, empty); }
            ObservedReportPublication.Publish(workspace.ReceiptDirectory, "stage_receipt.json", payload, Validate, CancellationToken.None);
            return new(envelope, Path.Combine(workspace.ReceiptDirectory, "stage_receipt.json"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) {
            state = e is PhaseSevenLimitException ? "STAGING_INCOMPLETE" : "STAGING_FAILED"; errors.Add("RECEIPT_NOT_PUBLISHED: " + e.Message);
            receipt = Receipt(); return new(new(receipt, PhaseSevenFormat.Digest(receipt)), null);
        }
    }
}

// Shared fresh-source acquisition only; destination verification is separate.
internal sealed class PhaseSevenSourceLease : IDisposable
{
    internal Dictionary<PhaseSixSource, PhaseFiveEntry> Entries { get; private set; } = [];
    internal Dictionary<PhaseSixSource, FileStream> Streams { get; } = [];
    internal PhaseSevenSourceRead[] Records { get; private set; } = [];
    private readonly List<PhaseSevenDirectoryGuard> directories = [];
    private readonly Dictionary<PhaseSixSource, NativePathFacts> before = [];
    private readonly PhaseSevenSyntheticWorkspace workspace;
    private readonly PhaseSixPlan plan;
    private PhaseSevenSourceLease(PhaseSevenSyntheticWorkspace workspace, PhaseSixPlan plan) { this.workspace = workspace; this.plan = plan; }
    internal static PhaseSevenSourceLease Open(PhaseSevenSyntheticWorkspace workspace, PhaseSixPlan plan, PhaseSevenLimits limits, PhaseSevenBudget budget, PhaseSevenHooks? hooks = null)
    {
        var lease = new PhaseSevenSourceLease(workspace, plan);
        try {
            lease.Entries = plan.Roots.SelectMany(r => r.Entries).ToDictionary(e => new PhaseSixSource(e.ObservationRootId, e.EntryId));
            if (lease.Entries.Values.Count(e => e.Kind == "REGULAR_FILE") > limits.MaximumFiles || lease.Entries.Values.Count(e => e.Kind == "DIRECTORY") > limits.MaximumDirectories ||
                plan.VirtualEntries.Count(e => e.Kind == "DIRECTORY") > limits.MaximumDirectories || lease.Entries.Values.Where(e => e.Kind == "REGULAR_FILE").Sum(e => e.Size!.Value) > limits.MaximumSourceBytes)
                throw new PhaseSevenLimitException("source/directory/byte budget exceeded");
            lease.CheckNamespace(budget);
            foreach (var directory in lease.Entries.Values.Where(e => e.Kind == "DIRECTORY")) {
                budget.Check(); var facts = PhaseTwoSafety.Inspect(directory.OriginalPath); lease.CheckExpected(directory, facts); lease.directories.Add(new(directory.OriginalPath));
            }
            var records = new List<PhaseSevenSourceRead>(); var buffer = new byte[65536];
            foreach (var (source, entry) in lease.Entries.Where(p => p.Value.Kind == "REGULAR_FILE").OrderBy(p => p.Key.RootId).ThenBy(p => p.Key.EntryId)) {
                budget.Check(); hooks?.Hit("SOURCE_READ", entry.OriginalPath);
                var stream = new FileStream(entry.OriginalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan); lease.Streams.Add(source, stream);
                var facts = PhaseTwoSafety.Inspect(stream.SafeFileHandle, entry.OriginalPath); lease.CheckExpected(entry, facts); lease.before.Add(source, facts);
                var digest = Hash(stream, buffer, budget, out var length);
                if (length != entry.Size || digest != entry.Sha256) throw new PhaseSevenChangedException(entry.OriginalPath);
                lease.RequireFile(source); records.Add(new(source, entry.OriginalPath, digest, length, facts));
            }
            lease.Records = records.ToArray(); lease.CheckNamespace(budget); return lease;
        } catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or ReparsePointException) {
            lease.Dispose(); throw new PhaseSevenChangedException(e.Message);
        } catch { lease.Dispose(); throw; }
    }
    internal static string Hash(Stream stream, byte[] buffer, PhaseSevenBudget budget, out long length)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); length = 0; int count;
        while ((count = stream.Read(buffer)) != 0) { budget.Read(count); length += count; hash.AppendData(buffer, 0, count); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private void CheckExpected(PhaseFiveEntry entry, NativePathFacts actual)
    {
        if (actual.Identity != entry.NativeIdentity || actual.Attributes != entry.Attributes || actual.MtimeTicks != entry.MtimeTicks ||
            entry.Kind == "REGULAR_FILE" && actual.Size != entry.Size || !PathPolicy.Contains(workspace.Library, entry.OriginalPath)) throw new PhaseSevenChangedException(entry.OriginalPath);
    }
    internal void RequireFile(PhaseSixSource source)
    {
        var entry = Entries[source]; var facts = PhaseTwoSafety.Inspect(Streams[source].SafeFileHandle, entry.OriginalPath);
        if (facts != before[source] || PhaseTwoSafety.Inspect(entry.OriginalPath) != facts) throw new PhaseSevenChangedException(entry.OriginalPath);
    }
    internal void CheckNamespace(PhaseSevenBudget budget)
    {
        workspace.RequireBinding();
        foreach (var root in plan.Roots) {
            var expected = root.Entries.ToDictionary(e => e.RelativePath, StringComparer.Ordinal); var found = new HashSet<string>(StringComparer.Ordinal); var stack = new Stack<string>(); stack.Push(root.OriginalPath);
            while (stack.TryPop(out var path)) {
                budget.Check(); PathPolicy.RejectReparseAncestors(path); var relative = Path.GetRelativePath(root.OriginalPath, path).Replace('\\', '/');
                if (!expected.TryGetValue(relative, out var entry) || !found.Add(relative)) throw new PhaseSevenChangedException("Unexpected source namespace entry: " + path);
                var facts = PhaseTwoSafety.Inspect(path); CheckExpected(entry, facts);
                if (entry.Kind == "DIRECTORY") foreach (var child in Directory.EnumerateFileSystemEntries(path)) {
                    budget.Check(); var childRelative = Path.GetRelativePath(root.OriginalPath,child).Replace('\\','/');
                    if (!expected.ContainsKey(childRelative)) throw new PhaseSevenChangedException("Unexpected source namespace entry: " + child);
                    stack.Push(child);
                }
            }
            if (found.Count != expected.Count) throw new PhaseSevenChangedException("Source namespace is incomplete.");
        }
    }
    internal void Recheck(PhaseSevenBudget budget)
    {
        CheckNamespace(budget); var buffer = new byte[65536];
        foreach (var (source, stream) in Streams) { RequireFile(source); stream.Position = 0; var digest = Hash(stream, buffer, budget, out var length); if (digest != Entries[source].Sha256 || length != Entries[source].Size) throw new PhaseSevenChangedException(Entries[source].OriginalPath); RequireFile(source); }
        CheckNamespace(budget);
    }
    public void Dispose() { foreach (var stream in Streams.Values) stream.Dispose(); foreach (var directory in directories) directory.Dispose(); }
}

internal sealed class PhaseSevenInputLease : IDisposable
{
    private readonly Dictionary<string, FileStream> streams = new(PathPolicy.Comparer);
    internal PhaseSevenInput[] Records { get; }
    internal PhaseSevenInputLease(PhaseSevenSyntheticWorkspace workspace, PhaseSevenBudget budget)
    {
        var records = new List<PhaseSevenInput>();
        try {
            foreach (var path in new[] { workspace.SourceDatabase, workspace.Evidence, workspace.PlanPath }) {
                budget.Check(); PhaseTwoSafety.RejectSidecars(path); var facts = PhaseTwoSafety.Inspect(path);
                workspace.RequireOwnedInput(path, facts);
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); streams.Add(path, stream);
                if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts) throw new IOException("Owned input changed before payload read.");
                var sha = PhaseSevenSourceLease.Hash(stream, new byte[65536], budget, out _);
                if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts || PhaseTwoSafety.Inspect(path) != facts) throw new IOException("Input evidence changed.");
                records.Add(new(path, sha, facts));
            }
            Records = records.ToArray();
        } catch { Dispose(); throw; }
    }
    internal void RequireUnchanged() {
        foreach (var record in Records) { PhaseTwoSafety.RejectSidecars(record.Path); if (PhaseTwoSafety.Inspect(streams[record.Path].SafeFileHandle, record.Path) != record.Facts || PhaseTwoSafety.Inspect(record.Path) != record.Facts) throw new IOException("Input evidence binding changed."); }
    }
    public void Dispose() { foreach (var stream in streams.Values) stream.Dispose(); }
}
