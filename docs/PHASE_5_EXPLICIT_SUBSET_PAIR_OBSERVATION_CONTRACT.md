# Explicit subset pair observation — frozen synthetic contract 1

Baseline: `e7bb15b75253834eda6321e46144abdfc238351e`. Authorization: synthetic
implementation only. This contract implements the approved observation-scope
research, option A/3; that historical research remains unchanged.

## Identity and storage

Profile `explicit-subset-pair-1`, contract `observation-pair-1`, SQLite application
ID `0x424D5350` (BMSP), user/schema version 1. One new `.db` contains exactly one
captured source, one selected pair and at most one attempt at each acquisition
stage. No import, migration, resume, recovery, overwrite or resealing.

The exact compiled schema has metadata and append-only `records`: name (SOURCE,
SELECTION, PREFLIGHT, CONTENT), state, parent digest, successful digest and bounded
canonical JSON payload. Typed payloads persist the conceptual records: source
database facts/fingerprint/scan/all roots/charts/diagnostics; selected roots and
direct discovery edge; preflight root bindings, recursive path entries/memberships,
chart reads and verification outcomes; content path reads, root outcomes, pair
dimensions, differences, digest groups, conflicts and diagnostics. JSON is storage,
not trusted proof: strict deserialization, canonical order and reconstruction are
mandatory. Primary keys, stage/parent/state/digest checks, JSON validity and
insert-order constraints apply; update/delete triggers protect every record and
metadata. Exact DDL, metadata, header, integrity and foreign keys are verified.

Domains are `BmsLibraryAudit.ObservationPair.Source.v1`,
`BmsLibraryAudit.ObservationPair.Preflight.v1`, and
`BmsLibraryAudit.ObservationPair.Content.v1`. Each uses the existing length-token
canonical encoding: one group named by stage, fields `profile`, `contract`,
`payload`, one row containing the fixed profile/contract and canonical compact
UTF-8 JSON string. Arrays have explicit ordinal ordering; no export timestamp.
SOURCE excludes selection. PREFLIGHT includes SOURCE digest and UnitId; CONTENT
includes accepted PREFLIGHT digest and UnitId, all reads and derived results.
Successful digests are lowercase SHA-256; failures have no successful digest.

UnitId and PairId are the same SHA-256 identity, domain
`BmsLibraryAudit.ObservationPair.Selection.v1`, group `selection`, fields
`profile`, `policy`, `source`, `root_a`, `root_b`; one row, policy
`strict-direct-chart-subset-1`. Root keys use existing Windows normalization,
sorted ordinally. Root IDs are 1/2 in that order; entry IDs increase in root-ID /
ordinal-relative-path order. Reversing user arguments preserves identity and
orientation. Direction is the analyzer's actual canonical subset direction.

## Admission and coverage

Capture uses existing guarded read-only Phase 1 acquisition, rollback header 1/1,
latest exact COMPLETED scan, full fingerprints and the same captured diagnostics.
Exactly two distinct direct-chart folders must reproduce A_SUBSET_OF_B or
B_SUBSET_OF_A through RelationshipAnalyzer. No connectivity closure, exact/overlap
edge, resource-only root, ancestor/descendant or native alias is admitted.
Known historical/current library roots protect all destinations. Coverage-error
diagnostics whose affected directory intersects either recursive view block
selection; unrelated diagnostics remain visible without invalidating the pair.

Direct charts are present source records with that parent. Recursive charts are
all present source charts below each selected root, including nested/BMSON and
duplicates. All recursive names must match an exhaustive fresh inventory, and
every chart gets fresh SHA-256 matching SOURCE. Phase 1 has no native chart IDs;
PREFLIGHT establishes fresh bindings, not retrospective native authentication.
Older source size/mtime may differ when names and fresh bytes match. Native
identities/stamps fixed during PREFLIGHT must remain stable thereafter.

Inventory includes root (`.`), directories/empty directories and every regular
path, regardless of extension/use. Original, normalized, ordinal relative path,
root/entry ID, type, metadata, native ID and chart role remain distinct. Hardlink
names remain separate. No reparse traversal. Non-chart readability is NOT_TESTED
in PREFLIGHT; inventory is rewalked and compared after chart revalidation.

CONTENT requires the exact externally accepted complete PREFLIGHT digest. It
rechecks namespace/stamps before and after reads, reads every regular path freshly
through the existing handle/stamp/byte-count reader, and matches all chart hashes
to PREFLIGHT. At most two unchanged-stamp attempts per path, no cached reads.
SUCCESS requires expected before/after facts, full size, valid digest, bounded
attempts/bytes and ordered historical timestamps. Failure has no digest.

## States, verification and physical results

Terminal states: BLOCKED (admission/precondition), STALE (namespace/identity/stamp/
required chart bytes drift), INCOMPLETE (inventory/read/budget coverage), CANCELLED
(cooperative token), FAILED (execution/integrity), COMPLETE (reproduced coverage).
Capture rejection before creating an owned DB is an explicit refusal; later
attempts persist terminal audit payloads. A crash may leave a missing stage and
owned temporary; neither is complete and neither is automatically resumed.

