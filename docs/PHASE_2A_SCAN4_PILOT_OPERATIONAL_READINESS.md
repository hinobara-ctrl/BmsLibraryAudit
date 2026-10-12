# Phase 2A Scan 4 pilot operational readiness

Preparation date: **2026-10-11**.
Phase: `PHASE_2A.REAL_LIBRARY_OPERATIONAL_READINESS`.
Repository: `hinobara-ctrl/BmsLibraryAudit`.
Required and observed baseline: `7f9740291258b57d39fcfc33dc540b4cc8ef0e55`.

**PREPARATION ONLY. THE REAL PILOT IS NOT AUTHORIZED OR EXECUTED.**

This operational proposal is subordinate to the
[real-library validation plan](PHASE_2A_REAL_LIBRARY_VALIDATION_PLAN.md) and the
[governing contract](PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md).
It freezes a proposed selection, not current filesystem truth or execution
authority. **Heuristics discover. Exact evidence proves. Verified plans mutate.**

## A. Baseline and provenance

Repository inspection verified `main`, HEAD and origin/main both equal to the
required SHA, and an initially clean worktree. The five latest commits were
`7f97402`, `743d2f1`, `a3b3e63`, `808a19e` and `09063ad`, in descending order.
No Git history, index or branch was changed. Adding this document makes the
checkout dirty; a subsequent build here cannot be presumed clean.

Reviewed authority: [AGENTS.md](../AGENTS.md), [MASTER_CONTEXT.md](../MASTER_CONTEXT.md),
[README.md](../README.md), [SOURCES.md](SOURCES.md),
[Phase 1 validation](PHASE_1_REAL_LIBRARY_VALIDATION.md), the contract and plan above,
[Phase 2A implementation](PHASE_2A_IMPLEMENTATION_REPORT.md), and
[corrective hardening/performance](PHASE_2A_CORRECTIVE_HARDENING_AND_PERFORMANCE_REPORT.md).
The contract's proposed-status text and older report populations are historical;
current code implements Phase 2A only. These documents remain unchanged.

Evidence classes used throughout:

| Class | Meaning in this preparation |
| --- | --- |
| Verified repository fact | Inspected source, tests, documents or read-only Git results at this baseline. This is not a new independent code audit or test run. |
| User-reported operational fact | Scan 4 figures, report location, backup digest and provisional component selection supplied in the task. |
| Independently inspected persisted real evidence | None in this task. No Scan 4 CSV/JSON was supplied as an attachment or found in the reviewed repository documentation inventory; the external report location was not probed. |
| Pending verification | Current DB/scan, exact paths, reports, binary identity, destinations and operating conditions. |
| Execution blocker | Any unresolved prerequisite at the relevant authorization gate; it need not prevent this plan from being ready for review. |

The following paths are **reported identifiers only**, not inspected locations:

- Library: `E:\Juegos\beatp\canciones`.
- Active project-owned Phase 1 DB: `C:\BmsLibraryAuditData\audit.db`.
- Scan 4 report: `C:\BmsLibraryAuditData\reports\phase1-scan4-20261011_223743`.
- Historical backup: `C:\BmsLibraryAuditData\backups\audit-before-rescan-20261011_213338.db`.

Historical original/backup SHA-256, as reported:
`E8348B149B08A5FA770A38B15AEEDDD0D62CC95DC682ACAF9B3CA5FDB345D1E3`.
This identifies the **PRE-SCAN-4** database. It is not the expected active DB
hash. Scan 4 legitimately changed the active DB. Its current digest, native
identity, length and UTC mtime are unknown until separately authorized checks.
Do not open or hash the backup merely to reverify this historical assertion.

### Reported Scan 4 population

| Scan metric | Reported result |
| --- | ---: |
| Scan ID | 4 |
| Discovered charts | 243483 |
| Reused hashes | 226607 |
| Newly hashed charts | 16876 |
| New charts | 16876 |
| Changed charts | 0 |
| Missing charts | 0 |
| Errored charts | 0 |
| Unstable charts | 0 |
| Diagnostics | 0 |
| CLI exit code | 0 |

| Report metric | Reported result |
| --- | ---: |
| Verified charts | 243483 |
| Unique SHA-256 | 154925 |
| Analyzed chart folders | 19925 |
| Candidate folder pairs | 13149 |
| Duplicate SHA-256 groups | 53432 |
| EXACT_ONLY components | 417 |
| EXACT_ONLY components with 2 members | 407 |
| EXACT_ONLY components with 3 members | 10 |
| Warnings | 0 |
| Errors | 0 |

| Relationship | Reported count |
| --- | ---: |
| EXACT_CHART_SET | 437 |
| A_SUBSET_OF_B | 2512 |
| B_SUBSET_OF_A | 2275 |
| HIGH_OVERLAP | 618 |
| MEDIUM_OVERLAP | 3039 |
| SHARED_CHARTS | 4268 |

Arithmetic reconciliation of supplied numbers: 226607 + 16876 = 243483;
407 + 10 = 417; 407 + 10 * 3 = 437; relationship counts sum to 13149.
This checks consistency of the report supplied in prose, not persisted results.
The incremental cache reused hashes by path/size/mtime; it did not independently
rehash all 226607 historical charts. Phase 2A must fresh-read selected direct charts.

