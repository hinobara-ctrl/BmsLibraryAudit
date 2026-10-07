# Phase 2 read-only exact candidate filesystem evidence contract

Contract version: `1.0-draft`, dated 2026-10-06.
Phase: `PHASE_2.READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE`.
Status: **PROPOSED DESIGN; IMPLEMENTATION AND REAL-LIBRARY EXECUTION ARE NOT AUTHORIZED**.

MUST/MUST NOT define acceptance requirements for a future implementation.
Nothing in this document describes an implemented Phase 2 command or result.
This task adds this document only; existing code, tests, dependencies, project
files, Phase 1 schema and historical documentation remain unchanged.

## 1. Verified context and design authority

Repository baseline: `b58242014a5e860a0f84adbafd8fc12788e0ffba`.
Phase 1 implementation baseline: `53c45ca322f63da67b1657b34be6826d838cfd92`.
At contract preparation, HEAD and origin/main match the repository baseline,
the branch is main and the initial working tree is clean. The configured remote
still spells the repository `hinobara-ctrl/BMSAudit`; it has not been changed.
Commit identities, rather than a repository display name, bind this design.

Governing documents are [AGENTS.md](../AGENTS.md),
[MASTER_CONTEXT.md](../MASTER_CONTEXT.md), [README.md](../README.md),
[PHASE_1.md](PHASE_1.md),
[PHASE_1_REAL_LIBRARY_VALIDATION.md](PHASE_1_REAL_LIBRARY_VALIDATION.md),
[IMPLEMENTATION_REPORT.md](IMPLEMENTATION_REPORT.md) and [SOURCES.md](SOURCES.md).
The original implementation report is historical; the later validation record
closes Phase 1. Its earlier uncommitted/synthetic-only state is not a current
repository-state claim and needs no retrospective rewrite.

### 1.1 Current verified Phase 1 facts

- `PHASE_1_STATUS: VALIDATED`; only scan/report commands exist today.
- `AuditDatabase` owns schema 1, application ID `0x424D5341`, and the tables
  schema_metadata, scan_sessions, library_roots, chart_files and diagnostics.
- `Snapshot()` selects the latest COMPLETED scan and the mutable current
  chart_files rows. Scan timestamps alone cannot reconstruct an older chart
  snapshot. A scan ID is not a historical immutable manifest.
- `present` and valid hashes are separate. Analysis trusts present/OK rows,
  accounts for diagnostics and excludes known incomplete folders.
- `RelationshipAnalyzer` groups direct-parent folders, uses distinct SHA-256
  sets, derives exact edges and emits EXACT_ONLY components with 2+ members.
  The component labels are sorted ordinal navigation IDs, not durable IDs.
- `ChartHasher` streams MD5/SHA-256 together, checks size/mtime/bytes read and
  retries once. The scanner can reuse hashes based on metadata.
- `PathPolicy` preserves spelling and Unicode, uses invariant uppercase
  comparison paths on Windows, ordinal paths elsewhere, rejects device path
  prefixes and reparse ancestors, and rejects overlapping configured library
  roots. That last rule does not prohibit nested *chart-folder* observations.
- `ReportWriter` uses UTF-8 without BOM, LF, invariant values, CSV escaping,
  new/empty output directories and CreateNew files. A report set is not atomic.

These facts were inspected in AuditDatabase.cs, Models.cs, ChartScanner.cs,
ChartHasher.cs, RelationshipAnalyzer.cs, PathPolicy.cs, ReportWriter.cs,
the CLI Program.cs and PhaseOneTests.cs at the authorized baseline.

### 1.2 Recorded planning evidence, not algorithm constants

The existing validation document records the following; the real library,
real audit database and simulator databases were not accessed for this design.

| Recorded metric | Value |
| --- | ---: |
| Verified charts | 226607 |
| Unique chart SHA-256 | 138049 |
| Analyzed chart folders | 19925 |
| Candidate pairs | 13149 |
| EXACT_CHART_SET / A_SUBSET_OF_B / B_SUBSET_OF_A | 529 / 2645 / 2369 |
| HIGH_OVERLAP / MEDIUM_OVERLAP / SHARED_CHARTS | 1010 / 3265 / 3331 |
| EXACT_ONLY components | 504 |
| Component sizes 2 / 3 / 4 | 493 / 10 / 1 |
| Members of those components | 1020 |

These numbers describe one Phase 1 observation. Phase 2 MUST derive population
and volume dynamically; none is an admission threshold or expected run result.
The known large-container pathology, such as the musicbox example documented
in Phase 1, supports excluding subset relationships, not guessing package roots.

## 2. Evidence boundary and vocabulary

The governing rule remains: **Heuristics discover. Exact evidence proves.
Verified plans mutate.** This phase stops at physical evidence.

```text
RELATED != EQUIVALENT != SAFE_TO_REMOVE
CHART_BYTE_EXACT != PACKAGE_EXACT
EXACT_CHART_SET != SAFE_TO_MERGE
SUBSET != SAME_PACKAGE
same filename != same content
same content != automatically removable
```

An **OBSERVATION_ROOT** is the direct parent of a Phase 1 candidate chart,
using the captured absolute path. It is not a proven package root. The scope
is that directory and its recursive descendants, including ordinary unknown
files and directories. Phase 2 MUST NOT infer ownership by inspecting parent
or sibling contents or automatically expand upward.

For example, PackageA/song.bms and PackageB/sound/song.bms admit different
observation boundaries. The latter observation says nothing about files in
PackageB outside sound. Identical observed subtrees still say nothing about
logical references beyond them. Ancestor metadata checks required for path
safety are permitted; they do not permit ancestor/sibling content enumeration.

**NON_CHART_FILES** means regular files whose extension is not .bms/.bme/.bml/
.pms/.bmson, compared case-insensitively. It is a filename-based projection,
not REQUIRED_RESOURCES, referenced files or unused files. Charts and non-chart
files remain opaque bytes. The meaning of an extension never exempts a file
from Phase 2B hashing.

## 3. Operational gates and scope selection

The proposed future CLI is deliberately two-step:

```text
bms-audit phase2 preflight --source-db <audit.db> --source-scan <latest-id>
    --db <phase2.db> --output <new-or-empty-directory>
    [--component <EXACT_ONLY-id> ...]

# Human reviews the persisted scope, workload and anomalies, then invokes:
bms-audit phase2 hash --db <phase2.db> --preflight <preflight-session-id>
    --accept-manifest <preflight-sha256> --output <new-or-empty-directory>

bms-audit phase2 report --db <phase2.db> --session <session-id>
    --output <new-or-empty-directory>
```

These are proposed commands only. Phase 1 scan/report semantics stay intact.
`--source-scan` asserts the latest completed source scan; it MUST NOT select
historical chart rows that Phase 1 cannot supply. A mismatch is a fatal error.

