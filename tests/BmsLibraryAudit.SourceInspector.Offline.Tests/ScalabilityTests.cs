using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;
using Xunit.Abstractions;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

[CollectionDefinition("BoundedStress", DisableParallelization = true)]
public sealed class BoundedStressCollection;

[Collection("BoundedStress")]
public sealed class ScalabilityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("resource")] [InlineData("environment")] [InlineData("failed_execution")]
    public void AdmissionAccountingIsVerifiedWithoutLargeAllocations(string outcome)
    {
        var emitted = new List<string>(); var started = false;
        // Inject tiny budgets; this test never allocates the requested chart population.
        var admitted = outcome != "resource";
        var execution = new StressExecution(emitted.Add, "accounting-control-" + outcome, "synthetic control", 1, 2, admitted,
            "Explicit " + outcome + " record independent of a passing framework assertion", "CONTROL");
        if (outcome == "resource")
        {
            Assert.False(StressExecution.Fits(2, 1, 100));
            using (execution)
            {
                Assert.False(execution.Admitted);
                Assert.Throws<InvalidOperationException>(execution.Start);
                Assert.Throws<InvalidOperationException>(() => execution.Complete("EXECUTED_PASS", "forbidden", "elapsed_ms=0"));
            }
        }
        else if (outcome == "environment")
        {
            using (execution) execution.BlockEnvironment("Synthetic environment unavailable; no allocation attempted.");
        }
        else
        {
            Assert.IsType<InvalidOperationException>(Record.Exception((Action)(() =>
            {
                using (execution) { execution.Start(); started = true; throw new InvalidOperationException("independent controlled failure"); }
            })));
        }
        var line = Assert.Single(emitted);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<StressExecutionRecord>(line[StressExecution.Prefix.Length..])!;
        Assert.Equal(started, parsed.WorkloadExecuted); Assert.Equal("CONTROL", parsed.Purpose);
        Assert.Equal("REQUIRES_TRX_CORRELATION", parsed.TestRunnerStatus);
        Assert.Equal(outcome switch { "resource" => "NOT_EXECUTED_RESOURCE_GATE", "environment" => "BLOCKED_ENVIRONMENT", _ => "EXECUTED_FAIL" }, parsed.ActualOutcome);
        Assert.NotNull(parsed.Reason);
        if (!started) { Assert.Null(parsed.ElapsedMilliseconds); Assert.Null(parsed.Measurements); }
        output.WriteLine(line); // The separate TRX audit resolves the actual runner status.
    }
    private const long RunBudget = 1024L * 1024 * 1024;
    private static bool Fits(long estimate) => StressExecution.Fits(estimate, RunBudget, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
    private sealed class Measurement : IDisposable
    {
        private readonly long allocated = GC.GetTotalAllocatedBytes(true);
        private readonly Stopwatch timer = Stopwatch.StartNew();
        private readonly Process process = Process.GetCurrentProcess();
        private readonly Timer sampler;
        private long peakManaged, peakWorkingSet;
        public Measurement() { Sample(); sampler = new(_ => Sample(), null, 100, 100); }
        private void Sample()
        {
            lock (process)
            {
                process.Refresh(); peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
                peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
            }
        }
        public string Finish() { Sample(); return $"elapsed_ms={timer.ElapsedMilliseconds}; allocated_managed={GC.GetTotalAllocatedBytes(true) - allocated}; sampled_peak_managed={peakManaged}; sampled_peak_working_set={peakWorkingSet}; lifetime_peak_working_set={process.PeakWorkingSet64}"; }
        public void Dispose() { sampler.DisposeAsync().AsTask().GetAwaiter().GetResult(); process.Dispose(); }
    }
    private static Chart[] Population(int count, int folderCount, bool shared)
    {
        var parents = Enumerable.Range(0, folderCount).Select(i => Fixtures.RootPath + "\\F" + i.ToString("D5", CultureInfo.InvariantCulture)).ToArray();
        var hashes = Enumerable.Range(0, shared ? 16 : folderCount).Select(i => i.ToString("x64", CultureInfo.InvariantCulture)).ToArray();
        var md5s = Enumerable.Range(0, hashes.Length).Select(i => i.ToString("x32", CultureInfo.InvariantCulture)).ToArray();
        var rows = new Chart[count];
        for (var i = 0; i < rows.Length; i++)
        {
            var folder = i % folderCount; var identity = shared ? i / folderCount % 16 : folder;
            var name = "c" + i.ToString(CultureInfo.InvariantCulture) + ".bms"; var path = parents[folder] + "\\" + name;
            rows[i] = new(1, path, path.ToUpperInvariant(), parents[folder], name, ".bms", 1, 0, md5s[identity], hashes[identity], 1, 1, 1, "OK");
        }
        return rows;
    }
    [Theory]
    [InlineData(250000, 25000, false)]
    [InlineData(500000, 128, true)]
    [InlineData(1000000, 128, true)]
    public void ProgressiveChartPopulationsHaveExactIndependentOracles(int count, int folderCount, bool shared)
    {
        // The million-row profile was admitted only after the separate 500k calibration TRX
        // measured 269,101,312 managed / 359,452,672 working-set bytes. Keep headroom.
        var estimate = checked(count == 1000000 ? count * 800L + 100_000_000L : count * 1536L + folderCount * 4096L);
        var contributions = shared ? 16L * folderCount * (folderCount - 1) / 2 : 0;
        var expectedPairs = shared ? folderCount * (folderCount - 1) / 2 : 0;
        output.WriteLine($"PLAN charts={count}; folders={folderCount}; estimate={estimate}; run_budget={RunBudget}; contributions={contributions}; pairs={expectedPairs}; gc_available={GC.GetGCMemoryInfo().TotalAvailableMemoryBytes}");
        using var execution = new StressExecution(output.WriteLine, $"charts-{count}-{folderCount}-{shared}", "charts", count, estimate, Fits(estimate),
            $"folders={folderCount}; hashes={(shared ? 16 : folderCount)}; contributions={contributions}; pairs={expectedPairs}; complete exact closure");
        if (!execution.Admitted) return;
        execution.Start();
        using var measurement = new Measurement();
        var charts = Population(count, folderCount, shared);
        var policy = Fixtures.Policy with { MaxChartRecords = count, MaxFolders = folderCount };
        Assert.True(Provenance.Validate(Fixtures.Evidence(charts), 1, policy).Valid);
        var analysis = Relationships.Analyze(charts, [], policy);
        Assert.Equal(folderCount, analysis.Folders.Count); Assert.Equal(count, analysis.Folders.Sum(f => f.PhysicalFiles));
        Assert.Equal(shared ? 16 : folderCount, analysis.Duplicates.Count);
        Assert.Equal(count, analysis.Duplicates.Sum(g => g.Locations.Count));
        Assert.Equal(expectedPairs, analysis.Candidates.Count); Assert.Empty(analysis.IncompleteFolders);
        Assert.All(analysis.Folders, f => Assert.Equal(shared ? 16 : 1, f.Hashes.Count));
        Assert.All(analysis.Duplicates, g => Assert.True(g.Locations.Zip(g.Locations.Skip(1)).All(p => StringComparer.Ordinal.Compare(p.First.Key, p.Second.Key) < 0)));
        if (shared)
        {
            Assert.All(analysis.Candidates, c => { Assert.Equal("EXACT_CHART_SET", c.Relationship); Assert.Equal(16, c.Common); Assert.Equal(1, c.Jaccard); });
            Assert.Equal(2, analysis.Components.Count);
            Assert.All(analysis.Components, c => Assert.Equal(folderCount, c.Members.Count));
            Assert.Equal("RELATED_000001", analysis.Components[0].Id); Assert.Equal("EXACT_ONLY_000001", analysis.Components[1].Id);
        }
        else Assert.Empty(analysis.Components);
        output.WriteLine($"STRESS PASSED complete=true; charts={count}; hashes={(shared ? 16 : folderCount)}; contributions={contributions}; pairs={expectedPairs}; {measurement.Finish()}");
        execution.Complete("EXECUTED_PASS", "Full provenance and relationship oracles passed.", measurement.Finish());
    }
    [Fact]
    public void InitialMillionChartEstimateIsRejectedBeforeCalibration()
    {
        var estimate = 1_000_000L * 1536 + 25_000L * 4096;
        using var execution = new StressExecution(output.WriteLine, "initial-million-admission", "charts", 1000000, estimate, Fits(estimate),
            "Reject initial estimate above the unchanged 1 GiB budget without chart allocation.", "ADMISSION_PLANNER");
        Assert.True(estimate > RunBudget);
        output.WriteLine($"INITIAL PLAN NOT_EXECUTED charts=1000000; estimated_bytes={estimate}; run_budget={RunBudget}; reason=uncalibrated retention estimate exceeds per-run budget. A separately measured 500k profile justifies the revised million-row experiment; not all million-row distributions are admitted.");
    }
    [Fact]
    public void ManyUniqueHashesFitTheRevisedDefaultChartEnvelope()
    {
        const int count = 250000, folders = 25000;
        const long estimate = 750000000;
        output.WriteLine($"PLAN charts={count}; folders={folders}; hashes={count}; contributions=0; pairs=0; estimate={estimate}");
        using var execution = new StressExecution(output.WriteLine, "unique-hashes-250000", "charts", count, estimate, Fits(estimate),
            "25000 folders; 250000 distinct hashes; zero duplicate groups/pairs/components");
        if (!execution.Admitted) return;
        execution.Start();
        using var measurement = new Measurement();
        var charts = Population(count, folders, false);
        for (var i = 0; i < charts.Length; i++) charts[i] = charts[i] with { Sha256 = i.ToString("x64", CultureInfo.InvariantCulture), Md5 = i.ToString("x32", CultureInfo.InvariantCulture) };
        Assert.True(Provenance.Validate(Fixtures.Evidence(charts), 1, Fixtures.Policy).Valid);
        var result = Relationships.Analyze(charts, [], Fixtures.Policy);
        Assert.Equal(folders, result.Folders.Count); Assert.Equal(count, result.Folders.Sum(f => f.PhysicalFiles));
        Assert.Equal(count, result.Folders.Sum(f => f.Hashes.Count));
        Assert.All(result.Folders, f => Assert.Equal(10, f.Hashes.Count));
        Assert.Empty(result.Duplicates); Assert.Empty(result.Candidates); Assert.Empty(result.Components);
        output.WriteLine($"STRESS PASSED complete=true; charts={count}; hashes={count}; contributions=0; pairs=0; {measurement.Finish()}");
        execution.Complete("EXECUTED_PASS", "Every default-envelope count and folder set passed.", measurement.Finish());
    }
    [Fact]
    public void DensePairsFitDeclaredBudgetWithoutLosingExactClosure()
    {
        const int folders = 447, pairs = 99681;
        using var execution = new StressExecution(output.WriteLine, "dense-pairs-447", "charts", folders, 80000000, true,
            "99681 exact pairs; two complete 447-member components"); execution.Start();
        output.WriteLine("PLAN charts=447; folders=447; hashes=1; contributions=99681; pairs=99681; estimated_bytes=80000000");
        using var measurement = new Measurement();
        var charts = Population(folders, folders, false);
        for (var i = 0; i < charts.Length; i++) charts[i] = charts[i] with { Sha256 = Fixtures.Hash('a') };
        var result = Relationships.Analyze(charts, [], Fixtures.Policy);
        Assert.Equal(pairs, result.Candidates.Count); Assert.All(result.Candidates, c => Assert.Equal("EXACT_CHART_SET", c.Relationship));
        Assert.Equal(2, result.Components.Count); Assert.All(result.Components, c => Assert.Equal(folders, c.Members.Count));
        output.WriteLine("STRESS PASSED complete=true; contributions=99681; pairs=99681; " + measurement.Finish());
        execution.Complete("EXECUTED_PASS", "Full dense pair and closure assertions passed.", measurement.Finish());
    }
    [Fact]
    public void CancellationDuringLargeChartProcessingReturnsNoAnalysis()
    {
        using var execution = new StressExecution(output.WriteLine, "chart-cancellation-150000", "charts", 150000, 234496000, true,
            "Owned cancellation at visited chart 100001; no analysis"); execution.Start();
        output.WriteLine("PLAN charts=150000; folders=1000; cancel_on_chart=100001; estimated_bytes=234496000; pair_work=0");
        using var cancel = new CancellationTokenSource(); using var measurement = new Measurement();
        var charts = Population(150000, 1000, false);
        var source = new CancelCharts(charts, cancel);
        Assert.Throws<OperationCanceledException>(() => Relationships.Analyze(source, [], Fixtures.Policy, cancel.Token));
        Assert.Equal(100001, source.Visited);
        output.WriteLine("STRESS CANCELLED complete=false; visited_charts=100001; " + measurement.Finish());
        execution.Complete("CANCELLED_EXPECTED", "Cancellation observed at chart 100001; no analysis returned.", measurement.Finish());
    }
    private sealed class CancelCharts(Chart[] rows, CancellationTokenSource cancellation) : IReadOnlyList<Chart>
    {
        public int Visited { get; private set; }
        public int Count => rows.Length;
        public Chart this[int index] => rows[index];
        public IEnumerator<Chart> GetEnumerator()
        {
            foreach (var row in rows) { if (++Visited == 100001) cancellation.Cancel(); yield return row; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    [Fact]
    public void HighlySharedHashStopsBeforeQuadraticPairAllocation()
    {
        const int folders = 10000;
        using var execution = new StressExecution(output.WriteLine, "shared-hash-10000", "charts", folders, 56320000, true,
            "49995000 contributions; PAIR_EXPANSION_LIMIT before materialized pairs"); execution.Start();
        output.WriteLine("PLAN charts=10000; folders=10000; hashes=1; contributions=49995000; materialized_pairs=0; estimated_bytes=56320000");
        using var measurement = new Measurement();
        var charts = Population(folders, folders, false).Select(c => c with { Sha256 = Fixtures.Hash('a') }).ToArray();
        var result = Reconciliation.Compare(Fixtures.Evidence(charts), Fixtures.ReportsForSimple(), 1, Fixtures.Policy);
        Assert.Contains(result.Findings, f => f.Code == "PAIR_EXPANSION_LIMIT" && f.Kind == FindingKind.ResourceLimit);
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement); Assert.Null(result.Analysis);
        output.WriteLine("STRESS RESOURCE_BLOCKED complete=false; materialized_pairs=0; " + measurement.Finish());
        execution.Complete("RESOURCE_STOP_EXPECTED", "PAIR_EXPANSION_LIMIT; incomplete reconciliation; no analysis.", measurement.Finish());
    }
    [Fact]
    public void ManySparseRelationshipsPreserveTransitiveClosureAndExactLabels()
    {
        const int folders = 20000;
        using var execution = new StressExecution(output.WriteLine, "sparse-chain-20000", "charts", 39998, 143357952, true,
            "19999 pairs; one complete 20000-member RELATED component; no EXACT_ONLY"); execution.Start();
        output.WriteLine("PLAN charts=39998; folders=20000; hashes=19999; contributions=19999; pairs=19999; estimated_bytes=143357952");
        using var measurement = new Measurement();
        var charts = new List<Chart>(2 * folders - 2);
        for (var edge = 0; edge < folders - 1; edge++)
        {
            var hash = edge.ToString("x64", CultureInfo.InvariantCulture);
            charts.Add(Fixtures.Chart("S" + edge.ToString("D5", CultureInfo.InvariantCulture), edge) with { Sha256 = hash });
            charts.Add(Fixtures.Chart("S" + (edge + 1).ToString("D5", CultureInfo.InvariantCulture), edge) with { Sha256 = hash });
        }
        var analysis = Relationships.Analyze(charts, [], Fixtures.Policy);
        Assert.Equal(folders - 1, analysis.Candidates.Count); Assert.Equal(folders, Assert.Single(analysis.Components).Members.Count);
        Assert.Equal("RELATED_000001", analysis.Components[0].Id);
        Assert.Equal("A_SUBSET_OF_B", analysis.Candidates[0].Relationship); Assert.Equal("B_SUBSET_OF_A", analysis.Candidates[^1].Relationship);
        Assert.All(analysis.Candidates.Skip(1).SkipLast(1), c => Assert.Equal("MEDIUM_OVERLAP", c.Relationship));
        output.WriteLine("STRESS PASSED complete=true; contributions=19999; pairs=19999; " + measurement.Finish());
        execution.Complete("EXECUTED_PASS", "Sparse pair labels and full transitive closure passed.", measurement.Finish());
    }
    [Fact]
    public void SeveralLargeExactComponentsKeepFullOrderedMembers()
    {
        using var execution = new StressExecution(output.WriteLine, "four-exact-components", "charts", 800, 11000000, true,
            "19800 exact pairs; four RELATED and four EXACT_ONLY components, 100 ordered members each"); execution.Start();
        output.WriteLine("PLAN charts=800; folders=400; hashes=8; contributions=39600; pairs=19800; estimated_bytes=11000000");
        using var measurement = new Measurement();
        var charts = new List<Chart>();
        for (var group = 0; group < 4; group++)
            for (var folder = 0; folder < 100; folder++)
                for (var chart = 0; chart < 2; chart++)
                    charts.Add(Fixtures.Chart($"G{group}\\F{folder:D3}", chart) with { Sha256 = (2 * group + chart).ToString("x64", CultureInfo.InvariantCulture) });
        var result = Relationships.Analyze(charts, [], Fixtures.Policy);
        Assert.Equal(19800, result.Candidates.Count); Assert.All(result.Candidates, c => Assert.Equal("EXACT_CHART_SET", c.Relationship));
        Assert.Equal(8, result.Components.Count);
        for (var group = 0; group < 4; group++)
        {
            Assert.Equal($"EXACT_ONLY_{group + 1:D6}", result.Components[group + 4].Id);
            Assert.Equal(Enumerable.Range(0, 100).Select(f => Fixtures.RootPath + $"\\G{group}\\F{f:D3}"), result.Components[group + 4].Members);
        }
        output.WriteLine("STRESS PASSED complete=true; contributions=39600; pairs=19800; " + measurement.Finish());
        execution.Complete("EXECUTED_PASS", "Every pair label and complete ordered exact component passed.", measurement.Finish());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LargeCsvUsesGeneratedStreamWithLateMalformedRecord(bool malformed)
    {
        const int rows = 250000;
        using var execution = new StressExecution(output.WriteLine, "cluster-csv-250000-" + (malformed ? "malformed" : "valid"), "rows", rows, 128000000, true,
            malformed ? "Late malformed record; no complete digest/value" : "250000 ordered rows; exact independently computed SHA-256/count"); execution.Start();
        output.WriteLine("PLAN rows=250000; estimated_retained_bytes=128000000; text_buffer=65536; pair_work=0");
        using var expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long byteCount = 0;
        foreach (var line in Lines(rows, malformed)) { var bytes = Encoding.UTF8.GetBytes(line); expected.AppendData(bytes); byteCount += bytes.Length; }
        var digest = Convert.ToHexStringLower(expected.GetHashAndReset());
        Assert.True(byteCount > 8 * 1024 * 1024);
        using var measurement = new Measurement(); using var input = new GeneratedStream(Lines(rows, malformed));
        var parsed = ReportInputs.Clusters(input, Fixtures.Policy with { MaxReportRows = rows });
        if (malformed)
        {
            Assert.Equal(ParseStatus.Malformed, parsed.Status); Assert.Null(parsed.Sha256); Assert.Null(parsed.Value); Assert.False(parsed.BytesComplete);
        }
        else
        {
            Assert.Equal(ParseStatus.Complete, parsed.Status); Assert.Equal(byteCount, parsed.BytesConsumed); Assert.Equal(digest, parsed.Sha256);
            Assert.Equal(rows, parsed.Value!.Count);
            for (var i = 0; i < rows; i++) Assert.Equal(new ClusterRow("RELATED", $"RELATED_{i / 2 + 1:D6}", 2, $"C:\\Synthetic\\R{i:D6}"), parsed.Value[i]);
        }
        output.WriteLine($"STRESS {(malformed ? "MALFORMED_EXPECTED" : "PASSED")}; complete={parsed.ParsingComplete}; input_bytes={byteCount}; consumed_bytes={parsed.BytesConsumed}; {measurement.Finish()}");
        execution.Complete("EXECUTED_PASS", $"Strict parser outcome={parsed.Status}; consumed_bytes={parsed.BytesConsumed}; independent oracle passed.", measurement.Finish());
    }
    [Fact]
    public void CancellationDuringLargeCsvStopsWithNoCompleteFingerprint()
    {
        using var execution = new StressExecution(output.WriteLine, "csv-cancellation-250000", "rows", 250000, 16000000, true,
            "Cancellation after 1048576 consumed bytes; no complete digest/value"); execution.Start();
        output.WriteLine("PLAN rows=250000; cancel_after_bytes=1048576; estimated_retained_bytes=16000000; pair_work=0");
        using var cancel = new CancellationTokenSource(); using var measurement = new Measurement();
        using var input = new GeneratedStream(Lines(250000, false), bytes => { if (bytes >= 1048576) cancel.Cancel(); });
        var parsed = ReportInputs.Clusters(input, Fixtures.Policy with { MaxReportRows = 250000 }, cancel.Token);
        Assert.Equal(ParseStatus.Cancelled, parsed.Status); Assert.Null(parsed.Sha256); Assert.Null(parsed.Value);
        output.WriteLine($"STRESS CANCELLED complete=false; consumed_bytes={parsed.BytesConsumed}; {measurement.Finish()}");
        execution.Complete("CANCELLED_EXPECTED", $"Cancelled; consumed_bytes={parsed.BytesConsumed}; digest/value absent.", measurement.Finish());
    }
    [Fact]
    public void LargeDuplicateCsvFitsDefaultStreamingEnvelopeWithEveryPhysicalRow()
    {
        const int rows = 250000;
        output.WriteLine("PLAN duplicate_rows=250000; estimate=400000000; retained_text_budget=67108864; pair_work=0");
        using var execution = new StressExecution(output.WriteLine, "duplicate-csv-250000", "rows", rows, 400000000, Fits(400000000),
            "250000 complete physical rows; exact byte count/SHA and ordered tuples");
        if (!execution.Admitted) return;
        execution.Start();
        var sha = Fixtures.Hash('a'); var md5 = new string('b', 32);
        IEnumerable<string> DuplicateLines()
        {
            yield return "sha256,md5,group_paths,root_id,absolute_path,parent_folder,filename,extension,size,mtime_utc\n";
            for (var i = 0; i < rows; i++) yield return $"{sha},{md5},250000,1,C:\\Synthetic\\A\\c{i:D6}.bms,C:\\Synthetic\\A,c{i:D6}.bms,.bms,1,2020-01-01T00:00:00.0000000+00:00\n";
        }
        using var expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long byteCount = 0;
        foreach (var line in DuplicateLines()) { var bytes = Encoding.UTF8.GetBytes(line); expected.AppendData(bytes); byteCount += bytes.Length; }
        var digest = Convert.ToHexStringLower(expected.GetHashAndReset());
        Assert.True(byteCount > 8 * 1024 * 1024);
        using var measurement = new Measurement(); using var input = new GeneratedStream(DuplicateLines());
        var result = ReportInputs.Duplicates(input, Fixtures.Policy, expectedByteCount: byteCount);
        Assert.Equal(ParseStatus.Complete, result.Status); Assert.Equal(byteCount, result.BytesConsumed); Assert.Equal(digest, result.Sha256);
        Assert.Equal(rows, result.Value!.Count);
        for (var i = 0; i < rows; i++) Assert.Equal(new DuplicateRow(sha, md5, rows, 1, $"C:\\Synthetic\\A\\c{i:D6}.bms",
            @"C:\Synthetic\A", $"c{i:D6}.bms", ".bms", 1, "2020-01-01T00:00:00.0000000+00:00"), result.Value[i]);
        output.WriteLine($"STRESS PASSED complete=true; duplicate_rows={rows}; input_bytes={byteCount}; {measurement.Finish()}");
        execution.Complete("EXECUTED_PASS", $"All physical tuples passed; consumed_bytes={result.BytesConsumed}.", measurement.Finish());
    }
    private static IEnumerable<string> Lines(int rows, bool malformed)
    {
        yield return CorrectiveTests.ClusterHeader;
        for (var i = 0; i < rows; i++) yield return $"RELATED,RELATED_{i / 2 + 1:D6},2,C:\\Synthetic\\R{i:D6}\n";
        if (malformed) yield return "RELATED,RELATED_999999,2,\"unfinished\n";
    }
    private sealed class GeneratedStream(IEnumerable<string> lines, Action<long>? progress = null) : Stream
    {
        private readonly IEnumerator<string> source = lines.GetEnumerator();
        private byte[] current = []; private int position; private long total;
        public override int Read(byte[] buffer, int offset, int count)
        {
            var written = 0;
            while (written < count)
            {
                if (position == current.Length)
                {
                    if (!source.MoveNext()) break;
                    current = Encoding.UTF8.GetBytes(source.Current); position = 0;
                }
                var take = Math.Min(count - written, current.Length - position);
                current.AsSpan(position, take).CopyTo(buffer.AsSpan(offset + written, take)); position += take; written += take;
            }
            total += written; progress?.Invoke(total); return written;
        }
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
