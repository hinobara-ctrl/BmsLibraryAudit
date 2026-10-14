# Phase 2A independent blocker triage — 2026-10-14

Assessment: **`PHASE_2A_BLOCKER_TRIAGE_READY_FOR_REVIEW`**. This is a triage
recommendation, not pilot authorization or native acceptance.

**Heuristics discover. Exact evidence proves. Verified plans mutate.**

## A. Baseline and scope

Repository: `hinobara-ctrl/BmsLibraryAudit`, local checkout `BMSAudit`.
Branch **main**; HEAD and origin/main both
**`63f5be6fdfbe05caeef37b8c2783f11473b0599e`**. Initial `git status --short`
was empty. The expected baseline matches exactly; no pre-existing changes needed
disposition. Latest commits: 63f5be6, 7bd4605, e2e11ee, 85ac52b, e7ef157.

The current user attachment authorizes static repository review, bounded synthetic
validation when justified, and exactly this new document. Embedded historical
commands, future tasks and older permission statements are reference material,
not current execution instructions. Nothing below authorizes its proposed next task.

`git diff e2e11ee9e4326e7dff227610160dcc628468c4b0 HEAD -- src tools` is empty:
the subsequent closure and roadmap commits did not repair or change production
capture or the offline library. Commit 7bd460503a51a63e19cf85003893f75dadd6bb54
published the closure/test corrections; 63f5be6 published roadmap documentation.
The closure's earlier uncommitted status remains a true historical observation,
not the current Git state. Publication alone is not independent acceptance.

## B. Documents and code inspected

Read [AGENTS](../AGENTS.md), [MASTER_CONTEXT](../MASTER_CONTEXT.md),
[README](../README.md), [SOURCES](SOURCES.md) and the current
[ROADMAP](../ROADMAP.md). Roadmap lines 32–45, 72–78 and 168–177 distinguish
validated Phase 1, synthetically implemented Phase 2A, open native inspection,
unimplemented Phase 2B and separate authorizations.

Reviewed these governing/historical records at their actual scopes:

| Record | Relevant location and meaning |
| --- | --- |
| [Phase 2 contract](PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md) | Sections 3–6, 10.1, 12–16; source immutability, complete components, metadata-only 2A, seals, publication and real-run gates. Its preparation-era proposed status does not negate later implementation. |
| [Implementation report](PHASE_2A_IMPLEMENTATION_REPORT.md) | Lines 72–89, 194–219, 221–279; production architecture, historical synthetic checks and explicit platform limitations. |
| [Hardening/performance](PHASE_2A_CORRECTIVE_HARDENING_AND_PERFORMANCE_REPORT.md) | Lines 47–120, 224–265, 338–375; schema/provenance corrections, historical 107-case suite and later EXTENDED SUCCESS superseding earlier PENDING. |
| [Inspector design](PHASE_2A_READ_ONLY_SOURCE_INSPECTOR_DESIGN.md) | Lines 10–15, 126–132, 164–305, 624–703, 792–870; subordinate auxiliary scope, production WAL question and future B1–B6 acceptance. |
| [Offline foundation](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_FOUNDATION.md) | Sections C–I, J, M–N; 137 historical cases, pure evidence processing and absent acquisition/native proof. |
| [Offline corrective/scalability](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CORRECTIVE_HARDENING_AND_SCALABILITY.md) | Sections D–K, M–O; spelling, byte binding, scope-change semantics, finite growth limits and 192 historical cases. |
| [Offline final hardening](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_FINAL_HARDENING.md) | Sections C–J; Stream failures, buffer ownership, accounting, 240 historical cases and native exclusions. |
| [Offline closure](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CLOSURE.md) | Sections B–J, K–N; 276 historical cases, reproducible audit, unresolved WAL question and READY rather than ACCEPTED. |
| [Real validation plan](PHASE_2A_REAL_LIBRARY_VALIDATION_PLAN.md) | Sections B–D, F–J; discovery, current scope, resource/operator decisions and independent pilot acceptance. |
| [Scan 4 readiness](PHASE_2A_SCAN4_PILOT_OPERATIONAL_READINESS.md) | Sections B–J; four-component planning freeze, reviewed discovery tooling, observation and distinct authorization gates. |
| [Historical S0 blocker](PHASE_2A_READ_ONLY_SOURCE_INSPECTOR_SYNTHETIC_IMPLEMENTATION.md) | Lines 190–213, 270–280; observer decision unresolved, UNKNOWN operation count, no claim that every observer is impossible. |

Production review included the seven requested files: SourceCapture, Safety,
PreflightRunner, Database, Manifest, FileSystem and ReportWriter. Specific methods
and ranges are cited below. Also inspected RelationshipAnalyzer, build provenance,
CLI routing, models, the production test fixtures and smoke scripts where needed
to establish integration, safety and coverage boundaries.

Offline review included Evidence, Provenance, Relationships, Reports,
ReportEvidence, IncrementalCsv, Reconciliation, WindowsNames' lexical boundary,
project/lock references, supporting functional/resource/hardening tests,
ScalabilityTests/StressExecution and ClosureInfrastructureTests. None supplies a
SQLite acquisition adapter. Existing TRX/audit inspection is described in K.

Evidence vocabulary:

- **CODE:** source/definition facts verified at the required HEAD.
- **HISTORICAL:** recorded previous execution/review claims, not newly run tests.
- **ARTIFACT:** existing owned evidence reread now; proves its contents/identity,
  not a fresh run or authentic production/native origin.
