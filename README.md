# BmsLibraryAudit

Independent, conservative BMS library auditor.

Current scope: `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`.
Heuristics discover. Exact evidence proves. Verified plans mutate.

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
[architectural context](MASTER_CONTEXT.md), [sources](docs/SOURCES.md) and
[repository policy](AGENTS.md). A synthetic CLI smoke test is available as
`./scripts/Smoke-Test.ps1` after the Release build.
