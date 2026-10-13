using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

[CollectionDefinition("ClosureAudit", DisableParallelization = true)]
public sealed class ClosureAuditCollection;

[Collection("ClosureAudit")]
public sealed class ClosureInfrastructureTests
{
    [Theory]
    [InlineData(80000000)] [InlineData(234496000)] [InlineData(56320000)] [InlineData(143357952)]
    [InlineData(11000000)] [InlineData(128000000)] [InlineData(16000000)]
    public void FixedWorkloadsRefuseInsufficientResourcesBeforeAllocation(long estimate)
    {
        const long budget = 1024L * 1024 * 1024;
        Assert.False(StressExecution.Fits(estimate, budget, 3 * estimate));
        Assert.True(StressExecution.Fits(estimate, budget, 3 * estimate + 3));
        var output = new List<string>(); var allocations = 0;
        using (var refused = new StressExecution(output.Add, "bounded-admission-example", "charts", 1, estimate,
            StressExecution.Fits(estimate, budget, 3 * estimate), "No fixture allocation on refusal"))
        {
            if (refused.Admitted) { allocations++; refused.Start(); }
            Assert.Throws<InvalidOperationException>(() => refused.Complete("EXECUTED_PASS", "forbidden", "elapsed_ms=0"));
        }
        Assert.Equal(0, allocations);
        var record = JsonSerializer.Deserialize<StressExecutionRecord>(Assert.Single(output)[StressExecution.Prefix.Length..])!;
        Assert.False(record.WorkloadExecuted); Assert.Equal("NOT_EXECUTED_RESOURCE_GATE", record.ActualOutcome);
        Assert.Null(record.Measurements); Assert.Null(record.ElapsedMilliseconds); Assert.NotNull(record.Reason);
    }
    [Fact]
    public void AdmittedExecutionUsesOneImmutableMeasurementAndCannotCompleteTwice()
    {
        var output = new List<string>();
        const string measurement = "elapsed_ms=0; allocated_managed=2; sampled_peak_managed=3; sampled_peak_working_set=4; lifetime_peak_working_set=5";
        using (var execution = new StressExecution(output.Add, "admitted-example", "charts", 1, 2, true, "One completed measurement"))
        {
            execution.Start(); Assert.True(execution.Record.WorkloadExecuted);
            execution.Complete("EXECUTED_PASS", "Assertions verified", measurement);
            Assert.Throws<InvalidOperationException>(() => execution.Complete("EXECUTED_PASS", "duplicate completion", measurement));
        }
        var record = JsonSerializer.Deserialize<StressExecutionRecord>(Assert.Single(output)[StressExecution.Prefix.Length..])!;
        Assert.Equal(2, record.Measurements!["allocated_managed"]); Assert.Equal("EXECUTED_PASS", record.ActualOutcome);
    }
    [Fact]
    public void EnvironmentBlockCannotBeStartedAsAnAdmittedWorkload()
    {
        using var execution = new StressExecution(_ => { }, "blocked-example", "charts", 1, 2, true, "Refuse blocked environment");
        execution.BlockEnvironment("independent synthetic block"); Assert.False(execution.Admitted);
        Assert.Throws<InvalidOperationException>(execution.Start);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IndependentVerifierAcceptsCompleteAndResourceOmittedEvidence(bool omitWorkloads)
    {
        var fixture = Fixture(omitWorkloads);
        var result = await Run(fixture, "positive");
        Assert.True(result.ExitCode == 0, result.Output);
        var audit = JsonNode.Parse(File.ReadAllText(result.OutputPath))!;
        Assert.Equal(17, audit["Framework"]!["Total"]!.GetValue<int>());
        Assert.Equal(omitWorkloads ? 0 : 13, audit["Accounting"]!["WorkloadsExecuted"]!.GetValue<int>());
        Assert.Equal(omitWorkloads ? 13 : 0, audit["Accounting"]!["OmittedByAdmission"]!.GetValue<int>());
        Assert.Equal(omitWorkloads ? 0 : 9, audit["Accounting"]!["FullyCompletedData"]!.GetValue<int>());
        Assert.Equal(1, audit["Accounting"]!["PlannerCases"]!.GetValue<int>()); Assert.Equal(3, audit["Accounting"]!["ControlCases"]!.GetValue<int>());
    }
    [Theory]
    [InlineData("missing_scenario")] [InlineData("duplicate_scenario")] [InlineData("contradictory_runner")]
    [InlineData("completion_without_execution")] [InlineData("failed_workload_success")]
    [InlineData("nonexecution_metrics")] [InlineData("population")] [InlineData("estimate")]
    [InlineData("invalid_json")] [InlineData("missing_field")] [InlineData("duplicate_json_key")]
    [InlineData("missing_record")] [InlineData("duplicate_record")] [InlineData("measurement_mismatch")]
    [InlineData("invalid_measurement")] [InlineData("runner_status")] [InlineData("test_identity")]
    [InlineData("incomplete_trx")] [InlineData("failed_trx")] [InlineData("counter_mismatch")]
    public async Task IndependentVerifierRejectsInvalidSyntheticEvidence(string mutation)
    {
        var fixture = Fixture(false); var ns = fixture.Root!.Name.Namespace;
        var results = fixture.Root.Element(ns + "Results")!.Elements().ToArray();
        var stdout = results[0].Descendants(ns + "StdOut").Single();
        var lines = stdout.Value.Split('\n');
        var record = JsonNode.Parse(lines[^1][StressExecution.Prefix.Length..])!.AsObject();
        switch (mutation)
        {
            case "missing_scenario": results[0].Remove(); fixture.Root.Element(ns + "TestDefinitions")!.Elements().First().Remove(); SetCounters(fixture, 16); break;
            case "duplicate_scenario": record["ScenarioId"] = "charts-500000-128-True"; break;
            case "contradictory_runner": record["ActualOutcome"] = "EXECUTED_FAIL"; break;
            case "completion_without_execution": record["WorkloadExecuted"] = false; break;
            case "failed_workload_success": results[0].SetAttributeValue("outcome", "Failed"); break;
            case "nonexecution_metrics": record["WorkloadExecuted"] = false; record["ActualOutcome"] = "NOT_EXECUTED_RESOURCE_GATE"; record["Admission"] = "REJECTED_RESOURCE_GATE"; record["ActualResult"] = null; record["Reason"] = "synthetic resource stop"; record["ElapsedMilliseconds"] = null; break;
            case "population": record["RequestedPopulation"] = 42; break;
            case "estimate": record["EstimatedBytes"] = 42; break;
            case "invalid_json": stdout.Value = "STRESS_EXECUTION_JSON {"; break;
            case "missing_field": record.Remove("Reason"); break;
            case "duplicate_json_key": stdout.Value = string.Join('\n', lines[..^1]) + "\n" + StressExecution.Prefix + record.ToJsonString().Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal); break;
            case "missing_record": stdout.Value = lines[0]; break;
            case "duplicate_record": stdout.Value += "\n" + lines[^1]; break;
            case "measurement_mismatch": record["Measurements"]!["allocated_managed"] = 123; break;
            case "invalid_measurement": record["Measurements"]!["allocated_managed"] = -1; break;
            case "runner_status": record["TestRunnerStatus"] = "Passed"; break;
            case "test_identity": fixture.Root.Element(ns + "TestDefinitions")!.Elements().First().SetAttributeValue("name", "unrelated"); break;
            case "incomplete_trx": fixture.Root.Element(ns + "ResultSummary")!.SetAttributeValue("outcome", "Aborted"); break;
            case "failed_trx": fixture.Root.Element(ns + "ResultSummary")!.Element(ns + "Counters")!.SetAttributeValue("failed", 1); break;
            case "counter_mismatch": SetCounters(fixture, 18); break;
        }
        if (mutation is not ("invalid_json" or "duplicate_json_key" or "missing_record" or "duplicate_record"))
            stdout.Value = string.Join('\n', lines[..^1]) + "\n" + StressExecution.Prefix + record.ToJsonString();
        var result = await Run(fixture, mutation);
        Assert.True(result.ExitCode != 0, "Verifier accepted " + mutation); Assert.False(File.Exists(result.OutputPath));
    }
    [Fact]
    public async Task VerifierNeverOverwritesExistingEvidence()
    {
        var result = await Run(Fixture(false), "existing_output", existingOutput: true);
        Assert.NotEqual(0, result.ExitCode); Assert.Equal("owned synthetic marker", File.ReadAllText(result.OutputPath));
    }
    [Theory]
    [InlineData("missing_input")] [InlineData("invalid_xml")] [InlineData("dtd")]
    public async Task VerifierRejectsMissingMalformedOrEntityBasedTrx(string kind)
    {
        var text = kind == "invalid_xml" ? "<TestRun>" : kind == "dtd" ? "<!DOCTYPE TestRun [<!ENTITY sample 'forbidden'>]><TestRun>&sample;</TestRun>" : null;
        var result = await Run(Fixture(false), kind, sourceText: text, missingInput: kind == "missing_input");
        Assert.NotEqual(0, result.ExitCode); Assert.False(File.Exists(result.OutputPath));
    }
    [Fact]
    public async Task NonexecutionCannotPublishHumanMeasurementsWhileJsonClaimsNone()
    {
        var fixture = Fixture(true); var ns = fixture.Root!.Name.Namespace;
        var stdout = fixture.Root.Element(ns + "Results")!.Elements().First().Descendants(ns + "StdOut").Single();
        stdout.Value = "SYNTHETIC elapsed_ms=1; allocated_managed=2; sampled_peak_managed=3; sampled_peak_working_set=4; lifetime_peak_working_set=5\n" + stdout.Value;
        var result = await Run(fixture, "fabricated_human_metrics"); Assert.NotEqual(0, result.ExitCode); Assert.False(File.Exists(result.OutputPath));
    }
    private static readonly string[] Cases =
    [
        "charts-250000-25000-False|ProgressiveChartPopulationsHaveExactIndependentOracles(count: 250000, folderCount: 25000, shared: False)|charts|250000|486400000|WORKLOAD|EXECUTED_PASS",
        "charts-500000-128-True|ProgressiveChartPopulationsHaveExactIndependentOracles(count: 500000, folderCount: 128, shared: True)|charts|500000|768524288|WORKLOAD|EXECUTED_PASS",
        "charts-1000000-128-True|ProgressiveChartPopulationsHaveExactIndependentOracles(count: 1000000, folderCount: 128, shared: True)|charts|1000000|900000000|WORKLOAD|EXECUTED_PASS",
        "unique-hashes-250000|ManyUniqueHashesFitTheRevisedDefaultChartEnvelope|charts|250000|750000000|WORKLOAD|EXECUTED_PASS",
        "dense-pairs-447|DensePairsFitDeclaredBudgetWithoutLosingExactClosure|charts|447|80000000|WORKLOAD|EXECUTED_PASS",
        "chart-cancellation-150000|CancellationDuringLargeChartProcessingReturnsNoAnalysis|charts|150000|234496000|WORKLOAD|CANCELLED_EXPECTED",
        "shared-hash-10000|HighlySharedHashStopsBeforeQuadraticPairAllocation|charts|10000|56320000|WORKLOAD|RESOURCE_STOP_EXPECTED",
        "sparse-chain-20000|ManySparseRelationshipsPreserveTransitiveClosureAndExactLabels|charts|39998|143357952|WORKLOAD|EXECUTED_PASS",
        "four-exact-components|SeveralLargeExactComponentsKeepFullOrderedMembers|charts|800|11000000|WORKLOAD|EXECUTED_PASS",
        "cluster-csv-250000-valid|LargeCsvUsesGeneratedStreamWithLateMalformedRecord(malformed: False)|rows|250000|128000000|WORKLOAD|EXECUTED_PASS",
        "cluster-csv-250000-malformed|LargeCsvUsesGeneratedStreamWithLateMalformedRecord(malformed: True)|rows|250000|128000000|WORKLOAD|EXECUTED_PASS",
        "csv-cancellation-250000|CancellationDuringLargeCsvStopsWithNoCompleteFingerprint|rows|250000|16000000|WORKLOAD|CANCELLED_EXPECTED",
        "duplicate-csv-250000|LargeDuplicateCsvFitsDefaultStreamingEnvelopeWithEveryPhysicalRow|rows|250000|400000000|WORKLOAD|EXECUTED_PASS",
        "initial-million-admission|InitialMillionChartEstimateIsRejectedBeforeCalibration|charts|1000000|1638400000|ADMISSION_PLANNER|NOT_EXECUTED_RESOURCE_GATE",
        "accounting-control-resource|AdmissionAccountingIsVerifiedWithoutLargeAllocations(outcome: \"resource\")|synthetic control|1|2|CONTROL|NOT_EXECUTED_RESOURCE_GATE",
        "accounting-control-environment|AdmissionAccountingIsVerifiedWithoutLargeAllocations(outcome: \"environment\")|synthetic control|1|2|CONTROL|BLOCKED_ENVIRONMENT",
        "accounting-control-failed_execution|AdmissionAccountingIsVerifiedWithoutLargeAllocations(outcome: \"failed_execution\")|synthetic control|1|2|CONTROL|EXECUTED_FAIL"
    ];
    private static XDocument Fixture(bool omitWorkloads)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var definitions = new XElement(ns + "TestDefinitions"); var results = new XElement(ns + "Results");
        foreach (var data in Cases)
        {
            var p = data.Split('|'); var name = "BmsLibraryAudit.SourceInspector.Offline.Tests.ScalabilityTests." + p[1];
            var id = Guid.NewGuid().ToString(); var execution = Guid.NewGuid().ToString();
            definitions.Add(new XElement(ns + "UnitTest", new XAttribute("id", id), new XAttribute("name", name),
                new XElement(ns + "Execution", new XAttribute("id", execution)),
                new XElement(ns + "TestMethod", new XAttribute("className", "BmsLibraryAudit.SourceInspector.Offline.Tests.ScalabilityTests"), new XAttribute("name", p[1].Split('(')[0]))));
            var outcome = omitWorkloads && p[5] == "WORKLOAD" ? "NOT_EXECUTED_RESOURCE_GATE" : p[6];
            var executed = outcome is not ("NOT_EXECUTED_RESOURCE_GATE" or "BLOCKED_ENVIRONMENT");
            var measured = executed && p[5] == "WORKLOAD";
            var record = new JsonObject
            {
                ["Version"] = 1, ["ScenarioId"] = p[0], ["Purpose"] = p[5], ["PopulationUnit"] = p[2],
                ["RequestedPopulation"] = long.Parse(p[3]), ["EstimatedBytes"] = long.Parse(p[4]),
                ["Admission"] = executed ? "ADMITTED" : outcome == "BLOCKED_ENVIRONMENT" ? "BLOCKED_ENVIRONMENT" : "REJECTED_RESOURCE_GATE",
                ["WorkloadExecuted"] = executed, ["ExpectedResult"] = "Independently authored synthetic audit oracle",
                ["ActualOutcome"] = outcome, ["ActualResult"] = measured ? p[0].EndsWith("malformed", StringComparison.Ordinal) ? "Strict parser outcome=Malformed; synthetic oracle passed" : "Synthetic expected assertions passed" : null,
                ["TestRunnerStatus"] = "REQUIRES_TRX_CORRELATION", ["ElapsedMilliseconds"] = executed ? 1 : null,
                ["Measurements"] = measured ? new JsonObject { ["elapsed_ms"] = 1, ["allocated_managed"] = 2, ["sampled_peak_managed"] = 3, ["sampled_peak_working_set"] = 4, ["lifetime_peak_working_set"] = 5 } : null,
                ["Reason"] = !executed || outcome == "EXECUTED_FAIL" ? "Independent synthetic non-success reason" : null
            };
            var stdout = (measured ? "SYNTHETIC elapsed_ms=1; allocated_managed=2; sampled_peak_managed=3; sampled_peak_working_set=4; lifetime_peak_working_set=5\n" : "") + StressExecution.Prefix + record.ToJsonString();
            results.Add(new XElement(ns + "UnitTestResult", new XAttribute("testId", id), new XAttribute("executionId", execution), new XAttribute("testName", name),
                new XAttribute("outcome", "Passed"), new XElement(ns + "Output", new XElement(ns + "StdOut", stdout))));
        }
        var document = new XDocument(new XElement(ns + "TestRun", definitions, results, new XElement(ns + "ResultSummary", new XAttribute("outcome", "Completed"), new XElement(ns + "Counters"))));
        SetCounters(document, 17); return document;
    }
    private static void SetCounters(XDocument document, int total)
    {
        var ns = document.Root!.Name.Namespace;
        var counters = document.Root.Element(ns + "ResultSummary")!.Element(ns + "Counters")!;
        foreach (var field in new[] { "total", "executed", "passed" }) counters.SetAttributeValue(field, total);
        foreach (var field in new[] { "failed", "error", "timeout", "aborted", "inconclusive", "passedButRunAborted", "notRunnable", "notExecuted", "disconnected", "warning", "inProgress", "pending" }) counters.SetAttributeValue(field, 0);
    }
    private sealed record RunResult(int ExitCode, string Output, string OutputPath);
    private static async Task<RunResult> Run(XDocument document, string tag, bool existingOutput = false, string? sourceText = null, bool missingInput = false)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var directory = Path.Combine(root, ".artifacts", "source-inspector-offline", "closure-verifier-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var input = Path.Combine(directory, tag + ".trx"); var output = Path.Combine(directory, "audit.json");
        document.Save(input); if (sourceText is not null) File.WriteAllText(input, sourceText);
        var before = SHA256.HashData(File.ReadAllBytes(input));
        if (existingOutput) File.WriteAllText(output, "owned synthetic marker");
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = root };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "scripts", "Test-OfflineInspector-StressEvidence.ps1"), "-TrxPath", missingInput ? input + ".missing" : input, "-OutputJson", output }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(deadline.Token); } catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(input)));
        return new(process.ExitCode, await stdout + await stderr, output);
    }
}
