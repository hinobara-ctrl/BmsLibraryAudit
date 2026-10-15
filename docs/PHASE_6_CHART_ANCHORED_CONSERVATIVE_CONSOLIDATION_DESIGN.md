# Phase 6 — Chart-anchored conservative consolidation design

**Date:** 2026-10-15. **Status:** proposed design for independent review; not a
frozen implementation contract. **Inspected baseline:**
`84f1fc640d177920aed9b2ddbff8c8c0b2c08781` (`main` and local `origin/main`).
This intervention authorizes documentation and code inspection only. No Phase 6
planner, CLI, executor or new evidence profile is implemented or authorized here.

**Later implementation record (2026-10-15):** the owner separately authorized
the first synthetic 6A increment from design baseline `e8db0bae8e5e33e646d4de6c81ebe3cdedd91a72`.
See its [bounded contract and status](PHASE_6A_VIRTUAL_UNION_IMPLEMENTATION.md).
The design below retains its original scope and future matrix; only the two-root
physical consumer is implemented pending independent review. Phase 4 inputs are
not enabled; per-appearance dependency obligations remain UNKNOWN. No 6B–9 authority follows.

## 1. Purpose and owner decisions

The required end product remains conservative consolidation of related BMS/BMSON
distributions, preservation of all charts and variants, and eventual separately
verified retirement of genuinely redundant instances. Discovery and reports are
intermediate capabilities. The shortest proposed next increment is a read-only
virtual union of **two** verified observation roots, followed by separate gates
for groups, staging, publication and cleanup.

The owner has selected this durable sequence:

**Chart-anchored preservation → verified physical comparison → conflict-free union planning → intact variant preservation → independent staging verification → controlled publication → separately authorized cleanup.**

Agreed product decisions are: preserve chart bytes; retain every physical
appearance and context; conserve all inventory, including opaque auxiliaries;
never prefer a conflicting distribution automatically; preserve conflicting
distributions intact; require demonstrated relocation preservation or retain them
in place. These decisions guide future implementation but do not grant execution
permission. `RELATED != EQUIVALENT != SAFE_TO_MERGE != SAFE_TO_REMOVE` and
`UNKNOWN/BLOCKED` never means safe.

## 2. Verified code and reuse matrix

The following are **code facts at the inspected baseline**. Trust describes a
bounded historical observation, not current source freshness, producer
authentication or permission to mutate. Method names refer to existing code;
consumers and gaps in the final columns are **proposals/inferences**, not APIs
already available. All source links are repository-local.

