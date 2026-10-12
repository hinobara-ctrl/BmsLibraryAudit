# Phase 2A real-library validation plan

Preparation date: **2026-10-11**.
Phase: `PHASE_2A.REAL_LIBRARY_VALIDATION_PREPARATION`.
Preparation baseline: `743d2f176bd06e2bf4304f0d39a0ef7455acb7d2`.
Initial HEAD and origin/main matched that SHA, on `main`, with a clean worktree.

**PREPARATION ONLY. NO REAL-LIBRARY EXECUTION IS AUTHORIZED.**

This is a proposed protocol for independent human review. Preparing, parsing or
approving the wording of a command does not authorize running it. No real
library, real audit database, simulator database or real-library report artifact
was accessed for this preparation. The only repository addition is this document.

## A. Baseline and authority

**Heuristics discover. Exact evidence proves. Verified plans mutate.**

The [governing contract](PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md)
defines observation boundaries, source capture, all-or-nothing component
admission, metadata inventory, canonical identity and evidence limits. Its
historical proposed/unimplemented status describes its preparation date; the
current source implements Phase 2A preflight/report, while Phase 2B remains
unimplemented. This plan neither edits nor replaces its normative requirements.

Authority and source material:

- [Repository policy](../AGENTS.md), [context](../MASTER_CONTEXT.md),
  [README](../README.md) and [sources](SOURCES.md).
- [Phase 1 real-library validation](PHASE_1_REAL_LIBRARY_VALIDATION.md):
  `PHASE_1_STATUS: VALIDATED` for the recorded observation window.
- [Phase 2A implementation report](PHASE_2A_IMPLEMENTATION_REPORT.md): synthetic
  implementation evidence and Windows/platform limitations.
- [Corrective hardening/performance report](PHASE_2A_CORRECTIVE_HARDENING_AND_PERFORMANCE_REPORT.md):
  107 passing tests and its appended post-run EXTENDED SUCCESS record, covering
  500 admitted components, 1,000 roots and 20,000 non-chart placeholders.
  Earlier PENDING statements are historical launch observations; the appended
  completed result is the later synthetic assessment. Its runtime revision was
  UNKNOWN_DIRTY, not a clean-commit attestation or a real-library timing forecast.
- The current task states that independent review and final maintenance have
  completed. Before execution, attach the exact review reference and identify
  the clean executable to which that review applies. This preparation is source
  inspection, not a new independent code audit or a rerun of recorded tests.

Recorded Phase 1 observations were 226,607 verified charts, 138,049 unique
SHA-256 values, 19,925 analyzed folders, 504 EXACT_ONLY components and 1,020
component members. These are **historical facts, not expected results or pass
thresholds**. The recorded library location `E:\Juegos\beatp\canciones` is context
only; its current existence, ownership and contents have not been checked.
Historical scan IDs and component labels must not be reused by assumption.

Verified implementation limits include Windows-only native safety, refusal of
case-sensitive directories and inconclusive identity checks, unverified distinct
8.3 alias coverage, no demonstrated UNC/filesystem coverage beyond local fixtures,
and no simultaneous immutable filesystem snapshot. Source chart capture and
relationship analysis retain the complete chart population in memory. Synthetic
stress had 2,000 charts; it does not benchmark capture of the historical full
chart index or realistic HDD metadata workloads. No real-run result is asserted.

### Authorization matrix

| Step | Current authorization | Required separate decision |
| --- | --- | --- |
| Preparation | Repository/document inspection and this plan only | Independent review of this document; no real paths may be probed now. |
| Operational readiness and discovery | NOT AUTHORIZED | Explicit permission for the exact real Phase 1 DB, read-only provenance/root checks, current Phase 1 report and safety metadata checks. |
| Limited pilot | NOT AUTHORIZED | Approve the frozen current source identity, 2–5 complete component selections, clean executable, new DB/reports/logs, verification reads and any tracing. |
| Pilot acceptance | NOT AUTHORIZED as an execution step | Independent review of the actual pilot evidence; assign a proposed classification in H. No expansion follows automatically. |
| Full Phase 2A | NOT AUTHORIZED | New explicit scope/capacity decision and a fresh evidence capture in a different new DB/output set. |
| Phase 2B | NOT IMPLEMENTED; NOT AUTHORIZED | Separately reviewed contract-compliant implementation, compatibility policy and authorization of the exact sealed workload. |
| Source mutation, simulator/score/Backbeat access, migration, cleanup | PROHIBITED by this protocol | Not part of any approval above; no implicit permission from discovery or admission. |

