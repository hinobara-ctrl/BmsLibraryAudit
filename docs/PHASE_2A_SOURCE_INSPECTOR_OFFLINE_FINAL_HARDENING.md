# Independent source inspector offline final hardening

Phase: `PHASE_2A.READ_ONLY_SOURCE_INSPECTOR.OFFLINE_FINAL_HARDENING`.
Recommendation: **`SOURCE_INSPECTOR_OFFLINE_FINAL_HARDENING_READY_FOR_REVIEW`**.

## A. Baseline and authorization

Initial branch **main**; HEAD and origin/main both
`85ac52b92a2eb47502f6991a2ed65102482c48fe`. Initial Git status, tracked and
staged diffs were empty, with no untracked files. Latest commits were 85ac52b,
e7ef157, 6bd19dd, 932bcf2 and 7f97402. Local origin retains the BMSAudit.git
name; no remote, branch, index or history operation was performed.

Reviewed [policy](../AGENTS.md), [context](../MASTER_CONTEXT.md), [README](../README.md),
[sources](SOURCES.md), [inspector design](PHASE_2A_READ_ONLY_SOURCE_INSPECTOR_DESIGN.md),
[foundation](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_FOUNDATION.md) and
[corrective/scalability evidence](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CORRECTIVE_HARDENING_AND_SCALABILITY.md).
The new user attachment authorizes H1–H5 offline hardening; earlier documents'
native STOP gates remain future requirements, not a prohibition on this scope.
The explicit current task forbids production execution, including the older
repository-policy CLI smoke requirement. No production build/test/CLI or SQLite
opening was performed. No additional scalability phase, population, algorithm,
resource-policy revision or architecture was introduced.

## B. Changed-file inventory

Three tracked files modified and three new files; none staged.

| File | Status and responsibility |
| --- | --- |
| [IncrementalCsv.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/IncrementalCsv.cs) | Modified: translate known failures only at Stream.Read |
| [ReportEvidence.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/ReportEvidence.cs) | Modified: own one bounded CSV buffer snapshot; remove broad outer IOException handling |
| [ScalabilityTests.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/ScalabilityTests.cs) | Modified: explicit execution records for existing scenarios and three tiny accounting controls |
| [FinalHardeningTests.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/FinalHardeningTests.cs) | New: 45 independent Stream/buffer regression cases |
| [StressExecution.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/StressExecution.cs) | New, test-only: finite admission predicate and auditable execution record/lifecycle |
| This report | New: hardening evidence, limitations and final audit |

All other source files, contracts, historical documents, project/solution settings,
lockfiles and dependency versions are unchanged. Existing benchmark evidence was
not rewritten. New output occupies only approved project build/test locations.

## C. Stream exception contract and correction

Previously Csv caught IOException around the entire parser, while lifecycle and
unsupported Read errors could escape. The new private ReadInput wrapper surrounds
only the caller's Stream.Read call. A filtered catch recognizes exactly:

| Read exception | Offline result |
| --- | --- |
| IOException, including subclasses | Pending INPUT_READ_FAILED, ParseStatus.Unsupported |
| ObjectDisposedException | Same explicit incomplete input failure |
| NotSupportedException | Same explicit incomplete input failure |
| UnauthorizedAccessException | Same explicit incomplete input failure |
| OperationCanceledException with caller cancellation requested | Existing ParseStatus.Cancelled / CANCELLED |
| ArgumentException, InvalidOperationException, OutOfMemoryException, unowned cancellation and other unexpected exceptions | Propagate; not relabeled as input unavailability |

The outer IOException catch was removed so errors from unrelated parsing code
cannot be mistaken for Stream.Read failures. This is not a catch-all policy.
Unavailable acquisition is not malformed CSV. Known non-cancellation Read failures
remain input failures even if a cancellation request coincides; owned cancellation
exceptions/checkpoints retain the cancellation result.

Consumed count advances only for bytes successfully returned by Read. A Read
that throws is not credited with hypothetical partial bytes. A fault before EOF
never marks EOF observed. Failed/cancelled results have no bound Value or complete
SHA-256, ParsingComplete=false and BytesComplete=false. Reconciliation rejects
the failed binding even if the caller supplies otherwise matching typed rows.
Caller streams are neither sought nor disposed. Internal readers/hash state and
private memory adapters are disposed normally. Cooperative cancellation does not
promise immediate interruption of a blocking synchronous Read.