Preflight defaults to all dynamically derived EXACT_ONLY components. Repeated
--component options select complete source components, scoped to that source
scan/fingerprint. Unknown, non-exact or incomplete selections are rejected.
There is no arbitrary --root admission, individual-edge selection, partial
component selection, automatic container pruning or heuristic admission.

Only EXACT_CHART_SET edges contribute to EXACT_ONLY components. A_SUBSET_OF_B,
B_SUBSET_OF_A, HIGH_OVERLAP, MEDIUM_OVERLAP and SHARED_CHARTS are inadmissible
in v1. Members are not separately inventoried for every edge. Within each
admitted component, comparisons cover every unordered member pair, including
components with more than two members. No cross-component pairs are generated.

Phase 2A MUST stop after sealing its evidence and producing preflight reports.
It MUST NOT start, schedule or resume Phase 2B. The second invocation and exact
accepted manifest digest record the human gate. This authorizes only the
stated read workload. A different workload requires a new preflight; v1 does
not silently enlarge or shrink the admitted selection at the hash command.

Exit codes: 0 coherent completed operation; 1 fatal configuration/ownership/
database/report failure; 2 coherent preflight with rejected components or a
terminal FAILED/STALE content attempt; 130 cancellation. Warnings alone do not
imply failure. A completed preflight with zero admitted components is reportable
with exit 2; hash refuses it. Report failure never silently discards evidence
already committed in phase2.db.

## 4. Immutable Phase 1 provenance capture

Phase 1 audit.db and its -journal/-wal/-shm files MUST remain read-only inputs.
Phase 2 MUST NOT create, update, recover, checkpoint, truncate or delete any
Phase 1 database/sidecar, migrate its schema or change its journal mode.

Before creating/writing phase2.db:

1. Validate source/destination safety under section 13. Reject source reparse
   ancestors, missing/nonregular source DB, and any existing source sidecar,
   including a directory or dangling link at a sidecar path. Do not recover it.
2. Acquire a read-only guard handle before opening SQLite and hold it over the
   whole capture. On Windows it MUST
   deny write/delete sharing; if a writer already prevents this, fail SOURCE_BUSY.
   Obtain size/mtime and, where supported, handle-based identity. Do not use
   SQLite immutable mode as a substitute for demonstrating stable input.
3. Open audit.db read-only with pooling disabled. Verify its path remains bound
   to the guarded identity, then verify Phase 1 application ID, schema 1 and
   stored platform comparison mode. Connection-local query_only is permitted;
   no persistent PRAGMA writes or ATTACH are allowed. Check the guarded path
   binding again after extraction/fingerprinting; a replacement is SOURCE_CHANGED.
4. Stream SHA-256 of the source DB with a bounded buffer and stat/length/bytes
   checks. Provenance instability is fatal SOURCE_CHANGED, not an accepted
   retry against a silently different snapshot.
5. In one SQLite read transaction, read the latest completed scan, its roots,
   all library_roots including inactive historical roots, all current chart
   rows and that scan's diagnostics. Assert --source-scan and root/row consistency.
   Use the existing Phase 1 analysis rules to derive the exact components.
6. Recheck destinations against the full current/historical root inventory from
   step 5 before reserving or opening a writable Phase 2 DB. Capture the source
   evidence into a Phase 2 transaction. Retain *all current
   chart-index rows and current diagnostics*, not resource contents: this modest
   chart-only snapshot allows the admission analysis and whole-component closure
   to be reproduced later without the original database. Store selected complete
   component membership, exact edges, counts and distinct chart identities.
7. Fingerprint the guarded source DB again and verify the same digest, stamp,
   length and available identity, plus continued absence of sidecars. Only then
   accept/seal the captured source snapshot. Failure rolls back acceptance.

The two source-DB fingerprint passes bind logical extraction to one observed
database state. They do not authorize resource reads. Platforms unable to
provide the required exclusion must require a quiescent source and fail closed
when stability cannot be established. No temporary copy of audit.db is needed.

Record repository baseline, Phase 1 implementation baseline, runtime build SHA,
tool/contract version, original/normalized DB path, DB SHA-256 and byte length,
source schema/application ID/comparison mode, scan ID and UTC start/end, full
configured roots and historical roots, admission policy EXACT_ONLY, selected
components, original navigation labels and captured admission facts.

Use source navigation IDs only as scoped labels. A component identity binds
sorted normalized member paths and the captured distinct chart set. Phase 2B
MUST use the sealed Phase 2A data; it MUST NOT reopen or reinterpret a later
audit.db for admission. Later Phase 1 changes neither rewrite nor silently
invalidate the captured historical evidence. Current filesystem checks in 2B
remain mandatory.

## 5. Phase 2A: fresh direct-chart revalidation

Before recursively inventorying a selected component, revalidate every member:

1. Verify the captured root still exists as an ordinary directory under its
   captured library root, with no unsafe reparse ancestors.
2. Enumerate direct children only, including direct charts with recognized
   mixed-case extensions. Chart-named directories are not chart files.
3. Require complete enumeration. For each ordinary direct chart, fresh-read
   SHA-256 with a bounded sequential read-only stream, verify bytes read, stat
   before/after and retry once on changed stamp. Never use Phase 1 cache hashes
   as the fresh result. A second unstable attempt yields no trusted digest.
4. Compare *path membership and each path's digest* to the captured chart rows.
   The normalized path set, original direct filename spelling and individual
   SHA-256 values MUST correspond; duplicate chart copies remain distinct rows.
   Recompute distinct sets across the component as a consistency check.
5. Record source/fresh stamps, individual results and diagnostics. Mtime-only
   drift with identical paths/bytes is CHART_METADATA_DRIFT, not changed chart
   content; it is a warning and the fresh stamp becomes the preflight stamp.

Changed bytes, new/removed direct chart, filename spelling change (including
case-only rename), movement, unreadability, instability, reparse chart or root
failure produce STALE_PHASE_1_CANDIDATE. A rename need not be guessed: old-path
absence and unexpected new-path presence suffice, even if the distinct hash set
stays equal. Report both paths without a heuristic one-to-one mapping.

**If one member fails, the entire source EXACT_ONLY component is rejected.**
Do not salvage its healthy pairs or relabel membership inside this session.
Record every member's result and require a new Phase 1 scan followed by a new
Phase 2A before admitting that component. Other independent selected components
may continue through preflight. Rejected components do not receive recursive
non-chart inventory or content hashing.

One normalized direct chart path is read once for this revalidation pass;
membership rows can share that result. Verification passes for staleness are
separate from this optimization; it never permits trusting an old session.

## 6. Phase 2A: metadata inventory and workload

For chart-current components, enumerate every ordinary descendant and directory
under each observation root, without reading non-chart content. Include the
root directory as relative `.` and explicit child directory rows, including
empty directories. Include hidden/system ordinary files and unknown extensions.
Reparse files/directories and nonregular/special entries are recorded but not
followed; any such entry makes every containing observation view incomplete.