| Need | Existing evidence | Producer file / method | Trust and limit | Real gap | Proposed consumer |
| --- | --- | --- | --- | --- | --- |
| Find related charts | Direct-parent distinct SHA sets, path counts, exclusions and edges | [RelationshipAnalyzer.cs](../src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs), `Analyze`, `Classify` | Captured discovery; cached SHA may be historical. `PhysicalChartFiles` counts appearances; `Hashes.Count` counts distinct content | No relocation or package proof | Candidate selection only; CONTENT identities take precedence |
| Admit physical evidence | Exact compiled schema/profile, integrity, completed CONTENT and parent/source seals, component closure and outcomes | [PhaseTwoDatabase.cs](../src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs), `Open`, `Validate`; [PhaseTwoManifest.cs](../src/BmsLibraryAudit.Core/PhaseTwoManifest.cs), `VerifyContent`; [PhaseTwoContentRunner.cs](../src/BmsLibraryAudit.Core/PhaseTwoContentRunner.cs), `ValidateEvidence`, `ValidateScope`, `Parent` | Accepted legacy COMPLETE ADMITTED EXACT_ONLY; independent comparison reconstruction; no simultaneous snapshot guarantee | Planner-specific root selection and admission receipt | Two-root plan admission through existing mechanisms, read-only |
| Preserve entries and appearances | Root/component memberships, original/relative paths, entry/physical/native IDs, SHA, metadata and chart role | [PhaseFiveClassifier.cs](../src/BmsLibraryAudit.Core/PhaseFiveClassifier.cs), `Entries`, `ClassifyCore`; [PhaseFiveModels.cs](../src/BmsLibraryAudit.Core/PhaseFiveModels.cs), `PhaseFiveEntry`, `PhaseFiveRoot` | Reconstructed admitted CONTENT; path names and native objects remain distinct | Target-to-all-source mapping and per-appearance obligations | Plan inventory; reuse fields without treating unsigned classification JSON as evidence |
| Compare trees and content | Full/non-chart equality, per-path differences, empty directories, digest multiplicity | [PhaseTwoContentComparison.cs](../src/BmsLibraryAudit.Core/PhaseTwoContentComparison.cs), `View`, `Derive`, `Verify` | Exact ordinal historical views, SUCCESS regular-file hashes; counts and drill-down rederived | Virtual destination representation and complete accounting | Consume this kernel and verified dimensions; no third comparator |
| Retain independent dimensions | Chart sets/copies, subsets, exclusives, complements, conflicts and package UNKNOWN | [PhaseFiveClassification.cs](../src/BmsLibraryAudit.Core/PhaseFiveClassification.cs), `Pair`, `ReferenceIndex`; `PhaseFiveClassifier.ClassifyCore`, `Discover` | Physical proofs survive semantic UNKNOWN and unrelated discovery diagnostics; limits remain explicit | A union decision is not currently produced | Supporting dimensions, not a blanket safety label |
| Annotate physical conflicts | Same-path byte/type conflicts, case collisions, inconsistent native aliases, root-qualified IDs | [PhysicalConflictAnnotations.cs](../src/BmsLibraryAudit.Core/PhysicalConflictAnnotations.cs), `Derive` | Physical annotation only; no semantic/package admission | Destination-wide obligations and decision reasons | Reuse annotations; block affected target decisions |
| Reconstruct bounded dependency claims | Included chart bytes, parser reconstruction, CONTENT/root/chart identities, current graph/proof v2 | [PhaseFiveAdmission.cs](../src/BmsLibraryAudit.Core/PhaseFiveAdmission.cs), `Verify`; [PhaseFourResolver.cs](../src/BmsLibraryAudit.Core/PhaseFourResolver.cs), `ResolveCore`, `AnalyzeVerified` | `phase4-dependency-2`, `phase4-semantic-graph-2`, `observed-logical-physical-ordinal-binding-evidence-2`; historical resolver build remains declared/unverified | No proof of arbitrary relocation, global dependencies or player discovery | Per-appearance admitted semantic view; reject bare JSON PROVEN claims |
| Preserve semantic obligations | Declarations/usages/conditions, candidates, coexistences, BMSON proposals, logical and physical coverage, bounded proof/blockers | [PhaseFourSemanticModels.cs](../src/BmsLibraryAudit.Core/PhaseFourSemanticModels.cs), `PhaseFourSemanticAnalysis`, `ConservationObligation`; `PhaseFourResolver.AnalyzeVerified` | Named bounded models; Windows-qualified matches are not strict ordinal proof. Legacy `PhysicalBindingCoverage` alone is insufficient | Relocation-specific before/after obligation mapping | Conservation ledger with all uncertainty retained |
| Explain parent/external paths | Authored paths retained; lexical normalization inside selected root, outside-root refusal, alternate-extension proposals | [PhaseFourPaths.cs](../src/BmsLibraryAudit.Core/PhaseFourPaths.cs), `Resolve`, `BmsonAudioSearch` | Parent steps within root may resolve; escaping root is OUT_OF_SCOPE. No external resource opening, codec/order proof | External/shared context and relocation proof | Explicit unresolved obligation; block affected relocation |
| Understand broader physical scope | Separate strict-subset SOURCE/PREFLIGHT/CONTENT, recursive charts, all regular files, budgets/outcomes | [ObservationPairObserver.cs](../src/BmsLibraryAudit.Core/ObservationPairObserver.cs), `CaptureCore`, `PreflightCore`, `HashCore`; [ObservationPairModels.cs](../src/BmsLibraryAudit.Core/ObservationPairModels.cs) | Bounded synthetic transport; semantics NOT_EVALUATED | No Phase 4/5 adapter; not legacy evidence | Future separate admission increment, excluded from first 6A profile |
| Verify subset transport | Reproduced selection, seals, memberships, read outcomes, counts/flags/groups/conflicts | [ObservationPairVerification.cs](../src/BmsLibraryAudit.Core/ObservationPairVerification.cs), `LoadCore`, `ValidatePreflight`, `ValidateContent`, `Compare`, `Verify` | Accepted historical consistency, no payload reread during verification; both profiles reject each other | No union planner or semantic bridge | Reuse later only after separately authorized adapter; never translate/reseal historical evidence |
| Protect paths and evidence | Absolute/native resolution, Windows identities, reparse and case-sensitive-directory refusal, sidecar and alias exclusions | [PathPolicy.cs](../src/BmsLibraryAudit.Core/PathPolicy.cs), `Absolute`, `Contains`, `RejectReparseAncestors`; [PhaseTwoSafety.cs](../src/BmsLibraryAudit.Core/PhaseTwoSafety.cs), `Inspect`, `Resolve`, `RejectSidecars`, `ValidateDestinations` | Native Windows checks; not universal race freedom or current byte freshness | Future planner destination policy composition | Guard requested report locations and protected inputs; do not probe resources to fill gaps |
| Publish reports safely | Protected-root/input bindings, exclusive owned temporary, flush and no-overwrite rename | `PhaseFourResolver.Destination.Validate`; [ObservedReportPublication.cs](../src/BmsLibraryAudit.Core/ObservedReportPublication.cs), `Publish` | Output reports only; lost binding/crash may leave temporary; not a consolidation executor | New deterministic plan/report payload and independent plan validator | Reuse publication, never repurpose it as source copying machinery |

