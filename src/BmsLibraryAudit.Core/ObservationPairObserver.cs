using System.Globalization;
using System.Text;

namespace BmsLibraryAudit.Core;

public static partial class ObservationPairObserver
{
    internal sealed class Hooks
    {
        internal Action? BeforeVerification { get; init; }
        internal Action? BeforeSeal { get; init; }
        internal Action<string>? AfterRead { get; init; }
        internal PhaseThreePublicationTestHooks? Publication { get; init; }
    }
    public static ObservationPairResult Capture(ObservationPairCaptureOptions options,
        ObservationPairLimits? limits = null, CancellationToken token = default) => CaptureCore(options, limits ?? new(), token);
    internal static ObservationPairResult CaptureCore(ObservationPairCaptureOptions options, ObservationPairLimits limits, CancellationToken token)
    {
        ValidateLimits(limits); var budget = new PairBudget(limits, token); budget.Check();
        var path = PathPolicy.Absolute(options.DatabasePath); var output = PathPolicy.Absolute(options.OutputDirectory);
        using var writer = PhaseTwoWriter.Acquire(path);
        if (PhaseTwoSafety.Exists(path)) throw new InvalidOperationException("BLOCKED: pair database must be new.");
        if (new FileInfo(options.SourceDatabasePath).Length > limits.SourceBytes) throw new InvalidOperationException("BLOCKED: source byte budget.");
        var capture = PhaseOneSourceCapture.Capture(new(options.SourceDatabasePath, options.SourceScanId, path, output), null, token);
        budget.Check();
        var source = new PairSource(ObservationPairDatabase.Profile, ObservationPairDatabase.Contract, PhaseTwoBuildProvenance.Current.EvidenceRevision, limits, capture);
        ValidateSource(source, budget);
        var payload = PairJson.Text(source); var digest = ObservationPairDatabase.Seal("SOURCE", payload);
        var selection = Select(source, digest, options.RootA, options.RootB, budget);
        // Bind root identities before creating the new owned database. No payload reads.
        var roots = BindRoots(selection); ValidateRootBindings(roots);
        var destination = new PhaseFourResolver.Destination(output, capture.Roots.Select(r => r.OriginalPath).ToArray(), [capture.OriginalPath]);
        destination.Validate(null, true); budget.Check();
        if (Encoding.UTF8.GetByteCount(payload) > limits.PayloadBytes || Encoding.UTF8.GetByteCount(PairJson.Text(selection)) > limits.PayloadBytes ||
            Encoding.UTF8.GetByteCount(payload) + Encoding.UTF8.GetByteCount(PairJson.Text(selection)) + 65536 > limits.DatabaseBytes)
            throw new InvalidOperationException("BLOCKED: source/selection serialization exceeds lab budget.");
        using (var db = ObservationPairDatabase.Open(path, true, true)) {
            db.Put("SOURCE", "COMPLETE", null, digest, payload, limits);
            db.Put("SELECTION", "COMPLETE", digest, selection.UnitId, PairJson.Text(selection), limits);
        }
        var admitted = Load(path, "SOURCE", digest, token);
        return PublishLoaded(path, output, admitted.Source, admitted.Selection, null, null, "SOURCE", digest, token, null, destination);
    }

