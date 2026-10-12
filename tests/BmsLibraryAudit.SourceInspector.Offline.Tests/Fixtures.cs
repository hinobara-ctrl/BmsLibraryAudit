using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

internal static class Fixtures
{
    internal const string RootPath = @"C:\Synthetic";
    internal const string Start = "2020-01-01T00:00:00.0000000+00:00";
    internal const string End = "2020-01-01T00:00:01.0000000+00:00";
    internal static readonly ProcessingPolicy Policy = new();
    internal static string Hash(char c) => new(c, 64);
    internal static Chart Chart(string folder, int number, char hash = 'a')
    {
        var parent = RootPath + "\\" + folder; var name = "c" + number + ".bms"; var path = parent + "\\" + name;
        return new(1, path, path.ToUpperInvariant(), parent, name, ".bms", 1, 0, new string(hash, 32), Hash(hash), 1, 1, 1, "OK");
    }
    internal static Evidence Evidence(IReadOnlyList<Chart>? charts = null)
    {
        charts ??= [Chart("A", 0), Chart("A", 1, 'b')];
        return new(new(0x424D5341, 1, "BmsLibraryAudit", "1", "windows-ordinal-ignore-case"),
            [new(1, Start, End, "COMPLETED", 0, [RootPath], new(charts.Count, 0, charts.Count, charts.Count, 0, 0, 0, 0))],
            [new(1, RootPath, RootPath.ToUpperInvariant(), 1)], charts, []);
    }
    // Independent literal oracle: no production writer, serializer, parser or analyzer generates these lines.
    private static readonly string[] SummaryLines =
    [
        "{",
        "  \"tool_version\": \"0.1.0\",",
        "  \"schema_version\": 1,",
        "  \"phase\": \"PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY\",",
        "  \"scan_id\": 1,",
        "  \"scan_started_utc\": \"2020-01-01T00:00:00.0000000+00:00\",",
        "  \"scan_completed_utc\": \"2020-01-01T00:00:01.0000000+00:00\",",
        "  \"roots\": [",
        "    \"C:\\\\Synthetic\"",
        "  ],",
        "  \"current_chart_files\": 2,",
        "  \"verified_chart_files\": 2,",
        "  \"unverified_previously_indexed_chart_files\": 0,",
        "  \"unique_sha256\": 2,",
        "  \"unique_md5\": 2,",
        "  \"physical_chart_folders\": 1,",
        "  \"analyzed_chart_folders\": 1,",
        "  \"incomplete_folders\": [],",
        "  \"duplicate_sha256_groups\": 0,",
        "  \"candidate_folder_pairs\": 0,",
        "  \"relationship_counts\": {",
        "    \"EXACT_CHART_SET\": 0,",
        "    \"A_SUBSET_OF_B\": 0,",
        "    \"B_SUBSET_OF_A\": 0,",
        "    \"HIGH_OVERLAP\": 0,",
        "    \"MEDIUM_OVERLAP\": 0,",
        "    \"SHARED_CHARTS\": 0",
        "  },",
        "  \"cluster_count\": 0,",
        "  \"cluster_size_distribution\": {},",
        "  \"exact_only_cluster_count\": 0,",
        "  \"exact_only_cluster_size_distribution\": {},",
        "  \"hash_cache_statistics\": {",
        "    \"discovered\": 2,",
        "    \"reused\": 0,",
        "    \"hashed\": 2,",
        "    \"new\": 2,",
        "    \"changed\": 0,",
        "    \"missing\": 0,",
        "    \"errored\": 0,",
        "    \"unstable\": 0",
        "  },",
        "  \"warning_count\": 0,",
        "  \"error_count\": 0,",
        "  \"diagnostics\": [],",
        "  \"evidence_notice\": \"Chart relationships and connected components do not establish package equivalence or authorize source mutation. Cached hashes depend on size/mtime; use --rehash-all for fresh reads.\"",
        "}"
    ];
    internal static byte[] Canonical => Encoding.UTF8.GetBytes(string.Join("\n", SummaryLines) + "\n");
    internal static byte[] Legacy => Encoding.UTF8.GetBytes(string.Join("\r\n", SummaryLines) + "\n");
    internal static SummaryOracle Oracle => new("summary.json", "0.1.0", 1, Reports.Phase, "independent-two-chart-literal-v1", Canonical, Legacy);
    internal static ParsedReports ReportsForSimple(byte[]? bytes = null)
    {
        bytes ??= Canonical;
        return new(Reports.ParseSummary(bytes, Policy), [], [], [], bytes);
    }
    internal static Chart[] Mathematics => [Chart("A", 0), Chart("A", 1), Chart("A", 2, 'b'),
        Chart("B", 0), Chart("B", 1, 'b'), Chart("C", 0), Chart("C", 1, 'b'), Chart("D", 0), Chart("D", 1, 'c')];
    internal static Candidate[] ExpectedPairs =>
    [
        Pair("A", "B", 3, 2, 2, 1, 1), Pair("A", "C", 3, 2, 2, 1, 1),
        Pair("A", "D", 3, 2, 1, 0.5, 1.0 / 3), Pair("B", "C", 2, 2, 2, 1, 1),
        Pair("B", "D", 2, 2, 1, 0.5, 1.0 / 3), Pair("C", "D", 2, 2, 1, 0.5, 1.0 / 3)
    ];
    private static Candidate Pair(string a, string b, int physicalA, int physicalB, int common, double coverage, double jaccard) =>
        new(RootPath + "\\" + a, RootPath + "\\" + b, physicalA, physicalB, 2, 2, common, coverage, coverage, jaccard,
            common == 2 ? "EXACT_CHART_SET" : "MEDIUM_OVERLAP");
}