**Inference:** legacy `PREFLIGHT_AND_CONTENT` / `phase2ab-content-1` is the smallest
viable first profile: it already has full physical comparisons and the verified
Phase 4/5 path. The separate `explicit-subset-pair-1` / `observation-pair-1`
observer is useful implemented evidence, but admitting it now would add a second
transport and semantic-adapter work. First 6A must explicitly refuse it, without
widening legacy EXACT_ONLY or modifying either contract.

Existing database schemas, canonical encodings, seals, Phase 1 discovery, Phase
2–5 algorithms, contracts, authority indicators and historical reports remain
unchanged. No global hash index, new hashing engine, solver or external research
is needed for the proposed two-root consumer. Any later small internal admission
extraction must be separately authorized and preserve existing behavior; this
design neither performs nor requires a broad refactor.

**Owner-supplied review status, separate from code facts:** publication and
independent acceptance of the bounded synthetic subset observer and diagnostic
hardening at the inspected baseline are recorded from this task's instruction.
Code inspection verifies the implemented boundary; it does not independently
reproduce that review or make this proposed design accepted.

## 3. Chart identity, appearances and conservation

Logical chart identity is its admitted content SHA-256. An appearance is scoped
by evidence profile, CONTENT digest/session, component, observation root and
entry/physical IDs; it also retains original and relative paths, native identity,
size/stamps, observed digest and captured Phase 1 correspondence. A native object
can have several hardlink names; one entry can belong to nested root views.
Neither is silently counted as a new logical chart or discarded as a duplicate.

