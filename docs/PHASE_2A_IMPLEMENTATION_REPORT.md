# Phase 2A synthetic implementation report

Date: 2026-10-11. Scope: `PHASE_2A.READ_ONLY_PREFLIGHT`.

Assessment: `PHASE_2A_SYNTHETIC_IMPLEMENTATION_READY_FOR_HUMAN_REVIEW`.
This assessment concerns local implementation and synthetic Windows validation.
Real-library execution and Phase 2B require new explicit authorization.

## Baseline and authorization

Before implementation, HEAD and origin/main were both
`a300b54bf63d1e2077accb30e29c5bd40dacee2c`, branch `main`, with a clean
working tree. Final references remain unchanged. Nothing was staged, committed,
pushed or tagged. No reset, cleanup, migration or real-library run was performed.

The current user task explicitly authorizes Phase 2A implementation and synthetic
tests. This is the bounded exception to the earlier Phase 1 implementation scope.
The complete 991-line design contract and required repository documents were read.
The contract remains unchanged, including its historical authorization status;
the current task authorizes only Phase 2A. Phase 1 documentation, core code,
tests, schema, dependencies, project files and lockfiles remain unchanged.

An additional untracked `docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md` appeared
during work. It is outside this implementation and was preserved without editing.
Its research content was not used as implementation authority.

## Change inventory and architecture

| Path | Change and purpose |
| --- | --- |
| `README.md` | Modified: bounded Phase 2A capability, commands and documentation links. |
| `src/BmsLibraryAudit.Cli/Program.cs` | Modified: Phase 2 routing, argument validation, unsupported hash command and exit codes; existing scan/report routing preserved. |
| `src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs` | Added: protected, consistent Phase 1 capture and provenance validation. |
| `src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs` | Added: independent SQLite ownership, normalized evidence tables, constraints, immutable evidence and schema validation. |
| `src/BmsLibraryAudit.Core/PhaseTwoSafety.cs` | Added: native Windows identity, resolved aliases, case mode and safe destination checks. |
| `src/BmsLibraryAudit.Core/PhaseTwoWriter.cs` | Added: global named writer exclusion without lock files. |
| `src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs` | Added: ordinary-entry metadata, lazy directory traversal and bounded direct-chart reads. |
| `src/BmsLibraryAudit.Core/PhaseTwoModels.cs` | Added: options/results, filesystem boundary and deterministic test hooks. |
| `src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs` | Added: source capture, complete component selection, chart revalidation, inventory, second verification and atomic completion. |
| `src/BmsLibraryAudit.Core/PhaseTwoManifest.cs` | Added: normative source/component/preflight encodings and seal verification. |
| `src/BmsLibraryAudit.Core/PhaseTwoReportWriter.cs` | Added: persisted-evidence exports, deterministic formats and separate receipts. |
| `src/BmsLibraryAudit.Core/PhaseTwoAssemblyInfo.cs` | Added: test access to internal database/encoding boundaries. |
| `tests/BmsLibraryAudit.Tests/PhaseTwoPreflightTests.cs` | Added: 50 synthetic test cases including native Windows safety and canonical vectors. |
| `scripts/Phase2A-Smoke-Test.ps1` | Added: independent synthetic end-to-end CLI validation, retaining its disposable artifacts. |
| `docs/PHASE_2A_IMPLEMENTATION_REPORT.md` | Added: this scope, evidence, limitations and requirement map. |

No files were deleted. No external implementations or new packages were used.
`RelationshipAnalyzer.Analyze` supplies the existing Phase 1 semantics directly.

## CLI and observed synthetic execution

```text
bms-audit phase2 preflight --source-db <audit.db> --source-scan <latest-completed-id> --db <phase2.db> --output <new-or-empty-directory> [--component <EXACT_ONLY-id> ...]
bms-audit phase2 report --db <phase2.db> --session <preflight-id> --output <new-or-empty-directory>
```