For every entry capture original and normalized absolute path; entry kind;
size for regular files (NULL otherwise); mtime UTC ticks; the filesystem
attribute bitmask; extension and recognized-chart flag for regular files;
inventory status; and its observation-root memberships and relative paths.
If metadata fails, preserve the encountered path with NULL unknown fields and
a diagnostic. NULL is never zero size or proof of absence.

Phase 2A MUST NOT content-hash non-chart files or nested charts unless they are
direct members requiring the section 5 revalidation. Metadata readability does
not prove non-chart content readability; label it NOT_TESTED. An unreadable
directory/metadata/chart is reported in 2A; a content-only access denial may
first become known in 2B. Opening read-only handles for identity/safety without
reading payload bytes is permitted and must be instrumentable in tests.

Preflight finishes with a second metadata enumeration/stat verification of its
inventoried union and direct-chart membership. Added/removed/renamed nodes,
stamp/kind/attribute changes, new reparse entries or incomplete verification
invalidate the affected component's inventory. Do not silently replace the
first manifest with a different last view. Record INVENTORY_CHANGED or
OBSERVED_SUBTREE_INCOMPLETE; require a new 2A. If direct-chart membership changed,
also apply STALE_PHASE_1_CANDIDATE and require a new Phase 1 scan.

Component outcomes are ADMITTED, STALE_PHASE_1_CANDIDATE or
OBSERVED_SUBTREE_INCOMPLETE. ADMITTED requires every member chart-current and
every observation view completely inventoried/verified. These outcomes describe
preflight metadata, not completed content equality. No partial component is
admitted. A complete preflight may contain rejected components and diagnostics.

Preflight summary MUST provide selected/source/accepted/rejected component
counts, observation-root counts, per-root and union regular-file/directory
counts and logical bytes, chart revalidation bytes, extension distribution,
largest roots by bytes and file count, nested root pairs, shared-path views,
reparse/unreadable/metadata findings, stale candidates and completeness status.
Report largest 20 roots for each ranking (or all if fewer), breaking ties by
normalized path; the full roots CSV includes every root. This ranking is not
automatic container classification.

Estimated 2B volume is the sum of lengths of unique normalized ordinary-file
paths in ADMITTED components, including charts. This excludes retry bytes and
metadata traversal overhead; show retry allowance as up to one additional read
per unstable file rather than promising duration. Do not infer HDD throughput.
Logical per-root totals can overlap. Report the union as
`unique_normalized_path_bytes`, not actual allocated disk space or savings.
Hardlinks at different paths count separately in v1; path deduplication does not
prove inode uniqueness. An incomplete root's totals are explicitly lower bounds.

## 7. Path identity, nested roots and membership

PHYSICAL_FILE_IDENTITY in v1 means a session-scoped observed regular-file path,
keyed by its normalized absolute path. Optional native volume/file IDs can
supplement stability evidence but MUST NOT silently collapse different paths.
CONTENT_IDENTITY is SHA-256 of a successful fresh read. These are distinct.

OBSERVATION_ROOT_MEMBERSHIP maps an inventoried node to one root-specific
relative path. For A/B/file.wav when A and A/B are observation roots:

```text
one observed file path / one 2B hash
  membership A:   B/file.wav
  membership A/B: file.wav
```

The nested root directory itself is `B` in A's tree and `.` in its own tree.
One node can belong to multiple roots, including roots in different selected
components. This does not admit cross-component comparisons. Every containing
view inherits that node's failures; deduplication cannot hide them.

Traverse the union of topmost selected observation roots; do not traverse a
common ancestor outside that union. Store each normalized node once, then
project memberships into every containing view. Nested-root inventory is not
repeated per pair. Verification passes may re-enumerate; the guarantee is one
logical inventory and one fresh successful hash per physical path per 2B run.

Absolute identity follows the captured platform mode: invariant uppercase full
paths on Windows, ordinal elsewhere. Preserve original Unicode; do not apply
Unicode NFC/NFD normalization. Duplicate normalized keys with contradictory
spellings/kinds are ambiguous and make evidence incomplete; case-sensitive
Windows directories remain unsupported.

Relative paths use `/` as separator, preserve each name's actual Unicode and
case, have no leading separator or unresolved `.`/`..`, and are compared
**ordinal, case-sensitive**, including on Windows. This deliberately strict
tree identity can differ for Foo.wav versus foo.wav. They are path variants
with possible shared content, not automatically paired as the same name.
Record this mode as `relative-ordinal-spelling-v1`. All reported containment and
relative paths MUST be validated against the owning observation root.

## 8. Phase 2B preflight validation and fresh hashing

The hash command MUST require a COMPLETED/PREFLIGHT session, its exact accepted
manifest digest, at least one ADMITTED component and a matching contract/schema/
platform mode. Validate the seal before source content reads. All and only the
ADMITTED population in that manifest is the authorized scope.

### 8.1 Before reading content

Re-enumerate the complete admitted union and compare with the immutable 2A
metadata manifest: normalized identity, original root-relative spelling, node
set, kind, regular-file size, mtime and attribute bitmask. Verify directory
presence including empties; test enumeration success and reparse safety anew.
An added/removed/renamed file or directory, changed size/mtime/kind/attributes,
new link, unavailable root or newly unreadable subtree is STALE_PREFLIGHT.
Abort this 2B attempt; do not patch its manifest or continue on unaffected pairs.
A new Phase 2A is required. A direct-chart discrepancy also requires a new
Phase 1 scan under section 5. Source audit.db is not reinterpreted.

This validation is metadata-only. Equality of metadata cannot establish that
unhashed non-chart bytes have stayed unchanged between gates. A same-size,
mtime-preserving replacement is detected by a *fresh 2B read*, but cannot be
called a change from 2A's unknown non-chart bytes. Conversely, direct charts
have fresh 2A digests, so their digest change is detectable even with preserved
metadata. Tests MUST distinguish these two cases; do not invent 2A byte proof.

### 8.2 Content reads

Hash **all regular files** in the admitted union: charts, audio, images, videos,
text, ZIP/RAR, unknown extensions and extensionless files. Do not parse, decode,
decompress, extract, execute or resolve chart declarations. Compute SHA-256
only; resource MD5 has no concrete requirement in this contract.

Use sequential deterministic path order, a read-only FileStream, a bounded
reusable 64 KiB buffer and IncrementalHash SHA-256. Prefer FileShare.Read on
Windows, denying concurrent writes/deletes while open. Check kind/reparse,
preflight stamp and available identity immediately before open; obtain/check
handle metadata where supported; verify bytes read equals both lengths; stat
after; retain observed start/end and attempt count.

On an unstable read, retry once only if a fresh safety/stamp check still matches
the accepted manifest. Otherwise mark STALE_PREFLIGHT immediately. A second
unstable attempt yields FILE_UNSTABLE and no trusted digest. Unreadability is
FILE_READ_ERROR. Any failed required file makes the *entire* v1 content session
FAILED or STALE; partial results cannot support completed comparisons. A
failure propagates to every root membership of that file. This conservative
all-session rule avoids publishing a successful subset under an accepted larger
workload. Diagnostics may still be reported.

