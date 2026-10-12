namespace BmsLibraryAudit.SourceInspector.Offline;

public static class Relationships
{
    private sealed class FolderAccumulator(string display)
    {
        public string Display { get; set; } = display;
        public int Physical { get; set; }
        public HashSet<string> Hashes { get; } = new(StringComparer.Ordinal);
    }
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
        var hashLocations = new Dictionary<string, List<Chart>>(StringComparer.Ordinal);
        var folderEvidence = new Dictionary<string, FolderAccumulator>(WindowsNames.Comparer);
        var invalidFolders = new Dictionary<string, string>(WindowsNames.Comparer);
        var membershipCount = 0;
        var blocked = new List<string>();
        foreach (var d in diagnostics)
        {
            cancellation.ThrowIfCancellationRequested(); policy.Text(d.Path); policy.Text(d.Code);
            if (!Provenance.DiagnosticCodes.Contains(d.Code, StringComparer.Ordinal)) throw new EvidenceException(FindingKind.Pending, "DIAGNOSTIC_SEMANTICS_UNKNOWN");
            blocked.Add(d.Code is "DIRECTORY_UNREADABLE" or "REPARSE_SKIPPED" ? WindowsNames.Canonical(d.Path) : WindowsNames.Parent(d.Path));
        }
        var allFolders = new HashSet<string>(WindowsNames.Comparer);
        var firstPresentNames = new Dictionary<string, string>(WindowsNames.Comparer);
        var chartKeys = new HashSet<string>(StringComparer.Ordinal);
        var processedCharts = 0;
        foreach (var c in charts)
        {
            cancellation.ThrowIfCancellationRequested();
            policy.Limit(++processedCharts > policy.MaxChartRecords, "CHART_LIMIT");
            policy.Text(c.Path); policy.Text(c.Parent); policy.Text(c.Key);
            if (c.Present is not (0 or 1) || c.Status is not ("OK" or "ERROR" or "UNSTABLE" or "MISSING" or "UNVERIFIED" or "INACTIVE") ||
                c.Key != WindowsNames.Key(c.Path) || !chartKeys.Add(c.Key) || !WindowsNames.Comparer.Equals(WindowsNames.Canonical(c.Parent), WindowsNames.Parent(c.Path)))
                throw new EvidenceException(FindingKind.Invalid, "CHART_LOCAL_PROVENANCE");
            if (c.Present != 1) continue;
            var folder = WindowsNames.Canonical(c.Parent);
            allFolders.Add(folder); policy.Limit(allFolders.Count > policy.MaxFolders, "FOLDER_LIMIT");
            firstPresentNames.TryAdd(folder, c.Parent);
            // Historical text retains the first non-OK spelling, even if an OK row came first.
            if (c.Status != "OK") invalidFolders.TryAdd(folder, c.Parent);
            else
            {
                if (!Provenance.Digest(c.Sha256, 64)) throw new EvidenceException(FindingKind.Invalid, "CHART_HASH");
                if (!hashLocations.TryGetValue(c.Sha256!, out var locations))
                {
                    policy.Limit(hashLocations.Count >= policy.MaxDistinctHashes, "HASH_LIMIT");
                    hashLocations.Add(c.Sha256!, locations = []);
                }
                locations.Add(c);
                if (!folderEvidence.TryGetValue(folder, out var accumulator))
                    folderEvidence.Add(folder, accumulator = new(c.Parent));
                if (StringComparer.Ordinal.Compare(c.Parent, accumulator.Display) < 0) accumulator.Display = c.Parent;
                accumulator.Physical = checked(accumulator.Physical + 1);
                if (accumulator.Hashes.Add(c.Sha256!))
                    policy.Limit(++membershipCount > policy.MaxFolderHashMemberships, "FOLDER_HASH_LIMIT");
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
                if (excluded.Contains(p)) { invalidFolders.TryAdd(folder, firstPresentNames[folder]); break; }
                if (p.Length == 3) break;
                p = WindowsNames.Parent(p);
            }
        }
        // Keys were validated as unique: a multi-location hash necessarily has distinct physical paths.
        var duplicates = hashLocations.Where(g => g.Value.Count > 1).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => { cancellation.ThrowIfCancellationRequested(); return new DuplicateGroup(g.Key, g.Value.OrderBy(c => c.Key, StringComparer.Ordinal).ToArray()); }).ToArray();
        var folders = folderEvidence.Where(p => !invalidFolders.ContainsKey(p.Key))
            .Select(p => { cancellation.ThrowIfCancellationRequested(); var f = p.Value;
                return new Folder(f.Display, f.Physical, f.Hashes.Order(StringComparer.Ordinal).ToArray()); })
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
            cancellation.ThrowIfCancellationRequested();
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
                    if (intersections.TryGetValue(key, out var count)) intersections[key] = checked(count + 1);
                    else
                    {
                        policy.Limit(intersections.Count >= policy.MaxCandidatePairs, "CANDIDATE_PAIR_LIMIT");
                        intersections.Add(key, 1);
                    }
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
            invalidFolders.Values.Order(StringComparer.Ordinal).ToArray());
    }
}
