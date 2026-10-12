# Independent Phase 1 read-only source inspector design

Design date: **2026-10-12** (America/Santiago).
Phase: `PHASE_2A.READ_ONLY_SOURCE_INSPECTOR.DESIGN_AND_SECURITY_AUDIT`.
Review addendum: **2026-10-12**, `PHASE_2A.READ_ONLY_SOURCE_INSPECTOR.DESIGN_REVIEW_CORRECTION`.
Baseline: `932bcf220be96e731d941c07b000b4745a602bd4`.

**DESIGN ONLY. NO INSPECTOR IMPLEMENTATION OR EXECUTION IS AUTHORIZED.**

**Heuristics discover. Exact evidence proves. Verified plans mutate.**
This proposal is an auxiliary verification design, subordinate to the
[normative contract](PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md),
[real validation plan](PHASE_2A_REAL_LIBRARY_VALIDATION_PLAN.md) and
[Scan 4 operational plan](PHASE_2A_SCAN4_PILOT_OPERATIONAL_READINESS.md).
It changes none of their manifests, schemas, admission rules or authorization gates.

## A. Verified baseline and scope

Before design work, branch was `main`; HEAD and origin/main matched the baseline;
`git status --short` was empty. Latest commits: `932bcf2`, `7f97402`, `743d2f1`,
`a3b3e63`, `808a19e`. No index/history/branch operation was performed.

At this correction's entry, `main`, HEAD and origin/main still matched the exact
baseline. The sole working-tree entry was this expected untracked design document;
there were no tracked or staged changes. This addendum refines the accepted-in-principle
Option B specification only. It grants no implementation or execution permission.

Authority reviewed: [AGENTS.md](../AGENTS.md), [MASTER_CONTEXT.md](../MASTER_CONTEXT.md),
[README.md](../README.md), [SOURCES.md](SOURCES.md),
[Phase 1 validation](PHASE_1_REAL_LIBRARY_VALIDATION.md),
[implementation report](PHASE_2A_IMPLEMENTATION_REPORT.md),
[hardening/performance report](PHASE_2A_CORRECTIVE_HARDENING_AND_PERFORMANCE_REPORT.md),
the contract/plans above and the source/test references in B.

| Evidence class | Meaning |
| --- | --- |
| SOURCE-VERIFIED | Code, repository metadata or published provider source inspected in this design task. A source branch is not a deployed-binary experiment. |
| PRIOR-OBSERVED | Facts independently obtained in the preceding authorized readiness task, retained here as historical context. |
| PROPOSED | Required future inspector behavior, not an implemented guarantee. |
| TEST-GATE | A hypothesis or control requiring future synthetic validation and independent review. |
| UNKNOWN | Evidence unavailable; never encoded as successful, zero or absent. |

The preceding readiness assessment was `READ_ONLY_READINESS_PARTIAL`. Its existing
report evidence was internally consistent: 243483 verified charts, 154925 unique
SHA-256, 19925 analyzed folders, 13149 relationships, 417 EXACT_ONLY components,
844 exact root memberships and no source warnings/errors. Four planning components
were `EXACT_ONLY_000003`, `EXACT_ONLY_000004`, `EXACT_ONLY_000054`, `EXACT_ONLY_000068`:
ten roots, eight exact pairs, 31 root/distinct-hash memberships and 34 chart paths.
These are prior report observations, not a new SQL or filesystem verification.

Historical locations only, **not accessed during this task**:

- Phase 1 DB: `C:\BmsLibraryAuditData\audit.db`.
- Reports: `C:\BmsLibraryAuditData\reports\phase1-scan4-20261011_223743`.
- Library: `E:\Juegos\beatp\canciones`.

The prior DB observation was 171872256 bytes and SHA-256
`4bfaf45bd4a6ac693db2fdbc6befc2c935beb416222751e2d7f5dbaabd1e6d5b`.
Neither is a timeless expected value. The pre-Scan-4 backup digest is never a
current-source criterion. Ownership through SQL, integrity, foreign keys, all
root records, latest COMPLETED scan and current-index/report agreement remain
unverified. No real DB, backup, report directory or library metadata was reopened
for this design. Historical EXTENDED SUCCESS is supporting synthetic evidence,
not an inspector test or a runtime forecast.

## B. Existing implementation analysis

| Source inspected | SOURCE-VERIFIED behavior | Reuse decision / independent check |
| --- | --- | --- |
| [PhaseOneSourceCapture](../src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs) | Rejects sidecars; native path/handle comparison; read guard denies write/delete sharing; read-only, nonpooled connection; deferred transaction; ownership/quick/FK checks; current scan, all chart rows and roots; two bounded fingerprints. Guard ends when capture returns. Initial fingerprint is after opening and some SQL reads. | Use as a review comparator only. Inspector fingerprints before SQLite, retains guard through its full observation window, checks header/WAL and unexpected scans separately. No call to Capture or reflection into it. |
| [PhaseTwoDatabase](../src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs) | Connect accepts a caller-selected mode, pooling=false and timeout=1; Open supports writable reservation/initialization; validates Phase 2 schema via an in-memory initializer. | Never reuse Open/Connect/ExpectedSchema. Their ownership and write responsibilities are not Phase 1 inspection. |
| [PhaseTwoSafety](../src/BmsLibraryAudit.Core/PhaseTwoSafety.cs) | Native disk handles, volume-GUID final paths, volume/file IDs, link counts and case-mode queries; rejects reparse ancestors/ADS; resolves missing suffixes. Metadata handles use sharing 7. Destination logic can enumerate output directories. | Reuse platform API concepts, not this entire class. Add explicit filesystem/canonical-basename checks and retained ancestor guards in the auxiliary native boundary; validate independently. |
| [AuditDatabase](../src/BmsLibraryAudit.Core/AuditDatabase.cs) | ReadOnly branch, pooling=false, ownership probe, connection-local foreign_keys; also exposes initialization, writable Open, arbitrary Execute and scan mutations. Snapshot selects latest completed scan plus mutable current chart rows. | No dependency on this class. Freeze an independently reviewed schema-1 specification; never expose its write-capable API. |
| [PathPolicy](../src/BmsLibraryAudit.Core/PathPolicy.cs) | GetFullPath, Windows invariant uppercase/ignore-case comparison, reparse checks and lexical containment; Absolute itself can accept relative input. | Inspector independently requires fully qualified ordinary paths before normalization. Lexical comparison is not native alias proof. |
| [RelationshipAnalyzer](../src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs) | Current/OK rows and source diagnostics govern trust/incomplete folders; physical counts use group.Count; distinct hashes use a set; inverted-index edges; ordinal labels and connected components. | Independently reconstruct the declared mathematics and compare full tuples; do not call Analyze/Classify/UnionFind. |
| [PhaseTwoManifest](../src/BmsLibraryAudit.Core/PhaseTwoManifest.cs) | Domain-tagged source/component/preflight encodings bind persisted Phase 2 evidence, with explicit ordering and no Unicode normalization. | Inspector does not invoke it, create Phase 2 facts or invent a preflight seal. Any inspector receipt is a different format. |
| [ReportWriter](../src/BmsLibraryAudit.Core/ReportWriter.cs), [models](../src/BmsLibraryAudit.Core/Models.cs), [scanner](../src/BmsLibraryAudit.Core/ChartScanner.cs) | Exact CSV fields, summary projection and scan statistics. Non-OK chart rows can legitimately contain size=-1, mtime=0 and no hashes. UNSTABLE increments Errored too. | Independent parser/projection; do not incorrectly reject all unknown error-row metadata or double-count Unstable as another Errored population. |

Existing [Phase 2 tests](../tests/BmsLibraryAudit.Tests/PhaseTwoPreflightTests.cs)
cover a concurrent write-open denied during capture, sidecar/file/directory
collisions, wrong provenance, historical roots, multiplicity, aliases and source
preservation. [Phase 1 tests](../tests/BmsLibraryAudit.Tests/PhaseOneTests.cs)
cover cache hints, incomplete folders, schema/platform and root history.
They were inspected, not run. A previous pass is not proof of the new auxiliary
opening procedure, exact native DLL, race schedule or cancellation implementation.

### Provider and native opening research

[Lockfile](../src/BmsLibraryAudit.Core/packages.lock.json) fixes Microsoft.Data.Sqlite
and Core 10.0.12, with SQLitePCLRaw components 2.1.12. No new dependency is proposed.
Future acceptance must pin the exact managed/native bytes, SQLite engine version,
source ID, VFS and compile options; a package number alone does not prove those.

Primary sources reviewed, with findings limited to source/documentation:

