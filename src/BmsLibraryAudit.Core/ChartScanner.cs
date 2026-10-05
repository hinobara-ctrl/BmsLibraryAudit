namespace BmsLibraryAudit.Core;

public sealed class ChartScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".bms", ".bme", ".bml", ".pms", ".bmson" };
    private static bool Recoverable(Exception exception) => exception is IOException or UnauthorizedAccessException;

    public ScanResult Scan(ScanOptions options, CancellationToken cancellationToken = default)
    {
        var roots = PathPolicy.ValidateRoots(options.Roots);
        var databasePath = PathPolicy.Absolute(options.DatabasePath);
        PathPolicy.RequireOutsideRoots(databasePath, roots);
        if (File.Exists(databasePath))
        {
            using var probe = AuditDatabase.Open(databasePath, writable: false);
            PathPolicy.RequireOutsideRoots(databasePath, probe.AllRoots());
        }
        using var database = AuditDatabase.Open(databasePath, writable: true);
        database.Begin();
        var scan = database.StartScan(roots, options.RehashAll);
        var statistics = new ScanStatistics();
        var diagnostics = new List<Diagnostic>();
        var blocked = new List<string>();

        foreach (var root in roots)
        {
            var rootId = database.ActivateRoot(root);
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                try
                {
                    // Recheck at visit time, since a queued directory can change.
                    PathPolicy.RejectReparseAncestors(directory);
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FileAttributes attributes;
                        try { attributes = File.GetAttributes(entry); }
                        catch (Exception exception) when (Recoverable(exception))
                        {
                            blocked.Add(entry);
                            diagnostics.Add(new("ERROR", "ENTRY_METADATA", entry, exception.Message));
                            if (Extensions.Contains(Path.GetExtension(entry)))
                                RecordChart(database, scan, rootId, entry, options.RehashAll, statistics, diagnostics, cancellationToken);
                            continue;
                        }
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            blocked.Add(entry);
                            diagnostics.Add(new("WARNING", "REPARSE_SKIPPED", entry, "Reparse point was not followed; old evidence under it is unverified."));
                            if ((attributes & FileAttributes.Directory) == 0 && Extensions.Contains(Path.GetExtension(entry)))
                                RecordChart(database, scan, rootId, entry, options.RehashAll, statistics, diagnostics, cancellationToken);
                        }
                        else if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                        else if (Extensions.Contains(Path.GetExtension(entry)))
                            RecordChart(database, scan, rootId, entry, options.RehashAll, statistics, diagnostics, cancellationToken);
                    }
                }
                catch (Exception exception) when (Recoverable(exception))
                {
                    blocked.Add(directory);
                    diagnostics.Add(new("ERROR", "DIRECTORY_UNREADABLE", directory, exception.Message));
                }
            }
        }
        foreach (var old in database.UnseenCharts(scan))
        {
            var unverified = blocked.Any(path => PathPolicy.Contains(path, old.AbsolutePath));
            if (!unverified && old.Present) statistics.Missing++;
            database.StoreChart(old with { Present = false, Status = unverified ? "UNVERIFIED" : "MISSING" });
        }
        cancellationToken.ThrowIfCancellationRequested();
        database.CompleteScan(scan, statistics, diagnostics);
        database.Commit();
        return new(scan, statistics, diagnostics);
    }

    private static void RecordChart(AuditDatabase database, long scan, long rootId, string path,
        bool rehashAll, ScanStatistics statistics, List<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        statistics.Discovered++;
        path = PathPolicy.Absolute(path);
        var normalized = PathPolicy.Normalize(path);
        var old = database.FindChart(normalized);
        if (old is null) statistics.New++;
        FileStamp? stamp = null;
        ChartDigest? digest = null;
        var status = "OK";
        try
        {
            PathPolicy.RejectReparseAncestors(path);
            stamp = ChartHasher.Stat(path);
            var changed = old is not null && (old.Size != stamp.Size || old.MtimeUtcTicks != stamp.MtimeUtcTicks);
            if (changed) statistics.Changed++;
            if (!rehashAll && !changed && old is { Status: "OK", Md5: not null, Sha256: not null })
            {
                digest = new(stamp, old.Md5, old.Sha256);
                statistics.Reused++;
            }
            else
            {
                digest = ChartHasher.Hash(path, cancellationToken);
                stamp = digest.Stamp;
                statistics.Hashed++;
            }
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            status = exception is UnstableChartException ? "UNSTABLE" : "ERROR";
            statistics.Errored++;
            if (status == "UNSTABLE") statistics.Unstable++;
            diagnostics.Add(new("ERROR", status == "UNSTABLE" ? "CHART_UNSTABLE" : "CHART_READ", path, exception.Message));
        }
        database.StoreChart(new(rootId, path, normalized, Path.GetDirectoryName(path)!, Path.GetFileName(path),
            Path.GetExtension(path), stamp?.Size ?? -1, stamp?.MtimeUtcTicks ?? 0, digest?.Md5, digest?.Sha256,
            old?.FirstSeenScan ?? scan, scan, true, status));
    }
}
