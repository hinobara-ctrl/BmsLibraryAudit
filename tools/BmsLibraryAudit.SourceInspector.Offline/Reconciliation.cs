using System.Globalization;

namespace BmsLibraryAudit.SourceInspector.Offline;

public static class Reconciliation
{
    public static OfflineResult Compare(Evidence evidence, ParsedReports reports, long expectedScan, ProcessingPolicy policy,
        SummaryOracle? oracle = null, IReadOnlyDictionary<string, IReadOnlyList<string>>? selectedExact = null,
        CancellationToken cancellation = default)
    {
        policy.Validate();
        Findings? observed = null;
        ScanScopeChange? scopeChange = null;
        try
        {
            var validation = Provenance.Validate(evidence, expectedScan, policy, cancellation);
            scopeChange = validation.ScopeChange;
            var findings = observed = new Findings(policy);
            foreach (var f in validation.Findings) findings.Add(f.Kind, f.Code, f.Detail);
            if (!validation.Valid || validation.Latest?.Statistics is null || validation.Latest.Roots is null ||
                validation.Findings.Any(f => f.Kind == FindingKind.Pending && f.Code != "DATABASE_AND_NATIVE_ORIGIN_NOT_VERIFIED"))
                return new(false, false, findings.Items, null, null) { ScopeChange = validation.ScopeChange, ReportEvidence = reports.Bindings };
            var scan = validation.Latest;
            var ds = evidence.Diagnostics.Where(d => d.ScanId == scan.Id).ToArray();
            var analysis = Relationships.Analyze(evidence.Charts, ds, policy, cancellation);
            policy.Limit(reports.Duplicates.Count > policy.MaxReportRows || reports.Candidates.Count > policy.MaxReportRows || reports.Clusters.Count > policy.MaxReportRows, "REPORT_ROW_LIMIT");
            var current = evidence.Charts.Where(c => c.Present == 1).ToArray();
            var ok = current.Where(c => c.Status == "OK").ToArray();
            var activeRootIds = evidence.Roots.Where(r => r.Active == 1).Select(r => r.Id).ToHashSet();
            var related = analysis.Components.Where(c => c.Kind == "RELATED").ToArray();
            var exact = analysis.Components.Where(c => c.Kind == "EXACT_ONLY").ToArray();
            static Dictionary<int, long> Sizes(Component[] groups) => groups.GroupBy(c => c.Members.Count).ToDictionary(g => g.Key, g => g.LongCount());
            var expected = new Summary("0.1.0", 1, Reports.Phase, scan.Id, scan.StartedUtc, scan.CompletedUtc!, scan.Roots,
                current.LongLength, ok.LongLength, evidence.Charts.LongCount(c => c.Status == "UNVERIFIED" && activeRootIds.Contains(c.RootId)),
                ok.Select(c => c.Sha256).Distinct(StringComparer.Ordinal).LongCount(), ok.Select(c => c.Md5).Distinct(StringComparer.Ordinal).LongCount(),
                current.Select(c => WindowsNames.Canonical(c.Parent)).Distinct(WindowsNames.Comparer).LongCount(), analysis.Folders.Count,
                analysis.IncompleteFolders, analysis.Duplicates.Count, analysis.Candidates.Count,
                Relationships.Labels.ToDictionary(label => label, label => analysis.Candidates.LongCount(c => c.Relationship == label), StringComparer.Ordinal),
                related.LongLength, Sizes(related), exact.LongLength, Sizes(exact), scan.Statistics,
                ds.LongCount(d => d.Severity == "WARNING"), ds.LongCount(d => d.Severity == "ERROR"),
                ds.Select(d => new ReportDiagnostic(d.Severity, d.Code, d.Path, d.Message)).ToArray(), Reports.Notice);
            void Check(bool equal, string code) { if (!equal) findings.Add(FindingKind.Mismatch, code, "Complete supplied report evidence disagrees with independently derived facts."); }
            void SummaryCheck(Summary actual, string prefix)
            {
                cancellation.ThrowIfCancellationRequested();
                policy.Limit(actual.Roots.Count > policy.MaxFolders || actual.IncompleteFolders.Count > policy.MaxFolders || actual.Diagnostics.Count > policy.MaxReportRows,
                    "SUMMARY_COLLECTION_LIMIT");
                foreach (var s in actual.Roots.Concat(actual.IncompleteFolders).Concat(new[] { actual.ToolVersion, actual.Phase, actual.StartedUtc, actual.CompletedUtc, actual.EvidenceNotice })) policy.Text(s);
                foreach (var d in actual.Diagnostics) foreach (var s in new[] { d.Path, d.Code, d.Message, d.Severity }) policy.Text(s);
                Check(actual.ToolVersion == expected.ToolVersion && actual.SchemaVersion == expected.SchemaVersion && actual.Phase == expected.Phase &&
                    actual.ScanId == expected.ScanId && actual.StartedUtc == expected.StartedUtc && actual.CompletedUtc == expected.CompletedUtc &&
                    actual.Roots.SequenceEqual(expected.Roots, StringComparer.Ordinal), prefix + "_IDENTITY");
                Check(actual.Current == expected.Current && actual.Verified == expected.Verified && actual.Unverified == expected.Unverified &&
                    actual.UniqueSha256 == expected.UniqueSha256 && actual.UniqueMd5 == expected.UniqueMd5 && actual.PhysicalFolders == expected.PhysicalFolders &&
                    actual.AnalyzedFolders == expected.AnalyzedFolders && actual.DuplicateGroups == expected.DuplicateGroups && actual.CandidatePairs == expected.CandidatePairs &&
                    actual.RelatedCount == expected.RelatedCount && actual.ExactCount == expected.ExactCount && actual.Warnings == expected.Warnings && actual.Errors == expected.Errors, prefix + "_COUNTS");
                Check(actual.IncompleteFolders.SequenceEqual(expected.IncompleteFolders, StringComparer.Ordinal) && MapEqual(actual.RelationshipCounts, expected.RelationshipCounts) &&
                    MapEqual(actual.RelatedDistribution, expected.RelatedDistribution) && MapEqual(actual.ExactDistribution, expected.ExactDistribution), prefix + "_DISTRIBUTIONS");
                Check(actual.Statistics == expected.Statistics && OrderedDiagnostics(actual.Diagnostics).SequenceEqual(OrderedDiagnostics(expected.Diagnostics)) &&
                    actual.EvidenceNotice == expected.EvidenceNotice, prefix + "_DETAILS");
            }
            SummaryCheck(reports.Summary, "SUMMARY");
            var bound = reports.Bindings;
            var bytesBound = bound?.Complete == true;
            if (!bytesBound)
            {
                findings.Add(FindingKind.Pending, "REPORT_BYTE_BINDING_MISSING", "All four complete parser bindings are required for completed reconciliation.");
                if (bound is not null)
                    foreach (var f in new[] { bound.Summary.Finding, bound.Duplicates.Finding, bound.Candidates.Finding, bound.Clusters.Finding })
                        if (f is not null) findings.Add(f.Kind, f.Code, f.Detail);
            }
            else
            {
                Check(bound!.Summary.Kind == ReportKind.Summary && bound.Duplicates.Kind == ReportKind.Duplicates &&
                    bound.Candidates.Kind == ReportKind.Candidates && bound.Clusters.Kind == ReportKind.Clusters, "REPORT_BINDING_KIND");
                Check(bound.Duplicates.Value!.SequenceEqual(reports.Duplicates) && bound.Candidates.Value!.SequenceEqual(reports.Candidates) &&
                    bound.Clusters.Value!.SequenceEqual(reports.Clusters), "REPORT_ROW_BINDING");
                policy.Limit(reports.SummaryBytes.Length > policy.MaxInputBytes, "INPUT_BYTE_LIMIT");
                Check(bound.Summary.BytesConsumed == reports.SummaryBytes.Length &&
                    bound.Summary.Sha256 == System.Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(reports.SummaryBytes.Span)), "SUMMARY_BYTE_BINDING");
            }
            FormatRecognition? format = null;
            if (reports.SummaryBytes.IsEmpty) findings.Add(FindingKind.Pending, "SUMMARY_BYTES_MISSING", "Exact original-byte evidence is unavailable.");
            else
            {
                var parsed = Reports.ParseSummary(reports.SummaryBytes, policy, cancellation);
                SummaryCheck(parsed, "RAW_SUMMARY");
                format = Reports.RecognizeSummary(reports.SummaryBytes, policy, oracle, cancellation);
                if (!format.ExactOracleMatch) findings.Add(FindingKind.Pending, "SUMMARY_FORMAT_UNPROVEN", format.Reason);
                findings.Add(FindingKind.Pending, "REPORT_EXTERNAL_PROVENANCE_REQUIRED", "A synthetic byte match cannot establish actual producer/native report identity.");
            }
            var expectedDuplicates = analysis.Duplicates.SelectMany(g => g.Locations.Select(c =>
            {
                cancellation.ThrowIfCancellationRequested();
                return new DuplicateRow(g.Sha256, c.Md5!, g.Locations.Count, c.RootId, c.Path, c.Parent, c.Name, c.Extension, c.Size,
                    new DateTime(c.MtimeTicks, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
            }));
            foreach (var d in reports.Duplicates) { cancellation.ThrowIfCancellationRequested(); foreach (var s in new[] { d.Path, d.Parent, d.Filename, d.Extension, d.Sha256, d.Md5, d.MtimeUtc }) policy.Text(s); }
            foreach (var c in reports.Candidates) { cancellation.ThrowIfCancellationRequested(); foreach (var s in new[] { c.FolderA, c.FolderB, c.Relationship }) policy.Text(s); }
            foreach (var c in reports.Clusters) { cancellation.ThrowIfCancellationRequested(); foreach (var s in new[] { c.Kind, c.Id, c.FolderPath }) policy.Text(s); }
            // Complete ordered tuples preserve duplicate multiplicity; neither totals nor sets can hide missing rows.
            Check(reports.Duplicates.SequenceEqual(expectedDuplicates), "DUPLICATE_ROWS");
            Check(reports.Candidates.SequenceEqual(analysis.Candidates), "CANDIDATE_ROWS");
            var expectedClusters = analysis.Components.SelectMany(c => c.Members.Select(m =>
            { cancellation.ThrowIfCancellationRequested(); return new ClusterRow(c.Kind, c.Id, c.Members.Count, m); }));
            Check(reports.Clusters.SequenceEqual(expectedClusters), "CLUSTER_ROWS");
            if (selectedExact is not null)
            {
                policy.Limit(selectedExact.Count > policy.MaxFolders, "SELECTION_LIMIT");
                foreach (var selection in selectedExact)
                {
                    cancellation.ThrowIfCancellationRequested(); policy.Text(selection.Key);
                    policy.Limit(selection.Value.Count > policy.MaxFolders, "SELECTION_LIMIT");
                    foreach (var member in selection.Value) policy.Text(member);
                    var component = exact.SingleOrDefault(c => c.Id == selection.Key);
                    Check(component is not null && selection.Value.SequenceEqual(component.Members, StringComparer.Ordinal), "SELECTED_EXACT_CLOSURE");
                }
            }
            var mismatch = findings.Items.Any(f => f.Kind is FindingKind.Invalid or FindingKind.Mismatch);
            var formatComplete = format?.ExactOracleMatch == true && bytesBound && !mismatch;
            return new(formatComplete, !mismatch && (bound is null || bound.Complete), findings.Items, analysis, format) { ReportEvidence = bound };
        }
        catch (EvidenceException ex) { return Stop(ex.Kind, ex.Code, ex.PriorFindings.Count > 0 ? ex.PriorFindings : observed?.Items ?? []); }
        catch (OverflowException) { return Stop(FindingKind.Invalid, "INTEGER_OVERFLOW", observed?.Items ?? []); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { return Stop(FindingKind.Pending, "CANCELLED", observed?.Items ?? []); }
        OfflineResult Stop(FindingKind kind, string code, IReadOnlyList<Finding> prior) =>
            new(false, false, [.. prior, new(kind, code, "Offline processing stopped; required checks and native proof remain incomplete.")], null, null)
            { ScopeChange = scopeChange, ReportEvidence = reports.Bindings };
    }
    private static bool MapEqual<TKey>(IReadOnlyDictionary<TKey, long> a, IReadOnlyDictionary<TKey, long> b) where TKey : notnull =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var n) && n == p.Value);
    private static IEnumerable<ReportDiagnostic> OrderedDiagnostics(IReadOnlyList<ReportDiagnostic> items) => items.OrderBy(d => d.Path, StringComparer.Ordinal)
        .ThenBy(d => d.Code, StringComparer.Ordinal).ThenBy(d => d.Message, StringComparer.Ordinal).ThenBy(d => d.Severity, StringComparer.Ordinal);
}
