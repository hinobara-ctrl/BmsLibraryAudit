# BmsLibraryAudit — Master Context

**Research/design snapshot:** 2026-10-05

## 1. Purpose

BmsLibraryAudit is intended to become an independent, conservative and auditable tool for analyzing very large BMS song libraries assembled from events, packs, mirrors, old distributions and multiple simulators.

The target library may be on the order of 1 TB.

A typical song directory may contain:

- `.bms`, `.bme`, `.bml`, `.pms`, `.bmson` charts;
- hundreds or thousands of `.wav`, `.ogg`, `.flac`, `.mp3` resources;
- BGA images and videos;
- banners, stagefiles and previews;
- readmes, archives and miscellaneous files.

The real problem is not simply:

> Find duplicate files.

The real goal is:

> Determine which folders represent copies, supersets, variants or complementary distributions of the same BMS song/family, and eventually determine whether they can be consolidated without losing any chart, audio variation, BGA, conditional resource or auxiliary file.

A false positive which removes unique content is substantially worse than leaving redundant data.

---

## 2. Fundamental principle

The project follows this rule:

**Heuristics discover. Exact evidence proves. Verified plans mutate.**

These concepts must remain separate:

```text
RELATED
≠
EQUIVALENT
≠
SAFE TO REMOVE
```

Examples:

```text
one shared chart
→ related folders
→ NOT merge authority
```

```text
same complete chart set
→ very strong candidate
→ still NOT package equality
```

```text
same filename
→ no content guarantee
```

```text
same bytes, different filenames
→ content duplicate
→ not automatically removable because charts may reference both names
```

```text
identical BMS bytes
+
different 01.wav bytes
→ chart exact
→ package NOT exact
```

---

## 3. Existing real-library research

A previous read-only analysis of a real beatoraja `songdata.db` produced approximately:

- 226,390 chart rows;
- 137,928 unique SHA-256 chart identities;
- 226,390 chart paths;
- 19,795 physical chart directories;
- 47,356 chart SHA-256 values present in multiple folders;
- 13,087 candidate folder pairs.

Candidate relationships found:

```text
EXACT_CHART_SET     517
A_SUBSET_OF_B      2642
B_SUBSET_OF_A      2326
HIGH_OVERLAP       1010
MEDIUM_OVERLAP     3265
SHARED_CHARTS      3327
```

The 517 exact-set pair edges involved approximately:

```text
996 folders
492 connected components

481 components of size 2
10 components of size 3
1 component of size 4
```

This demonstrated that folder analysis must be cluster-aware rather than pair-only.

It also demonstrated that useful candidates can be discovered before hashing the enormous audio corpus.

---

## 4. Container directory problem

Not every directory containing charts represents one song.

The real dataset contained at least one directory with roughly:

```text
1807 charts
```

which created misleading subset relationships against normal song folders.

Therefore:

```text
subset
≠
same song package
```

and:

```text
connected component
≠
merge group
```

Container/compilation directories must eventually be detected or at least prevented from becoming automatic merge targets.

Useful signals include:

- abnormally high chart count;
- very high relationship degree;
- many unrelated titles/artists;
- tiny Jaccard similarity against small folders;
- extreme set-size asymmetry.

Phase 1 should expose metrics, not attempt aggressive container classification.

---

## 5. LR2/OpenLR2 research lesson

LR2 does not magically avoid reading charts.

Its useful architectural idea is:

```text
scan chart
→ read chart
→ calculate MD5
→ store path/hash/metadata in SQLite
→ reuse the index on later scans
```

Therefore the useful lesson is:

> Pay the initial chart-reading cost once and maintain a persistent incremental index.

BmsLibraryAudit should implement its own smaller and safer equivalent.

Phase 1 does not need LR2 itself.

It should:

- recursively locate chart files;
- calculate MD5 and SHA-256;
- store them in project-owned SQLite;
- reuse unchanged records during later scans.

Unlike a simulator, Phase 1 does not need full chart metadata parsing merely to find exact duplicate content.

