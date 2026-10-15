using System.Text;
using Microsoft.Data.Sqlite;

namespace BmsLibraryAudit.Core;

internal sealed class ObservationPairDatabase : IDisposable
{
    internal const string Profile = "explicit-subset-pair-1", Contract = "observation-pair-1";
    internal const int ApplicationId = 0x424D5350;
    internal const string Policy = "strict-direct-chart-subset-1";
    private const string Schema = """
        CREATE TABLE metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL) STRICT;
        CREATE TABLE records(
          name TEXT PRIMARY KEY CHECK(name IN ('SOURCE','SELECTION','PREFLIGHT','CONTENT')),
          state TEXT NOT NULL CHECK(state IN ('COMPLETE','BLOCKED','STALE','INCOMPLETE','CANCELLED','FAILED')),
          parent_digest TEXT, digest TEXT, payload TEXT NOT NULL CHECK(json_valid(payload)),
          CHECK((state='COMPLETE' AND digest IS NOT NULL AND length(digest)=64 AND digest NOT GLOB '*[^0-9a-f]*') OR (state<>'COMPLETE' AND digest IS NULL)),
          CHECK((name='SOURCE' AND parent_digest IS NULL) OR (name<>'SOURCE' AND parent_digest IS NOT NULL AND length(parent_digest)=64 AND parent_digest NOT GLOB '*[^0-9a-f]*')),
          CHECK(name NOT IN ('SOURCE','SELECTION') OR state='COMPLETE')
        ) STRICT;
        CREATE TRIGGER records_no_update BEFORE UPDATE ON records BEGIN SELECT RAISE(ABORT,'immutable record'); END;
        CREATE TRIGGER records_no_delete BEFORE DELETE ON records BEGIN SELECT RAISE(ABORT,'immutable record'); END;
        CREATE TRIGGER metadata_no_update BEFORE UPDATE ON metadata BEGIN SELECT RAISE(ABORT,'immutable metadata'); END;
        CREATE TRIGGER metadata_no_delete BEFORE DELETE ON metadata BEGIN SELECT RAISE(ABORT,'immutable metadata'); END;
        CREATE TRIGGER metadata_no_insert BEFORE INSERT ON metadata WHEN (SELECT count(*) FROM metadata)>=2 BEGIN SELECT RAISE(ABORT,'closed metadata'); END;
        CREATE TRIGGER records_order BEFORE INSERT ON records BEGIN
          SELECT CASE WHEN EXISTS(SELECT 1 FROM records WHERE name=NEW.name) THEN RAISE(ABORT,'immutable existing record') END;
          SELECT CASE WHEN NEW.name='SOURCE' AND (SELECT count(*) FROM records)<>0 THEN RAISE(ABORT,'source order') END;
          SELECT CASE WHEN NEW.name IN ('SELECTION','PREFLIGHT','CONTENT') AND NOT EXISTS(SELECT 1 FROM records WHERE name=CASE NEW.name WHEN 'CONTENT' THEN 'PREFLIGHT' ELSE 'SOURCE' END AND state='COMPLETE' AND digest=NEW.parent_digest) THEN RAISE(ABORT,'parent mismatch') END;
          SELECT CASE WHEN NEW.name='PREFLIGHT' AND NOT EXISTS(SELECT 1 FROM records WHERE name='SELECTION') THEN RAISE(ABORT,'selection missing') END;
        END;
        """;
    private readonly SqliteConnection connection;
    private readonly FileStream binding;
    private readonly NativePathFacts bound;
    private readonly bool writable;
    internal string Path { get; }
    private ObservationPairDatabase(string path, SqliteConnection connection, FileStream binding, bool writable)
    { Path = path; this.connection = connection; this.binding = binding; this.writable = writable; bound = PhaseTwoSafety.Inspect(binding.SafeFileHandle, path); }
    private static readonly Lazy<string> ExpectedSchema = new(() => {
        using var c = PhaseTwoDatabase.Connect(":memory:", SqliteOpenMode.ReadWriteCreate);
        using var command = c.CreateCommand(); command.CommandText = Schema; command.ExecuteNonQuery(); return SchemaHash(c);
    });
    internal static ObservationPairDatabase Open(string path, bool writable = false, bool create = false)
    {
        PhaseTwoSafety.RequirePlatform(); path = PathPolicy.Absolute(path);
        if (!string.Equals(System.IO.Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Pair evidence requires .db.");
        PhaseTwoSafety.RejectSidecars(path); PathPolicy.RejectReparseAncestors(path);
        if (create && PhaseTwoSafety.Exists(path)) throw new InvalidOperationException("Pair evidence must be new; no overwrite or resume.");
        if (!create && !PhaseTwoSafety.Exists(path)) throw new FileNotFoundException("Pair evidence missing.", path);
        if (create) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!); PathPolicy.RejectReparseAncestors(path); }
        var guard = new FileStream(path, create ? FileMode.CreateNew : FileMode.Open,
            create ? FileAccess.ReadWrite : FileAccess.Read, writable ? FileShare.ReadWrite : FileShare.Read);
        SqliteConnection? c = null;
        try {
            var native = PhaseTwoSafety.Inspect(guard.SafeFileHandle, path);
            if (native.Identity != PhaseTwoSafety.Inspect(path).Identity || native.LinkCount != 1) throw new IOException("Unsafe evidence binding/alias.");
            if (!create) { PhaseOneSourceCapture.ValidateHeader(guard, default); using var probe = PhaseTwoDatabase.Connect(path, SqliteOpenMode.ReadOnly); Validate(probe); }
            c = PhaseTwoDatabase.Connect(path, writable ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly);
            if (PhaseTwoSafety.Inspect(path).Identity != native.Identity) throw new IOException("Evidence replaced before provider open.");
            if (create) {
                using var command = c.CreateCommand(); command.CommandText = $"PRAGMA application_id={ApplicationId}; PRAGMA user_version=1; PRAGMA journal_mode=DELETE; " + Schema +
                    $"INSERT INTO metadata VALUES('profile','{Profile}'),('contract','{Contract}');"; command.ExecuteNonQuery();
            }
            Validate(c); return new(path, c, guard, writable);
        } catch { c?.Dispose(); guard.Dispose(); throw; }
    }
    private static void Validate(SqliteConnection c)
    {
        PhaseTwoDatabase.RequireScalar(c, "PRAGMA application_id", ApplicationId); PhaseTwoDatabase.RequireScalar(c, "PRAGMA user_version", 1);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        using (var reader = cmd.ExecuteReader()) if (!reader.Read() || reader.GetString(0) != "ok" || reader.Read()) throw new InvalidOperationException("Pair DB integrity failure.");
        cmd.CommandText = "PRAGMA foreign_key_check"; using (var reader = cmd.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("Pair foreign keys invalid.");
        if (SchemaHash(c) != ExpectedSchema.Value) throw new InvalidOperationException("Pair exact compiled schema mismatch.");
        cmd.CommandText = "SELECT key,value FROM metadata ORDER BY key";
        var metadata = new List<string>(); using (var reader = cmd.ExecuteReader()) while (reader.Read()) metadata.Add(reader.GetString(0) + "=" + reader.GetString(1));
        if (!metadata.SequenceEqual(new[] { "contract=" + Contract, "profile=" + Profile })) throw new InvalidOperationException("Pair profile metadata mismatch.");
    }
    private static string SchemaHash(SqliteConnection c)
    {
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT type,name,tbl_name,sql FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' ORDER BY type COLLATE BINARY,name COLLATE BINARY";
        var rows = new List<string?[]>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) rows.Add(Enumerable.Range(0, 4).Select(i => reader.IsDBNull(i) ? null : reader.GetString(i)).ToArray());
        return PhaseTwoCanonicalEncoding.Hash("BmsLibraryAudit.ObservationPair.Schema.v1", [("schema", ["type", "name", "table", "sql"], rows)]);
    }
    internal sealed record Record(string Name, string State, string? Parent, string? Digest, string Payload);
    internal Record? Read(string name)
    {
        using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT state,parent_digest,digest,payload FROM records WHERE name=$name"; cmd.Parameters.AddWithValue("$name", name);
        using var reader = cmd.ExecuteReader(); if (!reader.Read()) return null;
        if (reader.GetString(3).Length > new ObservationPairLimits().PayloadBytes || Encoding.UTF8.GetByteCount(reader.GetString(3)) > new ObservationPairLimits().PayloadBytes)
            throw new InvalidOperationException("Evidence payload exceeds hard limit.");
        return new(name, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3));
    }
    internal void BeginRead() { using var cmd = connection.CreateCommand(); cmd.CommandText = "BEGIN"; cmd.ExecuteNonQuery(); }
    internal void Put(string name, string state, string? parent, string? digest, string payload, ObservationPairLimits limits)
    {
        if (!writable) throw new InvalidOperationException("Read-only pair evidence.");
        if (Encoding.UTF8.GetByteCount(payload) > limits.PayloadBytes) throw new PairStopException("INCOMPLETE", "PAYLOAD_BUDGET_EXCEEDED");
        RequireBinding();
        if (new FileInfo(Path).Length + Encoding.UTF8.GetByteCount(payload) + 65536 > limits.DatabaseBytes) throw new PairStopException("INCOMPLETE", "DATABASE_BUDGET_EXCEEDED");
        using var cmd = connection.CreateCommand(); cmd.CommandText = "INSERT INTO records VALUES($n,$s,$p,$d,$v)";
        cmd.Parameters.AddWithValue("$n", name); cmd.Parameters.AddWithValue("$s", state); cmd.Parameters.AddWithValue("$p", (object?)parent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", (object?)digest ?? DBNull.Value); cmd.Parameters.AddWithValue("$v", payload); cmd.ExecuteNonQuery(); RequireBinding();
    }
    internal void RequireBinding()
    {
        var facts = PhaseTwoSafety.Inspect(Path); var held = PhaseTwoSafety.Inspect(binding.SafeFileHandle, Path);
        if (facts.Identity != bound.Identity || held.Identity != bound.Identity || facts.ResolvedPath != bound.ResolvedPath || facts.LinkCount != 1)
            throw new IOException("Pair evidence native binding changed.");
    }
    internal static string Seal(string stage, string payload) => PhaseTwoCanonicalEncoding.Hash(
        "BmsLibraryAudit.ObservationPair." + (stage == "SOURCE" ? "Source" : stage == "PREFLIGHT" ? "Preflight" : "Content") + ".v1",
        [(stage, ["profile", "contract", "payload"], new[] { new string?[] { Profile, Contract, payload } })]);
    internal static string Unit(string source, string a, string b) => PhaseTwoCanonicalEncoding.Hash("BmsLibraryAudit.ObservationPair.Selection.v1",
        [("selection", ["profile", "policy", "source", "root_a", "root_b"], new[] { new string?[] { Profile, Policy, source, a, b } })]);
    public void Dispose() { connection.Dispose(); binding.Dispose(); PhaseTwoSafety.RejectSidecars(Path); }
}