- **INFERENCE:** reasoned consequence, with its assumptions stated.
- **UNKNOWN:** environmental facts or unperformed experiments.
- **FUTURE:** separate human authorization and acceptance required.

## C. Implemented versus demonstrated guarantees

| Guarantee | CODE at current HEAD | Demonstration and outstanding boundary |
| --- | --- | --- |
| Protected Phase 1 capture | [Capture](../src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs), lines 17–79; native identity, read guard, readonly connection, latest completed scan, roots/charts/diagnostics and two fingerprints | HISTORICAL tests named in D. No pre-provider WAL-header refusal; no event oracle for transient operations. |
| Complete EXACT_ONLY admission | [Runner.Run](../src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs), lines 13–18, 45–76; [Analyzer](../src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs), lines 25–85 | Existing tests `ExactOnlySelectionPreservesPhysicalCopiesAndRejectsRelatedLabels` and changed-chart/whole-component cases support synthetic semantics. No current real selection authenticated. |
| Fresh direct-chart reads, metadata-only inventory | [ReadChart](../src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs), lines 32–81; Runner `Revalidate`, `Walk`, `Inventory`, `VerifyInventory` | Existing counted-read, drift, nested-membership and failure fixtures support this boundary. `non_chart_payload_reads=0` in [reports](../src/BmsLibraryAudit.Core/PhaseTwoReportWriter.cs), lines 93–98, is a declared invariant, not native telemetry. |
| Source/destination exclusion | [Safety](../src/BmsLibraryAudit.Core/PhaseTwoSafety.cs), `Inspect`, `Resolve`, `ValidateDestinations`, lines 29–137 | Historical Windows hardlink/SUBST/junction tests; distinct 8.3, UNC and alternative filesystem coverage not established. Current paths remain UNKNOWN. |
| Owned DB, immutable evidence and seals | [Database](../src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs), lines 38–104, 144–199; [Manifest](../src/BmsLibraryAudit.Core/PhaseTwoManifest.cs), lines 10–35, 43–91; Runner lines 81–98 | Existing constraint/tamper/canonical golden tests and historical smokes. A plausible seal or production recomputation is not an independent acquisition/preservation oracle. |
| Deterministic reports and receipts | ReportWriter `Write`, lines 14–37, 51–156 | Historical repeated exports and receipt/error tests. Re-export writes owned receipts; whole phase2.db hash can change while sealed digests stay equal. |
| Offline logical validation | [Evidence](../tools/BmsLibraryAudit.SourceInspector.Offline/Evidence.cs), lines 35–60; [Relationships](../tools/BmsLibraryAudit.SourceInspector.Offline/Relationships.cs), lines 12–19, 41–153; [Reconciliation](../tools/BmsLibraryAudit.SourceInspector.Offline/Reconciliation.cs), lines 16–128 | ARTIFACT 276 passing historical cases; independent small graph/byte oracles. Results always INSPECTOR_PARTIAL, native=false, authorization=false. |
| Complete report byte binding | [ReportEvidence](../tools/BmsLibraryAudit.SourceInspector.Offline/ReportEvidence.cs), lines 8–35, 83–114; [IncrementalCsv](../tools/BmsLibraryAudit.SourceInspector.Offline/IncrementalCsv.cs), lines 83–100 | Existing failed-stream, owned-buffer, substitution and truncation tests. A supplied Stream and digest do not authenticate a filesystem source, producer or complete SQL census. |
| No Phase 2B execution | [CLI RunPhaseTwo](../src/BmsLibraryAudit.Cli/Program.cs), lines 64–86; Database lines 171–174 | Hash route refuses before running; CONTENT insertion blocked. Resource-byte equality, comparison tables and mutation capabilities are unimplemented. |

Offline project has no PackageReference/ProjectReference; its lockfile's net10.0
dependency graph is empty. Offline tests reference only that library and approved
test packages. Core/CLI do not reference it; [solution](../BmsLibraryAudit.slnx)
contains only production projects/tests. It therefore neither hardens production
opening nor acts as a prerequisite called by preflight. Scoped handwritten-library
search found no filesystem-opening, SQLite, native, process or reflection-loading
API. `ResourceTests.AssemblyClosureHasNoProductionOrSqliteReferences`, lines 89–95,
is supporting historical runtime evidence; it was not rerun.

## D. PhaseOneSourceCapture WAL analysis

**Finding TRIAGE-01: `CONFIRMED_COVERAGE_GAP`.** The missing check is a verified
code fact. No provider write, recovery attempt, sidecar creation or real WAL state
has been demonstrated. `CONFIRMED_CODE_DEFECT` would overstate the available
behavioral evidence. Native behavior remains unresolved within this finding.

Exact current order in `PhaseOneSourceCapture.Capture`:

| Order | Location | Action |
| --- | --- | --- |
| 1 | Lines 17–18 | Normalize source path; ValidateDestinations with no captured library roots yet. Safety resolves source/destinations and checks native ancestors/aliases. |
| 2 | Line 19; Safety lines 101–109 | Reject currently existing -journal/-wal/-shm objects and reparse ancestors. This is existence policy, not a DB-header read. |
| 3 | Lines 20–21 | Inspect original native path facts; refuse directory source. |
| 4 | Lines 22–24 | Acquire FileMode.Open/FileAccess.Read/FileShare.Read guard; compare full original and guarded facts. |
| 5 | Line 25; Database.Connect lines 27–31 | Construct readonly, nonpooled SQLite connection with DefaultTimeout=1; Connect immediately calls Open and registers ORDINAL collation. There is no intervening header/version gate. |
| 6 | Lines 26–27 | First SQL: application_id and user_version before transaction creation. |
| 7 | Lines 28–55 | Begin deferred transaction; metadata, foreign_key_check, quick_check, latest completed scan, root/configuration queries; destinations checked against all captured roots. |
| 8 | Lines 56–57; Fingerprint lines 93–103 | First streaming SHA-256 of guarded file, with count/path/handle checks; test hook runs afterwards. This baseline is after provider.Open and several SQL reads. |
| 9 | Lines 58–74 | Read/validate all chart rows and selected diagnostics. |
| 10 | Lines 75–78 | Second fingerprint/path check, sidecar check, then transaction.Commit. These checks precede provider disposal. |
| 11 | Lines 79 and using declarations 22/25/28 | Return captured value, then dispose transaction, connection and guard in reverse declaration order. Guard covers provider disposal, but no explicit post-disposal fingerprint/sidecar observation follows. |

There is no read of the SQLite magic/100-byte header or format bytes 18/19 in
Capture, Safety or Connect. Thus a DB declaring WAL format with sidecars absent
is **not explicitly rejected before provider access**. It might fail later in the
provider; that would not establish pre-open refusal or zero forbidden attempts.
The header version interpretation and proposed 1/1-only profile are specified in
Inspector design lines 113–115 and 203–215; no new external/provider experiment
was used to infer shipped-binary behavior.

Tests actually present in [PhaseTwoPreflightTests](../tests/BmsLibraryAudit.Tests/PhaseTwoPreflightTests.cs):

- Lines 285–295, `SourceGuardPreventsConcurrentMutationAndCaptureFingerprintsMatch`:
  same-process write-open is denied at the **post-first-fingerprint** hook; final
  source bytes and stored digest agree. Not a cross-process whole-lifecycle oracle.
- Lines 311–321, `SourceSidecarsAndDirectoryCollisionsAreRefusedUnchanged`:
  three cases, journal file / WAL directory / SHM file. No WAL-header/no-sidecar case.
- Lines 323–340, `InvalidSourceProvenanceFailsBeforeDestinationWrites`: scan,
  version, application, comparison and digest refusal. These include SQL access;
  they do not prove rejection before provider.Open.
- Lines 557–565, `SourceBytesAndMtimesIncludingDatabaseRemainUnchanged`:
  final bytes/mtime equality and absence of surviving journal/WAL for one synthetic
  rollback fixture. No transient create/delete, temp access or close event coverage.

Source search across handwritten production/test files found no journal_mode WAL
fixture or DB-header/version test for this capture path. Historical test success
is narrower than the no-write invariant in contract lines 164–199. In particular,
two equal fingerprints taken after opening cannot independently prove the opening
did nothing; final absence cannot establish absence of transient operations.

Impact is directly on Phase 2A: `Runner.Run` line 13 calls Capture before selecting
components, before chart revalidation and before opening the owned destination
DB. A four-component pilot still takes this path. This is not limited to the
future auxiliary inspector. It does not retroactively invalidate Phase 1's recorded
validation or prove damage to any library. The smallest proposed preventive change
and its acceptance boundary are in I–J; production is unchanged now.

## E. Source Inspector B1–B6 status

All six are requirements for **native auxiliary acceptance**, not six new proven
production defects. Design lines 277–305 and 799–843 remain authoritative for that
workstream. No native inspector is implemented at current HEAD.