For each appearance retain its chart parent and observation/library context,
verified declarations/usages/conditions, all possible physical candidates and
their resolution policies/origins, report admission, separate physical/logical
coverage, truncations, external/shared references and unproved relocation
preconditions. Sharing syntax for identical **verified bytes and parser
version/limits/extension interpretation** is a possible later optimization;
bindings must always be reconstructed for the individual appearance/root.
There is no new syntax cache in this proposal's first consumer.

The conceptual flow is chart identity → appearances → known/potential/unknown
dependencies → complete inventory context → conservation ledger → physical
comparison → virtual union decisions. This order organizes obligations; it never
filters inventory by parser recognition. Every regular file, directory/empty
directory, chart appearance, opaque extension, archive, readme and unreferenced
auxiliary remains accounted for. Unsupported entry kinds or incomplete reads
block affected admission rather than disappearing from the plan.

Known means admitted evidence within its stated model, not universal necessity.
Potential declarations, alternatives, model-impossible syntax, uncertain codecs,
unused-looking declarations, missing matches and unrecognized dependencies remain
preserved. A missing Phase 4 report leaves semantic obligations UNKNOWN but does
not erase a complete physical comparison. No `BoundedProof` establishes package
closure, relocation safety, resource dispensability or simulator behavior.

## 4. Observation roots, distributions and package boundaries

Library roots, observation roots, chart parents, nested directories and owner-
identified distribution candidates are different scopes. CONTENT proves the
observed view; current code does not prove a complete distribution boundary.
Retain `PACKAGE_BOUNDARY_UNKNOWN` unless independent sufficient evidence is
explicitly admitted in a future scope. Folder names, chart count, depth and WAV
presence do not establish ownership. A container holding multiple songs must not
be promoted automatically to one song/package.

First 6A should select exactly two distinct, non-overlapping roots in the same
admitted COMPLETE legacy component. Refusing nested/aliased root selections is a
**proposed planner restriction**, not a change to the legacy profile, which can
represent nested memberships. Such evidence remains intact for later scope work.
A plan may describe a complete **observation union** while package boundaries
remain unknown; it cannot claim complete-package preservation or operational
eligibility from that fact. Different direct chart sets are outside the first
EXACT_ONLY profile even when their resources match.

## 5. Physical union decision matrix

These are proposed per-entry rules over admitted complete views. A target entry
is virtual, with no destination created. All contributing source occurrences
remain in its provenance, even when a future target needs only one copy.

| Evidence | Proposed virtual representation / state | Preservation and gate |
| --- | --- | --- |
| Same relative path, REGULAR_FILE, same SHA-256 | One target entry linked to every source occurrence | No original is retired; chart contexts remain distinct obligations |
| Same relative directory | One directory entry with all provenance | Preserve empty directories and structure |
| Exclusive file/directory | Retain at the same relative path | Includes unknown and auxiliary content |
| Same relative file path, different SHA-256 | `UNION_BLOCKED_CONFLICT` | Both originals and conflicting digests/IDs retained; no overwrite |
| File versus directory | `UNION_BLOCKED_CONFLICT` | Retain both structures; include ancestor/path-kind collisions |
| Same SHA-256 at different paths | Retain both target paths | No name substitution or hardlink deduplication |
| Same chart SHA-256 at different locations | Retain appearances and paths | No chart removal without separately proven substitution |
| Case collision or inconsistent native alias | Conflict annotation; no positive union decision | Retain authored spelling/native identity; unsupported alias interpretation blocks |
| Missing hash, unsupported entry, incomplete identity/coverage, truncation | `UNION_NEEDS_EVIDENCE` for affected decision | UNKNOWN/INCOMPLETE is not a demonstrated byte conflict or absence |
| External/ambiguous dependency or unproved context change | `RELOCATION_NOT_PROVEN` | Physical dimensions may remain proven; dependent relocation blocked |

Validate the complete proposed namespace, including directories and ancestors,
Windows case/native conflicts and every source mapping. Do not infer conflict
freedom from `tree_exact`, `path_conflict == false`, a global exit code, chart set
equality or digest multiset equality alone. A physical conflict-free historical
union is not functional equivalence and carries no storage-saving promise.