Identifiers must be positive; invalid, duplicate or inapplicable options are
refused. Component labels select complete source components. Exit codes are 0
for a completed preflight with admitted components and no rejections, 2 for
component rejections or zero admitted components, 1 for fatal/export failures,
and 130 for cancellation. `phase2 hash` returns an unsupported/not-authorized
error before opening evidence or reading payloads.

The final Phase 2A CLI smoke completed session 1 with one selected/admitted
component, no rejections, four regular files, four directories, 78 estimated
future read bytes and 30 chart revalidation bytes. Its preflight seal is
`31f32dc2f054f46039af557c964955e88cf00fc3c3e82cb3254598d2799c6590`.
Both exports were byte-identical. Source database and synthetic library file
bytes/mtimes were unchanged. Unsafe destination and unsupported hashing checks
passed. Artifacts: `.artifacts/phase2a-smoke-b2228b8e951542edafc22135aafae307`.

## Source protection and provenance

The source guard is a read-only FileStream with FileShare.Read, denying write
and delete sharing during capture. SQLite uses read-only mode without pooling,
not `immutable=1`. Native handle/path identity, size and mtime are checked and
the database is fingerprinted with streaming SHA-256 before and after capture.
Existing journal/WAL/SHM objects, including directory/reparse collisions, fail
closed. A consistent read transaction captures the latest COMPLETED scan,
every chart-index row, current scan diagnostics, and active/configured/historical
roots. Requested scan, application ID `0x424D5341`, schema 1, tool identity,
platform comparison mode, integrity, relationships and required hash formats
are validated. `AuditDatabase.Snapshot()` is not used as historical proof.

The copied source facts receive a separate source evidence digest and become
immutable before filesystem inventory, even while the preflight is RUNNING.
Report generation uses only persisted evidence and never reopens Phase 1
payload content or enumerates the library. Native destination-safety checks
may inspect path metadata.

## Independent database and immutable publication

Phase 2 uses application ID `0x424D5332`, schema version 1, tool identity
`BmsLibraryAudit.Phase2` and capability `PREFLIGHT_ONLY`. Required Phase 2A tables:

```text
schema_metadata, sessions, source_snapshots, source_library_roots,
source_charts, source_diagnostics, candidate_components, observation_roots,
component_memberships, source_exact_edges, chart_revalidations,
direct_chart_memberships, preflight_entries, physical_files,
observation_memberships, diagnostics, report_sets, report_files
```

Explicit primary keys, composite ownership foreign keys, NOT NULL columns,
CHECKs and indexes constrain the graph. Exact edges enforce
`common = charts_a AND common = charts_b AND common > 0` with non-null counts.
Native writable database binding is checked before mutation. A new database
uses CreateNew reservation; an existing file must have valid application/tool/
schema identity, no unsafe sidecars and one link. Exact compiled schema,
index and trigger digests are checked against an in-memory reference schema.
Foreign/empty/tampered databases are refused without rewriting them.

Evidence is written in bounded transactions; inventory batches contain at most
256 entries. Final completeness/integrity validation, completion timestamp,
canonical digest and COMPLETED transition commit atomically. Terminal evidence
and captured source facts are protected against insert/update/delete. Receipts
are separate mutable export records and do not alter the preflight identity.
Cancelled/failed sessions have no accepted final seal. Interrupted RUNNING
sessions are marked FAILED on an explicit later writable run, without resuming
their work. Export failure leaves a completed preflight intact and records a
failed report set.

This is the required Phase 2A schema subset, not a complete Phase 2B schema.
Future content result/comparison/group tables and Phase 2B processing are absent.
Session stage/parent/accepted-manifest fields reserve the contract's direction;
CONTENT session insertion is explicitly refused. A future extension requires
separate implementation and schema review; no current full-Phase-2 compatibility
claim or automatic migration is made.

## Selection, charts and observation union

