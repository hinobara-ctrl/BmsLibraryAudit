using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BmsLibraryAudit.SourceInspector.Offline;

public sealed record ReportDiagnostic(string Severity, string Code, string Path, string Message);
public sealed record Summary(string ToolVersion, int SchemaVersion, string Phase, long ScanId, string StartedUtc,
    string CompletedUtc, IReadOnlyList<string> Roots, long Current, long Verified, long Unverified, long UniqueSha256,
    long UniqueMd5, long PhysicalFolders, long AnalyzedFolders, IReadOnlyList<string> IncompleteFolders,
    long DuplicateGroups, long CandidatePairs, IReadOnlyDictionary<string, long> RelationshipCounts,
    long RelatedCount, IReadOnlyDictionary<int, long> RelatedDistribution, long ExactCount,
    IReadOnlyDictionary<int, long> ExactDistribution, Statistics Statistics, long Warnings, long Errors,
    IReadOnlyList<ReportDiagnostic> Diagnostics, string EvidenceNotice);
public sealed record DuplicateRow(string Sha256, string Md5, long GroupPaths, long RootId, string Path,
    string Parent, string Filename, string Extension, long Size, string MtimeUtc);
public sealed record ClusterRow(string Kind, string Id, int FolderCount, string FolderPath);
public sealed record ParsedReports(Summary Summary, IReadOnlyList<DuplicateRow> Duplicates,
    IReadOnlyList<Candidate> Candidates, IReadOnlyList<ClusterRow> Clusters, ReadOnlyMemory<byte> SummaryBytes);
public enum SummaryProfile { CanonicalLf, WindowsLegacy, Unsupported, Malformed }
public sealed record SummaryOracle(string ReportType, string ToolVersion, int SchemaVersion, string Phase,
    string SpecificationId, ReadOnlyMemory<byte> CanonicalBytes, ReadOnlyMemory<byte> LegacyBytes);
public sealed record FormatRecognition(SummaryProfile Profile, bool SyntacticMatch, bool ExactOracleMatch,
    string? OracleId, string Reason)
{
    public bool ExternalProvenanceRequired => true;
    public bool ProducerVerified => false;
}

