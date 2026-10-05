using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace BmsLibraryAudit.Core;

public sealed class AuditDatabase : IDisposable
{
    public const int SchemaVersion = 1;
    private const int ApplicationId = 0x424D5341;
    private readonly SqliteConnection connection;
    private SqliteTransaction? transaction;
    public string Path { get; }

    private AuditDatabase(string path, SqliteConnection connection)
    {
        Path = path;
        this.connection = connection;
    }

    public static AuditDatabase Open(string path, bool writable)
    {
        path = PathPolicy.Absolute(path);
        PathPolicy.RejectReparseAncestors(path);
        if (!string.Equals(System.IO.Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Audit database must have a .db extension.");
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
        {
            PathPolicy.RejectReparseAncestors(path + suffix);
            if (File.Exists(path + suffix) || Directory.Exists(path + suffix))
                throw new InvalidOperationException($"Existing SQLite sidecar requires manual inspection before access: {path + suffix}");
        }
        var exists = File.Exists(path);
        if (!exists && !writable) throw new FileNotFoundException("Audit database does not exist.", path);
        if (exists)
        {
            using var probe = Connect(path, SqliteOpenMode.ReadOnly);
            ValidateOwnership(probe);
        }
        else
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            // Reserve without truncating any file that appeared since the existence check.
            using var reservation = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        var connection = Connect(path, writable ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly);
        var database = new AuditDatabase(path, connection);
        try
        {
            if (!exists) database.Initialize();
            else ValidateOwnership(connection);
            database.Execute("PRAGMA foreign_keys=ON;");
            return database;
        }
        catch { database.Dispose(); throw; }
    }

    private static SqliteConnection Connect(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 30 }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static void ValidateOwnership(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA application_id;";
        if (Convert.ToInt32(command.ExecuteScalar()) != ApplicationId)
            throw new InvalidOperationException("Refusing foreign database: missing BmsLibraryAudit application ID.");
        command.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(command.ExecuteScalar()) != SchemaVersion)
            throw new InvalidOperationException("Unsupported audit database schema version.");
        command.CommandText = "SELECT value FROM schema_metadata WHERE key='path_comparison';";
        if ((string?)command.ExecuteScalar() != (OperatingSystem.IsWindows() ? "windows-ordinal-ignore-case" : "ordinal"))
            throw new InvalidOperationException("Audit database was created with a different platform path comparison.");
    }

    private void Initialize()
    {
        Begin();
        Execute($"PRAGMA application_id={ApplicationId}; PRAGMA user_version={SchemaVersion};");
        Execute("""
            CREATE TABLE schema_metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE scan_sessions(
                id INTEGER PRIMARY KEY, started_utc TEXT NOT NULL, completed_utc TEXT,
                status TEXT NOT NULL, rehash_all INTEGER NOT NULL,
                roots_json TEXT NOT NULL, statistics_json TEXT);
            CREATE TABLE library_roots(
                id INTEGER PRIMARY KEY, absolute_path TEXT NOT NULL,
                normalized_path TEXT NOT NULL UNIQUE, active INTEGER NOT NULL);
            CREATE TABLE chart_files(
                normalized_path TEXT PRIMARY KEY, root_id INTEGER NOT NULL REFERENCES library_roots(id),
                absolute_path TEXT NOT NULL, parent_folder TEXT NOT NULL,
                filename TEXT NOT NULL, extension TEXT NOT NULL,
                size INTEGER NOT NULL, mtime_utc_ticks INTEGER NOT NULL,
                md5 TEXT, sha256 TEXT,
                first_seen_scan INTEGER NOT NULL REFERENCES scan_sessions(id),
                last_seen_scan INTEGER NOT NULL REFERENCES scan_sessions(id),
                present INTEGER NOT NULL, status TEXT NOT NULL,
                CHECK(status <> 'OK' OR (md5 IS NOT NULL AND sha256 IS NOT NULL)));
            CREATE INDEX charts_current_hash ON chart_files(present, status, sha256);
            CREATE INDEX charts_root_scan ON chart_files(root_id, last_seen_scan);
            CREATE TABLE diagnostics(
                id INTEGER PRIMARY KEY, scan_id INTEGER NOT NULL REFERENCES scan_sessions(id),
                severity TEXT NOT NULL, code TEXT NOT NULL, path TEXT NOT NULL, message TEXT NOT NULL);
            """);
        Execute("INSERT INTO schema_metadata VALUES('schema_version',$version),('tool','BmsLibraryAudit'),('path_comparison',$comparison);",
            ("$version", SchemaVersion.ToString()),
            ("$comparison", OperatingSystem.IsWindows() ? "windows-ordinal-ignore-case" : "ordinal"));
        Commit();
    }

    public void Begin(bool readOnly = false) => transaction = connection.BeginTransaction(deferred: readOnly);
    public void Commit() { transaction!.Commit(); transaction.Dispose(); transaction = null; }
    private SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        return command;
    }
    public void Execute(string sql, params (string Name, object? Value)[] parameters)
    { using var command = Command(sql, parameters); command.ExecuteNonQuery(); }
    private long Scalar(string sql, params (string Name, object? Value)[] parameters)
    { using var command = Command(sql, parameters); return Convert.ToInt64(command.ExecuteScalar()); }