Fresh 2B direct-chart digests MUST equal the accepted 2A direct-chart digests.
If not, set STALE_PREFLIGHT and STALE_PHASE_1_CANDIDATE even if stamps match.
The initial 2B evidence has no cross-session cache and no resume. Neither old
Phase 1 hashes nor 2A chart hashes substitute for a 2B read. Within this run,
one successful hash row is shared by overlapping observation memberships;
identical digests at different paths still require independent fresh reads.

After all reads, repeat the complete metadata manifest verification. Any
membership, stamp, kind, attributes, directory or safety difference, or failure
to complete that pass, prevents completion and produces STALE_PREFLIGHT.
Only after this pass may comparisons and the session completion be committed.

### 8.3 Temporal limits of exact evidence

Recorded digests describe fresh stable reads during the completed session's
observation window. They are not a simultaneous immutable filesystem snapshot
or a guarantee of the live tree after completion. Metadata-preserving edits
after a file was read can escape final metadata checks. This limitation MUST
appear in reports; v1 does not claim NTFS snapshot/journal protection or perform
an unbounded cycle of rehashes. Future destructive authority would require
fresh independent revalidation, even for a COMPLETED Phase 2 session.

## 9. Exact comparison dimensions

Comparisons are computed only from one coherent COMPLETED content session and
from its admitted component member views. Endpoints A/B are ordered by
normalized absolute root path. Compare all unordered member pairs exactly once.
No source file is reread for a pair; join memberships to stored file hashes.

For a complete root R define:

- `D_R`: set of all relative directory paths, including `.` and empty directories.
- `F_R`: map from relative regular-file path to SHA-256, with one row per file.
- `M_R[h]`: number of regular-file memberships whose digest is h.
- `N_R`: F_R restricted to NON_CHART_FILES; `MN_R` is its content multiset.

Directories remain in the non-chart tree projection even if removing chart
files leaves them empty. Directory timestamps, file mtimes, attributes, access
permissions and absolute root names are not equality dimensions; they serve
stability/safety provenance. File sizes must be coherent with successful hash
reads; conflicting sizes for the same SHA-256 are an evidence integrity failure,
not accepted equality.

Store counts AND drill-down rows for these raw dimensions:

| Dimension | Exact rule |
| --- | --- |
| Same relative path, same bytes | p in F_A and F_B; SHA_A[p] = SHA_B[p] |
| Same relative path, different bytes | p in both; SHA_A[p] != SHA_B[p] |
| Same content, differing relative paths | Group every h by its A/B path memberships; list differing path sets, without invented matching |
| Files only in A / only in B | Relative regular-file keys present in one map only |
| Directories only in A / only in B | Relative directory keys present in one D only, including empty directories |
| Entry-kind conflict | Same relative path denotes a directory in one root and a file in the other |
| Full tree equality | D_A = D_B, keys(F_A) = keys(F_B), and equal digest at every key |
| Full content-multiset equality | M_A[h] = M_B[h] for every h in the union; filenames/directories ignored |
| Non-chart tree equality | D_A = D_B, keys(N_A) = keys(N_B), and equal digest at every non-chart key |
| Non-chart multiset equality | MN_A[h] = MN_B[h] for every h; directories ignored |

Empty-directory differences prevent both full and non-chart TREE_EXACT, but
do not affect content-multiset equality. The root `.` is present in each
complete view and does not compare the absolute root directory's basename.
Zero non-chart files yield equal empty multisets; differing directories can
still make the non-chart trees unequal. Classifying a file as chart depends
only on its own path extension, including in non-chart content-group counts.

Multiplicity example: A/foo.wav and A/bar.wav both hash X, while B/001.wav hashes
X. Their distinct digest sets are equal, but M_A[X]=2 and M_B[X]=1: the multisets
are unequal. Hardlinks and repeated copies each contribute their path membership
once in each root view; hashing deduplication never collapses multiplicity.

Derived labels are independent booleans/multilabel evidence, never one lossy
primary category:

| Label | Definition |
| --- | --- |
| OBSERVED_TREE_EXACT | Full tree equality as defined above |
| OBSERVED_CONTENT_MULTISET_EXACT | Full content-multiset equality |
| OBSERVED_NON_CHART_TREE_EXACT | Non-chart tree equality |
| OBSERVED_NON_CHART_CONTENT_MULTISET_EXACT | Non-chart multiset equality |
| OBSERVED_PATH_CONFLICT | At least one same-relative-file digest conflict or entry-kind conflict |
| OBSERVED_FILESET_VARIANT | Any full tree difference between complete views, including directory-only or content-only difference |
| OBSERVED_SUBTREE_INCOMPLETE | A view lacks complete safe enumeration or required trusted hashes |

For incomplete/failed/stale evidence, equality and conflict booleans are NULL
(unknown), not false or vacuously true. No completed folder_comparison row is
published; root/component outcomes and diagnostics explain the condition.
Complete views may have several true exact labels. None may be renamed to
PACKAGE_EXACT, SAFE_TO_MERGE, SAFE_TO_DELETE, REDUNDANT_PACKAGE or
REMOVABLE_DUPLICATE. This contract provides no mutation authority.

## 10. Proposed independent Phase 2 schema

The future project-owned database is configurable, conventionally phase2.db,
requires a .db extension, and has proposed application ID `0x424D5332`,
user_version/schema version 1 and tool identity `BmsLibraryAudit.Phase2`.
It MUST reject Phase 1's application ID, unknown ownership, empty pre-existing
files, unsupported versions and platform/contract mismatches before writing.
There is no Phase 1 migration. Use foreign keys and explicit CHECK/uniqueness
constraints; every table below is in phase2.db, not audit.db.

Integers for lengths/counters are signed 64-bit nonnegative values; UTC mtimes
use ticks. Digests are lowercase 64-character SHA-256 hex. Unknown metadata is
NULL. Status fields use the stated finite vocabularies. Original paths and
normalized keys have separate columns. Internal IDs never replace provenance.

