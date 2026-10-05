namespace BmsLibraryAudit.Core;

public static class RelationshipAnalyzer
{
    public static readonly IReadOnlyList<string> Labels = Array.AsReadOnly(new[]
    { "EXACT_CHART_SET", "A_SUBSET_OF_B", "B_SUBSET_OF_A", "HIGH_OVERLAP", "MEDIUM_OVERLAP", "SHARED_CHARTS" });

    public static AnalysisResult Analyze(IReadOnlyList<ChartRecord> charts, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        var incomplete = charts.Where(chart => chart.Present && chart.Status != "OK")
            .Select(chart => chart.ParentFolder).ToHashSet(PathPolicy.Comparer);
        var blocked = (diagnostics ?? []).Select(diagnostic => diagnostic.Code switch
        {
            "DIRECTORY_UNREADABLE" or "REPARSE_SKIPPED" => diagnostic.Path,
            "ENTRY_METADATA" or "CHART_READ" or "CHART_UNSTABLE" => Path.GetDirectoryName(diagnostic.Path),
            _ => null
        }).Where(path => path is not null).Select(path => path!).ToArray();
        foreach (var folder in charts.Where(chart => chart.Present).Select(chart => chart.ParentFolder).Distinct(PathPolicy.Comparer))
            if (blocked.Any(path => PathPolicy.Contains(path, folder))) incomplete.Add(folder);
        var current = charts.Where(chart => chart.Present && chart.Status == "OK" && chart.Sha256 is not null).ToArray();
        var duplicates = current.GroupBy(chart => chart.Sha256!, StringComparer.Ordinal)
            .Where(group => group.Select(chart => chart.NormalizedPath).Distinct(StringComparer.Ordinal).Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (IReadOnlyList<ChartRecord>)group.OrderBy(chart => chart.NormalizedPath, StringComparer.Ordinal).ToArray()).ToArray();
        var folders = current.Where(chart => !incomplete.Contains(chart.ParentFolder))
            .GroupBy(chart => chart.ParentFolder, PathPolicy.Comparer)
            .Select(group => new FolderEvidence(group.Select(chart => chart.ParentFolder).Order(StringComparer.Ordinal).First(),
                group.Count(), group.Select(chart => chart.Sha256!).ToHashSet(StringComparer.Ordinal)))
            .OrderBy(folder => folder.Path, StringComparer.Ordinal).ToArray();
        var inverted = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var folder = 0; folder < folders.Length; folder++)
            foreach (var hash in folders[folder].Hashes)
            {
                if (!inverted.TryGetValue(hash, out var list)) inverted[hash] = list = [];
                list.Add(folder);
            }
        var commonCounts = new Dictionary<(int A, int B), int>();
        foreach (var containing in inverted.Values)
            for (var i = 0; i < containing.Count; i++)
                for (var j = i + 1; j < containing.Count; j++)
                {
                    var key = (containing[i], containing[j]);
                    commonCounts[key] = commonCounts.GetValueOrDefault(key) + 1;
                }
        var candidates = new List<FolderCandidate>();
        var related = new UnionFind(folders.Length);
        var exact = new UnionFind(folders.Length);
        foreach (var (pair, common) in commonCounts.OrderBy(item => item.Key.A).ThenBy(item => item.Key.B))
        {
            var a = folders[pair.A];
            var b = folders[pair.B];
            var countA = a.Hashes.Count;
            var countB = b.Hashes.Count;
            var coverageA = (double)common / countA;
            var coverageB = (double)common / countB;
            var label = Classify(countA, countB, common);
            candidates.Add(new(a.Path, b.Path, a.PhysicalChartFiles, b.PhysicalChartFiles,
                countA, countB, common, coverageA, coverageB, (double)common / ((long)countA + countB - common), label));
            related.Union(pair.A, pair.B);
            if (label == "EXACT_CHART_SET") exact.Union(pair.A, pair.B);
        }
        var clusters = Components("RELATED", related, folders).Concat(Components("EXACT_ONLY", exact, folders)).ToArray();
        return new(folders, duplicates, candidates, clusters, incomplete.Order(StringComparer.Ordinal).ToArray());
    }

    public static string Classify(int chartsA, int chartsB, int common)
    {
        if (chartsA <= 0 || chartsB <= 0 || common <= 0 || common > Math.Min(chartsA, chartsB))
            throw new ArgumentOutOfRangeException(nameof(common));
        if (common == chartsA && common == chartsB) return "EXACT_CHART_SET";
        if (common == chartsA && chartsA < chartsB) return "A_SUBSET_OF_B";
        if (common == chartsB && chartsB < chartsA) return "B_SUBSET_OF_A";
        // Integer comparisons make the inclusive 0.80 and 0.50 thresholds exact.
        if ((long)common * 5 >= (long)Math.Max(chartsA, chartsB) * 4) return "HIGH_OVERLAP";
        if ((long)common * 2 >= Math.Max(chartsA, chartsB)) return "MEDIUM_OVERLAP";
        return "SHARED_CHARTS";
    }

    private static IEnumerable<FolderCluster> Components(string kind, UnionFind union, FolderEvidence[] folders)
    {
        var groups = Enumerable.Range(0, folders.Length).GroupBy(union.Find)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(index => folders[index].Path).Order(StringComparer.Ordinal).ToArray())
            .OrderBy(group => group[0], StringComparer.Ordinal).ToArray();
        for (var i = 0; i < groups.Length; i++) yield return new(kind, $"{kind}_{i + 1:D6}", groups[i]);
    }

    private sealed class UnionFind(int count)
    {
        private readonly int[] parent = Enumerable.Range(0, count).ToArray();
        private readonly int[] sizes = Enumerable.Repeat(1, count).ToArray();
        public int Find(int index)
        {
            while (index != parent[index]) { parent[index] = parent[parent[index]]; index = parent[index]; }
            return index;
        }
        public void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (sizes[a] < sizes[b]) (a, b) = (b, a);
            parent[b] = a;
            sizes[a] += sizes[b];
        }
    }
}