## D. In-memory byte ownership and snapshot correctness

Summary already took one owned bounded copy. The three CSV buffer overloads now
check policy and pre-cancellation, reject MaxInputBytes overflow **before accessing
or copying** caller memory, copy once with ToArray, and feed only that private
snapshot through MemoryInput. Incremental parsing and SHA therefore consume the
same owned sequence. No independently hashed original buffer or additional full
CSV copy is used. The default buffer cap remains 8 MiB; large CSV retains the
existing caller-owned Stream API, chunk/total/field/retention budgets and cancellation.

The private snapshot survives later changes to the caller array. A deterministic
MemoryManager regression mutates its storage on its second span access: the old
CSV adapter repeatedly accessed it during chunk consumption; the corrected
adapter accesses it exactly once. Tests then deliberately perform that second
access and verify the bound row/hash remain the original owned result. Chunk
sizes 1, 7 and 65,536 cover all CSV types; summary ownership is checked too.

This demonstrates bounded private ownership and avoids unreliable timing tests.
It does **not** prove atomic acquisition against concurrent mutation while the
copy itself runs. Callers must keep memory valid and stable during snapshot
creation; the resulting parser/hash sequence remains privately owned. No native
source snapshot, file identity, stable filesystem window or producer authenticity
is inferred. Caller-owned Streams can likewise change outside this component;
native stability must be established independently in future integration.

## E. Stress admission and execution accounting

Installed xunit.assert 2.9.3 documentation for SkipException.ForSkip explicitly
states its dynamic skip mechanism requires xUnit v3 runtime infrastructure. The
current approved test framework remains xUnit 2.9.3, so no unsupported skip throw,
runner replacement or package upgrade was used. Instead, the permitted separate
auditable-record approach makes a generic green count insufficient for capacity.

Every existing ScalabilityTests case emits one version-1 JSON line prefixed
STRESS_EXECUTION_JSON. Fields include unique scenario ID, purpose, requested unit/
population, estimated bytes, admission, actual execution flag, independent expected
result, actual outcome/result, elapsed time, resource measurements when applicable,
nonexecution reason and runner-status correlation marker. Purpose separates
WORKLOAD, ADMISSION_PLANNER and CONTROL. An unstarted scenario cannot call Complete;
a started scenario that never reaches verified completion defaults to EXECUTED_FAIL.
Rejected scenarios publish no fabricated timing or measurement. Records are emitted
on disposal through test output, including failure paths; no library file API is added.

The safe original rule is unchanged: estimate <=1 GiB and estimate <one third of
GC-reported available memory. Fixed bounded cases retain their previous admission.
No declined workload is forced to run or converted into a functional failure due
solely to limited RAM. The old framework early return now has an explicit machine
record; returning normally does not declare workload execution.

The final [TRX](../.artifacts/source-inspector-offline/offline-final-hardening.trx)
was independently joined to the [audited JSON](../.artifacts/source-inspector-offline/offline-final-hardening-stress-audited.json).
The audit required exactly one record per ScalabilityTests case, unique IDs,
consistent execution/outcome flags, no timing/measurements for nonexecution,
reasons for every omitted scenario and actual runner outcomes. A green WORKLOAD
with EXECUTED_FAIL would fail this audit. Raw TRX markers remain immutable evidence;
the joined record resolves TestRunnerStatus to the actual Passed result.

Actual counts: **17 accounting records**, comprising **13 executed workloads**,
**0 resource-admission-omitted real workloads**, **1 unexecuted initial planner**
and **3 tiny accounting controls**. Among the 13 workloads: 10 EXECUTED_PASS
(including one expected malformed-input test), 2 CANCELLED_EXPECTED and
1 RESOURCE_STOP_EXPECTED. Only nine are successful complete-data scenarios.
The planner/control records are not demonstrated scalability or actual environment
failures. The controlled EXECUTED_FAIL tests bookkeeping, not a failed inspector run.

