using System.Globalization;
using System.Text;

namespace BmsLibraryAudit.Core;

public static partial class ObservationPairObserver
{
    private sealed record Evidence(PairSource Source, PairSelection Selection, PairPreflight? Preflight, PairContent? Content);
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException("BLOCKED: invalid observation evidence: " + reason); }
    private static void ValidateLimits(ObservationPairLimits limits)
    {
        var upper = new ObservationPairLimits();
        foreach (var property in typeof(ObservationPairLimits).GetProperties()) {
            var value = Convert.ToInt64(property.GetValue(limits), CultureInfo.InvariantCulture);
            if (value < 1 || value > Convert.ToInt64(property.GetValue(upper), CultureInfo.InvariantCulture))
                throw new ArgumentException("Pair limits must be positive and may only reduce the frozen lab caps: " + property.Name);
        }
    }
    private static void Times(string start, string end) => Require(DateTimeOffset.TryParseExact(start, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) &&
        DateTimeOffset.TryParseExact(end, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var b) && a.Offset == TimeSpan.Zero && b.Offset == TimeSpan.Zero && a <= b, "historical timestamps");
    private static void ValidateSource(PairSource source, PairBudget budget)
    {
        Require(source.Profile == ObservationPairDatabase.Profile && source.Contract == ObservationPairDatabase.Contract, "source profile"); ValidateLimits(source.Limits);
        var c = source.Capture; var l = source.Limits; budget.Check();
        Require(c.ScanId > 0 && PhaseOneSourceCapture.Digest(c.Sha256, 64) && c.Facts.Size > 0 && c.Facts.Size <= l.SourceBytes, "source identity/size");
        Times(c.StartedUtc, c.CompletedUtc);
        Require(Path.IsPathFullyQualified(c.OriginalPath) && !string.IsNullOrEmpty(c.Facts.Identity) && c.Roots.Count > 0, "source path/roots");
        Require(c.Charts.Count <= l.SourceCharts && c.Charts.Select(x => PathPolicy.Normalize(x.ParentFolder)).Distinct().Count() <= l.SourceFolders && c.Diagnostics.Count <= l.Diagnostics, "source budgets");
        Require(c.Roots.Select(r => r.Id).Distinct().Count() == c.Roots.Count && c.Roots.Select(r => r.Key).SequenceEqual(c.Roots.Select(r => r.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)), "source root order/identity");
        Require(c.Charts.Select(r => r.NormalizedPath).SequenceEqual(c.Charts.Select(r => r.NormalizedPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)), "source chart order/uniqueness");
        foreach (var r in c.Roots) {
            budget.Check(); Require(r.Id > 0 && Path.IsPathFullyQualified(r.OriginalPath) && r.Key == PathPolicy.Normalize(r.OriginalPath) && r.Active == r.Configured, "captured root");
        }
        foreach (var chart in c.Charts) {
            budget.Check(); var root = c.Roots.SingleOrDefault(r => r.Id == chart.RootId);
            Require(root is not null && Path.IsPathFullyQualified(chart.AbsolutePath) && PathPolicy.Contains(root.OriginalPath, chart.AbsolutePath) &&
                chart.NormalizedPath == PathPolicy.Normalize(chart.AbsolutePath) && chart.ParentFolder == Path.GetDirectoryName(chart.AbsolutePath) &&
                chart.Filename == Path.GetFileName(chart.AbsolutePath) && chart.Extension == Path.GetExtension(chart.AbsolutePath) && PhaseTwoFileSystem.IsChart(chart.AbsolutePath) &&
                (!chart.Present || root.Active && chart.LastSeenScan == c.ScanId) && chart.FirstSeenScan > 0 && chart.FirstSeenScan <= chart.LastSeenScan && chart.LastSeenScan <= c.ScanId &&
                (chart.Status != "OK" || PhaseOneSourceCapture.Digest(chart.Sha256, 64) && PhaseOneSourceCapture.Digest(chart.Md5, 32) && chart.Size >= 0 && chart.MtimeUtcTicks >= 0), "captured chart provenance");
        }
        Require(c.Diagnostics.SequenceEqual(c.Diagnostics.OrderBy(d => d.Path, StringComparer.Ordinal).ThenBy(d => d.Code, StringComparer.Ordinal).ThenBy(d => d.Message, StringComparer.Ordinal).ThenBy(d => d.Severity, StringComparer.Ordinal)), "source diagnostic order");
        foreach (var d in c.Diagnostics) { budget.Check(); Require(d.Severity is "INFO" or "WARNING" or "ERROR" && d.Path is not null && d.Code is not null && d.Message is not null, "source diagnostic"); }
    }
    private static PairSelection Select(PairSource source, string digest, string a, string b, PairBudget budget)
    {
        var keys = new[] { PathPolicy.Normalize(a), PathPolicy.Normalize(b) }.Order(StringComparer.Ordinal).ToArray();
        Require(keys[0] != keys[1] && !PathPolicy.Contains(keys[0], keys[1]) && !PathPolicy.Contains(keys[1], keys[0]), "distinct disjoint selected roots");
        foreach (var d in source.Capture.Diagnostics) {
            budget.Check(); var affected = d.Code switch {
                "DIRECTORY_UNREADABLE" or "REPARSE_SKIPPED" => d.Path,
                "ENTRY_METADATA" or "CHART_READ" or "CHART_UNSTABLE" => Path.GetDirectoryName(d.Path), _ => null };
            if (affected is not null && keys.Any(k => PathPolicy.Contains(affected, k) || PathPolicy.Contains(k, affected)))
                throw new InvalidOperationException("BLOCKED: historical recursive coverage diagnostic intersects selected view.");
        }
        var analysis = RelationshipAnalyzer.Analyze(source.Capture.Charts, source.Capture.Diagnostics); budget.Check();
        var edge = analysis.Candidates.SingleOrDefault(e => new[] { PathPolicy.Normalize(e.FolderA), PathPolicy.Normalize(e.FolderB) }.Order(StringComparer.Ordinal).SequenceEqual(keys));
        Require(edge is not null && edge.Relationship is "A_SUBSET_OF_B" or "B_SUBSET_OF_A", "selection must reproduce a strict direct-chart subset edge");
        if (PathPolicy.Normalize(edge!.FolderA) != keys[0]) edge = new(edge.FolderB, edge.FolderA, edge.PhysicalFilesB, edge.PhysicalFilesA,
            edge.ChartsB, edge.ChartsA, edge.Common, edge.CoverageB, edge.CoverageA, edge.Jaccard, edge.Relationship == "A_SUBSET_OF_B" ? "B_SUBSET_OF_A" : "A_SUBSET_OF_B");
        Require(Recursive(source, keys[0]).Length + Recursive(source, keys[1]).Length <= source.Limits.Charts, "recursive chart budget");
        Require(keys.All(k => Recursive(source, k).All(c => c.Status == "OK")), "historical recursive chart coverage");
        var id = ObservationPairDatabase.Unit(digest, keys[0], keys[1]);
        return new(digest, id, id, ObservationPairDatabase.Policy, keys[0], keys[1], edge);
    }
    private static ChartRecord[] Recursive(PairSource source, string root) => source.Capture.Charts.Where(c => c.Present && PathPolicy.Contains(root, c.AbsolutePath)).ToArray();

    private static void ValidateRead(PhaseTwoContentRead read, PhaseTwoEntry expected, bool success)
    {
        Require(expected.Kind == "REGULAR_FILE" && read.Attempts is >= 1 and <= 2 && read.BytesRead >= 0 && read.BytesRead <= checked(expected.Size!.Value * read.Attempts), "read scope/attempts/bytes");
        Times(read.StartedUtc, read.EndedUtc);
        Require(read.Status is "SUCCESS" or "ERROR" or "UNSTABLE", "read status");
        Require(read.Before == expected && read.After == expected, "historical read before/after native facts");
        if (success || read.Status == "SUCCESS") Require(read.Status == "SUCCESS" && PhaseOneSourceCapture.Digest(read.Sha256, 64) && read.BytesRead >= expected.Size, "full successful read");
        else Require(read.Sha256 is null, "failed read cannot supply a digest");
    }
    private static void ValidatePreflight(PairSource source, PairSelection selection, PairPreflight pf, bool complete, PairBudget budget)
    {
        Require(pf.SourceDigest == selection.SourceDigest && pf.UnitId == selection.UnitId, "preflight parent/unit"); Times(pf.StartedUtc, pf.EndedUtc);
        Require(pf.State is "COMPLETE" or "BLOCKED" or "STALE" or "INCOMPLETE" or "CANCELLED" or "FAILED", "preflight terminal state");
        var l = source.Limits; Require(pf.Entries.Length <= l.Entries && pf.Charts.Length <= l.Charts && pf.Diagnostics.Length <= l.Diagnostics + 1, "preflight budgets");
        Require(pf.Roots.Length is 0 or 2 && (!complete || pf.Roots.Length == 2), "preflight root cardinality");
        if (pf.Roots.Length == 2) {
            Require(pf.Roots[0].Id == 1 && pf.Roots[1].Id == 2 && pf.Roots[0].Key == selection.RootA && pf.Roots[1].Key == selection.RootB &&
                pf.Roots[0].OriginalPath == selection.Discovery.FolderA && pf.Roots[1].OriginalPath == selection.Discovery.FolderB, "preflight roots");
            if (complete) ValidateRootBindings(pf.Roots); // Pure comparison of stored facts; never opens roots.
        }
        Require(pf.Entries.Select(e => e.Id).SequenceEqual(Enumerable.Range(1, pf.Entries.Length).Select(i => (long)i)) &&
            pf.Entries.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count() == pf.Entries.Length &&
            pf.Entries.SequenceEqual(pf.Entries.OrderBy(e => e.RootId).ThenBy(e => e.RelativePath, StringComparer.Ordinal)), "entry identity/order/uniqueness");
        long bytes = 0; var files = 0;
        foreach (var entry in pf.Entries) {
            budget.Check(); var root = pf.Roots.SingleOrDefault(r => r.Id == entry.RootId); Require(root is not null, "entry root membership");
            var f = entry.Facts;
            Require(Path.IsPathFullyQualified(f.OriginalPath) && entry.Key == PathPolicy.Normalize(f.OriginalPath) && PathPolicy.Contains(root!.OriginalPath, f.OriginalPath) &&
                entry.RelativePath == Path.GetRelativePath(root.OriginalPath, f.OriginalPath) && entry.RelativePath.Split(Path.DirectorySeparatorChar).Length <= l.Depth + 1, "ordinal relative membership/path");
            Require(f.Readability == "NOT_TESTED", "preflight must not claim resource readability");
            if (complete) Require(f.Status == "OK" && f.Kind is "DIRECTORY" or "REGULAR_FILE" && f.NativeIdentity is not null && f.MtimeTicks >= 0 && f.Attributes >= 0 &&
                ((f.Attributes & (long)(FileAttributes.ReparsePoint | FileAttributes.Device)) == 0) &&
                ((f.Attributes & (long)FileAttributes.Directory) != 0) == (f.Kind == "DIRECTORY"), "complete entry facts");
            if (f.Kind == "REGULAR_FILE") {
                files++; Require(f.Size >= 0 && f.Size <= l.FileBytes && f.IsChart == PhaseTwoFileSystem.IsChart(f.OriginalPath) && f.Extension == Path.GetExtension(f.OriginalPath), "regular-file facts"); bytes = checked(bytes + f.Size!.Value);
            } else if (f.Kind == "DIRECTORY") Require(f.Size is null && f.IsChart is null && f.Extension is null, "directory facts");
            if (complete && entry.RelativePath != ".") Require(pf.Entries.Any(e => e.RootId == root!.Id && e.Facts.OriginalPath == Path.GetDirectoryName(f.OriginalPath) && e.Facts.Kind == "DIRECTORY"), "directory ancestry coverage");
        }
        Require(files <= l.Files && bytes <= l.TotalBytes, "preflight file/byte caps");
        Require(pf.VerifiedRoots.Distinct().Count() == pf.VerifiedRoots.Length && pf.VerifiedRoots.SequenceEqual(pf.VerifiedRoots.Order()) && pf.VerifiedRoots.All(id => pf.Roots.Any(r => r.Id == id)), "verified root coverage");
        if (complete) Require(pf.VerifiedRoots.SequenceEqual(new[] { 1, 2 }) && pf.Diagnostics.Length == 0, "complete preflight verification");
        Require(pf.Charts.Select(c => c.EntryId).SequenceEqual(pf.Charts.Select(c => c.EntryId).Distinct().Order()), "chart read identity/order");
        foreach (var chart in pf.Charts) {
            budget.Check(); var entry = pf.Entries.SingleOrDefault(e => e.Id == chart.EntryId); Require(entry is not null && entry.Facts.IsChart == true, "chart entry");
            var original = source.Capture.Charts.SingleOrDefault(c => c.Present && c.NormalizedPath == entry!.Key);
            Require(original is not null && original.Status == "OK" && chart.SourceSha256 == original.Sha256 && chart.Direct == (PathPolicy.Normalize(original.ParentFolder) == pf.Roots.Single(r => r.Id == entry!.RootId).Key), "chart source/direct binding");
            ValidateRead(chart.Read, entry!.Facts, complete);
            Require(DateTimeOffset.Parse(chart.Read.StartedUtc, CultureInfo.InvariantCulture) >= DateTimeOffset.Parse(pf.StartedUtc, CultureInfo.InvariantCulture) &&
                DateTimeOffset.Parse(chart.Read.EndedUtc, CultureInfo.InvariantCulture) <= DateTimeOffset.Parse(pf.EndedUtc, CultureInfo.InvariantCulture), "preflight read time scope");
            if (complete) Require(chart.Read.Sha256 == original!.Sha256, "revalidated chart digest");
        }
        if (complete) foreach (var root in pf.Roots) {
            var rootEntry = pf.Entries.SingleOrDefault(e => e.RootId == root.Id && e.RelativePath == ".");
            Require(rootEntry is not null && rootEntry.Facts.Kind == "DIRECTORY" && rootEntry.Facts.NativeIdentity == root.Facts.Identity && rootEntry.Facts.MtimeTicks == root.Facts.MtimeTicks && rootEntry.Facts.Attributes == root.Facts.Attributes, "root native binding");
            var expected = Recursive(source, root.Key); var actual = pf.Entries.Where(e => e.RootId == root.Id && e.Facts.IsChart == true).ToArray();
            Require(expected.Select(c => c.AbsolutePath).Order(StringComparer.Ordinal).SequenceEqual(actual.Select(e => e.Facts.OriginalPath).Order(StringComparer.Ordinal)) && actual.All(e => pf.Charts.Any(c => c.EntryId == e.Id)), "complete recursive chart names/reads");
        }
        Require(PairJson.Equal(pf.Outcomes, PreflightOutcomes(pf.Roots, pf.Entries, pf.Charts, pf.VerifiedRoots, pf.State)), "preflight root outcomes");
    }
    private static void ValidateContent(PairPreflight pf, PairContent c, bool complete, ObservationPairLimits limits, PairBudget budget, CancellationToken token)
    {
        Require(c.UnitId == pf.UnitId && c.State is "COMPLETE" or "BLOCKED" or "STALE" or "INCOMPLETE" or "CANCELLED" or "FAILED", "content unit/state"); Times(c.StartedUtc, c.EndedUtc);
        Require(c.Reads.Length <= limits.Files && c.Diagnostics.Length <= limits.Diagnostics + 1 && c.Reads.Select(r => r.EntryId).SequenceEqual(c.Reads.Select(r => r.EntryId).Distinct().Order()), "content reads order/coverage limits");
        long bytes = 0;
        foreach (var r in c.Reads) {
            budget.Check(); var entry = pf.Entries.SingleOrDefault(e => e.Id == r.EntryId); Require(entry is not null && entry.Facts.Kind == "REGULAR_FILE", "content membership");
            ValidateRead(r.Read, entry!.Facts, complete); bytes = checked(bytes + r.Read.BytesRead);
            Require(DateTimeOffset.Parse(r.Read.StartedUtc, CultureInfo.InvariantCulture) >= DateTimeOffset.Parse(c.StartedUtc, CultureInfo.InvariantCulture) &&
                DateTimeOffset.Parse(r.Read.EndedUtc, CultureInfo.InvariantCulture) <= DateTimeOffset.Parse(c.EndedUtc, CultureInfo.InvariantCulture), "content read time scope");
            if (entry.Facts.IsChart == true && r.Read.Status == "SUCCESS" && complete) Require(r.Read.Sha256 == pf.Charts.Single(e => e.EntryId == r.EntryId).Read.Sha256, "content/preflight chart hash binding");
        }
        Require(bytes <= limits.AttemptBytes, "content attempted byte budget");
        Require(c.VerifiedRoots.SequenceEqual(c.VerifiedRoots.Distinct().Order()) && c.VerifiedRoots.All(id => id is 1 or 2), "content verified views");
        Require(PairJson.Equal(c.Outcomes, ContentOutcomes(pf, c.Reads, c.VerifiedRoots, c.State)), "content root outcomes");
        if (complete) {
            Require(c.VerifiedRoots.SequenceEqual(new[] { 1, 2 }) && c.Outcomes.All(o => o.State == "COMPLETE") && c.Diagnostics.Length == 0 &&
                c.Reads.Select(r => r.EntryId).SequenceEqual(pf.Entries.Where(e => e.Facts.Kind == "REGULAR_FILE").Select(e => e.Id)), "complete content coverage");
            Require(c.Comparison is not null && PairJson.Equal(c.Comparison, Compare(pf, c.Reads, token)), "reconstructed counts/flags/differences/groups/subsets/conflicts/charts");
        } else Require(c.Comparison is null, "incomplete pair cannot carry a comparison");
    }
    private static PairPhysicalComparison Compare(PairPreflight pf, PairRead[] reads, CancellationToken token)
    {
        var hashes = reads.ToDictionary(r => r.EntryId, r => r.Read.Sha256);
        PhaseTwoContentComparison.Node[] View(int root) => pf.Entries.Where(e => e.RootId == root).Select(e => new PhaseTwoContentComparison.Node(e.Id, e.RelativePath,
            e.Facts.Kind, hashes.GetValueOrDefault(e.Id), e.Facts.IsChart == true)).ToArray();
        var a = View(1); var b = View(2); var derived = PhaseTwoContentComparison.Derive(a, b, token);
        PhysicalPath[] Physical(int root) => pf.Entries.Where(e => e.RootId == root).Select(e => new PhysicalPath(e.Id, root, e.RelativePath, e.Facts.Kind,
            hashes.GetValueOrDefault(e.Id), e.Facts.NativeIdentity, e.Facts.IsChart == true)).ToArray();
        bool Subset(bool left) => !derived.Flags[0] && derived.Entries.All(d => d.Category is "SAME_FILE" or "SAME_DIRECTORY" ||
            d.Category == (left ? "FILE_ONLY_B" : "FILE_ONLY_A") || d.Category == (left ? "DIRECTORY_ONLY_B" : "DIRECTORY_ONLY_A"));
        var ca = a.Where(n => n.Chart).ToArray(); var cb = b.Where(n => n.Chart).ToArray();
        var ha = ca.Select(n => n.Digest!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(); var hb = cb.Select(n => n.Digest!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var structure = new List<string> { "PACKAGE_BOUNDARY_UNKNOWN" };
        if (pf.Entries.Where(e => e.Facts.IsChart == true).GroupBy(e => e.RootId).Any(g => g.Select(e => Path.GetDirectoryName(e.RelativePath)).Distinct(StringComparer.Ordinal).Skip(1).Any())) structure.Add("CONTAINER_DIRECTORY_CANDIDATE");
        return new(derived.Counts, derived.Flags, derived.Entries, derived.Groups, Subset(true), Subset(false),
            PhysicalConflictAnnotations.Derive(Physical(1), Physical(2), derived.Entries, token).ToArray(),
            ha.Intersect(hb, StringComparer.Ordinal).ToArray(), ha.Except(hb, StringComparer.Ordinal).ToArray(), hb.Except(ha, StringComparer.Ordinal).ToArray(), ca.Length, cb.Length, structure.ToArray());
    }
    private static Evidence Load(string path, string stage, string accepted, CancellationToken token)
    {
        try { return LoadCore(path, stage, accepted, token); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NullReferenceException or KeyNotFoundException or FormatException or OverflowException)
        { throw new InvalidOperationException("BLOCKED: malformed typed observation evidence.", ex); }
    }
    private static Evidence LoadCore(string path, string stage, string accepted, CancellationToken token)
    {
        Require(stage is "SOURCE" or "PREFLIGHT" or "CONTENT" && PhaseOneSourceCapture.Digest(accepted, 64), "stage/accepted digest");
        if (new FileInfo(path).Length > new ObservationPairLimits().DatabaseBytes) throw new InvalidOperationException("Pair DB exceeds lab limit.");
        using var db = ObservationPairDatabase.Open(path); db.BeginRead();
        var sr = db.Read("SOURCE") ?? throw new InvalidOperationException("Missing SOURCE.");
        var source = PairJson.Parse<PairSource>(sr.Payload); var budget = new PairBudget(source.Limits, token); ValidateSource(source, budget);
        Require(Encoding.UTF8.GetByteCount(sr.Payload) <= source.Limits.PayloadBytes && new FileInfo(path).Length <= source.Limits.DatabaseBytes, "source/evidence serialized budgets");
        Require(sr.State == "COMPLETE" && sr.Parent is null && sr.Digest == ObservationPairDatabase.Seal("SOURCE", sr.Payload) && sr.Payload == PairJson.Text(source), "canonical SOURCE seal");
        var selected = db.Read("SELECTION") ?? throw new InvalidOperationException("Missing SELECTION.");
        var selection = PairJson.Parse<PairSelection>(selected.Payload); var reproduced = Select(source, sr.Digest!, selection.RootA, selection.RootB, budget);
        Require(selected.State == "COMPLETE" && selected.Parent == sr.Digest && selected.Digest == selection.UnitId && PairJson.Equal(selection, reproduced) && selected.Payload == PairJson.Text(selection), "selection identity/provenance");
        PairPreflight? pf = null; PairContent? content = null; var target = sr;
        if (stage != "SOURCE") {
            var pr = db.Read("PREFLIGHT") ?? throw new InvalidOperationException("Missing PREFLIGHT."); pf = PairJson.Parse<PairPreflight>(pr.Payload);
            Require(Encoding.UTF8.GetByteCount(pr.Payload) <= source.Limits.PayloadBytes, "preflight serialized budget");
            Require(pr.Parent == sr.Digest && pr.State == "COMPLETE" && pf.State == pr.State && pf.SourceDigest == pr.Parent && pr.Digest == ObservationPairDatabase.Seal("PREFLIGHT", pr.Payload) && pr.Payload == PairJson.Text(pf), "canonical complete PREFLIGHT seal");
            ValidatePreflight(source, selection, pf, true, budget); target = pr;
            if (stage == "CONTENT") {
                var cr = db.Read("CONTENT") ?? throw new InvalidOperationException("Missing CONTENT."); content = PairJson.Parse<PairContent>(cr.Payload);
                Require(Encoding.UTF8.GetByteCount(cr.Payload) <= source.Limits.PayloadBytes, "content serialized budget");
                Require(cr.Parent == pr.Digest && cr.State == "COMPLETE" && content.State == cr.State && content.PreflightDigest == pr.Digest && cr.Digest == ObservationPairDatabase.Seal("CONTENT", cr.Payload) && cr.Payload == PairJson.Text(content), "canonical complete CONTENT seal");
                ValidateContent(pf, content, true, source.Limits, budget, token); target = cr;
            }
        }
        Require(target.Digest == accepted, "externally accepted manifest"); db.RequireBinding(); budget.Check(); return new(source, selection, pf, content);
    }
    public static ObservationPairResult Verify(string databasePath, string stage, string acceptedManifest, CancellationToken token = default)
    {
        var e = Load(databasePath, stage, acceptedManifest, token);
        return Result(e.Source, e.Selection, stage, "COMPLETE", acceptedManifest);
    }
    public static ObservationPairResult Export(string databasePath, string stage, string acceptedManifest, string outputDirectory, CancellationToken token = default)
    {
        var e = Load(databasePath, stage, acceptedManifest, token);
        return PublishLoaded(databasePath, PathPolicy.Absolute(outputDirectory), e.Source, e.Selection, e.Preflight, e.Content, stage, acceptedManifest, token, null);
    }
    private static ObservationPairResult Result(PairSource s, PairSelection selection, string stage, string state, string? digest) =>
        new(stage, state, digest, selection.UnitId, s.Capture.Diagnostics.Count > 0 || state != "COMPLETE" ? "INCOMPLETE" : "COMPLETE",
            stage == "CONTENT" && state == "COMPLETE" ? "COMPLETE" : "BLOCKED");
    private static ObservationPairResult PublishLoaded(string db, string output, PairSource source, PairSelection selection,
        PairPreflight? pf, PairContent? content, string stage, string? digest, CancellationToken token, PhaseThreePublicationTestHooks? hooks,
        PhaseFourResolver.Destination? acquisitionDestination = null)
    {
        var state = stage == "SOURCE" ? "COMPLETE" : stage == "PREFLIGHT" ? pf!.State : content!.State;
        if (digest is not null) _ = Load(db, stage, digest, token);
        var result = Result(source, selection, stage, state, digest);
        var bytes = Encoding.UTF8.GetBytes(PairJson.Text(new { Contract = ObservationPairDatabase.Contract, Profile = ObservationPairDatabase.Profile,
            Result = result, Source = source, Selection = selection, Preflight = pf, Content = content,
            Semantics = "SEMANTIC_EVIDENCE_NOT_EVALUATED", PackageBoundary = "PACKAGE_BOUNDARY_UNKNOWN",
            CurrentFreshness = "UNKNOWN", FunctionalEquivalence = "UNKNOWN", CodecUsability = "UNKNOWN", ResourceNecessity = "UNKNOWN",
            DependencyClosureProven = false, PackageEquivalenceProven = false, MergeAuthority = false, RemovalAuthority = false,
            Limitations = new[] { "Historical read observations, not a simultaneous snapshot or current freshness proof.",
                "Digests prove accepted consistency, not independent producer authentication.", "No Phase 4/5 adapter; no semantic resource binding or package proof.",
                "Cooperative lab budgets and native guards do not establish universal race freedom or real-scale acceptance." } }));
        if (bytes.Length > source.Limits.ReportBytes) throw new InvalidOperationException("FAILED_EXPORT: report byte budget; evidence is unchanged.");
        var destination = new PhaseFourResolver.Destination(output, source.Capture.Roots.Select(r => r.OriginalPath).ToArray(), [source.Capture.OriginalPath, db]);
        void Validate(string? temporary, bool empty) { acquisitionDestination?.Validate(temporary, empty); destination.Validate(temporary, empty); }
        ObservedReportPublication.Publish(output, "observation_pair.json", bytes, Validate, token, hooks); return result;
    }
}