## B. Operational prerequisites

Each item below is a **future check**, performed only after the corresponding
authorization. Any failed or unknown prerequisite stops execution for a human
decision; it does not start a scan, refresh hashes or repair evidence.

| Prerequisite | Evidence the future operator must record |
| --- | --- |
| Identified clean build | Approved code SHA, clean Git status at build time, Release build log, .NET SDK/runtime identity, assembly informational version and CLEAN build state. Hash the CLI, Core and deployed dependencies; retain the binary manifest. UNKNOWN/UNKNOWN_DIRTY is insufficient for this protocol. Do not rebuild during the run. |
| Supported environment | Windows version, local filesystem/native identity support and usable console cancellation. Use PowerShell 7+ for these examples. Do not assume the benchmark harness's environment exception applies to production paths. |
| Correct source | Explicitly identified project-owned Phase 1 DB; app ID 0x424D5341, schema 1, tool BmsLibraryAudit and windows-ordinal-ignore-case comparison mode. Read-only guarded inspection; no recovery-capable default SQLite opening. |
| Current scan | Actual latest COMPLETED scan ID, timestamps, configured roots, chart-row consistency and scan diagnostics. Reject mismatches, absent completion or unexpected concurrent/unfinished audit activity. A recent ID alone does not prove filesystem freshness. |
| Source stability | Native DB identity, length, UTC mtime and bounded streaming SHA-256 baseline before discovery/pilot; repeat checks afterwards. Quiesce Phase 1 writers for discovery, capture, verification and report acceptance. |
| Sidecars | No existing -journal/-wal/-shm object at source or prospective owned DB paths, including directories, dangling links or unsafe collisions. Record findings; never delete/checkpoint/recover them automatically. |
| Root safety set | All current/configured/active/inactive historical roots from the source, with normalized/original spelling and native path resolution. Resolve unavailable or ambiguous historical roots through human review; do not drop them from protection. |
| Destinations | Explicitly approved ordinary paths for a new non-existing Phase 2 DB and separate new report/log/trace directories, outside every protected root and source DB. Section C governs alias checks. |
| Capacity and resources | Available output/temp storage and memory, DB/sort/rollback-journal overhead, two report exports, receipts, logs and tracing budget. The full chart index is copied even for a small pilot. CSV memberships can exceed unique entry counts. Set a human-approved reserve; no arbitrary numeric capacity threshold is invented here. |
| Quiescent library | No downloads, extraction, synchronization, renaming, moves, chart edits or other audit/scanner activity during the observation window. Record how this was arranged and any observed exception. |

Writing this plan makes the preparation checkout dirty. A future operator must
use a separately approved clean build after the documentation review/Git decision,
or an already independently identified clean binary. Do not waive provenance
checks, manufacture a revision, or commit merely to run this protocol.

## C. Filesystem and database safety

Source inspection at the preparation baseline establishes these capabilities:

