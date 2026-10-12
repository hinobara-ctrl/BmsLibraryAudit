using System.Globalization;

namespace BmsLibraryAudit.SourceInspector.Offline;

public static class Provenance
{
    internal static readonly string[] DiagnosticCodes = ["DIRECTORY_UNREADABLE", "REPARSE_SKIPPED", "ENTRY_METADATA", "CHART_READ", "CHART_UNSTABLE"];
    public static bool Digest(string? text, int length) => text is not null && text.Length == length &&
        text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Utc(string? text, out DateTimeOffset date) => DateTimeOffset.TryParseExact(text, "O",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && date.Offset == TimeSpan.Zero;

    public static LogicalValidation Validate(Evidence evidence, long expectedScan, ProcessingPolicy policy,
        CancellationToken cancellation = default)
    {
        policy.Validate(); cancellation.ThrowIfCancellationRequested();
        policy.Limit(evidence.Charts.Count > policy.MaxChartRecords, "CHART_LIMIT");
        policy.Limit(evidence.Roots.Count > policy.MaxFolders, "ROOT_LIMIT");
        policy.Limit(evidence.Scans.Count > policy.MaxReportRows || evidence.Diagnostics.Count > policy.MaxReportRows, "ROW_LIMIT");
        var findings = new Findings(policy);
        void Invalid(bool condition, string code, string detail) { if (condition) findings.Add(FindingKind.Invalid, code, detail); }
        void Pending(string code) => findings.Add(FindingKind.Pending, code, "Supplied logical evidence does not establish this observation.");
        var o = evidence.Ownership;
        if (o is null || o.ApplicationId is null || o.UserVersion is null || o.Tool is null || o.SchemaVersion is null || o.Comparison is null)
            Pending("OWNERSHIP_EVIDENCE_MISSING");
        if (o is not null)
        {
            Invalid(o.ApplicationId is not null && o.ApplicationId != 0x424D5341 || o.UserVersion is not null && o.UserVersion != 1 ||
                o.Tool is not null && o.Tool != "BmsLibraryAudit" || o.SchemaVersion is not null && o.SchemaVersion != "1" ||
                o.Comparison is not null && o.Comparison != "windows-ordinal-ignore-case", "DECLARED_OWNERSHIP", "Supplied ownership fields disagree.");
            foreach (var s in new[] { o.Tool, o.SchemaVersion, o.Comparison }) if (s is not null) policy.Text(s);
        }
        var scanIds = new HashSet<long>();
        Scan? latest = null;
        foreach (var scan in evidence.Scans)
        {
            cancellation.ThrowIfCancellationRequested(); policy.Text(scan.StartedUtc); policy.Text(scan.Status);
            if (scan.CompletedUtc is not null) policy.Text(scan.CompletedUtc);
            Invalid(scan.Id <= 0 || !scanIds.Add(scan.Id), "SCAN_ID", "Scan IDs must be unique and positive.");
            Invalid(scan.RehashAll is not (0 or 1), "SCAN_BOOLEAN", "Invalid supplied rehash_all value.");
            Invalid(!Utc(scan.StartedUtc, out var start), "SCAN_TIME", "Invalid UTC start timestamp.");
            if (scan.Status == "COMPLETED")
            {
                Invalid(!Utc(scan.CompletedUtc, out var end) || end < start, "SCAN_TIME", "Invalid completion timestamp.");
                if (latest is null || scan.Id > latest.Id) latest = scan;
            }
            else if (scan.Status == "RUNNING") Pending("UNSETTLED_SCAN");
            else Invalid(true, "SCAN_STATUS", "Unsupported supplied scan status.");
            if (scan.Roots is null) Pending("SCAN_ROOTS_MISSING");
            else { policy.Limit(scan.Roots.Count > policy.MaxFolders, "ROOT_LIMIT"); foreach (var root in scan.Roots) policy.Text(root); }
            if (scan.Statistics is { } stats)
            {
                var values = new[] { stats.Discovered, stats.Reused, stats.Hashed, stats.New, stats.Changed, stats.Missing, stats.Errored, stats.Unstable };
                Invalid(values.Any(n => n < 0) || stats.Unstable > stats.Errored || stats.New > stats.Discovered || stats.Changed > stats.Discovered,
                    "STATISTICS", "Inconsistent scan counters.");
                try
                {
                    Invalid(checked(stats.Reused + stats.Hashed + stats.Errored) != stats.Discovered ||
                        checked(stats.New + stats.Changed) > checked(stats.Hashed + stats.Errored) || scan.RehashAll == 1 && stats.Reused != 0,
                        "STATISTICS", "Inconsistent discovered/cache partition; unstable overlaps errored.");
                }
                catch (OverflowException) { Invalid(true, "INTEGER_OVERFLOW", "Scan counters overflow."); }
            }
            else if (scan.Status == "COMPLETED") Pending("STATISTICS_MISSING");
        }
        Invalid(latest is null, "NO_COMPLETED_SCAN", "No completed scan supplied.");
        Invalid(expectedScan <= 0, "EXPECTED_SCAN", "Expected scan must be positive.");
        ScanScopeChange? scopeChange = null;
        if (latest is not null && expectedScan > 0 && latest.Id != expectedScan)
        {
            scopeChange = new(expectedScan, latest.Id, scanIds.Contains(expectedScan));
            findings.Add(FindingKind.ScopeChanged, "EXPECTED_SCAN_CHANGED", "Frozen expected scan differs from actual latest COMPLETED scan; future native disposition is BLOCKED.");
        }
        var roots = new Dictionary<long, Root>(); var rootKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in evidence.Roots)
        {
            cancellation.ThrowIfCancellationRequested(); policy.Text(root.Path); policy.Text(root.Key);
            Invalid(root.Id <= 0 || !roots.TryAdd(root.Id, root), "ROOT_ID", "Root IDs must be positive and unique.");
            Invalid(root.Active is not (0 or 1), "ROOT_BOOLEAN", "Invalid active flag.");
            try { Invalid(root.Key != WindowsNames.Key(root.Path) || !rootKeys.Add(root.Key), "ROOT_KEY", "Invalid/duplicate root comparison key."); }
            catch (EvidenceException ex) when (ex.Kind == FindingKind.Invalid) { Invalid(true, ex.Code, "Invalid root path."); }
        }
        var historicalRoots = new Dictionary<long, HashSet<string>>();
        foreach (var scan in evidence.Scans)
        {
            if (scan.Roots is null) continue;
            var history = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in scan.Roots)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    var key = WindowsNames.Key(path);
                    Invalid(!history.Add(key) || !rootKeys.Contains(key), "HISTORICAL_ROOT_REFERENCE", "Scan roots must be unique captured root records.");
                }
                catch (EvidenceException ex) when (ex.Kind == FindingKind.Invalid) { Invalid(true, ex.Code, "Invalid historical scan root."); }
            }
            foreach (var key in history)
            {
                var parent = key;
                while (parent.Length > 3)
                {
                    cancellation.ThrowIfCancellationRequested(); parent = WindowsNames.Parent(parent);
                    Invalid(history.Contains(parent), "HISTORICAL_ROOT_OVERLAP", "Historical configured roots overlap.");
                }
            }
            historicalRoots.TryAdd(scan.Id, history);
        }
        if (latest?.Roots is { } configured)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in configured)
            {
                try { Invalid(!keys.Add(WindowsNames.Key(root)), "ROOT_CONFIGURATION", "Duplicate configured root."); }
                catch (EvidenceException ex) when (ex.Kind == FindingKind.Invalid) { Invalid(true, ex.Code, "Invalid configured root path."); }
            }
            Invalid(!keys.SetEquals(evidence.Roots.Where(r => r.Active == 1).Select(r => r.Key)), "ROOT_CONFIGURATION", "Configured roots differ from active records.");
            foreach (var key in keys)
            {
                var parent = key;
                while (parent.Length > 3)
                {
                    parent = WindowsNames.Parent(parent);
                    Invalid(keys.Contains(parent), "OVERLAPPING_ROOTS", "Configured roots overlap.");
                }
            }
        }
        var chartKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chart in evidence.Charts)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var s in new[] { chart.Path, chart.Key, chart.Parent, chart.Name, chart.Extension, chart.Status }) policy.Text(s);
            if (chart.Sha256 is not null) policy.Text(chart.Sha256); if (chart.Md5 is not null) policy.Text(chart.Md5);
            Invalid(!chartKeys.Add(chart.Key), "DUPLICATE_CHART", "Duplicate supplied chart key.");
            var hasRoot = roots.TryGetValue(chart.RootId, out var root);
            Invalid(!hasRoot, "CHART_ROOT", "Unknown chart root.");
            Invalid(chart.Present is not (0 or 1), "CHART_BOOLEAN", "Invalid present flag.");
            Invalid(!scanIds.Contains(chart.FirstScan) || !scanIds.Contains(chart.LastScan) || chart.FirstScan > chart.LastScan,
                "CHART_SCAN", "Invalid scan references/order.");
            Invalid(chart.Status is not ("OK" or "ERROR" or "UNSTABLE" or "MISSING" or "UNVERIFIED" or "INACTIVE"), "CHART_STATUS", "Unsupported chart status.");
            Invalid(chart.Present == 1 && (root?.Active != 1 || chart.LastScan != latest?.Id || chart.Status is "MISSING" or "UNVERIFIED" or "INACTIVE"),
                "PRESENT_PROVENANCE", "Present chart lacks active latest-scan provenance.");
            Invalid(chart.Present == 0 && chart.Status is "OK" or "ERROR" or "UNSTABLE", "ABSENT_STATUS", "Observed status requires a present chart.");
            Invalid(chart.Status == "INACTIVE" && root?.Active != 0, "INACTIVE_ROOT", "Inactive chart requires inactive root.");
            try
            {
                Invalid(chart.Key != WindowsNames.Key(chart.Path) || !WindowsNames.Comparer.Equals(chart.Parent, WindowsNames.Parent(chart.Path)) ||
                    chart.Name != WindowsNames.Name(chart.Path) || chart.Extension != WindowsNames.Extension(chart.Path) ||
                    hasRoot && (!WindowsNames.Contains(root!.Path, chart.Path) || WindowsNames.Comparer.Equals(root.Path, chart.Path)), "CHART_PATH", "Chart path/name/parent/root mismatch.");
                foreach (var reference in new[] { chart.FirstScan, chart.LastScan })
                    if (historicalRoots.TryGetValue(reference, out var configuredRoots))
                        Invalid(!configuredRoots.Any(p => WindowsNames.Contains(p, chart.Path)), "CHART_SCAN_ROOT", "A seen chart must belong to its recorded scan's configured roots.");
            }
            catch (EvidenceException ex) when (ex.Kind == FindingKind.Invalid) { Invalid(true, ex.Code, "Invalid chart path."); }
            Invalid(!new[] { ".bms", ".bme", ".bml", ".pms", ".bmson" }.Contains(chart.Extension, StringComparer.OrdinalIgnoreCase), "CHART_EXTENSION", "Unknown chart extension.");
            Invalid(chart.Sha256 is not null && !Digest(chart.Sha256, 64) || chart.Md5 is not null && !Digest(chart.Md5, 32) ||
                chart.Status == "OK" && (chart.Sha256 is null || chart.Md5 is null || chart.Size < 0) || chart.Size < -1 ||
                chart.MtimeTicks < 0 || chart.MtimeTicks > DateTime.MaxValue.Ticks, "CHART_HASH_METADATA", "Invalid digest or known metadata; ERROR size=-1 remains unknown.");
        }
        var diagnosticIds = new HashSet<long>();
        foreach (var d in evidence.Diagnostics)
        {
            cancellation.ThrowIfCancellationRequested(); foreach (var s in new[] { d.Path, d.Severity, d.Code, d.Message }) policy.Text(s);
            Invalid(d.Id <= 0 || !diagnosticIds.Add(d.Id) || !scanIds.Contains(d.ScanId), "DIAGNOSTIC_REFERENCE", "Invalid diagnostic ID/scan.");
            Invalid(d.Severity is not ("WARNING" or "ERROR"), "DIAGNOSTIC_SEVERITY", "Unsupported severity.");
            if (!DiagnosticCodes.Contains(d.Code, StringComparer.Ordinal)) Pending("DIAGNOSTIC_SEMANTICS_UNKNOWN");
            try { WindowsNames.Canonical(d.Path); } catch (EvidenceException ex) when (ex.Kind == FindingKind.Invalid) { Invalid(true, ex.Code, "Invalid diagnostic path."); }
        }
        if (latest?.Statistics is { } currentStats)
        {
            long present = 0, errored = 0, unstable = 0;
            foreach (var c in evidence.Charts)
            {
                cancellation.ThrowIfCancellationRequested();
                if (c.Present != 1) continue;
                present++; if (c.Status is "ERROR" or "UNSTABLE") errored++; if (c.Status == "UNSTABLE") unstable++;
            }
            Invalid(currentStats.Discovered != present || currentStats.Errored != errored || currentStats.Unstable != unstable,
                "CURRENT_STATISTICS", "Current row statuses disagree with derivable scan counters.");
        }
        Pending("DATABASE_AND_NATIVE_ORIGIN_NOT_VERIFIED");
        return new(latest, findings.Items) { ScopeChange = scopeChange };
    }
}
