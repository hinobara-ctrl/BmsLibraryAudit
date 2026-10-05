using System.Text.Json;
using BmsLibraryAudit.Core;
using Microsoft.Data.Sqlite;

return Run(args);

static int Run(string[] args)
{
    const string usage = """
        BmsLibraryAudit Phase 1
        bms-audit scan --db <audit.db> --root <path> [--root <path> ...] [--rehash-all]
        bms-audit report --db <audit.db> --output <new-or-empty-directory>
        Outputs must be outside source libraries. Sources are only read.
        """;
    if (args.Length == 0 || args is ["--help"] or ["-h"])
    { Console.WriteLine(usage); return args.Length == 0 ? 1 : 0; }
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    try
    {
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
    catch (OperationCanceledException) { Console.Error.WriteLine("Scan cancelled; transaction rolled back."); return 130; }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or SqliteException)
    { Console.Error.WriteLine($"Error: {exception.Message}"); return 1; }
}
