namespace BmsLibraryAudit.SourceInspector.Offline;

// Lexical Windows semantics only. No System.IO path resolution or native alias claim.
public static class WindowsNames
{
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;
    public static string Canonical(string text)
    {
        var s = text.Replace('/', '\\');
        if (s.Length < 3 || !char.IsAsciiLetter(s[0]) || s[1] != ':' || s[2] != '\\')
            throw new EvidenceException(FindingKind.Invalid, "ORDINARY_ABSOLUTE_PATH_REQUIRED");
        var parts = s[3..].Split('\\');
        if (parts.Length > 0 && parts[^1] == "") parts = parts[..^1];
        foreach (var part in parts)
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(c => c < 32 || ":<>\"|?*".Contains(c)))
                throw new EvidenceException(FindingKind.Invalid, "AMBIGUOUS_PATH_COMPONENT");
            var stem = part.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9'))
                throw new EvidenceException(FindingKind.Invalid, "DEVICE_NAME");
        }
        return s[..3] + string.Join('\\', parts);
    }
    public static string Key(string text) => Canonical(text).ToUpperInvariant();
    public static bool Contains(string root, string path)
    {
        var a = Canonical(root); var b = Canonical(path);
        return Comparer.Equals(a, b) || b.StartsWith(a.EndsWith('\\') ? a : a + "\\", StringComparison.OrdinalIgnoreCase);
    }
    public static string Parent(string path)
    {
        var s = Canonical(path); var index = s.LastIndexOf('\\');
        if (s.Length == 3) throw new EvidenceException(FindingKind.Invalid, "FILE_PATH_REQUIRED");
        return index == 2 ? s[..3] : s[..index];
    }
    public static string Name(string path) { var s = Canonical(path); return s[(s.LastIndexOf('\\') + 1)..]; }
    public static string Extension(string path) { var name = Name(path); var dot = name.LastIndexOf('.'); return dot < 0 ? "" : name[dot..]; }
}