Candidate populations and labels are derived dynamically from the captured
snapshot using the unchanged RelationshipAnalyzer. Only EXACT_ONLY components
are selectable; other edge types do not admit candidates. Durable component
keys bind normalized members and chart digests separately from navigation labels.
Every member's direct chart-parent folder is an observation root, without upward
or sibling inference.

All direct `.bms/.bme/.bml/.pms/.bmson` files are freshly read with bounded 64 KiB
SHA-256 buffers and at most one retry, checking native identity and before/after
stamps. No chart parsing or cached hash proof occurs. Paths, spelling, membership,
individual digests and physical-file multiplicity are compared. Same-size/same-
mtime byte changes, added/removed/renamed/case-renamed charts and uncertain reads
reject the entire component as STALE_PHASE_1_CANDIDATE. Matching bytes with only
metadata drift produce a warning. Other independent components continue.

The walker enumerates the topmost admitted root union with a stack of lazy
enumerators. It includes each root as `.`, empty directories, ordinary hidden/
system files, unknown/extensionless resources, archives and nested charts.
One normalized physical path has distinct memberships and relative paths for
every containing observation root. Resource payloads are never read. Nested
charts are read only when independently selected direct charts. Readability is
NOT_TESTED, not inferred from successful metadata access.

A second traversal verifies membership, spelling, size, mtime, kind, attributes,
direct-chart membership and reparse safety against the preserved first manifest.
New charts between revalidation and initial inventory are also detected.
Enumeration/metadata errors remain explicit unknown facts and reject all affected
containing components as OBSERVED_SUBTREE_INCOMPLETE. Counts are lower bounds
for incomplete roots. No pair salvage or cross-component comparison is performed.

## Canonical seal and reports

The preflight encoder implements Section 10.1's exact 14-group order, field order
and domain `BmsLibraryAudit.Phase2.Preflight.v1`. Tokens use 4-byte unsigned
big-endian UTF-8 lengths, NULL `FFFFFFFF`, invariant decimals and true/false.
Strings receive no Unicode normalization. A SQLite ORDINAL collation provides
UTF-16 ordinal ordering; database IDs are replaced by logical keys. Diagnostics
preserve multiplicity and the normative null/tie ordering. Queries stream rows
instead of buffering the resource inventory. Report destination and export time
are excluded. Session/provenance timestamps specified by the contract remain
part of identity; distinct captures are not expected to have identical seals.

Independent primitive bytes and a complete empty-population/provenance vector
exercise all 14 groups. The latter expected SHA-256 is
`f4b2787b4700234b8dc7a433ac16b9f7e38fa2d99137c871dc003231544d75c8`.
Tests vary internal IDs, culture, source facts and sealed evidence to verify
equivalence and detect tampering. Real populated synthetic sessions additionally
recompute their seals after internal ID remapping in a test-only database clone.

Exports are exactly `session_summary.json`, `observation_roots.csv`,
`filesystem_inventory.csv`, and `diagnostics.csv`. UTF-8 has no BOM; line endings
are LF; numbers/ordering/CSV escaping are deterministic. Summary provenance,
scope/outcomes, per-root/union bytes, future accepted union workload, chart read
workload, nested/shared paths, extension distribution, largest roots, diagnostics
and seal are explicit. `trusted_content_evidence_scope=NONE`, zero non-chart
payload reads and `phase2b_executed=false` avoid a content-equality claim. Failed/
cancelled exports have provisional facts without trusted chart digests or a final
seal. Each report receipt contains byte count and SHA-256 for its output files.

Runtime build SHA is the assembly informational revision. In this uncommitted
working tree it identifies baseline `a300b54...`, not a new implementation commit.

## Safety guarantees and practical limits

Native Windows disk handles provide volume-GUID final paths, volume/file IDs,
link counts and directory case-sensitivity checks. Reparse ancestors/entries are
refused or diagnosed without following targets. Existing/missing destination
paths resolve through ordinary existing ancestors. Active, historical and earlier
captured source roots are protected. Hardlink aliases, SUBST/junction aliases,
unsafe containing output ancestors and alternate stream names are refused.
Global named mutex exclusion serializes this application's database writers.
File/guard identity is rebound and checked around writable operations and exports.