All runner statuses below are Passed. Population is the request, not the amount
processed after cancellation. Times are execution-scope elapsed milliseconds;
allocation/peak samples and separately timed operation intervals are in JSON.

| Scenario ID | Requested population | Admission | Workload executed | Actual workload outcome | Scope ms |
| --- | ---: | --- | --- | --- | ---: |
| charts-250000-25000-False | 250000 charts | ADMITTED | Yes | EXECUTED_PASS | 1315 |
| charts-500000-128-True | 500000 charts | ADMITTED | Yes | EXECUTED_PASS | 2379 |
| charts-1000000-128-True | 1000000 charts | ADMITTED | Yes | EXECUTED_PASS | 5186 |
| unique-hashes-250000 | 250000 charts | ADMITTED | Yes | EXECUTED_PASS | 1591 |
| dense-pairs-447 | 447 charts | ADMITTED | Yes | EXECUTED_PASS | 35 |
| sparse-chain-20000 | 39998 charts | ADMITTED | Yes | EXECUTED_PASS | 332 |
| four-exact-components | 800 charts | ADMITTED | Yes | EXECUTED_PASS | 27 |
| cluster-csv-250000-valid | 250000 rows | ADMITTED | Yes | EXECUTED_PASS | 646 |
| duplicate-csv-250000 | 250000 rows | ADMITTED | Yes | EXECUTED_PASS | 814 |
| cluster-csv-250000-malformed | 250000 rows + bad suffix | ADMITTED | Yes | EXECUTED_PASS, expected malformed | 312 |
| shared-hash-10000 | 10000 charts | ADMITTED | Yes | RESOURCE_STOP_EXPECTED | 74 |
| chart-cancellation-150000 | 150000 charts | ADMITTED | Yes | CANCELLED_EXPECTED | 206 |
| csv-cancellation-250000 | 250000 rows | ADMITTED | Yes | CANCELLED_EXPECTED | 41 |
| initial-million-admission | 1000000 charts | REJECTED_RESOURCE_GATE | No; planner | NOT_EXECUTED_RESOURCE_GATE | N/A |
| accounting-control-resource | 1 tiny control | REJECTED_RESOURCE_GATE | No | NOT_EXECUTED_RESOURCE_GATE | N/A |
| accounting-control-environment | 1 tiny control | BLOCKED_ENVIRONMENT | No | BLOCKED_ENVIRONMENT, simulated | N/A |
| accounting-control-failed_execution | 1 tiny control | ADMITTED | Yes; control only | EXECUTED_FAIL, deliberate caught exception | 0 |

The workload's existing independent assertions remain unchanged: counts, physical
multiplicity, full exact/related closures, ordering, digest/byte identity and expected
stops. Existing stress cases ran once in the authorized full regression suite;
this is not a repeated scalability calibration phase or a new capacity claim.
Sampling remains 100 ms plus initial/final observations, may miss spikes and
includes reused host/GC state. Cumulative managed allocation is not retained or
total native memory; lifetime working-set peaks are not per-scenario native peaks.

## F. Native integration boundary

Outstanding future integration requirements, NOT IMPLEMENTED / NOT EXECUTED:

- Source file identity, filesystem/platform admission and independently observed size.
- Captured byte count and source fingerprint correlated to that exact identity.
- Stable guarded acquisition window; refusal of unsupported/unstable sources.
- Native report identities, producer provenance and independent authentication.
- Independent correlation of native source observations to parser results.
- Native process-attributed filesystem-operation observation and positive controls.
- SQLite ownership/schema/integrity, source/scan identity and opening safety.
- Preservation before and after the entire inspection window, including closure.

EOF observed, supplied-buffer parsing, expected-length match, filesystem identity
and authenticated producer remain distinct facts. Offline SHA identifies supplied
bytes only. FilesystemIdentityVerified=false, HistoricalProducerVerified=false,
NativeInspectionComplete=false and Phase2ExecutionAuthorized=false remain fixed.
Every offline result retains INSPECTOR_PARTIAL; no new native success status,
Win32 wrapper, speculative handle or SQLite dependency was introduced.

## G. Independent negative-test oracles