| Implementation | Enforced protection and practical boundary |
| --- | --- |
| [PhaseOneSourceCapture](../src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs) | Source opened read-only without pooling; a FileAccess.Read/FileShare.Read guard denies write/delete sharing during capture. Native identity/stamps and two DB fingerprints bind one read transaction, latest COMPLETED scan, all chart-index rows, diagnostics and all historical/current roots. The guard is disposed after capture, not held for the entire later inventory/export; operational quiescence and post-run source checks remain necessary. |
| [PhaseTwoSafety](../src/BmsLibraryAudit.Core/PhaseTwoSafety.cs) | Disk-handle identity, volume-GUID resolved paths, file/link counts, reparse ancestors and directory case mode. Missing destinations resolve through existing ordinary ancestors. Unsafe overlaps, alternate streams/device paths, source DB aliases and existing writable DB hardlink aliases are refused. Unsupported/inconclusive checks fail closed. |
| [PhaseTwoFileSystem](../src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs) | Selected direct charts get fresh bounded 64 KiB SHA-256 reads, read-only sharing, handle/stamp checks and at most one retry. Ordinary other entries receive metadata/identity inspection only; reparse targets are not followed. No parsing/decompression/media decoding. |
| [Preflight runner](../src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs) | Existing RelationshipAnalyzer semantics; complete EXACT_ONLY selection; chart failure rejects the entire component. Inventory traverses the admitted observation union and preserves the first manifest for a second verification. It never expands upward or invents package roots. |
| [PhaseTwoDatabase](../src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs) | New-file CreateNew reservation, app ID 0x424D5332/schema 1, PREFLIGHT_ONLY and schema_revision=phase2a-hardening-1; exact schema/index/trigger validation, composite ownership FKs and immutable source/terminal evidence. Foreign/older/empty DBs are refused. No migration. |
| [Writer exclusion](../src/BmsLibraryAudit.Core/PhaseTwoWriter.cs) | One application writer per native-resolved DB path through a global named mutex. This does not exclude unrelated external writers or prevent every adversarial filesystem race. |
| [Manifest](../src/BmsLibraryAudit.Core/PhaseTwoManifest.cs) and [reports](../src/BmsLibraryAudit.Core/PhaseTwoReportWriter.cs) | Contract Section 10.1 canonical preflight/source seals; atomic completion and seal validation before re-export. Exactly four reports with CreateNew files. Re-export reads persisted evidence, does not enumerate/rehash the library or reopen Phase 1 payload content; source/root path metadata may be inspected for destination safety. Export receipts legitimately write the owned Phase 2 DB. |

Permitted application writes are the newly owned Phase 2 DB, its normal SQLite
transaction/temporary processing artifacts and approved reports/receipts.
Operator logs/traces must also be outside protected sources. No source file or
directory receives a tool-authorized write. No simulator/score/Backbeat database
is supplied as input or opened as a database. Ordinary opaque filenames within an
admitted subtree remain metadata entries regardless of extension; this does not
authorize SQLite/resource-content access to a DB-looking resource.

### Future destination approval

Prefer an explicitly approved local ordinary NTFS evidence area outside
cloud-synchronized directories. This is a recommendation, not proof of safety.
The repository's OneDrive-associated location and the synthetic benchmark's
workspace anchor are **not approval of a real-output location**. Production
PhaseTwoSafety checks every relevant ancestor; cloud/reparse failures may not
be bypassed by reusing the benchmark's more narrowly scoped helper.

After permission to inspect real provenance, use a reviewer-approved read-only
metadata procedure equivalent to the existing Inspect/Resolve/ValidateDestinations
APIs to establish native disjointness against the complete root safety set and
source DB. There is no CLI dry-run/path-validation command. Do not invoke
preflight as a harmless path probe: it can create evidence and read charts.
String-prefix checks, directory names and absence of a file are insufficient.

The Phase 2 DB must not exist, including an empty reservation made by the
operator. Reports must be new (the application also permits empty ordinary
directories). No report directory may contain either DB or a protected root;
separate sibling DB/report locations can be approved without a report directory
containing the DB. Logs, tracing and temporary storage require their own safe
placement. Do not overwrite existing evidence, reassign directory ownership or
create aliases to make a refused location work.

The CLI can append sessions to an existing compatible owned DB; this protocol
deliberately requires a fresh DB for each pilot/full capture. On an explicit
later preflight it can mark old RUNNING sessions FAILED/INTERRUPTED. Do not reuse
an interrupted DB to trigger that behavior or call it resume/recovery. Normal
owned rollback-journal lifecycle is distinct from repairing foreign/source
sidecars. Retain refused/partial evidence for separately authorized inspection.

## D. Pilot selection

Propose **2–5 distinct complete EXACT_ONLY components**, subject to actual
availability. Two observation roots do not mean two independent components.
Choose, where available and suitable:

