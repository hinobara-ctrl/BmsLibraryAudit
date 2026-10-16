# BmsLibraryAudit

Independent, conservative BMS library auditor.

The separately authorized **synthetic explicit-subset pair observer** and its
diagnostic-budget hardening were published and independently accepted within
their bounded synthetic scope at `84f1fc640d177920aed9b2ddbff8c8c0b2c08781`.
It has a separate evidence profile and no Phase 4/5 adapter; see usage and limits
at the end of this document. Broader Phase 5 and real-operation gates remain open.

The [proposed chart-anchored Phase 6 design](docs/PHASE_6_CHART_ANCHORED_CONSERVATIVE_CONSOLIDATION_DESIGN.md)
guides the first two-root read-only physical union planner using verified legacy
EXACT_ONLY CONTENT. Phase 6A was independently accepted within its synthetic scope
and published at `3d8cce1a65c4f5ef1ded27a1ccee27710f86e8dc`; its
[bounded contract](docs/PHASE_6A_VIRTUAL_UNION_IMPLEMENTATION.md) retains every
appearance, auxiliary and conflict. The first internal synthetic Phase 7A staging
harness was published and independently accepted in its bounded synthetic scope
at `40dc261f27e0f25adc084057e3d2a14f316301c9`. The new read-only Phase 6B group
planner below is implemented synthetically pending independent review. Variant relocation, operational
publication and cleanup remain future capabilities with separate gates.

**Long-term objective:** verified fusion of related BMS/BMSON folders and removal
of provably redundant file instances, while preserving all unique content and
required chart-resource relationships. This is not a general library manager.
See the [current nine-phase roadmap](ROADMAP.md) for the final mutation gates
and priority research gaps; historical design remains in MASTER_CONTEXT.md.