Previously recorded synthetic validation includes 107 passing .NET cases,
both CLI smokes, QUICK/lifecycle checks and the appended EXTENDED SUCCESS:
500 admitted components, 1000 roots, 2000 charts, 20000 non-chart placeholders,
42000 memberships and runtime `UNKNOWN_DIRTY`. These are inspected historical
documentation claims, not rerun results or clean runtime evidence. The appended
SUCCESS supersedes the earlier launch's PENDING observation. It supplies no
forecast for a 243483-chart source capture or real HDD inventory.

## B. Pilot scope freeze

The four complete components below are approved **planning candidates**. No exact
member path was supplied here; collection/title descriptions cannot substitute
for original paths from the actual CSV/index. None is currently verified for execution.

| Component label | Members | Reported distinct direct-chart SHA-256 per member | Internal exact pairs | Reported selection rationale |
| --- | ---: | ---: | ---: | --- |
| EXACT_ONLY_000003 | 2 | 1 | 1 | `8k` and `osu converts`; simple converted-chart case. |
| EXACT_ONLY_000004 | 2 | 7 | 1 | `NESARIA`, `bms2/Insane111/Overjoy` and `bms/555`. |
| EXACT_ONLY_000054 | 3 | 3 | 3 | `depth meter`, across three collections; complete three-member admission. |
| EXACT_ONLY_000068 | 3 | 2 | 3 | Japanese-titled chart across three collections; proposed Unicode/long-path coverage. |

Expected selection arithmetic: **4 components, 10 observation roots, 8 exact
pair relationships, 31 (root, distinct chart SHA-256) memberships** (2 + 14 + 9 + 6).
These are per-root hash-set memberships, not a count of globally distinct hashes
or exactly 31 physical chart files. Multiple files in one root can share a hash;
actual physical counts, successful reads and byte volume remain unverified.
`RelationshipAnalyzer` sets `PhysicalChartFiles` from `group.Count()` and
`charts_a/b` from `Hashes.Count`; `common` counts shared distinct hashes.
Thus 1, 7, 3 and 2 apply to `charts_a`, `charts_b` and `common` for the selected
exact edges. Record observed `physical_files_a/b` separately: each must be at
least its endpoint's distinct-hash count and consistent across that root's edges,
but the two roots need not have equal physical counts. No physical count is invented.

After real-discovery authorization, freeze an operator/reviewer-approved record:

| Binding | Required frozen evidence |
| --- | --- |
| Source identity | Original/normalized DB path, resolved path, native volume/file identity, length, UTC mtime/ticks, SHA-256, ownership/schema/comparison mode. |
| Scan | Latest COMPLETED ID exactly 4, start/end, statistics, diagnostics and configured plus all historical roots; no intervening source transaction. |
| Report set | All four Phase 1 report paths, byte lengths/SHA-256, summary provenance and consistency with that current DB. A CSV filename is not provenance. |
| Navigation labels | All four labels scoped to this scan and DB fingerprint; `kind=EXACT_ONLY`, exact member counts, no omitted member. |
| Memberships | All ten full original paths and normalized keys, bound to their component and source library root, preserving case/Unicode spelling. Do not fill paths from titles or guesses. |
| Chart facts/edges | Every current direct chart path, physical count and distinct SHA-256 set; all eight unordered internal EXACT_CHART_SET edges with positive equal distinct counts. No external exact edge may enlarge a selected closure. |
| Future sealed identity | Map each label and every frozen member/edge/chart set to `candidate_components.component_key`, distinct-set digest and captured source seal in the actual Phase 2 session; then bind the final preflight digest. |

Labels are ordinal navigation identifiers, not immutable cross-scan identities.
A latest scan other than 4, changed fingerprint, remapped membership or changed
population stops this proposal and requires a new human scope decision. No new
scan, guessed replacement label, partial component or healthy-pair salvage.
Library changes cannot be excluded by a stable DB alone; quiescence/observation
and fresh chart/inventory checks are still required. Stale direct-chart evidence
requires a separately authorized new Phase 1 scan and new Phase 2A.

No selected nested-root case is established. Unicode/long-path suitability is a
reported rationale, pending exact-path inspection. Resource populations, shared
paths, unsafe entries and extension diversity remain unknown until an authorized
pilot. Do not traverse source folders to improve this selection or estimate counts.

## C. Operational environment

Source inspection verified the requirements below; the actual pilot machine,
installed host, deployed binary and budgets have not been validated in this task.

| Check before execution | Required evidence / stopping rule |
| --- | --- |
| Windows and native safety | Record OS/architecture; required ordinary disk handles, volume-GUID resolution, file IDs/link counts and directory case-mode queries must work. Unsupported/unknown checks fail closed; case-sensitive Windows directories are refused. No demonstrated broad UNC/alternative-filesystem support is claimed. |
| .NET 10 SDK/runtime | `global.json` requests 10.0.100 with latestFeature, no prerelease; projects target net10.0. Record exact approved host path/version, installed usable .NET 10 SDK/runtime and architecture. Historical SDK 10.0.401 is not proof of the current installation. No installation/build is performed here. |
| Operator shell | PowerShell 7+ for the documented procedures; check major version before platform-specific variables. The synthetic benchmark harness is not used. |
| Clean source and Release | Independently identify approved revision, clean Git status at build time, locked dependencies and Release build log. Use an already established clean binary or a separately authorized clean build; no commit solely for testing and no provenance overrides. |
| Build attestation | Inspect CLI and Core informational versions and Core `PhaseTwoBuildState`. Core must resolve CLEAN plus a valid lowercase 40-character revision to the approved SHA. `UNKNOWN` or `UNKNOWN_DIRTY` blocks this protocol. An informational SHA alone is insufficient. |
| Deployed binary manifest | SHA-256 and lengths for CLI, Core, managed dependencies, selected native SQLite dependency, deps/runtimeconfig files and host identity. Record paths, architecture and load context; verify before invocation and after all exports. No rebuild/replacement during the window. |
| Memory | Approve measured available memory/commit headroom and a stop threshold. Full index arrays, duplicate groups, folder sets and candidate edges remain in memory. No numeric safe budget or hard cap follows from four selections. |
| Storage | Approve free space/reserve on DB, reports, logs/traces and temp volumes. Budget source capture, entries/memberships/indexes, rollback journal/TEMP sorting, two exports, receipts and observations. Report expansion is not bounded by 31 per-root hash-set memberships. |
| Quiescence | Identify operator, reviewer, window and other audit/simulator/library synchronization activity. Stop downloads/extraction/reorganization and Phase 1/2 writers by human arrangement; do not kill processes by broad name. |