1. A straightforward component with two roots and modest indexed direct-chart
   counts, without known incomplete-source diagnostics.
2. A component with three or more members, without splitting its closure.
3. Nested observation-root relationships within/between complete selections.
4. Different directory/resource populations when known from explicitly supplied
   evidence. Otherwise document this as an unverified representation goal and
   assess it from the pilot; do not enumerate extra candidate trees to find it.

The future **authorized discovery procedure** is:

- Establish the source baseline and root/destination safety prerequisites first.
- Generate a new current Phase 1 report with the existing `report` command, which
  reads the stored chart index and writes a new report set without rescanning
  charts. Its path policy predates Phase 2 native alias checks, so independently
  approve its destination under C before running it.
- Read that new summary's scan_id and `clusters.csv`, filtering kind=EXACT_ONLY.
  Group every row by cluster_id; compare all member paths and folder_count,
  current diagnostics and exact edges in `folder_candidates.csv`. Never select
  RELATED labels, subset/overlap relationships, individual edges or invented roots.
- Freeze the selected label list, complete membership, source fingerprint/scan
  and selection rationale for human pilot approval. Labels are navigation IDs,
  not durable identifiers. The preflight later derives component digest keys.
- Verify the source baseline still matches discovery before invocation. A changed
  fingerprint/scan/selection requires a new human decision, not silent remapping.

Read-only provenance inspection can establish the latest scan from the existing
`scan_sessions` rows where status=COMPLETED, ordered by id descending. It must
also inspect all `library_roots` and relevant current diagnostics using an
approved read-only, no-recovery procedure. Phase 1 Snapshot() is a view of mutable
current chart rows, not an immutable historical snapshot. No `scan`, rehash,
refresh, simulator import or source DB copy is an automatic part of discovery.

Record representation gaps if only one component, no multi-member component or
no safe nesting is available; do not fabricate examples or enlarge scope.
Selection by component count does not cap recursive entry count, chart byte
volume, source-index capture cost or runtime. The application has no workload
limit switch; budget and stopping decisions must be approved independently.

## E. Pilot command specification

**DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.**

These are illustrative PowerShell templates, not a runnable pilot script. Each
block deliberately throws before the command. All angle-bracket values require
explicitly approved substitutions, including executable identity. No historical
real path, scan ID, component label or output location is filled in.

Current source report, only after discovery authorization and prerequisites:

```powershell
throw 'DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.'
$taskDotNet = '<APPROVED_DOTNET_10_HOST_ABSOLUTE_PATH>'
$taskCli = '<IDENTIFIED_CLEAN_RELEASE_BMS_AUDIT_DLL_ABSOLUTE_PATH>'
$taskSourceDb = '<APPROVED_PROJECT_OWNED_PHASE1_DB_ABSOLUTE_PATH>'
$taskDiscoveryReports = '<APPROVED_NEW_PHASE1_DISCOVERY_REPORT_DIRECTORY>'
& $taskDotNet $taskCli report --db $taskSourceDb --output $taskDiscoveryReports
$taskDiscoveryExit = $LASTEXITCODE
```

Limited pilot, only after separately approving the exact selections and binary
hash manifest (CLI/Core/dependencies, clean code revision and runtime):

**DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.**

```powershell
throw 'DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.'
$taskDotNet = '<APPROVED_DOTNET_10_HOST_ABSOLUTE_PATH>'
$taskCli = '<IDENTIFIED_CLEAN_RELEASE_BMS_AUDIT_DLL_ABSOLUTE_PATH>'
$taskSourceDb = '<APPROVED_PROJECT_OWNED_PHASE1_DB_ABSOLUTE_PATH>'
$taskSourceScan = '<ACTUAL_LATEST_COMPLETED_SCAN_ID>'
$taskComponentIds = @('<CURRENT_EXACT_ONLY_COMPONENT_ID_A>', '<CURRENT_EXACT_ONLY_COMPONENT_ID_B>')
$taskPilotDb = '<APPROVED_NEW_NONEXISTENT_PILOT_PHASE2_DATABASE_ABSOLUTE_PATH.db>'
$taskPilotReports = '<APPROVED_NEW_PILOT_REPORT_DIRECTORY>'
$taskPilotArgs = @('phase2', 'preflight', '--source-db', $taskSourceDb,
    '--source-scan', $taskSourceScan, '--db', $taskPilotDb, '--output', $taskPilotReports)
foreach ($taskId in $taskComponentIds) { $taskPilotArgs += @('--component', $taskId) }
$taskStartedUtc = [DateTime]::UtcNow.ToString('O')
& $taskDotNet $taskCli @taskPilotArgs
$taskExitCode = $LASTEXITCODE
$taskEndedUtc = [DateTime]::UtcNow.ToString('O')
```

