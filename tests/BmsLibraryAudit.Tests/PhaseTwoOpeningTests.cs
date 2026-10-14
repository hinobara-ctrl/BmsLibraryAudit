using BmsLibraryAudit.Core;
using Xunit;
using Fixture = BmsLibraryAudit.Tests.PhaseTwoPreflightTests.Fixture;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseTwoOpeningTests
{
    [WindowsPhaseTwoTheory]
    [InlineData("wal")] [InlineData("write-mixed")] [InlineData("read-mixed")]
    [InlineData("zero")] [InlineData("unknown")] [InlineData("magic")]
    [InlineData("empty")] [InlineData("truncated")]
    public void RejectedExistingHeadersNeverReachProviderAndRemainUnchanged(string kind)
    {
        using var f = new Fixture(); using (var db = PhaseTwoDatabase.Open(f.Db, true)) { }
        var bytes = File.ReadAllBytes(f.Db);
        switch (kind)
        {
            case "wal": bytes[18] = bytes[19] = 2; break;
            case "write-mixed": bytes[18] = 2; break;
            case "read-mixed": bytes[19] = 2; break;
            case "zero": bytes[18] = bytes[19] = 0; break;
            case "unknown": bytes[18] = bytes[19] = 255; break;
            case "magic": bytes[15] = 1; break;
            case "empty": bytes = []; break;
            case "truncated": bytes = bytes[..99]; break;
        }
        File.WriteAllBytes(f.Db, bytes); var before = PhaseTwoSafety.Inspect(f.Db);
        foreach (var writable in new[] { false, true })
        {
            var calls = 0;
            Assert.StartsWith("SOURCE_HEADER_", Assert.Throws<InvalidOperationException>(() => PhaseTwoDatabase.Open(f.Db, writable, () => calls++)).Message);
            Assert.Equal(0, calls); Assert.Equal(bytes, File.ReadAllBytes(f.Db)); Assert.Equal(before, PhaseTwoSafety.Inspect(f.Db));
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" }) Assert.False(File.Exists(f.Db + suffix));
        }
        Assert.Equal(new[] { f.Db }, Directory.GetFiles(f.Base));
    }
    [WindowsPhaseTwoFact]
    public void NewAndRollbackDatabasesRemainSupportedAndForeignProfilesRemainUnchanged()
    {
        using var f = new Fixture(); var calls = 0;
        using (var db = PhaseTwoDatabase.Open(f.Db, true, () => calls++)) { } Assert.Equal(1, calls);
        var before = File.ReadAllBytes(f.Db); var facts = PhaseTwoSafety.Inspect(f.Db); calls = 0;
        using (var db = PhaseTwoDatabase.Open(f.Db, false, () => calls++)) { } Assert.Equal(2, calls);
        Assert.Equal(before, File.ReadAllBytes(f.Db)); Assert.Equal(facts, PhaseTwoSafety.Inspect(f.Db));
        f.Sql(f.Db, "PRAGMA application_id=7"); Refused();
        f.Sql(f.Db, "PRAGMA application_id=1112363826; DROP TRIGGER immutable_schema_update; UPDATE schema_metadata SET value='PREFLIGHT_ONLY' WHERE key='capabilities'"); Refused();
        void Refused()
        {
            var bytes = File.ReadAllBytes(f.Db); var stamp = PhaseTwoSafety.Inspect(f.Db); var provider = 0;
            Assert.Throws<InvalidOperationException>(() => PhaseTwoDatabase.Open(f.Db, true, () => provider++));
            Assert.Equal(1, provider); Assert.Equal(bytes, File.ReadAllBytes(f.Db)); Assert.Equal(stamp, PhaseTwoSafety.Inspect(f.Db));
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" }) Assert.False(File.Exists(f.Db + suffix));
        }
    }
}