FinalHardeningTests adds 45 cases in nine methods. Expected facts were authored
independently of the implementation. The fixed cluster header is 41 bytes; each
test row is 40 bytes. Stream failure offsets are **0, 81, 121 and 161**, representing
before first bytes, after one row, after two rows and immediately before EOF.
All four known exception types run at each offset, proving exact count, Pending/
Unsupported classification, absent digest/value, absent EOF and rejected full
reconciliation. Disposal counters prove parser ownership; a preclosed stream
is not disposed again. Normal completion and two read-cancellation forms are checked.
Argument/defect/OOM-signal/unowned-cancellation tests require the exact original
exception to escape; the OOM signal allocates no large workload.

MemoryManager fixtures independently count accesses, mutate deterministic backing
storage, reject any access for oversized inputs and cancel on snapshot access.
Literal alternative CSV quoting preserves semantics but changes exact digest;
terminal-LF truncation never yields complete evidence. Existing chunk, strict UTF-8,
malformed-late-row, byte-count freeze and raw-binding tests remain intact.

Three H3 control cases inject tiny resource conditions rather than exhausting
the runner: rejected allocation is never started/completed; a mock environmental
block has no execution/timing; an intentionally caught tiny failure emits
EXECUTED_FAIL even though the framework assertion passes. Round-trip deserialization
checks purpose, execution flag, outcome, reason and unresolved-runner marker before
the independent TRX join. Controls are explicitly excluded from capacity totals.

## H. Release build and test results

SDK **10.0.401**, net10.0 Release. Locked restore and both explicit Release builds
exited 0; builds reported **0 warnings and 0 errors**. Final suite:
**240 executed, 240 passed, 0 failed, 0 skipped/not executed**, across 90 methods.
An exact-name comparison to the previous 192-case TRX found **0 missing cases**.

| Test class | Cases / passed |
| --- | ---: |
| LogicalTests | 55 / 55 |
| ReportTests | 52 / 52 |
| ResourceTests | 30 / 30 |
| CorrectiveTests | 12 / 12 |
| StreamingTests | 29 / 29 |
| FinalHardeningTests | 45 / 45 |
| ScalabilityTests | 17 / 17; execution accounting described separately in E |

Commands actually executed:

```powershell
dotnet restore tests/BmsLibraryAudit.SourceInspector.Offline.Tests/BmsLibraryAudit.SourceInspector.Offline.Tests.csproj --locked-mode
dotnet build tools/BmsLibraryAudit.SourceInspector.Offline/BmsLibraryAudit.SourceInspector.Offline.csproj -c Release --no-restore
dotnet build tests/BmsLibraryAudit.SourceInspector.Offline.Tests/BmsLibraryAudit.SourceInspector.Offline.Tests.csproj -c Release --no-restore
dotnet test tests/BmsLibraryAudit.SourceInspector.Offline.Tests/BmsLibraryAudit.SourceInspector.Offline.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=offline-final-hardening.trx" --results-directory .artifacts/source-inspector-offline
```

Library remains framework-only, with no package/project dependency. Test references
are unchanged: Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3,
xunit.runner.visualstudio 3.1.1, and only the offline library. Existing assembly
independence test passed; no production/native/SQLite reference or file-opening
API was introduced. Final auxiliary artifact SHA-256:

- Library: `F8B76A6CB0503BF9E9FCE1E4300A337E6052C23E1BE3C4CB9EA7AB5E3A09FE17`.
- Tests: `A774BE2F42E4DCB0FBE696EF7B1461547D5BE439CBF7E0483F1A347C51DE92BE`.
- Audited execution JSON: `2A294CC9C5ADCE8D0F36D526EA0484D741AF7D23EEA6523917A693F6D7C7483B`.

These identify dirty-worktree auxiliary artifacts, not clean/native provenance.
No commit was manufactured for testing or attribution.

## I. Environment failures and retries

**No NuGet auditing or testhost IPC failure occurred in this task.** Historical
failures in previous reports remain unchanged. Restore/testhost used the authorized
normal Windows host context outside restrictive sandbox IPC/network boundaries;
builds used the default context. Windows administrator-role check returned false.
No auditing switch, package replacement, elevation, security/configuration change,
installation or infrastructure workaround was introduced.

Preserved development evidence:

