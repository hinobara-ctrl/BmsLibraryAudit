# BmsLibraryAudit — Current Roadmap

**Planning revision:** 2026-10-12
**Status:** Proposed documentation revision; requires repository review and an authorized commit.
**Project baseline inspected:** `hinobara-ctrl/BmsLibraryAudit@7bd460503a51a63e19cf85003893f75dadd6bb54` (`main`).
**External comparison snapshot:** `Neeted/bemusicseeker-unofficial-fork@541db4fde12d1091ce0b7c939e11dd07d435b81c` (`dev`).

This roadmap updates *planning priorities*, **not** the implementation status, historical findings, sealed evidence, approved scope, or authorization gates. Existing phase contracts remain authoritative for their scopes. [`MASTER_CONTEXT.md`](MASTER_CONTEXT.md) remains the historical architecture and requirement record; [`AGENTS.md`](AGENTS.md) remains repository policy.

## 1. Mission and end state

Build an **independent, conservative and auditable BMS/BMSON library analyzer and consolidator** for very large libraries. Its **final objective is to merge related song folders and remove genuinely redundant files**, using **our own precise and independently verified definition of redundancy**, without silently losing any distinct chart, audio variant, BGA, conditional dependency, auxiliary file, or playable chart-to-resource relationship.

The intended final product is **not** a general-purpose library manager or a BeMusicSeeker replacement. It needs no score management, IR, playlists, difficulty tables, media player, package marketplace, or general simulator synchronization UI. Its eventual mutating capabilities are nevertheless **in scope and required for the long-term goal**, subject to separate implementation and authorization gates. A useful read-only auditor and plan generator are intermediate deliverables, **not a redefinition of success**.

**Fundamental invariant:** *Heuristics discover. Exact evidence proves. Verified plans mutate.*

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

## 2. Verified starting point and boundaries

| Workstream | Status at this planning revision | Consequence |
| --- | --- | --- |
| Phase 1: chart index, SHA-256/MD5, relationships, clusters, reports | `VALIDATED` on a real library; independent .NET 10 SQLite/CLI | Retain implemented semantics and avoid a broad rewrite. Historical Scan 4 figures are reported evidence, not a new source inspection. |
| Phase 2A: complete `EXACT_ONLY` component preflight, direct-chart revalidation, metadata-only inventory, independent preflight DB/seal/reports | Implemented with synthetic Windows validation; real-library pilot **not run/authorized** | Continue through existing review and independent pilot gates. No non-chart byte identity established. |
| Independent offline source inspector | Offline closure `READY_FOR_INDEPENDENT_REVIEW`; native observer and source-file inspection obligations open | Do not claim production/native coverage or use offline success as permission to access real source data. |
| Phase 2B: accepted-manifest-bound payload hashing/comparison | **UNIMPLEMENTED; NOT AUTHORIZED** | Implement only after contract/schema review and separate authorization; no code shortcut from Phase 2A into CONTENT. |
| BMS/BMSON dependency parser, conditional reachability, package classifier | Research/planning only | New implementations need dedicated design, tests and gates. |
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

### Phase 2 — Exact filesystem/content evidence `[2A IMPLEMENTED SYNTHETICALLY; 2B PENDING]`

**2A:** independent read-only Phase 1 source capture; fresh direct-chart verification; bounded metadata inventory of **complete EXACT_ONLY components**; native identity, path safety, sealed provenance and deterministic reports. Next: resolve independent-source-inspection/native blockers, review current source/report prerequisites and, **only with separate approval**, conduct the planned real-library pilot, independent review and potentially a separately approved full preflight.

**2B:** after accepted 2A evidence and its own design/authorization gate, freshly hash the complete selected physical-file observation surface. Join digest evidence to relative-path trees and multiplicity-preserving content multisets. Preserve all `UNKNOWN`, `UNSTABLE`, unreadable, missing, unsafe, incomplete and stale outcomes; never publish partial-pair equality for an incomplete component. Metadata-only equality is forbidden.

**Exit gate:** accepted reproducible content evidence for the admitted scope, with independently validated digests, source unchanged, bounded resources and documented limitations. **Phase 2 itself produces no merge/delete authority.**

### Phase 3 — Independent BMS/BMSON dependency extraction `[RESEARCH]`

Parse definitions, logical IDs, usages and authored paths separately from filesystem resolution; preserve original bytes, text encoding, unexpanded authored strings and diagnostics. Support BMS/BME/BML/PMS plus BMSON versions with appropriate format-specific semantics. Evaluate CP932/Shift-JIS, UTF-8/BOM and observed historical edge cases; do not assume one universal simulator interpretation.

Model `Chart -> LogicalResource -> CandidatePhysicalAsset -> PhysicalFile` with exact identity/provenance joins and explicit unsupported/ambiguous states. Include `#WAV`, `#BMP`/BGA, preview/banner/stagefile and supported extension aliases, with resource categories distinguished.

**Exit gate:** independently tested parser on adversarial synthetic fixtures and authorized real samples; errors/unknown directives fail closed. Do not treat 'parser succeeded' as completeness of conditional references.

