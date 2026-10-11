using System.Buffers;
using System.Security.Cryptography;

namespace BmsLibraryAudit.Core;

public sealed class PhaseTwoFileSystem : IPhaseTwoFileSystem
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
}