- [Before corrections](../.artifacts/source-inspector-offline/offline-final-hardening-before.trx):
  exit 1, 45 executed, 22 passed, 23 failed. Thirteen failures demonstrate missing
  known-stream/closed-stream handling; nine demonstrate repeated CSV-memory access.
  One additional failure was a test-fixture quoting error: it quoted a path prefix
  inside a longer absolute-path field. It was corrected to quote the complete
  delimited field without changing the expected valid-equivalent-row outcome.
- [H1/H2 after corrections](../.artifacts/source-inspector-offline/offline-final-hardening-h1-h2.trx):
  exit 0, 45/45 passed.
- First H3 control build: exit 1, CS0619 on Record.Exception(Func<Task>); no tests
  executed and no TRX produced. A throwing synchronous lambda selected the async
  overload. Explicit Action selection corrected the test, not functional behavior.
- [Functional retry](../.artifacts/source-inspector-offline/offline-final-hardening-functional.trx):
  same filtered test command after that compile correction, exit 0, 48/48 passed.
- Initial [JSON export](../.artifacts/source-inspector-offline/offline-final-hardening-stress.json)
  preserved valid scenario records but PowerShell serialized the XML counter node
  as an empty array. It is not the accepted audit artifact. A separate audited
  export used explicit integer counters and was round-trip checked against the
  240-case TRX; the earlier file was preserved, not overwritten.

These were regression demonstrations, test-code/fixture and export errors, not
environmental failures or hidden inspector defects. All observed outcomes are
retained separately from the final passing result; no expected assertion was
weakened to accommodate an implementation bug.

## J. Remaining risks and limitations

Copy ownership is not adversarial-concurrency/native-source authentication.
Blocking synchronous reads and snapshot copies have no immediate cancellation
deadline. Unexpected exceptions deliberately escape; no all-exception recovery,
OOM handling or arbitrary provider defect suppression is promised. A malformed
or cancelled stream can have consumed bytes without a complete report fingerprint.
Length matching cannot discover lost whole rows without external frozen length/
source census and full reconciliation. Native origins remain unverified.

Resource limits, 8 MiB JSON DOM/buffer cap, retained typed/chart/pair structures,
cooperative sort/graph cancellation and future large-output architecture limitations
from the prior report remain unchanged. A green xUnit method alone still cannot
prove workload execution: reviewers must inspect the JSON record and its TRX
correlation. Unexpected host termination can leave no record; that absence is
incomplete evidence, never a successful workload. Runtime skip support was not added.

## K. Git and documentation audit

Read/reviewed all changed/new source, exception boundaries, byte-ownership paths,
test oracles, accounting states and this report. Scoped static search found no
SQLite, native filesystem wrapper, production reference or filesystem-opening API
in the offline library. Production, scripts, all historical documents, project
settings and package lock/version diffs are empty. No protected data path was
opened, statted, enumerated, hashed, copied or modified; only in-memory fixtures
and approved repository/build/test artifacts were used.

Git checks: git diff --check; git diff --stat; git diff --cached --stat;
git status --short, plus separate git diff --no-index --check against NUL for
each untracked file. Strict UTF-8, relative links and Markdown table widths were
checked. HEAD/origin/main remain the required baseline; no staged files.

Final status:

```text
 M tests/BmsLibraryAudit.SourceInspector.Offline.Tests/ScalabilityTests.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/IncrementalCsv.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/ReportEvidence.cs
?? docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_FINAL_HARDENING.md
?? tests/BmsLibraryAudit.SourceInspector.Offline.Tests/FinalHardeningTests.cs
?? tests/BmsLibraryAudit.SourceInspector.Offline.Tests/StressExecution.cs
```

## L. Final recommendation

**`SOURCE_INSPECTOR_OFFLINE_FINAL_HARDENING_READY_FOR_REVIEW`**.
H1–H3 were implemented and independently tested; H4 remains a documented future
native requirement. H5 passed with separate framework and workload accounting.
READY covers this bounded offline scope only. No SQLite/native integration,
real reports/databases/library access, production execution, Phase 2A/2B, tracing,
staging, commit or push occurred. STOP for independent review and separate next-step
authorization; no continuation is automatically initiated.