[Build provenance](../src/BmsLibraryAudit.Core/PhaseTwoBuildProvenance.cs) and
[build target](../Directory.Build.targets) bind the Core revision conservatively
at build time; they do not prove reproducible binaries or exclude concurrent
build-source edits. No existing executable was loaded or declared clean here.
The embedded design baseline `a300b54...` and Phase 1 implementation baseline
`53c45ca...` are historical provenance fields, distinct from the runtime build SHA.
If a future approved clean revision differs from this baseline, review that
difference explicitly rather than substituting a SHA in evidence.

## D. Safe destination strategy

Proposed layout, entirely **unapproved placeholders**:

| Purpose | Proposed location |
| --- | --- |
| Evidence area | `<APPROVED_NATIVE_DISJOINT_EVIDENCE_ROOT>\<UNIQUE_PILOT_RUN>` |
| New Phase 2 DB | `<RUN>\database\phase2.db` |
| Initial reports | `<RUN>\reports-initial` |
| Repeated reports | `<RUN>\reports-repeat` |
| Execution logs and invocation record | `<RUN>\logs` |
| Optional approved tracer evidence | `<RUN>\trace` |
| Final manifest and review disposition | `<RUN>\verification` |
| Temporary processing area, if separately configured | `<APPROVED_TEMP_DIRECTORY_OUTSIDE_PROTECTED_SOURCES>` |

An ordinary local NTFS area is a recommendation, not approval. If an operator
proposes a location under `C:\BmsLibraryAuditData`, it is an **unverified candidate
destination**, including its ancestors and relationship to the active DB. The
OneDrive repository location and synthetic harness workspace exception do not
establish production output safety. Never reuse a benchmark helper to bypass it.

The DB must be nonexistent immediately before initial creation, with `.db`
extension and no pre-created empty reservation. Require new report directories;
the CLI also allows empty ordinary ones. Logs/trace/verification are separate
siblings so neither report directory contains a DB or source. No overwrite,
cleanup, migration or reuse of prior/failed pilot evidence is allowed.

After discovery/safety authorization, reviewed read-only native inspection must:

1. Resolve original paths and existing ancestors to native final paths; validate
   nonexisting suffixes without creating destinations. Check reparse/junction
   ancestors and directory case mode, SUBST/short-name/volume aliases and file IDs.
2. Protect every current/configured/active/inactive historical source root and
   observation root. Unknown historical-root resolution is a blocker; do not drop it.
3. Prove native disjointness from sources and audit.db, including source DB aliases.
   Reject destinations inside roots and report directories containing roots or
   either DB. Textual prefix comparison alone proves none of these protections.
4. Inspect existing writable DB identities/link counts when applicable; multiple
   hardlinks or equality with source DB identity are refused. Ordinary source
   hardlink paths remain separate read-only inventory identities.
5. Refuse every existing `-journal`, `-wal` or `-shm` object at source/prospective DB
   paths, including directories, dangling links and reparse collisions. Never
   recover, checkpoint, delete or rename them as preparation.
6. Reject unsafe containing directories, alternate streams/device-style names,
   non-disk handles, unsupported filesystems or inconclusive native queries.
   Recheck binding before actual writes under the later execution authorization.

[Native safety](../src/BmsLibraryAudit.Core/PhaseTwoSafety.cs) supplies these
production checks, with [PathPolicy](../src/BmsLibraryAudit.Core/PathPolicy.cs).
There is **no harmless Phase 2A CLI dry-run or safety-only command**. Preflight
must never be used as a path probe. Reviewed nonmutating inspection tooling is
an unresolved operational prerequisite, not a new tool implemented by this task.

## E. Current Phase 1 integrity checks

**Future read-only discovery only; separate authorization required.** No real DB
was opened, statted or hashed here. No new Phase 1 report is run during preparation.

The operator must execute this reproducible sequence through reviewed tooling:

1. Approve exact DB/report paths and a safe observation/log destination. Establish
   quiescence before the first access; record UTC time, observer/tool versions and
   evidence coverage. Do not assume external Windows paths are available.
2. Validate source ancestors/native path, regular-file kind and all sidecar paths
   without following targets or opening SQLite. If a sidecar/ambiguity exists,
   STOP. No GUI/default connection that might recover/create sidecars is acceptable.
