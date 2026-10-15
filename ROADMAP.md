# BmsLibraryAudit — Current Roadmap

**Planning revision:** 2026-10-15 (accepted bounded Phase 5; approved scope research and synthetic subset increment pending review).
**Status:** Original roadmap committed in `63f5be6fdfbe05caeef37b8c2783f11473b0599e`; the implementation-first strategy was published in `f3d6bfb028be026bb088f620ad76d76b3256c9c2`, Phase 2B in `08d2e1c9eeb1344330698e3de5d9cb5873cf0bc7`, Phase 3/Phase 2 opening correction in `f5c02dedf5920f169d000fc1f9ba962de22cc6bd`, Phase 3 corrective hardening in `e459e09fb703daec9a6ab6dfb597b23e322d7cf0`, the first Phase 4 increment with nested-RANDOM hardening in `45e2abfb6034cfadb70216e60ac4c49b3a98c385`, and semantic closure with sanitized compatibility data in `6af5f31ba08a77af7974796418bb1dc719847a95`. Final Phase 4 proof-interface hardening was published and independently reviewed in `8ae9fa94af48c3ae23b0d0bfa838f7e3d7381446`. The first bounded synthetic Phase 5 classifier was published in `e9d8e9e597cb95bf65e874ba9825efaebbf87a47`, with independent-audit hardening published in `8690aa53531dfbe6255103cc46a722b6e9f58f51`. Both increments were independently reviewed in their declared scopes: `PHASE_5_BOUNDED_SYNTHETIC_CLASSIFIER_ACCEPTED`. The separate strict-subset observer is now authorized for synthetic implementation under its frozen contract, pending independent acceptance; general overlap, universal package/dependency closure and real or destructive operations remain open or unauthorized.
**Historical planning baseline (2026-10-12):** `hinobara-ctrl/BmsLibraryAudit@7bd460503a51a63e19cf85003893f75dadd6bb54` (`main`).
**External comparison snapshot:** `Neeted/bemusicseeker-unofficial-fork@541db4fde12d1091ce0b7c939e11dd07d435b81c` (`dev`).

This roadmap records current planning priorities and implemented capabilities without changing historical findings, sealed evidence or authorization gates. Existing phase contracts remain authoritative for their scopes. [`MASTER_CONTEXT.md`](MASTER_CONTEXT.md) remains the historical architecture and requirement record; [`AGENTS.md`](AGENTS.md) remains repository policy.

## 1. Mission and end state

Build an **independent, conservative and auditable BMS/BMSON library analyzer and consolidator** for very large libraries. Its **final objective is to merge related song folders and remove genuinely redundant files**, using **our own precise and independently verified definition of redundancy**, without silently losing any distinct chart, audio variant, BGA, conditional dependency, auxiliary file, or playable chart-to-resource relationship.

The intended final product is **not** a general-purpose library manager or a BeMusicSeeker replacement. It needs no score management, IR, playlists, difficulty tables, media player, package marketplace, or general simulator synchronization UI. Its eventual mutating capabilities are nevertheless **in scope and required for the long-term goal**, subject to separate implementation and authorization gates. A useful read-only auditor and plan generator are intermediate deliverables, **not a redefinition of success**.

**Fundamental invariant:** *Heuristics discover. Exact evidence proves. Verified plans mutate.*

