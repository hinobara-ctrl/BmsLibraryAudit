# BmsLibraryAudit

Independent, conservative BMS library auditor.

**Long-term objective:** verified fusion of related BMS/BMSON folders and removal
of provably redundant file instances, while preserving all unique content and
required chart-resource relationships. This is not a general library manager.
See the [current nine-phase roadmap](ROADMAP.md) for the final mutation gates
and priority research gaps; historical design remains in MASTER_CONTEXT.md.

Development follows [the current master-context strategy](MASTER_CONTEXT.md#41-current-development-strategy--2026-10-14): implement sufficiently understood capabilities and research genuinely unresolved questions. Phase 2B now compares exact folder contents on bounded synthetic inputs. The next functional milestone is conservative chart/resource dependency extraction; it requires separate authorization.

Implemented scope: validated `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`
and `PHASE_2A.READ_ONLY_PREFLIGHT` / `PHASE_2B.EXACT_CONTENT_EVIDENCE` with synthetic validation on Windows.
Heuristics discover. Exact evidence proves. Verified plans mutate.

`PHASE_1_STATUS: VALIDATED`. Phase 1 was validated against the real library;
see [the real-library validation record](docs/PHASE_1_REAL_LIBRARY_VALIDATION.md).

This phase indexes opaque chart bytes and reports chart-set relationships.
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

Evidence describes stable read observations, not a simultaneous filesystem
snapshot: metadata-preserving edits after a read can escape final checks. A hash,
tree or multiset match grants no deletion/merge authority. OBSERVATION_ROOT is
not PACKAGE_ROOT; chart resource dependencies, encoding/resolution, plans,
staging/publication and cleanup are not implemented. Hashing is sequential with
a reused 64 KiB payload buffer; per-pair views and the existing captured chart
analysis still use memory proportional to their scope. No real-library Phase 2B
execution or large-scale performance acceptance is claimed.

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

The independent offline source inspector is a separate research component that
validates supplied synthetic evidence without opening source libraries or SQLite.
Its [consolidated closure](docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CLOSURE.md) is
ready for final independent review. Native verification remains incomplete;
offline acceptance grants no real-source or Phase 2A execution authorization.
