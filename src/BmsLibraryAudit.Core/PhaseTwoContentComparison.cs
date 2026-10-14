namespace BmsLibraryAudit.Core;

internal static class PhaseTwoContentComparison
{
    internal sealed record Node(long Entry, string Path, string Kind, string? Digest, bool Chart);
    internal sealed record Difference(string Path, string Category, long? EntryA, long? EntryB, string? DigestA, string? DigestB);
    internal sealed record Group(string Projection, string Digest, long CountA, long CountB, bool DifferentPaths);
    internal sealed record Evidence(long[] Counts, bool[] Flags, Difference[] Entries, Group[] Groups);
    internal const string ValueColumns = "same_path_same_bytes,same_path_different_bytes,files_only_a,files_only_b,directories_only_a,directories_only_b,kind_conflicts,tree_exact,content_multiset_exact,non_chart_tree_exact,non_chart_multiset_exact,path_conflict,fileset_variant";
    internal static Node[] View(PhaseTwoDatabase db, long session, long preflight, long root) => db.Rows("""
        SELECT e.entry_id,m.relative_path,e.kind,h.sha256,e.is_chart FROM observation_memberships m
        JOIN preflight_entries e ON e.preflight_id=m.preflight_id AND e.entry_id=m.entry_id
        LEFT JOIN physical_files p ON p.preflight_id=e.preflight_id AND p.entry_id=e.entry_id
        LEFT JOIN content_hashes h ON h.content_session_id=$0 AND h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
        WHERE m.preflight_id=$1 AND m.observation_root_id=$2 ORDER BY m.relative_path_key COLLATE ORDINAL
        """, session, preflight, root).Select(r => new Node(Convert.ToInt64(r[0]), (string)r[1]!, (string)r[2]!, (string?)r[3], r[4] is not null && Convert.ToInt64(r[4]) != 0)).ToArray();
    internal static Evidence Derive(Node[] a, Node[] b, CancellationToken token = default)
    {
        if (a.Concat(b).Any(n => n.Kind is not ("DIRECTORY" or "REGULAR_FILE") || n.Kind == "REGULAR_FILE" && !PhaseOneSourceCapture.Digest(n.Digest, 64)))
            throw new InvalidOperationException("Comparison lacks complete trusted file evidence.");
        var left = a.ToDictionary(n => n.Path, StringComparer.Ordinal); var right = b.ToDictionary(n => n.Path, StringComparer.Ordinal);
        var entries = new List<Difference>();
        foreach (var path in left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested(); left.TryGetValue(path, out var x); right.TryGetValue(path, out var y);
            var category = x is null ? y!.Kind == "DIRECTORY" ? "DIRECTORY_ONLY_B" : "FILE_ONLY_B" :
                y is null ? x.Kind == "DIRECTORY" ? "DIRECTORY_ONLY_A" : "FILE_ONLY_A" : x.Kind != y.Kind ? "KIND_CONFLICT" :
                x.Kind == "DIRECTORY" ? "SAME_DIRECTORY" : x.Digest == y.Digest ? "SAME_FILE" : "CONTENT_CONFLICT";
            entries.Add(new(path, category, x?.Entry, y?.Entry, x?.Digest, y?.Digest));
        }
        var groups = new List<Group>();
        foreach (var projection in new[] { "FULL", "NON_CHART" })
        {
            var x = a.Where(n => n.Kind == "REGULAR_FILE" && (projection == "FULL" || !n.Chart)).GroupBy(n => n.Digest!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(n => n.Path).Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            var y = b.Where(n => n.Kind == "REGULAR_FILE" && (projection == "FULL" || !n.Chart)).GroupBy(n => n.Digest!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(n => n.Path).Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            foreach (var digest in x.Keys.Concat(y.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested(); var pathsA = x.GetValueOrDefault(digest) ?? []; var pathsB = y.GetValueOrDefault(digest) ?? [];
                groups.Add(new(projection, digest, pathsA.LongLength, pathsB.LongLength, !pathsA.SequenceEqual(pathsB, StringComparer.Ordinal)));
            }
        }
        var fa = a.Where(n => n.Kind == "REGULAR_FILE").ToDictionary(n => n.Path, StringComparer.Ordinal);
        var fb = b.Where(n => n.Kind == "REGULAR_FILE").ToDictionary(n => n.Path, StringComparer.Ordinal);
        var da = a.Where(n => n.Kind == "DIRECTORY").Select(n => n.Path).ToHashSet(StringComparer.Ordinal);
        var db = b.Where(n => n.Kind == "DIRECTORY").Select(n => n.Path).ToHashSet(StringComparer.Ordinal);
        var shared = fa.Keys.Intersect(fb.Keys, StringComparer.Ordinal).ToArray();
        long[] counts = [shared.LongCount(p => fa[p].Digest == fb[p].Digest), shared.LongCount(p => fa[p].Digest != fb[p].Digest),
            fa.Keys.Except(fb.Keys, StringComparer.Ordinal).LongCount(), fb.Keys.Except(fa.Keys, StringComparer.Ordinal).LongCount(),
            da.Except(db).LongCount(), db.Except(da).LongCount(), entries.LongCount(e => e.Category == "KIND_CONFLICT")];
        var tree = entries.All(e => e.Category is "SAME_FILE" or "SAME_DIRECTORY");
        var na = fa.Values.Where(n => !n.Chart).ToDictionary(n => n.Path, StringComparer.Ordinal);
        var nb = fb.Values.Where(n => !n.Chart).ToDictionary(n => n.Path, StringComparer.Ordinal);
        var nonChartTree = da.SetEquals(db) && na.Count == nb.Count && na.All(p => nb.TryGetValue(p.Key, out var n) && n.Digest == p.Value.Digest);
        bool[] flags = [tree, groups.Where(g => g.Projection == "FULL").All(g => g.CountA == g.CountB), nonChartTree,
            groups.Where(g => g.Projection == "NON_CHART").All(g => g.CountA == g.CountB), counts[1] > 0 || counts[6] > 0, !tree];
        return new(counts, flags, entries.ToArray(), groups.ToArray());
    }
    private static (long Id, string Key)[] Roots(PhaseTwoDatabase db, long preflight, long component) => db.Rows("SELECT r.observation_root_id,r.root_key FROM component_memberships m JOIN observation_roots r ON r.preflight_id=m.preflight_id AND r.observation_root_id=m.observation_root_id WHERE m.preflight_id=$0 AND m.component_id=$1 ORDER BY r.root_key COLLATE ORDINAL", preflight, component)
        .Select(r => (Convert.ToInt64(r[0]), (string)r[1]!)).ToArray();
    internal static void Store(PhaseTwoDatabase db, long session, long preflight, long component, CancellationToken token)
    {
        var roots = Roots(db, preflight, component);
        for (var i = 0; i < roots.Length; i++) for (var j = i + 1; j < roots.Length; j++)
        {
            token.ThrowIfCancellationRequested(); var a = roots[i].Id; var b = roots[j].Id;
            var evidence = Derive(View(db, session, preflight, a), View(db, session, preflight, b), token);
            db.Insert("comparisons", "content_session_id,preflight_id,component_id,root_a_id,root_b_id," + ValueColumns,
                [session, preflight, component, a, b, .. evidence.Counts.Cast<object?>(), .. evidence.Flags.Cast<object?>()]);
            foreach (var e in evidence.Entries)
            {
                token.ThrowIfCancellationRequested();
                db.Insert("comparison_entries", "content_session_id,preflight_id,component_id,root_a_id,root_b_id,relative_path_key,category,entry_a_id,entry_b_id,sha256_a,sha256_b",
                    session, preflight, component, a, b, e.Path, e.Category, e.EntryA, e.EntryB, e.DigestA, e.DigestB);
            }
            foreach (var g in evidence.Groups)
                db.Insert("comparison_content_groups", "content_session_id,preflight_id,component_id,root_a_id,root_b_id,projection,sha256,count_a,count_b,differing_path_sets",
                    session, preflight, component, a, b, g.Projection, g.Digest, g.CountA, g.CountB, g.DifferentPaths);
        }
    }
    internal static void Verify(PhaseTwoDatabase db, long session, long preflight, long component, bool complete, CancellationToken token)
    {
        var roots = Roots(db, preflight, component);
        var expectedPairs = complete ? (long)roots.Length * (roots.Length - 1) / 2 : 0;
        if (db.Number("SELECT count(*) FROM comparisons WHERE content_session_id=$0 AND component_id=$1", session, component) != expectedPairs)
            throw new InvalidOperationException("Incomplete/extra comparison pair coverage.");
        if (!complete) return;
        for (var i = 0; i < roots.Length; i++) for (var j = i + 1; j < roots.Length; j++)
        {
            token.ThrowIfCancellationRequested(); var a = roots[i].Id; var b = roots[j].Id;
            var expected = Derive(View(db, session, preflight, a), View(db, session, preflight, b), token);
            var row = db.Rows("SELECT " + ValueColumns + " FROM comparisons WHERE content_session_id=$0 AND component_id=$1 AND root_a_id=$2 AND root_b_id=$3", session, component, a, b).SingleOrDefault();
            if (row is null || !row.Select(Convert.ToInt64).SequenceEqual(expected.Counts.Concat(expected.Flags.Select(f => f ? 1L : 0L))))
                throw new InvalidOperationException("Comparison dimensions do not reproduce from file evidence.");
            var entries = db.Rows("SELECT relative_path_key,category,entry_a_id,entry_b_id,sha256_a,sha256_b FROM comparison_entries WHERE content_session_id=$0 AND root_a_id=$1 AND root_b_id=$2 ORDER BY relative_path_key COLLATE ORDINAL", session, a, b)
                .Select(r => new Difference((string)r[0]!, (string)r[1]!, r[2] is null ? null : Convert.ToInt64(r[2]), r[3] is null ? null : Convert.ToInt64(r[3]), (string?)r[4], (string?)r[5]));
            var groups = db.Rows("SELECT projection,sha256,count_a,count_b,differing_path_sets FROM comparison_content_groups WHERE content_session_id=$0 AND root_a_id=$1 AND root_b_id=$2 ORDER BY projection COLLATE ORDINAL,sha256 COLLATE ORDINAL", session, a, b)
                .Select(r => new Group((string)r[0]!, (string)r[1]!, Convert.ToInt64(r[2]), Convert.ToInt64(r[3]), Convert.ToInt64(r[4]) != 0));
            if (!entries.SequenceEqual(expected.Entries) || !groups.SequenceEqual(expected.Groups.OrderBy(g => g.Projection, StringComparer.Ordinal).ThenBy(g => g.Digest, StringComparer.Ordinal)))
                throw new InvalidOperationException("Comparison drill-down/groups do not reproduce from file evidence.");
        }
    }
}