The two placeholders demonstrate repeated --component syntax; an approved list
may have 2–5 complete components. Capture raw stdout/stderr, invocation arguments,
timestamps and process identity into separately approved new logs without
overwriting existing files. No synthetic performance launcher is appropriate
for this real pilot. No --timeout, --root, --resume or --accept-manifest option
exists for the implemented preflight command.

## F. Execution and stopping criteria

Future authorized operator responsibilities:

- Start approved logging/tracing before the first source operation; record the
  exact binary manifest, host identity, command arguments and UTC observation window.
- Record process exit, JSON SessionId/State/ManifestSha256 and summary provenance:
  DB native identity, length/SHA-256/stamps, source scan, build/tool/schema/contract,
  source population, selected/admitted/rejected components and outcome reasons.
- Record direct-chart file/byte/retry workload, unique path file/directory counts,
  root-relative memberships, per-root/union logical bytes, lower-bound flags,
  future accepted read estimate, rankings, nesting/shared paths and extensions.
- Inspect diagnostic severity and scope. `chart_revalidation_file_count` counts
  recorded revalidation rows, including failures/unexpected/missing paths; it is
  not necessarily the count of successfully hashed files. Byte counters are not
  end-to-end disk-I/O telemetry and exclude source DB fingerprint/metadata costs.
- Retain exact output paths, source baseline checks, logs, reports and receipts.

| Process exit | Meaning and required response |
| --- | --- |
| 0 | COMPLETED coherent preflight with at least one admitted component and no rejections; still requires independent acceptance. Warnings alone can coexist with 0. |
| 2 | COMPLETED coherent preflight with component rejections, or zero admitted components. Inspect entire-component outcomes/lower bounds; this is not automatically an infrastructure failure. Successful re-reporting preserves the session's result code. |
| 1 | Fatal configuration, provenance, ownership, integrity, DB or reporting failure. Stop; a completed sealed preflight can survive a later export failure. Do not infer its state from the process exit alone. |
| 130 | Cancellation. Stop and inspect actual stage/session/export evidence. Cancellation before publication cannot seal partial evidence; cancellation during export may leave an already COMPLETED preflight with incomplete reports. |

An unexpected OS/process exit or missing exit evidence is not a supported success
code. Stop and classify from the actual integrity/failure evidence, retaining
logs; never infer completion merely because the process disappeared.

There is no native real-preflight timeout, autonomous real-run supervisor, resume
or progress polling interface. Console Ctrl+C requests cooperative cancellation
via Console.CancelKeyPress; tokens are checked between operations. A blocked
native/filesystem call is not guaranteed to stop promptly. A future separately
reviewed external process supervisor would require its own authorization and
could leave interrupted evidence; the synthetic harness is not that supervisor.

**STOP and withhold expansion/acceptance** on ambiguous source/output paths,
unresolved aliases or historical roots, source fingerprint/identity/mtime changes,
scan mismatch, ownership/schema/sidecar failures, unexpected write/target-follow
activity, invalid seals, fatal errors, incomplete/missing reports, cancellation
or interruption. Use cooperative cancellation if unsafe activity is observed
during execution. Never delete source sidecars or retry by weakening safety.

