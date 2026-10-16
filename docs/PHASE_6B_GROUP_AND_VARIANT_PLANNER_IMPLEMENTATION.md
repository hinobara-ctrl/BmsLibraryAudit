# Phase 6B — conservative group and intact variant planner

Date: 2026-10-15. Baseline: `40dc261f27e0f25adc084057e3d2a14f316301c9`.
Status: `IMPLEMENTED_SYNTHETIC_PENDING_INDEPENDENT_REVIEW`.
The owner records prior bounded synthetic acceptance of Phase 7A at that baseline.
This increment changes no historical contract, seal, SQLite schema or authority.

## Stage A gate and filesystem preservation scope

Two fixed factory scenarios extend only the private Phase 7A harness. Identical
`normal.bms` and `alternate.bms` in SongA/AnotherFolder retain both chart paths
and individual provenance, with one shared `kick.wav`. A second fixture preserves
different north/south structures, equal auxiliary bytes under different names,
and an empty nested directory. Independent target verification and source byte,
native-fact, directory and sidecar checks apply to both.

Before starting 6B, locked restore/Release build passed (zero warnings/errors),
the new cases passed 2/2, the main suite passed 674/674 and offline 276/276, with
no failures/skips. Phase 6A/7A smokes passed; the partial three-file diff and gate
record are retained in `.artifacts/phase6b-20261015-1f66eb4d`.

The historical Phase 7A contract already excludes universal NTFS preservation and
is unchanged. README clarifies ordinal paths/names, entry types, main-stream
bytes/size/SHA, appearances and empty directories; identities bind observations
but are new at the destination. Hardlink names become independent ordinary files;
topology, ACLs/permissions, ADS, timestamps and other filesystem metadata are not
proved preserved. Future real enablement must preserve required properties or
explicitly refuse unsupported cases. No public/arbitrary-path executor is added.

## Input, reuse and CLI

`phase6 group-plan --db <phase2.db> --content-session <id>
--accept-content-manifest <sha256> --component <key> --root <key-A>
--root <key-B> --root <key-C> [--root <key> ...] --output <new-or-empty>`
writes only `group_union_plan.json`.
`phase6 group-verify` accepts identical evidence/root selectors and
`--plan <group_union_plan.json>` instead of output; it writes only a CLI summary.

Exactly 3–6 distinct sealed root keys are canonicalized ordinally. Require one
ADMITTED COMPLETE coherent sealed comparison-eligible legacy EXACT_ONLY component,
COMPLETED CONTENT with exact accepted digest, verified parent/source/schema/
integrity, memberships, SUCCESS hashes and reproduced comparisons. Nested/aliased
root selections and unresolved native-identity conflicts are refused. Source
payloads are never reopened. Current native root metadata supports binding and
output exclusion; it is not fresh source-byte evidence. Component membership is
chart-set discovery, not a package boundary proof.

The internal PhaseSixEvidenceAdmission adds a separately bounded group entry
point; its existing two-root entry point retains its exact cardinality/behavior.
PhaseFive entry reading, PhaseTwoContentComparison and PhysicalConflictAnnotations
remain the physical kernels. No third comparator, hashing engine, parser, schema
migration or evidence translation is introduced. `phase6 plan/verify`, Phase 5
and observation-pair keep their contracts. The separate subset transport, broader
chart sets and Phase 4 semantic inputs are not admitted in this increment.

## Exhaustive alternatives and namespaces

Roots are in sealed key order; bit i of a subset mask refers to root i. All
nonempty masks 1..(2^N−1), including singletons, are independently checked against
their complete union namespace: same-path type/digest, Windows case collisions,
native conflict annotations, directory root and all path ancestors. No candidate
is accepted from pair labels or graph connectivity alone. All maximal compatible
subsets by inclusion become proposals, in ascending mask order. The planner
checks proper supersets; the verifier reconstructs the complete compatible mask
set and independently tests whether any missing root can extend a group.

Identical relative path/type/bytes maps to one virtual target with every origin;
exclusive entries, distinct names with identical SHA, opaque auxiliaries, chart
appearances and empty directories remain. Root/entry identifiers are qualified,
so hardlink names retain separate inventory appearances and native identity
relations. No original appearance is retired by sharing a virtual target.

