using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace BmsLibraryAudit.SourceInspector.Offline.Tests;

// Test-only execution evidence. Runner outcome is supplied by a separate TRX join.
internal sealed record StressExecutionRecord(int Version, string ScenarioId, string Purpose, string PopulationUnit,
    long RequestedPopulation, long EstimatedBytes, string Admission, bool WorkloadExecuted,
    string ExpectedResult, string ActualOutcome, string? ActualResult, string TestRunnerStatus,
    long? ElapsedMilliseconds, IReadOnlyDictionary<string, long>? Measurements, string? Reason);

internal sealed class StressExecution(Action<string> emit, string id, string unit, long population, long estimate,
    bool admitted, string expected, string purpose = "WORKLOAD") : IDisposable
{
    internal const string Prefix = "STRESS_EXECUTION_JSON ";
    private Stopwatch? elapsed;
    private bool disposed;
    private StressExecutionRecord record = new(1, id, purpose, unit, population, estimate,
        admitted ? "ADMITTED" : "REJECTED_RESOURCE_GATE", false, expected,
        admitted ? "BLOCKED_ENVIRONMENT" : "NOT_EXECUTED_RESOURCE_GATE", null, "REQUIRES_TRX_CORRELATION", null, null,
        admitted ? "Admitted scenario did not start; consult runner evidence." : "Conservative admission rejected the workload; no fixture allocated.");
    public bool Admitted => admitted;
    public StressExecutionRecord Record => record;
    internal static bool Fits(long estimate, long budget, long availableMemory) => estimate <= budget && estimate < availableMemory / 3;
    public void Start()
    {
        if (!admitted || elapsed is not null || disposed) throw new InvalidOperationException("Workload must be admitted and started exactly once.");
        elapsed = Stopwatch.StartNew();
        record = record with { WorkloadExecuted = true, ActualOutcome = "EXECUTED_FAIL", Reason = "Workload did not reach validated completion; consult TRX failure evidence." };
    }
    public void Complete(string outcome, string actual, string measurements)
    {
        if (elapsed is null || disposed || outcome is not ("EXECUTED_PASS" or "CANCELLED_EXPECTED" or "RESOURCE_STOP_EXPECTED"))
            throw new InvalidOperationException("Only an executed and validated workload can complete.");
        var values = measurements.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => long.Parse(p[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);
        record = record with { ActualOutcome = outcome, ActualResult = actual, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            Measurements = values, Reason = null };
    }
    public void BlockEnvironment(string reason)
    {
        if (elapsed is not null || disposed) throw new InvalidOperationException("Started workloads cannot be relabeled as unexecuted.");
        record = record with { Admission = "BLOCKED_ENVIRONMENT", ActualOutcome = "BLOCKED_ENVIRONMENT", Reason = reason };
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (elapsed is not null && record.ElapsedMilliseconds is null) record = record with { ElapsedMilliseconds = elapsed.ElapsedMilliseconds };
        emit(Prefix + JsonSerializer.Serialize(record));
    }
}
