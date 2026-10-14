using System.Security.Cryptography;
using System.Text;

namespace BmsLibraryAudit.Core;

public static class PhaseThreeParser
{
    public static ChartSyntaxResult Parse(ReadOnlyMemory<byte> bytes, string extension, PhaseThreeLimits? limits = null, CancellationToken token = default)
    {
        limits ??= new(); ValidateLimits(limits); token.ThrowIfCancellationRequested();
        var format = extension.TrimStart('.').ToUpperInvariant();
        var b = new ChartSyntaxBuilder(format, bytes.Length, limits, token);
        if (bytes.Length > limits.MaximumBytes) { b.Diagnostic("SIZE_LIMIT", "UNKNOWN", "Chart exceeds the bounded byte limit; no syntactic coverage is claimed."); return b.Finish(); }
        b.Bytes = bytes.ToArray(); b.Sha = Convert.ToHexStringLower(SHA256.HashData(b.Bytes));
        if (format is not ("BMS" or "BME" or "BML" or "PMS" or "BMSON")) { b.Diagnostic("FORMAT_UNSUPPORTED", "UNSUPPORTED", "Unsupported chart extension."); return b.Finish(); }
        if (bytes.Length == 0) { b.Diagnostic("EMPTY_CHART", "UNKNOWN", "Empty chart has no supported extraction."); return b.Finish(); }
        Decode(b);
        foreach (var decoding in b.Decodings)
        {
            token.ThrowIfCancellationRequested();
            try { if (format == "BMSON") PhaseThreeBmsonParser.Parse(b, decoding); else PhaseThreeTextParser.Parse(b, decoding); }
            catch (ChartSyntaxLimitException) { b.Diagnostic("FACT_OR_DEPTH_LIMIT", "UNKNOWN", "Extraction limit reached; retained bytes/text contain unprocessed evidence."); break; }
        }
        return b.Finish();
    }
    internal static void ValidateLimits(PhaseThreeLimits limits)
    {
        if (limits.MaximumBytes is < 1 or > 8 * 1024 * 1024 || limits.MaximumLineCharacters is < 1 or > 65536 ||
            limits.MaximumFacts is < 1 or > 50000 || limits.MaximumConditionalDepth is < 1 or > 64 || limits.MaximumJsonDepth is < 1 or > 64)
            throw new ArgumentException("Phase 3 limits must be positive and may only reduce the documented hard limits.");
    }
    private static void Decode(ChartSyntaxBuilder b)
    {
        var bytes = b.Bytes!; var bom = bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 });
        string? utf8 = null, cp932 = null;
        try { utf8 = new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)); }
        catch (DecoderFallbackException) { }
        if (b.Format != "BMSON" && !bom)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try { cp932 = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes); }
            catch (DecoderFallbackException) { }
        }
        if (utf8 is not null)
            b.Decodings.Add(new(0, bom ? "UTF-8-BOM" : "UTF-8", bom ? "Explicit BOM; strict UTF-8" : "Strict UTF-8 candidate; successful decoding does not prove author intent", "OBSERVED", utf8));
        if (cp932 is not null && utf8 != cp932)
            b.Decodings.Add(new(b.Decodings.Count, "CP932", "Strict Windows-932 candidate; successful decoding does not prove author intent", utf8 is null ? "POTENTIAL" : "AMBIGUOUS", cp932));
        if (utf8 is not null && cp932 is not null && utf8 != cp932)
            b.Diagnostic("AMBIGUOUS_ENCODING", "AMBIGUOUS", "Both strict decodings succeed with different text. Both interpretations and their potential resources are retained.");
        if (b.Decodings.Count == 0) b.Diagnostic("DECODING_FAILED", "UNKNOWN", "No supported strict decoding succeeded; original bytes are retained without replacement characters.");
        if (b.Decodings.Any(d => d.Text.Contains('\0'))) b.Diagnostic("NUL_IN_TEXT", "UNKNOWN", "Embedded NUL retained; textual interpretation is uncertain.");
    }
}