| Obligation / required guarantee | Existing implementation and synthetic support | Missing evidence / blocker kind | Exact next action and authorization |
| --- | --- | --- | --- |
| B1: actual loaded managed/native provider, deployment, connection/module/VFS correlation | Production locks Microsoft.Data.Sqlite 10.0.12; build SHA policy exists. Offline library uses no provider. Package names/build state do not identify the native module actually loaded. | Architectural and deployment evidence gap: inspector loading/profile adapter absent; no reviewed loaded-module allowlist/experiment. | FUTURE: approve exact deployment/profile and independent module/API correlation on owned fixtures. Separate native implementation/experiment authorization; no real DB permission follows. |
| B2: compatible retained guards, stable source/namespace identity and ordinary writer/delete exclusion | Production read guard/native checks; existing same-process source write denial and destination alias tests. Offline WindowsNames validates strings only. | Architectural/methodological gap: no auxiliary whole-window ancestor guards, cross-process stage barriers, measured handle/lock lifetime or leak proof. Not a demonstrated incompatibility. | FUTURE: accepted B1 profile plus scoped independent writer/controller and native observations through release. Needs separate authorization, approved fixtures and observer; retain quiescence limits. |
| B3: complete attributed forbidden-operation observation including recovery/sidecars/temp/closure | Production boundary sidecar checks and final preservation tests; no accepted native observer. Offline bindings expose filesystem-origin=false. | MISSING_COVERAGE; count UNKNOWN. Methodological/environmental decision open; historical token/tool limitations are not a current tool-impossibility finding. | FUTURE: reviewer-approved feasibility/coverage protocol with positive transient controls, PID/creation identity, loss counters and complete launch-to-drain window; then separately authorized experiments. No tracing/tool installation now. |
| B4: native cancellation, callback lifetime, interrupt and cleanup | Managed checkpoints and cancellation/no-false-completion tests; production using/disposal and Console.CancelKeyPress. No auxiliary SQLite progress/interrupt or callbacks exist. | Architectural and native evidence gap; no measured latency/lifetime/leaks. Blocking Stream.Read/kernel calls remain outside instant cancellation. | FUTURE: on accepted B1–B3 profile, implement/review finite callback/interrupt boundaries and run controlled cancellation/disposal fixtures. Separate authorization; a required hard deadline needs its own reviewed controller. |
| B5: finite enforced policy plus independent empirical envelope | Offline Evidence policy lines 45–60; relationship fanout checks before pair growth; stream/field/row limits; historical conditional stress. Production buffers/batches and 1-second provider wait are not a total resource policy. | Native configuration/calibration missing; no native heap/cache/temp/whole-window enforcement or accepted combined envelope. Counts fitting offline defaults do not establish production capacity. | FUTURE: freeze finite candidate limits/units/enforcement/overshoot, then calibrate with independent resource observations and B3 coverage. Separate implementation/measurement approval; no arbitrary safe values inferred here. |
| B6: independent correctness and preservation oracles | Offline hand-authored graphs, strict byte goldens and expected statuses; parser ownership/substitution tests; historical 276-case artifact. Production canonical primitive/full-group goldens also exist. | Logical subrequirements supported; independent actual SQL acquisition/storage types/catalog, producer/native receipt binding and external P0/P1/P2 preservation remain missing. | FUTURE: independently freeze SQL/schema/receipt/native fixture oracles and combine them with accepted B1–B5 evidence. Final closure review can occur statically; native acceptance needs separately authorized execution. |

B3's missing observation is not `FORBIDDEN_OPERATION_PROVEN`, `DROPPED_EVENTS` or
`INSTRUMENTATION_FAILED`: no run establishes those outcomes. No approved observer
means **UNKNOWN**, never zero. B6 is partly supported offline; it is neither wholly
failed nor wholly satisfied. None of these rows is marked native PASS.

## F. Phase 2A pilot prerequisite graph

Dependency reconstruction, rather than an additional contract:

```text
Repository/static review ──> production opening gap disposition
                         └─> offline closure independent review (separate area)

Reviewed discovery tool + its applicable safety acceptance
    + explicit exact-source/report/root-metadata authorization
    ──> current guarded Phase 1 discovery / full component freeze

Freeze + reviewed clean binary + native-disjoint new destinations
    + approved capacity/quiescence/observation/stop protocol + explicit pilot approval
    ──> four-component Phase 2A execution

Separate repeat-report/verification permission + reviewed Phase 2 evidence tool
    + actual seals/receipts/repeated bytes/preservation and adequate observation
    ──> independent pilot disposition
    ──> possible proposal for separately authorized fresh full Phase 2A

Full native Source Inspector acceptance: S0/S1 + B1–B6 ──> eligible auxiliary tool
Future Phase 2B: separate implementation/schema/accepted-workload authorization
```

Evidence for these arrows: contract sections 3–6 and 16; validation-plan lines
60–94, 127–191, 296–396, 398–447; Scan 4 lines 237–274 and 569–605; Inspector
design lines 10–15, 146–162, 792–845 and 863–870.

1. **Reviewing existing code** requires no source discovery, SQLite opening,
   observer or B1–B6 execution. This triage can finish now.
2. **Safe source discovery** requires reviewed no-recovery tooling and exact access
   approval. The selected auxiliary native inspector, if used, must meet its
   complete B1–B6 gates before real access. Its offline library cannot do acquisition.
3. **Authorizing the pilot** requires the frozen current source, production opening
   safety disposition, applicable reviewed tools, clean deployed executable,
   exclusions/capacity/quiescence and observation/stop plan. No current command
   calls Source Inspector or requires an INSPECTOR_VERIFIED receipt as a token.
4. **Independently accepting the pilot** additionally requires actual persisted
   evidence, repeated reports/receipts, independent seal verification and adequate
   write-observation coverage. Exit 0 alone does not satisfy this gate.
5. **Full Phase 2A** needs a new human scope/capacity decision and fresh capture;
   a pilot does not authorize automatic expansion.
6. **Native Source Inspector-only requirements** include its exact SQL allowlist,
   full RECONCILE catalog/format/receipt, native callbacks, single-link/canonical
   basename profile and resource calibration. These are not retroactively added
   production requirements merely because the auxiliary design is stricter.
7. **Phase 2B and later** own resource-byte hashing/comparison, dependency analysis,
   fusion/removal and new schemas. Their absence does not block metadata-only 2A.

**Important dependency limit:** contracts require a reviewed safe discovery path,
not specifically this one auxiliary executable. An alternative cannot bypass
discovery, safety, preservation or independent verification obligations; it would
need an explicit separately reviewed scope/approval. Under the currently selected
Inspector route, all native B1–B6 gates still apply. No alternative is approved here.