A safely diagnosed descendant reparse/metadata problem can reject a complete
component while the CLI continues independent components. That coherent result
is not a global infrastructure failure. Pause the human workflow for diagnosis;
unresolved safety ambiguity is BLOCKED. Stale direct charts require a separately
authorized new Phase 1 scan and new Phase 2A before their component can be
readmitted. Inventory drift requires a new authorized Phase 2A. No automatic
refresh, root removal, component splitting, repair, cleanup or reuse of failed
output is permitted. A fatal failure may occur before any session exists.

## G. Independent evidence verification

After the separately authorized pilot, an independent reviewer must:

1. Check DB ownership/revision/integrity using a reviewed read-only viewer that
   refuses sidecars/recovery. Verify stage=PREFLIGHT, actual session state,
   clean runtime revision, source provenance seal_flag/digest and the final
   preflight SHA-256. Do not trust a CSV, filename or session ID alone.
2. Compare frozen selections to captured candidate_components, exact edges and
   complete memberships; no partial/ad hoc component admission. Reproduce
   source closure from captured charts/diagnostics with the unchanged
   RelationshipAnalyzer when independent tooling is available. Do not reopen
   later Phase 1 evidence to reinterpret historical admission.
3. Review every rejection, chart revalidation result, source diagnostic and
   preflight diagnostic. The exported diagnostics.csv contains preflight
   diagnostics; captured source_diagnostics require read-only inspection or
   the authorized discovery report. Summary error counts do not replace that review.
4. Reconcile per-root/union counts, root `.`/empty directories, unknown values,
   nesting/shared memberships and lower bounds. Inventory CSV rows are view
   memberships, not unique physical paths. Different hardlink paths count
   separately. Estimated Phase 2B bytes cover unique normalized paths in
   ADMITTED components, not allocated disk space or duplicate-removal savings.
   Zero inventory for an uninventoried rejected root does not prove emptiness.
5. Compare source DB native identity, bytes/length/SHA-256 and UTC mtime to the
   pre-discovery/pre-pilot baseline and captured fingerprint, then to a stable
   post-run baseline. Verify sidecars remain absent. Source change makes this
   protocol's stability acceptance fail; do not mutate the captured snapshot.
6. Examine available write/access evidence. Distinguish static code/test evidence,
   actual observed process I/O, other writers and uncovered time windows. Ordinary
   source access-time effects are not authorized bytes/last-write mutations.
7. Generate a second report from the same sealed session with the same identified
   executable into a different approved new directory. The implemented report
   operation recomputes the preflight/source seals. No seal-only CLI exists.
8. Compare all four report bytes, lengths and SHA-256 values; verify UTF-8/no BOM,
   LF, CSV quoting/ordering and matching session/manifest identity. Require absence
   of folder_comparisons.csv and content_groups.csv. Review both report_sets as
   COMPLETED and their four report_files receipts each. Preserve both sets.

**DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.**

```powershell
throw 'DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.'
$taskDotNet = '<SAME_APPROVED_DOTNET_10_HOST_ABSOLUTE_PATH>'
$taskCli = '<SAME_IDENTIFIED_CLEAN_RELEASE_BMS_AUDIT_DLL_ABSOLUTE_PATH>'
$taskPilotDb = '<ACTUAL_APPROVED_PILOT_PHASE2_DATABASE_ABSOLUTE_PATH.db>'
$taskSession = '<ACTUAL_COMPLETED_PREFLIGHT_SESSION_ID>'
$taskRepeatReports = '<APPROVED_DIFFERENT_NEW_REPORT_DIRECTORY>'
& $taskDotNet $taskCli phase2 report --db $taskPilotDb --session $taskSession --output $taskRepeatReports
$taskReportExit = $LASTEXITCODE
```

Report bytes must be identical, but **the Phase 2 DB file hash can legitimately
change** when re-export adds receipts. The immutable session/source/preflight
digests must remain unchanged. Fingerprint/archive the owned evidence DB only
after the final approved receipt operation, without replacing prior evidence.
The Phase 1 DB must remain byte/mtime-identical throughout the agreed window.

### Write tracing and limits of proof

