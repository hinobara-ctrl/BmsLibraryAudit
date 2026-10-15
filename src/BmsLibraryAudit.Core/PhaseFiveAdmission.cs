using System.Security.Cryptography;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

internal static class PhaseFiveAdmission
{
    internal const string ResolverBuildRevisionState = "DECLARED_UNVERIFIED_BUILD_REVISION";
    internal static PhaseFourResult Verify(PhaseFourResult report, PhaseFiveProvenance accepted,
        PhaseFiveRoot root, string libraryRoot, CancellationToken token)
    {
        if (report.ResultVersion != "phase4-dependency-2" || report.Semantics?.Version != "phase4-semantic-graph-2" ||
            report.Semantics.BoundedProof.Version != "observed-logical-physical-ordinal-binding-evidence-2")
            throw new InvalidOperationException("Unsupported Phase 4 semantic/proof contract; v1 is not ordinal evidence.");
        PhaseFourResolver.ValidateLimits(report.Limits);
        PhaseThreeParser.ValidateLimits(report.VerifiedSyntax.Limits);
        var p = report.Provenance;
        var chart = root.Entries.SingleOrDefault(e => e.EntryId == p.Chart.EntryId && e.IsChart)
            ?? throw new InvalidOperationException("Chart does not belong to the selected observation root.");
        var candidate = Candidate(chart);
        if (p.ContentSessionId != accepted.ContentSessionId || p.ContentDigest != accepted.ContentDigest ||
            p.ParentPreflightId != accepted.ParentPreflightId || p.PreflightDigest != accepted.PreflightDigest ||
            p.ComponentId != accepted.ComponentId || p.ComponentKey != accepted.ComponentKey ||
            p.ObservationRootId != root.Id || p.ObservationRootKey != root.Key || p.ObservationRootPath != root.OriginalPath ||
            p.SourceLibraryRoot != libraryRoot || p.ChartParent != Path.GetDirectoryName(chart.OriginalPath) ||
            p.PackageRoot is not null || p.Chart != candidate || p.ChartSha256 != chart.Sha256 ||
            p.ContentStartedUtc != accepted.ContentStartedUtc || p.ContentCompletedUtc != accepted.ContentCompletedUtc ||
            p.ToolVersion != ReportWriter.ToolVersion || report.DependencyClosureProven || report.RemovalAuthority)
            throw new InvalidOperationException("Phase 4 provenance/authority disagrees with admitted CONTENT.");
        var bytes = report.VerifiedSyntax.OriginalBytes;
        if (bytes is null || bytes.Length > new PhaseThreeLimits().MaximumBytes || bytes.LongLength != chart.Size ||
            Convert.ToHexStringLower(SHA256.HashData(bytes)) != chart.Sha256)
            throw new InvalidOperationException("Included chart bytes are not bound to successful CONTENT.");
        var syntax = PhaseThreeParser.Parse(bytes, Path.GetExtension(chart.OriginalPath), report.VerifiedSyntax.Limits, token);
        if (!Equal(syntax, report.VerifiedSyntax)) throw new InvalidOperationException("Chart syntax does not reproduce from included bytes.");
        var count = root.Entries.Count(e => e.Kind == "REGULAR_FILE");
        var limited = count > report.Limits.MaximumInventoryFiles;
        var inventory = limited ? [] : root.Entries.Where(e => e.Kind == "REGULAR_FILE").Select(Candidate).ToArray();
        // CONTENT seals bind the historical inventory, not the later Phase 4
        // producer executable. ResolverBuildRevision remains a declared hint;
        // reusing it here does not authenticate it or require a current-build match.
        var expected = PhaseFourResolver.AnalyzeVerified(p, syntax, inventory, chart.RelativePath, limited, report.Limits, token);
        if (!Equal(expected, report)) throw new InvalidOperationException("Phase 4 declarations, graph, coverage, models or proof do not reproduce.");
        return expected;
    }
    internal static bool Equal<T>(T a, T b) => JsonElement.DeepEquals(
        JsonSerializer.SerializeToElement(a, PhaseFourResolver.Json), JsonSerializer.SerializeToElement(b, PhaseFourResolver.Json));
    internal static ObservedResourceCandidate Candidate(PhaseFiveEntry e) => new(e.EntryId, e.PhysicalFileId!.Value,
        e.ObservationRootId, e.ComponentId, e.EntryKey, e.OriginalPath, e.RelativePath, e.NativeIdentity!, e.Sha256!, e.Size!.Value, "SEALED_MEMBERSHIP");
}
