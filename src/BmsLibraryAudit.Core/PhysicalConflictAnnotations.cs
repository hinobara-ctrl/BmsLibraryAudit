namespace BmsLibraryAudit.Core;

// Physical view only: no component, package or semantic admission is implied.
internal sealed record PhysicalPath(long EntryId, long RootId, string RelativePath, string Kind,
    string? Sha256, string? NativeIdentity, bool IsChart);
internal sealed record PhysicalConflict(string RelativePath, string[] Kinds, long? EntryA, long? EntryB, long? RootA, long? RootB);
internal static class PhysicalConflictAnnotations
{
    internal static IEnumerable<PhysicalConflict> Derive(PhysicalPath[] a, PhysicalPath[] b,
        PhaseTwoContentComparison.Difference[] differences, CancellationToken token)
    {
        var ai = a.ToDictionary(e => e.EntryId); var bi = b.ToDictionary(e => e.EntryId);
        foreach (var d in differences) {
            token.ThrowIfCancellationRequested();
            if (d.Category == "CONTENT_CONFLICT") {
                var x = ai[d.EntryA!.Value]; var y = bi[d.EntryB!.Value];
                yield return new(d.Path, x.IsChart != y.IsChart ? ["SAME_RELATIVE_PATH_DIFFERENT_BYTES", "CHART_RESOURCE_PATH_CONFLICT"]
                    : !x.IsChart ? ["SAME_RELATIVE_PATH_DIFFERENT_BYTES", "RESOURCE_VARIANT"] : ["SAME_RELATIVE_PATH_DIFFERENT_BYTES"], x.EntryId, y.EntryId, x.RootId, y.RootId);
            }
            if (d.Category == "KIND_CONFLICT") yield return new(d.Path, ["FILE_DIRECTORY_KIND_CONFLICT"], d.EntryA, d.EntryB, ai[d.EntryA!.Value].RootId, bi[d.EntryB!.Value].RootId);
        }
        foreach (var group in a.Concat(b).GroupBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)) {
            token.ThrowIfCancellationRequested();
            if (group.Select(e => e.RelativePath).Distinct(StringComparer.Ordinal).Skip(1).Any()) {
                var entries = group.OrderBy(e => e.RootId).ThenBy(e => e.RelativePath, StringComparer.Ordinal).ToArray();
                var other = entries.First(e => e.RelativePath != entries[0].RelativePath);
                yield return new(group.Key, ["CASE_COLLISION"], entries[0].EntryId, other.EntryId, entries[0].RootId, other.RootId);
            }
        }
        foreach (var group in a.Concat(b).Where(e => e.Kind == "REGULAR_FILE" && e.NativeIdentity is not null).GroupBy(e => e.NativeIdentity, StringComparer.Ordinal)) {
            token.ThrowIfCancellationRequested();
            if (group.Select(e => e.Sha256).Distinct(StringComparer.Ordinal).Skip(1).Any()) {
                var entries = group.ToArray(); var other = entries.First(e => e.Sha256 != entries[0].Sha256);
                yield return new(entries[0].RelativePath, ["POTENTIAL_NATIVE_ALIAS_CONFLICT"],
                    entries[0].EntryId, other.EntryId, entries[0].RootId, other.RootId);
            }
        }
    }
}
