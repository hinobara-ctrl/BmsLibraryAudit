using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BmsLibraryAudit.Tests;

public sealed class PhaseOneTests
{
    [Fact]
    public void IdenticalBytesExposeEveryLocationButDifferentBytesDoNotDuplicate()
    {
        using var library = new SyntheticLibrary();
        var a = library.Chart("A", "same.BmS", "same bytes");
        var b = library.Chart("B", "other.bmson", "same bytes");
        library.Chart("C", "same.bms", "different bytes");
        library.Chart("A", "ignored.wav", "same bytes");
        var result = library.Scan();
        var snapshot = library.Snapshot();
        var analysis = RelationshipAnalyzer.Analyze(snapshot.Charts);
        Assert.Equal(3, result.Statistics.Discovered);
        var duplicate = Assert.Single(analysis.DuplicateGroups);
        Assert.Equal(new[] { a, b }, duplicate.Select(chart => chart.AbsolutePath));
        Assert.Equal(2, snapshot.Charts.Select(chart => chart.Sha256).Distinct().Count());
    }

    [Theory]
    [InlineData(2, 2, 2, "EXACT_CHART_SET")]
    [InlineData(1, 2, 1, "A_SUBSET_OF_B")]
    [InlineData(2, 1, 1, "B_SUBSET_OF_A")]
    [InlineData(5, 5, 4, "HIGH_OVERLAP")]
    [InlineData(6, 5, 4, "MEDIUM_OVERLAP")]
    [InlineData(2, 2, 1, "MEDIUM_OVERLAP")]
    [InlineData(3, 4, 1, "SHARED_CHARTS")]
    public void ClassificationUsesExactInclusiveThresholds(int a, int b, int common, string expected)
        => Assert.Equal(expected, RelationshipAnalyzer.Classify(a, b, common));

    [Theory]
    [InlineData(2, 2, 2, "EXACT_CHART_SET")]
    [InlineData(1, 2, 1, "A_SUBSET_OF_B")]
    [InlineData(2, 1, 1, "B_SUBSET_OF_A")]
    [InlineData(5, 5, 4, "HIGH_OVERLAP")]
    [InlineData(2, 2, 1, "MEDIUM_OVERLAP")]
    [InlineData(3, 4, 1, "SHARED_CHARTS")]
    public void RealFolderSetsProduceAllRelationshipLabels(int a, int b, int common, string expected)
    {
        using var library = new SyntheticLibrary();
        for (var i = 0; i < common; i++)
        {
            library.Chart("A", $"common{i}.bms", $"common{i}");
            library.Chart("B", $"common{i}.bms", $"common{i}");
        }
        for (var i = common; i < a; i++) library.Chart("A", $"extra{i}.bms", $"A{i}");
        for (var i = common; i < b; i++) library.Chart("B", $"extra{i}.bms", $"B{i}");
        library.Scan();
        var candidate = Assert.Single(RelationshipAnalyzer.Analyze(library.Snapshot().Charts).Candidates);
        Assert.Equal(expected, candidate.Relationship);
        Assert.Equal(common, candidate.Common);
        Assert.Equal((double)common / a, candidate.CoverageA);
        Assert.Equal((double)common / b, candidate.CoverageB);
        Assert.Equal((double)common / (a + b - common), candidate.Jaccard);
    }