| Table | Keys, required data and constraints |
| --- | --- |
| schema_metadata | PK key; value. Tool/application/schema/contract version and absolute/relative path comparison modes |
| sessions | PK session_id; stage PREFLIGHT or CONTENT; parent_preflight_id FK for CONTENT only; state; started/completed UTC; runtime build SHA/tool version; accepted_manifest_sha256 for CONTENT; final evidence digest; counters. CHECK parent and accepted digest present only for CONTENT; terminal states require ended UTC |
| source_snapshots | PK snapshot_id; UNIQUE preflight_session_id FK; both baseline SHAs; source DB original/normalized path, guarded identity if available, byte length/SHA-256/stamps; application/schema/comparison; latest scan ID/timestamps; policy EXACT_ONLY; captured-evidence digest; seal flag |
| source_library_roots | PK (snapshot_id, source_root_id); FK snapshot; original/normalized absolute path; active flag; configured-in-source-scan flag. UNIQUE(snapshot_id, normalized_path); retain inactive roots for safety |
| source_charts | PK (snapshot_id, normalized_path); root FK; original path/direct parent/name/extension/size/mtime; MD5 and SHA-256 copied as source facts; first/last-seen scan, present/status. Contains the complete current chart-index snapshot used by analysis, not source bytes |
| source_diagnostics | PK (snapshot_id, ordinal); source scan ID, severity/code/path/message; deterministic source order; reproduce Phase 1 incomplete-folder filtering |
| candidate_components | PK component_id; preflight FK; source component label; component identity digest; captured distinct-set identity/count; member count; outcome ADMITTED, STALE_PHASE_1_CANDIDATE or OBSERVED_SUBTREE_INCOMPLETE; reason. UNIQUE(preflight_id, source_component_label) |
| observation_roots | PK observation_root_id; preflight FK; captured source library-root FK; original/normalized path; chart state PENDING/CURRENT/STALE/ERROR; inventory state PENDING/COMPLETE/INCOMPLETE; logical counts/bytes and lower-bound flag. UNIQUE(preflight_id, normalized_path) |
| component_memberships | PK(component_id, observation_root_id); FKs; UNIQUE(observation_root_id) within its preflight: one source exact component per selected root |
| source_exact_edges | PK(component_id, root_a_id, root_b_id); ordered endpoint FKs; captured charts_a/charts_b/common and physical counts; CHECK common=charts_a=charts_b>0. Edges must reproduce selected component connectivity |
| chart_revalidations | PK(preflight_id, normalized_chart_path); nullable captured-chart FK (NULL for unexpected paths); original observed filename, fresh stamps/size/SHA-256, bytes read/attempts; read_status SUCCESS/MISSING/UNREADABLE/UNSTABLE/REPARSE, match_status MATCH/CHANGED/MISSING/UNEXPECTED/UNKNOWN. Fresh digest NULL unless read_status=SUCCESS; a successful read of changed/unexpected bytes does not admit a stale component |
| direct_chart_memberships | PK(observation_root_id, normalized_chart_path); chart_revalidation FK; captured/source membership flag, observed membership flag; compare paths individually, retaining unexpected and missing paths |
| preflight_entries | PK entry_id; preflight FK; original/normalized absolute path; kind DIRECTORY/REGULAR_FILE/REPARSE/OTHER/UNKNOWN; size/mtime/attributes/extension/chart flag; status and readability. UNIQUE(preflight_id, normalized_path); failed metadata retains a row |
| physical_files | PK physical_file_id; UNIQUE entry_id FK; only REGULAR_FILE entries; optional native volume/file identity. This is path identity, not SHA-256 identity; no UNIQUE constraint on file digest or native inode across different names |
| observation_memberships | PK(observation_root_id, entry_id); relative path and comparison key; UNIQUE(observation_root_id, relative_path_key); FKs share the same preflight; membership includes directories and root `.` |
| content_hashes | PK(content_session_id, physical_file_id); FKs bind the content session to its parent preflight; SHA-256, bytes read, before/after stamps/identity, attempts and status PENDING/SUCCESS/ERROR/UNSTABLE. SHA-256 NULL unless SUCCESS; successful bytes equal verified size |
| diagnostics | PK diagnostic_id; session FK; optional component/root/entry/file FKs; gate, severity, stable code, original/normalized path, attempt and message. Do not attach later-session diagnostics to a sealed earlier session |
| comparisons | PK(content_session_id, root_a_id, root_b_id); component FK; ordered distinct endpoints in same admitted component; raw counts and all section 9 booleans; coherence/seal. Available only for successfully completed content evidence |
| comparison_entries | PK(comparison FK, relative_path_key); evidence category, nullable A/B entry FKs and digests; carries equal-path/conflict/only-side/kind/directory differences, not guessed filename matching |
| comparison_content_groups | PK(comparison FK, projection FULL/NON_CHART, sha256); counts A/B and differing-path-set flag; membership paths derive by joining shared hashes and observation_memberships, not duplicating file hashes per pair |
| report_sets | PK report_set_id; session FK; evidence-stage/state/identity; original/normalized destination; export status RUNNING/COMPLETED/FAILED; completed UTC. Report attempts do not modify the session's evidence identity |
| report_files | PK(report_set_id, filename); byte length, SHA-256 of emitted report and status; export receipts are Phase 2-owned provenance and are excluded from their own report content |

Indexes MUST cover source snapshot/path/status, preflight node identity,
memberships by root/relative key, successful hashes by session/SHA-256 and
comparisons by session/component. SQL joins MUST explicitly constrain the
session/preflight; a digest from another run never satisfies a missing hash.
Multisets count membership rows, not distinct digests or content_hashes rows.

Logical diagrams of ownership:

```text
source snapshot -> captured chart index -> exact component -> observation root
preflight entry -> physical file -> one content hash per content session
observation root -> membership(relative path) -> preflight entry
content session -> comparison -> exact dimensions/content-group counts
```

### 10.1 Seals and immutable identities

A COMPLETED preflight seals provenance, selection, chart revalidation,
inventory, memberships, outcomes and diagnostics. Update/delete of those facts
MUST be rejected by database write guards/constraints; later attempts append new
sessions. A COMPLETED content session similarly seals hashes and comparisons.
State transitions for running sessions and export receipts are separate from
sealed evidence. No automated pruning or deletion is in v1.

Define `preflight_manifest_sha256` over a canonical encoding of those immutable
preflight facts, including contract/platform mode, accepted scope and source
snapshot digest. It excludes its own digest field, output destinations/export
receipts, row insertion order, progress counters and unrelated later sessions.
The digest MUST include directory entries and all root-specific relative paths.

Canonical encoding v1 uses a domain string
`BmsLibraryAudit.Phase2.Preflight.v1`, followed by source provenance, roots,
captured charts/diagnostics, components/edges/memberships, chart revalidation,
entries/memberships/outcomes and preflight diagnostics in that fixed group order.
Within each group sort by the logical keys below using ordinal normalized
paths/digest strings and ascending integers; internal insertion IDs are replaced
by those logical references. Encode each group name, decimal row count, decimal
field count and ordered field names before rows. Each string token is UTF-8 preceded by its 4-byte
unsigned big-endian byte length; NULL uses length 0xFFFFFFFF. Integers, flags
and UTC values are strings with invariant decimal, `true`/`false`, and UTC O
format respectively. Fields follow the column order in the schema table;
optional fields encode NULL and are never omitted. No Unicode normalization,
ambient culture, floats or delimiter-based path concatenation is allowed.

The preflight seal uses this fixed logical field order (names separated by
commas are distinct fields, not serialized CSV). `component_key` is its
domain-tagged identity; `root_key`/`entry_key` are normalized absolute paths.
Session IDs, if present in reports, are not canonical key substitutes.