**TRIAGE-07 documentation ambiguity:** Roadmap lines 74 and 171 use broad
“source-inspector native blocker” ordering, while the subordinate Inspector design
lines 10–15, 166–167 and 845–869 separates its stricter acceptance from pilot gates.
The exact tool-specific dependency should be explicitly dispositioned by the human
reviewer before scheduling the native workstream; this report grants no shortcut.
Likewise, tracing is called optional in validation-plan lines 353–364 and Scan 4
lines 542–554, but adequate write-observation coverage is mandatory for accepted
pilot classifications (validation-plan lines 387–390; Scan 4 lines 561–564).
Selecting a particular tracer is optional; accepting missing preservation proof
is not. No specific alternative proof mechanism is approved in these documents.

### Four-component plan coherence and external prerequisites

The Scan 4 plan remains coherent **as planning**: complete components of sizes
2/2/3/3, ten roots and eight exact pairs. Its 1/7/3/2 expectations are distinct
hash counts, not physical counts; 31 is a root/hash-membership sum (Scan 4 lines
114–143, template lines 326–355). Analyzer lines 27–28 and 52–58 independently
confirm physical group.Count versus Hashes.Count. No physical count is invented.
The later Inspector design's 34 paths is a historical report observation, not a
new census or interchangeable with 31. Nested/Unicode/long-path representation
remains conditional, not a reason to reject a coherent planning document.

Cannot be resolved from repository inspection alone:

- Current source identity/bytes/stamps/sidecar/header state, SQL ownership,
  integrity, latest scan, all active/historical roots and unsettled writers.
- Current four-report identities, actual producer/runtime/legacy byte-profile
  provenance where required, current DB/report agreement, every chart path and
  complete closure of the selected labels. A changed latest scan requires new
  scope review, not silent reuse of Scan 4 navigation labels.
- Supported native source/output paths, canonical aliases/ancestors, filesystem
  and case mode; nonexistent DB and distinct report/log/trace/temp destinations.
- Exact deployed CLEAN binary/runtime/dependency identity and applicable code
  review. A clean current checkout does not attest previously built DLLs;
  writing this document makes a subsequent build dirty.
- Available memory/commit/free-space reserves, full-index/fanout cost, recursive
  union size, actual storage throughput, permitted observation overhead and stop
  thresholds. Four selected components do not bound full capture/analysis costs.
- Operational quiescence, operator/reviewer, observation coverage/controls and
  separate discovery, pilot, post-pilot verification and full-run authorizations.
- Actual completed source/preflight seals, four initial/repeat outputs and both
  receipt sets, source preservation and independently reviewed pilot disposition.

These are future checks, not failed measurements. No real input was probed here.
The offline native design is Phase 1-specific and does not itself implement the
read-only Phase 2 seal/receipt reviewer demanded by Scan 4 lines 515–528. That
verification tool also needs a reviewed scope; calling production report twice
alone is not independent validation of all persisted facts.

## G. Findings and blocker matrix

One underlying issue has one ID; B1–B6 detail facets of the native route rather
than six duplicate global pilot blockers. Severity describes potential impact;
priority follows dependency and smallest useful scope.