Separate dimensions must state physical compatibility, inventory conservation,
path/reference conservation, relocation admissibility, staging construction,
independent verification, publication and source retirement. Proposed summary
labels `UNION_PHYSICALLY_COMPATIBLE`, `UNION_BLOCKED_CONFLICT`,
`UNION_NEEDS_EVIDENCE`, `VARIANT_PRESERVATION_CANDIDATE`, `PRESERVE_IN_PLACE` and
`RELOCATION_NOT_PROVEN` are **not frozen contracts**. CONFLICT means demonstrated
incompatibility; UNKNOWN/INCOMPLETE means missing proof. A plan must say planned,
never built/verified/published/merged/retired before those separate events.

## 6. Conflicts, intact variants and human review

Never choose the newest, largest, highest-chart-count or otherwise preferred
resource/distribution automatically. A conflicting distribution is retained as
an entire unit: all charts and regular files, auxiliaries, directories, relative
structure, original bytes and provenance. Keeping only the conflict file fails
the owner's preservation requirement. No chart or reference rewriting is proposed.

Phase 6B may describe this **conditional logical layout**:

```text
SongA/
  [compatible main union and exclusive entries]
  _variantes/
    001/ [entire admitted conflicting distribution]
    002/ [entire admitted conflicting distribution]
```

This is not permission to create it. A reference such as `../shared/kick.wav`
can change meaning after moving an otherwise byte-identical tree. Relocation
requires a before/after conservation proof for all admitted obligations, external
and shared resources, unresolved semantics, aliases and player discovery effects
relevant to the operation. Bounded Phase 4 proof alone is insufficient. Until
that specific proof exists, use `PRESERVE_IN_PLACE` / `RELOCATION_NOT_PROVEN`.
Unknown package boundaries also prevent claiming an intact full distribution
was moved merely because an observation subtree was copied.

An existing `_variantes` name, a Windows-equivalent spelling, nested containers
or target/input aliases must block or be explicitly reviewed; never overwrite or
silently rename them. Candidate IDs/order should derive from canonical evidence
and source identities, not enumeration or a semantic preference. The exact naming
and reserved-name policy remain open for review. IDs `001`, `002` are illustrative.

For several possible compatible main groups, present alternatives and reasons
to the human owner. Human choice cannot override a failed preservation proof;
it selects among demonstrably admitted alternatives or requests more evidence.

## 7. More than two distributions

A–B compatibility plus B–C compatibility does not establish A–C or a global union.
For example A has `kick.wav=X`, B lacks that path, C has `kick.wav=Y`; both B
pairs may be physically compatible while A–C conflict. Do not turn connected
components into union groups. With complete consistent path maps, checking every
pair for the same strictly physical rule can establish that limited property;
it still does not establish shared context, package or operational safety.

Phase 6B must validate the **whole** proposed target namespace and conservation
ledger, not merely chain previously positive pair labels. Preserve all sources,
charts, exclusives, variants, conflicts and unknowns, including distributions
excluded from a candidate main group. Offer alternative compatible groups without
automatically selecting an optimum. A general package solver, exhaustive grouping
search and global optimization are outside the proposed increments.

## 8. Minimal future architecture and reports

The proposed 6A consumer has three small responsibilities: admit existing legacy
evidence, construct a virtual per-path union with complete source mappings, and
validate/export the resulting conservation account. Reuse database/seal/comparison
verification, Phase 5 entry/reference concepts and Phase 4 semantic reconstruction;
do not treat exported JSON or CSV as sealed authority. Optional Phase 4 inputs
must pass the same current v2 reconstruction as `PhaseFiveAdmission.Verify`.
Missing/untrusted input leaves dependencies UNKNOWN and preserves physical facts.