For A:kick=X, B:no kick and C:kick=Y, alternatives are A+B and B+C, never all
three. A maximal singleton is retained when it conflicts with every other root;
maximal does not mean maximum cardinality. No age/size/name/ordering preference
selects a main distribution. Multiple proposals require HUMAN_SELECTION_REQUIRED;
even one proposal is UNIQUE_PHYSICAL_PROPOSAL_NOT_SELECTED. Every proposal role
is MAIN_GROUP_CANDIDATE. No human-selection mechanism is implemented here.

## Complete variants and conservation

Each proposal includes a complete copy of the admitted inventory of every excluded
observation root, original root key/path and all physical/native/hash identities.
Excluded roots in sealed key order receive `_variantes/001`, `002`, etc. Labels
are conditional presentation only; changing sources can recalculate them, and
numbers imply no quality. Every variant stays PRESERVE_IN_PLACE and
RELOCATION_NOT_PROVEN. They are complete observed distributions, not proof of
complete real package boundaries or permission to move them.

Each proposal ledger contains exactly one row for every original root/entry:
REPRESENTED_VIRTUALLY with matching target provenance, or
PRESERVE_COMPLETE_VARIANT_IN_PLACE with original relative path and conditional
label. No original resources are filtered by parser recognition or apparent use.
Every chart appearance also retains its original parent, logical digest and
UNKNOWN_ALL_POTENTIAL_DEPENDENCIES_RETAINED obligation. No semantic closure or
reference equivalence is asserted; external/player dependencies remain unknown.

When variants exist, a proposed main namespace containing `_variantes`, any
case-equivalent spelling or descendant records VARIANT_LAYOUT_NAMESPACE_CONFLICT
for each affected existing path. File/directory/ancestor collisions are not
overwritten or silently renamed. That layout remains unavailable; historical
physical compatibility of the main group is an independent fact.

## Contract, digest and independent verification

Envelope: `plan`, `plan_sha256`. Version: `phase6b-group-variant-plan-1`.
Evidence profile: `PREFLIGHT_AND_CONTENT/phase2ab-content-1/EXACT_ONLY`.
The plan includes provenance with historical timestamps/current declared validator
revision, all root inventories, full physical conflict witnesses, every subset,
maximal proposals, complete virtual targets/variants/ledgers, chart obligations,
separate states, counts, limits and limitations. Conflicting digests are accessible
through root-qualified witness references into the complete inventories.

Digest: SHA-256 of UTF-8 `BmsLibraryAudit.Phase6B.GroupVariantPlan.v1`, a zero
byte, then compact UTF-8 snake_case JSON of `plan` in model property order.
Canonical JSON options reuse Phase 6A serialization only, not its hash domain.
Roots/inventories/targets are ordinal by key/path; sources and ledger rows sort
by root ID then entry ID; conflict ordering remains the shared admission ordering;
subsets/proposals sort by mask; variants follow excluded root-key order. Output
is indented UTF-8/LF with final newline. Output locations/current execution times
do not enter the digest. Same evidence/selection/limits/declared revision exports
identical bytes despite root argument order. Integrity is not producer authenticity;
matching UNKNOWN_DIRTY values authenticates no executable. Cross-revision replay
is not established by this first version.

The verifier readmits evidence and rejects protected/aliased alleged plan inputs
before payload reads. It reads a bounded guarded single-link ordinary file,
rejects duplicate/unknown JSON properties, checks the own version/profile/digest,
reconstructs all subset decisions/maximality and accounts for every appearance
independently. It checks whole variants, target path/type/SHA/source sets, ledger
bijection, conditional numbering/layout conflicts, chart obligations, all states,
counts and limits. It never calls the planner. Rehashing manipulated JSON cannot
authorize omitted alternatives, incomplete variants, main selection, relocation
or retirement. Sharing physical kernels is explicit; fixture oracles are separate.

