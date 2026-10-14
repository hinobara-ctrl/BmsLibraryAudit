using System.Buffers;
using System.Security.Cryptography;

namespace BmsLibraryAudit.Core;

public sealed class PhaseTwoFileSystem : IPhaseTwoContentFileSystem
{
    public static bool IsChart(string path) => Path.GetExtension(path).ToLowerInvariant()
        is ".bms" or ".bme" or ".bml" or ".pms" or ".bmson";

    public PhaseTwoEntry Metadata(string path)
    {
        path = PathPolicy.Absolute(path);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return new(path, "REPARSE", null, null, (long)attributes, null, null, "REPARSE", "NOT_TESTED", null);
        var facts = PhaseTwoSafety.Inspect(path);
        var directory = (attributes & FileAttributes.Directory) != 0;
        if ((attributes & FileAttributes.Device) != 0) throw new IOException("Nonregular entry: " + path);
        return new(path, directory ? "DIRECTORY" : "REGULAR_FILE", directory ? null : facts.Size,
            facts.MtimeTicks, (long)facts.Attributes, directory ? null : Path.GetExtension(path),
            directory ? null : IsChart(path), "OK", "NOT_TESTED", facts.Identity);
    }

    public IEnumerable<string> Children(string directory)
    {
        PathPolicy.RejectReparseAncestors(directory);
        _ = PhaseTwoSafety.Inspect(directory);
        return Directory.EnumerateFileSystemEntries(directory);
    }

    public PhaseTwoChartRead ReadChart(string path, CancellationToken cancellationToken)
    {
        if (!IsChart(path)) throw new ArgumentException("Only direct charts may be payload-read in Phase 2A.");
        long bytes = 0;
        PhaseTwoEntry? before = null, after = null;
        var attempts = 0;
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                attempts = attempt;
                cancellationToken.ThrowIfCancellationRequested();
                before = Metadata(path);
                if (before.Kind != "REGULAR_FILE")
                    return Result("REPARSE", null);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.SequentialScan);
                var handleFacts = PhaseTwoSafety.Inspect(stream.SafeFileHandle, path);
                if (handleFacts.Identity != before.NativeIdentity || handleFacts.Size != before.Size ||
                    handleFacts.MtimeTicks != before.MtimeTicks) continue;
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                long attemptBytes = 0;
                try
                {
                    int count;
                    while ((count = stream.Read(buffer, 0, 64 * 1024)) != 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        hash.AppendData(buffer, 0, count);
                        attemptBytes = checked(attemptBytes + count);
                    }
                }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
                bytes = checked(bytes + attemptBytes);
                after = Metadata(path);
                var finalHandle = PhaseTwoSafety.Inspect(stream.SafeFileHandle, path);
                if (before == after && handleFacts == finalHandle && attemptBytes == before.Size)
                    return Result("SUCCESS", Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
            }
            return Result("UNSTABLE", null);
        }
        catch (FileNotFoundException) { return Result("MISSING", null); }
        catch (DirectoryNotFoundException) { return Result("MISSING", null); }
        catch (ReparsePointException) { return Result("REPARSE", null); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return Result("UNREADABLE", null); }

        PhaseTwoChartRead Result(string status, string? sha) => new(before?.Size, before?.MtimeTicks,
            after?.Size, after?.MtimeTicks, sha, bytes, attempts, status);
    }

    public PhaseTwoContentRead ReadContent(PhaseTwoEntry expected, byte[] buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length != 64 * 1024) throw new ArgumentException("Content reads require the reusable 64 KiB buffer.");
        var path = expected.OriginalPath;
        var started = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        long bytes = 0; int attempts = 0;
        PhaseTwoEntry? before = null, after = null;
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                attempts = attempt; cancellationToken.ThrowIfCancellationRequested();
                before = Metadata(path);
                if (before != expected || before.Kind != "REGULAR_FILE") throw new PhaseTwoStaleException(path);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.SequentialScan);
                var facts = PhaseTwoSafety.Inspect(stream.SafeFileHandle, path);
                if (facts.Identity != expected.NativeIdentity || facts.Size != expected.Size ||
                    facts.MtimeTicks != expected.MtimeTicks || facts.Attributes != expected.Attributes)
                    throw new PhaseTwoStaleException(path);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long attemptBytes = 0; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    bytes = checked(bytes + count); attemptBytes = checked(attemptBytes + count);
                    cancellationToken.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count);
                }
                cancellationToken.ThrowIfCancellationRequested();
                after = Metadata(path);
                if (after != expected || PhaseTwoSafety.Inspect(stream.SafeFileHandle, path) != facts)
                    throw new PhaseTwoStaleException(path);
                if (attemptBytes == expected.Size)
                    return Result("SUCCESS", Convert.ToHexStringLower(hash.GetHashAndReset()));
                // A short/unstable read may retry only against the unchanged accepted stamp.
            }
            return Result("UNSTABLE", null);
        }
        catch (PhaseTwoStaleException) { throw; }
        catch (ReparsePointException) { throw new PhaseTwoStaleException(path); }
        catch (FileNotFoundException) { throw new PhaseTwoStaleException(path); }
        catch (DirectoryNotFoundException) { throw new PhaseTwoStaleException(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Content-only failure is local only if metadata/namespace safety is still provable.
            try { after = Metadata(path); }
            catch (Exception metadataError) when (metadataError is IOException or UnauthorizedAccessException)
            { throw new PhaseTwoStaleException(path); }
            if (before != expected || after != expected) throw new PhaseTwoStaleException(path);
            return Result("ERROR", null);
        }
        PhaseTwoContentRead Result(string status, string? digest) => new(before, after, digest, bytes, attempts,
            status, started, DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }
}
