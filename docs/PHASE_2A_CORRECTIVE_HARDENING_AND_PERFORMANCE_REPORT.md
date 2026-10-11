# Phase 2A corrective hardening and autonomous performance report

Date: 2026-10-11. Authorized work: local corrective code, synthetic tests and
bounded autonomous benchmarks only. No real-library, simulator or Backbeat
data access, Phase 2B implementation, migration, staging, commit or push.

## Baseline and changed files

Initial HEAD and origin/main: `808a19e7547776b12d9df1165b6d0744a5bb2404`.
Branch: main. Initial worktree: clean. Phase 2A implementation is contained in
`09063adeb4e59563b1b8ad667e6436f2c188dd6a`; the later required HEAD adds the
Backbeat research record. The governing design baseline remains `a300b54...`.
The contract and historical Phase 1/Phase 2A documents remain unchanged.

| File | Change |
| --- | --- |
| README.md | Modified: research navigation, benchmark commands and corrective report. |
| docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md | Modified: Markdown formatting only, retaining all substantive research. |
| src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs | Modified: immutable session identity, transition/publication guards and explicit schema revision. |
| src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs | Modified: conservative runtime evidence revision selection. |
| tests/BmsLibraryAudit.Tests/PhaseTwoPreflightTests.cs | Modified: 22 additional direct SQL/provenance cases. |
| Directory.Build.targets | Added: build-time Git status attestation for Phase 2, with UNKNOWN fallback. |
| src/BmsLibraryAudit.Core/PhaseTwoBuildProvenance.cs | Added: strict SDK revision parsing and CLEAN/DIRTY/UNKNOWN evidence policy. |
| scripts/Phase2A-Performance.Common.ps1 | Added: native directory identity, owned run validation, atomic JSON and private Windows Job Object. |
| scripts/Start-Phase2A-PerformanceTest.ps1 | Added: unique run creation and prompt detached launch. |
| scripts/Phase2A-Performance-Test.ps1 | Added: independent supervisor and synthetic worker. |
| scripts/Get-Phase2A-PerformanceTest.ps1 | Added: final-status inspection and cancellation request. |
| scripts/Test-Phase2A-PerformanceHarness.ps1 | Added: separate QUICK/lifecycle/failure validation and Markdown preservation checks. |
| docs/PHASE_2A_CORRECTIVE_HARDENING_AND_PERFORMANCE_REPORT.md | Added: this report. |

No files were deleted, no dependencies added, and no project/lockfile, Phase 1
core/schema/test or production filesystem safety helper was changed. Existing
`.gitignore` already excludes generated `.artifacts/`, bin/obj and `.tools/`;
research, code, scripts and reports remain eligible for tracking.

## Research formatting

Removed accidental escapes from headings, emphasis, list markers, links and
underscores, decoded `&#x20;` spacing, reduced repeated blank lines, and repaired
contiguous tables and code fences. A preservation check compares every
non-whitespace substantive character against the committed original after
decoding those formatting escapes. It also checks balanced fences and remaining
escape/entity patterns. URLs, reference SHAs, conclusions, RESEARCH ONLY status,
prohibitions and separation from Phase 2 are preserved. No new research claim
was made or external repository consulted.

## SQLite invariants and compatibility

The earlier schema blocked CONTENT insertion but permitted a RUNNING PREFLIGHT
row to change stage while simultaneously changing parent/accepted-manifest fields
to satisfy its CHECK. The new null-safe UPDATE trigger makes these immutable:
session_id, stage, parent_preflight_id, accepted_manifest_sha256, started_utc,
tool_version, runtime_build_sha and source_component_count.

Sessions must be inserted RUNNING without a final digest. From RUNNING, Phase 2A
permits RUNNING, COMPLETED, FAILED or CANCELLED. STALE is not a Phase 2A global
transition; observed drift remains a component rejection. COMPLETED requires a
64-character lowercase hex final digest and completion timestamp. Other states
cannot carry accepted final evidence. Terminal rows reject every update,
including no-op/repeated transitions, and every deletion. The existing source/
evidence guards and transactionally staged completion timestamp remain intact.
Application finalization still validates full coverage and computes the
canonical digest before the atomic COMPLETED commit; SQL transition validation
alone does not fabricate a valid manifest.