States distinguish inventory, accounting, exhaustive enumeration, candidate
physical compatibility, human selection, semantic/package uncertainty, relocation,
construction, virtual plan verification, physical destination verification,
publication and retirement. MainGroupSelected, DependencyClosureProven,
RelocationSafe, MergeAuthority and RemovalAuthority always remain false.
Physical destination verification is NOT_PHYSICALLY_VERIFIED. No staging call,
source-copy choice, mutation script, destination structure or savings promise.

Exit 0: one valid proposal; 2: valid alternatives requiring human selection;
1: invalid evidence/plan, exceeded budget or output failure; 130: cancellation.
No complete partial/truncated plan is published on a failed budget or cancellation.

## Bounds and report preservation

Hard caps, reducible only through API tests: 6 roots, 63 subsets, 20,000 source
entries and conflict witnesses, 100,000 virtual entries and 200,000 accounted
appearances across alternatives, 1,280,000 examined appearances per reconstruction,
16 MiB plan input/output, 128 MiB managed-memory growth and 60 seconds consumer
work. Root cardinality remains at least three; reducing below it refuses input.
Every needed subset must be examined; no sampling or silent candidate truncation.
Legacy whole-session validation precedes consumer budgets and retains its
preexisting cost. LINQ/JSON serialization, native blocked I/O and cooperative
memory/time checks are not instantaneous hard process bounds.

Publication reuses PhaseFour destination exclusion and ObservedReportPublication:
known captured libraries/evidence/source DBs are protected by lexical/native
bindings, only a new/empty requested report directory is accepted, and exclusive
owned temporary files are flushed before no-overwrite publication. Failure may
leave an owned temporary if binding is lost; no universal atomicity/TOCTOU claim.
Unknown player roots cannot be inferred protected. No real library was accessed.

## Validation and remaining scope

Dedicated tests and the new Phase6B smoke exercise independent known payload/path
oracles, triples/quads/six roots, nontransitivity, maximal singleton alternatives,
different chart/resource names, hardlink appearance multiplicity, conflicts and
complete variants. The smoke runs scan→preflight→CONTENT→group-plan→group-verify,
plus deterministic reexport, with all original payload files exclusively locked
during planning/verification. It tests both a conflicting and a compatible triple.
Native metadata/bytes/tree/DB/sidecar preservation and rehashed-plan adversaries
are also covered by the dedicated suite. Exact final totals and commands are
recorded in the local A–R report after integral validation.

Integral validation also reproduced a preexisting test scheduling ambiguity in
PhaseTwoPreflightTests' writer-exclusion test: synchronously waiting Task.Run can
inline its contender on the mutex-owning thread and reenter the thread-affine
mutex. A local 100-attempt diagnostic against the unchanged writer reproduced
99 same-thread reentries and zero dedicated-thread exclusion failures. Only that
test's scheduling is strengthened to a dedicated LongRunning task, with an
additional distinct-thread assertion; its exception/read/file expectations stay
intact. PhaseTwoWriter and all Phase 2 production behavior remain unchanged.

Final locked restore and serial Release build passed with zero warnings/errors.
The post-correction full suite passed 731/731 (all 672 baseline cases retained,
two new 7A fixture cases and 57 new 6B cases); offline passed 276/276. No failures
or omissions. All ten synthetic smokes passed; the corrected writer test also
passed five isolated repetitions. Earlier failed/intermittent runs remain in the
evidence directory, alongside the final `release-validated.trx`, diff and A–R
report. The conflicting CLI fixture examined seven subsets, retained two maximal
alternatives and accounted for all 16 source appearances in each; the compatible
fixture retained one unselected proposal with ten virtual entries and the same
16 appearances. Whitespace, new-file privacy, historical-contract preservation,
PowerShell syntax and 47 local documentation links/anchors checks passed.
Artifacts: `.artifacts/phase6b-20261015-1f66eb4d`. No stage, commit or push.

Phase 6B is not independently accepted, Phase 6 is not universally closed, and
there is no general solver, observation-pair adapter, universal semantics/package
proof, real acceptance, group/variant construction, relocation, Phase 8 or 9.
STOP for independent review; no stage, commit or push.