3. Acquire `FileAccess.Read`/`FileShare.Read` guard denying write/delete sharing.
   Compare pre-open native identity/stamps with handle facts and path binding.
   Failure to acquire exclusion is SOURCE_BUSY. Stream bounded SHA-256, recording
   length, UTC mtime/ticks, bytes read and native identity. This is the **current**
   baseline, never the pre-Scan-4 backup digest.
4. Only while guarded, use Microsoft.Data.Sqlite `Mode=ReadOnly`, `Pooling=false`,
   no `immutable=1`, no ATTACH, no persistent PRAGMA, no recovery/checkpoint or
   backup-copy shortcut. Use one deferred read transaction and read queries below.
   A reviewed tool must enforce these properties before it is approved; read-only
   SQL text alone cannot establish safe opening. Tool selection remains BLOCKED
   for execution until reviewed, rather than falling back to an unverified viewer.
5. Validate ownership/schema/root/chart consistency equivalent to
   [PhaseOneSourceCapture](../src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs).
   Latest COMPLETED must be 4, with coherent completion, stats, diagnostics and
   chart rows. A source scan ID does not reconstruct an immutable old snapshot.
6. Recompute discovery using the unchanged
   [RelationshipAnalyzer](../src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs) over
   all captured chart-index rows and Scan 4 diagnostics. Compare persisted reports
   and whole-component closure without opening chart files. Record the B freeze.
   If no reviewed tool can reproduce this safely, keep that check pending; a CSV
   review alone does not prove equivalence to the current DB.
7. Repeat guard/handle/path facts, fingerprint and sidecar checks before releasing
   the read transaction/guard. Record unchanged results. Repeat immediately before
   pilot and after pilot/re-export under their respective approvals. The production
   source guard lasts capture only, not the entire inventory/export window.

Read-query specification for that reviewed tool (nonexecuted; identifiers are
existing Phase 1 schema names, not new CLI flags):

| Purpose | Read query / required interpretation |
| --- | --- |
| Ownership | `PRAGMA application_id` = `0x424D5341`; `PRAGMA user_version` = 1; `SELECT key,value FROM schema_metadata ORDER BY key` includes tool `BmsLibraryAudit`, schema_version `1`, path_comparison `windows-ordinal-ignore-case`. |
| Integrity | `PRAGMA quick_check` returns `ok`; `PRAGMA foreign_key_check` returns no rows. No schema migration/exact Phase 2 schema assumptions applied to Phase 1. |
| Latest scan | `SELECT id,started_utc,completed_utc,status,roots_json,statistics_json FROM scan_sessions WHERE status='COMPLETED' ORDER BY id DESC LIMIT 1`; require actual ID 4. Also inspect `SELECT id,status FROM scan_sessions ORDER BY id` for unexpected unfinished activity. |
| Protected roots | `SELECT id,absolute_path,normalized_path,active FROM library_roots ORDER BY normalized_path`; include inactive roots. Compare active flags to latest roots_json, native-resolve all exclusions without contents traversal. |
| Chart provenance | `SELECT root_id,absolute_path,normalized_path,parent_folder,filename,extension,size,mtime_utc_ticks,md5,sha256,first_seen_scan,last_seen_scan,present,status FROM chart_files ORDER BY normalized_path`; validate captured path/root/hash formats and present rows' last_seen_scan=4. Present/OK counts and analysis must match the supplied report. |
| Diagnostics | `SELECT severity,code,path,message FROM diagnostics WHERE scan_id=4 ORDER BY path,code,message,severity`; supplied count is zero; inspect every actual finding. |

Native source identity/exclusion cannot be established by `Get-FileHash`, SQLite
read-only mode, process-name checks or an unchanged mtime alone. The application
[Phase 1 reader/report code](../src/BmsLibraryAudit.Core/ReportWriter.cs) is not a
standalone native provenance verifier. Phase 1 scanning is transactional and its
cache uses metadata; [AuditDatabase](../src/BmsLibraryAudit.Core/AuditDatabase.cs)
current chart rows remain mutable across later scans.

### Existing-report inspection template

**DOCUMENTED ONLY — REAL EXECUTION NOT AUTHORIZED.** This future template reads
only the approved existing report files; it performs no DB connection or source
filesystem traversal. Paths in CSV fields remain strings. It checks the supplied
summary and selection, but cannot independently establish current DB provenance.
Human review must additionally freeze original paths, full exact closure and
individual source chart facts using the guarded procedure above.

