# Phase 7A — disposable synthetic staging, increment 1

Date: 2026-10-15. Baseline: `3d8cce1a65c4f5ef1ded27a1ccee27710f86e8dc`.
Contract fixed before code; implementation/validation and independent acceptance
are separate. No historical Phase 2–6 contract or authority is changed.
Status: `IMPLEMENTED_SYNTHETIC_PENDING_INDEPENDENT_REVIEW`.
Phase 6A's published bounded synthetic scope was independently accepted at this
baseline. Phase 7A remains pending independent review until its own audit.

## Admission and ownership

Only an internal capability created by the parameterless synthetic workspace
factory can invoke staging. Its private constructor, exclusive native creation
of a new random directory under the test application's base directory, pinned
native directory identities, and programmatically created fixed fixture files
establish ownership. It accepts no caller-provided source, evidence or target
path. A marker, folder name or IsSynthetic boolean is not admission evidence.
The factory also captures the three evidence/plan files' native facts; identity,
single-link and stamp checks precede any later payload/provider access, and read
guards then pin those inputs through admission and execution. An evidence hardlink
substitution cannot cause the executor to read an unrelated database.
No public CLI/API executor, reflection isolation or protection against malicious
in-process code is claimed. The test assembly alone is the existing friend.

The factory runs scan→preflight→CONTENT→Phase 6A plan/verify on its own A/B roots.
The builder readmits that exact plan and binds it to the owned sources/evidence.
Only `phase6a-virtual-union-1`, UNION_PHYSICALLY_COMPATIBLE,
COMPLETE_ACCOUNTING, zero conflicts/blocked entries and complete inventories
are eligible. Conflict plans are refused whole. Sources, evidence/reports, new
fixed stage and receipt directory are disjoint within the owned workspace.
Native exclusive directory creation and CreateNew files refuse existing objects;
reparse, aliases, invalid Win32 segments, escaping paths and case collisions fail.

## Fresh reads and copy

Every source regular-file appearance is freshly SHA-256 streamed, including
unchosen shared copies. The plan's kind, path, native ID, size, mtime and attributes
are checked against the read handle and path before/after. Source file handles
deny write/delete sharing and remain held through construction/verification;
native directory guards pin names. Complete source namespaces are compared with
the plan before/after, with all original paths retained. No source writes occur.
Detected changes yield SOURCE_STALE_OR_CHANGED; no global snapshot or universal
TOCTOU exclusion is claimed.

For each admitted regular target choose lowest (root ID, entry ID) deterministically
among its freshly verified appearances. Stream-copy through the guarded source
handle into a new ordinary file, hash/count actual copied bytes, flush and check
native binding. Equal path/type/bytes is copied once; different paths remain
different files even for equal SHA. No links, rewrites, move, overwrite or cleanup.
Create all directories including `.` as the stage root and empty directories.

## Independent verification and receipt

The separate verifier readmits the plan, enumerates the actual stage itself,
requires exact ordinal path/type sets and native containment, rejects unexpected
entries/links, and freshly streams every target to verify size/SHA. It rechecks
all source appearances, namespace and plan stamps. It derives expected selected
sources/counts from the admitted plan, rather than trusting builder receipts.
Receipt edits cannot override mismatched physical evidence or chosen provenance.

Receipt `phase7a-synthetic-stage-1` uses separate SHA-256 domain
`BmsLibraryAudit.Phase7A.SyntheticStageReceipt.v1`, zero byte, canonical compact
snake_case UTF-8 JSON. It records accepted plan digest/provenance, owned workspace
and stage identities, source hashes/facts, per-copy expected/read/target SHA,
all equivalent sources, selected source, actual copied bytes, counts, elapsed
milliseconds, states/events, errors and limits. It is published outside stage
with existing exclusive flushed no-overwrite report publication. Digest integrity
is neither execution authenticity nor physical verification.
Bytes copied count completed Write calls, including partial files. File/directory
counts cover independently verified targets; a failed partial tree can contain
additional unverified entries. Source-read records are admitted only after the
complete fresh-source phase; absence of records in a failed phase is not proof
that no reads occurred. An incomplete copied digest is empty, never trusted.

Events: PLAN_VERIFIED, SOURCE_REVALIDATION_PASSED, STAGING_CONSTRUCTED and
STAGING_PHYSICALLY_VERIFIED. Final failures: STAGING_INCOMPLETE (including
cancellation/limits), STAGING_FAILED or SOURCE_STALE_OR_CHANGED. A failure to
publish a receipt cannot return successful execution. Failed/partial stages remain
isolated for inspection, never resumed or automatically deleted. A best-effort
failure receipt may be written without resuming source reads/copying.

Package equivalence, dependencies, relocation safety, operational publication and
original retirement remain unproved/unauthorized. Physical verification does not
change Phase 6A's flags. No Phase 6B variants or Phase 8/9 capability is included.

## Bounds and tests

Hard maxima reducible only in internal tests: 128 source files, 64 source/target
directories each, 8 MiB source bytes, 8 MiB copied bytes, 64 MiB cumulative read
bytes, 2 MiB receipt, 128 MiB managed-memory growth and 60 seconds of staging work.
64 KiB reused streaming buffer; checks per entry/chunk, cancellation cooperative.
Legacy evidence verification has existing bounded-fixture cost; native blocked
I/O and serialization are not instantaneous hard limits. No sampling/truncation.

Fault hooks cover plan admission, fresh reads, directory creation, selected-source
read, partial copy, close/postcheck, independent verification and receipt creation.
The harness creates only fixed owned payloads and retains generated artifacts.
The dedicated smoke is a filtered integration test plus PowerShell inspection;
there is no `phase7 stage` command. STOP after synthetic validation for review.

Receipt verification requires the live factory context; importing a saved marker
to recover arbitrary workspaces is not implemented. The file contract concerns
ordinary default-stream payloads and paths, not preservation of all NTFS features,
ACLs, alternate streams or hardlink topology. No external player-root discovery
or protection of unknown player configurations is claimed by this fixed harness.

## Synthetic validation result

Locked restore and serial Release build passed with zero warnings/errors.
Main suite: 672/672, retaining all 617 prior cases and adding 55 Phase 7A cases.
Offline suite: 276/276. No failures or omissions. All nine synthetic smokes pass.
Adversarial cases include evidence hardlinks refused before payload/provider
access, unchosen shared-source drift, partial large-file writes/cancellation,
receipt edits with recalculated digest, changed/extra/missing stage entries,
junctions, preexisting destinations and reduced budgets.

The final small smoke built 10 files and 3 directories, copied and independently
verified 196,875 payload bytes, and measured 1,002 ms of staging work (fixture/
evidence preparation and final receipt publication excluded). This local synthetic
measurement is not real-library throughput or recoverable-storage authority.
Logs, TRX, complete diff and report A–N are retained locally under
`.artifacts/phase7a-20261015-8a807c0b`. STOP pending independent review; no Phase
6B, operational publication, cleanup or real-library execution is authorized.
