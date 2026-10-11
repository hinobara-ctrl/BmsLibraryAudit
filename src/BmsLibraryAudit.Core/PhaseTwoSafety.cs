using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BmsLibraryAudit.Core;

public sealed record NativePathFacts(string ResolvedPath, string Identity, uint LinkCount,
    long Size, long MtimeTicks, uint Attributes);

// Phase 2 requires handle-based Windows alias/identity checks. Other platforms
// are deliberately refused until an equivalent exclusion implementation exists.
public static class PhaseTwoSafety
{
    private static string Ordinary(string path)
    {
        path = PathPolicy.Absolute(path);
        var root = Path.GetPathRoot(path) ?? throw new ArgumentException("Missing absolute path root.");
        if (path[root.Length..].Contains(':', StringComparison.Ordinal))
            throw new ArgumentException("Alternate data streams/device-style names are not ordinary Phase 2 paths.");
        return path;
    }
    public static void RequirePlatform()
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Phase 2A requires Windows native identity and exclusion support.");
    }

    public static NativePathFacts Inspect(string path)
    {
        RequirePlatform();
        path = Ordinary(path);
        PathPolicy.RejectReparseAncestors(path);
        using var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) throw NativeError(path);
        for (var ancestor = Path.GetDirectoryName(path); ancestor is not null; ancestor = Path.GetDirectoryName(ancestor))
        {
            using var parent = CreateFile(ancestor, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            if (parent.IsInvalid) throw NativeError(ancestor);
            _ = Inspect(parent, ancestor);
        }
        return Inspect(handle, path);
    }

    public static NativePathFacts Inspect(SafeFileHandle handle, string path)
    {
        RequirePlatform();
        if (GetFileType(handle) != 1) throw new IOException("Non-disk/special filesystem handle: " + path);
        if (!GetFileInformationByHandle(handle, out var info)) throw NativeError(path);
        if ((info.Attributes & (uint)FileAttributes.ReparsePoint) != 0)
            throw new ReparsePointException(path);
        if ((info.Attributes & (uint)FileAttributes.Directory) != 0)
        {
            if (!GetFileInformationByHandleEx(handle, 23, out var flags, 4)) throw NativeError(path);
            if ((flags & 1) != 0) throw new IOException("Case-sensitive Windows directories are unsupported: " + path);
        }
        var capacity = 512;
        string resolved;
        while (true)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)capacity, 1);
            if (length == 0) throw NativeError(path);
            if (length < capacity) { resolved = buffer.ToString().TrimEnd('\\'); break; }
            capacity = checked((int)length + 1);
        }
        var identity = FormattableString.Invariant($"{info.VolumeSerial:x8}:{info.FileIndexHigh:x8}{info.FileIndexLow:x8}");
        return new(resolved.ToUpperInvariant(), identity, info.Links,
            checked(((long)info.SizeHigh << 32) | info.SizeLow),
            DateTime.FromFileTimeUtc(((long)info.WriteTimeHigh << 32) | info.WriteTimeLow).Ticks, info.Attributes);
    }

    public static string Resolve(string path)
    {
        RequirePlatform();
        path = Ordinary(path);
        PathPolicy.RejectReparseAncestors(path);
        var suffix = new Stack<string>();
        var ancestor = path;
        while (!Exists(ancestor))
        {
            suffix.Push(Path.GetFileName(ancestor));
            ancestor = Path.GetDirectoryName(ancestor)
                ?? throw new IOException("Cannot resolve existing ancestor: " + path);
        }
        var resolved = Inspect(ancestor).ResolvedPath;
        while (suffix.TryPop(out var part)) resolved += "\\" + part.ToUpperInvariant();
        return resolved;
    }

    private static bool ContainsResolved(string directory, string path) =>
        path == directory || path.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.Ordinal);

    public static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    public static void RejectSidecars(string path)
    {
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
        {
            PathPolicy.RejectReparseAncestors(path + suffix);
            if (Exists(path + suffix))
                throw new InvalidOperationException("Existing SQLite sidecar requires external inspection: " + path + suffix);
        }
    }

    public static void ValidateDestinations(string sourceDb, string phaseDb, string output, IEnumerable<string> roots)
    {
        RequirePlatform();
        var source = Resolve(sourceDb);
        var db = Resolve(phaseDb);
        var report = Resolve(output);
        if (!string.Equals(Path.GetExtension(phaseDb), ".db", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Phase 2 database must have a .db extension.");
        if (db == source || ContainsResolved(report, source) || ContainsResolved(report, db) || db == report)
            throw new InvalidOperationException("Source/database/report paths overlap.");
        foreach (var root in roots)
        {
            var resolvedRoot = Resolve(root);
            if (ContainsResolved(resolvedRoot, db) || ContainsResolved(resolvedRoot, report) || ContainsResolved(report, resolvedRoot))
                throw new InvalidOperationException("Destination overlaps a current or historical source root: " + root);
        }
        if (Exists(phaseDb))
        {
            var facts = Inspect(phaseDb);
            if ((facts.Attributes & (uint)FileAttributes.Directory) != 0 || facts.LinkCount != 1 ||
                (Exists(sourceDb) && facts.Identity == Inspect(sourceDb).Identity))
                throw new InvalidOperationException("Unsafe writable database identity/hardlink alias.");
        }
        RejectSidecars(phaseDb);
        if (Exists(output) && (!Directory.Exists(output) || Directory.EnumerateFileSystemEntries(output).Any()))
            throw new InvalidOperationException("Report output must be a new or empty ordinary directory.");
    }

    private static IOException NativeError(string path) => new("Native identity check failed for " + path,
        new Win32Exception(Marshal.GetLastWin32Error()));

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleInfo
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh,
            WriteTimeLow, WriteTimeHigh, VolumeSerial, SizeHigh, SizeLow, Links,
            FileIndexHigh, FileIndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint sharing, IntPtr security,
        uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleInfo info);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out uint flags, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetFinalPathNameByHandleW")]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
}