```powershell
throw 'DOCUMENTED ONLY — REAL EXECUTION NOT AUTHORIZED.'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskReports = '<APPROVED_EXISTING_SCAN4_REPORT_DIRECTORY>'
$taskSummary = Get-Content -LiteralPath (Join-Path $taskReports 'summary.json') -Raw -Encoding utf8 | ConvertFrom-Json
$taskExpected = @{ scan_id=4; verified_chart_files=243483; unique_sha256=154925;
    analyzed_chart_folders=19925; candidate_folder_pairs=13149;
    duplicate_sha256_groups=53432; exact_only_cluster_count=417; warning_count=0; error_count=0 }
foreach ($taskKey in $taskExpected.Keys) {
    if ($taskSummary.$taskKey -ne $taskExpected[$taskKey]) { throw "Scan 4 report mismatch: $taskKey" }
}
$taskRelations = @{ EXACT_CHART_SET=437; A_SUBSET_OF_B=2512; B_SUBSET_OF_A=2275;
    HIGH_OVERLAP=618; MEDIUM_OVERLAP=3039; SHARED_CHARTS=4268 }
foreach ($taskKey in $taskRelations.Keys) {
    if ($taskSummary.relationship_counts.$taskKey -ne $taskRelations[$taskKey]) { throw "Relationship mismatch: $taskKey" }
}
if ($taskSummary.exact_only_cluster_size_distribution.'2' -ne 407 -or
    $taskSummary.exact_only_cluster_size_distribution.'3' -ne 10 -or
    @($taskSummary.exact_only_cluster_size_distribution.PSObject.Properties).Count -ne 2) { throw 'Component distribution mismatch' }
$taskClusters = @(Import-Csv -LiteralPath (Join-Path $taskReports 'clusters.csv') -Encoding utf8)
$taskPairs = @(Import-Csv -LiteralPath (Join-Path $taskReports 'folder_candidates.csv') -Encoding utf8)
$taskSelection = @(
    @{ Id='EXACT_ONLY_000003'; Members=2; DistinctCharts=1; Edges=1 },
    @{ Id='EXACT_ONLY_000004'; Members=2; DistinctCharts=7; Edges=1 },
    @{ Id='EXACT_ONLY_000054'; Members=3; DistinctCharts=3; Edges=3 },
    @{ Id='EXACT_ONLY_000068'; Members=3; DistinctCharts=2; Edges=3 })
$taskPhysicalCounts = [Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
foreach ($taskComponent in $taskSelection) {
    $taskRows = @($taskClusters | Where-Object { $_.kind -ceq 'EXACT_ONLY' -and $_.cluster_id -ceq $taskComponent.Id })
    if ($taskRows.Count -ne $taskComponent.Members -or
        @($taskRows | Where-Object { [int]$_.folder_count -ne $taskComponent.Members }).Count -ne 0) { throw 'Incomplete cluster' }
    $taskMembers = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($taskRow in $taskRows) {
        if (-not $taskMembers.Add($taskRow.folder_path)) { throw 'Duplicate member path' }
    }
    $taskEdges = @($taskPairs | Where-Object { $taskMembers.Contains($_.folder_a) -and $taskMembers.Contains($_.folder_b) })
    if ($taskEdges.Count -ne $taskComponent.Edges) { throw 'Internal pair coverage mismatch' }
    foreach ($taskEdge in $taskEdges) {
        if ($taskEdge.relationship -cne 'EXACT_CHART_SET' -or
            [long]$taskEdge.charts_a -ne $taskComponent.DistinctCharts -or
            [long]$taskEdge.charts_b -ne $taskComponent.DistinctCharts -or
            [long]$taskEdge.common -ne $taskComponent.DistinctCharts) { throw 'Exact edge/distinct hash count mismatch' }
        if ([long]$taskEdge.physical_files_a -lt [long]$taskEdge.charts_a -or
            [long]$taskEdge.physical_files_b -lt [long]$taskEdge.charts_b) { throw 'Physical count below distinct hash count' }
        foreach ($taskEndpoint in @(
            @{ Path=$taskEdge.folder_a; PhysicalFiles=[long]$taskEdge.physical_files_a },
            @{ Path=$taskEdge.folder_b; PhysicalFiles=[long]$taskEdge.physical_files_b })) {
            if ($taskPhysicalCounts.ContainsKey($taskEndpoint.Path) -and
                $taskPhysicalCounts[$taskEndpoint.Path] -ne $taskEndpoint.PhysicalFiles) { throw 'Inconsistent physical count for root' }
            $taskPhysicalCounts[$taskEndpoint.Path] = $taskEndpoint.PhysicalFiles
        }
    }
    $taskRows | Select-Object kind,cluster_id,folder_count,folder_path
    $taskEdges
}
foreach ($taskName in @('summary.json','duplicate_charts.csv','folder_candidates.csv','clusters.csv')) {
    $taskFile = Join-Path $taskReports $taskName
    Get-Item -LiteralPath $taskFile | Select-Object FullName,Length,LastWriteTimeUtc
    Get-FileHash -LiteralPath $taskFile -Algorithm SHA256
}
```

Compare scan cache statistics to the first table using actual serialization
fields, completion/root timestamps and every diagnostic. Validate CSV row counts,
all 417 EXACT_ONLY closures/distribution and all 13149 relationships against the
summary and analysis; inspect duplicate-chart rows as needed. The template is a
bounded selection/summary check, not that entire independent verification.
Do not generate a replacement report or access the external path now.

## F. Pilot command preparation

All templates deliberately throw first and remain **nonexecuted planning text**,
not a launcher. Guards must remain in this document. Future execution requires
an independently reviewed invocation after the corresponding I gate, with all
placeholders bound to approved evidence. No build-on-run `dotnet run` is used.

**DOCUMENTED ONLY — REAL EXECUTION NOT AUTHORIZED.** The verified latest scan
value must come from E's current guarded check, not this task's prose/report.
The explicit conditional value 4 is also reasserted by the production source
capture at invocation. If it differs, refuse; never remap labels to a later scan.