Direct SQL tests exercise coordinated PREFLIGHT-to-CONTENT mutation, all eight
identity changes, valid COMPLETED/FAILED/CANCELLED transitions, transitions from
each terminal state, malformed/premature evidence and legacy schema refusal.
Existing cancellation, export failure, seal, ownership, canonical and source
protection cases also pass.

Application ID remains `0x424D5332`, user_version remains 1, capability remains
PREFLIGHT_ONLY. A required metadata key explicitly identifies
`schema_revision=phase2a-hardening-1`. Exact compiled schema validation includes
the new triggers automatically through the in-memory initializer.

**Previous Phase 2A databases are incompatible with this build.** This was tested
against an actual disposable baseline database produced before edits:
`.artifacts/phase2a-smoke-cb4d604c709e4689a914e3d951e87527/phase2.db`.
The corrected CLI refused it with exit 1, left bytes/mtime unchanged, and created
no report directory. A unit test also exercises refusal. No automatic migration
or silent compatibility claim is made.

Future Phase 2B requires a separately versioned schema and explicit compatibility
procedure. Safest default: retain old evidence and receipts for audit, create a
new owned database, and obtain a newly authorized preflight capture. An optional
future offline conversion would need to validate old seals/ownership, preserve
original files and record conversion provenance; content trust must not follow
merely from adding tables/triggers. That procedure is design only here.

## Runtime build provenance

The .NET SDK obtains SourceRevisionId through its Git/SourceLink targets and
appends it to AssemblyInformationalVersion. Before edits, a clean Release rebuild
at required HEAD produced `0.1.0+808a19e7547776b12d9df1165b6d0744a5bb2404`.
A synthetic preflight persisted/reported that same revision while its captured
repository design baseline remained `a300b54...`. This is appropriate: HEAD
contains the implementation commit and later documentation.

SDK revision alone does not attest to a clean tree. The added build target reads
Git HEAD/status at build time, checks agreement with SDK SourceRevisionId, and
embeds PhaseTwoBuildState. It never substitutes a hardcoded HEAD. Only a valid
40-character SDK revision with CLEAN status is emitted as runtime_build_sha.
Dirty trees emit UNKNOWN_DIRTY; missing/inconclusive metadata emit UNKNOWN.
Current uncommitted Release artifacts were observed DIRTY, and their sessions/
reports show UNKNOWN_DIRTY. Their SDK informational revision is contextual, not
a claim that the corrective source equals a commit.

With SDK source queries disabled in a separate test build, informational version
was 0.1.0, state UNKNOWN, and a synthetic CLI preflight persisted/reported UNKNOWN.
Artifacts: `.artifacts/provenance-without-git-proof/`. Parser tests also cover
invalid/missing revisions and unavailable status. CLEAN resolution is tested;
an actual clean build of these uncommitted corrections must await an authorized
commit and is not claimed. No build-time status check promises protection against
concurrent source edits or independently establishes reproducible binaries.

The manifest encoder and normative contract are unchanged. The runtime field
continues to bind the conservative value persisted at session creation. Golden
vectors still pass. Benchmark metadata separately records executable/Core hashes,
informational version, launch Git context and observed runtime evidence revision.

## Autonomous benchmark design and safety limits

Windows PowerShell 7+ is required. No scheduler, background job, network,
additional package, multimedia decoder or real-library input is used.

| Profile | Components | Observation roots | Direct charts | Non-chart files |
| --- | ---: | ---: | ---: | ---: |
| QUICK | 10 | 20 | 40 | 200 |
| STANDARD | 100 | 200 | 400 | 4,000 |
| EXTENDED | 500 | 1,000 | 2,000 | 20,000 |

Each component has identical BMS/PMS bytes in two roots, distinct from every
other component. The real Phase 1 scanner creates source evidence and the real
RelationshipAnalyzer discovers exact components. Seed defaults to 1729 and is
explicit in started.json. Tiny resource placeholders include nested directories,
empty directories, extensionless files and several opaque extensions.
`-NestedRoots` makes the second root a descendant, increasing view memberships
without duplicating physical paths. Fixtures have deterministic relative paths,
bytes and timestamps; session dates/absolute paths mean seals differ across runs.

