using System.Security.Cryptography;
using System.Text;

namespace BmsLibraryAudit.SourceInspector.Offline;

// Consumes only a caller-owned stream. Does not seek, close or open it.
internal sealed class IncrementalCsv(Stream input, ProcessingPolicy policy, CancellationToken cancellation) : IDisposable
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    public long BytesConsumed { get; private set; }
    public string? CompleteDigest { get; private set; }
    public bool EndOfInputObserved { get; private set; }

    public IEnumerable<string[]> Rows(IReadOnlyList<string> header)
    {
        policy.Validate(); cancellation.ThrowIfCancellationRequested();
        policy.Limit(header.Count > policy.MaxReportRows, "HEADER_LIMIT");
        foreach (var name in header) policy.Text(name);
        if (header.Count == 0 || header.Any(string.IsNullOrEmpty) || header.Distinct(StringComparer.Ordinal).Count() != header.Count)
            throw new EvidenceException(FindingKind.Invalid, "CSV_HEADER");
        var decoder = new UTF8Encoding(false, true).GetDecoder();
        var buffer = new byte[policy.MaxChunkBytes];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        var fields = new List<string>(); var field = new StringBuilder();
        bool quoted = false, ended = false, start = true, first = true, headerSeen = false, lastLf = false;
        var rowCount = 0; long retainedChars = 0;
        void FinishField()
        {
            fields.Add(field.ToString()); field.Clear(); ended = false; start = true;
            if (fields.Count > header.Count) throw new EvidenceException(FindingKind.Invalid, "CSV_FIELD_COUNT");
        }
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var count = input.Read(buffer, 0, buffer.Length);
            if (count == 0) EndOfInputObserved = true;
            BytesConsumed = checked(BytesConsumed + count);
            policy.Limit(BytesConsumed > policy.MaxStreamBytes, "STREAM_BYTE_LIMIT");
            hash.AppendData(buffer.AsSpan(0, count));
            cancellation.ThrowIfCancellationRequested();
            int written;
            try { written = decoder.GetChars(buffer, 0, count, chars, 0, flush: count == 0); }
            catch (DecoderFallbackException) { throw new EvidenceException(FindingKind.Invalid, "INVALID_UTF8"); }
            for (var index = 0; index < written; index++)
            {
                cancellation.ThrowIfCancellationRequested(); var c = chars[index];
                if (first && c == '\ufeff') throw new EvidenceException(FindingKind.Pending, "BOM_UNSUPPORTED");
                first = false; lastLf = false;
                if (quoted)
                {
                    if (c == '"') { quoted = false; ended = true; }
                    else field.Append(c);
                }
                else if (c == '"' && ended) { field.Append('"'); quoted = true; ended = false; }
                else if (c == ',') FinishField();
                else if (c == '\n')
                {
                    FinishField();
                    if (fields.Count != header.Count) throw new EvidenceException(FindingKind.Invalid, "CSV_FIELD_COUNT");
                    var row = fields.ToArray(); fields.Clear(); lastLf = true;
                    if (!headerSeen)
                    {
                        if (!row.SequenceEqual(header, StringComparer.Ordinal)) throw new EvidenceException(FindingKind.Invalid, "CSV_HEADER");
                        headerSeen = true;
                    }
                    else
                    {
                        policy.Limit(++rowCount > policy.MaxReportRows, "REPORT_ROW_LIMIT");
                        foreach (var value in row) retainedChars = checked(retainedChars + value.Length);
                        policy.Limit(retainedChars > policy.MaxRetainedReportChars, "REPORT_CHARACTER_BUDGET");
                        yield return row;
                    }
                }
                else if (c == '\r') throw new EvidenceException(FindingKind.Pending, "CSV_CRLF_UNSUPPORTED");
                else if (c == '"' && start) { quoted = true; start = false; }
                else
                {
                    if (ended || c == '"') throw new EvidenceException(FindingKind.Invalid, "CSV_QUOTING");
                    field.Append(c); start = false;
                }
                policy.Limit(field.Length > policy.MaxStringChars, "FIELD_LIMIT");
            }
            if (count == 0) break;
        }
        if (quoted) throw new EvidenceException(FindingKind.Invalid, "CSV_QUOTING");
        if (!lastLf || fields.Count != 0 || field.Length != 0 || ended)
            throw new EvidenceException(FindingKind.Pending, "CSV_FINAL_LF_REQUIRED");
        if (!headerSeen) throw new EvidenceException(FindingKind.Invalid, "CSV_HEADER");
        cancellation.ThrowIfCancellationRequested();
        CompleteDigest = Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public void Dispose() => hash.Dispose();
}

internal sealed class MemoryInput(ReadOnlyMemory<byte> bytes) : Stream
{
    private int position;
    public override int Read(byte[] buffer, int offset, int count)
    {
        var take = Math.Min(count, bytes.Length - position);
        bytes.Span.Slice(position, take).CopyTo(buffer.AsSpan(offset, take)); position += take; return take;
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