```powershell
throw 'DOCUMENTED ONLY — REAL EXECUTION NOT AUTHORIZED.'
$ErrorActionPreference = 'Stop'
$taskVerifiedLatestScanId = '<LATEST_COMPLETED_SCAN_ID_FROM_APPROVED_IMMEDIATE_DB_VERIFICATION>'
[long]$taskParsedScanId = 0
if (-not [long]::TryParse($taskVerifiedLatestScanId, [ref]$taskParsedScanId) -or $taskParsedScanId -ne 4) {
    throw 'STOP: latest authorized and independently verified COMPLETED scan must be 4.'
}
$taskDotNet = '<APPROVED_DOTNET_10_HOST_ABSOLUTE_PATH>'
$taskCli = '<INDEPENDENTLY_IDENTIFIED_CLEAN_RELEASE_CLI_DLL_ABSOLUTE_PATH>'
$taskSourceDb = '<APPROVED_CURRENT_PHASE1_DB_ABSOLUTE_PATH>'
$taskPilotDb = '<APPROVED_NEW_NONEXISTENT_PHASE2_DB_ABSOLUTE_PATH.db>'
$taskInitialReports = '<APPROVED_NEW_INITIAL_REPORT_DIRECTORY>'
$taskPilotArgs = @('phase2','preflight','--source-db',$taskSourceDb,'--source-scan','4',
    '--db',$taskPilotDb,'--output',$taskInitialReports,
    '--component','EXACT_ONLY_000003','--component','EXACT_ONLY_000004',
    '--component','EXACT_ONLY_000054','--component','EXACT_ONLY_000068')
$taskStartedUtc = [DateTime]::UtcNow.ToString('O')
& $taskDotNet $taskCli @taskPilotArgs
$taskExitCode = $LASTEXITCODE
$taskEndedUtc = [DateTime]::UtcNow.ToString('O')
```

Record approved argument list, binary manifest, UTC window, PID/creation time,
raw stdout/stderr and exact exit code in new approved logs. Logging/tracing
setup is separately reviewed; the example intentionally provides no unattended
launcher, timeout, polling, restart or automatic next command. The current
[CLI parser](../src/BmsLibraryAudit.Cli/Program.cs) supports only the options used
here for preflight. No dry-run, timeout, resume, root or accepted-manifest switch
exists for it.

**DOCUMENTED ONLY — REAL EXECUTION NOT AUTHORIZED.** Separate post-pilot
report/verification approval is needed; use the actual completed session ID,
unchanged binary and a different new directory. Reporting may inspect native
source/root path metadata for destination safety, but does not open source DB
payload or enumerate/hash the library. Receipts write the owned Phase 2 DB.

```powershell
throw 'DOCUMENTED ONLY — REAL EXECUTION NOT AUTHORIZED.'
$taskDotNet = '<SAME_APPROVED_DOTNET_10_HOST_ABSOLUTE_PATH>'
$taskCli = '<SAME_IDENTIFIED_CLEAN_RELEASE_CLI_DLL_ABSOLUTE_PATH>'
$taskPilotDb = '<ACTUAL_APPROVED_PILOT_PHASE2_DB_ABSOLUTE_PATH.db>'
$taskSessionId = '<ACTUAL_COMPLETED_PREFLIGHT_SESSION_ID>'
$taskRepeatReports = '<APPROVED_DIFFERENT_NEW_REPEAT_REPORT_DIRECTORY>'
& $taskDotNet $taskCli phase2 report --db $taskPilotDb --session $taskSessionId --output $taskRepeatReports
$taskReportExitCode = $LASTEXITCODE
```

## G. Workload and interruption strategy

The [runner](../src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs) first captures
all Phase 1 chart-index rows/roots/diagnostics and fingerprints the source DB
twice, then derives all exact components before selecting four. Source arrays,
folder hash sets, duplicate groups and candidate edges incur complete-population
memory and analysis cost. Candidate generation includes per-hash folder-pair
fanout; four components impose no hard processing-time or memory bound.

For the frozen selection, 31 reported per-root distinct-hash memberships are a
planning count, not 31 necessarily distinct physical files or an observed
successful-file/byte total. Every physical direct chart path still requires fresh
revalidation, including copies sharing one hash within a root. Fresh SHA-256 reads use 64 KiB buffers,
native handle/stamp checks and at most one retry. No Phase 1 cache result replaces
them. Entire components fail admission on changed/unreadable/unstable membership.
[Filesystem code](../src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs) reads only
selected direct chart payloads; other resources and unselected nested charts
receive metadata/identity inspection, not hashing, parsing or decompression.

Recursive inventory and second verification cover the topmost observation union,
including root `.`, empty directories, hidden/system/opaque entries. Overlapping
roots project multiple memberships onto one normalized entry; per-root totals
may overlap. Different hardlink paths remain distinct. No nested coverage is
claimed for this selection. Unknown fields are NULL, not zero; rejected or
incomplete views give lower bounds and can have unperformed inventories.

Cost includes native ancestor checks, directory I/O, indexed SQL work, 256-entry
inventory batches, seals, TEMP sorting, DB/index/journal overhead and report
membership expansion. HDD seeks, Unicode/long paths, antivirus and Windows
metadata limitations may dominate even with little chart payload. The synthetic
EXTENDED measurement is not a runtime estimate; none is proposed. Accepted
estimated Phase 2B bytes exclude rejected views, metadata and retry overhead and
do not mean allocated space, savings or resource-content equality.

| Observed exit | Operational interpretation |
| --- | --- |
| 0 | Coherent COMPLETED preflight with admitted components and no rejections; still verify all H evidence. Warnings can coexist with 0. |
| 2 | Coherent COMPLETED preflight with rejection(s) or zero admitted components; review complete component outcomes. Not automatically a global failure. |
| 1 | Fatal configuration/provenance/safety/DB/integrity/export failure; STOP. Export failure may leave a valid completed preflight seal but incomplete receipts/reports. |
| 130 | Cancellation; inspect actual session/export stage. Before publication no accepted final seal; during export a previously completed seal can survive. |
| Missing/unexpected OS exit | Never success by inference; retain evidence and assess actual state/integrity. |

