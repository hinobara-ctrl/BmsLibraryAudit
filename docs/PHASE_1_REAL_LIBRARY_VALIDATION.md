# Phase 1 real-library validation

Phase: `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`

Authorized implementation baseline: `53c45ca322f63da67b1657b34be6826d838cfd92`

`PHASE_1_STATUS: VALIDATED`

## Scope and evidence boundary

This record closes the read-only, real-library validation of Phase 1 chart
indexing, incremental cache reuse, relationship discovery, deterministic
reporting and safety boundaries. The configured Windows root was
`E:\Juegos\beatp\canciones`; the source library was treated as read-only.

This validation does not prove package equivalence. It includes no BMS/BMSON
semantic parsing, audio/resource/BGA comparison, merge planning or mutation.
The evidence boundaries remain:

```text
CHART_BYTE_EXACT != PACKAGE_EXACT
EXACT_CHART_SET != SAFE_TO_MERGE
SUBSET != same song/package
RELATED != EQUIVALENT != SAFE_TO_REMOVE
```

Large container folders, including the previously observed `...\bms\555\musicbox`
example, demonstrate why subset relationships are discovery evidence only and
must not be characterized as merge candidates without further proof.

## Scan validation

| Scan ID | Purpose | Discovered | Reused | Hashed | New | Changed | Missing | Errored | Unstable |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | Initial full filesystem scan | 226607 | 0 | 226607 | 226607 | 0 | 0 | 0 | 0 |
| 2 | First incremental re-scan | 226607 | 226607 | 0 | 0 | 0 | 0 | 0 | 0 |
| 3 | Second incremental re-scan | 226607 | 226607 | 0 | 0 | 0 | 0 | 0 | 0 |

All three scans had no diagnostics. Scan ID 1 terminal output was observed
directly. The artifacts named `scan-1.json` and `scan-2.json` identify Scan ID
2 and Scan ID 3 internally; they are not artifacts for Scan ID 1 and Scan ID 2.
Report generation does not create scan sessions.

## Stable report results

- Tool version `0.1.0`; schema version `1`.
- 226607 current and verified chart files; 0 unverified previously indexed
  chart files.
- 138049 unique SHA-256 values and 138049 unique MD5 values.
- 19925 physical and analyzed chart folders; 0 incomplete folders.
- 53432 duplicate SHA-256 groups and 13149 candidate folder pairs.
- Relationships: 529 `EXACT_CHART_SET`, 2645 `A_SUBSET_OF_B`, 2369
  `B_SUBSET_OF_A`, 1010 `HIGH_OVERLAP`, 3265 `MEDIUM_OVERLAP` and 3331
  `SHARED_CHARTS`.
- 4715 RELATED clusters. Size distribution: 2: 2436, 3: 1171, 4: 889,
  5: 163, 6: 48, 7: 3, 8: 1, 9: 2, 12: 1, 16: 1.
- 504 EXACT_ONLY clusters. Size distribution: 2: 493, 3: 10, 4: 1.
- 0 warnings, 0 errors and no diagnostics.

The existing evidence notice remains controlling: chart relationships and
connected components do not establish package equivalence or authorize source
mutation. Cached hashes depend on size/mtime; use `--rehash-all` for fresh
reads.

## Older beatoraja-derived reference

An earlier, separate read-only analysis of beatoraja's `songdata.db` found
approximately 226390 chart rows, 137928 unique SHA-256 values, 19795 physical
chart folders and 13087 candidate pairs. Its relationship counts were 517
`EXACT_CHART_SET`, 2642 `A_SUBSET_OF_B`, 2326 `B_SUBSET_OF_A`, 1010
`HIGH_OVERLAP`, 3265 `MEDIUM_OVERLAP` and 3327 `SHARED_CHARTS`.

Those database-derived results and the native filesystem scan are strongly
consistent at library scale, but they are different evidence sources and are
not claimed to represent identical snapshots. The current filesystem can
differ from stored simulator state, and discovery rules can differ. The native
scan is authoritative only for the filesystem state it actually scanned.

## Independent audits and known limitations

Two independent reviews found zero blocking findings:

- Review A: `PASS_WITH_NON_BLOCKING_NOTES`. Its observations included the
  size/mtime cache limitation, deferred filesystem identity aliases, concurrent
  reorganization risk, conservative incomplete-evidence handling and report-set
  non-atomicity. No Phase 1 source-mutation path was found.
- Review B (Jules): `PASS_WITH_FINDINGS`. Its LOW finding records that a chart
  file which is itself a reparse point can produce both `REPARSE_SKIPPED` and
  `CHART_READ` diagnostics. This is diagnostic noise only; the chart remains
  excluded from trusted evidence.
- Jules's NOTE records candidate generation cost of
  `O(N + U + sum choose(k_h, 2))`. A pathological ubiquitous hash could consume
  substantial memory. The real-library duplicate evidence had maximum path
  multiplicity 6, so no pathological ubiquitous-hash fanout was observed in
  the validated target dataset.

The LOW finding and NOTE are known, non-blocking Phase 1 debt. They do not add
implementation requirements to this documentation closure.

## Safety result and closure

Validation found no source-library mutation, no simulator database mutation,
no incomplete folder evidence used, no errors, no unstable charts and no
warnings in the validated scan state.

`PHASE_1_STATUS: VALIDATED`

Phase 1 validation does not authorize deletion, moving, renaming,
consolidation, deduplication or any other source-library mutation. Any Phase 2
work requires a separate design/contract and separate explicit human
authorization. Phase 2 has not been authorized by this validation.