OpenLR2 is a public behavioral/reference source only. Its implementation should not be copied into this project.

Reference:

https://github.com/GOMazk/OpenLR2

---

## 6. beatoraja research lesson

beatoraja demonstrates a useful modern song database containing fields such as:

```text
md5
sha256
path
folder
parent
title
artist
difficulty
mode
...
```

Reference:

https://github.com/exch-bms2/beatoraja

Relevant source:

`src/bms/player/beatoraja/song/SQLiteSongDatabaseAccessor.java`

Its database may later be supported as an optional bootstrap/import source.

However:

```text
external simulator DB
≠
filesystem truth
```

because records may be stale, moved or modified after the simulator indexed them.

BmsLibraryAudit must own its own independent database.

External databases must remain read-only.

---

## 7. BeMusicSeeker research lessons

Useful concepts observed during research:

- duplicate charts can connect folders transitively;
- Union-Find/connected components are useful for discovering related folder families;
- sibling charts in the involved folders must be considered, not only the duplicate chart that triggered the relationship;
- verification should precede cleanup.

Ideas explicitly rejected as authority:

```text
one shared chart
→ folders are duplicates
```

```text
newer timestamp
→ overwrite older file
```

```text
same path + different content
→ silently choose one
```

```text
rename resource automatically
→ without rewriting/validating charts
```

These are unsafe for a preservation-oriented BMS library auditor.

Public conceptual reference:

https://github.com/Neeted/bemusicseeker-unofficial-fork

---

## 8. Private-source boundary

A private experimental BMS editor belonging to a third party was reviewed during research.

Its source code must not be:

- copied;
- ported;
- translated;
- vendored;
- used as a dependency;
- reproduced from memory;
- used as an implementation reference.

Only generic requirements learned from the research are retained:

- BMS parsing requires careful provenance;
- branches such as RANDOM/IF/SWITCH may contain valid resources;
- unselected branches cannot simply be treated as unused;
- historical encodings matter;
- logical BMS resources and physical files are different concepts;
- BMSON explicitly associates sound channels with audio resources;
- malformed or ambiguous input should fail closed;
- filesystem publication should eventually use staging and verification.

These are requirements, not implementation details.

The private repository must not be named or linked in this project.

---

# 9. Long-term architecture

```text
Configured song roots
        │
        ▼
Chart Indexer
        │
        ▼
Project-owned audit.db
        │
        ▼
Exact Chart Index
        │
        ▼
Folder Relationship Discovery
        │
        ▼
Union-Find / Clusters
        │
        ▼
Candidate Folder Analysis
        │
        ▼
BMS/BMSON Dependency Parser        [future]
        │
        ▼
Resource Resolver                   [future]
        │
        ▼
Audio/BGA/File Hashing              [future]
        │
        ▼
Package Classifier                  [future]
        │
        ▼
Merge Planner                       [future]
        │
        ▼
Stage → Verify → Publish → Cleanup  [future]
```

Phase 1 intentionally stops near the top of this pipeline.

---

# 10. Phase 1 objective

Phase:

```text
PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY
```

Implement:

```text
chart enumeration
+
incremental SQLite index
+
MD5/SHA-256
+
duplicate-chart discovery
+
folder chart-set analysis
+
relationship clusters
+
read-only reports
```

Nothing destructive.

---

# 11. Phase 1 platform

Use:

```text
C#
.NET 10 LTS
Microsoft.Data.Sqlite
```

CLI first.

Suggested project layout:

```text
src/
  BmsLibraryAudit.Core/
  BmsLibraryAudit.Cli/

tests/
  BmsLibraryAudit.Tests/
```

Future modules may include:

```text
BmsLibraryAudit.Formats
BmsLibraryAudit.Resources
BmsLibraryAudit.Analysis
BmsLibraryAudit.Planning
BmsLibraryAudit.Execution
```

The future mutating module should remain isolated from read-only analysis.

---

# 12. Recognized charts

Case-insensitive:

```text
.bms
.bme
.bml
.pms
.bmson
```

Phase 1 treats them as opaque chart files.

No semantic parsing yet.