Recommendation, subject to explicit approval: use an already available,
reviewer-approved OS file-I/O tracer. Record its version, configuration,
observation window, CLI PID/creation time and any descendants. Capture source/
output path identities, desired access, open/create dispositions, data writes,
truncation, rename/delete and last-write/attribute operations. Preserve raw events
and note filters, dropped events and performance impact. Native metadata handles
and read opens are not payload writes. Keep trace files/buffers outside sources;
never place sentinels or configuration files in the library, rewrite ACLs,
install/configure tools or enable a persistent service without separate approval.
No tracing tool is executed or downloaded in this preparation.

Interpret the observed process's writes against owned evidence/receipt/log paths;
record any external writer activity if the authorized tracing scope captures it.
Process-only filtering cannot prove that other processes left the entire library
unchanged. Trace gaps make write-absence claims inconclusive. The summary's
non_chart_payload_reads=0 is a declared implementation invariant, not a live OS
I/O counter. Use source inspection/tests and actual tracing to support it.

Sampling charts/resources or comparing selected mtimes is **not proof that the
whole library stayed byte-identical**. Do not add a whole-library resource-hash
sweep to Phase 2A. Observations concern an identified window, with documented
coverage; even matching metadata is not resource-content equality or a lasting
guarantee of the live tree. Any broader preservation audit needs separate scope.

## H. Pilot acceptance gate

These are **proposed human validation classifications**, not application states,
SQL outcomes, CLI exit codes or permission tokens. Apply BLOCKED first when a
known safety/integrity violation exists; missing proof otherwise is INCONCLUSIVE.

| Classification | Required evidence and consequence |
| --- | --- |
| PILOT_ACCEPTED | All prerequisites and G checks pass; valid COMPLETED/PREFLIGHT seal and complete repeated reports/receipts; exit 0, every frozen component ADMITTED with verified chart/inventory views, no unexplained source/preflight diagnostics, no lower-bound/unknown evidence for accepted views, unchanged source DB and adequate write-observation coverage. A reviewer may propose full Phase 2A; it remains separately unauthorized. |
| PILOT_ACCEPTED_WITH_DIAGNOSTICS | Global provenance/safety/seals/source preservation and report determinism pass, at least one whole component is ADMITTED, and all warnings/local rejections are fully explained and independently reviewed. Matching-byte CHART_METADATA_DRIFT or coherent component-level stale/incomplete results are possible. An explicit reviewer disposition must state coverage gaps, excluded components and any required new capture. A conditional full-run proposal is permitted only after that human disposition, never automatic expansion or readmission of stale members. |
| PILOT_BLOCKED | Proven source/output safety ambiguity, unauthorized write/target read, source change, ownership/schema/scan failure, invalid seal or global infrastructure failure preventing trustworthy capture. Stop; investigate through separately approved nonmutating steps. No full-run proposal until the blocker is resolved and an authorized new/reviewed pilot establishes the missing invariants. An export-only failure with a verified completed seal follows the INCONCLUSIVE rule below. |
| PILOT_INCONCLUSIVE | Insufficient trustworthy evidence: no admitted component despite coherent exit 2, cancelled/interrupted attempt, incomplete exports/receipts or tracing gaps without a proven violation. Retain everything. A completed seal surviving export failure is not sufficient pilot acceptance. Human authorization is needed for any report retry/new pilot; no full expansion. |

Exit 2 alone determines none of these classifications. Warnings are not forced
global failures; COMPLETED is not proof that every component was admitted.
Record classification, reviewer, date, evidence identities, representation gaps
and decision rationale. Pilot acceptance authorizes neither the next process nor
filesystem mutation. Only a new explicit full-run approval can cross that gate.

## I. Full Phase 2A validation

After independently accepted/dispositioned pilot evidence, propose a new full
authorization specifying current provenance, clean binary, safety/capacity budget,
new DB/reports/logs, verification/tracing scope and stopping decisions. Recheck
B/C against current evidence under that authorization. Omitting --component
selects **all dynamically derived EXACT_ONLY components**; no historical count
or label list is substituted. If the index changed, document and review the new
scope rather than claiming it is the accepted pilot snapshot.

**DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.**