    public IReadOnlyList<string> AllRoots()
    {
        using var command = Command("SELECT absolute_path FROM library_roots ORDER BY normalized_path;");
        using var reader = command.ExecuteReader();
        var roots = new List<string>();
        while (reader.Read()) roots.Add(reader.GetString(0));
        return roots;
    }

    public long StartScan(IReadOnlyList<string> roots, bool rehashAll)
    {
        Execute("INSERT INTO scan_sessions(started_utc,status,rehash_all,roots_json) VALUES($date,'RUNNING',$rehash,$roots);",
            ("$date", DateTimeOffset.UtcNow.ToString("O")), ("$rehash", rehashAll), ("$roots", JsonSerializer.Serialize(roots)));
        Execute("UPDATE library_roots SET active=0;");
        return Scalar("SELECT last_insert_rowid();");
    }

    public long ActivateRoot(string root)
    {
        Execute("""
            INSERT INTO library_roots(absolute_path,normalized_path,active) VALUES($path,$normalized,1)
            ON CONFLICT(normalized_path) DO UPDATE SET absolute_path=excluded.absolute_path,active=1;
            """, ("$path", root), ("$normalized", PathPolicy.Normalize(root)));
        return Scalar("SELECT id FROM library_roots WHERE normalized_path=$path;", ("$path", PathPolicy.Normalize(root)));
    }