Hard limits: 25,000 generated files, 10,000 directories, 16 MiB of generated
fixture payload, and 1–120 minutes (default 60) runtime. Profile sizes are checked
before generation; byte/file limits are checked before each placeholder write.
Directory counts are also verified. Database/report/log overhead is separate
from the fixture payload limit. Maximum profile remains 22,000 files and bounded
tiny payloads. No cleanup is provided: all runs/logs/results are retained.

Artifact root is fixed under the explicitly authorized repository:
`.artifacts/phase2a-performance`. No arbitrary artifact-root argument exists.
The workspace itself must be an ordinary directory with proven volume-GUID
identity. Every descendant ancestor is checked for reparse/case-sensitive
configuration; ownership markers and native run identities must agree.
PowerShell exposes the cloud Desktop above this workspace as a reparse point;
the existing workspace is therefore the explicit authorization anchor, not an
arbitrary supplied root. Native containment is checked against that anchor.
Production Phase 1/2A native source/destination policies are unchanged and run
independently in the CLI. Unsupported metadata configurations fail closed.
These checks do not claim adversarial atomic directory immutability.

The launcher creates one unique owned run, captures inputs/binary hashes, starts
a separate hidden pwsh supervisor and returns its PID/run path promptly. Each
run has one supervisor claim. The supervisor creates a private KILL_ON_JOB_CLOSE
Windows Job Object, assigns the worker before opening its startup gate, then
enforces timeout/cancellation independently. Worker native CLI descendants inherit
that private job. Closing it kills only owned worker descendants. An assignment
failure kills the just-created, gated child before it can generate a fixture.

The worker incrementally logs phases, records timings/exit codes/CPU and sampled
peak memory, and verifies expected component/root/file/workload counts, seals,
repeat report hashes and unchanged synthetic source bytes/mtimes. Phase 2A timing
includes its mandatory initial export; persisted reporting is timed independently.
Validation reads synthetic placeholder bytes solely to verify preservation, and
is timed separately from production Phase 2A resource-read accounting.

Worker exit alone cannot establish success. The supervisor requires exit 0 plus
a matching validated SUCCESS worker result. Exceptions/crash/timeout/cancellation
produce FAILED/TIMEOUT/CANCELLED. JSON uses a flushed temporary file and atomic
rename; incomplete output cannot look like a valid final result. If the supervisor
itself is forcibly killed, its job closes and status reports INTERRUPTED when no
final result exists. PID plus exact creation time protects against PID reuse.
No automatic resume or repair is performed.

## Commands and retained evidence

From the repository root, after Release build:

```powershell
./scripts/Start-Phase2A-PerformanceTest.ps1 -Profile QUICK -Seed 1729 -NestedRoots -TimeoutMinutes 5 -Synchronous
./scripts/Start-Phase2A-PerformanceTest.ps1 -Profile EXTENDED -Seed 1729 -NestedRoots -TimeoutMinutes 60
./scripts/Get-Phase2A-PerformanceTest.ps1 -RunDirectory <printed-run-directory>
./scripts/Get-Phase2A-PerformanceTest.ps1 -RunDirectory <printed-run-directory> -Cancel
Get-Content -LiteralPath <printed-run-directory>/result.json -Raw | ConvertFrom-Json
```

The local `.tools/dotnet/dotnet.exe` is the default; an explicit `-DotNet` path
is supported. Inspection is a user action, not an instruction to keep Codex
running or poll. Cancellation creates a request consumed by the independent
supervisor; do not kill a bare numeric PID or unrelated process. Preserve logs
when status is INTERRUPTED. There is no cancellation-by-broad-process-name command.

Retained files include started.json, launcher.json, supervisor.json, worker.json,
stdout.log, stderr.log, per-phase logs, fixture-manifest.json, metrics.json,
worker-result.json and atomic result.json. Full reports and the two generated
databases are retained. Final result records the first relevant failure and
available metrics; absent metrics are not reported as completed measurements.

## Synchronous validation and performance findings

