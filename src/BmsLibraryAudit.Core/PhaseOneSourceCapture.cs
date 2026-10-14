using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace BmsLibraryAudit.Core;

internal sealed record CapturedLibraryRoot(long Id, string OriginalPath, string Key, bool Active, bool Configured);
internal sealed record CapturedPhaseOne(string OriginalPath, NativePathFacts Facts, string Sha256,
    long ScanId, string StartedUtc, string CompletedUtc, IReadOnlyList<CapturedLibraryRoot> Roots,
    IReadOnlyList<ChartRecord> Charts, IReadOnlyList<Diagnostic> Diagnostics);

internal static class PhaseOneSourceCapture
{
    internal static CapturedPhaseOne Capture(PhaseTwoPreflightOptions options, PhaseTwoTestHooks? hooks,
        CancellationToken cancellationToken, Action? providerOpening = null)
    {
        var path = PathPolicy.Absolute(options.SourceDatabasePath);
        PhaseTwoSafety.ValidateDestinations(path, options.DatabasePath, options.OutputDirectory, []);
        PhaseTwoSafety.RejectSidecars(path);
        var original = PhaseTwoSafety.Inspect(path);
        if ((original.Attributes & (uint)FileAttributes.Directory) != 0) throw new IOException("Source must be a regular database.");
        using var guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        var guarded = PhaseTwoSafety.Inspect(guard.SafeFileHandle, path);
        if (original != guarded) throw new IOException("SOURCE_CHANGED: guarded identity differs.");
        ValidateHeader(guard, cancellationToken);
        providerOpening?.Invoke(); // Internal test boundary; not native I/O telemetry.
        using var connection = PhaseTwoDatabase.Connect(path, SqliteOpenMode.ReadOnly);
        PhaseTwoDatabase.RequireScalar(connection, "PRAGMA application_id", 0x424D5341);
        PhaseTwoDatabase.RequireScalar(connection, "PRAGMA user_version", 1);
        using var transaction = connection.BeginTransaction(deferred: true);
        object? Scalar(string sql) { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command.ExecuteScalar(); }
        if ((string?)Scalar("SELECT value FROM schema_metadata WHERE key='path_comparison'") != PhaseTwoDatabase.AbsoluteMode)
            throw new InvalidOperationException("Source platform comparison mismatch.");
        if ((string?)Scalar("SELECT value FROM schema_metadata WHERE key='tool'") != "BmsLibraryAudit")
            throw new InvalidOperationException("Source tool ownership mismatch.");
        if ((string?)Scalar("SELECT value FROM schema_metadata WHERE key='schema_version'") != "1" || Read("PRAGMA foreign_key_check").Count != 0)
            throw new InvalidOperationException("Source schema/foreign-key integrity failure.");
        if ((string?)Scalar("PRAGMA quick_check") != "ok") throw new InvalidOperationException("Source database integrity failure.");
        var scans = Read("SELECT id,started_utc,completed_utc,roots_json FROM scan_sessions WHERE status='COMPLETED' ORDER BY id DESC LIMIT 1");
        if (scans.Count != 1) throw new InvalidOperationException("Source has no COMPLETED scan.");
        var scan = Convert.ToInt64(scans[0][0]);
        if (scan != options.SourceScanId) throw new ArgumentException("--source-scan must equal the latest COMPLETED source scan.");
        var configured = JsonSerializer.Deserialize<string[]>((string)scans[0][3]!)
            ?? throw new InvalidOperationException("Source scan root inventory missing.");
        if (Convert.ToInt64(Scalar("SELECT count(*) FROM library_roots WHERE active NOT IN (0,1)")) != 0 ||
            Convert.ToInt64(Scalar("SELECT count(*) FROM chart_files WHERE present NOT IN (0,1)")) != 0)
            throw new InvalidOperationException("Invalid source boolean facts.");
        var roots = Read("SELECT id,absolute_path,normalized_path,active FROM library_roots ORDER BY normalized_path COLLATE BINARY")
            .Select(row => new CapturedLibraryRoot(Convert.ToInt64(row[0]), (string)row[1]!, (string)row[2]!,
                Convert.ToInt64(row[3]) == 1, configured.Any(p => PathPolicy.Normalize(p) == (string)row[2]!))).ToArray();
        foreach (var root in roots)
            if (!Path.IsPathFullyQualified(root.OriginalPath) || root.Key != PathPolicy.Normalize(root.OriginalPath) || root.Active != root.Configured)
                throw new InvalidOperationException("Inconsistent source root inventory.");
        if (configured.Any(p => !roots.Any(r => r.Key == PathPolicy.Normalize(p) && r.Active)))
            throw new InvalidOperationException("Source scan has an uncaptured root.");
        // Reject destinations using all roots before any chart read or writable DB.
        PhaseTwoSafety.ValidateDestinations(path, options.DatabasePath, options.OutputDirectory, roots.Select(r => r.OriginalPath));
        var fingerprint = Fingerprint();
        hooks?.AfterSourceFingerprint?.Invoke();
        var charts = Read("SELECT root_id,absolute_path,normalized_path,parent_folder,filename,extension,size,mtime_utc_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status FROM chart_files ORDER BY normalized_path COLLATE BINARY")
            .Select(r => new ChartRecord(Convert.ToInt64(r[0]), (string)r[1]!, (string)r[2]!, (string)r[3]!,
                (string)r[4]!, (string)r[5]!, Convert.ToInt64(r[6]), Convert.ToInt64(r[7]), (string?)r[8],
                (string?)r[9], Convert.ToInt64(r[10]), Convert.ToInt64(r[11]), Convert.ToInt64(r[12]) == 1, (string)r[13]!)).ToArray();
        foreach (var chart in charts)
        {
            var root = roots.SingleOrDefault(r => r.Id == chart.RootId) ?? throw new InvalidOperationException("Source chart has no root.");
            if (!Path.IsPathFullyQualified(chart.AbsolutePath) || chart.NormalizedPath != PathPolicy.Normalize(chart.AbsolutePath) || !PathPolicy.Contains(root.OriginalPath, chart.AbsolutePath) ||
                !PathPolicy.Comparer.Equals(chart.ParentFolder, Path.GetDirectoryName(chart.AbsolutePath)) ||
                chart.Filename != Path.GetFileName(chart.AbsolutePath) || chart.Extension != Path.GetExtension(chart.AbsolutePath) ||
                !PhaseTwoFileSystem.IsChart(chart.AbsolutePath) ||
                (chart.Present && (!root.Active || chart.LastSeenScan != scan)) ||
                (chart.Status == "OK" && (!Digest(chart.Sha256, 64) || !Digest(chart.Md5, 32) || chart.Size < 0 || chart.MtimeUtcTicks < 0)))
                throw new InvalidOperationException("Inconsistent source chart/hash provenance: " + chart.AbsolutePath);
        }
        var diagnostics = Read($"SELECT severity,code,path,message FROM diagnostics WHERE scan_id={scan} ORDER BY path COLLATE BINARY,code COLLATE BINARY,message COLLATE BINARY,severity COLLATE BINARY")
            .Select(r => new Diagnostic((string)r[0]!, (string)r[1]!, (string)r[2]!, (string)r[3]!)).ToArray();
        if (Fingerprint() != fingerprint || PhaseTwoSafety.Inspect(path) != guarded)
            throw new IOException("SOURCE_CHANGED: capture fingerprints or identity differ.");
        PhaseTwoSafety.RejectSidecars(path);
        transaction.Commit();
        return new(path, guarded, fingerprint, scan, (string)scans[0][1]!, (string)scans[0][2]!, roots, charts, diagnostics);

        List<object?[]> Read(string sql)
        {
            using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<object?[]>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray());
            }
            return rows;
        }
        string Fingerprint()
        {
            cancellationToken.ThrowIfCancellationRequested();
            guard.Position = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024]; long bytes = 0; int count;
            while ((count = guard.Read(buffer, 0, buffer.Length)) != 0)
            { cancellationToken.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); bytes += count; }
            if (bytes != guarded.Size || PhaseTwoSafety.Inspect(guard.SafeFileHandle, path) != guarded || PhaseTwoSafety.Inspect(path) != guarded)
                throw new IOException("SOURCE_CHANGED: unstable database fingerprint.");
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
    }
    internal static void ValidateHeader(Stream source, CancellationToken cancellationToken)
    {
        var position = source.Position;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.Position = 0;
            Span<byte> header = stackalloc byte[100];
            var received = 0;
            while (received < header.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = source.Read(header[received..]);
                cancellationToken.ThrowIfCancellationRequested();
                if (count == 0) throw new InvalidOperationException("SOURCE_HEADER_INVALID: truncated SQLite header.");
                received += count;
            }
            if (!header[..16].SequenceEqual("SQLite format 3\0"u8))
                throw new InvalidOperationException("SOURCE_HEADER_INVALID: incorrect SQLite magic.");
            // Offset 18 is the write version; offset 19 is the read version.
            if (header[18] != 1 || header[19] != 1)
                throw new InvalidOperationException("SOURCE_HEADER_UNSUPPORTED: only SQLite rollback format 1/1 is supported; WAL, mixed and unknown formats are refused before provider access.");
        }
        finally { source.Position = position; }
    }
    internal static bool Digest(string? value, int length) => value is not null && value.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