Validation was on Windows only. Other platforms and native configurations whose
required identity/case-mode evidence cannot be obtained fail closed. Distinct
8.3 short names were unavailable in this workspace: the short-name query returned
the original long path. Native canonical resolution supports the check, but a
distinct 8.3 alias fixture was not executed and is not reported as passing.
UNC/other filesystem support is not asserted from these local NTFS fixtures.
Unreadability, unstable reads, lazy enumeration and export failure are also
exercised with deterministic injection rather than hardware-failure claims.

Metadata inventory is not content equality or a filesystem snapshot. Checks
detect observed changes; they do not promise adversarial atomic tree immutability.
Traversal and hashing are bounded, but source chart capture/relationship analysis
retain the Phase 1 chart population in memory. No real-library scale benchmark
was performed. No simulator database was opened. No unresolved failure occurred
in the required synthetic validation; real-library acceptance remains deferred.

## Validation commands and results

The project-local .NET SDK 10.0.401 was used without installing/changing packages.
Commands ran from the repository with these environment settings:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
& ./.tools/dotnet/dotnet.exe restore --locked-mode
& ./.tools/dotnet/dotnet.exe build -c Release --no-restore
& ./.tools/dotnet/dotnet.exe test -c Release --no-build
./scripts/Smoke-Test.ps1 -DotNet (Join-Path $PWD '.tools/dotnet/dotnet.exe')
./scripts/Phase2A-Smoke-Test.ps1 -DotNet (Join-Path $PWD '.tools/dotnet/dotnet.exe')
git diff --check
```

Local commands required the approved sandbox escalation to execute the bundled
SDK; no external library or new dependency access was needed. Restore exited 0;
final Release build had zero warnings/errors. Tests: 85 passed, zero failed,
zero skipped (35 unchanged Phase 1 cases plus 50 new Phase 2 cases). Native
hardlink, SUBST, volume identity, junction/reparse and writer exclusion fixtures
executed on Windows. Non-Windows annotations do not count as executed coverage.

Phase 1 CLI smoke exited 0 with five charts, two distinct SHA-256 values, two
duplicate groups, two candidate pairs and one related cluster. Its second scan
reused five hashes and hashed zero files; warnings/errors were zero. Artifacts:
`.artifacts/smoke-33e83102e0e14395a50732bc15853d13`.
Phase 2A CLI smoke also exited 0, with the observed evidence above.
Tracked and new implementation files were checked for whitespace independently;
Git diff alone omits untracked additions.

## Phase 2A requirement-to-evidence map

All tests below are in `PhaseTwoPreflightTests.cs` unless identified as CLI smoke.
Only the Phase 2A portions of the contract matrix apply; content/comparison tests
belong to the unauthorized Phase 2B implementation.

| Requirement / contract section | Implementation | Synthetic evidence |
| --- | --- | --- |
| Task CLI; contract 3 operational gate | Program, Runner | Phase2A smoke: preflight/report/hash refusal/unsafe output; invalid selection tests. |
| 4 immutable current source; T25/T27 | SourceCapture, Safety | SourceGuardPreventsConcurrentMutationAndCaptureFingerprintsMatch; SourceSidecarsAndDirectoryCollisionsAreRefusedUnchanged; InvalidSourceProvenanceFailsBeforeDestinationWrites; SourceWithoutCompletedScanIsRefused; SourceBytesAndMtimesIncludingDatabaseRemainUnchanged. |
| 4 complete roots/charts/provenance | SourceCapture, Database, Manifest | MissingSourceDigestAndHistoricalRootsStayExcludedAndProtected; CapturedSourceFactsAreImmutableEvenWhilePreflightIsRunning; PreviouslyCapturedRootsStayProtectedAcrossDifferentSourceDatabases. |
| 3 complete EXACT_ONLY selection; T35/T38 | unchanged RelationshipAnalyzer, Runner | ExactOnlySelectionPreservesPhysicalCopiesAndRejectsRelatedLabels; ChangedChartWithPreservedSizeAndMtimeRejectsWholeComponentButIndependentComponentContinues. |
| 5 fresh charts; T10/T11/T12/T13/T14 | FileSystem, Runner | FreshChartRevalidationDoesNotReadResourceOrUnselectedNestedCharts; DirectChartMembershipAndSpellingChangesCannotBeSalvaged (4 cases); ContentOnlyReadDenialRejectsChartAndNeverInventsDigest; TwoUnstableChartAttemptsRejectComponent; MatchingBytesWithMetadataDriftWarnAndUseFreshStamp; ReparseDirectoryAndChartTargetsAreNeverFollowed. |
| 6 metadata-only scope; T15/T16 | FileSystem, Runner, CLI | FreshChartRevalidationDoesNotReadResourceOrUnselectedNestedCharts; Phase2A smoke verifies zero resource reads/no Phase2B reports and hash refusal. |
| 6 unknown/incomplete entries and counts | Runner, Reports | ResourceMetadataFailureAndLazyEnumerationFailureRemainComponentLocal; SecondInventoryDetectsChangesAndPreservesFirstManifest (5 cases); ChartAddedAfterRevalidationButBeforeFirstInventoryIsStillStale. |
| 7 nested union membership; T08 | Runner, Database | NestedRootsShareOneEntryWithIndependentRelativeMemberships. |
| 10 ownership/schema constraints; T28/T30 | Database, Safety | ExactEdgeConstraintRequiresNonNullPositiveEqualCountsAndContentSessionsAreDisabled; InvalidPhaseTwoOwnershipAndSidecarsAreNotRewritten (4 cases); SealGuardsRejectUpdatesDeletesAndCrossSessionJoins. |
| 10.1 normative encoding; T40 preflight | Manifest | CanonicalTokenGoldenVectorHasBigEndianLengthsNullAndUnicode; FullContractPreflightGoldenVectorBindsEmptyPopulationProvenanceAndAllFourteenGroups; CanonicalPreflightIsIndependentOfCultureAndInsertionIdsAndDetectsTampering. |
| 11 atomic completion/cancellation/failure | Runner, Database, Reports | CancellationCannotSealOrPublishPartialEvidence; ReportFailureHasReceiptAndDoesNotChangeCompletedSeal; SealGuardsRejectUpdatesDeletesAndCrossSessionJoins. |
| 12 deterministic persisted reporting; T31/T32 | Reports | RepeatedPersistedReportsAreByteIdenticalWithUnicodeAndCsvEscapingAndNoLibraryReads; Phase2A CLI smoke. |
| 13 destinations/aliases; T28/T29 | Safety, Writer, Database, Reports | UnsafeDestinationsFailBeforeAnyChartRead; NativeHardlinkAndSubstAliasesCannotBypassDestinationSafety; ReparseDestinationAliasIsRejectedBeforeWritesAndNativeVolumeIdentityIsCanonical; GlobalWriterExclusionRefusesConcurrentPreflightAndExistingOwnedHardlinkAliases; AlternateStreamDestinationCannotModifyUnrelatedSentinel. |
| 13 history/reparse/read-only | Safety, FileSystem, SourceCapture | Historical-root, reparse, source-guard and bytes/mtimes tests above. |
| 14 deferred work and 16 acceptance gates | CLI, PREFLIGHT_ONLY schema, this report | Unsupported hash smoke and CONTENT-session constraint; no real-library or simulator access; Phase 1 regression suite/smoke. |

Phase 2A implementation and its required synthetic checks pass. Distinct 8.3
alias execution and real-library scale validation are explicitly unverified.
Phase 2B schema/processing, resource hashing, package equivalence, consolidation
plans and mutation remain unimplemented and unauthorized. Stop here for review.