Using the existing project-local SDK 10.0.401:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
& ./.tools/dotnet/dotnet.exe restore --locked-mode
& ./.tools/dotnet/dotnet.exe build -c Release --no-restore
& ./.tools/dotnet/dotnet.exe test -c Release --no-build
./scripts/Smoke-Test.ps1 -DotNet (Join-Path $PWD '.tools/dotnet/dotnet.exe')
./scripts/Phase2A-Smoke-Test.ps1 -DotNet (Join-Path $PWD '.tools/dotnet/dotnet.exe')
./scripts/Test-Phase2A-PerformanceHarness.ps1 -DotNet (Join-Path $PWD '.tools/dotnet/dotnet.exe')
git diff --check
```

Locked restore passed; final Release build: zero warnings/errors. Tests: 107
passed, zero failed, zero skipped (35 Phase 1 + 50 original Phase 2 + 22 new).
Both existing CLI smokes passed. Final Phase 1 artifacts:
`.artifacts/smoke-2381b6a03f164f0e8f598aacced377fc`.
Final Phase 2A artifacts: `.artifacts/phase2a-smoke-26c3672c19ab4cd29f8f0f80f879cb62`;
one admitted component, no rejects, four files/four directories, 78 future read
bytes and 30 chart read bytes, unchanged source files/database, identical exports.

Performance measurements and final harness/lifecycle evidence are recorded below
after synchronous validation. Long-run stress completion is deliberately not
awaited or asserted.

The combined preflight/initial export dominates the observed small-workload CLI
time. It does not identify a single internal bottleneck. Source inspection shows
per-entry indexed SQLite lookups/inserts, scans of containing observation roots,
two enumerations, repeated native ancestor checks, 256-entry transaction batches
and a TEMP verified-entry index. Root membership work grows with roots × entries;
topmost-root selection also examines root pairs. These are hypotheses for larger
measurements, not proven hotspot attribution. Source relationships remain in
memory; resource payloads remain streamed/opaque. Child CLI memory measurements
are separated from the PowerShell generator/validator's memory.

No production algorithm optimization was made: QUICK phase timings do not justify
a broad rewrite or identify an isolated cost with equivalent before/after evidence.
Future optimization should profile SQL/native/membership time on the retained
EXTENDED workload, then compare equivalent fixtures and logical evidence, allowing
the contract's intentional session/time identity differences. No concurrent hashing,
asynchronous filesystem walker or speculative caching was introduced.

## Remaining limits and review boundary

Benchmarks validate local Windows behavior, not HDD multimedia throughput, UNC,
worst-case all-shared chart pairs, million-entry inventories or the real library.
8.3 alias coverage remains the earlier documented limitation. A lost supervisor
cannot promise a terminal JSON result; INTERRUPTED without success is deliberate.
Development harness checks initially exposed a cloud-ancestor rejection and an
EOF-based lifecycle assertion; both were corrected in the harness, preserving
production safety. Development artifacts remain retained.

Review the incompatible schema revision and conservative build provenance before
any future evidence capture. No automatic migration, Phase 2B tables, content
sessions or real-library execution is implemented/authorized by this iteration.
The autonomous stress run is a separate pending measurement, not an acceptance
claim. Stop after this corrective report and bounded launch.

## Observed QUICK and harness evidence

Final completed QUICK: `run-0aafdedfd3c046a98573aaf93a989b20`, SUCCESS.
Evidence directory: `.artifacts/phase2a-performance/run-0aafdedfd3c046a98573aaf93a989b20/`.

| Measurement | Observed |
| --- | ---: |
| Supervisor total elapsed | 6.508 s |
| Fixture generation | 1.236 s |
| Phase 1 scan | 0.371 s |
| Phase 2A plus initial export | 2.895 s |
| Independent persisted report | 0.384 s |
| Preservation/report validation | 0.083 s |
| Generated charts / resources / directories | 40 / 200 / 131 |
| Selected / admitted / rejected components | 10 / 10 / 0 |
| Observation roots / union directories / view memberships | 20 / 120 / 540 |
| Nested root pairs / shared paths | 10 / 180 |
| Total fixture payload / estimated future read bytes | 6,925 / 6,925 |
| Phase 1 / Phase 2 DB bytes | 81,920 / 622,592 |
| Sampled Phase 2A process peak working set | 65,892,352 bytes |
| Worker/generator peak working set | 203,444,224 bytes |

All three CLI phase exits were 0, with zero diagnostic errors/warnings. Preflight
seal: `f3c4f2118a0c42fbf5b4509a0471bdc37862fc36ec650811f85bffe504f3c06d`.
Fixture manifest SHA-256:
`3f0db6823a39fb2e546358bb3d055316021cc5defebf25b739728d38924413f9`.
The same seed/profile produced that identical fixture digest in earlier runs;
preflight seals intentionally differ with session/path/provenance facts.

`harness-validation.json` reports nine successful checks: substantive Markdown
preservation/syntax, foreign-directory refusal, synchronous nested QUICK,
detached normal QUICK, injected exception, worker crash without a worker result,
timeout, cancellation, and supervisor survival after launcher-shell exit.
Unrelated process preservation and owned descendant termination passed. The five
measured detached launcher calls returned in 0.357–0.542 seconds. Failure-mode
runs correctly record FAILED/TIMEOUT/CANCELLED; they are successful harness
assertions, not successful performance runs. Missing terminal evidence is
explicitly INTERRUPTED, never SUCCESS.

The launcher gives the supervisor an independent hidden Windows console without
launcher stream redirection; the supervisor owns worker stdout/stderr capture.
This avoids tying supervisor lifetime to unused launcher pipe EOF. Its short
process-lifecycle fixture completed after the launching pwsh had exited.

## Autonomous stress launch disposition

The one bounded EXTENDED launch is recorded here after invocation. Its result
is deliberately PENDING, not PASS; no agent monitoring or completion wait occurs.

- Run ID: `run-847a960f75ee45dc83d31dd9fa087fe9`.
- Supervisor PID returned by launcher: `9608`.
- Run directory: `C:\Users\benja\OneDrive\Escritorio\BMSAudit\.artifacts\phase2a-performance\run-847a960f75ee45dc83d31dd9fa087fe9`.
- Profile: EXTENDED; seed: 1729; nested roots enabled.
- Requested population: 500 components, 1,000 observation roots, 2,000 charts and 20,000 non-chart placeholders.
- Timeout: 3,600 seconds from supervisor start. Launch invocation returned with exit 0 in 1.415 seconds.
- State observed from launcher: PENDING. No completion, stress metrics or success is claimed.
- Expected evidence: started.json, stdout.log, stderr.log, metrics.json and atomic result.json in that directory.

User inspection/cancellation, without a Codex monitoring session:

```powershell
$taskRun = Join-Path $PWD '.artifacts/phase2a-performance/run-847a960f75ee45dc83d31dd9fa087fe9'
./scripts/Get-Phase2A-PerformanceTest.ps1 -RunDirectory $taskRun
# Only if cancellation is desired:
./scripts/Get-Phase2A-PerformanceTest.ps1 -RunDirectory $taskRun -Cancel
```

Final recommendation: `PHASE_2A_CORRECTIONS_VALIDATED_STRESS_PENDING`.

## Post-run EXTENDED benchmark validation — 2026-10-11

The autonomous EXTENDED benchmark completed successfully after the original report was written.

- Run ID: `run-847a960f75ee45dc83d31dd9fa087fe9`
- Final state: SUCCESS
- Worker exit code: 0
- Total elapsed: 379.555 seconds
- Timeout: 3,600 seconds
- Selected/admitted/rejected components: 500 / 500 / 0
- Generated charts: 2,000
- Generated non-chart files: 20,000
- Observation roots: 1,000
- Observation membership rows: 42,000
- Shared paths: 14,000
- Phase 2 database size: 30,752,768 bytes
- Phase 2A sampled peak working set: 93,761,536 bytes
- Runtime build SHA: UNKNOWN_DIRTY

### Phase timings

| Phase | Elapsed seconds |
| --- | ---: |
| Fixture generation | 112.250 |
| Phase 1 scan | 7.336 |
| Phase 2A preflight and initial export | 241.296 |
| Independent persisted report | 10.856 |
| Validation | 5.358 |

All three CLI phases exited with code 0.

Preflight manifest SHA-256:
`38b09c9167d59247baa94305e3c5b05f596b14505b58abf20944ed1c2d8fb81e`

The result establishes successful synthetic stress validation for this workload. It does not establish real-library performance, exhaustive filesystem compatibility, or authorization for Phase 2B.

Updated assessment:
`PHASE_2A_CORRECTIONS_STRESS_VALIDATED_PENDING_CODE_REVIEW`