- [Provider opening, v10.0.12](https://raw.githubusercontent.com/dotnet/efcore/v10.0.12/src/Microsoft.Data.Sqlite.Core/SqliteConnectionInternal.cs):
  ReadOnly selects SQLITE_OPEN_READONLY; Private selects a private cache. URI names,
  password options, ForeignKeys and RecursiveTriggers alter setup. Forbid caller
  connection strings, URI/encryption options and custom VFS; explicitly select options.
- [Provider factory, v10.0.12](https://raw.githubusercontent.com/dotnet/efcore/v10.0.12/src/Microsoft.Data.Sqlite.Core/SqliteConnectionFactory.cs):
  Pooling=false selects a nonpooled connection. Dispose must still be checked.
- [Provider transactions, v10.0.12](https://raw.githubusercontent.com/dotnet/efcore/v10.0.12/src/Microsoft.Data.Sqlite.Core/SqliteTransaction.cs):
  Serializable+deferred starts BEGIN; nondeferred starts BEGIN IMMEDIATE. Select
  deferred explicitly; no savepoints, write transaction or isolation change.
- [Provider commands, v10.0.12](https://raw.githubusercontent.com/dotnet/efcore/v10.0.12/src/Microsoft.Data.Sqlite.Core/SqliteCommand.cs):
  Cancel is a no-op; CommandTimeout addresses lock waits, not overall query cost;
  commands can contain multiple statements. Inspector must impose its own boundaries.
- [SQLite WAL](https://www.sqlite.org/wal.html): read-only WAL access can require
  auxiliary files. Reject WAL before opening; ReadOnly is not a sidecar policy.
- [SQLite rollback locking](https://www.sqlite.org/lockingv3.html): a read can
  encounter recovery work for a hot journal. Reject every journal object, not
  merely journals deemed hot; no recovery fallback.
- [SQLite format](https://www.sqlite.org/fileformat.html): header bytes 18/19
  identify rollback (1/1) versus WAL (2/2). Use a bounded pre-open check, not a
  substitute for SQL ownership/integrity verification.
- [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)
  and [upstream Windows VFS](https://raw.githubusercontent.com/sqlite/sqlite/master/src/os_win.c):
  requested access and existing sharing restrictions both matter. Upstream main-DB
  ReadOnly opens use read access and read/write sharing. This suggests guard
  compatibility; the mutable upstream branch does not attest to the shipped DLL.
  Exact-package synthetic handle tests remain mandatory.
- [SQLitePCLRaw 2.1.12 bindings](https://raw.githubusercontent.com/ericsink/SQLitePCL.raw/v2.1.12/src/SQLitePCLRaw.core/raw.cs)
  expose readonly/filename queries, authorizer, progress/interrupt and resource-limit
  interfaces. Their delegate lifetimes and native behavior require auxiliary tests.

**Unresolved production safety-coverage finding requiring separate review.** The
current Capture opens SQLite after sidecar rejection without first refusing a
WAL-mode header (see its RejectSidecars/Inspect/guard/Connect sequence). Its tests
do not establish WAL-without-sidecars safety or observe transient sidecar events.
Published WAL behavior makes this a concrete coverage/security question for an
independent production review. No current-source WAL state, sidecar creation or
production mutation is asserted; no repair is authorized here.

## C. Architectural alternatives and selected design

| Criterion | A: separate executable wrapping Core services | B: independent diagnostic project, selected | C: existing SQLite CLI/other external inspection |
| --- | --- | --- | --- |
| Code independence | Low when capture/analyzer/database wrappers are reused; shared errors can agree. | Independent schema, SQL catalog, reconciliation, graph logic and result model. | Engine-independent only if another implementation is used; often same SQLite engine with a different wrapper. |
| Attack surface / accidental writes | Core exposes scanner, Phase 2 writer and mutable DB APIs; reflective access is undesirable. | No Core/CLI reference or reflective loading; narrowly scoped native facade and fixed SQL. | Shell/GUI defaults, connection strings, recovery and extension behavior broaden the audit. |
| Opening behavior | Coupled to production assumptions and Phase 2 option/destination checks. | Explicit D procedure; no creation mode, recovery or writable DB connection. | Nominal readonly/immutable flags insufficient without a separately audited guard and opening procedure. |
| Dependencies / Windows | Existing .NET 10 packages, but large application dependency. | Existing locked Microsoft.Data.Sqlite/SQLitePCLRaw plus .NET/Win32; local ordinary NTFS v1. | Installation/version drift or an unreviewed native guard may be required; not currently demonstrated. |
| Testability | Easy happy path, weak independence of oracle. | Independent fixtures, negative injections, native I/O observation and resource tests. | CLI/UI recovery and handle control complicate deterministic fault injection. |
| Complexity / maintenance | Less initial code; tight production coupling. | More initial specification/tests; small explicit compatibility surface. | Tool-specific deployment/settings/behavior must be maintained and reviewed. |
| Evidence preservation | Conditional on wrappers and their reachable write paths. | No source write API; guards, pre/post fingerprints, no-recovery gates and reviewed publication. | Conditional on independently demonstrated opening, sidecar and output behavior. |

**Recommend exactly Option B.** A future independent .NET 10 auxiliary executable
will reference only the already locked SQLite dependencies and standard platform
libraries. It will not become a production command, run preflight/report, replace
PhaseOneSourceCapture or modify source/schema/manifest code.

Proposed boundaries: argument/policy validation; native path/handle inspection;
guarded readonly connection; immutable SQL catalog; independent evidence model;
independent relationship/report reconciliation; receipt publisher. No arbitrary
SQL, writable connection factory, directory walker or chart reader is included.

**Shared project code: none in v1.** Reused primitives are Win32 calls already
identified in PhaseTwoSafety: CreateFileW, handle metadata, final-path resolution
and directory case-mode queries; .NET SafeHandle/streaming hashing; existing
SQLite packages. The auxiliary facade independently binds these platform APIs.
Production source/tests are review comparators, not linked source, runtime services
or sole test oracles. Do not copy third-party implementations. Any later proposal
to extract/share project code changes this design and needs separate review.

## D. SQLite safety and native filesystem contract

All requirements here are PROPOSED, with TEST-GATE before real access. They are
stricter auxiliary acceptance restrictions, not changes to the production contract.

### Ordered opening and closure protocol

1. **Explicit scope and policy.** Require exact approved DB/report inputs, requested
   mode, expected current scan when reconciling a frozen report set, finite resource
   limits and output policy. Windows only; v1 supports proven local fixed NTFS.
   Reject missing inputs, unsupported filesystems, UNC/network/cloud/reparse or
   case-sensitive paths and unresolved native facts. No automatic alternative mode.
2. **Ordinary source path.** Require a fully qualified ordinary drive path before
   GetFullPath. Refuse URI/file: names, :memory:, DataDirectory macros, ADS, device
   prefixes, ambiguous trailing-dot/space components and unresolved dot segments.
   Preserve authored spelling. No filesystem call on a chart path from a DB row.
3. **Identity and namespace.** Open metadata handles without following reparse
   targets; validate every necessary existing ancestor, directory case mode, disk
   kind, filesystem, volume-GUID resolved path and volume/file ID. Resolve the
   ordinary canonical long-name DOS source basename, verify its handle identity
   and retain both spellings. v1 rejects a supplied short-name/SUBST alias requiring
   source-path remapping rather than silently changing SQLite's sidecar basename.
   Never send a user-controlled internal/device path to the provider.
4. **Single-link source.** Require a regular DB with native link count exactly one.
   Reading a hardlink is not itself a write, but multiple basenames can hide
   different journal locations. Refuse instead of inspecting guessed aliases or
   collapsing identities. This does not alter production chart-path multiplicity.
5. **Sidecars before SQLite.** Check exact canonical source -journal/-wal/-shm
   locations and any supplied spelling still relevant to validation. An ordinary
   file, empty file, directory, dangling reparse entry, access denial or ambiguous
   absence is BLOCKED. Only definite file/path-not-found counts as absence.
   Do not read a journal target, recover, delete, truncate, checkpoint or copy DBs.
6. **Retained guards.** Acquire FileMode.Open/FileAccess.Read/FileShare.Read with
   a bounded sequential stream. Compare native path/handle identity, resolved
   name, size, mtime, attributes and link count. Sharing failure is SOURCE_BUSY.
   Retain required ordinary ancestor metadata handles denying delete sharing
   where the reviewed native implementation can establish this; unavailable
   protection is BLOCKED, not a fallback to weaker checks. Validate this guard
   arrangement against the provider's own handles in G, including its lock calls.
7. **Header and initial fingerprint.** Under guards, read the 100-byte SQLite
   header, validate magic/length and require read/write format versions 1/1.
   WAL 2/2, mixed/unknown versions or encryption/unsupported headers are refused
   before provider.Open. Header app/user IDs can be recorded as preliminary facts,
   then independently checked by SQL. Stream SHA-256 with a reusable 64 KiB buffer;
   check bytes read equals guarded length and unchanged path/handle facts. No
   retry against a silently changed source. This is the immediate source baseline.
8. **One readonly connection.** Build options internally: exact validated ordinary
   DataSource; Mode=ReadOnly; Pooling=false; Cache=Private; finite lock-wait timeout;
   empty password; no URI/custom VFS; no RecursiveTriggers; ForeignKeys option
   unset to avoid implicit setup SQL. No caller-supplied connection string.
   The native dependency must be pinned and loaded from its identified deployment,
   not an uncontrolled search path. Recheck source binding/sidecars before opening.
9. **Verify and harden the connection.** Require sqlite3_db_readonly(main)=1 and
   a native-resolved sqlite3_db_filename(main) matching the guarded identity.
   Install the finite authorizer/progress controls and disable loadable extensions;
   no custom SQL functions or collation callbacks. Apply only F's connection-local
   settings and read them back. Check exact engine/VFS/compile-option profile,
   including effective in-memory temporary work; failure or unavailable capability
   is BLOCKED. Readonly state is not proof that auxiliary filesystem writes cannot occur.
10. **Deferred consistent reads.** Begin a Serializable deferred read transaction,
    never BEGIN IMMEDIATE/EXCLUSIVE. Validate schema before querying its data or
    running checks that may evaluate schema expressions. Execute only F's catalog
    and E's mode. Hold source/ancestor guards and transaction through all requested
    reads/reconciliation. Reports get separate read guards after their native
    identity/scope safety is established. No Phase 1 report is generated.
11. **Final preservation.** Dispose readers/statements; end the read transaction
    and close the nonpooled connection. While source guards still remain, recheck
    source path/handle identity, size, UTC mtime, attributes/link count, header,
    sidecars and a second whole-file SHA-256. Compare to step 7. Repeat report
    identity/hash checks. Any difference/unknown blocks trusted completion.
12. **Publication and release.** Publish only after all requested checks and
    preservation checks finish. For approved file output, complete I's native
    disjointness/CreateNew checks while holding relevant guards; close output,
    release callbacks/connection/guards in a defined finally order. Never delete
    refused or partial evidence. Publication errors cannot leave a trustworthy
    success interpretation; receipt and process outcome must agree.

### Sufficiency, concurrency and limits

The production guard/mode/transaction pattern is a sound starting mechanism for
ordinary Windows writers, **not sufficient by naming alone**. Required additional
precautions are the pre-SQL baseline/header gate, canonical basename/single-link
policy, ancestor binding, schema/SQL/resource restrictions, whole-window guard
lifetime and synthetic observation of sidecar/temp I/O and provider closure.

Windows sharing checks are symmetric: a provider read handle whose sharing allows
the guard's reads appears compatible with a read-only guard; an existing write
handle should conflict with the guard's sharing. This is an inference from the
primary sources, not a verified experiment for the locked native artifact.
Opening on incompatibility fails; no ReadWrite fallback or relaxation is allowed.

Recheck sidecars at every stage/SQL batch boundary and after SQLite closes. A
new sidecar causes immediate cancellation/closure and BLOCKED, not recovery.
Boundary checks cannot detect every transient create/delete race. The selected
rollback-only profile, guards and pinned-provider no-write tests must establish
the intended nonmutating behavior under the approved quiescent operating model.
If G shows a provider-side recovery attempt or auxiliary file mutation in that
model, real access is blocked; redesign/review is required. Do not patch around
it with immutable=1 or an unreviewed VFS. A stronger custom VFS is not selected v1.

Active writer connection, disappearing path, changed identity/fingerprint,
ancestor rename, lost permissions or unsupported metadata yields BLOCKED with
the exact stage/reason. A failed post-check never becomes VERIFIED. Cancellation
is PARTIAL only when no known safety/integrity violation supersedes it; completed
subchecks stay historical facts and unperformed checks stay UNKNOWN.

No general protection is claimed against privileged/kernel access, writable
mappings acquired before exclusion, malicious namespace changes invisible at
boundaries, direct volume writes, hardware faults or adversarial transient
edit-and-restore. Quiescence remains an operator prerequisite. Native file guards
and SQLite's snapshot do not capture the source library. No BMS/chart/resource
file is opened, statted or enumerated by the primary workflow.

### Measurable provider/native acceptance specification (B1-B6)

These are required future experiments, **not executed or passed here**. Use only
owned synthetic fixtures and independent inspector/controller processes with
recorded PID plus creation time. For each run preserve the fixture specification,
pre/post bytes and native facts, exact binaries, approved policy, raw observations,
instrument settings/coverage, timing, complete receipt/exit or interruption record,
and reviewer decision. P0/P1/P2 in G apply; never delete refused evidence. A failed
acceptance experiment disqualifies that implementation/profile from real access,
even if its emitted receipt incorrectly says VERIFIED.

| Requirement / exact behavior | Synthetic fixture | Independent observation | Required result | Failure / STOP | Preserved evidence | Coverage limit | Blocks real DB authorization? |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B1: identify every actually loaded provider/native dependency | T31 valid rollback DB under guards; separate substituted/unapproved DLL or managed dependency and unavailable module-identification variants | External process module inventory after load and at close; resolved module path, native file identity and SHA-256 compared to reviewed deployment; record managed Microsoft.Data.Sqlite/Core and all SQLitePCLRaw assembly identities, versions, MVIDs and hashes. Correlate the native module used by the raw connection with engine version/source ID, actual active VFS and complete compile options; record Windows build, NTFS/volume/case/long-path policy, process architecture, .NET runtime and System.Text.Json identity. | Exact match to the independently reviewed managed/native allowlist, including e_sqlite3.dll actually loaded. Module and raw API evidence agree; no unexpected dependency or load path. | Unknown, substituted, unexpected or mismatching module/VFS/profile => BLOCKED before evidence queries; no search-path fallback. Inability to correlate the connection to the loaded module fails acceptance. | Signed-off identity manifest, module snapshots/load events, binary digests/native IDs, raw profile observations and deployment source reference | Proves only the measured deployment/OS/architecture; lockfile/NuGet/name alone proves no loaded binary. Alternate artifact is another unapproved profile. | Yes |
| B2: actual native guard/provider compatibility and exclusion | T19 writer open before guard; independent writer/delete process attempts at synchronized stages from guard acquisition through final checks; T31 valid source; T22 path/ancestor replacement; T35 close/fault paths | Process-attributed native opens with desired access, share flags, disposition and Win32 result; controller synchronization; handle/lock lifetime observations, native identity and hashes. Reprobe writer after all guards close; examine handle inventory for leaks before controller mutation. | Valid full RECONCILE succeeds while source/ancestor guards stay held; preexisting writer yields SOURCE_BUSY; later ordinary writes/deletes denied by Windows; no inspector source write/append/delete access requested; no pooled/leaked source handle. Post-release writer succeeds in a separate controlled step; preserved original compared before that step. Stable canonical path and volume/file ID throughout. | Any incompatible provider lock/open, requested write access, allowed competing mutation, leak, unstable binding or unknown trace => acceptance STOP/BLOCKED profile. Never relax guard sharing or switch modes. | Native open/close/lock results, writer acknowledgments/barriers, handle snapshots, identities and original preserved fixture bytes (controller later write recorded as P1) | Ordinary process sharing only; mappings/privileged I/O and undetectable edit-and-restore remain outside guarantee. Managed exceptions alone are insufficient. | Yes |
| B3: no recovery, sidecar or temporary writes through the whole lifecycle | T14 journals (empty/stale/hot); T15 WAL/SHM and header 2/2 with no sidecars; T16 collisions; T29 transient sidecar injector; T32 memory-pressure/temp-work; T20/T21/T35 cancel/crash/close | Reviewer-approved filesystem-event observer, independent of inspector receipts; start before launch, continue through closure/termination and drain; correlate process creation/module activity, exact paths/native identity and create/open access/write/truncate/rename/delete events. Observe all inspector filesystem destinations including system temp and inherited output, distinguishing approved receipt output. Independent positive-control writer must produce captured known transient events before/after each session. | Existing sidecars and WAL header refused before provider.Open with no recovery/target reads; supported valid rollback profile observes zero forbidden operations, including transient temp/sidecar attempts and close/dispose effects. Injector events attributed separately; fingerprints agree with P0 or prescribed P1/P2 effects. | Any forbidden attempt/operation, unobserved interval, dropped events or failed observer/control => acceptance fails; no zero inferred from final absence. Stop approval and preserve trace, no cleanup/recovery. | Unfiltered retained event evidence or reviewed lossless capture, start/end/drain times, coverage/drop counters, positive controls, process attribution, sidecar/header/native snapshots | Observer must prove its access/create/write coverage and attribution for this Windows profile; traces do not defeat kernel attacks or prove all library state. Tool unavailable/unreviewed => explicit blocker. | Yes |
| B4: bounded cooperative cancellation and safe callback/handle lifecycle | T20 long allowed integrity/chart queries with cancellation synchronized after progress; cancel hashing/graph/report phases; T21 terminate at callback/close/publication; T35 dispose-fault paths; controlled blocked filesystem operations | Independent monotonic timestamps for request/callback/interrupt/return/close; callback entry/exit counts and registration lifetime; native return codes and handle observations plus B3 trace. Test progress callback returning nonzero and sqlite3_interrupt separately while the connection is live; provider Cancel alone is a negative control. Controller resumes a blocked synthetic I/O operation or terminates only its owned identified process. | Demonstrated callback invocation and interruption (SQLITE_INTERRUPT) within a preapproved VM-work/cancellation latency budget; delegates rooted until calls quiesce; no callback/use/interrupt after native close. Readers/statements and transaction close before connection, guards retained through final checks; no leak or forbidden operation. Cancel returns PARTIAL/130 if no stronger failure, incomplete receipt/checks remain explicit; killed process never accepted as success. | Missing callback/interrupt, exceeded approved responsive-query budget, unsafe lifetime/disposal, false completion or leak => acceptance STOP. Blocked kernel I/O has no instant-cancel guarantee; unsupported required deadline blocks profile instead of reporting success. | Timeline/callback/interrupt/return observations, termination/exit/receipt, cleanup handle evidence and P0/P2 preservation | Cannot guarantee prompt interruption of arbitrary Windows kernel opens/reads. No hard termination controller is implicitly authorized or benchmark supervisor reused. | Yes |
| B5: finite policy enforced and empirically supported | T27 field/depth pressure; T32 native cache/temp pressure; T33 DB/row/report/fanout/memory/timeout boundaries, described in H | Compare predeclared policy to independently measured peaks, checkpoints/stops, VM callbacks, memory/cache native readings, wall time and B3 events; inject each limit separately and in approved combined worst cases | All H limits finite and approved; boundary behavior matches independent oracle, valid below-limit fixtures reconcile fully; exhaustion stops PARTIAL without skipped work/downgrade/disk spill. Measured safe envelope and margins documented separately from configuration. | Missing/uncalibrated required limit or coverage => acceptance blocked; supported budget exceeded => PARTIAL, unsafe native/temp behavior => profile STOP | Policy revision/units/enforcement sites, workload definitions/seeds, raw peaks/timings, expected versus actual stops, traces and calibration approval | Tested envelope only; historical Scan 4 population is no universal capacity or runtime proof. | Yes |
| B6: correctness and preservation have independent primary oracles | T01-T13,T23,T27,T34,T36 and format subcases below; independently constructed schema, rows and expected receipts | Independently reviewed schema/catalog, literal report bytes, hand-calculated sets/closures/multiplicities and expected statuses/receipts, combined with external native P0/P1/P2 observations; compare every expected field, not just totals | Every valid/invalid fixture produces independently specified checks/status/exit and complete logical receipt; production differential agreement is secondary. Native proof does not come solely from inspector self-report. | Only production-generated expected result, missing hand oracle, shared production code or unexplained mismatch => acceptance STOP; preserve both results for review | Fixture/specification digests, hand derivations, golden byte/receipt expectations, differences, independent native evidence and reviewer approval | Independent code still shares a requirements model; separate review needed for conceptual mistakes. | Yes |

B3 observation outcomes are explicitly **ZERO_OBSERVED**, **MISSING_COVERAGE**,
**DROPPED_EVENTS**, **INSTRUMENTATION_FAILED**, or **FORBIDDEN_OPERATION_PROVEN**.
Only ZERO_OBSERVED, with successful controls and complete attributed coverage,
satisfies acceptance. The other categories are unknown/failed proof or a violation,
never a zero write count. G/I preserve that distinction even for partial runs.
No suitable independently reviewed observer or native-profile experiment has
been established in this task; these remain real-access blockers. Selection,
installation, configuration and execution of tracing are not performed here.

## E. Read-only evidence verification algorithm

### Modes and evidence boundaries

- **INTEGRITY:** source safety/preservation, header, ownership, exact schema,
  quick_check and foreign_key_check, scan/root records and population overview.
  No reports or relationship proof. quick_check does not provide all the index/
  uniqueness checks of integrity_check. A successful lightweight run is PARTIAL
  for the full readiness objective; requested_scope_complete can still be true.
- **RECONCILE:** all opening checks plus full integrity_check, full chart/diagnostic
  validation, independent relationship reconstruction, four report comparisons
  and selected-component closure. Reports and expected latest scan are mandatory.
  Never silently reduce to INTEGRITY, skip fanout or substitute report counts.

No mode runs scan, rehash, report generation, preflight, content hashing or mutation.
An expected scan ID is a caller assertion about the **latest** snapshot, not a
request to reconstruct mutable chart rows from an earlier scan.

### Independent checks

| Check / source | Proposed verification | Evidence boundary |
| --- | --- | --- |
| Ownership: header + SQL | main application_id=0x424D5341; user_version=1; schema_metadata tool=BmsLibraryAudit, schema_version=1, path_comparison=windows-ordinal-ignore-case. Check storage classes, duplicates and exact values. | Identifies this DB's declared ownership; no filesystem freshness proof. |
| Schema: catalog + fixed introspection | Independently frozen schema-1 specification: schema_metadata, scan_sessions, library_roots, chart_files, diagnostics; exact columns/types/nullability/PK/FKs/CHECK and the two named chart indexes. Validate independently approved sqlite_schema signatures, including expected internal autoindexes. Reject unknown views/triggers/virtual tables, altered expressions/indexes or extra schema objects before integrity checks. Never execute stored SQL text. | Schema compatibility, not a migration or Phase 2 schema check. Golden catalog fixture must be reviewed before implementation acceptance. |
| Integrity: SQL | INTEGRITY uses quick_check; RECONCILE uses full integrity_check and requires only ok. Both require foreign_key_check returning no rows. Capture all returned errors within bounded error output. | Engine-level checks, with the executed check named; not proof of absent malicious engine bugs. |
| Scan identity: SQL | Read every scan record, then latest COMPLETED by descending positive ID. Validate UTC start/end, completion, roots/statistics JSON and references. RUNNING blocks settled-source acceptance. Other unsupported/failed statuses are preserved and rejected under the schema-1 compatibility policy; no repair or omission. | A later valid COMPLETED scan mismatching expected 4 is EXPECTED_SCAN_CHANGED/BLOCKED, not corruption and not label remapping. |
| Root configuration: SQL + lexical rules | All roots, including inactive: positive IDs, original/normalized uniqueness, valid booleans, fully qualified ordinary spelling and Windows comparison keys. Active roots must equal latest roots_json after normalization; configured roots must exist as records and be disjoint under stored path rules. | Roots are stored facts. Native root metadata requires explicit scope approval; no contents traversal. Unknown historical native resolution cannot be dropped from output safety. |
| Chart records: SQL + lexical rules | Read all rows, including missing/inactive/unverified. Check native-independent path normalization, direct parent/name/extension, containment in stored root, first/last scan references, present/status consistency and booleans. Present rows require active owner and last_seen=latest. OK requires lowercase valid MD5/SHA-256 and nonnegative size/mtime. Recognized extensions are case-insensitive. | Non-OK may legitimately have unknown size/stamp/digests; preserve distinctions. Never inspect chart paths on disk. |
| Diagnostics/statistics: SQL | Validate latest scan diagnostics and metadata. Parse stored statistics with the actual Phase 1 PascalCase fields, compare their snake_case report projection, retain zero versus unknown. Error/unstable counters overlap; reused hashes are cache facts. Unknown diagnostic semantics cannot be silently ignored in full reconciliation. | Discovery-stage observations, not fresh payload reads. |
| Snapshot overview: SQL-derived typed rows | Independently count present/OK/unverified rows, unique hashes, physical folders and known incomplete folders; compute duplicate groups and every location from complete indexed rows. | No reliance on summary.json as the database oracle. |
| Relationships: independent model | Derive complete trusted-folder sets, every candidate tuple/class and RELATED/EXACT_ONLY closure, then compare report tuples and labels. | Production Analyzer is a specification comparator only, never called. |
| Four reports: guarded files | Strict UTF-8/CSV/JSON; native identities, hashes, lengths, timestamps; compare every relevant row and summary field to independently computed facts, including dates, roots, stats, diagnostics and memberships. | Same logical indexed observation; filenames and raw DB hash alone do not bind the historical report to the current DB. |
| Source preservation: native facts + bounded fingerprints | Equal before/after identity/length/mtime/attributes/link count/SHA-256 and sidecar absence, plus any requested write-observation evidence. | Observed DB window only; unavailable post-check is unknown, not unchanged. |
| Current source chart/tree truth | NOT PERFORMED; output an explicit NOT_OBSERVED value. | Requires separately authorized future filesystem observations/Phase 2A direct-chart checks. |

Independently reconstruct incomplete folders using present non-OK rows and source
diagnostics: DIRECTORY_UNREADABLE/REPARSE_SKIPPED block the recorded subtree;
ENTRY_METADATA/CHART_READ/CHART_UNSTABLE block the recorded parent subtree.
Duplicate groups preserve valid individual present/OK rows even when their folder
is incomplete. Folder relationships exclude incomplete folders. Unknown codes
require an explicit supported-semantics decision rather than guessing admission.

Group folders with Windows OrdinalIgnoreCase keys; choose the ordinal-minimum
original parent spelling for display, as required by existing discovery semantics.
Retain every physical chart row and form a separate distinct SHA-256 set. For
each shared hash, independently accumulate unordered folder-pair intersections.
Exact means common=distinct_a=distinct_b>0; subsets require unequal sizes; other
classes use exact inclusive min-coverage thresholds 0.80 and 0.50, then SHARED.
Jaccard/coverage comparisons use the declared invariant numeric representation
and a documented round-trip floating comparison policy. Do not use fuzzy admission.

Independently derive graphs for all relationships and for exact edges only.
Sort original members/components using .NET UTF-16 StringComparer.Ordinal;
component labels follow the existing ordinal navigation order. Do not substitute
SQL UTF-8/BINARY ordering, culture, Unicode NFC/NFD, Python case folding or
normalized uppercase ordering for authored-name ordering. Check all closures,
not just the four selected edges; reject extra exact neighbors and missing pairs.
This independent algorithm needs its own hand-authored oracles and differential
tests; agreement with the production analyzer alone cannot prove correctness.

For the frozen Scan 4 request, assert latest ID 4 and reproduce the selected
member/hash sets and all ten original paths. Verify 1/7/3/2 distinct hashes per
root and all eight exact pairs. Count physical paths independently: 34 was the
prior report observation; 31 was a hash-membership sum. Reject drift from the
approved report freeze rather than imposing these constants on other captures.
Even an exact stored-hash match does not prove current chart bytes are unchanged.

Report parsing must reject malformed quoting, missing/extra/duplicate rows or
headers, duplicate JSON keys, bad types, invalid UTF-8 and lossy Unicode conversion.
Decode SQLite TEXT strictly from verified native UTF-8 slices with validated
lifetime/storage class; do not rely on a decoder silently replacing invalid bytes.
No SQL/report path string triggers stat, hashing, archive extraction or traversal.
Strict decoding and typed-reader integration are implementation TEST-GATE items.

Compare every duplicate-chart row with its indexed path/hash/root/size/stamp;
every folder candidate including physical multiplicities and exact class/counts;
every cluster member/label/count/distribution; and the full summary projection.
Reports omit singleton chart locations, so report-only arithmetic cannot establish
the complete current index. No regeneration, repair or new Phase 1 report occurs.

### Historical Phase 1 summary format recognition

SOURCE-VERIFIED: ReportWriter sets WriteIndented=true and SnakeCaseLower, writes
the serialized string with Write(string), then writes one literal LF. Its UTF-8
StreamWriter has no BOM and NewLine=LF, which governs CSV WriteLine, not newlines
already inside a string. The [.NET 10 serializer source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializerOptions.cs)
defaults JSON NewLine to Environment.NewLine. Thus Windows serialization explains
internal CRLF followed by a separate final LF; it does not establish the exact
historical runtime or independently validate the real file. The prior observation
of 60 CR and 61 LF is context, never a recognition rule or a fixed line count.

Two explicit byte profiles are proposed for Phase 1 summary.json only:

- **PHASE1_SUMMARY_CANONICAL_LF:** independently frozen schema-1 summary layout,
  UTF-8 without BOM, LF at every structural line break and exactly one final LF.
- **PHASE1_SUMMARY_WINDOWS_LEGACY:** the same independently frozen producer layout
  with CRLF at every internal structural break and exactly one separate terminal
  LF after the closing JSON token. No other lone LF/CR, terminal CRLF, extra blank
  line, trailing space, BOM or unsupported indentation/escaping/order is admitted.
  Escaped string characters are tokens, not structural breaks. This deliberately
  mixed terminal convention is distinct from arbitrary mixed line endings.

Before recognizing legacy, freeze and independently approve the exact producer
profile: Phase 1 tool_version=0.1.0, schema_version=1, phase identity, source
revision/summary projection, actual producing System.Text.Json/runtime identity
and options (including default indentation, escaping, number/date representation,
property and nested collection order). A version string is insufficient historical
provenance. Bind recognition to the approved report set, scan, guarded report
native identity and unchanged raw SHA-256/length, plus independently verified
schema and database/report semantics. No automatic exception for CSV, another JSON
report, an inspector receipt, unknown producer or another schema/tool version.

The recognition oracle must be independent literal golden UTF-8 bytes, reviewed
against that frozen serialization specification, including nested structures,
empty arrays/objects, Unicode/escaping, dates and numbers. Hand-authored semantic
fixtures and an independent token/layout checker must show exact byte agreement
for the full expected summary, not just equal parsed values or CR/LF totals.
Calling production ReportWriter to generate expected bytes is not an oracle;
using the same serializer alone is also insufficient. A pinned serializer may
serve as an additional differential check. No lossy normalization, newline
replacement, regeneration or source rewrite is permitted for recognition.

| Evidence class | Format result and overall consequence |
| --- | --- |
| Exact canonical profile | CANONICAL; eligible for VERIFIED only after every full RECONCILE gate passes. |
| Exact independently proven legacy profile and bound identity/provenance | LEGACY_COMPATIBLE; eligible for VERIFIED under the same complete gates, with an explicit historical exception in the receipt. It is not labeled canonical. |
| Syntactically valid, semantically correct but unsupported formatting; or insufficient exact legacy characterization | UNSUPPORTED/UNPROVEN; required format check incomplete/failed, PARTIAL with the unresolved requirement. No generic newline-tolerant admission. |
| Malformed UTF-8/JSON, duplicate keys, prohibited fields, wrong types/version, or semantic mismatch | INVALID_SOURCE with invalid_target=REPORT_SET; formatting compatibility cannot waive it. Known safety/provenance failures retain their stronger applicable classification from I. |

The exception cannot override ownership, schema, full integrity, FK, current-scan,
independent semantics, preservation or any safety/provenance violation. Receipt
fields identify profile/specification/oracle versions, original byte digest/native
identity, binding evidence, exact checks and exception reason; unavailable proof
is UNKNOWN. G's format subcases are mandatory before claiming support. No inspector
exists or has accepted the Scan 4 report. Phase 2 LF/canonical-manifest requirements
and historical stored evidence remain unchanged.

## F. SQL allowlist and prohibited operations

The following is a **nonexecuted design catalog**, not an inspector script.
Only compiled statement IDs and typed parameters are allowed. Table names are
fixed literals, never interpolated user input. No arbitrary SQL/connection-string
option exists. Future implementation review freezes actual text for every ID.

| ID/category | Exact statement or fixed finite family | Restriction |
| --- | --- | --- |
| SETUP | PRAGMA query_only=ON; PRAGMA trusted_schema=OFF; PRAGMA temp_store=MEMORY; PRAGMA read_uncommitted=OFF; PRAGMA recursive_triggers=OFF; PRAGMA mmap_size=0; bounded connection-local cache_size. | Separate internally approved statements before evidence queries; read back effective settings. Never persistent default_cache_size/temp_store_directory. No user SQL. |
| IDENTITY | PRAGMA main.application_id; PRAGMA main.user_version; SELECT key,value FROM main.schema_metadata. | Read IDs and exact typed metadata only. |
| CATALOG | SELECT type,name,tbl_name,sql FROM main.sqlite_schema. | Data for schema validation only; never run sql column contents. |
| STRUCTURE | PRAGMA main.table_xinfo for the five fixed tables; foreign_key_list for those tables; index_list and index_xinfo for the independently fixed expected index names. | Includes generated/hidden-column detection; reject unsupported structure. No arbitrary object name. |
| INTEGRITY | PRAGMA main.quick_check or PRAGMA main.integrity_check; PRAGMA main.foreign_key_check. | Mode determines full versus quick; unexpected/truncated/error results cannot pass. |
| SCANS | SELECT id,started_utc,completed_utc,status,rehash_all,roots_json,statistics_json FROM main.scan_sessions ORDER BY id. | Inspect all records, derive latest COMPLETED independently; expected scan asserted after reading. |
| ROOTS | SELECT id,absolute_path,normalized_path,active FROM main.library_roots. | Retain inactive roots and original spelling. |
| CHARTS | SELECT root_id,absolute_path,normalized_path,parent_folder,filename,extension,size,mtime_utc_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status FROM main.chart_files. | Sequential typed read of all indexed facts. No payload lookup. |
| DIAGNOSTICS | SELECT id,scan_id,severity,code,path,message FROM main.diagnostics WHERE scan_id=$scan. | Bind validated latest positive ID; validate referenced scans independently through FK/scan checks. |
| TYPE CHECKS | Fixed typeof projections over exactly the cataloged columns. | No dynamic expressions, user functions or arbitrary projections. |
| PROFILE | PRAGMA compile_options; read-only journal_mode query and setup-setting reads; native engine version/source-ID/readonly/filename queries. | journal_mode is read only here: no assignment, checkpoint or transition. Header gate precedes SQLite. |
| TRANSACTION | Provider's internally generated BEGIN and end-of-read COMMIT or ROLLBACK. | Deferred read connection only. Ending a transaction is not journal recovery; no IMMEDIATE/EXCLUSIVE/savepoint or write attempt. |

Use the existing raw authorizer as defense in depth: deny unknown actions,
attachments, DDL/DML, non-main reads, unapproved PRAGMAs/functions and accesses
through views/triggers. Permit only the catalog's precise setup/control exceptions
and reads/functions it actually needs. Never authorize by a SELECT text prefix.
The authorizer cannot prevent filesystem side effects during opening/recovery.

[Statement-readonly checks](https://www.sqlite.org/c3ref/stmt_readonly.html) do not
cover every indirect effect. Use native readonly/filename checks, fixed statements
and the [authorizer](https://www.sqlite.org/c3ref/set_authorizer.html) together,
without presenting any of them as filesystem nonmutation proof. Provider batching
must be constrained by the catalog; a semicolon injection cannot append a statement.

Explicitly forbidden: INSERT/UPDATE/DELETE/REPLACE, schema creation/drop/alter,
ATTACH/DETACH, VACUUM/INTO, backup/deserialize/copy workarounds, journal-mode
assignments, WAL checkpoint, recovery, load_extension, user-defined functions,
arbitrary VFS/plugins, URI immutable/nolock options, writable_schema, persistent
PRAGMA writes, unapproved temp tables/indexes and automatic deletion/repair.

[SQLite PRAGMA documentation](https://www.sqlite.org/pragma.html) distinguishes
connection settings and storage effects. temp_store=MEMORY alone is insufficient
if the native compile profile overrides it; verify the profile and instrument
temp-file creation. Set it on a fresh connection before any temp object; never
change global temp-directory settings or silently spill to disk. If in-memory
processing cannot be maintained within limits, stop PARTIAL instead of copying
source facts into an unapproved scratch database.

## G. Negative-test matrix

**Design only: none of these tests is implemented or run here.** Every fixture
is synthetic, disposable, project-owned and outside any real DB/library. An
independently specified schema/row oracle is primary; production-created fixtures
may be an additional differential comparator, never the only valid fixture.

Common preservation assertions applied to **every** case:

- **P0:** Compare full synthetic source bytes, SHA-256, native volume/file identity,
  link count, length, UTC mtime and relevant attributes before/after. Compare all
  preexisting sidecar/collision/sentinel objects and report files. Expect equality.
  Record access-time behavior separately; it is not last-write/content equality.
- **P1:** For an injector deliberately changing bytes/identity/namespace, compare
  pre/post facts to the injector's exact independent expected effects. Require
  zero inspector mutation; do not falsely blame the inspector for the injected
  change or require impossible whole-fixture equality. Snapshot both generations.
- **P2:** For no final receipt/crash, an external synthetic controller records
  post-termination facts and event coverage; missing facts never imply preserved.
- For P0/P1/P2, a reviewer-approved native event observer records inspector and
  injector PIDs/creation times, desired access, create/write/truncate/rename/delete/
  attributes and exact paths. Detect sidecars that are created then removed as
  well as survivors, including SQLite close/dispose and crash. Final existence
  checks alone are insufficient. Dropped events invalidate the relevant assertion.
- Synthetic observers may inspect their owned fixture directory; the inspector
  itself has no source-library walker. Known hot journals are prepared by the
  fixture controller, never recovered by the inspector. Keep refused evidence.

Codes/statuses below are proposed inspector outcomes from I, not Phase 2 exits.
Unless a row explicitly specifies INTEGRITY, it requests RECONCILE. Test-controller
SQL/fault hooks are isolated synthetic injection facilities, not public inspector
SQL options. Neither mode may run on real inputs during design validation.

| ID | Synthetic fixture and fault injection | Expected result / status / exit | Preservation | What it proves | What remains unproven |
| --- | --- | --- | --- | --- | --- |
| T01 | Independently built schema-1 rollback DB, valid scans/roots/charts, complete reports and selected two/three-member sets. | RECONCILE completes all checks: VERIFIED/0. INTEGRITY intentionally PARTIAL/2. | P0 | Valid opening, complete reconciliation and mode boundaries. | Real dataset capacity and untested platforms. |
| T02 | Foreign ordinary SQLite DB from another tool, created before baseline. | Refuse ownership; INVALID_SOURCE/4, no repair. | P0 | Foreign data cannot become trusted project evidence. | Every possible foreign schema. |
| T03 | Valid fixture with wrong header application ID; also mismatch SQL ownership metadata. | INVALID_SOURCE/4 with exact ownership reason. | P0 | Header/SQL consistency and fixed ownership gate. | Deliberate forgeries of all metadata. |
| T04 | user_version or metadata schema_version changed before baseline. | INVALID_SOURCE/4, no migration. | P0 | Unsupported version refusal. | Future supported schema conversion. |
| T05 | path_comparison changed to ordinal or unknown value. | INVALID_SOURCE/4. | P0 | Platform comparison binding. | Actual live chart-directory case modes. |
| T06 | Truncated/page-corrupt synthetic DB; stage faults before baseline. | Bad header/integrity => INVALID_SOURCE/4; no recovery. | P0 | Corruption cannot publish verified facts. | All corruptions or engine vulnerabilities. |
| T07 | Fixture has orphan chart root/scan or diagnostic FK; controller bypasses FK enforcement before baseline. | FK check reports rows; INVALID_SOURCE/4. | P0 | FK checking independent of setup enforcement. | Non-FK logical provenance, covered by T12. |
| T08 | Remove required table/index/metadata or alter a CHECK before baseline. | Schema/metadata refusal: INVALID_SOURCE/4. | P0 | Explicit schema gate precedes evidence interpretation. | Legitimate future extensions. |
| T09 | No completed scan; separate persisted RUNNING/failed/unknown records. | No completion/unsupported status => INVALID_SOURCE/4; unsettled RUNNING => BLOCKED/3. | P0 | Unexpected records are not silently filtered out. | Whether another actor is still active. |
| T10 | Valid later COMPLETED scan 5 with changed chart rows, request/report frozen to 4. | EXPECTED_SCAN_CHANGED, BLOCKED/3; no historical reconstruction/remapping. | P0 | Latest-scan assertion and stale scope refusal. | Current chart-byte freshness. |
| T11 | roots_json disagrees with active flags; inactive root owns present chart. | INVALID_SOURCE/4; retain all historical roots. | P0 | Root activation/configuration consistency. | Physical availability of each root. |
| T12 | Bad root_id, normalized/path/parent/name/hash, first/last scan or present/status provenance; include valid ERROR size=-1 control. | Bad facts INVALID_SOURCE/4; valid unknown error metadata retained. | P0 | Logical provenance and unknown-versus-invalid distinction. | Actual files and their bytes. |
| T13 | Correct DB with changed/missing report row, membership, summary date/count or hash. | Semantic mismatch => INVALID_SOURCE/4 with invalid_target=REPORT_SET; missing required input => BLOCKED/3. | P0 | DB/report agreement is not inferred from filenames. | Original report-generation execution history. |
| T14 | Ordinary, empty, stale and real hot rollback-journal fixtures at canonical basename. | BLOCKED/3 before provider.Open; no journal reads/recovery. | P0 | Every journal is refused, not just a guessed hot state. | Races after the initial check, T29. |
| T15 | Existing WAL/SHM; separate clean WAL-header DB with both sidecars absent. | BLOCKED/3 before provider.Open in both variants; no create/checkpoint. | P0 | ReadOnly naming cannot bypass WAL/sidecar policy. | Every SQLite build/VFS; pin exact native artifact. |
| T16 | Directory, dangling symlink/junction and access-denied object at each sidecar path. | BLOCKED/3; never follow, delete or replace. | P0 | Collision/unknown-absence fail-closed handling. | All reparse types/privileged namespace attacks. |
| T17 | Source/ancestor reparse point, ADS/device/relative/ambiguous path input; target sentinel. | BLOCKED/3 before SQLite/target payload. | P0 | Path policy and target nonaccess. | Arbitrary races/alias types, T18/T22. |
| T18 | Source hardlink alias, SUBST and distinct 8.3 name; output aliases to DB/root. | Source multi-link/remapping and unsafe output BLOCKED/3; original unchanged. | P0 | Canonical basename, identity and native output exclusions. | Distinct 8.3 unavailable fixture must be an explicit unsupported/coverage gate, not a pass. |
| T19 | Separate writer already open; another ordinary write/delete open attempted after guard. | Preexisting writer SOURCE_BUSY/BLOCKED/3; later opens denied, valid inspected source may finish VERIFIED/0 if all checks pass. | P0 | Actual sharing exclusion with process-separated handles. | Writable mappings/privileged access, T30. |
| T20 | Cancel before open, fingerprint, integrity query, chart cursor, graph construction, report parse and publication. | PARTIAL/130, no VERIFIED result; preserve known failures' stronger status. | P0 | Cancellation checkpoints/native VM interruption and cleanup. | Prompt interruption of blocked kernel I/O. |
| T21 | Controller kills only identified synthetic inspector process before/during query/close/result publication. | No trusted success without complete receipt+matching exit; external assessment PARTIAL/INTERRUPTED. | P2 | Termination cannot fabricate accepted completion or trigger restart. | OS/power-loss guarantees. |
| T22 | Controller attempts file replacement/disappearance/ancestor rename before guard and while guards held; inject identity mismatch at boundaries. | Detected changed binding BLOCKED/3; ordinary denied operations leave original. | P1 | Binding checks and guard lifetimes across namespace stages. | Undetectable adversarial edit-and-restore. |
| T23 | Japanese/decomposed/composed Unicode, case variants and ordinary paths over 260 characters in index/reports/source fixture. | Supported case matches exact spelling; unsupported native/provider length => BLOCKED/3, never truncate/normalize into success. | P0 | UTF-16 ordinal and strict UTF-8/path handling for tested configuration. | All Windows long-path policies and Unicode names. |
| T24 | Deny metadata/read/guard permissions; independent report-read denial. | BLOCKED/3 before unsafe access; no ACL changes/fallback. | P0 | Permission failures remain unknown, not absent. | All ACL/filter-driver configurations. |
| T25 | Inject unsupported OS/disk type/filesystem/case-mode or missing native identity query. | BLOCKED/3, no lexical-only fallback. | P0 | Unsupported native conditions fail closed. | Support for the refused environment. |
| T26 | Call every fixed SQL entry under authorizer; inject ATTACH, DDL/DML, PRAGMA writes, extra statement, load_extension, URI/password/custom-VFS input. | Legitimate catalog works; unauthorized input/action BLOCKED/3 before execution. | P0 | Finite SQL/options surface and no batched injection. | Provider/engine bugs before authorizer installation. |
| T27 | Large malicious report fields, malformed/duplicate-key JSON, extra CSV columns, invalid UTF-8 SQLite TEXT and corrupt quoting. | Malformed evidence INVALID_SOURCE/4; explicit size budget reached PARTIAL/2. | P0 | Strict decoding/parsing and bounded input, not lossy acceptance. | All parser defects. |
| T28 | Replace a table with a view/virtual table; hostile CHECK/default/generated/index expression or unknown trigger. | Schema gate INVALID_SOURCE/4 before dependent integrity/data queries; no functions/extensions/secondary inputs. | P0 | Schema is untrusted executable input, not just table names. | Native parser vulnerability on malformed schema. |
| T29 | Inject sidecar after an absence check, including transient create/delete; stamp change during hashes and setup. | BLOCKED/3 on observed drift; any inspector-side recovery/write event fails acceptance regardless of final absence. | P1 | Boundary/race evidence and no automatic recovery under tested schedules. | Every interleaving; quiescence remains required. |
| T30 | Synthetic preexisting writable mapping changes bytes despite preserved size/mtime; separate link-count/case-mode change. | Fingerprint/native mismatch BLOCKED/3; record injector effects. | P1 | Metadata alone is insufficient and exclusion has limits. | Transient edit-and-restore or privileged direct-volume writes. |
| T31 | Exact pinned provider's read handle under file and ancestor guards; trace access/lock/close, then retry only as a separate fixture with another provider build. | Required configuration must read successfully with no writable/source-sidecar handles; incompatibility BLOCKED/3. | P0 | Real guard/provider handle compatibility, beyond API-name inference. | Any untested managed/native/OS combination. |
| T32 | Force native compile profile/temp spill, very large index checks and distinct/group sorting; observe process-wide temp activity. | Unsupported temp profile BLOCKED/3; tested memory-only profile creates no temp/source file; budget => PARTIAL/2. | P0 | temp_store setting plus actual I/O behavior, including close. | Every memory-pressure/engine build behavior. |
| T33 | Large synthetic row/hash fanout/report population, integer-overflow and deadline/memory limits. | PARTIAL/2 on supported runtime budget; invalid overflowed facts INVALID_SOURCE/4; no skipped pairs/silent mode switch. | P0 | Resource refusal and no partial result promoted to complete. | Real-library duration/HDD behavior. |
| T34 | Complete three-member closure, duplicate copies per root, nested indexed folder names and new external exact neighbor. | Valid multiplicity/closure VERIFIED/0; stale/partial report selection INVALID_SOURCE/4 or expected-scope BLOCKED/3. | P0 | Physical counts differ from hash counts; full closure and diagnostic filtering. | Live nested-tree inventory/package ownership. |
| T35 | Retained guard during reader/transaction/connection disposal; immediately open writer only after successful release. | No leak; earlier write refused, later controller write succeeds; cleanup failure BLOCKED/3. | P1 | Correct lifetimes/no hidden pooled source connection. | OS handle leaks outside tested failure paths. |
| T36 | Unapproved/nonexistent output parent, output containing source DB/root, existing result, unsafe inherited stdio file handle, missing/dropped/failed trace coverage or a proven forbidden operation, broken stdout/file publication. | Unsafe output BLOCKED/3 before emitting to that sink; missing requested observation evidence PARTIAL/2; no trustworthy partial JSON success. Every non-ZERO_OBSERVED tracing category fails native acceptance; proven safety violation takes precedence. | P0/P1/P2 | Native publication/observer provenance and preservation of earlier outputs; any shell prelaunch mutation attributed separately. | Receiver durability or observations after guard release. |

For T19/T31, test exact Win32 access/sharing combinations and independent process
handles, not only an injected managed exception. For T14/T15/T29/T32, require
event evidence that no inspector-side create/delete/truncate/recovery occurred
on refused artifacts or during dispose. Missing/unsupported fixtures block the
corresponding future native-support claim; no non-Windows annotation is a pass.

### Format subcases under the original 36 IDs

These expand T13/T27 without renumbering T01-T36. All use guarded independent
schema-1 fixtures, P0 and complete RECONCILE; VERIFIED below is conditional on
every other gate succeeding. No case has been executed. Freeze literal byte
oracles independently; do not obtain expected output by calling ReportWriter.
Each mutation is authored before the baseline and identified by an exact byte
offset/edit specification, not by normalizing the observed input.

| Subcase | Fixture / independent oracle | Expected format and outcome |
| --- | --- | --- |
| T13.a | Full canonical LF summary and valid CSVs; independently authored semantic rows plus literal golden UTF-8/layout and expected logical receipt | CANONICAL, VERIFIED/0 after complete gates; no exception |
| T13.b | Same hand-authored semantic fixture in exact Windows legacy bytes; independently frozen producer identity/options/projection, reviewed literal CRLF-internal/single-LF-terminal bytes and receipt exception | LEGACY_COMPATIBLE, VERIFIED/0 only with complete bound recognition and all gates; raw digest unchanged |
| T13.c | Paired LF and exact legacy summaries with identical hand-specified values; independently expected different raw bytes/digests and respective profile fields | Both eligible under their exact profiles; semantic equality does not erase byte/profile differences. Uniform CRLF including the terminal newline is unsupported, PARTIAL/2 |
| T13.d | Correct canonical and exact legacy layouts but wrong count/date/root/membership; hand SQL rows and report mutation define the mismatch independently | INVALID_SOURCE/4, REPORT_SET; no format exception can hide incorrect data |
| T13.e | Attach recognition to another report/receipt, wrong version/type or approved-set/scan/producer binding; independent type/schema/profile and identity expectations | Never LEGACY_COMPATIBLE. Unsupported format on another otherwise valid report or unproven producer => PARTIAL/2; wrong type/version/semantic binding => INVALID_SOURCE/4; changed expected latest scan => BLOCKED/3. Native instability/safety failure retains BLOCKED |
| T27.a | Replace just one internal CRLF with LF (and inverse canonical edit), or introduce lone CR; literal edit oracle plus independent strict syntax/layout check | Unexpected mixed but valid JSON => unsupported/PARTIAL/2; malformed JSON => INVALID_SOURCE/4. Never legacy based on aggregate CR/LF totals |
| T27.b | Add spaces/tabs after a line's token or after final JSON token; golden bytes plus independently specified offsets | Extra trailing whitespace is unsupported/PARTIAL/2 even if semantic data match |
| T27.c | Remove final LF; append extra LF(s); replace terminal LF with CRLF, for both profiles; golden terminal byte sequences | Missing/multiple/unsupported final newline => PARTIAL/2; no trimming/rewrite |
| T27.d | Prefix UTF-8 BOM to valid canonical/legacy bytes; independent BOM byte signature and unchanged golden semantics | BOM is unsupported/PARTIAL/2; no BOM-stripping admission |
| T27.e | Invalid UTF-8 sequences, truncated multibyte or lossy replacement opportunity in report/SQLite TEXT; independently specified invalid byte offsets and encoding rules | INVALID_SOURCE/4, exact target identified; no replacement decoding |
| T27.f | Duplicate top-level/nested key with equal and unequal values; independently authored token sequence/key-occurrence oracle | INVALID_SOURCE/4 even when a general parser would keep the last value |
| T27.g | Extra prohibited field at top level or nested schema; independent complete field/type specification and golden expected keys | INVALID_SOURCE/4; no unknown-field omission |
| T27.h | Correct semantic values in compact JSON, different indent/order/escaping or uniform CRLF with terminal CRLF; independent semantic model and unsupported literal layout | Unsupported format => PARTIAL/2; no general newline/whitespace tolerance and no false corruption claim |

### Primary independent oracles (B6)

An independently reviewed schema-1 DDL/catalog specification and literal SQL
row lists define T01-T12/T28 valid ownership, columns/constraints/indexes, UTC
scan/root history and typed invalid variants. A fixture controller constructs
these synthetic databases without calling production database initializers;
foreign examples use a separately specified nonproject schema. Expected errors
and rejected stage are written before running the inspector.

For T01/T23/T34, hand-calculate small sets: A has paths for hashes {x,x,y},
B={x,y}, C={x,y}, D={x,z}, where x/y/z stand for distinct valid literal digests.
A has three physical files but two distinct hashes; A/B/C have common=2 and
exactly three exact edges, one complete three-member EXACT_ONLY closure. D has
one shared hash with each and cannot enter that exact closure. Record all pair
counts, union/intersection/coverage, threshold-boundary classes, RELATED closure,
ordinal labels and every duplicate path independently; add incomplete/error
folders and Unicode/case examples. Omitted C, added exact neighbor, changed
multiplicity or nested stored path must produce the predeclared differences.

T13 and its subcases compare literal independently authored CSV/JSON reports to
those rows/hand computations, with known mismatch offsets/fields. Independent
receipt goldens cover every deterministic logical field, check result, scope,
legacy exception and status/exit; separately assert observation-envelope native
identities, real times and process IDs against the external controller rather
than inventing fixed values. T20/T21/T36 include incomplete/broken receipts.
P0/P1/P2 hashes/native facts and B3 process-attributed observations come from
independent controller instrumentation, not inspector preservation claims.
Production analyzer/report results may supplement this with differential
comparisons only; disagreement or agreement never replaces the primary oracle.

## H. Performance and resource bounds

Around 243000 indexed charts is a prior planning population, not a baked-in
algorithm constant. Fingerprints are sequential with reusable 64 KiB storage.
Use typed forward-only readers, cancellation checkpoints per row/batch and bounded
diagnostic samples. Avoid per-row object-array lists and multiple full chart copies.
Every input string/JSON depth/CSV field/row count/DB byte count has a finite policy
limit checked before allocation; valid large inputs can exceed a supported budget
without being called corrupt. Actual limits are reviewed engineering policy,
not inferred safe capacity from historical counts.

INTEGRITY avoids full relationship reconstruction; quick_check still scans data
and is not an instant check. RECONCILE requires full integrity_check, FK checking,
all indexed rows, full report comparison and independent relationship work.
Maintain compact row references, per-folder distinct hash sets and a hash-to-folder
index. Memory is data-dependent: O(N+U+folders+candidate_pairs); pair work includes
sum choose(k_hash,2). Estimate checked fanout and enforce a finite pair/memory
budget before expansion. Selected four-component scope does not eliminate the
whole-index cost or permit ignoring other exact neighbors.

No automatic disk spill, temp SQLite DB, source copy or pagination that loses
cross-page groups. The native memory/temp profile is pinned and tested; managed
allocation/native heap/SQLite cache budgets are separately enforced. Connection-local
cache limits are not a total process-memory guarantee. Stop PARTIAL on budget
exhaustion; report original requested mode and incomplete checks. No retry/restart.

Native [progress callbacks](https://www.sqlite.org/c3ref/progress_handler.html)
and [interrupt](https://www.sqlite.org/c3ref/interrupt.html) supplement managed
checkpoints. Keep delegates/native handles alive; interrupt registration must be
quiesced before handle disposal. Callbacks never issue SQL or mutate output.
Finite busy timeout is not a wall-clock query limit. Blocked OS opens/reads may
outlast cancellation; do not claim guaranteed immediate stopping. If a human
requires a hard operational deadline, a separately reviewed controller must own
the exact PID/creation time and any child job, preserve interruption evidence
and never restart. Do not reuse the synthetic benchmark supervisor.

Hold source guards until SQL, report comparison and final fingerprints finish;
this intentionally blocks ordinary writers for the full observation window.
Require operator quiescence rather than periodically releasing/reopening guards.
Progress goes to stderr: stage, actual rows/bytes, elapsed time and requested
scope; no false percentage/ETA for unknown relationship/integrity cost. Errors
include stage, statement ID, native/SQLite code and bounded context. Finally
disposes readers, transaction, callbacks, nonpooled connection and guards in
reviewed order; failures stay visible. No source/evidence deletion in cleanup.

No build, stress measurement or time estimate is derived from EXTENDED here.

### Finite resource-policy and calibration acceptance (B5)

Freeze one versioned policy with units, finite thresholds, enforcement points,
maximum measurement/check interval and fail-safe reaction. No unset/unlimited
default is valid. Numerical thresholds are deliberately pending calibration;
the historical population and an existing buffer size do not supply safe limits.

| Policy dimension | Required enforcement / measurement | Synthetic calibration / independent expected stop |
| --- | --- | --- |
| DB/report byte sizes, individual input strings/CSV fields, total JSON bytes/depth/tokens | Guarded lengths before read; strict bounded decoders/parsers check limits before allocating or growing buffers | T27/T33 fixtures immediately below/at/above each proposed threshold, deeply nested and multibyte inputs; log bytes/peak buffers, reject over-limit before unbounded allocation |
| Chart/scan/root/diagnostic/report rows and relationship fanout | Checked integer counts, rows consumed versus expected, per-hash folders, total pair expansions and distinct pair budget before materialization | T33 row boundaries and skewed many-folders-per-hash fixture; hand count sum choose(k,2), duplicates and cross-page closure; no skipped row/pair or overflow accepted |
| Managed memory, process/private memory and native SQLite heap/cache | Separate finite budgets, independently sampled peaks plus enforcement via supported reviewed native limits/cache settings and managed checkpoints; finite sampling/overshoot margin | T32/T33 high fanout, large strings, integrity/index work and combined pressure; record managed/native/cache/process peaks, allocation failure and safe stop. Cache setting is a configuration value, not process-wide proof |
| Busy timeout, VM/progress interval, responsive-query cancel latency and total guarded observation window | Finite provider lock wait plus monotonic whole-window deadline, native callbacks/interrupt and per-stage checkpoints; kernel stalls recorded separately | T19/T20/T33 independently timed lock/cancel/deadline cases, including controlled blocked I/O; no instant kernel cancellation claim or false success. Required hard deadline needs separately reviewed owned controller |
| Temporary storage, diagnostics and progress | Memory-only approved compile/effective profile with B3 zero forbidden temp operations; finite diagnostic sample/byte count, progress frequency/line length and output byte/rate bounds; truncation indicators cannot hide failed checks | T32 memory pressure/temp-work and T27/T36 repetitive errors/backpressured output; native events and output counters confirm no spill and bounded publication. Inability to emit a trustworthy final outcome stays incomplete |

Before implementing enforcement, reviewer approval must fix finite **candidate
configuration** values/units, enforcement APIs and the synthetic calibration plan,
including tolerated measurement interval/overshoot and cleanup reserve. They are
not advertised as empirically safe. Future authorized calibration must exercise
boundary cases and combined worst-case valid/malformed workloads on each B1
profile, repeated as predeclared, with independent resource/timing observations,
full B3 coverage, expected stops/receipts and preservation. Preserve raw series,
workload sizes/seeds, failure paths and deviations; record tested envelope and
justified headroom/stop latency. Exact approved safe values and evidence require
independent sign-off before real authorization; uncalibrated values remain a
blocker. If a budget cannot be enforced or measured with reviewed existing APIs,
stop profile acceptance instead of inventing safety or adding a dependency here.

Within an accepted profile, runtime exhaustion yields PARTIAL (or cancellation
130), with the original RECONCILE request and unfinished checks. Malformed facts
and proven safety violations retain their stronger classifications. Configuration
and measured safe envelope are separate receipt/acceptance fields; neither
predicts real HDD duration or permits a silent switch to INTEGRITY.

## I. Evidence output, statuses and provenance

Default proposed output is one final JSON object on stdout, with progress on
stderr and no automatic log/result file. It is an **inspector receipt**, not a
Phase 1/Phase 2 schema, canonical manifest or execution permission. Version its
format independently; do not use PhaseTwoManifest or an accept-manifest field.
Inspect inherited stream kinds before emitting bytes. V1 permits console/pipe
stdio and rejects inherited disk-backed stdout/stderr; approved receipt files
use the controlled publication path below. A shell may truncate a redirect target
before launching the process; the inspector cannot reverse/prevent that prior
external action. Operators must approve any pipe receiver/log destination, and
preservation claims begin at the recorded guarded baseline, not before launch.
Do not send a refusal message to a sink already identified as unsafe.

| Result group | Required contents |
| --- | --- |
| Inspector identity | Receipt version, auxiliary tool/version, actual clean/dirty/unknown build attestation, informational revision, executable/managed/native dependency SHA-256, SDK/runtime/OS/architecture, engine source-ID/version/VFS/compile profile, design baseline separately identified. |
| Request/window | Original requested mode, exact approved input identities, expected latest scan/components, resource policy, UTC start/end, completed_requested_scope and observation scope. Never hardcode current build or scan. |
| Database | Original/canonical/resolved paths, native identity/link count, before/after length/mtime/attributes/header and SHA-256, sidecar observations, guarded-open flags, pooling, transaction and effective settings. Unknown facts are NULL with a reason. |
| SQL evidence | Ownership/schema results, integrity method and full result, FK findings, all scan states/latest ID/metadata, current and historical roots, chart populations/statuses/hash validity and diagnostics. |
| Reconciliation | Four report paths/native facts/hashes/timestamps/format checks, independently derived counts/relations/distributions, complete selected member paths/hash sets/physical multiplicities and mismatch records. Per report: canonical/legacy/unsupported/malformed classification; exact format-spec/oracle versions, producer/schema/scan/native identity binding and explicit exception/reason. LEGACY_COMPATIBLE never means canonical. |
| Preservation | Separate DB/report before-after results; guard lifetime; native/observer evidence availability and gaps; B3 observation category, coverage/control/drop facts and exact process attribution. observed_write_events=NULL without complete valid tracing, never inferred zero. Matching fingerprints are not a live write counter or all-library proof. Synthetic acceptance trace evidence is mandatory; absent real-window tracing cannot be claimed as newly observed zero. |
| Outcome | Classification, exact failed/incomplete checks and reasons, errors, resource stop/cancellation, unsupported/unknown facts, source_chart_bytes=NOT_OBSERVED and phase2_execution_authorized=false. |

Use UTF-8 without BOM, LF, invariant typed values, original Unicode and fixed
field/collection ordering for emitted receipts. The logical evidence payload
should serialize deterministically for identical captured inputs. A separate
observation envelope includes real times/process/progress, so separate inspection
runs need not have identical whole-receipt bytes. Bind input hashes and tool
identity to any receipt digest; never include the digest recursively or call
it a production preflight seal. No trusted success record before post-checks.

| Proposed classification | Precise conditions | Proposed exit |
| --- | --- | ---: |
| INSPECTOR_VERIFIED | Requested RECONCILE fully completed; supported independently accepted B1-B6 opening/native profile, stable source/report guards and fingerprints, valid ownership/schema/full integrity/FKs/provenance, correct expected latest scan, complete independent report/selection agreement and every required check passed. Exact independently proven E legacy compatibility may satisfy the summary format check only with visible bound exception; it waives no other requirement. Nonzero coherent source diagnostics remain facts, not pilot acceptance. | 0 |
| INSPECTOR_PARTIAL | Valid bounded evidence but insufficient full readiness proof: intentional INTEGRITY mode, runtime budget/deadline, cancellation, missing requested observation evidence or semantically valid unsupported report format/insufficient precise legacy characterization. E's exact accepted legacy profile alone is not a partial result. completed_requested_scope reflects reality; no silent downgrade. A proven safety/integrity/schema/evidence violation takes precedence. | 2; 130 for cooperative cancellation |
| INSPECTOR_BLOCKED | Safety/exclusion/native/tool/output prerequisite fails, existing/new sidecar, WAL/unsupported header, source/report instability, unsupported policy/profile/diagnostic semantics, expected latest scan changed, active writer or unavailable post-check preventing trust. | 3 |
| INSPECTOR_INVALID_SOURCE | Proven foreign/incompatible/corrupt DB or malformed/inconsistent indexed facts; stable but mismatching/malformed required report evidence. Record invalid_target=DATABASE or REPORT_SET rather than claiming all evidence is corrupted. | 4 |

An unexpected OS exit/no complete receipt is not VERIFIED. An external reviewer
may assess it as interrupted/PARTIAL, but no emitted terminal inspector outcome
is fabricated. Require complete receipt **and** matching normal process exit;
syntactically valid JSON alone is insufficient. Known safety failure overrides
partial cancellation; unknown does not mean invalid bytes or success.

Optional receipt-file publication needs separate path approval. Do not write until
all current/historical protected roots and DB/report exclusions are known and
native-disjointness is established. Unavailable/unapproved historical-root metadata
leaves file publication BLOCKED; stdout can carry the already obtained limited
evidence. No probing an unexpected root outside approved metadata scope. Source
root metadata approval permits exact root/ancestor inspection only, not contents.
Use a nonexistent .json target and CreateNew under an ordinary approved parent;
no replacement/overwriting, sidecar names or cleanup. Reject a parent containing
protected sources/databases and preserve earlier results. Hold/recheck parent
identity around publication; partial file output is not accepted evidence.

## J. Threat model and residual risks

Scope: ordinary user-space inspection on a reviewed Windows/native profile,
with untrusted DB/report bytes and potentially competing ordinary processes.
Administrative/kernel/hardware attacks and atomic whole-library preservation
are outside the guarantee. They are disclosed, not silently classified safe.
This is a design security analysis, not independent certification of an executable.

| Risk / severity | Existing mitigation inspected | Proposed auxiliary mitigation | Residual risk | Tests | Blocks future real authorization? |
| --- | --- | --- | --- | --- | --- |
| Accidental writes / CRITICAL | ReadOnly source capture, guard and before/after source checks. | No Core/CLI write APIs; fixed readonly options, SQL catalog/authorizer, pinned dependencies, process I/O tests. | Native engine defect/unreviewed load context. | T01,T26,T31,T35 | Yes until tested/audited. |
| Automatic recovery/WAL sidecars / CRITICAL | Sidecar refusal; no production header/WAL gate demonstrated. | Rollback-only pre-open header, no sidecar objects, no repair/immutable workaround, guards and event assertions through close. | Transient malicious journal races. | T14-T16,T29,T31 | Yes; record production coverage question separately. |
| Corrupt/foreign executable schema / HIGH | IDs, quick_check/FKs; production Phase 2 exact schema. | Independent Phase 1 catalog/signature gate before dependent queries, full integrity in RECONCILE, no extensions/trusted schema. | Native parsing vulnerabilities before gate completes. | T02-T08,T28 | Yes for unknown schema or unsupported engine. |
| Sidecar collisions / HIGH | Native sidecar existence/reparse checks. | Exact canonical basename, definite-absence semantics, no target reads, repeated boundary checks/event evidence. | Unobserved namespace transitions. | T14-T18,T29 | Yes for any existing/ambiguous object. |
| Path aliases/hardlinks / HIGH | Native IDs/volume-GUID, output alias checks. | Single-link source/report inputs, canonical ordinary basename, retained ancestor handles and native output exclusions. | Privileged remapping/metadata-only modification rights. | T17,T18,T22,T30 | Yes until exact configuration passes. |
| TOCTOU identity/races / HIGH | Path/handle rebinding; adversarial atomicity not claimed. | Retain guards, multiple identity/header/fingerprint stages, quiescent approved window. | Writable mappings, edit-and-restore or kernel attacks. | T19,T22,T29,T30 | Yes for observed drift or unmet operating model; remaining explicit limits require reviewer acceptance. |
| Concurrent writers / HIGH | FileShare.Read during capture, Phase 2 writer mutex separately. | Guard whole inspection; no reliance on unrelated Phase 2 mutex/process names; source busy refused. | Previously acquired mappings/external actors after release. | T19,T30,T31,T35 | Yes if exclusion/quiescence missing. |
| Resource exhaustion/unbounded queries / HIGH | Bounded hash buffer; production source arrays/pair fanout in memory. | Fixed schema/statements, finite string/row/heap/pair/VM/deadline limits, no disk spill or silently skipped work. | Kernel stalls and hostile native parser cost. | T06,T27,T28,T32,T33 | Yes until finite policies and stopping behavior validated. |
| Unexpected SQL/setup behavior / HIGH | Fixed production source queries; provider can batch/setup commands. | No caller SQL/options, readback of setup, authorizer, schema gate and profile pinning. | Authorizer does not govern recovery/VFS effects. | T15,T26,T28,T31,T32 | Yes. |
| Improper cleanup / HIGH | using/dispose and nonpooling. | Ordered statement/transaction/callback/connection/guard disposal; no source delete/repair; test hidden connections. | Abrupt termination or disposal exceptions. | T20,T21,T29,T35,T36 | Yes until fault paths have preservation evidence. |
| Unreliable interruption / HIGH | Managed cancellation checkpoints; provider Cancel is no-op. | Native VM callbacks/interrupt plus managed checks; safe callback lifetime; separate owned supervisor only if approved. | Blocked kernel calls cannot be promised promptly interruptible. | T20,T21,T33 | Yes if required operational deadline cannot be met safely. |
| Wrong provenance / HIGH | Conservative Phase 2 build state and source fingerprint fields. | Independent inspector attestation, actual engine/DLL hashes, immediate source baseline, original expected-scan request. | Metadata can be forged; clean-state tag alone is not reproducibility. | T03,T10,T13,T31 | Yes for unidentified binary or substituted revision. |
| False-positive verification / HIGH | Exact sets, whole components, canonical Phase 2 evidence. | Independent graph/typed report equality, full closures/multiplicity, failure precedence, no success from partial data/JSON alone; exact legacy recognition binds identity/schema/producer and literal bytes, never generic newline tolerance. | Shared requirements may contain a conceptual error; real historical producer identity may be unproven. | T01,T09-T13,T27,T34,T36 and format subcases | Yes until independent oracles/review pass; legacy support conditional on E proof. |
| Mutable source-state dependency / HIGH | Captured current rows; filesystem freshness checked later by Phase 2A. | One guarded read observation; never reconstruct older rows or stat chart paths; output NOT_OBSERVED live-byte boundary. | Stored hashes may be stale despite perfect reconciliation. | T10,T12,T22,T30,T34 | Does not invalidate scoped SQL proof; always blocks any claim of fresh chart/package proof. |
| Unauthorized output / HIGH | Production native source/output protection and CreateNew reports. | Console/pipe stdout default with approved receivers; reject inherited disk-file stdio; file publication separately approved against all protected identities/roots; no cleanup/receipt DB. | Output race, receiver durability or shell prelaunch effects. | T18,T36 | Yes for unsafe or unapproved file output. |

No reproduced production mutation defect was established in this task. B's WAL
opening observation remains an **Unresolved production safety-coverage finding
requiring separate review.** No real WAL state is assumed. Stricter auxiliary
criteria neither approve production opening nor authorize its repair/contract change.

## K. Future implementation acceptance criteria

Before implementation: independently review this architecture/security design,
freeze the schema/SQL/result specifications and finite resource policy, then
explicitly authorize a bounded auxiliary implementation with synthetic tests.
Do not add it as a production preflight step or alter existing schemas/lockfiles.

Before any real DB authorization, require:

- Independent audit of all reachable opening, SQL, native dependency loading,
  cleanup and optional output paths; no application write/admission code reachable.
- Exact managed/native build provenance and source review, pinned dependency
  profile, tested readonly/sharing/ancestor/close behavior and no source/sidecar/
  temp writes under valid or refused synthetic inputs.
- All 36 G cases and their T13/T27 format subcases exercised with actual Windows native fixtures where applicable;
  reported coverage limitations, event losses and skipped alias cases resolved
  before claiming their support. Independent schema/graph/golden receipt oracles.
- Strict storage-type/UTF-8/CSV/JSON tests, all expected-schema signatures and
  statement IDs reviewed; no user SQL, destructive recovery or format normalization.
- Effective temp/memory behavior, native callback/interrupt lifetime and finite
  budget/failure publication verified. No downgrade of requested RECONCILE.
- Whole-window source identity/stamp/fingerprint checks and persistent/transient
  sidecar observation; injected changes attributed to the injector, not masked.
- Existing production regression requirements handled only in a future authorized
  implementation task; this design task does not run restore/build/tests/smokes.
- Real access approval bound to exact DB/report inputs, expected latest scan,
  approved metadata scope/destinations, binary, quiescent window and reviewer.

### Compact acceptance matrix

All rows below are pending future proof, not PASS or completed experiments. B1-B6
define measurements, evidence, STOP conditions and coverage limits; G/H define
independent fixture/oracle and finite-policy obligations. Unavailable reviewed
instrumentation or required native capability remains an explicit blocker.

| Area | Required proof | Synthetic test IDs | Real-access blocker |
| --- | --- | --- | --- |
| Native source identity | B1/B2 actual module/guard/native path and volume/file identity, stable pre/post facts, refused aliases and unsupported native conditions | T17-T19,T22-T25,T30,T31 | Yes |
| SQLite read-only compatibility | B1/B2 actual pinned provider under retained Windows guards, denied competing writers, no inspector write access or leaked handle | T01,T19,T31,T35 | Yes |
| WAL and recovery refusal | B3 header gate before open, all journal/WAL/SHM collision forms refused, complete attributed zero forbidden events through close/crash | T14-T16,T20,T21,T29,T31,T32,T35 | Yes |
| Source preservation | Independent P0/P1/P2 bytes/hashes/native facts, injector attribution and valid B3 observation coverage/control evidence | T01,T14-T22,T29-T32,T35,T36 | Yes |
| SQL and schema restrictions | B6 independent schema/catalog and typed malformed fixtures; F's fixed statements/authorizer/setup with actual native profile | T02-T12,T26-T28,T31,T32 | Yes |
| Cancellation and cleanup | B4 measured callback/interrupt/latency/lifetime, blocked-I/O limits, no false success, no leaks and preservation on termination | T20,T21,T33,T35,T36 | Yes |
| Resource controls | B5/H approved finite configuration plus calibrated measured envelope, boundaries/combined pressure, no temp spill or mode downgrade | T19,T20,T27,T32,T33,T36 | Yes |
| Report reconciliation | B6 independently constructed rows/reports/receipt, hand arithmetic/multiplicity, full current index and multi-member closures | T01,T10-T13,T23,T27,T34,T36,T13.a,T13.d | Yes |
| Legacy report compatibility | E exact literal serialization oracle and identity/schema/producer binding, visible exception, all negative subcases and unchanged bytes | T13.a-T13.e,T27.a-T27.h | Conditional: required to accept the historical profile; unavailable precise proof leaves full readiness PARTIAL. Canonical input can satisfy its own profile without this exception |

The observer needed for B2/B3, actual loaded-module/VFS correlation, callback
lifetime measurements and calibrated native resource enforcement are specified
requirements, not demonstrated available reviewed tools. Until approved mechanisms
and evidence exist, corresponding rows stay blocked. No tracing is installed,
configured or executed now. No synthetic success authorizes real access by itself.

A synthetic pass cannot itself approve real inspection. A verified inspector
receipt cannot approve Phase 2A, establish fresh chart bytes or authorize mutation.

## L. Remaining questions and authorization gates

The architecture is selected; these are concrete implementation/security
TEST-GATE decisions, not claims of already available capability:

| Item to resolve | Required decision/evidence |
| --- | --- |
| Exact SQLite native profile | B1 reviewed deployment identities/module correlation, engine version/source ID, active VFS, compile options/DLL hash, Windows/NTFS/runtime/architecture; confirm memory-only temp work. |
| Native exclusion | Guard/provider locks and retained ancestor handles on supported Windows/NTFS; canonical basename/single-link/long-path behavior; no shared write fallback. |
| SQL/hardening adapter | Fixed schema signature/statement/authorizer actions, strict native TEXT decoding, effective connection settings, callback/disposal lifetime. |
| Resource policy | B5/H approved finite candidate values/units, enforcement and calibration plan before implementation; measured safe envelope/sign-off before real access. Supported response for kernel stalls; no invented safe runtime. |
| Evidence/format scope | E exact full report/producer/runtime specification, literal independent format/receipt goldens and scan/native binding; legacy unknown remains PARTIAL. Numeric comparison and logical payload versus actual observation envelope. |
| Process I/O observer | B2/B3 reviewed synthetic mechanism/tool availability, operation coverage, controls/drop detection and native PID attribution; absent capability remains a blocker. No tracer installation/configuration/execution now. |
| Independent review | Named reviewer and acceptance evidence for safety, independent computation and original evidence preservation. |

| Gate | Current status |
| --- | --- |
| Design/source research/documentation | Authorized and completed by this document. |
| Independent design review | Required next; no execution permission. |
| Auxiliary implementation + synthetic tests | NOT AUTHORIZED; needs explicit bounded approval after review. |
| Real inspector execution | NOT AUTHORIZED; needs accepted implementation/security evidence and exact operational scope. |
| Pilot/full Phase 2A | NOT AUTHORIZED; inspector results only inform a separate human gate. |
| Phase 2B/source mutations/recovery/migrations/Git publishing | Outside this task; no implied authority. |

Design-task validation checks document structure/readability, repository links,
research attribution, synthetic matrix completeness, scope/independence wording
and tracked/untracked whitespace/inventory. No inspector/project/test/dependency
was created; only this existing untracked design document was corrected. No other
document, contract, source code or generated evidence was modified.
No SQLite connection, real source/report access, library probe, synthetic run,
build, scan, report, benchmark or Phase 2A/2B execution occurred in this task.

Assessment: `SOURCE_INSPECTOR_DESIGN_READY_FOR_IMPLEMENTATION_REVIEW`.
This means the corrected specification is ready for independent review before
implementation; it is not implementation authorization or a native guarantee.
**STOP for independent review and explicit implementation authorization.**
