using System.Buffers;
using System.Security.Cryptography;

namespace BmsLibraryAudit.Core;

public sealed record FileStamp(long Size, long MtimeUtcTicks);
public sealed record ChartDigest(FileStamp Stamp, string Md5, string Sha256);
public sealed class UnstableChartException(string path) : IOException($"Chart changed during both hash attempts: {path}");

public static class ChartHasher
{
    public static FileStamp Stat(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists) throw new FileNotFoundException("Chart no longer exists.", path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Chart is a reparse point: {path}");
        return new(info.Length, info.LastWriteTimeUtc.Ticks);
    }

    public static ChartDigest Hash(string path, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = Stat(path);
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            long bytes = 0;
            try
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    md5.AppendData(buffer, 0, read);
                    sha256.AppendData(buffer, 0, read);
                    bytes += read;
                }
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
            var after = Stat(path);
            if (before == after && bytes == after.Size)
                return new(after, Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant(),
                    Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant());
        }
        throw new UnstableChartException(path);
    }
}