internal sealed class ChartSyntaxLimitException : Exception;
internal sealed class ChartSyntaxBuilder(string format, long size, PhaseThreeLimits limits, CancellationToken token)
{
    internal string Format { get; } = format;
    internal PhaseThreeLimits Limits { get; } = limits;
    internal CancellationToken Token { get; } = token;
    internal byte[]? Bytes { get; set; }
    internal string? Sha { get; set; }
    internal List<string> Versions { get; } = [];
    internal List<ChartDecoding> Decodings { get; } = [];
    internal List<ChartResourceDeclaration> Declarations { get; } = [];
    internal List<ChartResourceUsage> Usages { get; } = [];
    internal List<ChartConditionalContext> Contexts { get; } = [];
    internal List<ChartUnknownEvidence> Unknown { get; } = [];
    private readonly List<ChartSyntaxDiagnostic> diagnostics = [];
    private bool partial;
    private int facts;
    internal void Fact() { Token.ThrowIfCancellationRequested(); if (++facts > Limits.MaximumFacts) throw new ChartSyntaxLimitException(); }
    internal void Diagnostic(string code, string state, string message, ChartSourcePosition? source = null)
    {
        partial = true;
        var item = new ChartSyntaxDiagnostic(code, state, message, source);
        if (diagnostics.Count < Limits.MaximumFacts) diagnostics.Add(item);
        else if (code.Contains("LIMIT", StringComparison.Ordinal)) diagnostics[^1] = item;
    }
    internal void Unrecognized(string kind, ChartSourcePosition source)
    { Fact(); Unknown.Add(new(kind, source)); Diagnostic("UNSUPPORTED_SYNTAX", "UNSUPPORTED", "Uninterpreted evidence is retained; dependency completeness cannot be claimed.", source); }
    internal int Declare(string ns, string? id, string kind, string? path, string value, ChartSourcePosition source, int[]? context = null, string state = "OBSERVED")
    {
        Fact(); var number = Declarations.Count;
        Declarations.Add(new(number, ns, id, kind, path, value, state, context ?? [], source));
        if (path is not null && string.IsNullOrWhiteSpace(path)) Diagnostic("EMPTY_RESOURCE_PATH", "UNKNOWN", "Empty resource path is retained.", source);
        return number;
    }
    internal void Use(string ns, string? id, string kind, ChartSourcePosition source, int[]? context = null, string state = "POTENTIAL", string? lane = null, long? pulse = null, long? length = null, bool? continuation = null)
    { Fact(); Usages.Add(new(Usages.Count, ns, id, kind, state, null, context ?? [], source, lane, pulse, length, continuation)); }
    internal ChartSyntaxResult Finish()
    {
        var groups = Declarations.Where(d => d.LogicalId is not null).GroupBy(d => (d.Source.DecodingId, d.Namespace, d.LogicalId)).ToDictionary(g => g.Key, g => g.ToArray());
        var logical = groups.Select((g, i) => new ChartLogicalResource(i, g.Key.DecodingId, g.Key.Namespace, g.Key.LogicalId!, g.Value.Select(d => d.Id).ToArray())).ToArray();
        var groupIds = logical.ToDictionary(l => (l.DecodingId, l.Namespace, (string?)l.LogicalId), l => l.Id);
        foreach (var group in groups.Values.Where(g => g.Length > 1))
        {
            var same = group.Select(d => d.RawValue).Distinct(StringComparer.Ordinal).Count() == 1;
            Diagnostic(same ? "DUPLICATE_DECLARATION" : "CONFLICTING_DECLARATIONS", "AMBIGUOUS", "All declarations retained; no first/last-wins or branch choice is applied.", group[0].Source);
            foreach (var d in group) Declarations[d.Id] = d with { State = "AMBIGUOUS" };
        }
        for (var i = 0; i < Usages.Count; i++)
        {
            var u = Usages[i]; var matches = u.LogicalId is not null && groups.TryGetValue((u.Source.DecodingId, u.Namespace, u.LogicalId), out var g) ? g : [];
            if (matches.Length == 0) Diagnostic(u.LogicalId is null ? "UNRESOLVED_LOGICAL_ID" : "UNDECLARED_LOGICAL_ID", "UNKNOWN", "Logical binding is unproved; no physical missing-file conclusion is made.", u.Source);
            Usages[i] = u with { LogicalResourceId = matches.Length == 0 ? null : groupIds[(u.Source.DecodingId, u.Namespace, u.LogicalId)], State = matches.Length == 0 ? "UNKNOWN" : matches.Length > 1 ? "AMBIGUOUS" : u.State };
        }
        return new(Format, Versions.ToArray(), size, Sha, Bytes, Decodings.ToArray(), Declarations.ToArray(), logical, Usages.ToArray(),
            Contexts.ToArray(), Unknown.ToArray(), diagnostics.ToArray(), partial ? "INCOMPLETE" : "COMPLETE_SUPPORTED_SYNTAX", false, Limits);
    }
}