Root COMPLETE requires full verified inventory/reads; pair COMPLETE requires both
roots complete. Incomplete inventory/read never produces a positive pair result.
Root outcomes and global coverage are independent: unrelated captured diagnostics
can make global coverage INCOMPLETE while this pair remains COMPLETE. A failed
unit does not write to another unit's DB. No incomplete-result salvage or retry.

Verifier opens evidence read-only, checks exact profile/schema/metadata/header,
recalculates SOURCE/selection/PREFLIGHT/CONTENT identities and parents, validates
captured source invariants and discovery, unique paths/IDs/root memberships,
recursive/direct chart coverage, root facts, chart reads, verification outcomes,
content historical read records and root/pair states. It never reopens original
chart/resource payloads. It rederives the exact comparison with
PhaseTwoContentComparison.Derive and compares all counts, flags, differences and
FULL/NON_CHART digest/multiplicity groups. It reconstructs physical subset direction
from ordinal differences and shared pure case/native/path conflict annotations.
Stored flags, JSON and digests alone are insufficient. Digests establish accepted
historical consistency, not producer authentication against full replacement and
resealing by an attacker. Historical read records cannot authenticate original
bytes independently of the accepted evidence anchor.

Reports retain all inventories, chart paths/digests/copies (direct vs recursive),
source/scan/roots, provenance, read outcomes, diagnostics, limits, conflicts and
complete dimensions. Physical chart-set inclusion is independent of tree inclusion.
No semantic association is inferred from resource names. Semantics is
SEMANTIC_EVIDENCE_NOT_EVALUATED; package boundaries, freshness, dependencies,
functional equivalence, codec usability and resource necessity remain UNKNOWN.
DependencyClosureProven, PackageEquivalenceProven, MergeAuthority and RemovalAuthority
are always false. No removal list or recoverable-space claim.

## CLI, limits and publication

New command family `observation-pair` only:

```text
capture --source-db <audit.db> --source-scan <id> --root <a> --root <b> --db <new.db> --output <new/empty>
preflight --db <pair.db> --accept-source <source-sha256> --output <new/empty>
hash --db <pair.db> --accept-manifest <preflight-sha256> --output <new/empty>
verify --db <pair.db> --stage <SOURCE|PREFLIGHT|CONTENT> --accept-manifest <sha256>
report --db <pair.db> --stage <SOURCE|PREFLIGHT|CONTENT> --accept-manifest <sha256> --output <new/empty>
```

Acquisition commands export `observation_pair.json` after sealing/verification.
Verify/report cannot acquire missing source evidence. A failed stage is inspected
as audit data, never accepted by a successful-manifest command. Exit 0 complete,
2 incomplete/stale, 130 cancelled, 1 blocked/failed/refused. Failed export is
separate from sealed acquisition and never changes its digest.

Provisional lab maxima (API may only reduce positive limits): roots 2, pairs 1;
source DB 64 MiB, captured charts 512, distinct direct folders 128, diagnostics
1024; recursive charts 512, entries 8192, regular paths 4096, depth 32; per regular
file 256 MiB, expected total payload 1 GiB, aggregate attempted payload per stage
2 GiB; attempts/path 2; cooperative work time/stage 300 seconds; canonical stage
payload 16 MiB, evidence DB 64 MiB, report 32 MiB, materialized managed-memory
growth 256 MiB (cooperative checkpoints, not an OS memory guarantee). No silent
truncation or sampling. Entry/source caps bound comparison/conflict materialization;
all conflicts must fit or comparison is incomplete. Presentation limits never skip
integrity verification. Input/payload limits are checked before deserialization;
serialization can temporarily allocate up to bounded materialization, not a hard
instantaneous peak guarantee. No performance or real-scale acceptance.

Windows acquisition and existing native path/reparse/alias/sidecar policies are
mandatory. Protect all known roots and source/evidence files; new owned DB only,
single writer, exclusive CreateNew initialization, no historical DB writes. Reuse
ObservedReportPublication with protected-root/input native bindings revalidated
before staging/publication, exclusive owned temporary, flush and no-overwrite.
Cancel/failure cleanup only provably owned temporary; never delete source/foreign
files. No universal TOCTOU, instantaneous snapshot or destructive-safety claim.

Legacy Phase 2 schema/domains/seals/policies, Phase 1 discovery and Phase 3–5
contracts remain unchanged. Old and new DB consumers reject each other's profiles.
No Phase 4/5 adapter, batches, general overlap or next-phase capability.

## Freeze rule

CONTRACT_FREEZE_PASS precedes functional code. Material changes to admission,
seals, identity, versions, authority or safety require STOP and reporting; editorial
corrections alone may follow. Acceptance of this implementation remains pending
independent review.