public static class Reports
{
    public const string Phase = "PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY";
    public const string Notice = "Chart relationships and connected components do not establish package equivalence or authorize source mutation. Cached hashes depend on size/mtime; use --rehash-all for fresh reads.";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly string[] SummaryFields = ["tool_version", "schema_version", "phase", "scan_id", "scan_started_utc", "scan_completed_utc", "roots",
        "current_chart_files", "verified_chart_files", "unverified_previously_indexed_chart_files", "unique_sha256", "unique_md5", "physical_chart_folders",
        "analyzed_chart_folders", "incomplete_folders", "duplicate_sha256_groups", "candidate_folder_pairs", "relationship_counts", "cluster_count",
        "cluster_size_distribution", "exact_only_cluster_count", "exact_only_cluster_size_distribution", "hash_cache_statistics", "warning_count", "error_count", "diagnostics", "evidence_notice"];
    private static readonly string[] StatsFields = ["discovered", "reused", "hashed", "new", "changed", "missing", "errored", "unstable"];
    internal static string Decode(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy)
    {
        policy.Validate(); policy.Limit(bytes.Length > policy.MaxInputBytes, "INPUT_BYTE_LIMIT");
        try { return Utf8.GetString(bytes.Span); }
        catch (DecoderFallbackException) { throw new EvidenceException(FindingKind.Invalid, "INVALID_UTF8"); }
    }
    private static JsonDocument Json(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested(); var text = Decode(bytes, policy);
        if (text.StartsWith('\ufeff')) throw new EvidenceException(FindingKind.Pending, "BOM_UNSUPPORTED");
        JsonDocument document;
        try
        {
            // One extra reader level lets the explicit policy check classify depth independently of localized exception text.
            var reader = new Utf8JsonReader(bytes.Span, new JsonReaderOptions { MaxDepth = policy.MaxJsonDepth + 1, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    policy.Limit(reader.CurrentDepth + 1 > policy.MaxJsonDepth, "JSON_DEPTH_LIMIT");
                if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
                {
                    var raw = reader.ValueSpan;
                    for (var i = 0; i < raw.Length; i++)
                    {
                        if (raw[i] != (byte)'\\') continue;
                        i++; if (raw[i] != (byte)'u') continue;
                        var code = int.Parse(Encoding.ASCII.GetString(raw.Slice(i + 1, 4)), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        if (code is >= 0xd800 and <= 0xdbff)
                        {
                            var next = i + 5;
                            if (next + 5 >= raw.Length || raw[next] != (byte)'\\' || raw[next + 1] != (byte)'u')
                                throw new EvidenceException(FindingKind.Invalid, "INVALID_JSON_UNICODE");
                            var low = int.Parse(Encoding.ASCII.GetString(raw.Slice(next + 2, 4)), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            if (low is < 0xdc00 or > 0xdfff) throw new EvidenceException(FindingKind.Invalid, "INVALID_JSON_UNICODE");
                            i = next + 5;
                        }
                        else { if (code is >= 0xdc00 and <= 0xdfff) throw new EvidenceException(FindingKind.Invalid, "INVALID_JSON_UNICODE"); i += 4; }
                    }
                    policy.Text(reader.GetString()!);
                }
            }
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = policy.MaxJsonDepth, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        }
        catch (JsonException)
        {
            throw new EvidenceException(FindingKind.Invalid, "MALFORMED_JSON");
        }
        var nodes = 0;
        void Inspect(JsonElement element)
        {
            cancellation.ThrowIfCancellationRequested(); policy.Limit(++nodes > policy.MaxReportRows, "JSON_NODE_LIMIT");
            if (element.ValueKind == JsonValueKind.String) policy.Text(element.GetString()!);
            if (element.ValueKind == JsonValueKind.Object)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    policy.Text(property.Name);
                    if (!keys.Add(property.Name)) throw new EvidenceException(FindingKind.Invalid, "DUPLICATE_JSON_KEY");
                    Inspect(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Inspect(item);
        }
        try { Inspect(document.RootElement); return document; } catch { document.Dispose(); throw; }
    }
    private static void Fields(JsonElement value, IEnumerable<string> fields)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(fields))
            throw new EvidenceException(FindingKind.Invalid, "JSON_FIELDS");
    }
    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new EvidenceException(FindingKind.Invalid, "JSON_TYPE");
    private static long Number(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var n) || n < 0) throw new EvidenceException(FindingKind.Invalid, "JSON_INTEGER");
        return n;
    }
    private static int Int(JsonElement value) { var n = Number(value); if (n > int.MaxValue) throw new EvidenceException(FindingKind.Invalid, "INTEGER_OVERFLOW"); return (int)n; }
    private static string[] Strings(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(Text).ToArray() : throw new EvidenceException(FindingKind.Invalid, "JSON_TYPE");
    private static Dictionary<int, long> Distribution(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new EvidenceException(FindingKind.Invalid, "JSON_TYPE");
        var result = new Dictionary<int, long>();
        foreach (var p in value.EnumerateObject())
        {
            if (!int.TryParse(p.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size < 2 ||
                p.Name != size.ToString(CultureInfo.InvariantCulture) || !result.TryAdd(size, Number(p.Value))) throw new EvidenceException(FindingKind.Invalid, "DISTRIBUTION_KEY");
        }
        return result;
    }
    public static Summary ParseSummary(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        using var document = Json(bytes, policy, cancellation); var r = document.RootElement; Fields(r, SummaryFields);
        var tool = Text(r.GetProperty("tool_version")); var schema = Int(r.GetProperty("schema_version")); var phase = Text(r.GetProperty("phase"));
        if (tool != "0.1.0" || schema != 1 || phase != Phase) throw new EvidenceException(FindingKind.Invalid, "REPORT_VERSION");
        var stats = r.GetProperty("hash_cache_statistics"); Fields(stats, StatsFields);
        var counts = r.GetProperty("relationship_counts"); Fields(counts, Relationships.Labels);
        var diagnostics = r.GetProperty("diagnostics");
        if (diagnostics.ValueKind != JsonValueKind.Array) throw new EvidenceException(FindingKind.Invalid, "JSON_TYPE");
        var ds = new List<ReportDiagnostic>();
        foreach (var d in diagnostics.EnumerateArray())
        {
            Fields(d, ["severity", "code", "path", "message"]);
            var severity = Text(d.GetProperty("severity"));
            if (severity is not ("WARNING" or "ERROR")) throw new EvidenceException(FindingKind.Invalid, "DIAGNOSTIC_SEVERITY");
            ds.Add(new(severity, Text(d.GetProperty("code")), Text(d.GetProperty("path")), Text(d.GetProperty("message"))));
        }
        var started = Text(r.GetProperty("scan_started_utc")); var completed = Text(r.GetProperty("scan_completed_utc"));
        if (!Provenance.Utc(started, out var from) || !Provenance.Utc(completed, out var to) || to < from || Number(r.GetProperty("scan_id")) == 0)
            throw new EvidenceException(FindingKind.Invalid, "SUMMARY_SCAN");
        return new(tool, schema, phase, Number(r.GetProperty("scan_id")), started, completed, Strings(r.GetProperty("roots")),
            Number(r.GetProperty("current_chart_files")), Number(r.GetProperty("verified_chart_files")), Number(r.GetProperty("unverified_previously_indexed_chart_files")),
            Number(r.GetProperty("unique_sha256")), Number(r.GetProperty("unique_md5")), Number(r.GetProperty("physical_chart_folders")),
            Number(r.GetProperty("analyzed_chart_folders")), Strings(r.GetProperty("incomplete_folders")), Number(r.GetProperty("duplicate_sha256_groups")),
            Number(r.GetProperty("candidate_folder_pairs")), counts.EnumerateObject().ToDictionary(p => p.Name, p => Number(p.Value), StringComparer.Ordinal),
            Number(r.GetProperty("cluster_count")), Distribution(r.GetProperty("cluster_size_distribution")), Number(r.GetProperty("exact_only_cluster_count")),
            Distribution(r.GetProperty("exact_only_cluster_size_distribution")), new(Number(stats.GetProperty("discovered")), Number(stats.GetProperty("reused")),
                Number(stats.GetProperty("hashed")), Number(stats.GetProperty("new")), Number(stats.GetProperty("changed")), Number(stats.GetProperty("missing")),
                Number(stats.GetProperty("errored")), Number(stats.GetProperty("unstable"))), Number(r.GetProperty("warning_count")), Number(r.GetProperty("error_count")), ds, Text(r.GetProperty("evidence_notice")));
    }
    public static FormatRecognition RecognizeSummary(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy,
        SummaryOracle? oracle = null, CancellationToken cancellation = default)
    {
        string text;
        try { text = Decode(bytes, policy); ParseSummary(bytes, policy, cancellation); }
        catch (EvidenceException ex) when (ex.Kind != FindingKind.ResourceLimit)
        { return new(ex.Kind == FindingKind.Pending ? SummaryProfile.Unsupported : SummaryProfile.Malformed, false, false, null, ex.Code); }
        var profile = SummaryProfile.Unsupported;
        var inside = false; var escape = false; var breaks = new List<bool>(); var bad = false;
        for (var i = 0; i < text.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested(); var c = text[i];
            if (inside) { if (escape) escape = false; else if (c == '\\') escape = true; else if (c == '"') inside = false; continue; }
            if (c == '"') { inside = true; continue; }
            if (c == '\r') { if (i + 1 >= text.Length || text[i + 1] != '\n') bad = true; else { breaks.Add(true); i++; } }
            else if (c == '\n') breaks.Add(false);
            var previous = i - (c == '\r' ? 2 : 1);
            if ((c == '\r' || c == '\n') && previous >= 0 && text[previous] is ' ' or '\t') bad = true;
        }
        if (!bad && text.EndsWith("}\n", StringComparison.Ordinal) && breaks.Count >= 2 && !breaks[^1])
        {
            if (breaks.All(b => !b)) profile = SummaryProfile.CanonicalLf;
            else if (breaks.Take(breaks.Count - 1).All(b => b)) profile = SummaryProfile.WindowsLegacy;
        }
        var exact = false; string? id = null;
        if (oracle is not null)
        {
            if (oracle.ReportType != "summary.json" || oracle.ToolVersion != "0.1.0" || oracle.SchemaVersion != 1 || oracle.Phase != Phase || string.IsNullOrWhiteSpace(oracle.SpecificationId))
                return new(SummaryProfile.Unsupported, false, false, null, "ORACLE_BINDING_MISMATCH");
            policy.Text(oracle.SpecificationId);
            policy.Limit(oracle.CanonicalBytes.Length > policy.MaxInputBytes || oracle.LegacyBytes.Length > policy.MaxInputBytes, "ORACLE_BYTE_LIMIT");
            exact = profile == SummaryProfile.CanonicalLf ? bytes.Span.SequenceEqual(oracle.CanonicalBytes.Span) :
                profile == SummaryProfile.WindowsLegacy && bytes.Span.SequenceEqual(oracle.LegacyBytes.Span);
            id = oracle.SpecificationId;
            if (!exact) profile = SummaryProfile.Unsupported;
        }
        return new(profile, profile is SummaryProfile.CanonicalLf or SummaryProfile.WindowsLegacy, exact, id,
            exact ? "SYNTHETIC_ORACLE_MATCH_EXTERNAL_PROVENANCE_PENDING" : "EXACT_ORACLE_AND_EXTERNAL_PROVENANCE_REQUIRED");
    }
    public static IReadOnlyList<string[]> ParseCsv(ReadOnlyMemory<byte> bytes, IReadOnlyList<string> header,
        ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        var text = Decode(bytes, policy);
        policy.Limit(header.Count > policy.MaxReportRows, "HEADER_LIMIT");
        foreach (var name in header) policy.Text(name);
        if (header.Count == 0 || header.Any(string.IsNullOrEmpty) || header.Distinct(StringComparer.Ordinal).Count() != header.Count)
            throw new EvidenceException(FindingKind.Invalid, "CSV_HEADER");
        if (text.StartsWith('\ufeff')) throw new EvidenceException(FindingKind.Pending, "BOM_UNSUPPORTED");
        var rows = new List<string[]>(); var fields = new List<string>(); var field = new StringBuilder();
        var quoted = false; var ended = false; var start = true;
        void FinishField() { fields.Add(field.ToString()); field.Clear(); ended = false; start = true; if (fields.Count > header.Count) throw new EvidenceException(FindingKind.Invalid, "CSV_FIELD_COUNT"); }
        void FinishRow()
        {
            FinishField(); if (fields.Count != header.Count) throw new EvidenceException(FindingKind.Invalid, "CSV_FIELD_COUNT");
            rows.Add(fields.ToArray()); fields.Clear(); policy.Limit(rows.Count - 1 > policy.MaxReportRows, "REPORT_ROW_LIMIT");
        }
        for (var i = 0; i < text.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested(); var c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; ended = true; } }
                else field.Append(c);
            }
            else if (c == ',') FinishField();
            else if (c == '\n') FinishRow();
            else if (c == '\r') throw new EvidenceException(FindingKind.Pending, "CSV_CRLF_UNSUPPORTED");
            else if (c == '"' && start) { quoted = true; start = false; }
            else
            {
                if (ended || c == '"') throw new EvidenceException(FindingKind.Invalid, "CSV_QUOTING");
                field.Append(c); start = false;
            }
            policy.Limit(field.Length > policy.MaxStringChars, "FIELD_LIMIT");
        }
        if (quoted) throw new EvidenceException(FindingKind.Invalid, "CSV_QUOTING");
        if (!text.EndsWith('\n') || fields.Count != 0 || field.Length != 0 || ended) throw new EvidenceException(FindingKind.Pending, "CSV_FINAL_LF_REQUIRED");
        if (rows.Count == 0 || !rows[0].SequenceEqual(header, StringComparer.Ordinal)) throw new EvidenceException(FindingKind.Invalid, "CSV_HEADER");
        return rows.Skip(1).ToArray();
    }
    private static long Integer(string text)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0) throw new EvidenceException(FindingKind.Invalid, "CSV_INTEGER");
        if (text != n.ToString(CultureInfo.InvariantCulture)) throw new EvidenceException(FindingKind.Pending, "CSV_NUMBER_FORMAT");
        return n;
    }
    private static int Small(string text) { var n = Integer(text); if (n > int.MaxValue) throw new EvidenceException(FindingKind.Invalid, "INTEGER_OVERFLOW"); return (int)n; }
    private static double Ratio(string text)
    {
        if (!double.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0 || n > 1)
            throw new EvidenceException(FindingKind.Invalid, "CSV_RATIO");
        if (text != n.ToString(CultureInfo.InvariantCulture)) throw new EvidenceException(FindingKind.Pending, "CSV_NUMBER_FORMAT");
        return n;
    }
    public static IReadOnlyList<DuplicateRow> ParseDuplicates(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        var rows = ParseCsv(bytes, ["sha256", "md5", "group_paths", "root_id", "absolute_path", "parent_folder", "filename", "extension", "size", "mtime_utc"], policy, cancellation);
        var result = new List<DuplicateRow>(); var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Provenance.Digest(r[0], 64) || !Provenance.Digest(r[1], 32) || Integer(r[2]) < 2 || Integer(r[3]) == 0 ||
                !Provenance.Utc(r[9], out _) || !paths.Add(r[4])) throw new EvidenceException(FindingKind.Invalid, "DUPLICATE_ROW");
            result.Add(new(r[0], r[1], Integer(r[2]), Integer(r[3]), r[4], r[5], r[6], r[7], Integer(r[8]), r[9]));
        }
        return result;
    }
    public static IReadOnlyList<Candidate> ParseCandidates(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        var rows = ParseCsv(bytes, ["folder_a", "folder_b", "physical_files_a", "physical_files_b", "charts_a", "charts_b", "common", "coverage_a", "coverage_b", "jaccard", "relationship"], policy, cancellation);
        var result = new List<Candidate>(); var pairs = new HashSet<(string, string)>();
        foreach (var r in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            if (StringComparer.Ordinal.Compare(r[0], r[1]) >= 0 || !pairs.Add((r[0], r[1])) || !Relationships.Labels.Contains(r[10], StringComparer.Ordinal))
                throw new EvidenceException(FindingKind.Invalid, "CANDIDATE_ROW");
            var physicalA = Small(r[2]); var physicalB = Small(r[3]); var a = Small(r[4]); var b = Small(r[5]); var common = Small(r[6]);
            var coverageA = Ratio(r[7]); var coverageB = Ratio(r[8]); var jaccard = Ratio(r[9]);
            if (a == 0 || b == 0 || common == 0 || common > Math.Min(a, b) || physicalA < a || physicalB < b ||
                coverageA != (double)common / a || coverageB != (double)common / b || jaccard != (double)common / ((long)a + b - common) ||
                r[10] != Relationships.Classify(a, b, common)) throw new EvidenceException(FindingKind.Invalid, "CANDIDATE_MATH");
            result.Add(new(r[0], r[1], physicalA, physicalB, a, b, common, coverageA, coverageB, jaccard, r[10]));
        }
        return result;
    }
    public static IReadOnlyList<ClusterRow> ParseClusters(ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        var rows = ParseCsv(bytes, ["kind", "cluster_id", "folder_count", "folder_path"], policy, cancellation);
        var result = new List<ClusterRow>(); var members = new HashSet<(string, string, string)>();
        foreach (var r in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            var prefix = r[0] + "_";
            if (r[0] is not ("RELATED" or "EXACT_ONLY") || !r[1].StartsWith(prefix, StringComparison.Ordinal) || r[1].Length != prefix.Length + 6 ||
                !r[1][prefix.Length..].All(char.IsAsciiDigit) || Integer(r[1][prefix.Length..].TrimStart('0')) == 0 || Small(r[2]) < 2 || !members.Add((r[0], r[1], r[3])))
                throw new EvidenceException(FindingKind.Invalid, "CLUSTER_ROW");
            result.Add(new(r[0], r[1], Small(r[2]), r[3]));
        }
        return result;
    }
}
