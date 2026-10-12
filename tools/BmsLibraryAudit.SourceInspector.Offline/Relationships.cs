namespace BmsLibraryAudit.SourceInspector.Offline;

public static class Relationships
{
    public static IReadOnlyList<string> Labels { get; } = Array.AsReadOnly(new[] { "EXACT_CHART_SET", "A_SUBSET_OF_B", "B_SUBSET_OF_A", "HIGH_OVERLAP", "MEDIUM_OVERLAP", "SHARED_CHARTS" });
    public static string Classify(int a, int b, int shared)
    {
        if (a <= 0 || b <= 0 || shared <= 0 || shared > Math.Min(a, b)) throw new ArgumentOutOfRangeException(nameof(shared));
        if (shared == a) return a == b ? Labels[0] : Labels[1];
        if (shared == b) return Labels[2];
        // Exact rational comparison, never binary floating point for admission.
        var larger = (long)Math.Max(a, b);
        return 5L * shared >= 4L * larger ? Labels[3] : 2L * shared >= larger ? Labels[4] : Labels[5];
    }
    public static Analysis Analyze(IReadOnlyList<Chart> charts, IReadOnlyList<Diagnostic> diagnostics,
        ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        policy.Validate(); cancellation.ThrowIfCancellationRequested();
        policy.Limit(charts.Count > policy.MaxChartRecords || diagnostics.Count > policy.MaxReportRows, "ROW_LIMIT");
        var present = new List<Chart>(); var invalidFolders = new HashSet<string>(WindowsNames.Comparer);
        var blocked = new List<string>();
        foreach (var d in diagnostics)
        {
            cancellation.ThrowIfCancellationRequested(); policy.Text(d.Path); policy.Text(d.Code);
            if (!Provenance.DiagnosticCodes.Contains(d.Code, StringComparer.Ordinal)) throw new EvidenceException(FindingKind.Pending, "DIAGNOSTIC_SEMANTICS_UNKNOWN");
            blocked.Add(d.Code is "DIRECTORY_UNREADABLE" or "REPARSE_SKIPPED" ? WindowsNames.Canonical(d.Path) : WindowsNames.Parent(d.Path));
        }
        var allFolders = new HashSet<string>(WindowsNames.Comparer);
        var displayNames = new Dictionary<string, string>(WindowsNames.Comparer);
        var chartKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in charts)
        {
            cancellation.ThrowIfCancellationRequested(); policy.Text(c.Path); policy.Text(c.Parent); policy.Text(c.Key);
            if (c.Present is not (0 or 1) || c.Status is not ("OK" or "ERROR" or "UNSTABLE" or "MISSING" or "UNVERIFIED" or "INACTIVE") ||
                c.Key != WindowsNames.Key(c.Path) || !chartKeys.Add(c.Key) || !WindowsNames.Comparer.Equals(WindowsNames.Canonical(c.Parent), WindowsNames.Parent(c.Path)))
                throw new EvidenceException(FindingKind.Invalid, "CHART_LOCAL_PROVENANCE");
            if (c.Present != 1) continue;
            var folder = WindowsNames.Canonical(c.Parent);
            allFolders.Add(folder); policy.Limit(allFolders.Count > policy.MaxFolders, "FOLDER_LIMIT");
            if (!displayNames.TryGetValue(folder, out var display) || StringComparer.Ordinal.Compare(c.Parent, display) < 0) displayNames[folder] = c.Parent;
            if (c.Status != "OK") invalidFolders.Add(folder);
            else
            {
                if (!Provenance.Digest(c.Sha256, 64)) throw new EvidenceException(FindingKind.Invalid, "CHART_HASH");
                present.Add(c);
            }
        }
        // Ancestor membership is lexical and bounded by field length, rather than folders x diagnostics.
        var excluded = new HashSet<string>(blocked, WindowsNames.Comparer);
        foreach (var folder in allFolders)
        {
            cancellation.ThrowIfCancellationRequested();
            var p = folder;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                if (excluded.Contains(p)) { invalidFolders.Add(folder); break; }
                if (p.Length == 3) break;
                p = WindowsNames.Parent(p);
            }
        }
        var duplicates = present.GroupBy(c => c.Sha256!, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Where(g => g.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => new DuplicateGroup(g.Key, g.OrderBy(c => c.Key, StringComparer.Ordinal).ToArray())).ToArray();
        var folders = present.Where(c => !invalidFolders.Contains(WindowsNames.Canonical(c.Parent)))
            .GroupBy(c => WindowsNames.Canonical(c.Parent), WindowsNames.Comparer)
            .Select(g => new Folder(g.Select(c => c.Parent).Min(StringComparer.Ordinal)!, g.Count(),
                g.Select(c => c.Sha256!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
        var hashOwners = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < folders.Length; i++)
            foreach (var hash in folders[i].Hashes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!hashOwners.TryGetValue(hash, out var owners)) hashOwners.Add(hash, owners = []);
                owners.Add(i);
            }
        long work = 0;
        foreach (var owners in hashOwners.Values)
        {
            work = checked(work + (long)owners.Count * (owners.Count - 1) / 2);
            policy.Limit(work > policy.MaxPairExpansions, "PAIR_EXPANSION_LIMIT");
        }
        var intersections = new Dictionary<(int, int), int>();
        foreach (var owners in hashOwners.Values)
            for (var left = 0; left < owners.Count; left++)
                for (var right = left + 1; right < owners.Count; right++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var key = (owners[left], owners[right]);
                    intersections[key] = checked(intersections.GetValueOrDefault(key) + 1);
                }
        var candidates = new List<Candidate>();
        var related = Enumerable.Range(0, folders.Length).Select(_ => new List<int>()).ToArray();
        var exact = Enumerable.Range(0, folders.Length).Select(_ => new List<int>()).ToArray();
        foreach (var pair in intersections.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2))
        {
            cancellation.ThrowIfCancellationRequested();
            var (i, j) = pair.Key; var a = folders[i]; var b = folders[j]; var common = pair.Value;
            var label = Classify(a.Hashes.Count, b.Hashes.Count, common);
            candidates.Add(new(a.Path, b.Path, a.PhysicalFiles, b.PhysicalFiles, a.Hashes.Count, b.Hashes.Count, common,
                (double)common / a.Hashes.Count, (double)common / b.Hashes.Count, (double)common / ((long)a.Hashes.Count + b.Hashes.Count - common), label));
            related[i].Add(j); related[j].Add(i);
            if (label == Labels[0]) { exact[i].Add(j); exact[j].Add(i); }
        }
        IReadOnlyList<Component> Traverse(string kind, List<int>[] edges)
        {
            var seen = new bool[folders.Length]; var groups = new List<string[]>();
            for (var first = 0; first < seen.Length; first++)
            {
                if (seen[first]) continue;
                var members = new List<string>(); var queue = new Queue<int>(); queue.Enqueue(first); seen[first] = true;
                while (queue.TryDequeue(out var at))
                {
                    cancellation.ThrowIfCancellationRequested(); members.Add(folders[at].Path);
                    foreach (var neighbor in edges[at]) if (!seen[neighbor]) { seen[neighbor] = true; queue.Enqueue(neighbor); }
                }
                if (members.Count > 1) groups.Add(members.Order(StringComparer.Ordinal).ToArray());
            }
            return groups.OrderBy(g => g[0], StringComparer.Ordinal).Select((g, index) =>
                new Component(kind, kind + "_" + (index + 1).ToString("D6", System.Globalization.CultureInfo.InvariantCulture), g)).ToArray();
        }
        return new(folders, duplicates, candidates, Traverse("RELATED", related).Concat(Traverse("EXACT_ONLY", exact)).ToArray(),
            invalidFolders.Select(f => displayNames[f]).Order(StringComparer.Ordinal).ToArray());
    }
}
