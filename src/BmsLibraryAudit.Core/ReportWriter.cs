using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

public static class ReportWriter
{
    public const string ToolVersion = "0.1.0";
    public static AnalysisResult Write(string databasePath, string outputDirectory)
    {
        using var database = AuditDatabase.Open(databasePath, writable: false);
        var snapshot = database.Snapshot();
        outputDirectory = PathPolicy.Absolute(outputDirectory);
        PathPolicy.RequireOutsideRoots(outputDirectory, database.AllRoots());
        if (PathPolicy.Contains(outputDirectory, database.Path))
            throw new InvalidOperationException("Report directory must not contain the audit database.");
        if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new InvalidOperationException("Report output must be a new or empty directory; existing files will not be overwritten.");
        var analysis = RelationshipAnalyzer.Analyze(snapshot.Charts, snapshot.Diagnostics);
        Directory.CreateDirectory(outputDirectory);
        var related = analysis.Clusters.Where(cluster => cluster.Kind == "RELATED").ToArray();
        var exact = analysis.Clusters.Where(cluster => cluster.Kind == "EXACT_ONLY").ToArray();
        var counts = RelationshipAnalyzer.Labels.ToDictionary(label => label,
            label => analysis.Candidates.Count(candidate => candidate.Relationship == label), StringComparer.Ordinal);
        var summary = new
        {
            tool_version = ToolVersion, schema_version = AuditDatabase.SchemaVersion,
            phase = "PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY",
            scan_id = snapshot.ScanId, scan_started_utc = snapshot.StartedUtc, scan_completed_utc = snapshot.CompletedUtc,
            roots = snapshot.Roots, current_chart_files = snapshot.Charts.Count,
            verified_chart_files = snapshot.Charts.Count(chart => chart.Status == "OK"),
            unverified_previously_indexed_chart_files = snapshot.UnverifiedChartFiles,
            unique_sha256 = snapshot.Charts.Where(chart => chart.Status == "OK").Select(chart => chart.Sha256).Distinct().Count(),
            unique_md5 = snapshot.Charts.Where(chart => chart.Status == "OK").Select(chart => chart.Md5).Distinct().Count(),
            physical_chart_folders = snapshot.Charts.Select(chart => chart.ParentFolder).Distinct(PathPolicy.Comparer).Count(),
            analyzed_chart_folders = analysis.Folders.Count, incomplete_folders = analysis.IncompleteFolders,
            duplicate_sha256_groups = analysis.DuplicateGroups.Count,
            candidate_folder_pairs = analysis.Candidates.Count, relationship_counts = counts,
            cluster_count = related.Length, cluster_size_distribution = SizeDistribution(related),
            exact_only_cluster_count = exact.Length, exact_only_cluster_size_distribution = SizeDistribution(exact),
            hash_cache_statistics = snapshot.Statistics,
            warning_count = snapshot.Diagnostics.Count(item => item.Severity == "WARNING"),
            error_count = snapshot.Diagnostics.Count(item => item.Severity == "ERROR"),
            diagnostics = snapshot.Diagnostics,
            evidence_notice = "Chart relationships and connected components do not establish package equivalence or authorize source mutation. Cached hashes depend on size/mtime; use --rehash-all for fresh reads."
        };
        using (var writer = Create(outputDirectory, "summary.json"))
        {
            writer.Write(JsonSerializer.Serialize(summary, new JsonSerializerOptions
                { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
            writer.Write('\n');
        }
        using (var writer = Create(outputDirectory, "duplicate_charts.csv"))
        {
            Csv(writer, "sha256", "md5", "group_paths", "root_id", "absolute_path", "parent_folder", "filename", "extension", "size", "mtime_utc");
            foreach (var group in analysis.DuplicateGroups)
                foreach (var chart in group)
                    Csv(writer, chart.Sha256!, chart.Md5!, group.Count, chart.RootId, chart.AbsolutePath, chart.ParentFolder,
                        chart.Filename, chart.Extension, chart.Size, new DateTime(chart.MtimeUtcTicks, DateTimeKind.Utc).ToString("O"));
        }
        using (var writer = Create(outputDirectory, "folder_candidates.csv"))
        {
            Csv(writer, "folder_a", "folder_b", "physical_files_a", "physical_files_b", "charts_a", "charts_b", "common", "coverage_a", "coverage_b", "jaccard", "relationship");
            foreach (var item in analysis.Candidates)
                Csv(writer, item.FolderA, item.FolderB, item.PhysicalFilesA, item.PhysicalFilesB, item.ChartsA,
                    item.ChartsB, item.Common, item.CoverageA, item.CoverageB, item.Jaccard, item.Relationship);
        }
        using (var writer = Create(outputDirectory, "clusters.csv"))
        {
            Csv(writer, "kind", "cluster_id", "folder_count", "folder_path");
            foreach (var cluster in analysis.Clusters)
                foreach (var member in cluster.Members) Csv(writer, cluster.Kind, cluster.Id, cluster.Members.Count, member);
        }
        return analysis;
    }

    private static SortedDictionary<int, int> SizeDistribution(IEnumerable<FolderCluster> clusters) =>
        new(clusters.GroupBy(cluster => cluster.Members.Count).ToDictionary(group => group.Key, group => group.Count()));
    private static StreamWriter Create(string directory, string filename) => new(
        new FileStream(Path.Combine(directory, filename), FileMode.CreateNew, FileAccess.Write, FileShare.None),
        new UTF8Encoding(false)) { NewLine = "\n" };
    private static void Csv(TextWriter writer, params object[] values)
    {
        writer.WriteLine(string.Join(',', values.Select(value =>
        {
            var text = value is IFormattable formatted
                ? formatted.ToString(null, CultureInfo.InvariantCulture) : value.ToString()!;
            return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? '"' + text.Replace("\"", "\"\"") + '"' : text;
        })));
    }
}
