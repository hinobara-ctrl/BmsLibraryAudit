using System.Text.Json;

namespace BmsLibraryAudit.Core;

internal static class PhaseSevenStageVerifier
{
    // Enumerates and hashes actual output, independent of builder copy records.
    internal static PhaseSevenTarget[] Inspect(PhaseSevenSyntheticWorkspace workspace, PhaseSixEnvelope admitted,
        PhaseSevenSourceLease sources, PhaseSevenLimits limits, PhaseSevenBudget budget)
    {
        workspace.RequireStage(); var expected = admitted.Plan.VirtualEntries.ToDictionary(e => e.RelativePath, StringComparer.Ordinal);
        var found = new Dictionary<string, PhaseSevenTarget>(StringComparer.Ordinal); var nativeFiles = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(); stack.Push(workspace.Stage); var buffer = new byte[65536];
        while (stack.TryPop(out var path)) {
            budget.Check(); workspace.RequireStage(); PathPolicy.RejectReparseAncestors(path);
            var relative = Path.GetRelativePath(workspace.Stage, path).Replace('\\', '/');
            if (!expected.TryGetValue(relative, out var target) || !string.Equals(workspace.TargetPath(relative), path, StringComparison.Ordinal)) throw new IOException("Unexpected staging path: " + relative);
            var facts = PhaseTwoSafety.Inspect(path); var directory = (facts.Attributes & (uint)FileAttributes.Directory) != 0;
            if (directory != (target.Kind == "DIRECTORY")) throw new IOException("Staging entry kind mismatch: " + relative);
            if (!directory) {
                if (facts.LinkCount != 1 || !nativeFiles.Add(facts.Identity) || sources.Entries.Values.Any(e => e.NativeIdentity == facts.Identity)) throw new IOException("Staging file is a protected/shared native alias.");
                var expectedSize = sources.Entries[target.Sources[0]].Size!.Value;
                if (facts.Size != expectedSize) throw new IOException("Staging file size mismatch: " + relative);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts) throw new IOException("Staging binding changed before verification.");
                var digest = PhaseSevenSourceLease.Hash(stream, buffer, budget, out var read);
                if (read != expectedSize || digest != target.Sha256 || PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts || PhaseTwoSafety.Inspect(path) != facts) throw new IOException("Staging payload/binding verification failed: " + relative);
                found.Add(relative, new(relative, "REGULAR_FILE", read, digest, facts));
            } else {
                found.Add(relative, new(relative, "DIRECTORY", null, null, facts));
                if (found.Values.Count(t => t.Kind == "DIRECTORY") > limits.MaximumDirectories) throw new PhaseSevenLimitException("target directory budget exceeded");
                foreach (var child in Directory.EnumerateFileSystemEntries(path)) {
                    budget.Check(); var childRelative = Path.GetRelativePath(workspace.Stage,child).Replace('\\','/');
                    if (!expected.ContainsKey(childRelative)) throw new IOException("Unexpected staging path: " + childRelative);
                    stack.Push(child);
                }
            }
            if (found.Count > expected.Count) throw new IOException("Extra stage entries.");
        }
        if (!found.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys)) throw new IOException("Stage is missing expected entries.");
        sources.Recheck(budget); workspace.RequireStage();
        return found.Values.OrderBy(t => t.RelativePath, StringComparer.Ordinal).ToArray();
    }

    internal static PhaseSevenEnvelope Verify(PhaseSevenSyntheticWorkspace workspace, PhaseSevenEnvelope envelope, CancellationToken token = default)
    {
        var receipt = envelope.Receipt; PhaseSevenBudget.Validate(receipt.Limits); var budget = new PhaseSevenBudget(receipt.Limits, token); budget.Check();
        if (receipt.Version != PhaseSevenFormat.Version || envelope.ReceiptSha256 != PhaseSevenFormat.Digest(receipt) ||
            receipt.State != "STAGING_PHYSICALLY_VERIFIED" || receipt.SemanticEvidence != "UNKNOWN" || receipt.PackageEquivalenceProven ||
            receipt.AllDependenciesProven || receipt.RelocationSafe || receipt.Published || receipt.OriginalsRemovable || receipt.Errors.Length != 0 || receipt.ElapsedMilliseconds < 0 ||
            receipt.ElapsedMilliseconds > receipt.Limits.MaximumSeconds * 1000L || receipt.Notice != PhaseSevenFormat.Notice ||
            JsonSerializer.SerializeToUtf8Bytes(envelope, PhaseSixFormat.Pretty).Length + 1 > receipt.Limits.MaximumReceiptBytes)
            throw new InvalidOperationException("Receipt state/digest/authority is not admissible.");
        using var inputs = new PhaseSevenInputLease(workspace, budget);
        var plan = workspace.AdmitPlan();
        if (receipt.PlanSha256 != plan.PlanSha256 || !PhaseSixFormat.Equal(receipt.Provenance, plan.Plan.Provenance) ||
            receipt.Workspace != workspace.Root || receipt.Stage != workspace.Stage || receipt.WorkspaceFacts != workspace.Facts || receipt.StageFacts != workspace.StageIdentity)
            throw new InvalidOperationException("Receipt workspace/plan provenance mismatch.");
        using var evidence = PhaseSixEvidenceAdmission.Load(workspace.Options, new(), token);
        using var sources = PhaseSevenSourceLease.Open(workspace, plan.Plan, receipt.Limits, budget);
        var actual = Inspect(workspace, plan, sources, receipt.Limits, budget);
        var copies = plan.Plan.VirtualEntries.Where(t => t.Kind == "REGULAR_FILE").Select(t => {
            var chosen = t.Sources.OrderBy(s => s.RootId).ThenBy(s => s.EntryId).First();
            return new PhaseSevenCopy(t.RelativePath, chosen, t.Sources, t.Sha256!, t.Sha256!, sources.Entries[chosen].Size!.Value);
        }).ToArray();
        if (copies.Sum(c => c.BytesCopied) > receipt.Limits.MaximumCopyBytes) throw new PhaseSevenLimitException("receipt copy budget inconsistent with required target");
        if (!PhaseSixFormat.Equal(receipt.Sources, sources.Records) || !PhaseSixFormat.Equal(receipt.Inputs, inputs.Records) ||
            !PhaseSixFormat.Equal(receipt.Targets, actual) || !PhaseSixFormat.Equal(receipt.Copies, copies) ||
            receipt.BytesCopied != copies.Sum(c => c.BytesCopied) || receipt.Files != actual.Count(t => t.Kind == "REGULAR_FILE") || receipt.Directories != actual.Count(t => t.Kind == "DIRECTORY") ||
            !receipt.Events.SequenceEqual(new[] { "PLAN_VERIFIED", "SOURCE_REVALIDATION_PASSED", "STAGING_CONSTRUCTED", "STAGING_PHYSICALLY_VERIFIED" }, StringComparer.Ordinal))
            throw new InvalidOperationException("Receipt claims disagree with independent physical reconstruction.");
        inputs.RequireUnchanged(); evidence.RequireBinding(); budget.Check(); return envelope;
    }
    internal static PhaseSevenEnvelope ReadReceipt(PhaseSevenSyntheticWorkspace workspace)
    {
        workspace.RequireBinding(); var path = Path.Combine(workspace.ReceiptDirectory, "stage_receipt.json"); PathPolicy.RejectReparseAncestors(path);
        var facts = PhaseTwoSafety.Inspect(path);
        if (facts.LinkCount != 1 || facts.Size > new PhaseSevenLimits().MaximumReceiptBytes) throw new InvalidOperationException("Unsafe/oversize receipt.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts) throw new IOException("Receipt changed before read.");
        var bytes = new byte[checked((int)facts.Size)]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts || PhaseTwoSafety.Inspect(path) != facts) throw new IOException("Receipt changed during read.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 }); PhaseFourResolver.RejectDuplicateProperties(document.RootElement);
        return JsonSerializer.Deserialize<PhaseSevenEnvelope>(bytes, PhaseSixFormat.Canonical) ?? throw new InvalidOperationException("Missing stage receipt.");
    }
}