Development follows [the current master-context strategy](MASTER_CONTEXT.md#41-current-development-strategy--2026-10-14): implement sufficiently understood capabilities and research genuinely unresolved questions. Phase 2B compares exact folder contents, Phase 3 extracts resource syntax, and bounded Phase 4 joins verified syntax to sealed physical observations on synthetic inputs.

Implemented scope: validated `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`
and `PHASE_2A.READ_ONLY_PREFLIGHT` / `PHASE_2B.EXACT_CONTENT_EVIDENCE` / Phase 3 syntactic inspection / `PHASE_4.CONSERVATIVE_DEPENDENCY_RESOLUTION` with synthetic validation on Windows. Phase 4 now proves bounded logical-reference coverage; universal dependency closure remains unproved.
Heuristics discover. Exact evidence proves. Verified plans mutate.

`PHASE_1_STATUS: VALIDATED`. Phase 1 was validated against the real library;
see [the real-library validation record](docs/PHASE_1_REAL_LIBRARY_VALIDATION.md).

Phase 1 indexes opaque chart bytes and reports chart-set relationships.
It does not authorize consolidation or removal of any source content.

The .NET 10 CLI builds an independent incremental SQLite index, streams MD5 and
SHA-256 together, discovers duplicate charts, classifies shared chart sets and
reports related/exact-only components. No LR2 or beatoraja is needed.

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- scan --db C:\Audit\audit.db --root E:\Songs
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- report --db C:\Audit\audit.db --output C:\Audit\reports-001
```

Use repeated `--root` options for multiple disjoint roots and `--rehash-all`
to refresh every hash. Each scan supplies the entire active root configuration.
Size/mtime cache reuse is a performance hint, not equality proof. Read errors
are persisted; incomplete folders cannot become classification candidates.

Database and reports must be outside source libraries. The application rejects
foreign databases and reparse-point ancestors, skips nested directory links and
never modifies source files. Report output must be a new or empty directory.
It generates `summary.json`, `duplicate_charts.csv`, `folder_candidates.csv`
and `clusters.csv`. Clusters describe relationship paths, not merge authority.

See [Phase 1 usage, schema, safety and limitations](docs/PHASE_1.md),
[real-library validation](docs/PHASE_1_REAL_LIBRARY_VALIDATION.md),
[architectural context](MASTER_CONTEXT.md), [sources](docs/SOURCES.md) and
[repository policy](AGENTS.md). A synthetic CLI smoke test is available as
`./scripts/Smoke-Test.ps1` after the Release build.

Phase 2A captures a read-only Phase 1 database, freshly revalidates direct charts
in complete EXACT_ONLY components, and inventories their observation subtrees
using metadata only. It persists an independent `phase2.db`, a deterministic
preflight seal and four reports. Non-chart resources and unselected nested
charts are not payload-read. Windows native identity, alias and exclusion
checks are mandatory; unsupported configurations fail closed.
Before source SQLite provider access, capture now checks the guarded 100-byte
header, exact magic and rollback format versions 1/1; WAL without sidecars,
mixed/unknown versions and truncated headers are refused. This bounded safeguard
does not establish complete native nonmutation evidence or authorize a real pilot.

```powershell
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- phase2 preflight --source-db C:\Synthetic\audit.db --source-scan 1 --db C:\Synthetic\phase2.db --output C:\Synthetic\preflight
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- phase2 report --db C:\Synthetic\phase2.db --session 1 --output C:\Synthetic\repeat
./scripts/Phase2A-Smoke-Test.ps1
```

Use repeated `--component EXACT_ONLY_000001` to select complete components from
the captured latest scan. Report output must be new or empty and outside every
captured current/historical source root. Reporting uses persisted evidence.
Real-library Phase 2A/2B execution requires separate authorization. Preflight
admission is metadata evidence, with no package-equivalence or mutation claim.
See [the Phase 2A implementation report](docs/PHASE_2A_IMPLEMENTATION_REPORT.md)
and [the governing design contract](docs/PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md).

Phase 2B is a separate, explicit command. Accept the exact digest of a sealed
COMPLETED preflight with ADMITTED complete EXACT_ONLY components:

```powershell
# Synthetic inputs only; replace the digest with that preflight's ManifestSha256.
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- phase2 hash --db C:\Synthetic\phase2.db --preflight 1 --accept-manifest <preflight-sha256> --output C:\Synthetic\content
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- phase2 report --db C:\Synthetic\phase2.db --session 2 --output C:\Synthetic\repeat-content
./scripts/Phase2B-Smoke-Test.ps1
```

The CLI checks the full accepted namespace/metadata before and after streaming
fresh SHA-256 reads of every required regular path, including opaque resources,
archives, unknown extensions and nested charts. One normalized physical path
is read once per run, with at most one unchanged-stamp instability retry;
hardlink names remain distinct paths and each receives a fresh read. Direct
chart digests must match the accepted preflight. Namespace/stamp/safety drift
makes the whole CONTENT session STALE. A content-only read error or instability
makes every dependent component INCOMPLETE; other complete components continue.
Exit codes are 0 for all complete content components, 2 for incomplete/stale,
1 for refused/failed operations, and 130 for cancellation. Interrupted sessions
are never resumed or repaired automatically.

Completed CONTENT exports contain `session_summary.json`, `observation_roots.csv`,
`filesystem_inventory.csv`, `diagnostics.csv`, `folder_comparisons.csv` and
`content_groups.csv`. Comparisons retain relative file/directory paths, conflicts,
exclusive entries, full/non-chart tree equality and digest multiplicity; the
six OBSERVED flags describe complete views only. INCOMPLETE components publish
no comparison rows. FAILED/STALE/CANCELLED exports have the four audit reports,
no trusted hashes or equality conclusions. Separate export receipts do not
change the evidence seal; repeated exports are byte-identical.

Fresh Phase 2 databases use capability `PREFLIGHT_AND_CONTENT`, revision
`phase2ab-content-1` (application ID/schema version and contract v1 unchanged).
Older `PREFLIGHT_ONLY`/unsupported profiles are rejected unchanged before writable
provider access. There is no migration, resealing or historical evidence import;
create a fresh synthetic preflight database with this version. The original
PREFLIGHT canonical groups and report meaning remain intact. CONTENT uses the
contract's four canonical groups, immutable terminal evidence and rederived
comparisons during verification/export.

Existing Phase 2 databases now undergo the same guarded, pre-provider rollback
header validation as Phase 1 source capture. WAL 2/2, mixed/unknown versions,
bad magic and truncated headers are refused without provider invocation or
repair; legitimate new database initialization and in-memory schema generation
remain supported. This does not establish real-source operational acceptance.

Evidence describes stable read observations, not a simultaneous filesystem
snapshot: metadata-preserving edits after a read can escape final checks. A hash,
tree or multiset match grants no deletion/merge authority. OBSERVATION_ROOT is
not PACKAGE_ROOT; Phase 2 does not provide semantic dependency closure, universal
player resolution, plans, staging/publication or cleanup. Hashing is sequential with
a reused 64 KiB payload buffer; per-pair views and the existing captured chart
analysis still use memory proportional to their scope. No real-library Phase 2B
execution or large-scale performance acceptance is claimed.

Phase 3 provides a per-file, read-only syntactic inspector without databases,
library recursion or resource file access:

```powershell
# An explicitly created synthetic chart; reports are outside its source folder.
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase3 inspect --chart .artifacts/synthetic-phase3/charts/chart.bms --source-root .artifacts/synthetic-phase3/charts --output .artifacts/synthetic-phase3/report
./scripts/Phase3-Smoke-Test.ps1
```

`chart_inspection.json` is deterministic UTF-8 without BOM, with retained original
bytes (base64), fresh SHA-256, decoded text candidates, declarations, logical
resource groups, potential usages, conditional contexts, raw unknown evidence
and diagnostics. Source locations are BMS line/column or BMSON JSON pointers;
duplicate JSON members remain separately observed and are diagnosed. No first-
or last-wins rule is applied. `OBSERVED` describes syntax; `POTENTIAL`,
`AMBIGUOUS`, `UNKNOWN` and `UNSUPPORTED` express the remaining uncertainty.

`--source-root` is mandatory in both the CLI and inspector API: declare the whole
source library boundary to protect. The chart must be inside that existing,
ordinary root; output cannot equal, descend from, or be an ancestor of it.
Protection therefore includes sibling song folders under that root. Native
identities/resolved paths and reparse checks are revalidated during staging and
before publication. The declared root and its native facts appear in the report.
No immediate-folder fallback or automatic detection of other libraries exists;
undeclared roots cannot be claimed protected by this inspector.

Reports are serialized deterministically, written to one exclusive operation-owned
temporary in the authorized destination, fully flushed, and renamed within that
directory to `chart_inspection.json` without overwriting existing files. Failure
or cancellation before publication does not create a final report name. Cleanup
only removes a provably owned temporary in the same validated directory; it never
deletes directories, pre-existing reports or replacement files. A process crash
or lost native binding may leave a `.tmp` for inspection. This protocol does not
claim universal rename atomicity or power-loss durability on every filesystem.

BMS/BME/BML/PMS support includes WAV/BMP declarations, stage/banner/backdrop/
preview paths, distinct BPM/STOP namespaces, recognized BGM/note/BGA channels,
and syntactic RANDOM/IF/SWITCH families. All visible branch resources remain
represented; reachability and simulator-specific extensions are deferred.
Strict UTF-8, UTF-8 BOM and Windows CP932 are decoded without replacement.
Successful decoding does not prove author intent: if both strict candidates
produce different text, both extractions are retained. Authored path strings,
including Unicode, spaces, separators and quotes, are never physically resolved
or renamed.

BMSON support is explicitly limited to dependency syntax of version `1.0.0`:
sound-channel names and notes, BGA headers/events/layers/poor events, and the
documented image/preview fields in `info`. Lane, pulse, length and continuation
facts remain recorded; audio slicing and playback simulation are not implemented.
Legacy/unknown versions, duplicate/conflicting properties, unexpected types and
unknown properties produce conservative diagnostics with retained raw evidence.
`info.title_image` is preserved as a potential VISUAL declaration with its authored
string and JSON pointer. The specification's descriptive section includes it,
while the introductory IDL omits it; `TITLE_IMAGE_SPEC_DISCREPANCY` explicitly
marks this uncertainty and incomplete coverage. Null has no invented path and
is explicitly diagnosed; empty strings remain declarations with a diagnostic;
invalid types retain raw diagnostics; duplicates retain every alternative.
No universal simulator interpretation or physical binding is inferred. See the
[specific primary text](https://github.com/bemusic/bmson-spec/blob/master/doc/index.rst).
The format references are the [BMSON specification](https://github.com/bemusic/bmson-spec),
[original BM98 specification](https://bm98.yaneu.com/bm98/bmsformat.html) and
[BMS command memo](https://hitkey.bms.ms/cmds). Implementation is independent.

Default hard limits are 8 MiB per chart, 65,536 characters per BMS line, 50,000
syntax facts, 64 conditional levels and 64 JSON nesting levels. The API can
reduce these limits. Over-size files are not payload-read or hashed: their report
has no byte digest/retained payload and explicitly incomplete coverage. Other
limits preserve accepted bytes/decoded text and diagnose unprocessed evidence.
Logical bindings are stored once per group, avoiding a declaration/use Cartesian
expansion. These are bounded inspections, not large-library performance claims.

Exit 0 means supported dependency syntax was extracted; exit 2 means incomplete
or ambiguous coverage, 1 is an operational refusal/failure, and 130 cancellation.
Output must be new/empty and outside the explicitly declared source root, using Windows
native identity/alias checks. Syntactic coverage is not full chart validation or
dependency closure: `dependency_closure_proven` is always false. No required/
unused labels, physical bindings, Phase 2B provenance joins, Phase 4, rewrites,
consolidation or removal are implemented. No real-library Phase 3 execution is
authorized by synthetic validation.

Deferred research: [Backbeat interoperability and library completeness](docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md)
is a research record with no current integration or mutation authorization.

Synthetic performance checks are separate from unit tests. With Windows
PowerShell 7+, run `./scripts/Start-Phase2A-PerformanceTest.ps1 -Profile QUICK -Synchronous`
or start an autonomous bounded run:

```powershell
./scripts/Start-Phase2A-PerformanceTest.ps1 -Profile EXTENDED -Seed 1729 -NestedRoots -TimeoutMinutes 60
./scripts/Get-Phase2A-PerformanceTest.ps1 -RunDirectory <printed-run-directory>
./scripts/Get-Phase2A-PerformanceTest.ps1 -RunDirectory <printed-run-directory> -Cancel
```

The launcher returns the supervisor PID and an owned run directory under
`.artifacts/phase2a-performance`. The independent supervisor records completion,
failure, timeout or cancellation; no open shell or Codex monitoring is required.
See [corrective hardening and performance](docs/PHASE_2A_CORRECTIVE_HARDENING_AND_PERFORMANCE_REPORT.md)
for limits, schema incompatibility, build provenance and validation evidence.

Phase 4 now supplies `phase4 resolve` for **owned synthetic evidence only**:

```powershell
# First build Release. The smoke creates all inputs and prints its artifact path.
pwsh -NoLogo -NoProfile -NonInteractive -File scripts/Phase4-Smoke-Test.ps1

# Example for that generated fixture; use the actual CONTENT ID and digest.
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase4 resolve --db C:\Synthetic\phase2.db --content-session 2 --accept-content-manifest <content-sha256> --chart-report C:\Synthetic\syntax\chart_inspection.json --observation-root C:\SYNTHETIC\LIBRARY\A --output C:\Synthetic\resolved-new
```

Select the exact `root_key` from the accepted observation-root report explicitly.
The evidence DB opens read-only; the compiled profile, exact COMPLETED CONTENT
digest, linked PREFLIGHT/source seals, integrity, complete coverage and rederived
comparisons are verified. Only an ADMITTED EXACT_ONLY component with COMPLETE,
coherent, sealed and eligible CONTENT is admitted. Other INCOMPLETE components
in that same globally coherent completed session do not invalidate the complete
component; nothing is salvaged from an incomplete component. No schema, migration,
resealing, persistent Phase 4 database or Phase 2 report receipt is introduced.

The unsigned Phase 3 report must contain bounded original bytes and declared
root/path/native chart identity, size, mtime and attributes matching a SUCCESS
physical membership of the selected root. SHA-256 is recalculated from included
bytes and must equal accepted CONTENT; the current parser reconstructs **every**
syntactic field and rejects manipulated declarations, digests or coverage. The
declared library root must match the sealed chart's Phase 1 source membership.
Library-root native facts in the unsigned envelope remain hints, not an additional
sealed identity proof. Missing evidence causes refusal; original charts/resources
are never opened to supplement it. Directory/input metadata for native destination
exclusion is still checked. A binding proves identity with a historical observation,
not that the original chart remains identical today.

`dependency_resolution.json` (`phase4-dependency-2`) is deterministic UTF-8/LF and includes version/build
provenance, CONTENT/PREFLIGHT/component/root IDs and digests, historical observation
times, verified syntax, declaration/usage relations, lexical bases, physical entry
IDs, native identities, SHA-256, candidates, states, predicates and limitations.
Library root, observation root and chart parent remain distinct; package root is
explicitly unknown. Repeated exports of identical evidence by the same build are
byte-identical. All known captured roots and evidence inputs are protected; output
must be new/empty. Phase 3 and 4 share exclusive temporary staging, flush and
no-overwrite publication. Binding loss/crash can leave a temporary; only a provably
owned temporary can be cleaned up, never user files or directories.

Ordinary relative paths use explicit lexical chart-parent interpretation and
selected observation memberships. `..` is accepted only within that root; mixed
separators are normalized lexically. Ordinal spelling and Windows ordinal-ignore-case
matches remain separate. No filesystem lookup of authored paths occurs. Outcomes:
`OBSERVED_PATH_MATCH`, `AMBIGUOUS_PATH_MATCH`, `NO_OBSERVED_MATCH`, `OUT_OF_SCOPE`,
`UNSUPPORTED_RESOLUTION`, `UNKNOWN`. No observed match does **not** establish absolute
absence. Same-stem alternate/omitted extensions are retained as explicitly unproved
proposals, never substituted; equal digests under distinct names grant no renaming
or removal authority. Absolute/UNC/device paths are out of scope. ADS, quotes,
reserved/invalid names, terminal dots/spaces and potential short-name aliases are
unsupported without rewriting. Unicode is not normalized; all strict decoding
alternatives, duplicate declarations and nested/shared memberships are preserved.
Hardlink names retain separate entry/physical IDs despite shared native identity.

The named `nested-random-first-matching-branch-v1` model computes intersections
over symbolic random variables in `1..n`, IF equalities and earlier-branch exclusions
for ELSEIF/ELSE. It supports well-formed nested scopes without enumerating products
of random ranges. An outer RANDOM may end at EOF; nested scopes require explicit
closure. This model follows the [original BMS specification](https://bm98.yaneu.com/bm98/bmsformat.html)
and the documented [control-flow variants](https://hitkey.bms.ms/cmds.htm#CONTROL-FLOW).
Those references also describe incompatible player behavior. `POSSIBLE_WITHIN_MODEL`
and `IMPOSSIBLE_WITHIN_MODEL` are qualified conclusions only, with predicates and
justification; **even impossible syntax and candidates remain in the report**.
The preserved v1 reachability fields keep their original model: direct content inside RANDOM without IF, malformed/unknown control evidence,
SETRANDOM, SWITCH/SETSWITCH/CASE/DEF/SKIP and bounded-out conditions remain UNKNOWN.
Every active RANDOM scope must have its own admitted IF-chain predicate, identified
by that RANDOM's context ID. Predicates inherited from an outer scope or belonging
to an inner scope do not cover an unguarded RANDOM; reachability stays UNKNOWN
before any contradiction reduction, while observed physical candidates remain.
The preserved `potential_usage_ids` links are namespace/decoding-specific syntactic possibilities and
are not first/last-wins playback bindings; mutually exclusive pairs are retained.
No REQUIRED_ALWAYS/REQUIRED_CONDITIONAL/DECLARED_UNUSED classification is asserted.
Dependency closure and removal authority always remain false.

Defaults/hard caps (API callers can reduce them): 64 MiB input/output report,
8 MiB included chart bytes, 50,000 entries per syntactic evidence array (two
decodings), 100,000 selected inventory paths, 50,000 declarations/usages,
64 candidates or usage links per declaration, 100,000 physical links and 100,000
usage links, depth 64 and 100,000 symbolic terms. Route/stem indexes avoid repeated
inventory scans per declaration. Inventory/reference/candidate/symbolic limits
yield explicit INCOMPLETE/UNKNOWN; retained syntax is not silently dropped. Input
or serialized-output size limits refuse publication with an explicit INCOMPLETE
error. Existing Phase 2 seal/graph/comparison verification still has its documented
scope-sized cost; this is not million-file or universal simulator acceptance.
The owned 400-resource/200-declaration regression verifies indexed moderate input.

The additional `semantics` object (`phase4-semantic-graph-2`) preserves the original
fields and adds explicit declaration/usage edges, individual/joint predicates,
physical-candidate catalog IDs, coexistence conflicts, coverage witnesses and
conservation obligations. Relations use `MAY_BIND_WITHIN_MODEL`,
`CANNOT_CO_OCCUR_WITHIN_MODEL`, `UNKNOWN_SEMANTICS` or
`UNRESOLVED_LOGICAL_REFERENCE`; effective declarations distinguish single candidates,
branch-exclusive alternatives and ambiguous coexistence. A global declaration
plus conditional redefinition is recorded as a possible redefinition with unknown
precedence. No first/last-wins rule is selected, including equal-byte duplicates.
Textual before/after positions are reported without assuming application order;
existing Phase 3 line/column/JSON-pointer evidence is sufficient and unchanged.

Semantic analysis reuses the random predicates and evaluates a separate documented
fixed-value model for SETRANDOM. Fixed controller domains are explicit, including
uses outside the block. A small `random-fixed-flat-switch-explicit-skip-v1` model
admits flat SWITCH/SETSWITCH sections with SKIP between branches, DEF last and
mandatory ENDSW. SKIP suppresses later facts in that same section, whose syntax
and candidates remain retained. Fallthrough, nested controls inside SWITCH,
DEF-before-CASE and malformed structures remain UNKNOWN. The documented
[control-flow differences](https://hitkey.bms.ms/cmds.htm) are not universal player
semantics. Legacy v1 reachability remains unchanged; the new graph identifies its
separate models. In graph predicates `random_context_id` identifies the controlling
context row, including SWITCH in that specifically named model.

Usage coverage subtracts finite interval-set declaration boxes from the usage
region without enumerating values or products. An uncovered symbolic box is a
checkable witness of unbound assignments. Separate pair/triple intersections
detect declarations that coexist specifically at that usage. Enumeration, model
coverage, effective precedence and observed physical bindings are separate fields.
The bounded proof `observed-logical-physical-ordinal-binding-evidence-2` can be
`PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE` only when admitted syntax has one decoding,
at least one usage has a proven satisfiable region, all satisfiable usage assignments
have unique declaration candidates in the named model, at least one covered,
unambiguous satisfiable declaration/usage relation binds a physical resource, and
all authored physical paths have unique ordinal-exact historical CONTENT bindings.
The required policy is `LEXICAL_ORDINAL`, with exactly one retained candidate and
no truncation or Windows spelling collision. Unknown
syntax/models, holes, ambiguous precedence, limits or unobserved paths prevent
that proof. Only `UNCONDITIONAL_SYNTAX` and `POSSIBLE_WITHIN_MODEL` establish a
satisfiable usage region, using the existing predicates including fixed domains;
`UNKNOWN` does not. Empty usage graphs and graphs with only impossible usages
receive `NOT_PROVEN` with `NO_SATISFIABLE_USAGE_WITHIN_ADMITTED_MODEL`.
`USAGE_IMPOSSIBLE_WITHIN_MODEL` and complete vacuous coverage remain mathematical
facts, separate from positive binding proof. Impossible usages remain present and
do not block a proof when another reachable usage is covered and all other gates pass.
This scope expressly excludes
playback, codecs/fallback, implicit dependencies, package closure and portability;
`dependency_closure_proven` and `removal_authority` remain false in every result.

Two optional additive coverage objects separate the claims:
`logical_reference_coverage` records admitted logical coverage and counts of
proven satisfiable, impossible and unknown usages. `physical_resource_binding_coverage`
records each physical declaration's policy, candidate count, ordinal/Windows match,
ambiguity, no-match, unknown/out-of-scope state or extension/BMSON proposal. It also
counts satisfiable physical relations, covered unambiguous observed bindings and
the subset with ordinal-exact bindings. These counters do not certify global
completion independently of the model, enumeration and proof gates.

| Authored chart / observed inventory | Logical coverage | Physical evidence / combined proof |
| --- | --- | --- |
| `#WAV01 kick.wav` plus usage / `kick.wav` | May be complete | `LEXICAL_ORDINAL`; one ordinal binding can permit PROVEN if all gates pass. |
| `#WAV01 KICK.WAV` plus usage / `kick.wav` | May be complete | `WINDOWS_ORDINAL_IGNORE_CASE`; observed candidate/hash retained, strict ordinal proof NOT_PROVEN. |
| Extended `#BPM01 150` plus `#00108:01` / no resource paths | May be complete | No authored physical-resource declarations or bindings; NOT_PROVEN with `NO_PHYSICAL_RESOURCE_BINDINGS_IN_SCOPE`. |
| Reachable BPM usage and an unused/model-impossible WAV declaration | May be complete | Ordinal path observation alone is insufficient; no admitted satisfiable physical binding. Declaration/candidates remain conservation obligations. |

`LEXICAL_ORDINAL` compares the admitted normalized lexical path with the stored
relative spelling, not raw chart text byte identity. Windows-only case matches
are successful historical correspondence under their policy; they are not changed
to no-match. Distinct `kick.wav` / `KICK.WAV` entries remain ambiguous even when one
spelling is ordinal-exact. Equal SHA-256 never compensates for a different name.
Timing-only reports remain valid logical analysis, without inventing a BPM file or
declaring the chart useless. Legacy top-level coverage/exit rules remain; a non-path
declaration can retain legacy unsupported physical resolution/exit 2, while the
new logical coverage is complete. NOT_PROVEN is not an operational refusal.

The old `physical_binding_coverage` and `effective_declaration_coverage` strings
retain their graph-v1 meanings for compatibility. In particular, the former accepts
Windows matches and can be vacuous; it is not the strict new physical proof.
Consumers must check both semantic/proof versions and the precise additive coverage.
Older graph-v1 results deserialize with the two new objects null; absence does not
establish the reinforced proof. No historical result is migrated or resealed.

BMSON 1.0.0 sound-channel lookup is separate from literal resolution and BMS rules.
The [BMSON specification](https://github.com/bemusic/bmson-spec/blob/master/doc/index.rst#sound-channels)
allows compatible-name search for omitted extensions and unusable/missing literal
files. The graph retains observed same-stem entries within the selected inventory,
even with a literal match because usability is not checked. WAV/OGG/M4A are marked
as recommended-format potentials; other entries remain codec-compatibility UNKNOWN.
No search order, decoder success, extension equivalence or physical selection is
asserted. Notes retain pulse/length/continuation evidence without audio slicing.
BGA/header relations remain distinct; title_image retains its normative uncertainty.

Additional hard caps, reducible through the API: 100,000 semantic work units
(edges/coexistence/triple checks), 100,000 semantic physical-candidate references
and 100,000 semantic symbolic terms, also capped by the existing symbolic limit.
Reaching any cap yields explicit incomplete coverage and no bounded proof; full
verified input syntax and old declaration evidence remain present. JSON v2 keeps
the v1 field meanings and adds `semantics` and these limits. Consumers must check
`result_version`, tolerate the additive object, and consult semantic coverage rather
than treating old `potential_usage_ids` as branch-compatible bindings.

Exit 0 means completed observed-path analysis, semantic enumeration and supported syntax within the
bounded model, **never full dependency closure**; a literal NO_OBSERVED_MATCH can
still have exit 0. Exit 2 retains incomplete/ambiguous/unsupported syntax or links;
1 refuses invalid evidence/destination or publication, and 130 means cancellation.
The synthetic predicate/coverage scope is implemented and tested. Simulator-specific
precedence, fallthrough models, codec usability, implicit dependencies and universal
required/unused or dependency closure remain open and blocked. Real-data operational
acceptance, Phase 5 and all mutations need separate authorization.

The Phase 5 consumer must keep five independent evidence levels:

| Level | Existing result evidence | Meaning |
| --- | --- | --- |
| Physical provenance | `provenance`, declaration candidates, physical catalog | Accepted CONTENT/PREFLIGHT, component/root/chart SHA-256, entry identities and physical multiplicity. |
| Lexical resolution | Declaration resolution/path policies and BMSON lookups | Observed match, policy-qualified no-match, Windows spelling and potential audio alternatives. |
| Chart semantics | Verified syntax, relations, predicates, coexistences and usage coverage | Declarations/uses/contexts, compatibility, unselected precedence, reachability and symbolic witnesses. |
| Bounded proof | Named models, `bounded_proof.state`, scope and blockers | Positive evidence requires a satisfiable usage; impossibility/uncertainty and limits remain explicit. |
| Operational authority | Global flags and limitations | No demonstrated global closure, safe fusion or removal authority. |

Consumption rules, applicable to the existing v2 interface and Phase 5:

1. `ExitCode == 0` is not proof that resources are complete.
2. `OBSERVED_PATH_MATCH` does not prove effective resource use.
3. `NO_OBSERVED_MATCH` does not prove absolute physical absence.
4. `PROVEN_FOR_NAMED_OBSERVED_BINDING_SCOPE` is not universal closure.
5. `IMPOSSIBLE_WITHIN_MODEL` is not impossibility in every simulator.
6. `NO_CO_OCCURRING_USE_OBSERVED_WITHIN_MODEL` is not an unused-resource classification.
7. Equal SHA-256 does not imply interchangeable paths or names.
8. Historical observation does not establish current filesystem state.
9. Consumers must preserve uncertainties, truncations and ambiguities.
10. No Phase 4 result authorizes fusion or deletion.

Package comparison must evaluate each dimension independently and preserve
physical identities, playback alternatives, auxiliary files and path conflicts.
Phase 4 provides no package classifier, recoverable-space calculation
or deletion list. JSON stays `phase4-dependency-2` with the same fields; the proof
restriction is expressed by its state/blockers. Legacy compatibility is tested
with an explicitly sanitized deserialization-only fixture derived from an owned
synthetic v1 smoke report in
`tests/BmsLibraryAudit.Tests/Fixtures/phase4-v1-synthetic.json` (SHA-256
`33f790fca0285be16ff7224cb2f24494cf753386cc6cc339708ae463ef921bb8`).
All absolute paths use the neutral synthetic base `C:\BmsLibraryAuditSynthetic\phase4-v1`;
native identities, observation timestamps and seal-related digests are synthetic
placeholders. The modified fixture does not establish valid CONTENT/PREFLIGHT seals
or physical provenance. The test checks v1 field deserialization, multiplicity and
additive defaults only; it never opens those paths or invokes a seal verifier.

## Phase 5: conservative observation classification (synthetic scope)

```powershell
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase5 classify `
  --db <owned-phase2.db> --content-session <CONTENT-id> `
  --accept-content-manifest <accepted-CONTENT-sha256> --component <component-key> `
  --phase4-report <root-A-chart-report.json> --phase4-report <root-B-chart-report.json> `
  --output <new-or-empty-directory-outside-all-source-roots>
```

`--phase4-report` can occur zero or more times. Component keys come from the accepted
CONTENT/observation-root reports. This first increment admits only captured
EXACT_ONLY components that are ADMITTED, COMPLETE, coherent, sealed and comparison
eligible. The database opens read-only with the existing header/schema/integrity,
CONTENT/PREFLIGHT/source seals and comparison reconstruction. No new scan, resource
read, schema migration, evidence reseal or observation expansion occurs.

The deterministic UTF-8/LF `classification_report.json` (`phase5-conservative-classification-2`) combines the summary,
root inventories, pair relationships, differences, content groups, conflicts,
verified chart dependencies, diagnostics and pending discovery. Each entry retains
relative/original paths, entry/physical IDs, native identity, SHA-256, metadata,
chart role and captured Phase 1 SHA-256 when present. CONTENT chart identities are
stronger than cached discovery; distinct hashes and physical counts are separate.
Shared/nested memberships and hardlink names remain individually visible.

Pair dimensions are independent: chart sets, full/non-chart trees, full/non-chart
content multisets, directional tree inclusion, exclusive entries, conflicts and
dependency state. The existing PhaseTwoContentComparison implementation is reused
and its persisted counts/flags checked; no new equality engine replaces it.
Empty directories, same-path byte/kind conflicts, case collisions and potential
native-alias conflicts remain explicit. Accepted references annotate resource
conflicts with chart/declaration/policy/model details; both resources are retained.
Every verified Phase 4 result preserves syntax, alternatives, conservation
obligations, UNKNOWNs, limits and BMSON proposals in the root dependency record.

Examples (historical observations, not package conclusions):

| Evidence | Independent classification |
| --- | --- |
| Same relative entries/types/bytes | OBSERVATION_TREE_EXACT; PACKAGE_BOUNDARY_UNKNOWN. |
| Equal SHA-256 multiplicities under different names | CONTENT_MULTISET_EXACT_WITH_PATH_VARIANTS; no path substitution. |
| Same charts, different non-chart files (including auxiliary TXT) | CHART_EXACT_WITH_NONCHART_DIFFERENCES; no chart-resource claim from CONTENT alone. |
| Same charts, different kick.wav bytes with admitted compatible declaration/use bindings on both sides | CHART_EXACT_POTENTIAL_REFERENCED_RESOURCE_VARIANT and HARD_PATH_CONTENT_CONFLICT; not simulator-selected resources or a playback difference. |
| A adds audio, B adds image, complete conflict enumeration and no detected coexistence conflict | OBSERVED_COMPLEMENTARY_CONTENT; no safe-union assertion. |
| All A paths/types/digests occur in B, B has extra entries | OBSERVED_TREE_SUBSET_A_IN_B; no redundancy assertion. |

Version 2 keeps the JSON field layout but replaces the overly broad v1
CHART_EXACT_RESOURCE_VARIANT label with the generic physical label and a separate
qualified potential-reference label. The latter requires matching chart/declaration/
usage/model identities, complete admitted model/enumeration coverage, a compatible
MAY_BIND_WITHIN_MODEL relation with covered unambiguous usage, and unique observed
ordinal or Windows-qualified candidates on both sides whose path/hash sets differ.
UNKNOWN, unused/model-impossible declarations and unverified/absent reports cannot
establish that label; their candidates and uncertainties remain in the report.
Windows matches retain their policy qualification and do not gain strict ordinal
proof. BMSON search proposals alone do not establish it. No effective playback
selection, interchangeability or necessity is inferred. Physical RESOURCE_VARIANT
conflicts remain content differences, independently of any chart association.
Complementarity is suppressed by case, kind, content or native-alias conflicts,
including incomplete conflict enumeration, without changing trees or multisets.
Existing v1 reports remain historical artifacts and are not rewritten; consumers
must check result_version before interpreting the changed composite labels.
Phase 4 result/graph/proof versions and CONTENT seals remain unchanged.

Trees/multisets are `PROVEN_FROM_ACCEPTED_CONTENT`. Phase 1 relationships are
rederived from the sealed captured snapshot with the existing analyzer; candidates
outside the selected admitted root scope remain `CANDIDATE_FROM_PHASE1` /
`PHYSICAL_COMPARISON_NOT_ADMITTED`, never inferred physical equality.
Pairwise statements remain pairwise; component connectedness is not transitive
equivalence. Multiple chart parents/nested roots can suggest a container, while
package boundaries always remain unknown in this increment.

Pending discovery reuses charts **and captured diagnostics from the same admitted
snapshot**. Historical inaccessible/skipped/error coverage excludes affected folders
through the unchanged Phase 1 analyzer. Exclusion leaves relationships UNKNOWN,
not proven absent, and does not invalidate independent admitted CONTENT pairs.
Conflict references keep `path_policy` as the declaration policy and additionally
retain `candidate_match_policy`, `candidate_origin`, `candidate_entry_id` and
`candidate_relative_path`. DECLARATION_RESOLUTION and BMSON_AUDIO_LOOKUP_PROPOSAL
remain separate even for the same entry. BMSON search/unknown-codec policies are
potential observations, never literal selection, codec equivalence or playback.
These optional additive fields keep the v2 contract; absent fields in earlier
reports mean unknown candidate provenance, not inherited literal policy.

Unsigned Phase 4 JSON is not trusted. Admission requires the exact current result,
graph and proof versions, matching CONTENT/PREFLIGHT/component/root/chart/native
membership, CONTENT-bound included bytes and reconstructed syntax. The shared
pure Phase 4 constructor recomputes declarations, policies, graph, coverage and
proof; all result fields must match. Historical v1 proofs are not accepted as v2
ordinal evidence. Invalid/duplicate reports are unverified; missing charts are
explicitly incomplete. Physical classifications continue independently. No single
chart report stands for the whole root. A root is `PROVEN_WITHIN_NAMED_MODEL` only
when every observed chart has a unique admitted report and its bounded proof is
positive; this still excludes global closure, playback/codecs and current freshness.

`ADMITTED_RECONSTRUCTED_PHASE4` authenticates no historical producer executable.
The included `resolver_build_revision` remains a historical declaration, with
`resolver_build_revision_state = DECLARED_UNVERIFIED_BUILD_REVISION` on admitted
chart records. No existing seal independently anchors the later resolver's build.
A different or edited revision string can therefore remain semantically reproducible;
matching CONTENT or the current build does not authenticate it. Sealed membership,
digests and reconstructed syntax/graph/proof retain their existing checks. The
Phase 5 `provenance.build_revision` describes the current validator build separately.
The new state is additive; its absence does not establish historical authenticity.

Exit 0 means the bounded classification/admission work completed; exit 2 retains
limits or incomplete/unknown dependency admission, including physical-only runs
without Phase 4. Independently proven physical dimensions remain usable in that
report. Exit 1 refuses invalid CONTENT/component/destination or publication; 130
is cancellation. All four authority/equivalence flags remain false. No recoverable
space, removable-file list, fusion plan or mutating executor is generated.

Global and individual states answer different questions:

| Field | Scope and meaning |
| --- | --- |
| Result.Coverage | Global classification/admission work: any retained diagnostic yields INCOMPLETE. |
| Result.ExitCode | 0 only for COMPLETE; otherwise 2 for the returned result. |
| Pair.Trust | PROVEN_FROM_ACCEPTED_CONTENT for a reproduced admitted physical pair; not package equivalence. |
| Pair.ConflictCoverage | COMPLETE or INCOMPLETE enumeration of conflict details; incomplete is never absence. |
| Pair.DependencyState | PROVEN_WITHIN_NAMED_MODEL only if both roots have positive semantic trust; otherwise UNKNOWN. |
| Root.PhysicalEvidenceState | Complete admitted root view, or BLOCKED when its view exceeds the budget. |
| Root.SemanticCoverage | Whether every observed chart has one admitted reconstructed report; not universal dependency closure. |
| Root.SemanticTrust | Positive only if all those charts have the named bounded proof; otherwise UNKNOWN. |

For example, a historical DIRECTORY_UNREADABLE exclusion can yield global
INCOMPLETE while an independent admitted pair retains PROVEN_FROM_ACCEPTED_CONTENT.
Consumers must inspect each dimension rather than replacing its state with the
global status. PACKAGE_BOUNDARY_UNKNOWN still applies to songs, partial distributions,
variant folders, auxiliary collections and nested containers alike.

Hard caps (API budgets can reduce them): 32 roots, 256 pairs, 100,000 root-view
entries and 100,000 cumulative pair-view entries, 64 Phase 4 inputs totaling 64 MiB,
100,000 imported semantic relations, 100,000 conflict details globally, 64 MiB output,
100,000 discovery charts/128 folders/1,024 discovery pairs. Limits leave explicit
unclassified scope; no truncated tree/conflict enumeration is silently complete.
Existing global CONTENT/source verification retains its scope-sized cost before
these budgets; no million-file or real-library performance acceptance is claimed.
Serialization materializes the result and UTF-8 JSON before checking its byte cap,
so that cap is not a universal memory ceiling. Multiple roots can require quadratic
pair work before limits apply. Synthetic validation provides no measured acceptance
for million-file libraries; future scope design must measure these costs separately.
Output/source/input namespace guards and shared exclusive no-overwrite publication
are reused; they do not prove universal race freedom or destructive safety.

Run `pwsh -NoLogo -NoProfile -NonInteractive -File scripts/Phase5-Smoke-Test.ps1`
after a Release build for the owned three-root Phase 1→2A→2B→3→4→5 fixture. It checks
known payload hashes, directional subset and independent conflicts, report
admission, preservation with exclusive source handles and byte-identical reexport.
Real libraries, wider observations, Phase 6 and mutations require separate authorization.

The independent offline source inspector is a separate research component that
validates supplied synthetic evidence without opening source libraries or SQLite.
Its [consolidated closure](docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CLOSURE.md) is
ready for final independent review. Native verification remains incomplete;
offline acceptance grants no real-source or Phase 2A execution authorization.

## Synthetic explicit subset pair observation

`observation-pair` creates profile `explicit-subset-pair-1` / contract
`observation-pair-1` in a **new, independent DB**. Its
[frozen contract](docs/PHASE_5_EXPLICIT_SUBSET_PAIR_OBSERVATION_CONTRACT.md) governs
the implementation. The observer and diagnostic-budget hardening were published
and independently accepted in their bounded synthetic scope at
`84f1fc640d177920aed9b2ddbff8c8c0b2c08781`; this does not close all Phase 5 or
add a Phase 4/5 adapter. Start with the reproducible synthetic fixture:

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File scripts/Observation-Pair-Smoke-Test.ps1
```

For an already generated **synthetic** Phase 1 source, the explicit flow is:

```powershell
$cli = 'src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll'
$capture = dotnet $cli observation-pair capture --source-db C:\Synthetic\audit.db --source-scan 1 --root C:\Synthetic\library\A --root C:\Synthetic\library\B --db C:\Synthetic\pair.db --output C:\Synthetic\source-report | ConvertFrom-Json
$preflight = dotnet $cli observation-pair preflight --db C:\Synthetic\pair.db --accept-source $capture.ManifestSha256 --output C:\Synthetic\preflight-report | ConvertFrom-Json
$content = dotnet $cli observation-pair hash --db C:\Synthetic\pair.db --accept-manifest $preflight.ManifestSha256 --output C:\Synthetic\content-report | ConvertFrom-Json
dotnet $cli observation-pair verify --db C:\Synthetic\pair.db --stage CONTENT --accept-manifest $content.ManifestSha256
dotnet $cli observation-pair report --db C:\Synthetic\pair.db --stage CONTENT --accept-manifest $content.ManifestSha256 --output C:\Synthetic\repeat-report
```

Capture requires exactly two disjoint, non-aliased direct-chart folders and the
latest completed source scan. RelationshipAnalyzer must reproduce a strict
`A_SUBSET_OF_B` or `B_SUBSET_OF_A` from the same captured charts and diagnostics.
Exact/overlap/disjoint/resource-only/transitively inferred selections are refused.
Reversing `--root` order preserves UnitId/PairId and canonical subset direction.
Recursive coverage diagnostics block the affected pair; unrelated source diagnostics
remain visible as incomplete global coverage without erasing a complete pair.

SOURCE seals the protected source capture, version and limits. PREFLIGHT seals
the selected roots, complete recursive metadata inventory, fresh chart SHA-256
reads and memberships, including exclusive/nested/BMSON/duplicate charts.
Non-chart readability remains NOT_TESTED. CONTENT explicitly accepts that exact
PREFLIGHT digest and freshly reads every regular path, including hardlink names,
auxiliaries and unknown files. Namespace/stamp/identity drift is STALE; stable
resource read failure makes the affected root INCOMPLETE and prevents any positive
pair comparison. Cancellation and operational failures retain terminal audit
records without successful digests. Attempts cannot resume or reseal.

Every stage exports `observation_pair.json`. Successful `verify`/`report` commands
require the exact supplied successful digest and reconstruct historical evidence,
root outcomes and comparisons; they do not reopen original charts/resources.
Reports retain direct discovery separately from recursive chart copies, all path
entries/native identities/hashes, ordinal differences, FULL/NON_CHART digest
multisets, directional tree inclusion and path/type/case/native-alias conflicts.
Chart subset alone never establishes physical inclusion. The shared exact kernel
is unchanged. Same sealed evidence reexports byte-identically without a new
timestamp. A publication failure leaves sealed evidence intact for a separate
export to a new destination; no existing report or foreign temporary is replaced.

Provisional synthetic caps: 2 roots/1 pair; source DB 64 MiB, 512 source charts,
128 direct folders and 1024 diagnostics; 512 recursive charts, 8192 entries,
4096 regular paths, depth 32; 256 MiB/file, 1 GiB expected payload and 2 GiB attempted
bytes per stage, at most two unchanged-stamp read attempts/path; 300 seconds and
256 MiB managed-memory growth at cooperative checkpoints; 16 MiB/stage payload,
64 MiB/evidence DB, 32 MiB/report. API limits can only reduce these positive caps.
Limits refuse or explicitly make coverage incomplete; they never sample or skip
integrity verification. Serialization/checkpoints are not instantaneous memory,
blocked-I/O or real-library performance guarantees.

Both DB profiles reject each other. Historical Phase 2 seals, admission and CLI
are unchanged; `phase4 resolve` and `phase5 classify` do not consume the new profile.
Semantic evidence is NOT_EVALUATED, package boundary and current freshness are
UNKNOWN, and all dependency-closure/package-equivalence/merge/removal authority
flags stay false. Digests anchor accepted consistency, not authenticated producer
identity against complete replacement/resealing. This increment does not authorize
real-library execution, semantic adapters, resource substitution, deletion or fusion.

## Phase 6A: historical virtual union (bounded synthetic scope accepted)

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File scripts/Phase6A-Smoke-Test.ps1
# Use only the owned synthetic evidence emitted by that smoke:
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase6 plan `
  --db <phase2.db> --content-session <id> --accept-content-manifest <sha256> `
  --component <sealed-component-key> --root <sealed-root-key-A> --root <sealed-root-key-B> `
  --output <new-or-empty-directory-outside-protected-roots>
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase6 verify `
  --db <phase2.db> --content-session <id> --accept-content-manifest <sha256> `
  --component <sealed-component-key> --root <sealed-root-key-A> --root <sealed-root-key-B> `
  --plan <virtual_union_plan.json>
```

Exactly two distinct physically disjoint roots must belong to the same admitted
COMPLETE legacy EXACT_ONLY component. Both commands readmit CONTENT/parent/source
seals, schema, memberships, SUCCESS hashes and reconstructed comparisons. Neither
accepts observation-pair or Phase 4 report inputs, reopens source payloads, writes
evidence, nor materializes a destination. Current native directory binding checks
guard selection/exclusion; historical bytes remain historical.

The deterministic `virtual_union_plan.json` records each source appearance and
chart context, per-entry conservation, virtual entries with all contributing
origins, full conflicts, counts and a separate versioned canonical plan digest.
Same path/type/bytes has one virtual target; exclusives and same bytes at different
paths remain. Byte/type/case/native conflicts block affected paths and descendants;
both originals remain. Missing semantic reports are explicitly UNKNOWN, with one
unknown dependency obligation per chart appearance. Package boundary and relocation
remain unproved; no merge/removal authority, copy-source choice or savings claim.

`phase6 verify` checks exact inventories/provenance and reconstructs conservation
and target mappings independently of the planner; a recalculated digest alone
cannot admit a changed plan. Current validator revision participates in provenance;
this first format requires a matching declared validator revision value.
`UNKNOWN_DIRTY` remains unknown build identity; matching it authenticates no
executable. Cross-version replay and historical producer authentication are unproved.

Exit 0: compatible historical virtual plan; 2: verified conflict plan; 1: refused
evidence/selection, malformed plan, exceeded budget or publication error; 130:
cancellation. CLI summaries expose counts, states, digest and report path. A
conflict plan is not permission to unite the remaining entries operationally.

Limits: 2 roots, 20,000 source entries, 20,000 virtual entries/conflicts, zero
imported semantic reports/references, 16 MiB plan input/output, 128 MiB managed
memory growth and 60 seconds at cooperative checkpoints after existing global
CONTENT verification. Budgets may only be reduced through the API. Limits refuse
without partial success; serialization/blocked I/O and preexisting global verifier
work are not instantaneous hard bounds. Existing protected-path/native checks and
exclusive no-overwrite report publication are reused. See the
[Phase 6A contract](docs/PHASE_6A_VIRTUAL_UNION_IMPLEMENTATION.md) for exact digest,
ordering, states, preservation and verification rules. The separate Phase 6B
consumer below does not change this two-root contract. Phases 8/9 remain
unimplemented; internal synthetic Phase 7A grants no real-data or operational authority.

## Phase 7A: internal disposable synthetic staging

Phase 6A's bounded synthetic implementation was independently accepted and
published at `3d8cce1a65c4f5ef1ded27a1ccee27710f86e8dc`. Its historical contract
and authority flags remain unchanged. The owner records independent acceptance
of the first bounded synthetic Phase 7A increment at `40dc261f27e0f25adc084057e3d2a14f316301c9`:

```powershell
# Release build first; this invokes one fixed integration test, not an executor CLI.
pwsh -NoLogo -NoProfile -NonInteractive -File scripts/Phase7A-Smoke-Test.ps1
```

There is no `phase7 stage` command or public API accepting arbitrary sources,
plans or destinations. An internal factory exclusively creates a random owned
workspace under the test application's base directory, seeds fixed synthetic A/B
payloads and runs scan→preflight→CONTENT→Phase 6A plan/verify. Its private context,
owned-file identities, pinned native directories and fixed disjoint stage path
provide the admission boundary; a marker or IsSynthetic boolean does not.
Generated workspaces are retained for inspection and are not music-library roots.

The constructor readmits only a compatible complete conflict-free plan, freshly
hashes **every** source appearance, holds source handles denying write/delete
sharing, and checks source namespaces/metadata/native identities before and after.
Deterministic (root ID, entry ID) selection chooses a verified copy source only.
CreateNew files and exclusive directory creation build the exact relative tree:
one file per shared path, all exclusives, distinct equal-hash names, byte-identical
charts and empty directories. It does not recreate hardlink topology or rewrite
charts/references. Existing paths, reparse points, escaping/ambiguous Win32 names
and alias substitutions are refused.

The independent verifier enumerates and hashes the real stage, checks every
expected path/type/size/SHA, rejects extras/missing/changed files, rechecks all
sources and reconstructs receipt claims from the admitted plan. Recomputing a
receipt digest cannot authorize incorrect target bytes or edited provenance.
`stage_receipt.json` is outside the staged song tree; it records source/target
facts, all equivalent appearances, chosen sources, actual bytes written and elapsed
milliseconds. Failure receipts distinguish stale sources, incomplete/cancelled
work and operational failure. Partial stages remain isolated, unaccepted and are
never automatically resumed or deleted. Partial-write byte counters include
completed Write calls; file/directory counts describe independently verified
targets, not an assertion that an incomplete stage contains no other entries.

Limits and fault coverage are in the [Phase 7A contract](docs/PHASE_7A_SYNTHETIC_STAGING_IMPLEMENTATION.md):
128 source files, 64 source/target directories, 8 MiB source/copy bytes, 64 MiB
cumulative reads, 2 MiB receipt, 128 MiB managed-memory growth and 60 seconds at
cooperative checkpoints. Limits may only be reduced. This is not a process sandbox
against malicious in-process code, universal race exclusion or a global filesystem
transaction. The smoke prints the physical stage, receipt, file list, bytes and
measured synthetic cost.

`STAGING_PHYSICALLY_VERIFIED` proves faithful physical construction of this owned
fixture. Dependencies, package boundaries, reference/relocation safety and universal
player behavior remain unproved. Sources remain intact, publication and retirement
remain false. This increment implements no Phase 6B construction or Phase 8/9.
No real-library execution or operational consolidation is authorized.

The follow-up synthetic fixtures also cover identical charts named `normal.bms`
and `alternate.bms` in differently named folders, and complementary internal
directories with equal auxiliary bytes at distinct paths. Both chart names and
all distinct paths remain. Verification covers ordinal paths/names, entry kinds,
default-stream bytes/size/SHA, source appearances and empty directories. Native
identities bind source/target observations; target identities are new. Hardlink
names are retained as ordinary independent files, without reproducing link
topology. ACLs, permissions, alternate data streams, timestamps and other NTFS
metadata are not proved preserved. Future real-library enablement must preserve
required properties or explicitly refuse unsupported cases; this harness grants
no such enablement. The historical Phase 7A contract remains unchanged.

## Phase 6B: conservative group and intact variant planning

Implemented for synthetic review, using only 3–6 explicit disjoint roots in one
verified ADMITTED COMPLETE legacy EXACT_ONLY CONTENT component. Neither similarly
named folders nor identical chart filenames are required. The separate
observation-pair/subset profile and additional distinct chart sets are not admitted.

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File scripts/Phase6B-Smoke-Test.ps1
# Use only the owned synthetic evidence emitted by this smoke:
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase6 group-plan `
  --db <phase2.db> --content-session <id> --accept-content-manifest <sha256> `
  --component <sealed-key> --root <root-A> --root <root-B> --root <root-C> `
  --output <new-or-empty-directory-outside-protected-roots>
dotnet src/BmsLibraryAudit.Cli/bin/Release/net10.0/bms-audit.dll phase6 group-verify `
  --db <phase2.db> --content-session <id> --accept-content-manifest <sha256> `
  --component <sealed-key> --root <root-A> --root <root-B> --root <root-C> `
  --plan <group_union_plan.json>
```

`group_union_plan.json` includes full inventories, native/physical identities,
chart SHA identities and appearances, conflicts, every examined subset, all
maximal compatible groups, their full virtual namespaces and per-alternative
conservation ledgers. Each subset receives its own whole-namespace check, including
ancestors and Windows collisions; connected pairs do not establish a group.
For A with `kick.wav=X`, B without it and C with `kick.wav=Y`, the alternatives
are A+B and B+C. Neither is selected. Multiple maximal alternatives yield
`HUMAN_SELECTION_REQUIRED`; a single full union is a proposal, still unselected.

Every excluded observation root remains a complete variant: all charts, opaque
auxiliaries, resources, empty directories, original paths and evidence identities.
`_variantes/001` labels follow excluded sealed root-key order and may change if
the selected sources change. They convey no quality preference or destination
permission. Variants remain `PRESERVE_IN_PLACE` / `RELOCATION_NOT_PROVEN`.
Existing case-equivalent `_variantes` names block the suggested presentation with
`VARIANT_LAYOUT_NAMESPACE_CONFLICT`, without renaming or dropping originals.
Same path/type/bytes shares one virtual entry with all origins; distinct paths
remain distinct even for identical bytes. No original physical appearance is retired.

The separate verifier readmits CONTENT/PREFLIGHT/source seals and independently
reconstructs exhaustive maximality, source accounting, complete variants, labels,
states and counts. It does not call the planner; a recomputed plan digest alone
cannot admit fabricated results. Semantic obligations remain UNKNOWN per chart;
Phase 4 JSON inputs are not accepted. Dependencies, package boundaries, NTFS
preservation and relocation are not proved by a physically compatible plan.
No source payload is reopened, database written, main distribution selected,
directory consolidated, variant built, or staging invoked.

Exit 0: one valid physical proposal; 2: valid alternatives requiring human
selection; 1: invalid evidence/plan, exceeded budget or publication error; 130:
cancellation. Exit 2 is an inspection result, never execution authority.
The [Phase 6B contract](docs/PHASE_6B_GROUP_AND_VARIANT_PLANNER_IMPLEMENTATION.md)
defines its own version/digest, canonical order and bounds: 6 roots, 63 subsets,
20,000 source entries/conflicts, 100,000 virtual entries and 200,000 ledger rows
across alternatives, 1,280,000 examined appearances per reconstruction, 16 MiB
report, 128 MiB managed-memory growth and 60 seconds at cooperative checkpoints.
Limits can only be reduced; insufficient budgets refuse rather than truncate.
Existing whole-session validation precedes those consumer budgets. Report
publication reuses protected-root/input/native bindings and exclusive no-overwrite
publication. Source freshness, authenticity and universal race freedom remain
unproved. STOP for independent review; no real execution or Phase 8/9 is authorized.
