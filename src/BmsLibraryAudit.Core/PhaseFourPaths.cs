using System.Text.RegularExpressions;

namespace BmsLibraryAudit.Core;

internal sealed class PhaseFourPaths
{
    internal sealed record Match(string? RelativePath, string Policy, string State, ObservedResourceCandidate[] Candidates, string[] Limitations);
    private readonly Dictionary<string, ObservedResourceCandidate[]> paths;
    private readonly Dictionary<string, ObservedResourceCandidate[]> stems;
    private readonly PhaseFourLimits limits;
    private int links;
    internal bool Limited { get; private set; }
    internal PhaseFourPaths(IEnumerable<ObservedResourceCandidate> inventory, PhaseFourLimits limits)
    {
        this.limits = limits;
        paths = inventory.GroupBy(f => Slashes(f.RelativePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        stems = inventory.GroupBy(f => Stem(Slashes(f.RelativePath)), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
    }
    internal static string Slashes(string text) => text.Replace('\\', '/');
    private static string Stem(string text)
    {
        var dot = text.LastIndexOf('.'); return dot > text.LastIndexOf('/') ? text[..dot] : text;
    }
    internal Match Resolve(string relativeBase, string? authored)
    {
        if (authored is null) return new(null, "NONE", "UNSUPPORTED_RESOLUTION", [], ["Non-path declaration; timing/logical value is not a physical resource."]);
        if (authored.Length == 0) return new(null, "NONE", "UNKNOWN", [], ["Empty authored resource path."]);
        if (authored.StartsWith('/') || authored.StartsWith('\\') || Regex.IsMatch(authored, "^[A-Za-z]:", RegexOptions.CultureInvariant))
            return new(null, "NO_ABSOLUTE_OR_DEVICE_LOOKUP", "OUT_OF_SCOPE", [], ["Absolute, rooted, UNC and device paths are not admitted to this observation root."]);
        var pieces = Slashes(authored).Split('/');
        if (pieces.Any(p => p.Length == 0 || p is not ("." or "..") &&
            (p.EndsWith('.') || p.EndsWith(' ') || p.Any(c => c < 32 || "<>:\"|?*".Contains(c)) ||
            Regex.IsMatch(p, "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            Regex.IsMatch(p, "~[0-9]", RegexOptions.CultureInvariant))))
            return new(null, "NO_AMBIGUOUS_WIN32_REWRITE", "UNSUPPORTED_RESOLUTION", [], ["Quotes, ADS, reserved/invalid names, terminal dots/spaces or potential short-name aliases require unsupported interpretation."]);
        var parts = relativeBase.Length == 0 ? new List<string>() : Slashes(relativeBase).Split('/').ToList();
        foreach (var piece in pieces)
        {
            if (piece == ".") continue;
            if (piece == "..")
            {
                if (parts.Count == 0) return new(null, "LEXICAL_RELATIVE_V1", "OUT_OF_SCOPE", [], ["Parent traversal escapes the explicitly selected observation root."]);
                parts.RemoveAt(parts.Count - 1);
            }
            else parts.Add(piece);
        }
        var relative = string.Join('/', parts);
        if (paths.TryGetValue(relative, out var matches))
        {
            var exact = matches.Where(f => Slashes(f.RelativePath) == relative).ToArray();
            // Preserve all equivalent Windows spellings; no first-wins decision.
            var policy = exact.Length == matches.Length ? "LEXICAL_ORDINAL" : "WINDOWS_ORDINAL_IGNORE_CASE";
            return Bound(relative, policy, matches.Length == 1 ? "OBSERVED_PATH_MATCH" : "AMBIGUOUS_PATH_MATCH", matches,
                ["Historical inventory correspondence only; simulator lookup/necessity and current filesystem state remain unproved."]);
        }
        if (stems.TryGetValue(Stem(relative), out var alternatives))
            return Bound(relative, "ALTERNATE_EXTENSION_UNPROVEN", "UNSUPPORTED_RESOLUTION", alternatives,
                ["Same stem with another/omitted extension is a proposal only; no extension substitution rule is applied."]);
        return new(relative, "LEXICAL_RELATIVE_V1", "NO_OBSERVED_MATCH", [],
            ["No match under this literal scoped policy; external aliases, implicit extensions or other player behavior could differ. Absolute absence is not established."]);
    }
    private Match Bound(string relative, string policy, string state, ObservedResourceCandidate[] candidates, string[] limitations)
    {
        var available = Math.Max(0, Math.Min(limits.MaximumCandidatesPerReference, limits.MaximumCandidateLinks - links));
        var retained = candidates.Take(available).Select(c => c with { MatchPolicy = policy }).ToArray(); links += retained.Length;
        if (retained.Length != candidates.Length)
        { Limited = true; state = "UNKNOWN"; limitations = [.. limitations, "CANDIDATE_LIMIT: alternatives omitted from links; no unique binding asserted."]; }
        return new(relative, policy, state, retained, limitations);
    }
}
