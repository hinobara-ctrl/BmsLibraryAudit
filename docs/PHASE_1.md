# Phase 1 implementation

Scope: `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`.
Chart bytes are opaque. The only recognized extensions are `.bms`, `.bme`,
`.bml`, `.pms`, `.bmson`, compared without case sensitivity.

## Build and run

Requires a .NET 10 SDK. Direct production dependency: Microsoft.Data.Sqlite
10.0.12. Tests use xUnit and Microsoft.NET.Test.Sdk. Dependency lock files are
checked in. The CLI assembly is named `bms-audit`.

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- scan --db C:\Audit\audit.db --root E:\Songs --root D:\Events
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- scan --db C:\Audit\audit.db --root E:\Songs --rehash-all
dotnet run --project src/BmsLibraryAudit.Cli -c Release -- report --db C:\Audit\audit.db --output C:\Audit\reports-001
```

Each scan specifies the complete root configuration for that snapshot. Roots
omitted from a later scan become inactive; their old records remain in the
database but cannot participate in reports. Duplicate roots are collapsed;
nested/overlapping roots are rejected to keep root ownership unambiguous.
Missing/inaccessible configured roots are fatal configuration errors. Nested
enumeration/read failures become contextual persisted diagnostics.

Exit codes: 0 success (possibly warnings), 1 configuration/database/report
failure, 2 completed scan containing errors, 130 cancelled scan. Cancellation
or fatal failure rolls back the entire scan. An error-bearing completed scan
can still be reported; unresolved evidence is excluded from classification.

Run the reproducible synthetic CLI smoke test after building:

```powershell
./scripts/Smoke-Test.ps1
```

This session also prepared an ignored local SDK under `.tools/dotnet`. To use
it in the restricted workspace:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
./.tools/dotnet/dotnet.exe build -c Release
./scripts/Smoke-Test.ps1 -DotNet (Join-Path $PWD '.tools/dotnet/dotnet.exe')
```

## Architecture and SQLite ownership

Core contains path policy, hashing, transactional indexing, relationship
analysis and report writing. CLI only validates options, handles cancellation
and formats results. The test suite creates disposable synthetic libraries;
it does not need a simulator or real song collection. Source-file mutation in
test fixtures/smoke setup is solely for creating and exercising synthetic data.

Database schema version 1 has application ID `0x424D5341` and `user_version=1`:

| Table | Purpose |
| --- | --- |
| `schema_metadata` | Tool/schema identity and platform path comparison |
| `scan_sessions` | UTC start/end, status, rehash flag, root configuration, statistics JSON |
| `library_roots` | Original absolute spelling, unique comparison path, active flag |
| `chart_files` | Unique normalized path, root FK, original absolute path, direct parent, filename, extension, size, mtime UTC ticks, MD5/SHA-256, first/last-seen scan FKs, present flag, status |
| `diagnostics` | Scan FK, severity, code, path, contextual message |

Indexes support current hash lookup and root/scan lookup. Hash digests are
lowercase hex. Unknown size/time uses -1/0 after metadata failure. Chart
statuses: `OK`, `ERROR`, `UNSTABLE`, `UNVERIFIED`, `MISSING`, `INACTIVE`.
`present` is independent of hash validity: a discovered unreadable file is
present but its previous hashes are cleared. An unseen record under an
unreadable/skipped subtree becomes non-present and `UNVERIFIED`, never a
confirmed disappearance. Only confirmed previously present removals increment
`missing`; repeating that scan does not count the removal again.

Existing databases are probed read-only before any writable connection. Foreign
application IDs, unsupported versions and platform comparison mismatches are
rejected. An existing empty file is also foreign; only a newly reserved file is
initialized. Database filenames require `.db`. A new file is reserved with
CreateNew, never truncated. Pooling is disabled and connections/statements/
readers/transactions are disposed. SQLite uses its normal rollback journal;
scan updates and diagnostics commit together. Existing `-journal`, `-wal`,
`-shm` sidecars cause refusal before access, so a interrupted database requires
manual inspection/recovery with trusted SQLite tooling. Automatic recovery is
outside this bounded application. Long scans hold one write transaction.

## Hashing and cache

Sequential read-only FileStreams feed MD5 and SHA-256 IncrementalHash instances
from one pooled 64 KiB buffer. Charts are not loaded whole or read separately
for each algorithm. Windows sharing denies concurrent write/delete while the
stream is open. Size and last-write UTC ticks are checked before and after,
along with actual bytes read. A changed stamp retries once; a second unstable
attempt has no accepted digest and records `UNSTABLE`/an error.

Cache reuse requires the same normalized path, size and UTC mtime and an old
`OK` record with both digests. `--rehash-all` bypasses reuse. **Size + mtime is
not content equality proof.** Changes that preserve both may escape a normal
scan, which the tests demonstrate. SHA-256 identifies chart content; MD5 is
ecosystem compatibility data. Scan statistics record discovered, reused,
hashed (successful files, not attempts), new, changed metadata, missing,
errored chart files and unstable chart files. Directory diagnostics are counted
in summary errors separately from the chart-file `errored` statistic.