    public static ObservationPairResult Preflight(string databasePath, string acceptedSource, string outputDirectory,
        CancellationToken token = default) => PreflightCore(databasePath, acceptedSource, outputDirectory, new PhaseTwoFileSystem(), null, token);
    internal static ObservationPairResult PreflightCore(string databasePath, string acceptedSource, string outputDirectory,
        IPhaseTwoContentFileSystem files, Hooks? hooks, CancellationToken token)
    {
        using var writer = PhaseTwoWriter.Acquire(databasePath);
        var evidence = Load(databasePath, "SOURCE", acceptedSource, token); var s = evidence.Source; var selection = evidence.Selection;
        var output = PathPolicy.Absolute(outputDirectory);
        var destination = ValidateWritableDestination(databasePath, output, s);
        using var db = ObservationPairDatabase.Open(databasePath, true);
        if (db.Read("PREFLIGHT") is not null) throw new InvalidOperationException("BLOCKED: preflight attempt already exists; no resume.");
        var budget = new PairBudget(s.Limits, token); var start = Now(); string state = "COMPLETE";
        PairRoot[] roots = []; PairEntry[] entries = []; var charts = new List<PairChart>(); var verified = new List<int>(); var diagnostics = new List<Diagnostic>();
        try {
            budget.Check(); roots = BindRoots(selection); ValidateRootBindings(roots);
            var inventory = Inventory(roots, files, s.Limits, budget, diagnostics, token); entries = inventory.Entries;
            foreach (var root in roots) {
                budget.Check(); if (!inventory.Complete.Contains(root.Id)) continue;
                var expected = Recursive(s, root.Key); var actual = entries.Where(e => e.RootId == root.Id && e.Facts.IsChart == true).ToArray();
                if (!expected.Select(c => c.AbsolutePath).Order(StringComparer.Ordinal).SequenceEqual(actual.Select(e => e.Facts.OriginalPath).Order(StringComparer.Ordinal), StringComparer.Ordinal))
                    throw new PairStopException("STALE", "RECURSIVE_CHART_MEMBERSHIP_CHANGED", root.OriginalPath);
                if (charts.Count + actual.Length > s.Limits.Charts) throw new PairStopException("INCOMPLETE", "CHART_BUDGET_EXCEEDED", root.OriginalPath);
                foreach (var entry in actual) {
                    budget.Check(); var old = expected.Single(c => c.NormalizedPath == entry.Key);
                    var read = files.ReadContent(entry.Facts, new byte[65536], token); budget.AddBytes(read.BytesRead);
                    ValidateRead(read, entry.Facts, false);
                    charts.Add(new(entry.Id, old.Sha256!, PathPolicy.Normalize(old.ParentFolder) == root.Key, read)); hooks?.AfterRead?.Invoke(entry.Facts.OriginalPath);
                    if (read.Status != "SUCCESS") throw new PairStopException("INCOMPLETE", "CHART_READ_INCOMPLETE", entry.Facts.OriginalPath);
                    if (read.Sha256 != old.Sha256) throw new PairStopException("STALE", "CHART_BYTES_CHANGED", entry.Facts.OriginalPath);
                }
            }
            hooks?.BeforeVerification?.Invoke();
            verified.AddRange(VerifyInventory(roots, entries, files, s.Limits, budget, inventory.Complete, token));
            if (inventory.Complete.Length != 2) state = "INCOMPLETE";
            hooks?.BeforeSeal?.Invoke(); budget.Check();
        } catch (Exception ex) when (AcquisitionError(ex)) { state = Failure(ex); diagnostics.Add(DiagnosticFor(ex)); }
        if (state != "COMPLETE" && diagnostics.Count == 0) diagnostics.Add(new("ERROR", "INVENTORY_INCOMPLETE", "", "No complete pair comparison is admitted."));
        var pf = new PairPreflight(acceptedSource, selection.UnitId, state, start, Now(), roots, entries,
            charts.OrderBy(c => c.EntryId).ToArray(), verified.Order().ToArray(), PreflightOutcomes(roots, entries, charts, verified, state), diagnostics.ToArray());
        var text = PairJson.Text(pf);
        if (!Fits(db, text, s.Limits)) {
            state = "INCOMPLETE"; pf = pf with { State = state, Roots = [], Entries = [], Charts = [], VerifiedRoots = [], Outcomes = [], Diagnostics = [new("ERROR", "EVIDENCE_BUDGET_EXCEEDED", "", "Unpublished materialization omitted; no complete coverage claim.")] };
            text = PairJson.Text(pf);
        }
        var digest = state == "COMPLETE" ? ObservationPairDatabase.Seal("PREFLIGHT", text) : null;
        ValidatePreflight(s, selection, pf, state == "COMPLETE", new(s.Limits, CancellationToken.None));
        db.Put("PREFLIGHT", state, acceptedSource, digest, text, s.Limits);
        db.RequireBinding();
        db.Dispose();
        // Publication is a separate failure boundary; a sealed stage is never rewritten.
        return PublishLoaded(databasePath, output, s, selection, pf, null, "PREFLIGHT", digest, token, hooks?.Publication, destination);
    }