No original chart/WAV/BMP/resource payload may be reopened by the planner to fill
missing evidence. Native metadata checks used for existing report exclusion are
distinct from payload freshness. No new source scan/hash, DB write, migration,
reseal, report rewrite or recursive inspection of real roots is part of 6A.
Revalidate admitted input bindings before publishing a requested report using
existing path/publication protections. Reports must be outside protected roots,
evidence/input files and player-scanned directories declared to the operation.
Unknown player scan roots cannot be claimed protected automatically.

Proposed compact structured plan and human report must make these fields accessible:

- Plan format/revision and digest, deterministic order, current validator revision,
  historical observation dates, accepted profile/CONTENT/PREFLIGHT/source seals,
  component and explicitly selected roots; no invented current observation time.
- All source appearances: original full/relative paths, entry/physical/native IDs,
  kind, SHA-256, multiplicity, root membership and all target-to-source mappings.
- Each virtual entry/decision, supporting reproduced dimensions, complete source
  accounting, conflicts with both identities/digests and relevant **admitted**
  chart references, models/policies/origins, missing evidence and precise blockers.
- Conservation obligations, coverage and truncations, package boundary UNKNOWN,
  external/shared references, relocation preconditions, prohibited operations and
  evidence needed to reconsider each blocked decision.
- Independent states for plan completion and each physical/semantic/relocation
  dimension; unclassified scope retained. No `SafeToMerge`, deletion list or
  promised recoverable bytes. No commands that execute mutations.

One failed pair or semantic report must not invalidate unrelated complete
physical evidence. Conversely a complete physical sub-result cannot hide missing
conservation mappings. Bounds and cooperative cancellation must be explicit:
two roots/one selected pair; finite entry, report byte, semantic-link and conflict
budgets with no silent sampling. Exact synthetic caps, plan serialization and
digest domain require review in the separately authorized 6A implementation;
existing verifier cost before consumer budgets must remain disclosed.

## 9. Proposed functional increments and acceptance gates

| Increment | Bounded deliverable | Gate still required |
| --- | --- | --- |
| 6A | Read-only two-root virtual physical union of accepted COMPLETE ADMITTED legacy EXACT_ONLY CONTENT; all files/provenance, explicit conflicts and preserved dependency information | New implementation authorization, synthetic tests and independent review; no materialization or broader profile |
| 6B | Multiple distribution groups, alternative main unions and intact variant layouts with conditional relocation | Separate scope/authorization; complete combined namespace and conservation proof; no general solver |
| 7A | Disposable synthetic staging from an accepted plan, fresh source preconditions, one admitted shared target copy and intact admitted variants | Separate executor authorization, independent byte/path/obligation verifier, injected failures and unchanged sources; no original retirement |
| 8 | Controlled publication of independently verified staging with receipts and recovery limits | Separate implementation and operational permission; original sources retained |
| 9 | Precisely proven per-instance cleanup after publication and fresh dependent-obligation checks | Separate executor, review and explicit cleanup authorization; equality alone never suffices |

Phases 6–9 remain necessary for the final mission, not optional improvements.
First 6A acceptance should be verifiable by the following finite criteria:

1. Only the selected legacy profile and two admitted distinct disjoint roots are
   accepted; completed CONTENT, parent/source seals, schema/integrity, membership,
   SUCCESS hashes and per-path comparisons are reconstructed. Wrong digests,
   incomplete components and other profiles are refused unchanged.
2. Independent synthetic expectations account for every source entry exactly
   once per root view; equal path/type/bytes map to one **virtual** target with
   all provenance, exclusives remain, different paths retain multiplicity,
   directories remain and byte/kind/case/alias conflicts block positive decisions.
3. Every chart appearance/context, admitted obligation and unknown is traceable.
   Manipulated Phase 4 reports cannot grant semantic authority; absent reports
   preserve physical comparison but block unsupported relocation conclusions.
