# BmsLibraryAudit

Independent, conservative BMS library auditor.

**Long-term objective:** verified fusion of related BMS/BMSON folders and removal
of provably redundant file instances, while preserving all unique content and
required chart-resource relationships. This is not a general library manager.
See the [current nine-phase roadmap](ROADMAP.md) for the final mutation gates
and priority research gaps; historical design remains in MASTER_CONTEXT.md.

Development follows [the current master-context strategy](MASTER_CONTEXT.md#41-current-development-strategy--2026-10-14): implement sufficiently understood capabilities and research genuinely unresolved questions. Phase 2B compares exact folder contents, Phase 3 extracts resource syntax, and bounded Phase 4 joins verified syntax to sealed physical observations on synthetic inputs.

Implemented scope: validated `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`
and `PHASE_2A.READ_ONLY_PREFLIGHT` / `PHASE_2B.EXACT_CONTENT_EVIDENCE` / Phase 3 syntactic inspection / `PHASE_4.CONSERVATIVE_DEPENDENCY_RESOLUTION` with synthetic validation on Windows. Phase 4 closure remains incomplete.
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
not PACKAGE_ROOT; semantic dependency closure, universal player resolution, plans,
staging/publication and cleanup are not implemented. Hashing is sequential with
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

`dependency_resolution.json` is deterministic UTF-8/LF and includes version/build
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
Direct content inside RANDOM without IF, malformed/unknown control evidence,
SETRANDOM, SWITCH/SETSWITCH/CASE/DEF/SKIP and bounded-out conditions remain UNKNOWN.
Every active RANDOM scope must have its own admitted IF-chain predicate, identified
by that RANDOM's context ID. Predicates inherited from an outer scope or belonging
to an inner scope do not cover an unguarded RANDOM; reachability stays UNKNOWN
before any contradiction reduction, while observed physical candidates remain.
Logical usage links are namespace/decoding-specific syntactic possibilities and
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

Exit 0 means completed observed-path analysis and supported syntax within the
bounded model, **never full dependency closure**; a literal NO_OBSERVED_MATCH can
still have exit 0. Exit 2 retains incomplete/ambiguous/unsupported syntax or links;
1 refuses invalid evidence/destination or publication, and 130 means cancellation.
Full player-specific lookup, branch-sensitive effective declaration semantics,
required/unused proof and conservative dependency closure remain genuine Phase 4
work. Phase 5, real-library execution and all mutations need separate authorization.

The independent offline source inspector is a separate research component that
validates supplied synthetic evidence without opening source libraries or SQLite.
Its [consolidated closure](docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CLOSURE.md) is
ready for final independent review. Native verification remains incomplete;
offline acceptance grants no real-source or Phase 2A execution authorization.