### Phase 4 — Conditional reachability and exact resource resolution `[RESEARCH]`

Explore **all relevant possible valid** `RANDOM/SETRANDOM/IF/ELSEIF/ELSE/ENDIF`, `SWITCH/SETSWITCH/CASE/DEF/SKIP/ENDSW` semantics rather than selecting one playback branch. Prefer bounded symbolic/reachability modeling; if branch explosion or uncertain semantics prevents proof, mark `UNKNOWN/UNSUPPORTED`, retain all potentially relevant resources and block unsafe fusion. Investigate duplicate/conflicting declarations, nested conditionals, legacy malformed files, case sensitivity, relative/parent/external paths, inherited/shared resources and simulator-specific lookup behavior.

Classify `REQUIRED_ALWAYS`, `REQUIRED_CONDITIONAL`, `DECLARED_UNUSED`, `PHYSICAL_UNREFERENCED`, `MISSING_RESOURCE`, `AMBIGUOUS_RESOURCE_BINDING`, `UNKNOWN`. **Declared unused / unreferenced is not automatic deletion**, since package provenance and auxiliary files matter.

**Exit gate:** conservative dependency closure and reproducible physical resolution with coverage diagnostics, no speculative loss of branch-specific assets.

### Phase 5 — Full package/family classification and candidate-scope expansion `[RESEARCH]`

Combine Phase 1 relations, complete Phase 2 content evidence, and Phases 3–4 dependency graph; distinguish `PACKAGE_TREE_EXACT`, `CONTENT_MULTISET_EXACT`, `CHART_EXACT_RESOURCE_VARIANT`, `CHART_SUBSET`, `SAFE_UNION_CANDIDATE`, `HARD_CONFLICT`, `MISSING_RESOURCE`, `CONTAINER_DIRECTORY`, `UNKNOWN`, etc., as **independent dimensions** and proof statuses, not a single blanket safety label. Integrate nested folder/package boundaries without treating a large collection container as one song.

Expand beyond `EXACT_ONLY` into directed subsets, high overlap and resource-overlap **only via a new bounded/authorized observation and comparison contract**. External similarity/Jaccard suggestions may rank candidate work; they never certify equivalence.

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

## 5. Research questions to prioritize (instead of general library-management development)

| Priority | Research question | Smallest useful proof or experiment | Depends on |
| --- | --- | --- | --- |
| **P0 — immediate operational** | Can Phase 2A and its native source-reader boundary be independently accepted and its real pilot safely scoped? | Close explicitly documented native/SQLite/WAL observation gaps; reviewer disposition; authorized four-component pilot and independently reproduced receipts if approved. | Existing Phase 2A contracts and explicit authorizations. |
| **P1 — immediate design** | How do we hash all required candidate resources efficiently **without** hashing a terabyte blindly or losing multiplicity? | Independent Phase 2B schema/protocol design, synthetic same-name/different-byte and hardlink/nesting tests, bounded I/O and complete component seals. | Valid 2A preflight plus 2B gate. |
| **P2 — parallel offline research** | What are the **conservation** gaps of modern Neeted's parsers, resolver and collision policy? | Source-to-contract matrix at pinned commit; minimal self-created fixture suite: matching MD5, colliding resource name with same size/time but different bytes, conditional unused branch, resource-only folders. No source mutation outside disposable copies. | Public documentation/code only; provenance review. |
| **P3 — parser design** | How to enumerate potential resources from malformed and nested `RANDOM/IF/SWITCH` safely? | Formal conditional-dependency model plus independent golden fixtures and bounded branch analysis. | Phase 3 design; no real-library reads without approval. |
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

1. **Documentation only (this revision):** review and accept current roadmap; archive historical planning by reference. Do **not** change Phase 1/2A contracts, historical measured outcomes or test claims. This is not operational authorization.
2. **Existing Phase 2A review:** resolve source-inspector native blocker, historical WAL-opening concern and appropriate provenance/preservation oracle, each by its own approved workstream. Do not assert prior work is accepted when the reports say 'ready for independent review'.
3. **Real pilot, when specifically approved:** current Phase 1 source/report discovery; freeze accepted EXACT_ONLY component closures; separately approve pilot, receipt verification and any full 2A run. No speculative real-source commands in this roadmap.
4. **Phase 2B bounded design and authorization:** write and review its precise content evidence contract/schema/tests and perform synthetic implementation only in a separately authorized task. Pilot success is not an automatic 2B permission.
5. **Parallel research, not parallel permission:** investigate modern Neeted gaps and independent BMS/BMSON dependency semantics using public code and **synthetic** fixtures, documenting evidence and uncertainty without modifying the source library.
6. **Continue Phase 3 through Phase 9** through distinct reviewed gates; success is **verified fusion and cleanup**, not merely producing reports.

**No authorization is implied** to stage/commit/push this document, access real source databases/folders, run the Phase 2A pilot, implement Phase 2B, modify a source library, or execute consolidation. The prohibition in present operational contracts stays in force until a later phase explicitly authorizes and implements those actions.

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