Ctrl+C uses `Console.CancelKeyPress` to request cooperative cancellation. Checks
occur between operations; blocked native/HDD calls need not stop promptly. Agree
memory/disk/time observation thresholds and an operator response in advance.
There is no production timeout, progress interface, resume or real-run supervisor.
Any external supervisor is optional and separately approved, bound to exact
owned PID/creation time/descendants, and may leave partial evidence. Never reuse
the synthetic benchmark harness or automatically restart an interrupted process.

STOP for scan/source/binary changes, unsafe paths/writes/target following,
inadequate capacity, ownership/sidecars/seal failures, cancellation or missing
evidence. Preserve logs, partial reports, DB and any sidecars without recovery,
cleanup or retry. A later writable preflight can mark old RUNNING rows FAILED;
do not trigger that bookkeeping as a repair/resume. Reporting refuses RUNNING
sessions and active/interrupted export records. No session may be presumed to
exist after an early failure. Component-local diagnostics may allow the CLI to
finish independent components; the human workflow still stops for disposition.

## H. Verification and acceptance

Independent review after separately approved execution must check:

- Actual CLI exit and DB stage/state, source/application/schema identity,
  current scan=4, stable clean runtime revision and binary manifest; match raw
  terminal JSON to persisted evidence, not filenames alone.
- Frozen four selected components, all ten memberships/eight exact edges and
  source chart facts against the captured source seal. Verify every admitted
  and rejected component, every reason and direct-chart result. No partial admission.
- Chart revalidation success/membership/digests/stamps/attempts and bytes. A
  `chart_revalidation_file_count` is recorded rows, not necessarily successful
  hashes. Physical chart counts and distinct-set counts stay separate.
- Inventory kinds, counts, per-root/union bytes, memberships, nesting/shared
  paths, extensions, largest roots, readability NOT_TESTED, unknowns/lower bounds,
  reparse/metadata/drift findings and second-pass completeness. Zero counts for
  an unperformed inventory do not prove an empty root.
- Every source diagnostic (captured `source_diagnostics`) and Phase 2 diagnostic.
  Exported `diagnostics.csv` alone omits source diagnostics; reconcile both sets.
- Source DB native identity/length/SHA-256/UTC mtime to its immediate pre-pilot
  baseline and captured fingerprint, and after the approved reporting window;
  sidecars remain absent. The historical backup hash is irrelevant to this test.
- [Canonical seals](../src/BmsLibraryAudit.Core/PhaseTwoManifest.cs): valid sealed
  source capture plus final preflight digest, COMPLETED/PREFLIGHT, exact same
  selection graph, immutable facts and compatible platform/contract metadata.
  A read-only reviewer tool must validate DB ownership/integrity and reproduce
  canonical identity; reports/summary claims alone are insufficient.
- Exactly four initial and four repeat reports: `session_summary.json`,
  `observation_roots.csv`, `filesystem_inventory.csv`, `diagnostics.csv`.
  Verify each pair byte-for-byte, lengths/SHA-256, UTF-8 without BOM, LF,
  invariant ordering/values and Unicode/CSV escaping. The
  [report writer](../src/BmsLibraryAudit.Core/PhaseTwoReportWriter.cs) revalidates
  seals and uses persisted evidence with separate receipts.
- Two corresponding `report_sets` COMPLETED with matching session/evidence
  identity/destinations; each has four completed `report_files` receipts with
  actual bytes/hash. Preserve failed attempts too; do not overwrite outputs.
- No CONTENT session/evidence, no `folder_comparisons.csv` or `content_groups.csv`,
  content scope NONE and `phase2b_executed=false`. The reported non-chart-read
  zero is an implementation invariant, not OS telemetry or byte-equality proof.
- No unauthorized source writes in the approved window: distinguish source/test
  inspection from actual trace evidence, other processes and missing coverage.

The immutable preflight/source digests must survive re-export. **The Phase 2 DB
file hash may legitimately change** because export receipts are mutable owned
bookkeeping. Fingerprint its final file only after the last approved receipt
operation; retain earlier identities if recorded. Phase 1 DB preservation is a
separate invariant and receives no receipt writes. Normal owned SQLite lifecycle
does not authorize recovering or changing source sidecars.

Optional write tracing requires its own approval: use an already available,
reviewer-approved observer, record version/configuration, PID/creation time and
descendants, paths/native identities, UTC start/end, filters, dropped events and
overhead. Observe write/create/truncate/rename/delete/attribute/last-write
operations and handle access/dispositions. Store raw logs outside sources; no
sentinels, ACL changes, installation or persistent service activation here.
[Writer exclusion](../src/BmsLibraryAudit.Core/PhaseTwoWriter.cs) serializes this
application's owned DB writer; it does not prove absence of unrelated writers.
Process-only tracing cannot prove all other processes left the library unchanged.
Unobserved windows/dropped events limit or invalidate absence claims. Access-time
effects are distinct from tool writes to bytes/last-write timestamps. Sampling
or metadata equality is not proof of whole-library byte preservation; no extra
resource hashing sweep is authorized.