4. Reports state historical physical scope, UNKNOWN package/relocation conditions
   and no operational authority. Plans are deterministic and independently
   validated; no destination tree, executable mutation plan or savings claim.
5. Controlled fixtures demonstrate no source payload access during planning,
   unchanged source/evidence bytes and metadata, no SQLite sidecars, safe report
   exclusion/no overwrite, cancellation/failure/limit behavior and no partial
   result promoted to complete. Real access remains unauthorized.
6. Existing applicable locked restore, Release build/full suite and synthetic
   smokes plus future planner-specific tests pass during implementation, with
   logs, exclusive diff and independent review. This document does not claim
   those future tests or the following matrix have been executed.

## 10. Future synthetic acceptance matrix — not implemented tests

Each oracle is independent expected fixture data or a separately designed
verifier, never the planner checking its own result. Rows outside 6A are retained
future requirements, not grounds to broaden the first evidence contract.

| Fixture | Required evidence | Expected decision | Conserved | Prohibited | Independent oracle |
| --- | --- | --- | --- | --- | --- |
| 1. Identical two-root trees | Complete sealed legacy CONTENT, known payloads/dirs | Physical compatible observation union; package UNKNOWN | One virtual entry/path with all origins | Remove originals or claim playback equivalence | Hand-authored tree and SHA list, provenance bijection |
| 2. Different direct chart sets, identical resources | Separate broader admission required | Outside first EXACT_ONLY 6A; needs evidence | Both charts/resources | Fake legacy admissibility | Known distinct chart sets and profile refusal |
| 3. Exclusive files on either side | Admitted complete views | Retain all exclusives | Every path/byte, empty directory | Drop unshared file | Explicit expected union map |
| 4. Same chart SHA, different resources | CONTENT per appearance, optional admitted Phase 4 | Shared identity; conflict for colliding resource bytes | Both contexts/resources | Chart equality implies resource equality | Known chart digest and differing WAV/BGA payload digests |
| 5. Same resource path, different SHA | SUCCESS reads and reproduced conflict | UNION_BLOCKED_CONFLICT | Both entries and provenance | Overwrite/prefer newest | Independently hashed unequal payloads at one path |
| 6. File versus directory | Complete entries including ancestors | Type conflict blocks union | Both trees | Flatten/drop directory | Explicit kind/path map |
| 7. Equal SHA at different paths | CONTENT digest groups and multiplicities | Keep both paths | Names, bytes and all occurrences | Substitute names or deduplicate paths | Known two-path multiset and target mapping |
| 8. Unreferenced auxiliaries | Whole CONTENT inventory | Retain opaque/unknown/readme/archive entries | All auxiliaries | Parser-based filtering | Fixture inventory includes deliberately unparsed files |
| 9. BMS parent reference | Authored path plus scoped verified bindings | In-root parent path evaluated; escaping/unproved relocation blocked | Chart, path and parent obligation | Normalize escape into package or open external file | Hand-resolved before/after path graph |
| 10. Shared external resource | Explicit unresolved external evidence, future admitted context | PRESERVE_IN_PLACE until context proven | Shared resource and all consumers | Move external file opportunistically | Independent context graph including outside-root consumer |
| 11. Unresolved conditional | Reconstructed syntax/model coverage and blockers | Semantic UNKNOWN; physical facts independent | Every branch/declaration/candidate | Select a convenient branch | Fixed expected branch/obligation list |
| 12. Missing or unreadable resource | Scoped no-match or incomplete acquisition | Missing match does not prove absence; incomplete CONTENT refused | Chart, diagnostics, observed inventory | Invent digest or salvage incomplete pair | Controlled absent/read-failing fixture and expected state |
| 13. A–B/B–C compatible, A–C conflict | Future complete three-root scope; B lacks A/C colliding path | Global union blocked | All three distributions | Connectivity implies compatibility | A:kick=X, B:exclusive, C:kick=Y expected path map |
| 14. Multiple main-group alternatives | Future admitted whole-group inventory | Human alternatives without preference | Every included/excluded distribution | Rank by age/size as authority | Enumerated fixture alternatives and conservation account |
| 15. Existing `_variantes` or case-equivalent name | Future namespace/reserved-name policy | Block layout pending reviewed naming choice | Existing subtree | Overwrite/silent rename | Expected collision from explicit directory list |
| 16. Intact relocatable variant | Future sufficient boundary and before/after path/context proof | Conditional variant candidate in 6B/7A | Entire distribution including auxiliary bytes | Keep only conflict file | Independent full-tree and obligation mapping, then staged verifier |
| 17. Variant with broken parent/shared reference | Future before/after context evidence | RELOCATION_NOT_PROVEN; PRESERVE_IN_PLACE | Original whole distribution/context | Move because its own bytes match | Before/after resolution mismatch witness |
| 18. Windows case spellings | Ordinal paths and native Windows policy | CASE_COLLISION blocks unsupported coexistence | Both spellings/identities | Collapse by case without proof | Explicit spelling map and shared conflict annotations checked independently |
| 19. Hardlinks, aliases, reparse points | Native IDs/link names/guards | Preserve hardlink names; refuse unsafe reparse/alias scope | Each occurrence and native-object relation | Infer removable link or follow unsafe alias | Controlled native fixture facts and refusal expectations |
| 20. Cancellation or stale/tampered evidence | Token, seal/input-binding checks | No complete usable plan; historical staleness limits explicit | Evidence and source bytes | Refresh real data silently or publish partial complete | Fault schedule, before/after hashes, no-final-report checks |
| 21. Repeated file/chart with distinct native IDs | Root-qualified entries, physical IDs and same SHA | One logical chart, all physical appearances retained | Per-path multiplicity and provenance | Conflate SHA with physical object | Known appearance table, digest counts and native-ID partition |
| 22. Unknown package boundary/nested container | Complete observation inventory, structure and missing boundary proof | PACKAGE_BOUNDARY_UNKNOWN; operational relocation blocked | All observed data and unknown outside context | Declare package complete from chart folder | Fixture container/child map and explicit boundary nonclaim |

