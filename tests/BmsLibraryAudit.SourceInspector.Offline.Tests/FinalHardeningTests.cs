using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using BmsLibraryAudit.SourceInspector.Offline;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

public sealed class FinalHardeningTests
{
    private static readonly string[] Rows = ["RELATED,RELATED_000001,3,C:\\Synthetic\\A\n",
        "RELATED,RELATED_000001,3,C:\\Synthetic\\B\n", "RELATED,RELATED_000001,3,C:\\Synthetic\\C\n"];
    [Theory]
    [InlineData("io", 0)] [InlineData("io", 1)] [InlineData("io", 2)] [InlineData("io", 3)]
    [InlineData("disposed", 0)] [InlineData("disposed", 1)] [InlineData("disposed", 2)] [InlineData("disposed", 3)]
    [InlineData("unsupported", 0)] [InlineData("unsupported", 1)] [InlineData("unsupported", 2)] [InlineData("unsupported", 3)]
    [InlineData("denied", 0)] [InlineData("denied", 1)] [InlineData("denied", 2)] [InlineData("denied", 3)]
    public void KnownReadFailuresStopWithExactAccountingAndNoReconciliation(string failure, int afterRows)
    {
        var text = CorrectiveTests.ClusterHeader + string.Concat(Rows);
        var offset = afterRows == 0 ? 0 : Encoding.UTF8.GetByteCount(CorrectiveTests.ClusterHeader + string.Concat(Rows.Take(afterRows)));
        using var input = new FaultingStream(Encoding.UTF8.GetBytes(text), offset, () => failure switch
        {
            "io" => new IOException("synthetic"), "disposed" => new ObjectDisposedException("synthetic"),
            "unsupported" => new NotSupportedException("synthetic"), _ => new UnauthorizedAccessException("synthetic")
        });
        var parsed = ReportInputs.Clusters(input, Fixtures.Policy);
        Assert.Equal(ParseStatus.Unsupported, parsed.Status); Assert.Equal(FindingKind.Pending, parsed.Finding!.Kind);
        Assert.Equal("INPUT_READ_FAILED", parsed.Finding.Code); Assert.Equal(offset, parsed.BytesConsumed);
        Assert.Equal(offset, input.ReturnedBytes); Assert.False(parsed.EndOfSuppliedInputObserved);
        AssertIncomplete(parsed); Assert.Equal(0, input.DisposeCalls);
        var reports = Fixtures.ReportsForSimple(); reports = reports with { Bindings = reports.Bindings! with { Clusters = parsed } };
        var result = Reconciliation.Compare(Fixtures.Evidence(), reports, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement); Assert.Equal("INSPECTOR_PARTIAL", result.InspectorStatus);
        Assert.False(result.NativeInspectionComplete); Assert.False(result.Phase2ExecutionAuthorized);
        Assert.Contains(result.Findings, f => f.Code == "INPUT_READ_FAILED" && f.Kind == FindingKind.Pending);
        Assert.Equal(0, input.DisposeCalls);
    }
    [Fact]
    public void ClosedCallerStreamIsReportedButNeverDisposedAgain()
    {
        var input = new FaultingStream([], 0, () => new ObjectDisposedException("caller")); input.Dispose();
        var parsed = ReportInputs.Clusters(input, Fixtures.Policy);
        Assert.Equal("INPUT_READ_FAILED", parsed.Finding!.Code); AssertIncomplete(parsed); Assert.Equal(1, input.DisposeCalls);
    }
    [Fact]
    public void SuccessfulReadHasCompleteIndependentDigestAndCallerOwnership()
    {
        var bytes = Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader + string.Concat(Rows));
        using var input = new FaultingStream(bytes, null, () => new IOException());
        var parsed = ReportInputs.Clusters(input, Fixtures.Policy with { MaxChunkBytes = 7 });
        Assert.Equal(ParseStatus.Complete, parsed.Status); Assert.Equal(3, parsed.Value!.Count);
        Assert.Equal(bytes.Length, parsed.BytesConsumed); Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), parsed.Sha256);
        Assert.True(parsed.EndOfSuppliedInputObserved); Assert.Equal(0, input.DisposeCalls);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationFromReadKeepsOwnedCancellationSemantics(bool throwFromRead)
    {
        var bytes = Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader + Rows[0]);
        using var cancel = new CancellationTokenSource();
        using var input = new FaultingStream(bytes, throwFromRead ? bytes.Length : null,
            () => { cancel.Cancel(); return new OperationCanceledException(cancel.Token); },
            throwFromRead ? null : () => cancel.Cancel());
        var parsed = ReportInputs.Clusters(input, Fixtures.Policy, cancel.Token);
        Assert.Equal(ParseStatus.Cancelled, parsed.Status); Assert.Equal("CANCELLED", parsed.Finding!.Code);
        Assert.Equal(bytes.Length, parsed.BytesConsumed); AssertIncomplete(parsed); Assert.Equal(0, input.DisposeCalls);
        var reports = Fixtures.ReportsForSimple() with { Bindings = Fixtures.ReportsForSimple().Bindings! with { Clusters = parsed } };
        var result = Reconciliation.Compare(Fixtures.Evidence(), reports, 1, Fixtures.Policy, Fixtures.Oracle);
        Assert.False(result.CompletedLogicalScope); Assert.False(result.LogicalAgreement);
    }
    [Theory]
    [InlineData("argument")] [InlineData("defect")] [InlineData("oom")] [InlineData("unowned_cancel")]
    public void UnexpectedReadExceptionsAreNotSwallowed(string kind)
    {
        Exception expected = kind switch { "argument" => new ArgumentException("synthetic"), "defect" => new InvalidOperationException("synthetic"),
            "oom" => new OutOfMemoryException("synthetic signal only; no allocation"), _ => new OperationCanceledException() };
        using var input = new FaultingStream([], 0, () => expected);
        Assert.Same(expected, Record.Exception(() => ReportInputs.Clusters(input, Fixtures.Policy)));
        Assert.Equal(0, input.DisposeCalls);
    }
    [Theory]
    [InlineData("summary", 1)] [InlineData("clusters", 1)] [InlineData("duplicates", 1)] [InlineData("candidates", 1)]
    [InlineData("clusters", 7)] [InlineData("duplicates", 7)] [InlineData("candidates", 7)]
    [InlineData("clusters", 65536)] [InlineData("duplicates", 65536)] [InlineData("candidates", 65536)]
    public void BoundedInputsTakeExactlyOneOwnedSnapshot(string kind, int chunk)
    {
        var bytes = Payload(kind); var expected = Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var memory = new MutatingMemory(bytes);
        var parsed = Parse(kind, memory.Memory, Fixtures.Policy with { MaxChunkBytes = chunk });
        Assert.Equal(ParseStatus.Complete, parsed.Status); Assert.Equal(expected, parsed.Sha256); Assert.Equal(bytes.Length, parsed.Bytes);
        Assert.Equal(1, memory.SpanCalls); // Direct chunk reads would call repeatedly and switch the future A row to B.
        var changed = memory.Memory.Span; // Deterministically mutate caller storage after the owned snapshot.
        Assert.Equal(2, memory.SpanCalls); Assert.NotEqual(expected, Convert.ToHexStringLower(SHA256.HashData(changed)));
        Assert.Equal(expected, parsed.Sha256); Assert.Equal(kind == "summary" ? Fixtures.RootPath : @"C:\Synthetic\A", parsed.Path);
    }
    [Theory]
    [InlineData("summary")] [InlineData("clusters")] [InlineData("duplicates")] [InlineData("candidates")]
    public void OversizedBuffersAreRejectedBeforeAccessOrCopy(string kind)
    {
        using var memory = new RejectAccessMemory(10);
        var parsed = Parse(kind, memory.Memory, Fixtures.Policy with { MaxInputBytes = 9 });
        Assert.Equal(ParseStatus.ResourceStopped, parsed.Status); Assert.Null(parsed.Sha256); Assert.Equal(0, parsed.Bytes);
        Assert.Equal(0, memory.SpanCalls);
    }
    [Theory]
    [InlineData("summary")] [InlineData("clusters")] [InlineData("duplicates")] [InlineData("candidates")]
    public void CancellationDuringSnapshotCannotPublishCompleteEvidence(string kind)
    {
        using var cancel = new CancellationTokenSource(); using var memory = new MutatingMemory(Payload(kind), () => cancel.Cancel());
        var parsed = Parse(kind, memory.Memory, Fixtures.Policy, cancel.Token);
        Assert.Equal(ParseStatus.Cancelled, parsed.Status); Assert.Null(parsed.Sha256); Assert.Null(parsed.Path);
        Assert.Equal(1, memory.SpanCalls);
    }
    [Theory]
    [InlineData("clusters")] [InlineData("duplicates")] [InlineData("candidates")]
    public void EquivalentCsvRowsKeepDifferentExactOwnedByteIdentities(string kind)
    {
        var bytes = Payload(kind); var text = Encoding.UTF8.GetString(bytes);
        var alternative = Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(text,
            @"(?<=[,\n])C:\\Synthetic\\A(?=[,\n])", "\"C:\\Synthetic\\A\""));
        var first = Parse(kind, bytes, Fixtures.Policy); var second = Parse(kind, alternative, Fixtures.Policy);
        Assert.Equal(ParseStatus.Complete, first.Status); Assert.Equal(ParseStatus.Complete, second.Status);
        Assert.Equal(first.Path, second.Path); Assert.NotEqual(first.Sha256, second.Sha256);
        var truncated = Parse(kind, bytes.AsMemory(0, bytes.Length - 1), Fixtures.Policy);
        Assert.NotEqual(ParseStatus.Complete, truncated.Status); Assert.Null(truncated.Sha256); Assert.Null(truncated.Path);
    }
    private static void AssertIncomplete<T>(ReportEvidence<T> result) where T : class
    { Assert.False(result.ParsingComplete); Assert.False(result.BytesComplete); Assert.Null(result.Sha256); Assert.Null(result.Value); Assert.False(result.FilesystemIdentityVerified); Assert.False(result.HistoricalProducerVerified); }
    private sealed record View(ParseStatus Status, long Bytes, string? Sha256, string? Path);
    private static View Parse(string kind, ReadOnlyMemory<byte> bytes, ProcessingPolicy policy, CancellationToken cancellation = default)
    {
        if (kind == "summary") { var r = ReportInputs.Summary(bytes, policy, cancellation); return new(r.Status, r.BytesConsumed, r.Sha256, r.Value?.Roots[0]); }
        if (kind == "duplicates") { var r = ReportInputs.Duplicates(bytes, policy, cancellation); return new(r.Status, r.BytesConsumed, r.Sha256, r.Value?.FirstOrDefault()?.Parent); }
        if (kind == "candidates") { var r = ReportInputs.Candidates(bytes, policy, cancellation); return new(r.Status, r.BytesConsumed, r.Sha256, r.Value?.FirstOrDefault()?.FolderA); }
        var clusters = ReportInputs.Clusters(bytes, policy, cancellation); return new(clusters.Status, clusters.BytesConsumed, clusters.Sha256, clusters.Value?.FirstOrDefault()?.FolderPath);
    }
    private static byte[] Payload(string kind) => kind switch
    {
        "summary" => Fixtures.Canonical,
        "candidates" => Encoding.UTF8.GetBytes("folder_a,folder_b,physical_files_a,physical_files_b,charts_a,charts_b,common,coverage_a,coverage_b,jaccard,relationship\nC:\\Synthetic\\A,C:\\Synthetic\\Z,1,1,1,1,1,1,1,1,EXACT_CHART_SET\n"),
        "duplicates" => Encoding.UTF8.GetBytes("sha256,md5,group_paths,root_id,absolute_path,parent_folder,filename,extension,size,mtime_utc\n" +
            $"{new string('a', 64)},{new string('b', 32)},2,1,C:\\Synthetic\\A\\x.bms,C:\\Synthetic\\A,x.bms,.bms,1,2020-01-01T00:00:00.0000000+00:00\n"),
        _ => Encoding.UTF8.GetBytes(CorrectiveTests.ClusterHeader + "RELATED,RELATED_000001,2,C:\\Synthetic\\A\n")
    };
    private sealed class MutatingMemory(byte[] bytes, Action? accessed = null) : MemoryManager<byte>
    {
        public int SpanCalls { get; private set; }
        public override Memory<byte> Memory => CreateMemory(bytes.Length);
        public override Span<byte> GetSpan()
        {
            accessed?.Invoke();
            if (++SpanCalls == 2) { var at = Array.LastIndexOf(bytes, (byte)'A'); if (at < 0) at = Array.IndexOf(bytes, (byte)'S'); bytes[at]++; }
            return bytes;
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
    }
    private sealed class RejectAccessMemory(int length) : MemoryManager<byte>
    {
        public int SpanCalls { get; private set; }
        public override Memory<byte> Memory => CreateMemory(length);
        public override Span<byte> GetSpan() { SpanCalls++; throw new InvalidOperationException("Oversized input must not be accessed."); }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
    }
    private sealed class FaultingStream(byte[] bytes, int? failureAt, Func<Exception> failure, Action? afterRead = null) : Stream
    {
        private int position;
        public int DisposeCalls { get; private set; }
        public long ReturnedBytes => position;
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (DisposeCalls > 0) throw new ObjectDisposedException("caller");
            if (position == failureAt) throw failure();
            var take = Math.Min(count, (failureAt ?? bytes.Length) - position);
            bytes.AsSpan(position, take).CopyTo(buffer.AsSpan(offset, take)); position += take; afterRead?.Invoke(); return take;
        }
        protected override void Dispose(bool disposing) { if (disposing) DisposeCalls++; base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
