namespace BmsLibraryAudit.Core;

public sealed record PhaseThreeLimits(int MaximumBytes = 8 * 1024 * 1024, int MaximumLineCharacters = 65536,
    int MaximumFacts = 50000, int MaximumConditionalDepth = 64, int MaximumJsonDepth = 64);
public sealed record ChartSourcePosition(int DecodingId, int? Line, int? Column, string? JsonPointer, string RawText);
public sealed record ChartDecoding(int Id, string Encoding, string Policy, string State, string Text);
public sealed record ChartResourceDeclaration(int Id, string Namespace, string? LogicalId, string ResourceKind,
    string? WrittenPath, string RawValue, string State, int[] ConditionalContextIds, ChartSourcePosition Source);
public sealed record ChartResourceUsage(int Id, string Namespace, string? LogicalId, string Kind, string State,
    int? LogicalResourceId, int[] ConditionalContextIds, ChartSourcePosition Source,
    string? LaneJson = null, long? Pulse = null, long? Length = null, bool? Continuation = null);
public sealed record ChartLogicalResource(int Id, int DecodingId, string Namespace, string LogicalId, int[] DeclarationIds);
public sealed record ChartConditionalContext(int Id, string Directive, string Argument, int? ParentId, ChartSourcePosition Source);
public sealed record ChartSyntaxDiagnostic(string Code, string State, string Message, ChartSourcePosition? Source);
public sealed record ChartUnknownEvidence(string Kind, ChartSourcePosition Source);
public sealed record ChartSyntaxResult(string Format, string[] FormatVersions, long ByteLength, string? Sha256,
    byte[]? OriginalBytes, ChartDecoding[] Decodings, ChartResourceDeclaration[] Declarations, ChartLogicalResource[] LogicalResources,
    ChartResourceUsage[] Usages, ChartConditionalContext[] ConditionalContexts,
    ChartUnknownEvidence[] UnknownEvidence, ChartSyntaxDiagnostic[] Diagnostics, string SyntaxCoverage,
    bool DependencyClosureProven, PhaseThreeLimits Limits)
{
    public int ExitCode => SyntaxCoverage == "COMPLETE_SUPPORTED_SYNTAX" ? 0 : 2;
}