    [Fact]
    public void TransitiveRelatedClusterDoesNotInventEndpointRelationship()
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", "x.bms", "x");
        library.Chart("B", "x.bms", "x");
        library.Chart("B", "y.bms", "y");
        library.Chart("C", "y.bms", "y");
        library.Chart("Isolated", "z.bms", "z");
        library.Scan();
        var analysis = RelationshipAnalyzer.Analyze(library.Snapshot().Charts);
        Assert.Equal(2, analysis.Candidates.Count);
        var cluster = Assert.Single(analysis.Clusters);
        Assert.Equal("RELATED", cluster.Kind);
        Assert.Equal(new[] { "A", "B", "C" }, cluster.Members.Select(Path.GetFileName));
    }

    [Fact]
    public void CopiesWithinOneFolderDoNotInflateChartSetAndExactClustersAreAvailable()
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", "one.bms", "x");
        library.Chart("A", "copy.bme", "x");
        library.Chart("B", "one.bml", "x");
        library.Chart("C", "one.pms", "x");
        library.Scan();
        var analysis = RelationshipAnalyzer.Analyze(library.Snapshot().Charts);
        var a = analysis.Folders.Single(folder => Path.GetFileName(folder.Path) == "A");
        Assert.Equal(2, a.PhysicalChartFiles);
        Assert.Single(a.Hashes);
        Assert.Equal(3, analysis.Candidates.Count);
        Assert.All(analysis.Candidates, candidate =>
        { Assert.Equal(1, candidate.Common); Assert.Equal(1, candidate.Jaccard); Assert.Equal("EXACT_CHART_SET", candidate.Relationship); });
        Assert.Equal(2, analysis.Clusters.Count);
        Assert.All(analysis.Clusters, cluster => Assert.Equal(3, cluster.Members.Count));
    }

    [Fact]
    public void IncrementalScanReusesInvalidatesRemovesAndRehashesWithStableProvenance()
    {
        using var library = new SyntheticLibrary();
        var path = library.Chart("A", "one.bms", "original");
        var first = library.Scan();
        Assert.Equal(1, first.Statistics.Hashed);
        Assert.Equal(1, first.Statistics.New);
        var unchanged = library.Scan();
        Assert.Equal(1, unchanged.Statistics.Reused);
        Assert.Equal(0, unchanged.Statistics.Hashed);
        File.WriteAllText(path, "modified, with a different length");
        var changed = library.Scan();
        Assert.Equal(1, changed.Statistics.Changed);
        Assert.Equal(1, changed.Statistics.Hashed);
        var current = Assert.Single(library.Snapshot().Charts);
        Assert.Equal(first.ScanId, current.FirstSeenScan);
        Assert.Equal(changed.ScanId, current.LastSeenScan);
        var forced = library.Scan(rehash: true);
        Assert.Equal(1, forced.Statistics.Hashed);
        Assert.Equal(0, forced.Statistics.Reused);
        File.Delete(path);
        Assert.Equal(1, library.Scan().Statistics.Missing);
        Assert.Empty(library.Snapshot().Charts);
        Assert.Equal(0, library.Scan().Statistics.Missing);
        library.Chart("A", "one.bms", "reappeared");
        Assert.Equal(1, library.Scan().Statistics.Hashed);
        Assert.Equal(first.ScanId, Assert.Single(library.Snapshot().Charts).FirstSeenScan);
    }

    [Fact]
    public void SameMetadataIsOnlyACacheHintAndRehashAllFindsChangedBytes()
    {
        using var library = new SyntheticLibrary();
        var path = library.Chart("A", "one.bms", "AAAA");
        library.Scan();
        var old = Assert.Single(library.Snapshot().Charts);
        var mtime = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "BBBB", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(path, mtime);
        Assert.Equal(1, library.Scan().Statistics.Reused);
        Assert.Equal(old.Sha256, Assert.Single(library.Snapshot().Charts).Sha256);
        library.Scan(rehash: true);
        Assert.NotEqual(old.Sha256, Assert.Single(library.Snapshot().Charts).Sha256);
    }

    [Theory]
    [InlineData("a.BMS")]
    [InlineData("a.BmE")]
    [InlineData("a.BML")]
    [InlineData("a.pMs")]
    [InlineData("a.BmSoN")]
    public void ExtensionRecognitionIsCaseInsensitive(string filename)
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", filename, "opaque bytes");
        Assert.Equal(1, library.Scan().Statistics.Hashed);
    }

    [Fact]
    public void UnicodeIsPreservedAndReportsAreByteDeterministicWithEscapedCsv()
    {
        using var library = new SyntheticLibrary();
        library.Chart("日本語,folder", "z.bms", "x");
        library.Chart("ñ", "a.bms", "x");
        library.Chart("ñ", "b.bms", "y");
        library.Scan();
        var one = Path.Combine(library.Base, "reports1");
        var two = Path.Combine(library.Base, "reports2");
        ReportWriter.Write(library.Db, one);
        ReportWriter.Write(library.Db, two);
        var expected = new[] { "clusters.csv", "duplicate_charts.csv", "folder_candidates.csv", "summary.json" };
        Assert.Equal(expected, Directory.GetFiles(one).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var filename in expected) Assert.Equal(File.ReadAllBytes(Path.Combine(one, filename)), File.ReadAllBytes(Path.Combine(two, filename)));
        Assert.Contains("\"" + Path.Combine(library.Root, "日本語,folder") + "\"", File.ReadAllText(Path.Combine(one, "duplicate_charts.csv")));
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(one, "summary.json")));
        Assert.Equal(3, summary.RootElement.GetProperty("current_chart_files").GetInt32());
        Assert.Equal(1, summary.RootElement.GetProperty("candidate_folder_pairs").GetInt32());
        Assert.Throws<InvalidOperationException>(() => ReportWriter.Write(library.Db, one));
    }

    [Fact]
    public void HashingMatchesBclForLargeBinaryChart()
    {
        using var library = new SyntheticLibrary();
        var bytes = new byte[1024 * 1024 + 137];
        new Random(1234).NextBytes(bytes);
        var path = library.Chart("A", "large.bms", "");
        File.WriteAllBytes(path, bytes);
        var digest = ChartHasher.Hash(path);
        Assert.Equal(Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant(), digest.Md5);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), digest.Sha256);
        Assert.Equal(bytes.Length, digest.Stamp.Size);
    }

    [Fact]
    public void ReadFailureIsRecordedAndPreviousHashDoesNotParticipate()
    {
        using var library = new SyntheticLibrary();
        var path = library.Chart("A", "locked.bms", "x");
        library.Scan();
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = library.Scan(rehash: true);
            Assert.Equal(1, result.Statistics.Errored);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CHART_READ" && diagnostic.Path == path);
            var snapshot = library.Snapshot();
            var failed = Assert.Single(snapshot.Charts);
            Assert.Equal("ERROR", failed.Status);
            Assert.Null(failed.Sha256);
            Assert.Empty(RelationshipAnalyzer.Analyze(snapshot.Charts, snapshot.Diagnostics).Folders);
            ReportWriter.Write(library.Db, Path.Combine(library.Base, "error-reports"));
        }
        Assert.Equal(1, library.Scan().Statistics.Hashed);
    }

    [Fact]
    public void ReparseDirectoryIsNotTraversedAndPriorEvidenceIsUnverified()
    {
        using var library = new SyntheticLibrary();
        library.Chart("linked", "one.bms", "x");
        library.Scan();
        var source = Path.Combine(library.Root, "linked");
        var external = Path.Combine(library.Base, "external");
        Directory.Move(source, external);
        if (OperatingSystem.IsWindows())
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{source}\" \"{external}\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            })!;
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        }
        else Directory.CreateSymbolicLink(source, external);
        try
        {
            var result = library.Scan();
            Assert.Equal(0, result.Statistics.Discovered);
            Assert.Equal(0, result.Statistics.Missing);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "REPARSE_SKIPPED");
            Assert.Empty(library.Snapshot().Charts);
            Assert.Equal(1, library.Snapshot().UnverifiedChartFiles);
            using var db = AuditDatabase.Open(library.Db, false);
            Assert.Equal("UNVERIFIED", db.FindChart(PathPolicy.Normalize(Path.Combine(source, "one.bms")))!.Status);
        }
        finally
        {
            // Remove the temporary junction before its target is removed by fixture cleanup.
            Assert.True(PathPolicy.Contains(library.Base, source));
            Assert.True(PathPolicy.Contains(library.Base, external));
            Directory.Delete(source);
        }
    }

    [Fact]
    public void ForeignDatabaseAndSourceOutputsAreRejectedWithoutChanges()
    {
        using var library = new SyntheticLibrary();
        var path = library.Chart("A", "one.bms", "source");
        var foreign = Path.Combine(library.Base, "songdata.db");
        using (var connection = new SqliteConnection($"Data Source={foreign};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE song(title TEXT); INSERT INTO song VALUES('preserve');";
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(foreign);
        Assert.Throws<InvalidOperationException>(() => new ChartScanner().Scan(new(foreign, [library.Root])));
        Assert.Equal(before, File.ReadAllBytes(foreign));
        var invalidDb = Path.Combine(library.Root, "audit.db");
        Assert.Throws<InvalidOperationException>(() => new ChartScanner().Scan(new(invalidDb, [library.Root])));
        Assert.False(File.Exists(invalidDb));
        library.Scan();
        Assert.Throws<InvalidOperationException>(() => ReportWriter.Write(library.Db, Path.Combine(library.Root, "reports")));
        Assert.False(Directory.Exists(Path.Combine(library.Root, "reports")));
        Assert.Equal("source", File.ReadAllText(path));
    }

    [Fact]
    public void FailedOrCancelledScanRollsBackAndDoesNotReplaceSnapshot()
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", "one.bms", "x");
        var first = library.Scan();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ChartScanner().Scan(new(library.Db, [library.Root]), cancel.Token));
        Assert.Equal(first.ScanId, library.Snapshot().ScanId);
        Assert.Single(library.Snapshot().Charts);
        using (var database = AuditDatabase.Open(library.Db, true))
        {
            database.Begin();
            database.Execute("UPDATE chart_files SET present=0;");
            // Disposal without commit simulates an interrupted scan.
        }
        Assert.Single(library.Snapshot().Charts);
    }

    [Fact]
    public void LatestScanRootConfigurationExcludesOldRootsAndProtectsHistoricalSources()
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", "one.bms", "x");
        var other = Path.Combine(library.Base, "other-root");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "two.bms"), "x");
        new ChartScanner().Scan(new(library.Db, [library.Root, other]));
        Assert.Equal(2, library.Snapshot().Charts.Count);
        new ChartScanner().Scan(new(library.Db, [other]));
        Assert.Single(library.Snapshot().Charts);
        Assert.Equal(new[] { other }, library.Snapshot().Roots);
        Assert.Throws<InvalidOperationException>(() => ReportWriter.Write(library.Db, Path.Combine(library.Root, "reports")));
    }

    [Fact]
    public void InvalidRootConfigurationAndSidecarCollisionFailClosed()
    {
        using var library = new SyntheticLibrary();
        Assert.Throws<ArgumentException>(() => new ChartScanner().Scan(new(library.Db, [])));
        var nested = Path.Combine(library.Root, "nested");
        Directory.CreateDirectory(nested);
        Assert.Throws<ArgumentException>(() => new ChartScanner().Scan(new(library.Db, [library.Root, nested])));
        library.Scan();
        File.WriteAllText(library.Db + "-journal", "preserve");
        Assert.Throws<InvalidOperationException>(() => library.Scan());
        Assert.Equal("preserve", File.ReadAllText(library.Db + "-journal"));
    }

    [Fact]
    public void IncompleteFolderCannotAppearAsAnExactOrSubsetCandidate()
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", "common.bms", "shared");
        var unique = library.Chart("A", "unique.bms", "unique");
        library.Chart("B", "common.bms", "shared");
        library.Scan();
        using var locked = new FileStream(unique, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        library.Scan(rehash: true);
        var snapshot = library.Snapshot();
        var analysis = RelationshipAnalyzer.Analyze(snapshot.Charts, snapshot.Diagnostics);
        Assert.Empty(analysis.Candidates);
        Assert.Single(analysis.IncompleteFolders);
        Assert.Single(analysis.DuplicateGroups); // Valid individual chart evidence remains available.
        var output = Path.Combine(library.Base, "reports");
        ReportWriter.Write(library.Db, output);
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "summary.json")));
        Assert.Equal(3, summary.RootElement.GetProperty("current_chart_files").GetInt32());
        Assert.Equal(2, summary.RootElement.GetProperty("verified_chart_files").GetInt32());
        Assert.Equal(0, summary.RootElement.GetProperty("candidate_folder_pairs").GetInt32());
    }

    [Fact]
    public void ScanDoesNotModifySourceBytesOrLastWriteTimes()
    {
        using var library = new SyntheticLibrary();
        library.Chart("A", "one.bms", "x");
        library.Chart("B", "one.bms", "x");
        library.Chart("A", "001.wav", "opaque audio");
        var before = Directory.GetFiles(library.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => (Bytes: File.ReadAllBytes(path), Mtime: File.GetLastWriteTimeUtc(path)));
        library.Scan(); library.Scan(); library.Scan(rehash: true);
        ReportWriter.Write(library.Db, Path.Combine(library.Base, "reports"));
        Assert.Equal(before.Count, Directory.GetFiles(library.Root, "*", SearchOption.AllDirectories).Length);
        foreach (var (path, evidence) in before)
        {
            Assert.Equal(evidence.Bytes, File.ReadAllBytes(path));
            Assert.Equal(evidence.Mtime, File.GetLastWriteTimeUtc(path));
        }
    }

    [Fact]
    public void OwnershipVersionAndPlatformComparisonAreValidatedBeforeWriting()
    {
        using var library = new SyntheticLibrary();
        library.Scan();
        using (var database = AuditDatabase.Open(library.Db, true))
            database.Execute("PRAGMA user_version=999;");
        var before = File.ReadAllBytes(library.Db);
        Assert.Throws<InvalidOperationException>(() => library.Scan());
        Assert.Equal(before, File.ReadAllBytes(library.Db));
        using (var connection = new SqliteConnection($"Data Source={library.Db};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version=1; UPDATE schema_metadata SET value='other-platform' WHERE key='path_comparison';";
            command.ExecuteNonQuery();
        }
        before = File.ReadAllBytes(library.Db);
        Assert.Throws<InvalidOperationException>(() => library.Scan());
        Assert.Equal(before, File.ReadAllBytes(library.Db));
    }

    [Fact]
    public void WindowsDeviceAliasesCannotBypassContainmentChecks()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<ArgumentException>(() => PathPolicy.Absolute(@"\\?\C:\songs\audit.db"));
        Assert.Throws<ArgumentException>(() => PathPolicy.Absolute(@"\\.\C:\songs\audit.db"));
        Assert.Throws<ArgumentException>(() => PathPolicy.Absolute(@"\??\C:\songs\audit.db"));
    }

    private sealed class SyntheticLibrary : IDisposable
    {
        private readonly string temporaryParent = Path.Combine(AppContext.BaseDirectory, "test-libraries");
        public string Base { get; }
        public string Root { get; }
        public string Db => Path.Combine(Base, "audit.db");
        public SyntheticLibrary()
        {
            Base = Path.Combine(temporaryParent, Guid.NewGuid().ToString("N"));
            Root = Path.Combine(Base, "songs");
            Directory.CreateDirectory(Root);
        }
        public string Chart(string folder, string filename, string content)
        {
            var directory = Path.Combine(Root, folder);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, filename);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }
        public ScanResult Scan(bool rehash = false) => new ChartScanner().Scan(new(Db, [Root], rehash));
        public ScanSnapshot Snapshot()
        { using var database = AuditDatabase.Open(Db, false); return database.Snapshot(); }
        public void Dispose()
        {
            if (!PathPolicy.Contains(temporaryParent, Base) || PathPolicy.Absolute(Base) == PathPolicy.Absolute(temporaryParent))
                throw new InvalidOperationException("Refusing test cleanup outside its temporary child directory.");
            Directory.Delete(Base, recursive: true);
        }
    }
}