| ID / classification | Severity and practical impact | Evidence / test boundary | Milestone, required next action, smallest scope | Authorization / dependencies |
| --- | --- | --- | --- | --- |
| TRIAGE-01 — EVIDENCE_GAP; specific WAL disposition CONFIRMED_COVERAGE_GAP | HIGH: source may reach provider under unexamined WAL profile | Capture lines 17–28; Database.Connect 27–31; tests 285–295, 311–321, 557–565. Missing header/no-sidecar regression and native event proof; no reproduced forbidden operation. | Production source-capture safety before real pilot approval; narrow pre-open header gate plus deterministic refusal regression, J. Does not close TRIAGE-03. | Separate production correction and owned synthetic tests; no real/native observer access. No implementation dependency on native inspector. |
| TRIAGE-02 — CONFIRMED_BLOCKER | HIGH: current documented discovery/tool-selection gate explicitly BLOCKED | Scan 4 lines 255–270, 515–528; offline Evidence 38–40; dependency graph and absent native acquisition adapter. No approved operational tool established by inspected evidence. | Real discovery, then independent pilot verification; accept exact safe acquisition/reviewer tooling. Native Source Inspector is one selected route, not an implicit production dependency. | Separate tool review/implementation/native tests, then exact real access approval. Inspector route depends on E; production route also needs TRIAGE-01 disposition. |
| TRIAGE-03 — EVIDENCE_GAP | HIGH: absence of transient forbidden operations cannot be asserted | Historical S0 lines 190–213; closure 275–288; design B3 292–304; pilot H. UNKNOWN, not proven mutation. | Native acceptance and trustworthy pilot-preservation proof; approve feasible observer/coverage/positive-control protocol, then bounded experiments. | Separate observation research/execution and any tool/privilege decision. B1/B2 profile must be bound to later observations. No tool discovery or native experiment now. |
| TRIAGE-04 — EVIDENCE_GAP | HIGH: current source and frozen components may differ from history | Scan 4 lines 133–156, 237–294, 587–605; no real artifact read in this task. Historical source/report counts are context only. | Discovery/pilot approval; reviewed guarded current DB/report/closure check and approved ten-path freeze. | Exact real DB/report/root-metadata authorization after TRIAGE-02; no automatic new scan/report/copy. |
| TRIAGE-05 — CONDITIONAL_BLOCKER | HIGH: unsupported aliases, dirty/substituted build or unsafe destinations invalidate scope | Safety Inspect/Resolve/ValidateDestinations; build provenance lines 5–16 and Directory.Build.targets lines 4–25; Scan 4 C–D. Current deployment/path support UNKNOWN. | Pilot approval; identify accepted CLEAN bytes/runtime and native-disjoint locations. Distinct 8.3/UNC uncertainty matters only if required by chosen profile. | Separate clean-build/deployment and exact metadata checks; no safety waiver or commit manufactured for testing. Depends on protected roots from TRIAGE-04. |
| TRIAGE-06 — CONDITIONAL_BLOCKER | MEDIUM/HIGH: small selection still copies/analyzes full chart index; unsupported stop requirement | Capture Read/materialized arrays; Analyzer 20–63; Runner 13–18, 239–315; Scan 4 lines 434–489. Historical EXTENDED has only 2000 charts; offline 250k/1m results exercise different code. | Pilot approval; approve measured operational reserves and cooperative stop plan. Hard time/memory guarantee, if required, needs its own separately reviewed enforcement. | Separate bounded calibration/controller work only if concrete required budget cannot otherwise be justified. Do not demand full Inspector B5 for every production activity. Depends on profile/scope, TRIAGE-04/05. |
| TRIAGE-07 — DOCUMENTATION_AMBIGUITY | MEDIUM: overly broad inspector dependency or “optional tracing” can misdirect scheduling/acceptance | F cites Roadmap, Inspector design and both pilot plans with separate scopes. | Gate scheduling/independent acceptance; reviewer explicitly dispositions tool-route dependencies and sufficient observation proof; any later document edit needs exact scope approval. | Human decision; no governing text changed here. Cannot waive TRIAGE-02/03 or Inspector B1–B6. |
| TRIAGE-08 — EVIDENCE_GAP | MEDIUM: publication can be confused with independent offline acceptance | Closure lines 323–329; published by 7bd4605; Roadmap line 38. Historical favorable review is user-context claim, not a signed acceptance attached here. | Offline closure handoff / eventual native integration; independent review of published six-file closure and oracles with explicit disposition. | Separate review decision; may proceed statically without native execution. Not a new production-code defect or automatic barrier to unrelated repository review. |
| TRIAGE-09 — DEFERRED_NONBLOCKER | Later-phase safety significance; no impact on bounded metadata-only pilot scope | CLI 68; Database 171–174; contract sections 8–9/14; Roadmap phases 3–9. | Phase 2B/content equality, dependencies and mutation; separate future contracts/implementations, not this next task. | Explicit later phase authority and sealed workload where applicable; no current execution. |
| TRIAGE-10 — ALREADY_SATISFIED (bounded implementation only) | No open implementation blocker for existing pure logic and 2A mechanisms | C source map, existing tests and historical artifacts. Complete components, physical multiplicity, exact thresholds, finite offline policy, strict byte bindings, seals and separate receipts implemented. | Repository review and synthetic semantic support. Retain existing oracles; no general refactoring or further offline scaling justified by this triage. | No correction needed here. Does not satisfy real environmental or native evidence gates. |

## H. Priority and dependency assessment

TRIAGE-01 is the smallest concrete production-path omission relevant to the next
real milestone; it can be addressed without waiting for a full native auxiliary
architecture. Fixing it reduces an avoidable opening risk but does not establish
zero operations, reviewed discovery tooling or pilot readiness. TRIAGE-02/03
remain independent subsequent gates. TRIAGE-04/05/06 require operational evidence
and authorization, rather than speculative repository rewrites.

| Candidate next intervention | Assessment |
| --- | --- |
| Narrow production Capture correction | **Selected.** Verified absence of a pre-open header gate on the pilot's actual path; deterministic small negative fixture and positive rollback regression can establish the intended refusal order. |
| Further independent offline Inspector review | Useful separate review for TRIAGE-08; no new offline defect demonstrated. Repeating 276 cases or enlarging scaling does not fix production opening. |
| Native observer feasibility/acceptance research | Necessary for TRIAGE-03 and selected native Inspector route, but broader environmental/methodological work. The header gate does not depend on solving it first. It must precede any later claim of zero native forbidden operations. |
| Only an additional WAL synthetic regression | Useful counterexample but current code has no explicit policy to make refusal deterministic before provider access. Include it with the small preventive correction rather than call provider-dependent failure a sufficient pass. |
| Resolve contract/document dependency ambiguity | Human disposition is needed for scheduling, but no contradictory production implementation requirement or safe-opening alternative is demonstrated. Document-only wording cannot substitute for the concrete capture gate or preservation proof. |

Severity of possible source mutation is high; that does not mean implementing the
entire independent inspector first is the smallest useful action. No safety gate
is removed by this prioritization.

## I. Recommended single next technical task

**Production PhaseOneSourceCapture pre-provider rollback-header gate with bounded
synthetic regression and independent review.**

Under the already acquired/identity-checked read guard, validate exactly the bounded
SQLite header and require supported rollback read/write format versions before
Connect is invoked. Refuse WAL even with no sidecars; refuse malformed/truncated
or unsupported/mixed versions without provider access, destination creation,
recovery or fallback. Supported existing rollback capture must continue to work.

This is a proposed conservative restriction justified by the coverage gap, not a
claim of reproduced SQLite mutation. Independent source review plus an explicit
provider-boundary invocation oracle can prove the new refusal path. They cannot
prove whole-lifecycle native nonmutation. Native observations, current discovery,
resource approvals and pilot permission remain pending. Implement nothing now.