These proposed **human classifications** reuse validation-plan H; they are not
CLI states or authorization tokens. Apply a known violation's BLOCKED rule first.

| Classification | Required evidence / decision |
| --- | --- |
| PILOT_ACCEPTED | All prerequisites and H evidence pass; exit 0, valid COMPLETED/PREFLIGHT seal, all four components ADMITTED, complete views with no unknown/lower bounds, no unexplained diagnostics, source unchanged, clean binary/runtime, identical exports/completed receipts and adequate write-observation coverage. Reviewer may propose a separately authorized full run. |
| PILOT_ACCEPTED_WITH_DIAGNOSTICS | Global safety/provenance/source/seals/determinism and required preservation evidence pass; at least one whole component ADMITTED; every warning/local rejection or representation gap explained and independently dispositioned. Record exclusions, remaining evidence and required new captures. Missing safety/write-observation proof cannot be waived as a diagnostic. No automatic readmission or full run. |
| PILOT_BLOCKED | Known safety/source/binary/scan/ownership/schema violation, unauthorized write/target read, invalid seal or global infrastructure failure preventing trustworthy capture. STOP; investigate only through separately approved nonmutating steps. |
| PILOT_INCONCLUSIVE | Missing trustworthy proof, no admitted components despite coherent exit 2, cancellation/interruption, missing reports/receipts or tracing gaps without proven violation. An export-only failure with verified completed seal is still inconclusive for pilot acceptance. Retain all evidence; retry/new capture needs authorization. |

No classification is assigned to this unexecuted pilot. Record reviewer, date,
evidence manifest, coverage limits and rationale after the authorized review.

## I. Authorization and stop gates

| Gate | Current status | Required separate explicit human authorization |
| --- | --- | --- |
| 1 Preparation/repository review | Authorized; this document and static inspection only | Independent review of the preparation; no real DB/library probe follows. |
| 2 Real Phase 1 discovery/provenance | NOT AUTHORIZED | Exact DB/report paths, reviewed read-only tooling, native safety/root metadata, current fingerprints/scan and whole-component verification; approve any separately requested current report generation rather than assuming it. |
| 3 Pilot execution | NOT AUTHORIZED | Freeze Scan 4 source/components, clean executable, new DB/reports/logs, budgets, quiescence, observation and stopping procedures; explicitly approve the four-component preflight. |
| 4 Post-pilot reporting/verification | NOT AUTHORIZED | Exact completed session and repeat outputs/receipts, source/binary preservation checks, reviewer tooling and trace review. Reporting writes owned receipts and requires approval. |
| 5 Full Phase 2A | NOT AUTHORIZED | Independent pilot disposition plus a new explicit current-scope/capacity decision and fresh DB/capture/output set. Never automatically follow pilot success. |
| 6 Phase 2B | UNIMPLEMENTED; NOT AUTHORIZED | Separate contract-compliant implementation/schema compatibility review and authorization bound to an accepted sealed workload; no hash template supplied here. |
| 7 Source mutations | PROHIBITED | Outside every gate in this protocol; no approval above grants consolidation, relocation, deduplication, rewriting or deletion. |

Readiness evidence does not prove resource-content equality or package
equivalence. Simulator/score/Backbeat DB access, new scans, recovery, migration,
cleanup, source writes and Git staging/commits/pushes remain outside this task.

## J. Open decisions and final recommendation

Before any pilot execution, the human operator/reviewer must resolve:

- [ ] Approve current source/report discovery access and reviewed no-recovery
  inspector; establish real DB identity/digest/stamps/ownership, latest scan=4,
  all roots/diagnostics and no conflicting writer.
- [ ] Supply/inspect the persisted Scan 4 report set and freeze all ten original
  paths, chart facts, eight exact edges and complete closures; reject stale/remapped scope.
- [ ] Attach applicable independent code-review reference and identify exact
  clean Release executable/revision, SDK/runtime/host and deployed hash manifest.
- [ ] Approve native-safe nonexistent DB plus separate report/log/trace/manifest/
  temp destinations; validate ancestors, aliases, exclusions and sidecars.
- [ ] Agree actual free-space/memory/commit reserve, stop thresholds and operator
  observation window; no duration prediction from synthetic EXTENDED.
- [ ] Arrange library/source DB quiescence; choose approved write observation
  and explicitly record its scope, overhead and preservation-proof limitations.
- [ ] Approve cooperative cancellation and any separate external supervision;
  designate who stops unsafe activity and preserves interruption evidence, with no restart.
- [ ] Appoint independent acceptance reviewer and authorize pilot execution,
  then separately authorize post-pilot re-export/verification at the next gate.

Preparation validation: readable document; repository links checked; component
labels/figures/historical-hash distinction reviewed; command syntax parsed without
execution and options compared with the CLI; Git whitespace and untracked-file
inventory checked. Existing tests were inspected, not run. No build, benchmark,
scan, real DB access, real-library traversal, preflight or report command ran.
No production/contract/plan/historical evidence was modified. This document is
the sole addition; nothing staged, committed, pushed, reset or cleaned.

Remaining execution blockers are explicit, so the **preparation plan** assessment
is `PILOT_OPERATIONAL_PLAN_READY_FOR_REVIEW`. This is not pilot readiness approval,
an observed pilot result or permission to cross a gate.

**STOP after documentation validation. Await independent review and explicit
authorization; do not open audit.db, inspect source folders, create phase2.db,
launch Phase 2A, implement/execute Phase 2B or commit/push.**