---

# 13. Library roots

Allow one or more roots:

```text
E:\BMS\Songs
D:\Events
F:\OldPacks
```

Rules:

- recursively enumerate;
- do not follow directory symlinks/junctions/reparse points by default;
- never intentionally leave configured roots;
- never modify source contents;
- inaccessible paths must generate diagnostics rather than silently disappearing.

---

# 14. Per-chart data

At minimum persist:

```text
root_id
absolute_path
normalized_path
direct_parent_folder
filename
extension
size
mtime_utc
md5
sha256
first_seen_scan
last_seen_scan
present/current
```

Future filesystem identity such as NTFS volume/file ID is desirable but may be deferred if it threatens Phase 1 completion.

---

# 15. Hashing

When hashing a chart:

```text
open read-only
     │
     ▼
stream once
 ┌───┴────┐
 ▼        ▼
MD5     SHA-256
```

Do not read the file twice merely to calculate both.

Do not load the entire chart into memory merely to hash it.

MD5 exists for ecosystem compatibility.

SHA-256 is the Phase 1 exact chart-content identity.

Prefer checking size/mtime before and after hashing.

If a chart changes during hashing:

```text
retry once
or
mark UNSTABLE/ERROR
```

Never silently accept the digest.

---

# 16. Incremental cache

For a subsequent scan:

```text
same normalized path
+
same size
+
same mtime
→ hash may be reused
```

This is only a performance optimization.

It does NOT mean:

```text
size + mtime
→ files proven equal
```

Provide:

```text
--rehash-all
```

to recompute all current chart hashes.

Track scan statistics such as:

```text
discovered
reused
hashed
new
changed
missing
errored
unstable
```

Removed files must stop participating as current records.

---

# 17. Exact duplicate charts

Group current records by:

```text
SHA-256
```

A duplicate group requires:

```text
same SHA-256
+
2 or more distinct current paths
```

Every location must be visible in the report.

---

# 18. Folder chart sets

The direct parent directory of each chart is the Phase 1 physical chart folder.

Define:

```text
FolderChartSet =
set(distinct SHA-256 chart hashes)
```

Also keep:

```text
physical chart file count
```

separate from:

```text
distinct chart hash count
```

If the same chart is duplicated twice inside one folder, it must not inflate overlap metrics.

---

# 19. Efficient candidate discovery

Do not compare every folder against every folder.

Build:

```text
chart_sha256
→ folders containing this chart
```

For every SHA present in multiple folders, increment the unordered folder pairs.

Only folders sharing at least one exact chart become candidate pairs.

For each candidate compute:

```text
charts_a
charts_b
common

coverage_a = common / charts_a
coverage_b = common / charts_b

jaccard =
common /
(charts_a + charts_b - common)
```

---

# 20. Phase 1 relationship labels

Use exactly:

```text
EXACT_CHART_SET
```

when:

```text
common == charts_a == charts_b
```

Use:

```text
A_SUBSET_OF_B
```

when:

```text
common == charts_a
and
charts_a < charts_b
```

Use:

```text
B_SUBSET_OF_A
```

when:

```text
common == charts_b
and
charts_b < charts_a
```

Otherwise:

```text
HIGH_OVERLAP
```

if:

```text
min(coverage_a, coverage_b) >= 0.80
```

Otherwise:

```text
MEDIUM_OVERLAP
```

if:

```text
min(coverage_a, coverage_b) >= 0.50
```

Otherwise:

```text
SHARED_CHARTS
```

These labels describe chart-set relationships.

None means:

```text
SAFE_TO_MERGE
```

---

# 21. Clusters

Build connected components using Union-Find over related folders.

Example:

```text
A ↔ B
B ↔ C

Cluster:
A
B
C
```

Important:

```text
A related B
+
B related C

does NOT prove
A equivalent C
```

Clusters are navigation/discovery structures only.

An exact-only component view using only `EXACT_CHART_SET` edges is also useful.

---

# 22. Phase 1 reports

Produce deterministic UTF-8 reports:

```text
summary.json
duplicate_charts.csv
folder_candidates.csv
clusters.csv
```

`summary.json` should contain at least:

```text
tool/schema version
scan ID/date
roots
current chart files
unique SHA-256
unique MD5
physical chart folders
duplicate SHA groups
candidate folder pairs
counts per relationship class
cluster count
cluster-size distribution
cache/hash statistics
warning/error count
```

Stable ordering is mandatory so unchanged runs are diff-friendly.

---

# 23. Phase 1 safety boundary

Phase 1 must NEVER implement:

```text
delete
move
rename
overwrite
merge
cleanup
quarantine
hardlink
chart rewrite
source database mutation
```

The application may write only:

```text
its own audit.db
reports requested by the user
```

It must not modify:

```text
songdata.db
song.db
score databases
charts
audio
BGA
images
videos
source folders
```

---

# 24. Future BMS dependency parser

This is intentionally NOT Phase 1.

Eventually the tool must answer:

> Which files can this chart actually require?

Future representation:

```text
Chart
  ↓
LogicalResource
  ↓
PhysicalAsset
  ↓
PhysicalFile
```

Example:

```text
#WAV01 foo.wav
#WAV02 foo.wav
```

may mean:

```text
Resource 01 ─┐
             ├── Physical asset foo.wav
Resource 02 ─┘
```

Logical identity and physical identity must remain separate.

---

# 25. Future resource classes

Expected future states:

```text
REQUIRED_ALWAYS
REQUIRED_CONDITIONAL
DECLARED_UNUSED
PHYSICAL_UNREFERENCED
MISSING_RESOURCE
AMBIGUOUS_RESOURCE_BINDING
```

Safety default:

```text
uncertain
→ KEEP
```

not:

```text
uncertain
→ delete
```

---

# 26. RANDOM / IF / SWITCH

Future BMS parsing must preserve all valid resource possibilities.

Example:

```text
#RANDOM 2

#IF 1
#WAV01 a.wav
#ENDIF

#IF 2
#WAV01 b.wav
#ENDIF
```

Both:

```text
a.wav
b.wav
```

must be retained.

A single randomly chosen branch must never be used to classify the other resource as unused.

Prefer symbolic branch analysis instead of enumerating every combination.

---

# 27. Historical ambiguity

BMS behavior is partially implementation-dependent.

Example:

```text
#WAV01 a.wav
#WAV01 b.wav
```

must not automatically become:

```text
first wins
```

or:

```text
last wins
```

for destructive purposes.

A preservation-oriented result is:

```text
AMBIGUOUS_RESOURCE_BINDING
KEEP BOTH
AUTO MERGE BLOCKED
```

---

# 28. Encodings

Future parsing must account for historical BMS text encodings, at minimum:

```text
UTF-8
UTF-8 BOM
CP932 / Shift-JIS
```

Potential later support can include other legacy encodings found in real collections.

Keep distinct:

```text
original bytes
decoded text
authored path
resolved physical path
```

---

# 29. BMSON

Future BMSON support should follow the public specification:

https://github.com/bemusic/bmson-spec

In particular:

```text
sound_channels
SoundChannel.name
notes
BGM/playable distinction
continuation/restart semantics
visual assets
```

Phase 1 merely indexes `.bmson` files.

---

# 30. Future resource hashing

After candidate folders have been identified, hash their resources.

Do not hash the full terabyte of audio during discovery.

Possible pipeline:

```text
candidate folder
→ enumerate resources
→ size grouping
→ full strong hash
→ content identity
```

Caching should again use filesystem metadata only as an invalidation hint.

---

# 31. Same content under different filenames

Example:

```text
kick.wav
001.wav
```

with:

```text
SHA256(kick.wav)
==
SHA256(001.wav)
```

means:

```text
CONTENT_DUPLICATE
```

but not necessarily:

```text
REMOVABLE_DUPLICATE
```

because different charts may refer to each filename.

---

# 32. Same path, different content

Example:

```text
A/001.wav → hash X
B/001.wav → hash Y
```

If both are needed:

```text
HARD_CONFLICT
```

No policy such as:

```text
newer wins
older wins
larger wins
```

is acceptable.

---

# 33. No chart rewriting initially

Automatically renaming a resource would require rewriting:

```text
#WAVxx
```

or BMSON resource references.

That changes chart hashes and may affect:

```text
scores
IR identity
replays
historical identity
```

Therefore initial consolidation must preserve chart bytes.

Any merge requiring chart rewriting becomes:

```text
MANUAL_CONFLICT
```

---

# 34. Future classification

Possible long-term labels:

```text
PACKAGE_EXACT
CHART_EXACT_RESOURCE_EXACT
CHART_EXACT_RESOURCE_VARIANT
CHART_SUBSET
SAFE_UNION
AUDIO_VARIANT
RESOURCE_VARIANT
HARD_CONFLICT
MISSING_RESOURCE
CONTAINER_DIRECTORY
UNKNOWN
UNSUPPORTED
```

---

# 35. Future semantic chart comparison

Byte equality remains strongest.

Later, the tool may also construct:

```text
ChartStructureFingerprint
```

from:

```text
musical position
lane
note type
hold structure
timing
```

and:

```text
ChartPlaybackFingerprint
```

which additionally incorporates audio-content identity.

This may detect:

```text
same musical chart
different formatting/comments/encoding
```

or:

```text
same map
different audio version
```

Semantic equivalence is relationship evidence only and must not by itself authorize deletion.

---

# 36. Future audio fingerprints

Acoustic fingerprints may eventually identify:

```text
foo.wav
foo.ogg
```

as perceptually similar.

This must remain advisory:

```text
LIKELY_AUDIO_EQUIVALENT
```

not:

```text
SAFE_TO_DELETE
```

Binary content identity remains the destructive authority.

---

# 37. Future merge pipeline

Any mutating implementation must remain separate:

```text
ANALYZE
   ↓
PLAN
   ↓
REVALIDATE
   ↓
STAGE
   ↓
VERIFY
   ↓
PUBLISH
   ↓
OPTIONAL CLEANUP
```

Rules:

- analysis never changes source content;
- merge plan records exact input snapshot/hashes;
- changed inputs make the plan stale;
- staging copies before removing originals;
- destination files are independently verified;
- hard conflicts block publication;
- cleanup is a separate explicit command;
- early cleanup should favor quarantine/recycle behavior;
- every operation generates an audit receipt.

---

# 38. Testing philosophy

Phase 1 tests must use synthetic temporary libraries.

Required cases:

1. two identical chart files in different folders;
2. same filename with different bytes;
3. exact chart-set relationship;
4. A subset of B;
5. high-overlap pair;
6. shared-chart-only pair;
7. connected cluster of 3+ folders;
8. repeated identical chart files inside one folder do not inflate set similarity;
9. unchanged second scan uses cache;
10. changed chart invalidates cache;
11. removed chart stops being current;
12. `--rehash-all`;
13. case-insensitive chart extensions;
14. deterministic report ordering;
15. read error handling where practically testable;
16. reparse-point non-traversal where platform permits.

---

# 39. Recommended phase progression

```text
Phase 1
Chart index + relationship discovery

Phase 2
Filesystem comparison of strongest candidates

Phase 3
Independent BMS/BMSON dependency parser

Phase 4
Conditional resource analysis

Phase 5
Full package classifier

Phase 6
Immutable merge plans

Phase 7
Staging + verifier

Phase 8
Controlled publication

Phase 9
Optional cleanup + receipts
```

Each phase must have:

```text
exact baseline
explicit scope
tests
independent review
```

before moving to the next capability.

---

# 40. Phase 1 definition of success

Phase 1 succeeds when the program can independently take arbitrary song roots and reproducibly generate the chart/folder relationship evidence previously obtained using beatoraja, while:

- requiring neither LR2 nor beatoraja;
- retaining an incremental persistent index;
- using exact chart hashes;
- avoiding an O(all folders²) comparison;
- modifying no source-library file.

That is the intentional stopping point for the first implementation.