## J. Future Codex task specification

**DOCUMENTATION ONLY — NOT CURRENT IMPLEMENTATION AUTHORIZATION.**

Task: `PHASE_2A.SOURCE_CAPTURE.PREOPEN_ROLLBACK_HEADER_GATE`.

**Objective:** close TRIAGE-01's explicit pre-provider refusal gap on the production
capture path. Implement the smallest bounded gate under the existing read guard:
100-byte SQLite header, exact magic, both read/write format bytes equal to 1.
Refuse 2/2 WAL without sidecars, mixed/unknown versions, wrong magic and insufficient
header bytes before any source SQLite construction/opening or SQL. A short read
must be handled correctly; unavailable/cancelled input must not become accepted.
Restore stream position as needed for existing fingerprint semantics. Preserve
source identities, source/destination exclusion, scan validation, seals and normal
rollback behavior. Do not generalize this into the native Inspector protocol.

**Required baseline:** production reviewed at
`63f5be6fdfbe05caeef37b8c2783f11473b0599e`, branch main. Before implementation,
record HEAD/origin/main/status and read current AGENTS/context/sources/README and
this triage. At that exact HEAD, this expected untracked triage document may be
preserved. If human review has subsequently committed documentation or any code,
identify the exact diff and obtain the separately approved new baseline before
proceeding; never silently replace the requirement, reset or commit to manufacture
a clean test revision. Preserve every pre-existing change.

**Permitted files:**

- `src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs` only for bounded header
  validation and placement. If essential to prove no provider invocation, a
  narrowly scoped internal test seam at the existing provider boundary is allowed;
  production uses its original default behavior. No public arbitrary connection
  string/factory, CLI option or new runtime mode.
- `tests/BmsLibraryAudit.Tests/PhaseTwoPreflightTests.cs` for exact regression cases,
  preserving all current assertions; alternatively one strictly necessary new
  test-only file in that project for header/stream and capture-order tests.

**Explicit exclusions:** no changes to other production components, Phase 1
scanner/database/schema, CLI, PhaseTwoDatabase/Safety/Manifest/ReportWriter,
PhaseTwoModels public hooks, offline library, contracts, manifests, dependencies,
locks, build provenance or historical documentation/evidence. No native Inspector,
observer/tracing/tool installation/elevation, real source/report/library/simulator
access, real pilot/full run, Phase 2B, migrations, recovery/checkpoints, source
copies, readonly-mode fallback, production resource-policy redesign or benchmarks.
Do not move fingerprint/transaction/disposal stages merely to implement the header
gate; their remaining observation limitations stay explicit. No staging/commit/push.

**Synthetic authorization needed:** permission for the listed production edit and
bounded tests using owned repository fixtures only, including synthetic SQLite
and existing synthetic CLI smokes. Inspect inputs/outputs/dependencies/cleanup
before running. Existing Windows tests may use their already implemented owned
native handles/links and temporary SUBST fixture; do not add native experiments,
change persistent settings or request elevated privileges. If that fixture cannot
run safely within its owned scope, stop that validation and report it. New regression
evidence must be retained separately; no deletion/overwriting of historical artifacts.

**Required tests and oracles:**

1. Hand-authored header byte vectors: valid magic/1–1, 2–2, 1–2, 2–1, zero/unknown
   versions, wrong magic and lengths below 100. No external/third-party DB fixture.
2. Bounded Stream short-read, unexpected EOF, known read failure and cancellation;
   no fabricated completion or unnoticed position corruption. Do not exhaust RAM.
3. Owned synthetic rollback source accepted with unchanged current capture/results.
   For refused cases, edit only the header of a new owned fixture before its baseline
   or use literal header bytes; do not recover/checkpoint/delete sidecars to prepare it.
4. Independent test-side counter at the source provider boundary is **zero** for
   every refused header and reached for the valid rollback control. Source bytes,
   hash/mtime/native identity and sidecar absence/collision objects preserved;
   destination DB/reports absent for refusal. Refusal exception alone is insufficient.
5. Retain existing source-guard, sidecar, provenance, component multiplicity,
   staleness, canonical seal, receipt/export and cancellation regressions.
6. Locked solution restore, Release build, complete production Release test suite
   with a uniquely named TRX, Phase 1 and Phase 2A owned synthetic CLI smokes,
   `git diff --check` and independent new-file whitespace/scope audit. Report actual
   counts/exits, infrastructure failures and authorized retries. Do not rerun
   STANDARD/EXTENDED or offline scaling solely for this production correction.

**Independent acceptance criteria:** reviewer confirms bounded guard-based header
read is before the first source provider boundary, no alternate source opening
can bypass it, all rejected patterns demonstrate zero invocation and no destination
writes, valid rollback and existing tests/smokes pass with exact preserved oracles,
scope/dependency/manifests remain unchanged and all observed limitations are stated.
Provider-boundary counters are managed control-flow evidence, not B3 OS telemetry.
Neither this acceptance nor a passing synthetic suite authorizes real discovery,
closes B1–B6 or establishes zero native sidecar/temp/closure operations.

**STOP conditions:** baseline mismatch without disposition; unexpected changes;
need for schema/manifest/provider/dependency changes or a broader source/native
protocol; unsafe fixture paths/cleanup; source mutation or header refusal reaching
provider; unsupported required environment/permissions; inability to demonstrate
the stated test oracle. Do not weaken the gate or broaden authorization to force
validation. Preserve failures and report; stop after validation for human review.

