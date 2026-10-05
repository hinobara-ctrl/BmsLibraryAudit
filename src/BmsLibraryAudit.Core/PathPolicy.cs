namespace BmsLibraryAudit.Core;

public sealed class ReparsePointException(string path) : IOException($"Reparse point is not permitted: {path}");

public static class PathPolicy
{
    public static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public static StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static string Absolute(string path)
    {
        if (OperatingSystem.IsWindows() && (path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\??\", StringComparison.Ordinal)))
            throw new ArgumentException("Windows device/extended path aliases are not supported; use a regular absolute path.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
    public static string Normalize(string path) => OperatingSystem.IsWindows()
        ? Absolute(path).ToUpperInvariant() : Absolute(path);
    public static bool Contains(string directory, string path)
    {
        directory = Absolute(directory);
        path = Absolute(path);
        return string.Equals(directory, path, Comparison) ||
            path.StartsWith(Path.EndsInDirectorySeparator(directory)
                ? directory : directory + Path.DirectorySeparatorChar, Comparison);
    }

    // Reject aliases in every existing component, including configured roots.
    public static void RejectReparseAncestors(string path)
    {
        for (string? current = Absolute(path); current is not null;
             current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ReparsePointException(current);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    public static void RequireOutsideRoots(string path, IEnumerable<string> roots)
    {
        foreach (var root in roots)
            if (Contains(root, path))
                throw new InvalidOperationException($"Output must be outside library root '{root}': {path}");
        RejectReparseAncestors(path);
    }

    public static IReadOnlyList<string> ValidateRoots(IReadOnlyList<string> roots)
    {
        if (roots.Count == 0) throw new ArgumentException("At least one --root is required.");
        var result = roots.Select(Absolute).Distinct(Comparer).Order(StringComparer.Ordinal).ToArray();
        foreach (var root in result)
        {
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Library root does not exist: {root}");
            RejectReparseAncestors(root);
        }
        for (var i = 0; i < result.Length; i++)
            for (var j = i + 1; j < result.Length; j++)
                if (Contains(result[i], result[j]) || Contains(result[j], result[i]))
                    throw new ArgumentException("Overlapping roots are not supported; configure the outer root only.");
        return result;
    }
}
