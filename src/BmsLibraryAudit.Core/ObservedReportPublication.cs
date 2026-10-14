namespace BmsLibraryAudit.Core;

// Shared exclusive staging, identity-bound cleanup and no-overwrite publication.
internal static class ObservedReportPublication
{
    internal static void Publish(string outputDirectory, string filename, byte[] payload,
        Action<string?, bool> validate, CancellationToken token, PhaseThreePublicationTestHooks? hooks = null)
    {
        token.ThrowIfCancellationRequested(); validate(null, true);
        Directory.CreateDirectory(outputDirectory); var parent = PhaseTwoSafety.Inspect(outputDirectory);
        PathPolicy.RejectReparseAncestors(outputDirectory);
        var report = Path.Combine(outputDirectory, filename);
        var temporary = Path.Combine(outputDirectory, "." + Path.GetFileNameWithoutExtension(filename) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        NativePathFacts? ownedTemporary = null; var published = false;
        try
        {
            validate(null, true); RequireParent(outputDirectory, parent);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownedTemporary = PhaseTwoSafety.Inspect(stream.SafeFileHandle, temporary);
                RequireParent(outputDirectory, parent); RequireTemporaryPath(ownedTemporary, parent, temporary);
                hooks?.AfterTemporaryCreated?.Invoke(); token.ThrowIfCancellationRequested();
                validate(temporary, true); RequireParent(outputDirectory, parent);
                RequireTemporaryPath(PhaseTwoSafety.Inspect(stream.SafeFileHandle, temporary), parent, temporary);
                // A bounded first chunk supplies a precise internal fault-injection
                // point while the final filename remains absent.
                var first = Math.Min(payload.Length / 2, 65536); stream.Write(payload.AsSpan(0, first)); stream.Flush();
                hooks?.AfterFirstWrite?.Invoke(); token.ThrowIfCancellationRequested();
                validate(temporary, true); RequireParent(outputDirectory, parent);
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
            validate(temporary, true); RequireParent(outputDirectory, parent);
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
            if (!published && ownedTemporary is not null) CleanupTemporary(outputDirectory, temporary, parent, ownedTemporary, validate);
        }
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
    private static void CleanupTemporary(string output, string temporary, NativePathFacts parent, NativePathFacts owned, Action<string?, bool> validate)
    {
        try
        {
            // Cleanup never follows a changed namespace or deletes a replacement
            // file, pre-existing report, or any directory. A lost binding is left
            // untouched for inspection rather than guessed/recovered.
            validate(temporary, false); RequireParent(output, parent);
            if (!PhaseTwoSafety.Exists(temporary)) return;
            var current = PhaseTwoSafety.Inspect(temporary); RequireTemporaryPath(current, parent, temporary);
            if (current.Identity == owned.Identity) File.Delete(temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
    }
}