**Expected report:** baseline/initial status; exact diff/file inventory; header
policy and placement; independent refusal/positive oracles; commands and actual
counts/exits; source preservation and scope audit; infrastructure versus functional
failures; residual native/production limitations; final Git status and explicit STOP.
Report readiness for independent review, not real-source/pilot acceptance.

## K. Validation performed and limitations

**No tests, restore, builds, CLI smokes or benchmarks were run in this triage.**
This document-only assignment does not change executable code; inspected tests
cannot answer the missing pre-open/native-event question without new fixtures or
experiments. Avoided the full production suite: its existing native alias cases
invoke mklink/SUBST and fixture cleanup (tests lines 493–553, 598–617), beyond a
necessary static triage. Offline suite/sampler runs would only repeat already
available evidence and do not exercise production Capture.

Executed read-only Git baseline/log/status/diff checks, bounded source/document
reads and targeted searches. A first PowerShell wildcard search was rejected as
an invalid rg path; it was repeated correctly with explicit handwritten file paths
and obj exclusions. This was a search-command issue, not a test/build failure.
The first final Git whitespace check in the restricted context returned
`fatal: this operation must be run in a work tree`. The read-only Git checks were
then repeated successfully in the normal host context through the approved sandbox
override; this did not request administrator/UAC elevation or change Windows settings.

Reread only these existing owned offline artifacts:

| Artifact | ARTIFACT checks performed now |
| --- | --- |
| [offline-research-closure.trx](../.artifacts/source-inspector-offline/offline-research-closure.trx) | 464935 bytes; SHA-256 `BFCA443C0013F92C27D608FB02D7C3583EA8B1FAE8AA01176A7F4DCC8C3A04A6`, equal to closure line 222. Bounded XML parsing with DTD/external resolution prohibited: Completed, 276 executed/passed, 0 failed/not executed; 276 result rows, 17 ScalabilityTests rows, 17 raw stress marker lines. |
| [offline-research-closure-audit.json](../.artifacts/source-inspector-offline/offline-research-closure-audit.json) | 20701 bytes; SHA-256 `BD2C4B450FD0C7BD18F0B2178E5115C6D5539202D74A85B0201309F51E56941F`, equal to closure line 223. Parsed source digest agrees with TRX. Accepted=true, 13 workloads executed, 9 full-data, 4 incomplete, 0 omitted/environment-blocked, 1 planner, 3 controls, 2 expected cancellations, 1 expected resource stop. |

These are preserved historical run artifacts. The independent verifier's prior
execution is documented in closure lines 185–218; it was **not executed again**
now. Parsing its existing output is not a new verifier run, package/runtime
attestation or independent validation of every historical test assertion.
The production 107-case and EXTENDED results remain HISTORICAL report claims;
their databases or benchmarks were not opened/rerun. Prior independent-review
claims were not substituted for a newly supplied signed acceptance decision.

Final document validation: strict UTF-8; relative repository/artifact targets
resolve within the repository; cited source/test ranges checked against current
files; internal section/ID references, classification/dependency/status wording,
table widths and balanced code fences reviewed. `git diff --check` passes; separate
`git diff --no-index --check -- NUL docs/PHASE_2A_BLOCKER_TRIAGE_2026_10_14.md`
checks the untracked addition. Existing tracked/cached diffs remain empty. No
production, existing document, project/lockfile or historical evidence was written.
HEAD and origin/main remain the initial required SHA; no commit or push occurred.

Final `git status --short`:

```text
?? docs/PHASE_2A_BLOCKER_TRIAGE_2026_10_14.md
```

No real library, real Phase 1 source DB/backup/reports, simulator DB, native
observer/tracing, Phase 2B functionality or source mutation was accessed/executed.
No SQLite connection was opened in this triage, including synthetic/in-memory.
No real phase2.db, new fixture or native experiment was created. The only write
is this authorized technical findings document.

## L. Final recommendation and STOP

- **Confirmed blocker:** TRIAGE-02, the documented reviewed discovery/tooling gate
  is unresolved. Native auxiliary acceptance remains blocked by unfulfilled E;
  a current real-source approval is also absent.
- **Unresolved evidence gaps:** TRIAGE-01 pre-open WAL coverage, TRIAGE-03 native
  observation/preservation, TRIAGE-04 current provenance/selection and TRIAGE-08
  formal offline acceptance. Conditions in TRIAGE-05/06 require operational checks;
  UNKNOWN is not a failed measurement or zero-operation proof.
- **Not actual bounded-pilot implementation blockers:** existing offline algorithms
  and 2A mechanisms (TRIAGE-10), deferred Phase 2B/mutation (TRIAGE-09), historical
  proposed/uncommitted/PENDING labels, or absence of arbitrary universal scalability.
  TRIAGE-07 requires scope disposition, not an automatic full auxiliary rewrite.
- **Exactly one recommended next technical intervention:** I–J's production
  pre-provider rollback-header gate with bounded independent regression.
- **Exact permission needed next:** explicit approval to edit the listed Capture/
  test paths and run the specified owned synthetic SQLite/unit/CLI validation.
  That approval must exclude real-source access, native tracing, pilot/full 2A,
  Phase 2B and Git publication. None is granted by this triage.

**STOP. Await human review and explicit authorization. The future task above has
not been executed.**