| Canonical group, in encoding order | Fields in encoding order; row sort key |
| --- | --- |
| header | contract_version, phase2_schema_version, tool_version, runtime_build_sha, stage, state, started_utc, completed_utc, absolute_comparison_mode, relative_comparison_mode; exactly one PREFLIGHT/COMPLETED row |
| source_provenance | repository_baseline_sha, phase1_implementation_sha, source_db_original_path, source_db_normalized_path, source_db_native_identity, source_db_size, source_db_sha256, source_db_before_mtime_ticks, source_db_after_mtime_ticks, source_application_id, source_schema_version, source_comparison_mode, source_scan_id, source_scan_started_utc, source_scan_completed_utc, admission_policy; one row |
| source_roots | source_root_id, original_path, normalized_path, active, configured; normalized_path |
| source_charts | normalized_path, source_root_id, original_path, parent_folder, filename, extension, size, mtime_ticks, md5, sha256, first_seen_scan, last_seen_scan, present, status; normalized_path |
| source_diagnostics | severity, code, path, message; path, code, message, severity (identical rows retain multiplicity) |
| selected_components | component_key, source_component_label, distinct_chart_count, distinct_chart_set_digest, member_count, outcome, reason; component_key |
| observation_roots | root_key, original_path, source_root_id, chart_state, inventory_state, regular_file_count, directory_count, logical_bytes, counts_are_lower_bounds; root_key |
| exact_edges | component_key, root_a_key, root_b_key, charts_a, charts_b, common, physical_files_a, physical_files_b; component_key, root_a_key, root_b_key |
| component_memberships | component_key, root_key; component_key, root_key |
| chart_revalidations | chart_key, observed_filename, before_size, before_mtime_ticks, after_size, after_mtime_ticks, fresh_sha256, bytes_read, attempts, read_status, match_status; chart_key |
| direct_chart_memberships | root_key, chart_key, captured_member, observed_member; root_key, chart_key |
| entries | entry_key, original_path, kind, size, mtime_ticks, attributes, extension, is_chart, inventory_status, readability, native_identity; entry_key |
| observation_memberships | root_key, entry_key, relative_path, relative_path_key; root_key, relative_path_key |
| preflight_diagnostics | component_key, root_key, entry_key, gate, severity, code, original_path, normalized_path, attempt, message; component_key, root_key, normalized_path, code, attempt, message, severity, gate, entry_key, original_path |

All sort tuples compare NULL before non-NULL; missing native identities encode
NULL. `native_identity` is an opaque consistently formatted platform token, not
a content digest or implicit hardlink-deduplication instruction. Canonical
metadata fields supplement the overview tables; they MUST be persisted or
unambiguously derivable from the sealed facts. Derived rankings/estimates and
navigation-only inventory IDs need not be duplicated in the seal. A successful
chart revalidation row carries both stamps even when they are equal; a failed
row retains encountered stamps and NULL for unknown values.

For component identity, use domain `BmsLibraryAudit.Phase2.Component.v1` and
two single-field groups: member normalized root paths and distinct chart
SHA-256 values, each sorted ordinal. Source identity uses domain
`BmsLibraryAudit.Phase2.Source.v1` and the source_provenance/source_roots/
source_charts/source_diagnostics groups above. Content identity uses domain
`BmsLibraryAudit.Phase2.Content.v1`, the accepted preflight digest, completed
content-session header and successful hashes (physical path key, SHA-256,
verified size, before/after stamps, bytes read, attempts), sorted by physical
path. Comparison results MUST be reproducible from these sealed inputs and
need not duplicate digests into each pair identity. Specify golden encoding/
digest test vectors before implementation review. The hash command re-encodes
the sealed preflight and compares the
provided digest before creating a content RUNNING session. Structural
inconsistency or a seal mismatch is fatal EVIDENCE_INTEGRITY_ERROR.

## 11. Long-run state, interruption and publication

Sessions have state RUNNING, COMPLETED, FAILED, CANCELLED or STALE.
Allowed transitions are RUNNING to exactly one terminal state; a terminal
session is never reopened or overwritten.

- PREFLIGHT COMPLETED means a coherent, reviewed-able preflight was sealed,
  potentially with rejected components. It is never content proof.
- CONTENT COMPLETED requires every admitted file successful, valid final
  manifest, matching direct-chart digests and all required comparisons.
- STALE denotes SOURCE_CHANGED, STALE_PREFLIGHT or inventory drift preventing
  acceptance; include the specific condition, not a vague failure label.
- FAILED covers read/hash/database faults without coherent completion.
- CANCELLED records explicit cancellation; no completed comparisons exist.

Use short transactions for bounded hash-result batches; do not hold a write
transaction across hours of HDD reads. Partial rows may be committed under a
RUNNING session. Readers MUST not treat them as accepted evidence. In one final
transaction validate success coverage, write/seal comparisons and the content
identity, then mark COMPLETED. A crash during that transaction cannot publish
an incomplete completed session. Progress output distinguishes read bytes,
retry bytes, processed files and trusted-completed-session status.

Use one writer per phase2.db. A second writer refuses an active writer; it
does not race session updates. If a crash leaves RUNNING rows after trusted
SQLite recovery, a later explicit writable operation marks them FAILED with
INTERRUPTED diagnostics; reporting alone never performs recovery/state repair.
SQLite sidecars require manual inspection/recovery before access under
section 13.

Report commands read sealed evidence through a read-only Phase 2 connection
and refuse RUNNING sessions. They may write only export receipts through a
separate short-lived owned-Phase-2 writer transaction; they do not change sealed
source/preflight/content facts. If another writer is active, refuse the export
receipt operation rather than allowing conflicting writers. They never open
Phase 1 for writes or repair any session as a side effect of reporting.
Resume and cross-run hash caching are deferred. A new 2B attempt fresh-reads all
admitted files after full preflight verification; stale manifests need new 2A.

## 12. Deterministic reports

Use the following six report names. `session_summary.json` replaces separate
preflight/content summary filenames so one small schema explicitly carries
stage and state; it does not imply both gates have run.

| Output | Required contents and stable ordering |
| --- | --- |
| session_summary.json | Contract/tool/build/schema, phase, session/stage/state, source DB/scan/baselines, preflight ID/digest, scope counts/bytes/anomalies, diagnostics counts, hashing and comparison distributions if trusted; notices and `trusted_content_evidence` |
| observation_roots.csv | Component/root identities and paths; chart/inventory outcomes; admission and lower-bound counts/bytes; nested/shared path metrics; sorted component identity then normalized root path |
| filesystem_inventory.csv | One row per observation membership, including directories/errors; paths/kind/chart/metadata/physical path identity; SHA-256 only for trusted completed 2B; sorted normalized root then ordinal relative path |
| folder_comparisons.csv | One row per complete unordered pair; source/preflight/content/component IDs, all raw counts and nullable/boolean dimensions/labels; sorted component then normalized endpoints |
| content_groups.csv | One row per comparison/projection/SHA/side/relative membership path with multiplicity counts; FULL and NON_CHART projections; deterministic SHA grouping, no matched-name heuristic |
| diagnostics.csv | Stage/session/component/root/entry context, severity/code/path/attempt/message; sorted context, normalized path, code, attempt, message |