```powershell
throw 'DOCUMENTED ONLY — NOT AUTHORIZED FOR EXECUTION.'
$taskDotNet = '<APPROVED_DOTNET_10_HOST_ABSOLUTE_PATH>'
$taskCli = '<IDENTIFIED_CLEAN_RELEASE_BMS_AUDIT_DLL_ABSOLUTE_PATH>'
$taskSourceDb = '<APPROVED_CURRENT_PHASE1_DB_ABSOLUTE_PATH>'
$taskSourceScan = '<ACTUAL_LATEST_COMPLETED_SCAN_ID_AT_FULL_RUN>'
$taskFullDb = '<APPROVED_DIFFERENT_NEW_FULL_PHASE2_DATABASE_ABSOLUTE_PATH.db>'
$taskFullReports = '<APPROVED_NEW_FULL_PREFLIGHT_REPORT_DIRECTORY>'
& $taskDotNet $taskCli phase2 preflight --source-db $taskSourceDb --source-scan $taskSourceScan --db $taskFullDb --output $taskFullReports
$taskFullExit = $LASTEXITCODE
```

Do not append the full capture to the pilot DB or infer it from pilot rows.
Repeat F/G verification and independently review source/selected/admitted/
rejected populations, all roots and top-20 rankings, unique path files/directories,
per-root/union/accepted logical and future read bytes, nested memberships/shared
paths, extensions/opaque files, reparse/unsafe/unknown entries, stale candidates,
incomplete inventories and lower bounds. Exact counts are observed results,
not historical pass thresholds. Retain deterministic repeated reports and
provenance. Large metadata workload may justify a human stop/new proposal,
not automatic trimming of roots or components. Stop after full Phase 2A review.

## J. Relationship to Phase 2B

Phase 2A supplies immutable source/admission provenance, direct-chart checks,
observation scope, metadata/anomalies and estimated unique-path read volume for
a **future human workload decision**. It supplies no non-chart byte identity.
Resource metadata does not prove content equality; observation-root admission
does not prove package equivalence, ownership of external resources or removability.

Phase 2B is not implemented; `phase2 hash` currently rejects the operation before
payload reads. No hash invocation is authorized or provided here. Future Phase
2B requires contract-compliant implementation/review, explicit schema compatibility
and evidence-capture decisions, and new authorization bound to a valid accepted
preflight digest. The current PREFLIGHT_ONLY schema is not a complete content
schema. Do not migrate it or add content rows as a planning shortcut.
No consolidation, copying, relocation, deduplication, chart rewrite or deletion
is authorized by preparation, pilot acceptance or full preflight completion.

## Operational checklist for future human review

- [ ] Approve readiness/discovery access to exact source DB and safety metadata.
- [ ] Attach independent review and clean binary/runtime/hash-manifest identities.
- [ ] Establish source baseline/latest scan and all active/historical exclusions.
- [ ] Approve native-disjoint new DB/report/log/trace locations and capacity reserve.
- [ ] Confirm quiescence, observation coverage and realistic stopping controls.
- [ ] Review 2–5 complete current components and representation gaps; authorize pilot.
- [ ] Preserve actual exit/session/seal, source baselines and all diagnostics.
- [ ] Verify second export bytes, receipt completeness and absence of unauthorized writes.
- [ ] Assign one H classification; stop for independent human acceptance.
- [ ] Separately authorize a fresh full capture if justified; never auto-expand.
- [ ] Keep Phase 2B and every source mutation outside the authorization.

## Preparation validation and mandatory stop

This task validates repository links, document readability/structure, template
syntax and correspondence to the inspected CLI. It executes no embedded block,
build, scan, preflight, report, benchmark, real-path probe or database query.
No concrete documentation discrepancy requires additional runtime tests; the
historical report/contract status differences above are explicitly contextualized.
No other document, contract, schema, code or generated evidence is modified.

Preparation assessment: `PHASE_2A_REAL_VALIDATION_PLAN_READY_FOR_REVIEW`.
This assessment approves neither pilot nor full execution. **STOP for independent
review and explicit authorization of the next step.**