## 11. Concrete open risks and decisions

The first physical planner does not need universal BMS semantics. Unresolved
questions must block only dependent claims, with these concrete follow-ups:

- Review exact plan serialization/digest, two-root selection and reduced synthetic
  budget policy before implementing 6A. Verify every provenance contribution and
  bounded failure state independently; do not add another physical comparator.
- Package boundaries, implicit dependencies, external/shared ownership, precedence,
  codecs and player discovery have no general proof today. This blocks operational
  relocation/publication where relevant, not a historical virtual physical union.
- Review future reserved names and deterministic variant IDs before 6B; retain
  alternatives instead of choosing a main distribution through arbitrary order.
- Historical seals establish accepted consistency, not current freshness, producer
  authenticity or universal TOCTOU freedom. Phase 7 needs fresh authorized reads,
  independent staging verification and fault tests before any publication gate.
- Broader chart sets need separately admitted observation scope/adapters. Accepted
  subset observation does not make its transport consumable by legacy Phase 4/5
  or this proposed first planner. No evidence conversion is assumed.

## 12. Authorization boundary and STOP

This design changes no Phase 1–5 implementation, schema, profile, canonical seal,
contract, authority indicator, test, script or historical benchmark evidence.
It grants no real-library/DB access, Phase 6 code implementation, source copying,
move/rename/overwrite/rewrite, hardlink conversion, quarantine, deletion, staging,
publication, cleanup, GUI or general library-management capability. No new CLI
is announced. No stage, commit or push is authorized.

**STOP for independent review.** A future owner-authorized increment may implement
the bounded Phase 6A consumer above. Design readiness is neither implementation
nor acceptance and does not authorize that next increment.
