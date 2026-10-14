using System.Text.Json;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;

return Run(args);

static int Run(string[] args)
{
    const string usage = """
        BmsLibraryAudit: Phase 1 and synthetic-validated Phase 2A/2B/3/4 (Windows)
        bms-audit scan --db <audit.db> --root <path> [--root <path> ...] [--rehash-all]
        bms-audit report --db <audit.db> --output <new-or-empty-directory>
        bms-audit phase2 preflight --source-db <audit.db> --source-scan <id> --db <phase2.db> --output <new-or-empty-directory> [--component <EXACT_ONLY-id> ...]
        bms-audit phase2 hash --db <phase2.db> --preflight <id> --accept-manifest <sha256> --output <new-or-empty-directory>
        bms-audit phase2 report --db <phase2.db> --session <id> --output <new-or-empty-directory>
        bms-audit phase3 inspect --chart <synthetic-chart> --source-root <protected-root> --output <new-or-empty-directory>
        bms-audit phase4 resolve --db <phase2.db> --content-session <id> --accept-content-manifest <sha256> --chart-report <phase3.json> --observation-root <root-key> --output <new-or-empty-directory>
        CONTENT is explicit and requires an accepted completed PREFLIGHT. Real-library execution requires separate authorization.
        Outputs must be outside source libraries. Sources are only read.
        """;
    if (args.Length == 0 || args is ["--help"] or ["-h"])
    { Console.WriteLine(usage); return args.Length == 0 ? 1 : 0; }
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    try
    {
        if (args[0] == "phase2") return RunPhaseTwo(args, cancellation.Token);
        if (args[0] == "phase3") return RunPhaseThree(args, cancellation.Token);
        if (args[0] == "phase4") return RunPhaseFour(args, cancellation.Token);
        var command = args[0];
        if (command is not ("scan" or "report")) throw new ArgumentException($"Unknown command: {command}");
        string? db = null, output = null;
        var roots = new List<string>();
        var rehash = false;
        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];
            if (option == "--rehash-all" && command == "scan" && !rehash) { rehash = true; continue; }
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value or invalid option: {option}");
            var value = args[++index];
            switch (option)
            {
                case "--db" when db is null: db = value; break;
                case "--root" when command == "scan": roots.Add(value); break;
                case "--output" when command == "report" && output is null: output = value; break;
                default: throw new ArgumentException($"Unknown, duplicate or inapplicable option: {option}");
            }
        }
        if (db is null) throw new ArgumentException("--db is required.");
        if (command == "scan")
        {
            var result = new ChartScanner().Scan(new(db, roots, rehash), cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return result.Diagnostics.Any(item => item.Severity == "ERROR") ? 2 : 0;
        }
        if (output is null) throw new ArgumentException("--output is required.");
        var analysis = ReportWriter.Write(db, output);
        Console.WriteLine($"Reports: summary.json, duplicate_charts.csv, folder_candidates.csv, clusters.csv\nCandidate pairs: {analysis.Candidates.Count}; duplicate SHA-256 groups: {analysis.DuplicateGroups.Count}");
        return 0;
    }
    catch (OperationCanceledException) { Console.Error.WriteLine(args[0] == "phase2" ? "Phase 2 operation cancelled; inspect the persisted session/export state." : args[0] is "phase3" or "phase4" ? "Inspection/resolution cancelled; any partial export is not complete evidence." : "Scan cancelled; transaction rolled back."); return 130; }
    catch (Exception exception) when (args[0] is "phase2" or "phase4" && exception is JsonException or FormatException or InvalidCastException or OverflowException)
    { Console.Error.WriteLine($"Error: invalid {args[0]} input/evidence: {exception.Message}"); return 1; }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or SqliteException)
    { Console.Error.WriteLine($"Error: {exception.Message}"); return 1; }
}

