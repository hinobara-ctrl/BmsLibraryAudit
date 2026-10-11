using Microsoft.Data.Sqlite;

namespace BmsLibraryAudit.Core;

internal sealed class PhaseTwoDatabase : IDisposable
{
    internal const int ApplicationId = 0x424D5332;
    internal const string AbsoluteMode = "windows-ordinal-ignore-case";
    internal const string RelativeMode = "relative-ordinal-spelling-v1";
    internal const string ContractVersion = "1.0-draft";
    internal const string Baseline = "a300b54bf63d1e2077accb30e29c5bd40dacee2c";
    internal const string PhaseOneBaseline = "53c45ca322f63da67b1657b34be6826d838cfd92";
    internal SqliteConnection Connection { get; }
    private SqliteTransaction? transaction;
    private FileStream? binding;
    private string? boundIdentity;
    private string? boundResolvedPath;
    private bool writable;
    internal string Path { get; }
    private static readonly Lazy<string> ExpectedSchema = new(() =>
    {
        using var db = new PhaseTwoDatabase(":memory:", Connect(":memory:", SqliteOpenMode.ReadWriteCreate));
        db.Initialize(); return SchemaDigest(db.Connection);
    });

    private PhaseTwoDatabase(string path, SqliteConnection connection) { Path = path; Connection = connection; }
    internal static SqliteConnection Connect(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 1 }.ToString());
        try { connection.Open(); connection.CreateCollation("ORDINAL", string.CompareOrdinal); return connection; } catch { connection.Dispose(); throw; }
    }
    internal static void RequireScalar(SqliteConnection connection, string sql, long expected)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql;
        if (Convert.ToInt64(command.ExecuteScalar()) != expected) throw new InvalidOperationException("Database identity/schema mismatch: " + sql);
    }
    internal static PhaseTwoDatabase Open(string path, bool writable)
    {
        PhaseTwoSafety.RequirePlatform(); path = PathPolicy.Absolute(path);
        PhaseTwoSafety.RejectSidecars(path); PathPolicy.RejectReparseAncestors(path);
        if (!string.Equals(System.IO.Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Phase 2 database must have .db extension.");
        var exists = PhaseTwoSafety.Exists(path);
        FileStream? guard = null;
        try
        {
        if (exists)
        {
            var facts = PhaseTwoSafety.Inspect(path);
            if (writable && facts.LinkCount != 1) throw new InvalidOperationException("Writable database has hardlink aliases.");
            guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (PhaseTwoSafety.Inspect(guard.SafeFileHandle, path).Identity != facts.Identity)
                throw new IOException("Database identity changed before ownership probe.");
            using var probe = Connect(path, SqliteOpenMode.ReadOnly); Validate(probe);
        }
        else
        {
            if (!writable) throw new FileNotFoundException("Phase 2 database does not exist.", path);
            _ = PhaseTwoSafety.Resolve(path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            PathPolicy.RejectReparseAncestors(path);
            guard = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
        }
        var guarded = PhaseTwoSafety.Inspect(guard.SafeFileHandle, path);
        if (writable && guarded.LinkCount != 1) throw new IOException("Unsafe database link count.");
        var connection = Connect(path, writable ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly);
        var database = new PhaseTwoDatabase(path, connection) { binding = guard, boundIdentity = guarded.Identity, boundResolvedPath = guarded.ResolvedPath, writable = writable };
        try
        {
            if (PhaseTwoSafety.Inspect(path).Identity != guarded.Identity) throw new IOException("Database path changed before initialization.");
            database.Execute("PRAGMA foreign_keys=ON");
            if (!exists) database.Initialize(); else Validate(connection);
            return database;
        }
        catch { database.Dispose(); throw; }
        }
        catch { guard?.Dispose(); throw; }
    }
    private static void Validate(SqliteConnection connection)
    {
        RequireScalar(connection, "PRAGMA application_id", ApplicationId); RequireScalar(connection, "PRAGMA user_version", 1);
        using var command = connection.CreateCommand();
        foreach (var (key, value) in new[] { ("tool", "BmsLibraryAudit.Phase2"), ("contract_version", ContractVersion),
            ("absolute_comparison_mode", AbsoluteMode), ("relative_comparison_mode", RelativeMode), ("capabilities", "PREFLIGHT_ONLY") })
        {
            command.CommandText = "SELECT value FROM schema_metadata WHERE key=$key";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$key", key);
            if ((string?)command.ExecuteScalar() != value) throw new InvalidOperationException("Phase 2 ownership/contract/platform mismatch.");
        }
        command.CommandText = "PRAGMA quick_check"; command.Parameters.Clear();
        if ((string?)command.ExecuteScalar() != "ok") throw new InvalidOperationException("Phase 2 database integrity failure.");
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("Phase 2 foreign-key integrity failure.");
        if (SchemaDigest(connection) != ExpectedSchema.Value)
            throw new InvalidOperationException("Phase 2 required schema/index/seal constraints differ from this implementation.");
    }
    private static string SchemaDigest(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type,name,tbl_name,sql FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' ORDER BY type COLLATE BINARY,name COLLATE BINARY";
        using var reader = command.ExecuteReader(); var rows = new List<string?[]>();
        while (reader.Read()) rows.Add(Enumerable.Range(0, 4).Select(i => reader.IsDBNull(i) ? null : reader.GetString(i)).ToArray());
        return PhaseTwoCanonicalEncoding.Hash("BmsLibraryAudit.Phase2.Schema.v1", [("schema", ["type", "name", "table", "sql"], rows)]);
    }
    internal void Begin(bool readOnly = false) => transaction = Connection.BeginTransaction(deferred: readOnly);
    internal void Commit() { transaction!.Commit(); transaction.Dispose(); transaction = null; }
    internal void Rollback() { transaction?.Rollback(); transaction?.Dispose(); transaction = null; }
    internal SqliteCommand Command(string sql, params object?[] values)
    {
        var command = Connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$" + i, values[i] ?? DBNull.Value);
        return command;
    }
    internal void Execute(string sql, params object?[] values)
    {
        if (writable && binding is not null)
        {
            var facts = PhaseTwoSafety.Inspect(binding.SafeFileHandle, Path);
            if (facts.Identity != boundIdentity || facts.LinkCount != 1 || facts.ResolvedPath != boundResolvedPath)
                throw new IOException("Writable database binding or hardlink safety changed.");
        }
        using var command = Command(sql, values); command.ExecuteNonQuery();
    }
    internal object? Scalar(string sql, params object?[] values) { using var command = Command(sql, values); var value = command.ExecuteScalar(); return value is DBNull ? null : value; }
    internal long Number(string sql, params object?[] values) => Convert.ToInt64(Scalar(sql, values));
    internal IEnumerable<object?[]> Rows(string sql, params object?[] values)
    {
        using var command = Command(sql, values); using var reader = command.ExecuteReader();
        while (reader.Read()) yield return Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray();
    }
    internal long Insert(string table, string columns, params object?[] values)
    {
        Execute($"INSERT INTO {table}({columns}) VALUES({string.Join(',', Enumerable.Range(0, values.Length).Select(i => "$" + i))})", values);
        return Number("SELECT last_insert_rowid()");
    }
    internal void Diagnostic(long session, long? component, long? root, long? entry, string code, string path, string message, string severity = "ERROR", int? attempt = null)
        => Insert("diagnostics", "session_id,component_id,root_id,entry_id,gate,severity,code,original_path,normalized_path,attempt,message",
            session, component, root, entry, "PREFLIGHT", severity, code, path, PathPolicy.Normalize(path), attempt, message);
    internal void CheckIntegrity()
    {
        if ((string?)Scalar("PRAGMA quick_check") != "ok" || Rows("PRAGMA foreign_key_check").Any())
            throw new InvalidOperationException("EVIDENCE_INTEGRITY_ERROR");
    }
    private void Initialize()
    {
        Begin();
        Execute($"PRAGMA application_id={ApplicationId}; PRAGMA user_version=1");
        Execute(Schema);
        foreach (var (key, value) in new[] { ("tool", "BmsLibraryAudit.Phase2"), ("schema_version", "1"), ("contract_version", ContractVersion),
            ("absolute_comparison_mode", AbsoluteMode), ("relative_comparison_mode", RelativeMode), ("capabilities", "PREFLIGHT_ONLY") })
            Insert("schema_metadata", "key,value", key, value);
        foreach (var table in new[] { "source_snapshots", "source_library_roots", "source_charts", "source_diagnostics", "candidate_components",
            "observation_roots", "component_memberships", "source_exact_edges", "chart_revalidations", "direct_chart_memberships",
            "preflight_entries", "physical_files", "observation_memberships", "diagnostics" })
        {
            var field = table switch { "source_snapshots" => "preflight_id", "source_library_roots" or "source_charts" or "source_diagnostics" => "snapshot_id", "diagnostics" => "session_id", _ => "preflight_id" };
            foreach (var operation in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                var predicate = operation == "INSERT" ? $"NEW.{field}" : $"OLD.{field}";
                var extra = operation == "UPDATE" ? $" OR (SELECT state FROM sessions WHERE session_id=NEW.{field})<>'RUNNING'" : "";
                if (table == "source_snapshots")
                    extra += operation == "INSERT" ? " OR NEW.seal_flag=1" : " OR OLD.seal_flag=1";
                else if (table is "source_library_roots" or "source_charts" or "source_diagnostics")
                {
                    extra += $" OR (SELECT seal_flag FROM source_snapshots WHERE snapshot_id={predicate})=1";
                    if (operation == "UPDATE") extra += " OR (SELECT seal_flag FROM source_snapshots WHERE snapshot_id=NEW.snapshot_id)=1";
                }
                Execute($"CREATE TRIGGER seal_{table}_{operation} BEFORE {operation} ON {table} WHEN (SELECT state FROM sessions WHERE session_id={predicate})<>'RUNNING'{extra} BEGIN SELECT RAISE(ABORT,'Sealed evidence is immutable'); END");
            }
        }
        Execute("""
            CREATE TRIGGER content_not_implemented BEFORE INSERT ON sessions WHEN NEW.stage='CONTENT'
            BEGIN SELECT RAISE(ABORT,'Phase 2B is not implemented or authorized'); END;
            CREATE TRIGGER sealed_session_update BEFORE UPDATE ON sessions WHEN OLD.state<>'RUNNING'
            BEGIN SELECT RAISE(ABORT,'Terminal session is immutable'); END;
            CREATE TRIGGER sealed_session_delete BEFORE DELETE ON sessions
            BEGIN SELECT RAISE(ABORT,'Session deletion is not permitted'); END;
            CREATE TRIGGER immutable_schema_update BEFORE UPDATE ON schema_metadata
            BEGIN SELECT RAISE(ABORT,'Schema identity is immutable'); END;
            CREATE TRIGGER immutable_schema_delete BEFORE DELETE ON schema_metadata
            BEGIN SELECT RAISE(ABORT,'Schema identity is immutable'); END;
            """);
        Commit();
    }
    public void Dispose() { try { transaction?.Dispose(); Connection.Dispose(); } finally { binding?.Dispose(); } }

    private const string Schema = """
        CREATE TABLE schema_metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE sessions(session_id INTEGER PRIMARY KEY,stage TEXT NOT NULL CHECK(stage IN ('PREFLIGHT','CONTENT')),
            parent_preflight_id INTEGER REFERENCES sessions(session_id),accepted_manifest_sha256 TEXT,
            state TEXT NOT NULL CHECK(state IN ('RUNNING','COMPLETED','FAILED','CANCELLED','STALE')),
            started_utc TEXT NOT NULL,completed_utc TEXT,tool_version TEXT NOT NULL,runtime_build_sha TEXT NOT NULL,
            final_evidence_digest TEXT,source_component_count INTEGER NOT NULL CHECK(source_component_count>=0),
            CHECK((stage='PREFLIGHT' AND parent_preflight_id IS NULL AND accepted_manifest_sha256 IS NULL) OR
                  (stage='CONTENT' AND parent_preflight_id IS NOT NULL AND accepted_manifest_sha256 IS NOT NULL AND length(accepted_manifest_sha256)=64)),
            CHECK(state='RUNNING' OR completed_utc IS NOT NULL),CHECK(state<>'COMPLETED' OR (final_evidence_digest IS NOT NULL AND length(final_evidence_digest)=64)));
        CREATE TABLE source_snapshots(snapshot_id INTEGER PRIMARY KEY REFERENCES sessions(session_id),preflight_id INTEGER NOT NULL UNIQUE REFERENCES sessions(session_id),
            repository_baseline_sha TEXT NOT NULL,phase1_implementation_sha TEXT NOT NULL,
            source_db_original_path TEXT NOT NULL,source_db_normalized_path TEXT NOT NULL,source_db_native_identity TEXT,
            source_db_size INTEGER NOT NULL CHECK(source_db_size>=0),source_db_sha256 TEXT NOT NULL CHECK(length(source_db_sha256)=64),
            source_db_before_mtime_ticks INTEGER NOT NULL,source_db_after_mtime_ticks INTEGER NOT NULL,
            source_application_id INTEGER NOT NULL CHECK(source_application_id=1112363841),source_schema_version INTEGER NOT NULL CHECK(source_schema_version=1),
            source_comparison_mode TEXT NOT NULL,source_scan_id INTEGER NOT NULL,source_scan_started_utc TEXT NOT NULL,source_scan_completed_utc TEXT NOT NULL,
            admission_policy TEXT NOT NULL CHECK(admission_policy='EXACT_ONLY'),captured_evidence_digest TEXT,seal_flag INTEGER NOT NULL CHECK(seal_flag IN (0,1)),CHECK(snapshot_id=preflight_id));
        CREATE TABLE source_library_roots(snapshot_id INTEGER NOT NULL REFERENCES source_snapshots(snapshot_id),source_root_id INTEGER NOT NULL,
            original_path TEXT NOT NULL,normalized_path TEXT NOT NULL,active INTEGER NOT NULL CHECK(active IN (0,1)),configured INTEGER NOT NULL CHECK(configured IN (0,1)),
            PRIMARY KEY(snapshot_id,source_root_id),UNIQUE(snapshot_id,normalized_path));
        CREATE TABLE source_charts(snapshot_id INTEGER NOT NULL,normalized_path TEXT NOT NULL,source_root_id INTEGER NOT NULL,original_path TEXT NOT NULL,
            parent_folder TEXT NOT NULL,filename TEXT NOT NULL,extension TEXT NOT NULL,size INTEGER NOT NULL,mtime_ticks INTEGER NOT NULL,
            md5 TEXT,sha256 TEXT,first_seen_scan INTEGER NOT NULL,last_seen_scan INTEGER NOT NULL,present INTEGER NOT NULL CHECK(present IN (0,1)),status TEXT NOT NULL,
            PRIMARY KEY(snapshot_id,normalized_path),FOREIGN KEY(snapshot_id,source_root_id) REFERENCES source_library_roots(snapshot_id,source_root_id),
            CHECK(status<>'OK' OR (sha256 IS NOT NULL AND length(sha256)=64 AND md5 IS NOT NULL AND length(md5)=32 AND size>=0)));
        CREATE INDEX source_chart_status ON source_charts(snapshot_id,present,status,sha256);
        CREATE TABLE source_diagnostics(snapshot_id INTEGER NOT NULL REFERENCES source_snapshots(snapshot_id),ordinal INTEGER NOT NULL,
            severity TEXT NOT NULL,code TEXT NOT NULL,path TEXT NOT NULL,message TEXT NOT NULL,PRIMARY KEY(snapshot_id,ordinal));
        CREATE TABLE candidate_components(component_id INTEGER PRIMARY KEY,preflight_id INTEGER NOT NULL REFERENCES sessions(session_id),
            component_key TEXT NOT NULL,source_component_label TEXT NOT NULL,distinct_chart_count INTEGER NOT NULL CHECK(distinct_chart_count>0),
            distinct_chart_set_digest TEXT NOT NULL,member_count INTEGER NOT NULL CHECK(member_count>=2),
            outcome TEXT NOT NULL CHECK(outcome IN ('ADMITTED','STALE_PHASE_1_CANDIDATE','OBSERVED_SUBTREE_INCOMPLETE')),reason TEXT NOT NULL,
            UNIQUE(preflight_id,component_id),UNIQUE(preflight_id,component_key),UNIQUE(preflight_id,source_component_label));
        CREATE TABLE observation_roots(observation_root_id INTEGER PRIMARY KEY,preflight_id INTEGER NOT NULL,source_root_id INTEGER NOT NULL,
            original_path TEXT NOT NULL,root_key TEXT NOT NULL,chart_state TEXT NOT NULL CHECK(chart_state IN ('PENDING','CURRENT','STALE','ERROR')),
            inventory_state TEXT NOT NULL CHECK(inventory_state IN ('PENDING','COMPLETE','INCOMPLETE')),
            regular_file_count INTEGER NOT NULL DEFAULT 0 CHECK(regular_file_count>=0),directory_count INTEGER NOT NULL DEFAULT 0 CHECK(directory_count>=0),
            logical_bytes INTEGER NOT NULL DEFAULT 0 CHECK(logical_bytes>=0),counts_are_lower_bounds INTEGER NOT NULL CHECK(counts_are_lower_bounds IN (0,1)),
            UNIQUE(preflight_id,observation_root_id),UNIQUE(preflight_id,root_key),FOREIGN KEY(preflight_id,source_root_id) REFERENCES source_library_roots(snapshot_id,source_root_id));
        CREATE TABLE component_memberships(preflight_id INTEGER NOT NULL,component_id INTEGER NOT NULL,observation_root_id INTEGER NOT NULL,
            PRIMARY KEY(component_id,observation_root_id),UNIQUE(preflight_id,observation_root_id),UNIQUE(preflight_id,component_id,observation_root_id),
            FOREIGN KEY(preflight_id,component_id) REFERENCES candidate_components(preflight_id,component_id),
            FOREIGN KEY(preflight_id,observation_root_id) REFERENCES observation_roots(preflight_id,observation_root_id));
        CREATE TABLE source_exact_edges(preflight_id INTEGER NOT NULL,component_id INTEGER NOT NULL,root_a_id INTEGER NOT NULL,root_b_id INTEGER NOT NULL,
            charts_a INTEGER NOT NULL,charts_b INTEGER NOT NULL,common INTEGER NOT NULL,physical_files_a INTEGER NOT NULL CHECK(physical_files_a>0),physical_files_b INTEGER NOT NULL CHECK(physical_files_b>0),
            PRIMARY KEY(component_id,root_a_id,root_b_id),CHECK(root_a_id<>root_b_id),CHECK(common=charts_a AND common=charts_b AND common>0),
            FOREIGN KEY(preflight_id,component_id,root_a_id) REFERENCES component_memberships(preflight_id,component_id,observation_root_id),
            FOREIGN KEY(preflight_id,component_id,root_b_id) REFERENCES component_memberships(preflight_id,component_id,observation_root_id));
        CREATE TABLE chart_revalidations(preflight_id INTEGER NOT NULL REFERENCES sessions(session_id),chart_key TEXT NOT NULL,captured_chart_key TEXT,
            observed_filename TEXT NOT NULL,before_size INTEGER,before_mtime_ticks INTEGER,after_size INTEGER,after_mtime_ticks INTEGER,
            fresh_sha256 TEXT,bytes_read INTEGER NOT NULL CHECK(bytes_read>=0),attempts INTEGER NOT NULL CHECK(attempts BETWEEN 0 AND 2),
            read_status TEXT NOT NULL CHECK(read_status IN ('SUCCESS','MISSING','UNREADABLE','UNSTABLE','REPARSE')),
            match_status TEXT NOT NULL CHECK(match_status IN ('MATCH','CHANGED','MISSING','UNEXPECTED','UNKNOWN')),
            PRIMARY KEY(preflight_id,chart_key),FOREIGN KEY(preflight_id,captured_chart_key) REFERENCES source_charts(snapshot_id,normalized_path),
            CHECK((read_status='SUCCESS' AND fresh_sha256 IS NOT NULL AND length(fresh_sha256)=64) OR (read_status<>'SUCCESS' AND fresh_sha256 IS NULL)));
        CREATE TABLE direct_chart_memberships(preflight_id INTEGER NOT NULL,observation_root_id INTEGER NOT NULL,chart_key TEXT NOT NULL,
            captured_member INTEGER NOT NULL CHECK(captured_member IN (0,1)),observed_member INTEGER NOT NULL CHECK(observed_member IN (0,1)),
            PRIMARY KEY(observation_root_id,chart_key),FOREIGN KEY(preflight_id,observation_root_id) REFERENCES observation_roots(preflight_id,observation_root_id),
            FOREIGN KEY(preflight_id,chart_key) REFERENCES chart_revalidations(preflight_id,chart_key));
        CREATE TABLE preflight_entries(entry_id INTEGER PRIMARY KEY,preflight_id INTEGER NOT NULL REFERENCES sessions(session_id),entry_key TEXT NOT NULL,original_path TEXT NOT NULL,
            kind TEXT NOT NULL CHECK(kind IN ('DIRECTORY','REGULAR_FILE','REPARSE','OTHER','UNKNOWN')),size INTEGER CHECK(size>=0),mtime_ticks INTEGER,attributes INTEGER,
            extension TEXT,is_chart INTEGER CHECK(is_chart IN (0,1)),inventory_status TEXT NOT NULL,readability TEXT NOT NULL,native_identity TEXT,
            UNIQUE(preflight_id,entry_id),UNIQUE(preflight_id,entry_key),CHECK(kind<>'REGULAR_FILE' OR size IS NOT NULL));
        CREATE TABLE physical_files(physical_file_id INTEGER PRIMARY KEY,preflight_id INTEGER NOT NULL,entry_id INTEGER NOT NULL UNIQUE,native_identity TEXT,
            UNIQUE(preflight_id,physical_file_id),FOREIGN KEY(preflight_id,entry_id) REFERENCES preflight_entries(preflight_id,entry_id));
        CREATE TRIGGER physical_regular BEFORE INSERT ON physical_files WHEN (SELECT kind FROM preflight_entries WHERE entry_id=NEW.entry_id)<>'REGULAR_FILE'
            BEGIN SELECT RAISE(ABORT,'Physical file must be regular'); END;
        CREATE TRIGGER physical_regular_update BEFORE UPDATE ON physical_files WHEN (SELECT kind FROM preflight_entries WHERE entry_id=NEW.entry_id)<>'REGULAR_FILE'
            BEGIN SELECT RAISE(ABORT,'Physical file must be regular'); END;
        CREATE TABLE observation_memberships(preflight_id INTEGER NOT NULL,observation_root_id INTEGER NOT NULL,entry_id INTEGER NOT NULL,
            relative_path TEXT NOT NULL,relative_path_key TEXT NOT NULL,PRIMARY KEY(observation_root_id,entry_id),UNIQUE(observation_root_id,relative_path_key),
            FOREIGN KEY(preflight_id,observation_root_id) REFERENCES observation_roots(preflight_id,observation_root_id),
            FOREIGN KEY(preflight_id,entry_id) REFERENCES preflight_entries(preflight_id,entry_id));
        CREATE INDEX membership_entry ON observation_memberships(preflight_id,entry_id);
        CREATE TABLE diagnostics(diagnostic_id INTEGER PRIMARY KEY,session_id INTEGER NOT NULL REFERENCES sessions(session_id),component_id INTEGER,root_id INTEGER,entry_id INTEGER,
            gate TEXT NOT NULL,severity TEXT NOT NULL,code TEXT NOT NULL,original_path TEXT NOT NULL,normalized_path TEXT NOT NULL,attempt INTEGER,message TEXT NOT NULL,
            FOREIGN KEY(session_id,component_id) REFERENCES candidate_components(preflight_id,component_id),
            FOREIGN KEY(session_id,root_id) REFERENCES observation_roots(preflight_id,observation_root_id),FOREIGN KEY(session_id,entry_id) REFERENCES preflight_entries(preflight_id,entry_id));
        CREATE INDEX diagnostic_context ON diagnostics(session_id,component_id,root_id,normalized_path);
        CREATE TABLE report_sets(report_set_id INTEGER PRIMARY KEY,session_id INTEGER NOT NULL REFERENCES sessions(session_id),evidence_stage TEXT NOT NULL,
            evidence_state TEXT NOT NULL,evidence_identity TEXT,original_destination TEXT NOT NULL,normalized_destination TEXT NOT NULL,
            status TEXT NOT NULL CHECK(status IN ('RUNNING','COMPLETED','FAILED')),completed_utc TEXT);
        CREATE TABLE report_files(report_set_id INTEGER NOT NULL REFERENCES report_sets(report_set_id),filename TEXT NOT NULL,byte_length INTEGER NOT NULL CHECK(byte_length>=0),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64),status TEXT NOT NULL CHECK(status='COMPLETED'),PRIMARY KEY(report_set_id,filename));
        """;
}