Windows comparison paths use invariant uppercase full paths with ordinal keys;
containment comparisons use OrdinalIgnoreCase. Original Unicode/spelling is
retained. Non-Windows comparison is ordinal and case-sensitive. A database
cannot be reused across these comparison modes. Windows directories configured
for case-sensitive names are unsupported. Device/extended path prefixes are
rejected; use regular absolute paths (BCL long-path handling remains available).
Short-name, SUBST/drive aliases and NTFS file identity are deferred; use one
consistent ordinary path spelling for each physical location.

## Folder candidates and components

The direct parent is the physical chart folder. Each eligible folder stores a
set of distinct SHA-256 identities and a separate physical chart-file count.
Copies inside one folder therefore add no overlap. If a folder has unreadable
charts, uncertain entry metadata or an incomplete enumeration, it is excluded
from folder classifications. Valid individual chart duplicates can still be
reported; `summary.json` names known incomplete folders and all diagnostics.
Directory links are skipped; chart-file links are refused without reading their
target and make their parent folder incomplete.

A hash-to-folder inverted index increments each unordered pair once per shared
distinct hash. There is no unconditional all-folder comparison. For `N` indexed
chart rows, `U` folder/hash memberships and `k_h` folders containing hash `h`,
construction costs approximately `O(N + U + sum choose(k_h,2))` before sorting.
Memory holds current chart rows, memberships and actual candidate pairs. A hash
shared by almost all folders can still produce quadratic output; no silent cap
or lossy pruning is applied. This is inherent in emitting every matching pair.

For sorted endpoints A and B:

| Label | Condition, evaluated in this order |
| --- | --- |
| `EXACT_CHART_SET` | common = charts_a = charts_b |
| `A_SUBSET_OF_B` | common = charts_a < charts_b |
| `B_SUBSET_OF_A` | common = charts_b < charts_a |
| `HIGH_OVERLAP` | min(coverage_a, coverage_b) >= 0.80 |
| `MEDIUM_OVERLAP` | min(coverage_a, coverage_b) >= 0.50 |
| `SHARED_CHARTS` | Any remaining common >= 1 |

Coverage is common / endpoint set size. Jaccard is common /
(charts_a + charts_b - common). Threshold decisions use integer comparisons.
Union-Find with path compression and union by size constructs RELATED and
EXACT_ONLY connected components. Only components with at least two folders are
reported; isolated folders remain in folder counts. IDs derive from sorted
members/components and are deterministic within a snapshot, not durable IDs
across edited libraries. A related component asserts a relationship path, not
pairwise equivalence. Subset/overlap relationships are not transitive.

## Reports

Reports use UTF-8 without BOM, LF newlines, invariant numbers and RFC-style CSV
quoting (double internal quotes; quote commas/CR/LF/quotes). They have stable
ordering for the same stored snapshot. Re-reporting a snapshot to two new
directories yields byte-identical files. Different scans intentionally change
scan dates/IDs and cache statistics. Output must be new or empty; CreateNew
prevents accidental overwriting. A report failure can leave partial output;
select another new directory after resolving the failure.

| File | Rows/order and columns |
| --- | --- |
| `summary.json` | Tool/schema/phase; scan IDs and UTC dates; roots; present/verified/unverified counts; unique hashes; physical/eligible folders; incomplete folders; duplicate/pair/label counts; related/exact cluster counts and size distributions; cache stats; warning/error counts and diagnostics; evidence notice |
| `duplicate_charts.csv` | One row per path in groups with 2+ distinct normalized paths; sorted SHA-256 then normalized path. sha256, md5, group_paths, root_id, absolute_path, parent_folder, filename, extension, size, mtime_utc |
| `folder_candidates.csv` | Sorted ordinal folder_a/folder_b. folder_a, folder_b, physical_files_a/b, charts_a/b, common, coverage_a/b, jaccard, relationship |
| `clusters.csv` | RELATED then EXACT_ONLY; sorted components/members. kind, cluster_id, folder_count, folder_path |

`current_chart_files` counts discovered present records even with errors;
`verified_chart_files` and unique digest counts require OK hashes.
`physical_chart_folders` counts their known direct parents;
`analyzed_chart_folders` includes only complete hash-evidenced folders.
Unseen chart paths under skipped/unreadable subtrees are counted separately as
`unverified_previously_indexed_chart_files`. Unknown unseen new charts cannot be
counted. Diagnostic arrays are sorted by path, code and message.

## Safety and next boundary

The production application only writes its owned SQLite database (including
SQLite's transaction sidecars) and explicitly requested reports. It never
writes charts, audio, images, video, simulator/score databases or source
directories. Database/reports must be outside all known source roots, including
inactive historical roots. Existing reparse points in root/output ancestors are
rejected; nested directory reparse points are not traversed. There are no
source-delete/move/rename/merge/cleanup/quarantine/hardlink/rewrite commands.

Filesystem checks are deliberately conservative but do not create a filesystem
snapshot: an adversarial path replacement between a check and opening a file
cannot be prevented by these BCL checks. Filesystem identity, concurrent
directory rename handling, hardlink alias identity and stronger race protection
remain limitations. Do not reorganize a library during a scan. Persistent
cache metadata is an optimization, not a fresh byte comparison.

Phase 2 is **read-only filesystem comparison of the strongest complete
candidates**, with exact evidence and tests. It has not been implemented.
Semantic BMS/BMSON parsing, conditional resources, audio/BGA hashing, container
classification, simulator imports, merge plans, staging, publishing and cleanup
are explicitly deferred. Chart equality or any relationship label never
establishes package equality or grants mutation authority.