static int RunPhaseFour(string[] args, CancellationToken token)
{
    if (args.Length < 2 || args[1] != "resolve") throw new ArgumentException("phase4 requires resolve.");
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 2; i < args.Length; i++)
    {
        var key = args[i];
        if (key is not ("--db" or "--content-session" or "--accept-content-manifest" or "--chart-report" or "--observation-root" or "--output") ||
            ++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[i]))
            throw new ArgumentException("Unknown, duplicate or missing Phase 4 option: " + key);
    }
    string Required(string key) => options.TryGetValue(key, out var value) ? value : throw new ArgumentException(key + " is required.");
    if (!long.TryParse(Required("--content-session"), out var session) || session <= 0) throw new ArgumentException("Positive --content-session required.");
    var result = PhaseFourResolver.Resolve(new(Required("--db"), session, Required("--accept-content-manifest"), Required("--chart-report"), Required("--observation-root"), Required("--output")), token: token);
    Console.WriteLine(JsonSerializer.Serialize(new { result.ResultVersion, result.ResolutionCoverage, result.ConditionalCoverage,
        Declarations = result.Declarations.Length, Usages = result.Usages.Length, result.DependencyClosureProven, result.RemovalAuthority,
        SemanticRelationCoverage = result.Semantics?.RelationEnumerationCoverage,
        SemanticModelCoverage = result.Semantics?.ConditionalModelCoverage,
        BoundedDependencyEvidence = result.Semantics?.BoundedProof.State,
        Report = Path.Combine(PathPolicy.Absolute(Required("--output")), "dependency_resolution.json") }, new JsonSerializerOptions { WriteIndented = true }));
    return result.ExitCode;
}

static int RunPhaseThree(string[] args, CancellationToken token)
{
    if (args.Length < 2 || args[1] != "inspect") throw new ArgumentException("phase3 requires inspect.");
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 2; i < args.Length; i++)
    {
        var key = args[i];
        if (key is not ("--chart" or "--source-root" or "--output") || ++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[i]))
            throw new ArgumentException("Unknown, duplicate or missing Phase 3 option: " + key);
    }
    string Required(string key) => options.TryGetValue(key, out var value) ? value : throw new ArgumentException(key + " is required.");
    var inspection = PhaseThreeInspector.Inspect(Required("--chart"), Required("--source-root"), Required("--output"), token: token);
    Console.WriteLine(JsonSerializer.Serialize(new { inspection.Analysis.Format, inspection.Analysis.Sha256, inspection.Analysis.SyntaxCoverage,
        Declarations = inspection.Analysis.Declarations.Length, Usages = inspection.Analysis.Usages.Length, Diagnostics = inspection.Analysis.Diagnostics.Length,
        inspection.Analysis.DependencyClosureProven, Report = Path.Combine(PathPolicy.Absolute(Required("--output")), "chart_inspection.json") }, new JsonSerializerOptions { WriteIndented = true }));
    return inspection.Analysis.ExitCode;
}

static int RunPhaseTwo(string[] args, CancellationToken token)
{
    if (args.Length < 2) throw new ArgumentException("phase2 requires preflight, hash or report.");
    var command = args[1];
    if (command is not ("preflight" or "hash" or "report")) throw new ArgumentException("Unknown Phase 2 command.");
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var components = new List<string>();
    for (var index = 2; index < args.Length; index++)
    {
        var key = args[index];
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Missing option value: " + key);
        var value = args[index];
        if (key == "--component" && command == "preflight") { components.Add(value); continue; }
        var valid = key is "--db" or "--output" || command == "preflight" && key is "--source-db" or "--source-scan" || command == "report" && key == "--session" || command == "hash" && key is "--preflight" or "--accept-manifest";
        if (!valid || !options.TryAdd(key, value)) throw new ArgumentException("Unknown, duplicate or inapplicable option: " + key);
    }
    string Required(string key) => options.TryGetValue(key, out var value) ? value : throw new ArgumentException(key + " is required.");
    long Id(string key) => long.TryParse(Required(key), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new ArgumentException("Invalid positive identifier: " + key);
    var preflight = command == "hash" ? Id("--preflight") : 0;
    var accepted = command == "hash" ? Required("--accept-manifest") : "";
    var db = Required("--db"); var output = Required("--output");
    var result = command switch
    {
        "preflight" => new PhaseTwoPreflightRunner().Run(new(Required("--source-db"), Id("--source-scan"), db, output, components), token),
        "hash" => new PhaseTwoContentRunner().Run(new(db, preflight, accepted, output), token),
        _ => PhaseTwoReportWriter.Write(db, Id("--session"), output, token)
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return result.State == "COMPLETED" ? result.ExitCode : result.State == "CANCELLED" ? 130 : result.State == "STALE" ? 2 : 1;
}