    private const string ChartColumns = "root_id,absolute_path,normalized_path,parent_folder,filename,extension,size,mtime_utc_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status";
    private static ChartRecord ReadChart(SqliteDataReader reader) => new(reader.GetInt64(0), reader.GetString(1),
        reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt64(6),
        reader.GetInt64(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.GetInt64(10), reader.GetInt64(11), reader.GetBoolean(12), reader.GetString(13));

    public ChartRecord? FindChart(string normalizedPath)
    {
        using var command = Command($"SELECT {ChartColumns} FROM chart_files WHERE normalized_path=$path;", ("$path", normalizedPath));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadChart(reader) : null;
    }

    public void StoreChart(ChartRecord chart)
    {
        Execute("""
            INSERT INTO chart_files VALUES($normalized,$root,$path,$parent,$filename,$extension,$size,$mtime,$md5,$sha256,$first,$last,$present,$status)
            ON CONFLICT(normalized_path) DO UPDATE SET
              root_id=excluded.root_id,absolute_path=excluded.absolute_path,parent_folder=excluded.parent_folder,
              filename=excluded.filename,extension=excluded.extension,size=excluded.size,mtime_utc_ticks=excluded.mtime_utc_ticks,
              md5=excluded.md5,sha256=excluded.sha256,last_seen_scan=excluded.last_seen_scan,present=excluded.present,status=excluded.status;
            """, ("$normalized", chart.NormalizedPath), ("$root", chart.RootId), ("$path", chart.AbsolutePath),
            ("$parent", chart.ParentFolder), ("$filename", chart.Filename), ("$extension", chart.Extension),
            ("$size", chart.Size), ("$mtime", chart.MtimeUtcTicks), ("$md5", chart.Md5), ("$sha256", chart.Sha256),
            ("$first", chart.FirstSeenScan), ("$last", chart.LastSeenScan), ("$present", chart.Present), ("$status", chart.Status));
    }

    public IReadOnlyList<ChartRecord> UnseenCharts(long scan)
    {
        using var command = Command($"SELECT {ChartColumns} FROM chart_files WHERE root_id IN (SELECT id FROM library_roots WHERE active=1) AND last_seen_scan<>$scan;", ("$scan", scan));
        using var reader = command.ExecuteReader();
        var result = new List<ChartRecord>();
        while (reader.Read()) result.Add(ReadChart(reader));
        return result;
    }

    public void CompleteScan(long scan, ScanStatistics statistics, IReadOnlyList<Diagnostic> diagnostics)
    {
        Execute("UPDATE chart_files SET present=0,status='INACTIVE' WHERE root_id IN (SELECT id FROM library_roots WHERE active=0);");
        foreach (var item in diagnostics)
            Execute("INSERT INTO diagnostics(scan_id,severity,code,path,message) VALUES($scan,$severity,$code,$path,$message);",
                ("$scan", scan), ("$severity", item.Severity), ("$code", item.Code), ("$path", item.Path), ("$message", item.Message));
        Execute("UPDATE scan_sessions SET completed_utc=$date,status='COMPLETED',statistics_json=$statistics WHERE id=$scan;",
            ("$date", DateTimeOffset.UtcNow.ToString("O")), ("$statistics", JsonSerializer.Serialize(statistics)), ("$scan", scan));
    }

    public ScanSnapshot Snapshot()
    {
        Begin(readOnly: true);
        using var command = Command("SELECT id,started_utc,completed_utc,roots_json,statistics_json FROM scan_sessions WHERE status='COMPLETED' ORDER BY id DESC LIMIT 1;");
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("Database has no completed scan.");
        var id = reader.GetInt64(0);
        var started = reader.GetString(1);
        var completed = reader.GetString(2);
        var roots = JsonSerializer.Deserialize<string[]>(reader.GetString(3))!;
        var stats = JsonSerializer.Deserialize<ScanStatistics>(reader.GetString(4))!;
        reader.Close();
        using var chartsCommand = Command($"SELECT {ChartColumns} FROM chart_files WHERE present=1 ORDER BY normalized_path;");
        using var chartsReader = chartsCommand.ExecuteReader();
        var charts = new List<ChartRecord>();
        while (chartsReader.Read()) charts.Add(ReadChart(chartsReader));
        chartsReader.Close();
        using var diagnosticCommand = Command("SELECT severity,code,path,message FROM diagnostics WHERE scan_id=$scan ORDER BY path,code,message;", ("$scan", id));
        using var diagnosticReader = diagnosticCommand.ExecuteReader();
        var diagnostics = new List<Diagnostic>();
        while (diagnosticReader.Read()) diagnostics.Add(new(diagnosticReader.GetString(0), diagnosticReader.GetString(1), diagnosticReader.GetString(2), diagnosticReader.GetString(3)));
        diagnosticReader.Close();
        var unverified = Scalar("SELECT COUNT(*) FROM chart_files WHERE status='UNVERIFIED' AND root_id IN (SELECT id FROM library_roots WHERE active=1);");
        Commit();
        return new(id, started, completed, roots, charts, stats, diagnostics, unverified);
    }

    public void Dispose() { transaction?.Dispose(); connection.Dispose(); }
}