PREFLIGHT exports contain summary, roots, inventory and diagnostics only.
folder_comparisons.csv and content_groups.csv MUST be absent, not empty files
that resemble a completed content analysis. Summary says stage=PREFLIGHT,
`trusted_content_evidence=false`, content booleans/counts NULL, and includes
content hashing volume estimate. This gate may report chart revalidation
digests in a distinctly named field, never as completed 2B content evidence.

COMPLETED/CONTENT exports include all six with
`trusted_content_evidence=true`. FAILED/CANCELLED/STALE terminal exports include
the four audit outputs only, with content digests suppressed/NULL and partial
progress explicitly nontrusted. They cannot expose completed equality labels.

Every CSV row includes session/preflight/source-snapshot identity; summary
includes the full provenance and relevant manifest/content identity. File names
do not replace that identity. Emit UTF-8 without BOM, LF, invariant numbers,
UTC timestamps and proper quote/comma/CR/LF escaping. Preserve Unicode spelling.
Sort all JSON collections, component members and maps deterministically;
absent equality evidence uses NULL, not zero counts.

Repeated reporting of the same sealed session into two new directories MUST
produce byte-identical files. Exclude export-time timestamps, output directory
names, report-set IDs and receipt hashes from emitted evidence; otherwise
receipts would change its bytes. Different sessions intentionally differ in
identity/timing/stamps. Diagnostics retain real context and their sorted order.

Report output is outside sources, new or empty, with CreateNew for every file.
No overwrite, cleanup, replacement or repair of existing outputs. A failed
export can leave partial reports; its report_set is FAILED and the user selects
a new directory for retry. Report generation never triggers enumeration or
hashing and never modifies Phase 1. Partial output is not a completed report
receipt; verify against the Phase 2 session seal before accepting evidence.

## 13. Filesystem and database safety invariants

Future production writes are limited to its owned phase2.db/SQLite transaction
sidecars and explicitly requested Phase 2 reports/receipts. No source chart,
audio, image, video, archive, text, source directory, Phase 1 data, simulator or
score database is writable. Source bytes and last-write mtimes MUST stay equal
under validation; OS access-time behavior is not a tool write authorization.

Before any destination creation/opening:

1. Check against *all captured known source library roots*, including inactive
   historical Phase 1 roots, and all observation roots. Reject destinations
   inside them; reject an output directory containing a source root or either
   DB. Reject DB/report/source path overlaps, Phase 2 DB == Phase 1 DB, and report
   directories containing phase2.db. Newly learned roots enlarge the forbidden
   set, never shrink it. 2B uses the preflight's captured known-root safety set.
2. Apply existing ordinary-path/reparse ancestor policy, then strengthen it
   for Phase 2 output alias safety. Resolve existing ancestor identity by
   read-only platform metadata/handles, accounting for short-name/SUBST/volume
   aliases; append non-existing suffixes only after validating the resolved
   ancestor. If disjointness cannot be established, fail configuration rather
   than silently relying on lexical path comparison. This is a new Phase 2
   safety responsibility, not a retrospective Phase 1 algorithm change.
3. For existing writable DB files, verify native identity and refuse multiple
   hardlink aliases or identity equality with captured source DB. Where required
   native identity/link-count checks are unavailable, reject that existing-write
   configuration. Ordinary inventory hardlinks remain separate read-only paths.
4. Reject foreign/unsupported Phase 2 DBs via a read-only ownership probe before
   write access. Reserve a new DB with CreateNew, without truncation. Disable
   connection pooling and dispose streams/readers/connections/transactions.
5. Refuse existing -journal/-wal/-shm objects before DB access, including reparse
   or directory collisions. Do not manipulate source sidecars. Owned Phase 2
   sidecar recovery is an explicit external prerequisite, not automatic cleanup.
6. Recheck entry/ancestor/kind/handle safety at visitation and immediately before
   reads. Do not traverse directory links or read file-link targets. Unknown,
   special, denied or unstable states are diagnosed and block exact completion.

Safety checks do not grant permission to enumerate non-admitted libraries or
guess package scope. Complete protection against adversarial path races is not
claimed by path checks alone; section 8.3 limits temporal proof. Unsafe/unresolved
states MUST fail closed. Do not reorganize the source library during observation.

## 14. Deferred work and architectural progression

Phase 1 supplies exact chart identity and relationship discovery. Phase 2
supplies path/membership/content SHA-256 evidence in bounded observation views.
Phase 3 will independently parse BMS/BMSON logical resource declarations;
Phase 4 will analyze conditional reachability. Later phases may classify full
packages and create immutable plans with revalidation, staging, verification,
publication and separately authorized optional cleanup.

Future parsing can join logical resource -> resolved physical path -> observed
physical_file_id -> successful session SHA-256. It MUST retain observation and
time provenance; a historical digest is not perpetually current content proof.

Explicitly out of Phase 2 v1: BMS/BMSON semantic parsing; #WAV/#BMP resolution;
resource dependency graphs; #RANDOM/#SETRANDOM/#IF/#ELSEIF/#ELSE/#ENDIF;
#SWITCH/#SETSWITCH/#CASE/#DEF/#SKIP/#ENDSW and branch logic; required/unused
classification; BGA/image semantics; audio decoding or acoustic fingerprints;
archive extraction/execution; readme interpretation; package-root inference;
container auto-classification; chart rewrites/resource renames; merge planning;
copying/moving/renaming/deletion/hardlinks/quarantine/staging/publishing/cleanup.
Native read-only identity checks for safety are not hardlink creation.
Resume, cross-session caches, parallel hashing and stress optimizations are
deferred until independently measured and reviewed.

## 15. Required future synthetic test matrix

All required v1 tests use disposable synthetic directories/databases and
assert precise stored dimensions/statuses, not only summary labels. No test
needs a real library or simulator. Platform-dependent link/permission/identity
fixtures MUST report execution or justified skips; the Windows validation run
must practically exercise supported reparse and output-alias safety.