    public static ObservationPairResult Hash(string databasePath, string acceptedPreflight, string outputDirectory,
        CancellationToken token = default) => HashCore(databasePath, acceptedPreflight, outputDirectory, new PhaseTwoFileSystem(), null, token);
    internal static ObservationPairResult HashCore(string databasePath, string acceptedPreflight, string outputDirectory,
        IPhaseTwoContentFileSystem files, Hooks? hooks, CancellationToken token)
    {
        using var writer = PhaseTwoWriter.Acquire(databasePath);
        var evidence = Load(databasePath, "PREFLIGHT", acceptedPreflight, token); var s = evidence.Source; var selection = evidence.Selection; var pf = evidence.Preflight!;
        var output = PathPolicy.Absolute(outputDirectory); var destination = ValidateWritableDestination(databasePath, output, s);
        using var db = ObservationPairDatabase.Open(databasePath, true);
        if (db.Read("CONTENT") is not null) throw new InvalidOperationException("BLOCKED: content attempt already exists; no resume.");
        var budget = new PairBudget(s.Limits, token); var start = Now(); string state = "COMPLETE";
        var reads = new List<PairRead>(); var verified = new List<int>(); var diagnostics = new List<Diagnostic>(); PairPhysicalComparison? comparison = null;
        try {
            budget.Check(); VerifyInventory(pf.Roots, pf.Entries, files, s.Limits, budget, [1, 2], token);
            var buffer = new byte[65536];
            foreach (var entry in pf.Entries.Where(e => e.Facts.Kind == "REGULAR_FILE")) {
                budget.Check(); var read = files.ReadContent(entry.Facts, buffer, token); budget.AddBytes(read.BytesRead);
                ValidateRead(read, entry.Facts, false); reads.Add(new(entry.Id, read));
                if (entry.Facts.IsChart == true && read.Status == "SUCCESS" && read.Sha256 != pf.Charts.Single(c => c.EntryId == entry.Id).Read.Sha256)
                    throw new PairStopException("STALE", "CHART_BYTES_CHANGED", entry.Facts.OriginalPath);
                if (read.Status != "SUCCESS") diagnostics.Add(new("ERROR", "CONTENT_READ_" + read.Status, entry.Facts.OriginalPath, "Regular path has no accepted complete hash."));
                if (diagnostics.Count > s.Limits.Diagnostics) throw new PairStopException("INCOMPLETE", "DIAGNOSTIC_BUDGET_EXCEEDED");
                hooks?.AfterRead?.Invoke(entry.Facts.OriginalPath);
            }
            hooks?.BeforeVerification?.Invoke(); verified.AddRange(VerifyInventory(pf.Roots, pf.Entries, files, s.Limits, budget, [1, 2], token));
            if (reads.Any(r => r.Read.Status != "SUCCESS")) state = "INCOMPLETE";
            else comparison = Compare(pf, reads.ToArray(), token);
            hooks?.BeforeSeal?.Invoke(); budget.Check();
        } catch (Exception ex) when (AcquisitionError(ex)) { state = Failure(ex); diagnostics.Add(DiagnosticFor(ex)); comparison = null; }
        var content = new PairContent(acceptedPreflight, selection.UnitId, state, start, Now(), reads.ToArray(), verified.Order().ToArray(),
            ContentOutcomes(pf, reads, verified, state), comparison, diagnostics.ToArray());
        ValidateContent(pf, content, state == "COMPLETE", s.Limits, new(s.Limits, CancellationToken.None), CancellationToken.None);
        var text = PairJson.Text(content);
        if (!Fits(db, text, s.Limits)) {
            state = "INCOMPLETE"; content = content with { State = state, Reads = [], VerifiedRoots = [], Comparison = null,
                Outcomes = ContentOutcomes(pf, [], [], state), Diagnostics = [new("ERROR", "EVIDENCE_BUDGET_EXCEEDED", "", "Unpublished materialization omitted; no complete coverage claim.")] };
            text = PairJson.Text(content);
        }
        var digest = state == "COMPLETE" ? ObservationPairDatabase.Seal("CONTENT", text) : null;
        db.Put("CONTENT", state, acceptedPreflight, digest, text, s.Limits);
        db.Dispose();
        return PublishLoaded(databasePath, output, s, selection, pf, content, "CONTENT", digest, token, hooks?.Publication, destination);
    }
    private static bool Fits(ObservationPairDatabase db, string text, ObservationPairLimits limits) =>
        Encoding.UTF8.GetByteCount(text) <= limits.PayloadBytes && new FileInfo(db.Path).Length + Encoding.UTF8.GetByteCount(text) + 65536 <= limits.DatabaseBytes;
    private static PhaseFourResolver.Destination ValidateWritableDestination(string db, string output, PairSource source)
    {
        PhaseTwoSafety.ValidateDestinations(source.Capture.OriginalPath, db, output, source.Capture.Roots.Select(r => r.OriginalPath));
        var destination = new PhaseFourResolver.Destination(output, source.Capture.Roots.Select(r => r.OriginalPath).ToArray(), [source.Capture.OriginalPath, db]);
        destination.Validate(null, true); return destination;
    }
    private static PairRoot[] BindRoots(PairSelection selection) => new[] { selection.RootA, selection.RootB }
        .Select((key, i) => new PairRoot(i + 1, key, i == 0 ? selection.Discovery.FolderA : selection.Discovery.FolderB,
            PhaseTwoSafety.Inspect(i == 0 ? selection.Discovery.FolderA : selection.Discovery.FolderB))).ToArray();
    private static void ValidateRootBindings(PairRoot[] roots)
    {
        if (roots.Any(r => (r.Facts.Attributes & (uint)FileAttributes.Directory) == 0) || roots[0].Facts.Identity == roots[1].Facts.Identity ||
            PathPolicy.Contains(roots[0].OriginalPath, roots[1].OriginalPath) || PathPolicy.Contains(roots[1].OriginalPath, roots[0].OriginalPath) ||
            WithinNative(roots[0].Facts.ResolvedPath, roots[1].Facts.ResolvedPath) || WithinNative(roots[1].Facts.ResolvedPath, roots[0].Facts.ResolvedPath))
            throw new PairStopException("BLOCKED", "OVERLAPPING_OR_ALIASED_ROOTS");
    }
    private static bool WithinNative(string parent, string path) => path == parent || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.Ordinal);
    private sealed record InventoryResult(PairEntry[] Entries, int[] Complete);
    private static InventoryResult Inventory(PairRoot[] roots, IPhaseTwoContentFileSystem files, ObservationPairLimits limits,
        PairBudget budget, List<Diagnostic> diagnostics, CancellationToken token)
    {
        var entries = new List<PairEntry>(); var complete = new List<int>(); long total = 0; var regular = 0;
        foreach (var root in roots) {
            try { Walk(root.OriginalPath, 0); complete.Add(root.Id); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && ex is not (PairStopException or PhaseTwoStaleException)) {
                diagnostics.Add(new("ERROR", "INVENTORY_INCOMPLETE", root.OriginalPath, ex.GetType().Name));
            }
            void Walk(string path, int depth)
            {
                budget.Check(); token.ThrowIfCancellationRequested();
                if (depth > limits.Depth || entries.Count >= limits.Entries) throw new PairStopException("INCOMPLETE", "INVENTORY_BUDGET_EXCEEDED", path);
                var facts = files.Metadata(path);
                if (!string.Equals(facts.OriginalPath, path, StringComparison.Ordinal) || facts.Key != PathPolicy.Normalize(path) || !PathPolicy.Contains(root.OriginalPath, path))
                    throw new PairStopException("STALE", "INVENTORY_PATH_CHANGED", path);
                var relative = depth == 0 ? "." : Path.GetRelativePath(root.OriginalPath, path);
                if (entries.Any(e => e.Key == facts.Key)) throw new PairStopException("STALE", "DUPLICATE_NAMESPACE_PATH", path);
                entries.Add(new(0, root.Id, relative, facts.Key, facts));
                if (facts.Status != "OK" || facts.Kind is not ("REGULAR_FILE" or "DIRECTORY")) throw new IOException("Unobservable/reparse entry.");
                if (facts.Kind == "REGULAR_FILE") {
                    regular++; total = checked(total + facts.Size!.Value);
                    if (regular > limits.Files || facts.Size > limits.FileBytes || total > limits.TotalBytes) throw new PairStopException("INCOMPLETE", "PAYLOAD_SCOPE_BUDGET_EXCEEDED", path);
                } else {
                    foreach (var child in files.Children(path).Order(StringComparer.Ordinal)) {
                        if (!string.Equals(Path.GetDirectoryName(child), path, StringComparison.Ordinal)) throw new PairStopException("STALE", "INVALID_CHILD_NAMESPACE", child);
                        Walk(child, depth + 1);
                    }
                }
            }
        }
        return new(entries.OrderBy(e => e.RootId).ThenBy(e => e.RelativePath, StringComparer.Ordinal).Select((e, i) => e with { Id = i + 1 }).ToArray(), complete.ToArray());
    }
    private static int[] VerifyInventory(PairRoot[] roots, PairEntry[] entries, IPhaseTwoContentFileSystem files,
        ObservationPairLimits limits, PairBudget budget, int[] required, CancellationToken token)
    {
        var diagnostics = new List<Diagnostic>(); var fresh = Inventory(roots, files, limits, budget, diagnostics, token);
        foreach (var id in required) {
            if (!fresh.Complete.Contains(id)) throw new PairStopException("STALE", "NAMESPACE_NO_LONGER_OBSERVABLE", roots.Single(r => r.Id == id).OriginalPath);
            if (!PairJson.Equal(entries.Where(e => e.RootId == id).Select(e => e with { Id = 0 }).ToArray(), fresh.Entries.Where(e => e.RootId == id).Select(e => e with { Id = 0 }).ToArray()))
                throw new PairStopException("STALE", "INVENTORY_CHANGED", roots.Single(r => r.Id == id).OriginalPath);
            var root = roots.Single(r => r.Id == id);
            if (PhaseTwoSafety.Inspect(root.OriginalPath) != root.Facts) throw new PairStopException("STALE", "ROOT_BINDING_CHANGED", root.OriginalPath);
        }
        return required;
    }
    private static PairOutcome[] PreflightOutcomes(PairRoot[] roots, PairEntry[] entries, IEnumerable<PairChart> charts, IEnumerable<int> verified, string state) =>
        roots.Select(r => new PairOutcome(r.Id, state == "COMPLETE" ? "COMPLETE" : verified.Contains(r.Id) && state == "INCOMPLETE" ? "COMPLETE" : state,
            entries.LongCount(e => e.RootId == r.Id), entries.LongCount(e => e.RootId == r.Id && e.Facts.Kind == "REGULAR_FILE"),
            charts.LongCount(c => c.Read.Status == "SUCCESS" && entries.Any(e => e.Id == c.EntryId && e.RootId == r.Id)))).ToArray();
    private static PairOutcome[] ContentOutcomes(PairPreflight pf, IEnumerable<PairRead> reads, IEnumerable<int> verified, string state) => pf.Roots.Select(r => {
        var scope = pf.Entries.Where(e => e.RootId == r.Id).ToArray(); var ids = scope.Where(e => e.Facts.Kind == "REGULAR_FILE").Select(e => e.Id).ToHashSet();
        var good = reads.LongCount(e => ids.Contains(e.EntryId) && e.Read.Status == "SUCCESS");
        return new PairOutcome(r.Id, state is "STALE" or "CANCELLED" or "FAILED" or "BLOCKED" ? state : verified.Contains(r.Id) && good == ids.Count ? "COMPLETE" : "INCOMPLETE", scope.Length, ids.Count, good);
    }).ToArray();
    private static bool AcquisitionError(Exception ex) => ex is OperationCanceledException or IOException or UnauthorizedAccessException or InvalidOperationException;
    private static string Failure(Exception ex) => ex is OperationCanceledException ? "CANCELLED" : ex is PairStopException p ? p.State : ex is PhaseTwoStaleException ? "STALE" : "FAILED";
    private static Diagnostic DiagnosticFor(Exception ex) => new("ERROR", ex is PairStopException p ? p.Code : ex is PhaseTwoStaleException ? "STALE_PREFLIGHT" : Failure(ex),
        ex is PairStopException stop ? stop.AffectedPath : "", ex.GetType().Name);
    private static string Now() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
}