**Development principle:** *Implement what is sufficiently understood. Research what remains genuinely uncertain.* See [MASTER_CONTEXT section 41](MASTER_CONTEXT.md#41-current-development-strategy--2026-10-14). Implement resolved technical findings when their scope is authorized, with proportionate independent testing. Reserve research for genuine uncertainty, contradictory evidence or material security/preservation questions that block a concrete implementation decision. Reuse project mechanisms; progress is usable functionality, not additional planning stages or duplicate documents.

`RELATED != EQUIVALENT != SAFE_TO_MERGE != SAFE_TO_REMOVE`. A comparison label is not mutation authority. Unknown or conflicting evidence blocks destructive action; false negatives in cleanup are preferable to false-positive deletion.

### 1.1 Working definitions

- **Related:** folders share chart hashes, resource evidence, or other candidate-discovery signals. Transitive connectedness alone does not imply equivalence.
- **Exact content identity:** regular-file payload bytes have been freshly and successfully verified with strong cryptographic digests within an explicitly bounded observation session. Metadata (size/mtime) is not substitute proof. A digest is evidence tied to its observation, not a promise of current content.
- **Package equivalence:** an explicit, scoped claim about a distribution's tree, content, and/or playable dependencies. Different equality dimensions remain separate; equal chart sets or content multisets alone are insufficient.
- **Redundant file instance:** a particular physical instance whose removal has been shown, against an immutable and revalidated plan, to leave every required payload, supported chart-resource binding, file identity/path obligation, and explicitly retained auxiliary artifact preserved in the published result. Same bytes under two names **alone** does not prove removability.
- **Safe folder fusion:** a demonstrated relocation/union plan that preserves source chart bytes initially, unique physical content and relevant relative paths/resolution semantics, with no unresolved hard conflicts, and that can be staged and verified before publication.
- **Safe removal:** separately approved cleanup of **specific** proven redundant source instances after successful independent destination verification. No global delete inferred from a similarity score, a cluster, a matching filename, MD5 alone, timestamps, acoustic resemblance, or unobserved assumptions.
- **Blocked/unknown:** absence of evidence or an unsupported condition. It is never silently coerced to safe or equivalent.

The first mutation-capable release must avoid automatic rewriting of BMS/BMSON. If preserving colliding variants requires changes to chart references or bytes, mark `MANUAL_CONFLICT` / blocked, or design a later separately reviewed rewrite feature that preserves/explicitly accounts for historical chart identity impacts (scores, IR, replays). Preservation of raw bytes and preservation of playback semantics are both obligations.

### 1.2 Current usable milestone: exact folder-content comparison

Phase 2B delivers read-only comparison on owned synthetic fixtures: freshly hashed required files, exclusive entries, relative-path conflicts, same-content/different-name groups, multiplicity and explicit COMPLETE/INCOMPLETE or global STALE/FAILED/CANCELLED outcomes. `phase2 hash` requires a sealed preflight ID and its exact accepted manifest. Complete EXACT_ONLY components remain the only scope; wider related candidates require a separately reviewed contract. Phase 3 extracts declarations and potential usages from individual synthetic charts. Bounded Phase 4 now joins reconstructed syntax from included chart bytes to accepted CONTENT physical memberships and supports an explicit conditional possibility model. Content equality, observed bindings and qualified reachability do not replace dependency closure, fusion and cleanup proofs.

Implementation, verification and real-data access are separate permissions. Unresolved conditions block affected operations without making unsupported behavior safe or unnecessarily blocking unrelated authorized code work. Source Inspector native acceptance applies to its selected acquisition/verification workstream; B1–B6 are not automatically dependencies of every code change. Actual safe-discovery tooling, pilot provenance, independent verification, preservation/observation and real-access gates remain mandatory.

## 2. Verified starting point and boundaries

| Workstream | Status at this planning revision | Consequence |
| --- | --- | --- |
| Phase 1: chart index, SHA-256/MD5, relationships, clusters, reports | `VALIDATED` on a real library; independent .NET 10 SQLite/CLI | Retain implemented semantics and avoid a broad rewrite. Historical Scan 4 figures are reported evidence, not a new source inspection. |
| Phase 2A: complete `EXACT_ONLY` component preflight, direct-chart revalidation, metadata-only inventory, independent preflight DB/seal/reports | Implemented with synthetic Windows validation; real-library pilot **not run/authorized** | Continue through existing review and independent pilot gates. No non-chart byte identity established. |
| Independent offline source inspector | Offline closure `READY_FOR_INDEPENDENT_REVIEW`; native observer and source-file inspection obligations open | Do not claim production/native coverage or use offline success as permission to access real source data. |
| Phase 2B: accepted-manifest-bound payload hashing/comparison | Implemented with synthetic Windows validation; **no real-library execution authorized** | Explicit CONTENT command, fresh SHA-256 per required path, exact tree/multiset comparisons, immutable evidence and deterministic reports. New `PREFLIGHT_AND_CONTENT` / `phase2ab-content-1` profile; old profiles rejected unchanged without migration. Independent review remains required. |
| Phase 3 BMS/BMSON syntactic resource extractor | Implemented with synthetic validation; explicit per-chart inspector | Mandatory declared source root, checked report publication and conservative title_image preservation. Retain byte/decoding evidence, declarations, potential usages, branches and uncertainty. No physical bindings or dependency closure. |
| Phase 4 conservative resource resolution and semantic dependency evidence | Named synthetic scopes implemented; universal closure open | Sealed CONTENT/chart join, lexical candidates, declaration/usage intersections, coexistence conflicts, usage-domain coverage witnesses, qualified fixed-value and explicit-SKIP SWITCH models, BMSON audio-search proposals and conservation obligations. Unknown player behavior blocks broader claims; no necessity/unused/removal proof. |
| Phase 5 conservative observation classifier | First bounded synthetic increment accepted; e9d8e9e plus published/reviewed hardening 8690aa5. Separate explicit-subset observer in synthetic implementation, pending review | Existing classifier still admits EXACT_ONLY only; the new observer has a separate frozen profile and no Phase 4/5 adapter. General wider scope, package boundaries, universal closure and mutation authority remain open. |
| Immutable merge plans, staging, publishing and cleanup | Not implemented; no source mutation authorized | Keep as long-term mandatory objectives, explicitly distinct from any current permission. |

The present [`Phase 2 contract`](docs/PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md) intentionally admits **complete EXACT_ONLY components only**. Subsets, overlaps, resource-related candidates and container/family inference require a **subsequent separate scope contract**; do not retroactively extend an existing seal or reinterpret past evidence. The [`Scan 4 pilot readiness document`](docs/PHASE_2A_SCAN4_PILOT_OPERATIONAL_READINESS.md) is preparation, not pilot acceptance.

Historical `MASTER_CONTEXT.md` and phase documents retain their provenance and historical dates. This roadmap is the **current scheduling and research-priority view**, and does not modify their rules.

## 3. What the modern BeMusicSeeker fork changes — and what it does not

The **Neeted-maintained, actively developed reconstructed fork** must be evaluated as a new/current external implementation, not dismissed as covered by our historical brief reference to its URL. Assessment is pinned to the `dev` commit above. Its documentation and source provide **design comparisons**, not independent proof of production behavior or permission to copy code.

| Area | Modern Neeted capabilities observed in repository | BmsLibraryAudit decision |
| --- | --- | --- |
| Duplicate discovery | MD5-primary duplicate chart groups, Union-Find connectivity, sibling inclusion, index reuse, tested merging and duplicate cleanup | Do not recreate a GUI/manager; keep our stronger SHA-256-based independent relationship and evidence pipeline. Treat graph connectivity only as discovery. |
| Resource relationship estimates | Relative resource keys, reverse lookup, audio/image/video categories, `Health`, `Precision`, `Jaccard`, metadata tie-breaking and destination suggestions | Borrow **conceptual methods** for later discovery prioritization, never as proof of resource-byte identity or safe removal. |
| BMS/BMSON parsing | Detailed parser compatibility work, encodings, branch decisions, timing/LN/bmson tests | Use as an external behavioral comparison and test-case source (subject to provenance/licensing), but independently design an **all-possible-dependencies conservation parser**. A playback-oriented selected RANDOM branch does not prove full conditional coverage. |
| Library mutation | Session receipts, collision prechecks, temporary backups, local compensation, SQLite/index reconciliation | Study failure models, but preserve our explicit stage/verify/publish/separately-authorized cleanup architecture. No dependency on its manager. |
| Resource collision policy | `DecideComponentMove` uses source/destination size and modification time (2-second tolerance for `SkipSame`) rather than a universal strong content proof | **Not acceptable** as authority to discard a resource under our redundancy rules. Same path/size/time with different bytes is a mandatory adversarial test. |
| All branches/unique assets | No demonstrated universal closure over every `RANDOM/IF/SWITCH`, physical variant and auxiliary-file retention for arbitrary merges | Remains a core research obligation, not an assertion of defects in every installation case. |

The fork reports reconstructed/decompiled origins and a declared MIT scope. Review provenance and fixture licenses before any use beyond public conceptual comparison. **Implementation remains independent:** do not copy/port/vendor its implementation or turn it into a mandatory runtime dependency. Do not reuse third-party chart test data without checking permissions. Preserve the stricter private-source restrictions in `MASTER_CONTEXT.md` and `docs/SOURCES.md`.

**Avoid reinventing:** graphical library manager, playlists/IR/scores, full player, generic catalog services and broad destination-estimation framework. **Investigate further:** exact resource identity, conditional dependency closure, cross-folder ownership/resolution, irreducible conflicts, content-preserving fusion and independently verified cleanup.

## 4. Committed-to-goal phase progression (nine phases)

### Phase 1 — Independent chart discovery and relationships `[IMPLEMENTED/VALIDATED]`

Index opaque chart bytes, compute exact chart identities, establish folder chart sets, directed subsets, partial overlaps, connected components and deterministic reports. Retain existing semantics, tests and real-validation evidence. Do not reimplement because an external program provides similar features.

**Exit gate:** already recorded as `VALIDATED`; any future algorithm changes need an independently scoped regression review.

### Phase 2 — Exact filesystem/content evidence `[2A/2B IMPLEMENTED SYNTHETICALLY]`

**2A:** independent read-only Phase 1 source capture; fresh direct-chart verification; bounded metadata inventory of **complete EXACT_ONLY components**; native identity, path safety, sealed provenance and deterministic reports. Apply authorized, resolved source-capture corrections with synthetic regression. Native tool acceptance remains specific to the chosen discovery/verification route; it does not block unrelated safe implementation. Before real access, resolve applicable opening, provenance, preservation and observation requirements, then, **only with separate approval**, conduct the planned pilot, independent review and potentially a separately approved full preflight.

**2B:** implemented explicit accepted-manifest hashing of the complete selected physical-file observation surface. Join digest evidence to relative-path trees and multiplicity-preserving content multisets. Preserve unreadable, unstable, unsafe, incomplete and stale outcomes; never publish partial-pair equality for an incomplete component. Metadata-only equality is forbidden. Existing Phase 2 database opening now also validates rollback header magic/versions before each provider access; unsupported databases remain unmodified and no migration is performed.

**Exit gate:** accepted reproducible content evidence for the admitted scope, with independently validated digests, source unchanged, bounded resources and documented limitations. **Phase 2 itself produces no merge/delete authority.**

### Phase 3 — Independent BMS/BMSON dependency extraction `[SYNTACTIC EXTRACTOR IMPLEMENTED SYNTHETICALLY]`

The explicit inspector parses definitions, logical IDs, potential usages and authored paths separately from filesystem resolution. It retains original accepted bytes, strict decoding candidates, unexpanded authored strings and diagnostics. Implemented scope is BMS/BME/BML/PMS core resource syntax and BMSON 1.0.0 dependency fields. UTF-8/BOM and Windows CP932 are supported; differing valid text interpretations are both retained. Unknown extensions/versions, malformed structures and limits yield incomplete evidence. No universal simulator interpretation is claimed.

The implemented model is `Chart -> LogicalResource -> Declaration -> PotentialUsage`, with source positions, conditional syntax contexts and explicit uncertainty. WAV/BMP/timing namespaces remain distinct. Physical asset/file provenance joins, extension alternatives, reachability, required/unused labels and package classification are deferred to later phases.

**Current gate:** synthetic parser/inspection tests pass. Independent review and any future real-sample execution remain separate gates; no real data was accessed. Supported syntax is never dependency closure or deletion authority.

Corrective hardening protects the explicitly declared whole source root, including
sibling song folders; other roots are not autodetected. Reports are staged and
flushed before no-overwrite publication in their validated destination. BMSON
`title_image` is preserved with explicit IDL/prose uncertainty. These corrections
have synthetic regressions and confer no real-operation or Phase 4 authority.

### Phase 4 — Conditional reachability and exact resource resolution `[SYNTHETIC SCOPE CLOSED FOR REVIEW; UNIVERSAL CLOSURE OPEN]`

Implemented `phase4 resolve`: read-only compiled-profile and CONTENT/PREFLIGHT seal
verification, COMPLETE admitted component/root selection, included-byte SHA-256
binding, full parser reconstruction, scoped route/stem indexes and deterministic
JSON with physical multiplicity, syntactic usages, candidates and limitations.
All known source roots/evidence inputs are excluded from report destinations;
publication reuses the small shared Phase 3 helper. No source payload reopens,
schema change or evidence-DB writes occur. The named nested-random first-matching
branch model intersects symbolic predicates without combinatorial replay. Malformed,
unsupported controls and directly orphaned RANDOM content remain UNKNOWN; even
model-impossible declarations remain recorded. See README for exact caps/states.

The semantic extension adds joint declaration/usage predicates, usage-specific
coexistence and unknown precedence, and bounded interval-box subtraction to prove
coverage or expose unbound assignments. SETRANDOM uses an explicit fixed domain;
flat SWITCH/SETSWITCH admits CASE/DEF only with SKIP between branches, final DEF
and ENDSW. Fallthrough/nested SWITCH forms remain unsupported rather than modeled
as ordinary if/else. BMSON sound-channel compatible-name proposals remain distinct
from literal/BMS resolution, with codec usability unknown. JSON v2 retains v1
field meanings and adds a versioned graph, catalog, conservation summary and
coverage. The scoped observed-binding proof excludes playback, implicit resources,
codec/fallback decisions, portability and package closure. Global dependency closure
and removal authority remain false; no Phase 2 schema/seal or Phase 3 parser changes.

The named synthetic scope now includes an explicit non-vacuity gate: a positive
observed-binding proof requires at least one proven satisfiable usage, including
fixed controller domains. Empty/all-impossible usage graphs receive NOT_PROVEN;
impossible syntax, candidates and mathematical vacuous coverage remain retained.
Mixed reachable/impossible usages can still prove the bounded scope. Unknown flow
and exhausted relation/symbolic budgets cannot supply proof. Regression coverage
includes the complete sealed synthetic Phase 1–4 path and a sanitized v1 JSON
deserialization fixture, which is not valid sealed evidence. That semantic closure
was published in `6af5f31ba08a77af7974796418bb1dc719847a95`.

Final interface hardening separates admitted logical coverage from physical
path/binding coverage. Graph v2 requires at least one covered, unambiguous,
satisfiable physical relation and unique `LEXICAL_ORDINAL` candidates for all
authored physical paths. Windows-qualified matches retain their candidates and
hashes but cannot establish strict ordinal proof; timing-only and unused WAV
observations cannot substitute for an effective physical binding. Legacy fields
and JSON v1/v2 reading remain; the reinforced semantic/proof versions and additive
coverage objects are documented in README. This hardening was published and reviewed
in `8ae9fa94af48c3ae23b0d0bfa838f7e3d7381446`; no universal closure or operational authority follows.

The following remain completion/research goals, not current classifications:

Explore **all relevant possible valid** `RANDOM/SETRANDOM/IF/ELSEIF/ELSE/ENDIF`, `SWITCH/SETSWITCH/CASE/DEF/SKIP/ENDSW` semantics rather than selecting one playback branch. Prefer bounded symbolic/reachability modeling; if branch explosion or uncertain semantics prevents proof, mark `UNKNOWN/UNSUPPORTED`, retain all potentially relevant resources and block unsafe fusion. Investigate duplicate/conflicting declarations, nested conditionals, legacy malformed files, case sensitivity, relative/parent/external paths, inherited/shared resources and simulator-specific lookup behavior.

Future proven semantics may classify `REQUIRED_ALWAYS`, `REQUIRED_CONDITIONAL`, `DECLARED_UNUSED`, `PHYSICAL_UNREFERENCED`, `MISSING_RESOURCE`, `AMBIGUOUS_RESOURCE_BINDING`, `UNKNOWN`. The current bounded increment does not assert required/unused or absolute missing classifications. **Declared unused / unreferenced is not automatic deletion**, since package provenance and auxiliary files matter.

**Exit gate:** conservative dependency closure and reproducible physical resolution with coverage diagnostics, no speculative loss of branch-specific assets.

### Phase 5 — Package/family classification `[BOUNDED SYNTHETIC INCREMENT; WIDER SCOPE OPEN]`

The first functional increment adds `phase5 classify` over one accepted, complete
EXACT_ONLY component. It reuses sealed Phase 1 discovery and Phase 2B comparisons;
separate chart sets, recursive trees, multisets, exclusive entries, directional
subsets, complements and conflicts retain paths/native identities/multiplicity.
Optional Phase 4 inputs require current v2 semantic/proof reconstruction from
CONTENT-bound bytes; missing/unverified/unknown dependencies do not erase physical
proofs. Nested observation memberships remain scoped; container suggestions do not
establish packages. Reports are bounded, deterministic and safely published, with
all operational/equivalence flags false. No wider observation, fusion plan or
mutating executor is implemented. See README for CLI, budgets and synthetic smoke.

Published audit corrections preserve captured discovery diagnostics, separate declaration
and candidate/BMSON origin policies, and mark historical resolver build claims as
unverified independently of current semantic reconstruction. The bounded synthetic
increment is accepted at 8690aa5; broader Phase 5 scope and real-operation gates remain open.

Combine Phase 1 relations, complete Phase 2 content evidence, and Phases 3–4 dependency graph; distinguish `PACKAGE_TREE_EXACT`, `CONTENT_MULTISET_EXACT`, `CHART_EXACT_RESOURCE_VARIANT`, `CHART_SUBSET`, `SAFE_UNION_CANDIDATE`, `HARD_CONFLICT`, `MISSING_RESOURCE`, `CONTAINER_DIRECTORY`, `UNKNOWN`, etc., as **independent dimensions** and proof statuses, not a single blanket safety label. Integrate nested folder/package boundaries without treating a large collection container as one song.

The existing Phase 4 interface is consumed by the bounded increment, as documented
in README: keep physical provenance/multiplicity, lexical resolution, chart
semantics, non-vacuous bounded proof and operational authority independent.
Preserve physical identities, playback alternatives, auxiliary files, path
conflicts and every uncertainty/truncation. No removal list or recoverable-space
estimate is implemented. Remaining
acceptance includes client precedence, unsupported SWITCH/fallthrough, codecs and
search order, implicit dependencies, universal package closure, historical-evidence
freshness, real-library operations and separately verified destructive safety.

Expand beyond `EXACT_ONLY` into directed subsets, high overlap and resource-overlap **only via a new bounded/authorized observation and comparison contract**. External similarity/Jaccard suggestions may rank candidate work; they never certify equivalence.

The [approved observation-scope research](docs/PHASE_5_OBSERVATION_SCOPE_EXPANSION_RESEARCH.md)
was published in `e7bb15b75253834eda6321e46144abdfc238351e`. Its first explicit
strict-subset pair observer is now in synthetic implementation, pending independent
acceptance, under a [separate frozen contract](docs/PHASE_5_EXPLICIT_SUBSET_PAIR_OBSERVATION_CONTRACT.md).
It creates independent SOURCE/PREFLIGHT/CONTENT evidence for two disjoint roots;
the historical EXACT_ONLY profile and Phase 4/5 consumers remain unchanged.
General overlap, shared-surface batching, semantic adapters and real operation
remain outside this increment. Synthetic implementation is not independent acceptance.

**Exit gate:** independently explainable package reports for exact, subset, complement, variant and conflict cases, with explicit confidence/evidence completeness and no mutation.

### Phase 6 — Immutable, verifiable fusion and redundancy plans `[RESEARCH]`

Produce **concrete, dry-run** per-file plans, not merely ranked folder choices: target structure, each chart and resource dependency, copy/keep/reuse/rename/block actions, known byte identities, predicted reclaimed storage, non-chart auxiliary retention, source/destination mappings, conflict sets, external/parent reference obligations, atomic groups and an explicit removal-candidate list with one proof per instance. Include a plan digest, observation provenance, freshness prerequisites and deterministic human-readable/structured reports.

No resource rename/rewrite that would break chart reference resolution. For same-relative-path different-byte variants, propose namespaced layouts only where chart-relative resource resolution is **proven** preserved without rewriting; otherwise block and retain both originals. Never force same-folder fusion for an irreducible conflict. Acoustically equivalent/different-encoding files are *advisory similarities*, not byte-redundant.

**Exit gate:** human-reviewable, immutable plans that produce zero writes to source libraries and certify only decisions fully supported by their evidence. Include explicit `BLOCKED/NEEDS_REVIEW` outcomes.

### Phase 7 — Safe staging, independent verifier and fault testing `[FUTURE MUTATION CAPABILITY]`

Implement an isolated mutating executor **separate from analysis**, restricted to accepted plans and disposable/test copies first. Revalidate every source precondition immediately before each operation; detect TOCTOU and interrupted inputs; copy/assemble to a separate safe staging destination; independently hash/compare all expected bytes, path trees, chart references and required content. Test nested/parent paths, long Unicode paths, hardlinks, reparse points, cross-volume moves, insufficient disk space, partial reads, cancellation and mid-run failures.

**Exit gate:** reproducible stage receipts and strong proof the fully verified staged result preserves required charts, assets and playback-reference bindings. **No source cleanup permitted.**

### Phase 8 — Controlled publication of verified merged results `[FUTURE MUTATION CAPABILITY]`

Publish only approved, still-current, independently verified staged output. Keep originals available during publication; respect what cannot be atomic across volumes, multiple directories and SQLite. Use immutable change receipts, explicit post-commit verification and an honest partial-failure/recovery model. Any unexpected condition aborts unsafe successors without fabricating a rolled-back global transaction.

**Exit gate:** the final published content and dependencies are verified; no source deletion is implied by successful publication.

### Phase 9 — Explicit cleanup of proven redundant instances `[FINAL OBJECTIVE]`

After independent post-publication verification, prepare a **separate** per-instance removal manifest proving retained byte identity, sufficient surviving copies, dependency/reference safety, coverage of auxiliary files, complete publication and no stale facts. Confirm again immediately before cleanup. Prefer reversible quarantine/recycle or a recoverable retention period where practical; irreversible deletion requires its own explicit final approval. Unknown, conflicting, unreferenced-but-unclassified, unverified or unique data is retained. Receipt includes input identities, actual actions, bytes reclaimed, verification evidence, and recovery instructions/limits.

**Exit gate:** controlled real-library validation demonstrates folder fusion and actual removal of precisely authorized redundancies **without loss of distinct content or required playback dependencies**, including adversarial/failure cases. This is required to declare the **long-term project goal met**.

### Phase invariants

Every new phase requires: exact Git baseline and worktree status; reviewed scope and source/target boundaries; design/contract revisions explicitly authorized; deterministic tests and independent acceptance; relevant performance measurements; no source writes from analyzers; no silent migration of evidence; no automatic advancing of gates. Real-library reads/writes, mutation implementation, git staging/commit/push and subsequent phases require their own applicable approvals.

## 5. Concrete implementation needs and unresolved research questions

Implement established solutions under bounded authorization. The questions below justify additional research only where existing evidence cannot settle a concrete decision; they do not require another design phase for every functional increment.

| Priority | Research question | Smallest useful proof or experiment | Depends on |
| --- | --- | --- | --- |
| **P0 — immediate safety/review** | Can Phase 2A's source-opening boundary and future pilot be accepted for their exact scopes? | WAL-header refusal and synthetic regressions are implemented; obtain independent reviewer disposition and applicable native/provenance/preservation evidence before separately authorized real discovery/pilot. | Existing Phase 2A contracts and distinct verification/real-access authorizations. |
| **P1 — semantic scope review** | Which conditional logical references have unique observed declaration bindings? | Review Phase 4 intersections, interval coverage proofs/witnesses, coexistence ambiguity, named fixed/SWITCH subsets and BMSON proposals. Player precedence, fallthrough, codec usability, implicit resources and universal closure remain open. | Phase 2B/3/4 independent review; real execution and Phase 5 have their own gates. |
| **P2 — parallel offline research** | What are the **conservation** gaps of modern Neeted's parsers, resolver and collision policy? | Source-to-contract matrix at pinned commit; minimal self-created fixture suite: matching MD5, colliding resource name with same size/time but different bytes, conditional unused branch, resource-only folders. No source mutation outside disposable copies. | Public documentation/code only; provenance review. |
| **P3 — conditional semantics** | Which syntactically observed `RANDOM/IF/SWITCH` alternatives are reachable? | Bounded Phase 4 conditional analysis over Phase 3 evidence; preserve all possibilities when semantics remain uncertain. | Tested Phase 3 syntax model; no real-library reads without approval. |
| **P4 — resolver design** | When can two physical files with different paths substitute without breaking references? | File/path/encoding/simulator compatibility matrix; independent fixtures preserving chart bytes; include nested roots, external references and case collisions. | Phases 3–4. |
| **P5 — classification** | How are partial pack overlaps, audio variants, complementary resources and giant container folders represented without false fusion? | Conflict-aware package graph with explicit memberships and non-transitive compatibility tests. | Phases 2B–4. |
| **P6 — mutation safety** | Can staging/publication/recovery and cleanup remain correct under failures, stale inputs and cross-volume conditions? | Fault-injected disposable-source scenarios and verified per-instance removal proof. | Phases 6–9, explicit approvals. |

**Deferred advisory research, not authority for deleting bytes:** semantic chart fingerprints, perceptual audio fingerprints, player-specific rendering/audio equivalence, archives unpacking, simulator database integration, and Backbeat interoperability. Reconsider only when a concrete unresolved conservation case justifies them.

## 6. Adversarial acceptance corpus

Minimum synthetic examples (build independently and extend over time):

1. Identical complete folders, different root names; equality does not confuse physical instances.
2. Identical chart bytes but different bytes for `01.wav`, including **identical file size and mtime**: `RESOURCE_VARIANT` or hard conflict, not `SkipSame` removal.
3. Same resource bytes under different relative names; prove chart-relative bindings before deleting a path.
4. A chart-set subset of B with a BGA/audio variant in A: no naïve A deletion.
5. Complementary packs containing different charts and non-overlapping required audio: preserve union and all bindings.
6. Duplicate charts in transitive clusters A–B–C with different per-pair conflicts: no transitive safe-merge conclusion.
7. Nested `RANDOM/IF/SWITCH`, alternate declarations, unknown directives and deliberate branch explosion: retain all possible resources or block.
8. CP932/UTF-8/BOM and invalid or ambiguous byte sequences and same path with different Unicode/case spellings.
9. Parent/external-relative paths, shared resource directories, nested observation roots and opaque auxiliary/readme/archive files.
10. Missing/unreadable/unstable resources, changed bytes with unchanged size/mtime, stale database and mixed healthy/unhealthy components.
11. Same-path file-versus-directory collisions, hardlinks/reparse points, long paths and source/destination aliasing.
12. Mid-stage, mid-publish, after-commit and mid-cleanup faults; wrong-volume, no-space, interrupted writes and irreversible-loss avoidance.
13. Reports that independently account for original chart bytes, unique resource digests, path/ref mappings, all blocked files and actual reclaimable bytes.

Tests of Neeted on disposable fixtures can be an **external comparator**, but should not use its output as the oracle. Independent expected outcomes derive from our preservation contract.

## 7. Near-term execution sequence and decision rules

1. **Implement resolved findings when authorized:** prefer small, usable corrections and functional increments with proportionate regression, preserving contracts and historical evidence. Current strategy documentation is not operational or next-phase authorization.
2. **Phase 2A safety and review:** the WAL pre-open correction is implemented; independently review it and resolve appropriate acquisition/provenance/preservation evidence for the operation actually proposed. Native Source Inspector gates remain in their applicable workstream. Do not promote READY_FOR_REVIEW to accepted or weaken real-pilot prerequisites.
3. **Real pilot, when specifically approved:** current Phase 1 source/report discovery; freeze accepted EXACT_ONLY component closures; separately approve pilot, receipt verification and any full 2A run. No speculative real-source commands in this roadmap.
4. **Phase 2B independent review:** review the implemented synthetic CONTENT flow, schema refusal, complete physical coverage, comparisons, seals and deterministic reports. Sequential reads are bounded; pair views use scope-sized memory, and no large-scale acceptance is claimed. Real execution and broader candidate scope require their own gates; pilot success grants no automatic 2B permission.
5. **Parallel research, not parallel permission:** investigate modern Neeted gaps and independent BMS/BMSON dependency semantics using public code and **synthetic** fixtures, documenting evidence and uncertainty without modifying the source library.
6. **Review Phase 3 and continue Phase 4 through Phase 9** through distinct gates; success is **verified fusion and cleanup**, not merely producing reports.

**No authorization is implied** to stage/commit/push this document, access real source databases/folders, run a real Phase 2/3/4 pilot, implement Phase 5, modify a source library, or execute consolidation. The explicit Phase 2/3/4 implementation authorizations apply only to development and synthetic validation. Present operational gates remain in force.

## 8. Traceable reference documents

### Own project

- [`MASTER_CONTEXT.md`](MASTER_CONTEXT.md): historical mission, invariants, legacy architecture, and initial Phase 1–9 progression.
- [`docs/PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md`](docs/PHASE_2_READ_ONLY_EXACT_CANDIDATE_FILESYSTEM_EVIDENCE_CONTRACT.md): governing Phase 2 observation, digest and comparison contract.
- [`docs/PHASE_2A_IMPLEMENTATION_REPORT.md`](docs/PHASE_2A_IMPLEMENTATION_REPORT.md): implemented synthetic preflight scope and limitations.
- [`docs/PHASE_2A_SCAN4_PILOT_OPERATIONAL_READINESS.md`](docs/PHASE_2A_SCAN4_PILOT_OPERATIONAL_READINESS.md): non-authorized real-library pilot preparation and gates.
- [`docs/PHASE_2A_REAL_LIBRARY_VALIDATION_PLAN.md`](docs/PHASE_2A_REAL_LIBRARY_VALIDATION_PLAN.md): proposed human acceptance criteria.
- [`docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CLOSURE.md`](docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CLOSURE.md): native requirements still unresolved.
- [`docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md`](docs/BACKBEAT_INTEROPERABILITY_RESEARCH_NOTE.md): deferred completeness/interoperability research, not integration authority.
- [`AGENTS.md`](AGENTS.md) and [`docs/SOURCES.md`](docs/SOURCES.md): implementation policy and provenance.

### Modern Neeted research snapshot (not a dependency or mutation oracle)

- [Fork README](https://github.com/Neeted/bemusicseeker-unofficial-fork/blob/541db4fde12d1091ce0b7c939e11dd07d435b81c/README.md) — reconstructed/decompiled provenance and branch model.
- [Duplicate maintenance specification](https://github.com/Neeted/bemusicseeker-unofficial-fork/blob/541db4fde12d1091ce0b7c939e11dd07d435b81c/devdocs/spec/library/duplicate-files.md) — primary MD5, connected groups, merge behavior.
- [Resource destination estimation](https://github.com/Neeted/bemusicseeker-unofficial-fork/blob/541db4fde12d1091ce0b7c939e11dd07d435b81c/devdocs/spec/library/install-estimation.md) — relative keys and heuristic resource matching.
- [Parser compatibility](https://github.com/Neeted/bemusicseeker-unofficial-fork/blob/541db4fde12d1091ce0b7c939e11dd07d435b81c/devdocs/spec/library/parser-compatibility.md) — playback-oriented branch/encoding rules.
- [Resource move policy](https://github.com/Neeted/bemusicseeker-unofficial-fork/blob/541db4fde12d1091ce0b7c939e11dd07d435b81c/BeMusicSeeker/Models/BmsLibraryInternal/Install/BmsLibraryPackageInstallService.cs) — `DecideComponentMove`, metadata-based `SkipSame`.
- [File/database consistency](https://github.com/Neeted/bemusicseeker-unofficial-fork/blob/541db4fde12d1091ce0b7c939e11dd07d435b81c/devdocs/spec/library/file-db-consistency.md) — explicit mutation/recovery limits.

**Revision principle:** research may revise the *future schedule* after review; it must not silently rewrite source evidence, change the meaning of established phase labels or weaken the proof required before removing data.