| ID | Required fixture | Required assertion |
| --- | --- | --- |
| T01 | Two source exact roots with identical full trees | Both gates; all four equality booleans true, no conflicts |
| T02 | Same distinct chart set, different chart filenames already in Phase 1 | Chart admission succeeds; full tree false, full multiset true if counts agree; non-chart dimensions independently true |
| T03 | Same non-chart bytes at different names | Non-chart tree false, multiset true; deterministic SHA/path groups, no guessed name pairing |
| T04 | Same relative filename, different bytes | Same-path conflict count/rows and OBSERVED_PATH_CONFLICT true |
| T05 | File only A; separate fixture only B | Correct side counts and paths; full tree false |
| T06 | Extra empty directory in A or B | Directory-only rows; full/non-chart trees false; multisets unaffected |
| T07 | Two X files versus one X file | Distinct sets may agree; content multiset false, counts 2 versus 1 |
| T08 | Nested observation roots in one or different components | One node/hash per normalized path; distinct relative memberships; no scope expansion or cross-component comparisons |
| T09 | One physical path in multiple views; duplicate bytes at other names | Shared path hashed once; other paths freshly read; multiplicity retained per view |
| T10 | Successful fresh chart revalidation | Every direct path independently matched; instrumentation proves no Phase 1 cache substitution |
| T11 | Chart bytes changed after Phase 1, including preserved size/mtime | Whole component stale; no payload inventory/hashing for it; new Phase 1 required |
| T12 | Chart added/removed/renamed or moved after Phase 1 | Individual path discrepancy detected even when distinct SHA set stays equal; case-only rename tested |
| T13 | Unreadable/unstable/reparse direct chart | Diagnostic, no trusted digest, whole component rejected; no healthy-pair salvage |
| T14 | Chart mtime-only drift with same bytes | Warning, current chart membership accepted; fresh stamp retained |
| T15 | Counted non-chart and nested-chart readers during 2A | Zero payload byte reads except direct candidate chart revalidation; no decompression/parsing |
| T16 | Preflight invocation alone; wrong/missing accept digest | No automatic 2B call/session; hash command fails before any payload read |
| T17 | File added/removed/renamed between gates | STALE_PREFLIGHT; no patching accepted manifest |
| T18 | File size/mtime/kind/attributes change; disappeared directory/new unreadable subtree | Manifest stale, including directories; require new 2A |
| T19 | Non-chart same-size/mtime-preserving byte edit between gates | Fresh 2B hashes edited bytes; no fabricated comparison with unknown 2A bytes; limitation stated |
| T20 | Direct chart same-metadata byte edit between gates | 2B digest detects stale preflight/candidate, no completion |
| T21 | Manifest edit during hashing, including empty-directory change | Final verification prevents completion |
| T22 | Unreadable non-chart file after readable metadata preflight | FAILED content session, no exact comparisons, all containing views diagnosed |
| T23 | Unstable non-chart file | At most one retry; no digest after second failure; stale stamp cannot become a new accepted baseline |
| T24 | Directory/file reparse point introduced before/during either gate | No traversal/target read; incomplete or stale status; no equal-tree proof |
| T25 | Source DB changes during fingerprint/capture; existing source sidecar | Fail closed; provenance not accepted; no source/sidecar writes or recovery |
| T26 | Source DB updated after accepted 2A | 2B derives admission from captured snapshot; never rereads the later database |
| T27 | Hashes/snapshot before and after both gates | Source DB/library bytes and mtimes unchanged; simulator DB sentinel untouched; forbidden write calls absent |
| T28 | Phase 2 DB equals Phase 1 by spelling/normalized/native alias | Rejected before write; source preserved |
| T29 | DB/report in active/inactive source root or ancestor containing sources | Rejected before creation; alias/reparse/hardlink safety fixtures where supported |
| T30 | Foreign/empty pre-existing/unsupported Phase 2 DB; sidecar collisions | Rejected unchanged; owned new reservation succeeds |
| T31 | Same completed session reported twice | Every emitted file byte-identical; UTF-8/LF/invariant/CSV quoting verified; no source-content reads |
| T32 | Unicode, mixed-case extensions, commas/quotes/newlines where supported | Spelling preserved; explicit ordinal relative-tree rules and escaped fields |
| T33 | Cancel/crash after committed partial hash batches or during final commit | No completed exact proof; interrupted RUNNING later becomes FAILED on explicit writable operation |
| T34 | Wrong session/preflight hash join; tampered seal | Integrity error; another run's digest cannot fill a missing result |
| T35 | Source subset/overlap candidates and manual partial-component selection | Never admitted; component members/edges reproduce exact-only source closure |
| T36 | Multi-member exact component | Every unordered pair once; no repeated per-edge source reads; deterministic IDs/order |
| T37 | Non-chart empty multiset; file-directory conflict; case variants | Defined directory projection and all independent booleans exercised |
| T38 | Completed preflight with rejected component plus admitted component | Preflight-only outputs, correct lower bounds, only whole admitted scope hashable |
| T39 | Failed/stale/cancelled content report or report write failure | No trusted digests/equality outputs; report failure does not unseal evidence |
| T40 | Golden canonical encoding, multiple insertion orders/cultures | Identical manifest identity for identical logical facts; any scoped fact change alters identity |

Future optional stress tests are separate: million-entry inventories, huge
files, worst-case component pair counts, low memory/disk space, HDD throughput,
network roots and alternative platform modes. They may motivate measured
optimizations but cannot weaken required freshness, scope, multiplicity or
failure semantics. V1 hashing is sequential; no seek-heavy parallel default.

## 16. Future real-library validation and implementation acceptance

No real-library Phase 2 run is claimed or authorized by this design. After a
separately authorized implementation and passing synthetic tests:

1. Obtain an independent code audit covering write paths, Phase 1 immutability,
   source capture, hash coverage, complete-component admission and publication.
2. Run **Phase 2A only** against the validated Phase 1 evidence, using a separate
   safe Phase 2 database and report location. Derive counts dynamically.
3. Review stale candidates, accepted components, file/directory counts, unique
   path bytes, largest roots, extension distributions, reparse/errors, nested
   views and all unknown/lower-bound volume. Never assume 1020 roots are small.
4. Stop for a human decision on practical HDD workload and scope. Any scope
   change produces a new complete-component preflight; no hidden bypass.
5. Only explicit human invocation of hash with the accepted preflight digest
   runs 2B. Record actual bytes, retries, start/end and manifest validation.
6. Inspect all independent equality/conflict/multiset distributions and paths;
   retain incomplete/stale results without package or removability conclusions.
7. Export the same sealed session twice and compare output bytes.
8. Verify source DB/sidecars and representative plus instrumented source
   bytes/mtimes were not mutated; compare source metadata baselines and review
   write tracing. Sampling alone must not be called proof of all-library byte
   preservation. Record any external filesystem changes as limits/diagnostics.

Implementation is acceptable only if every required v1 fixture passes (with
explicit platform limitations), Phase 1 behavior/schema/tests remain compatible,
admission is reproducible from captured source facts, the human gate cannot be
bypassed, failed sessions cannot expose exact proof, and reports preserve all
raw dimensions, provenance and temporal limits. Later parser or mutation work
requires a new bounded contract and explicit authorization.

This contract fixes v1 decisions; there are no unresolved design questions
requiring guessing by the implementer. Implementation/execution authorization
and human workload approval remain separate future actions.
