# Offline source inspector corrective hardening and scalability

Date: 2026-10-12, America/Santiago. Phase:
`PHASE_2A.READ_ONLY_SOURCE_INSPECTOR.OFFLINE_CORRECTIVE_HARDENING_AND_SCALABILITY`.

## A. Baseline and authorization

Initial branch main; HEAD and origin/main:
`e7ef157bcff13765a4514802ecd871075285f6f2`. Initial status, tracked and staged
diffs empty. Origin URL locally retains `hinobara-ctrl/BMSAudit.git`; the exact
required commit and offline projects match. No remote or history modification.
Read [policy](../AGENTS.md), [context](../MASTER_CONTEXT.md), [README](../README.md),
[sources](SOURCES.md), [design](PHASE_2A_READ_ONLY_SOURCE_INSPECTOR_DESIGN.md),
[foundation](PHASE_2A_SOURCE_INSPECTOR_OFFLINE_FOUNDATION.md) and historical
[BLOCKED evidence](PHASE_2A_READ_ONLY_SOURCE_INSPECTOR_SYNTHETIC_IMPLEMENTATION.md).
The new user attachment authorizes offline corrections and synthetic capacity
tests; historical native STOP instructions do not prohibit this authorized scope.
No SQLite, production execution, source library or real artifact access is permitted.

## B. Changed-file inventory

Seven tracked files modified and six new untracked files. No staged files.

| File | Change |
| --- | --- |
| [Evidence.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/Evidence.cs) | Scope-change types, returned report bindings, finite resource policy |
| [Provenance.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/Provenance.cs) | Expected/latest distinction and allocation-free current counters |
| [Relationships.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/Relationships.cs) | Historical incomplete spelling, aggregation and separate growth budgets |
| [Reports.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/Reports.cs) | Buffer adapters and row conversion shared with incremental input |
| [Reconciliation.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/Reconciliation.cs) | Binding checks, no false completion, lazy expected rows, retained findings |
| [Fixtures.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/Fixtures.cs) | Independently literal reports supplied through parser bindings |
| [LogicalTests.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/LogicalTests.cs) | Correct the previously incompatible incomplete-spelling expectation |
| [ReportEvidence.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/ReportEvidence.cs), new | Parser-only bindings and stream/buffer input APIs |
| [IncrementalCsv.cs](../tools/BmsLibraryAudit.SourceInspector.Offline/IncrementalCsv.cs), new | Chunked strict decoding, incremental hashing, CSV state machine |
| [CorrectiveTests.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/CorrectiveTests.cs), new | Functional corrections and independent oracles |
| [StreamingTests.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/StreamingTests.cs), new | Chunk boundaries, immutable identities, truncation, limits and cancellation |
| [ScalabilityTests.cs](../tests/BmsLibraryAudit.SourceInspector.Offline.Tests/ScalabilityTests.cs), new | Progressive bounded stress fixtures and measured results |
| This report, new | Corrective implementation/validation evidence |

Projects, solution, package references, lockfiles and build targets are unchanged.
No historical document or generated benchmark evidence was modified.

## C. Findings 2–4: evidence and corrections

C1 functional gate executed before scalability changes: 149 passed, zero failed
or skipped. Four independently authored spelling cases failed against the original
implementation and passed after correction. Evidence:
[before](../.artifacts/source-inspector-offline/corrective-c1-before.trx),
[after](../.artifacts/source-inspector-offline/corrective-c1-after.trx).
The former exit 1 was an intentional functional regression demonstration, not
an infrastructure failure. The existing spelling test's uppercase expectation
was corrected because static historical-source analysis and independent lowercase
oracles demonstrate that the old ordinal-minimum behavior was incompatible.
All existing test methods remain present.

## D. Historical compatibility decisions

[Historical analyzer](../src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs) first
inserts present non-OK parent spellings into an OrdinalIgnoreCase HashSet, then
adds diagnostic-excluded folders in first-present order. Equal additions preserve
the existing representative. The offline implementation independently keeps those
first spellings explicitly in dictionaries and sorts the final values ordinally.
Complete folders still choose their ordinal-minimum original spelling.
Tests cover OK/error reversal, multiple errors, diagnostics, nested subtrees and
accented case equivalents. Set equality alone cannot establish exact report text.
Frozen chart order remains a prerequisite: no claim of order-independent spelling
or historical input ordering outside supplied evidence is made.

## E. Raw CSV byte binding

C1 introduced parser-only complete bindings for all four reports: kind, exact
byte count/SHA-256, schema/tool versions, strict result, status/finding, completeness
and explicitly unverified filesystem/producer identity. CSV row collections are
read-only snapshots. Typed-only legacy reconciliation remains diagnostic and
cannot complete without all bindings. Substituted rows and changed summary bytes
are checked against their binding. Failed/cancelled inputs have no value or complete
digest. Matching bytes do not authenticate a historical producer.
Bindings are returned in OfflineResult.ReportEvidence; reconciliation agreement
is a separate result, never inferred by the parser. Summary binding collections
are frozen too. Failed bindings cannot pass reconciliation through supplied
replacement structures. EOF/captured-buffer observation is independently exposed
as EndOfSuppliedInputObserved; it can be true for malformed or cancelled parsing.
BytesComplete is the stricter complete parsed-report/fingerprint gate.

Stream inputs can freeze an expectedByteCount. A clean EOF at an otherwise valid
row boundary with the wrong frozen length produces INPUT_LENGTH_MISMATCH and no
complete digest/value. Without an external expected length or source census, EOF
only establishes the end of the supplied stream: loss of whole final rows cannot
be inferred from CSV syntax alone. Full ordered reconciliation and future native
acquisition remain necessary. No filesystem identity is inferred from EOF or SHA.

## F. Expected scan change classification

`ScopeChanged` and `ScanScopeChange` preserve expected ID, actual latest COMPLETED
ID and whether the expected ID exists. Its future native disposition is BLOCKED;
overall offline status remains INSPECTOR_PARTIAL. Validation continues to inspect
the supplied latest state for contradictions; invalid facts take precedence over
scope change, which prevents reconciliation for the frozen wrong snapshot.
No historical current rows are reconstructed or silently substituted. Tests cover
matching, later, missing expected, no completion, RUNNING, duplicate history and
malformed present-row provenance.

## G. Initial scalability analysis (C2, before algorithm changes)

Let N be retained charts, F folders, H global hashes, M distinct folder/hash
memberships, P=sum over hashes of k*(k-1)/2 contributions and E distinct pairs.
Input evidence already retains O(N) records and strings. Initial analyzer retains
another O(N) list of references, LINQ grouping buffers for duplicate and folder
passes, ordered duplicate location arrays, folder sets O(M), hash ownership O(M),
pair dictionary O(E), candidates O(E) and two adjacency graphs O(F+E).
Sorting costs O(N log N) in the worst duplicate group, O(H log H), O(F log F),
O(E log E) and component member sorts. Pair work is O(P), not O(E): 10,000 owners
of one hash require 49,995,000 contributions. Checking P before pair-map growth
already avoids allocating this hostile dense graph. It must remain exact.

Old CSV parsing retains full input bytes, decoded UTF-16 text, string[][] fields
and then typed rows. Even a streaming tokenizer still needs O(rows) for the
existing materialized reconciliation API. Reconciliation also allocates expected
duplicate/cluster arrays and current/OK chart arrays. Repeated path canonicalization
and LINQ groups are allocation hotspots. Cancellation occurs in explicit loops,
but framework sorting is cooperative only between operations. No wall-clock or
total native-memory guarantee exists.

Planned C3: chunked strict CSV decoding with incremental hashing, per-row conversion,
finite total/chunk/retention budgets, distinct candidate/hash budgets, fewer temporary
chart/group buffers and cancellation checkpoints. Retain the exact inverted-index
and BFS architecture. Dense output that exceeds budgets must remain explicitly
resource-limited, with no skipped edges. Disk storage or architectural replacement
is not authorized. Summary JSON's bounded DOM requires separate future work.

## H. Implemented scalability improvements

C3 retains the inverted-index and independent BFS architecture. A single chart
pass accumulates hash locations and folder physical counts/sets; repeated full
LINQ grouping and an additional present list are removed. Duplicate path-key
uniqueness is already validated, so a redundant per-group distinct-key pass is
unnecessary. Spelling, hash/pair/member order, classifications and labels retain
exact oracles. Separate hash/membership/pair limits control collection growth.
The contribution check remains before allocating the pair map. Known findings
survive later reconciliation cancellation/resource stops.

CSV uses a reusable byte block and strict stateful UTF-8 decoder, preserving
multibyte characters, surrogate pairs, doubled quotes and embedded CR/LF across
chunks. SHA receives exactly the bytes read before decoding; no normalization.
Each row is converted immediately; full original text and string[][] tables are
not retained by typed parsers. Caller streams are not sought, closed or opened.
Old buffer APIs adapt through a memory provider and retain their finite budget.
Expected duplicate and cluster rows are compared lazily instead of allocating
complete expected arrays. Provenance current counters use one cancellable loop.

Asymptotic pair time stays O(P), pair/graph storage O(E+F); exact complete dense
output cannot be made linear by these changes. Retained typed rows, chart evidence,
hash memberships and duplicate locations still cost O(N+M). Reconciliation retains
current/OK arrays and the bounded summary DOM. No disk spill, probabilistic pruning,
approximate graph or hidden pair/component truncation was introduced.

## I. Revised resource policy

All budgets are configurable and finite; exhaustion is ResourceLimit/ResourceStopped
and incomplete, not corruption. Library population is distinct from operational
admission. The new default chart envelope was demonstrated with 250,000 charts,
25,000 folders and, separately, 250,000 distinct hashes. Historical Scan 4 counts
243,483/19,925/154,925 are capacity references from documentation only; they fit
these individual count budgets. Actual historical pair/field/report distributions
were not inspected and are not certified by those counts.

| Budget | Old default | New default / rationale |
| --- | ---: | --- |
| Chart records | 100,000 | 250,000; executed default-envelope population |
| Folders | 20,000 | 25,000; executed default-envelope population |
| Report rows | 200,000 | 250,000; executed generated CSV population |
| Buffer bytes, including summary DOM | 8 MiB | 8 MiB retained; large CSV uses Stream overload |
| Pair contributions | 250,000 | 250,000 retained; separate from unique pairs |
| Individual string/field | 16,384 UTF-16 units | Unchanged |
| JSON depth / findings | 32 / 64 | Unchanged; one finding slot reserved for stop |
| Input chunk | None | 64 KiB default, finite validated maximum 1 MiB |
| Stream total bytes | None | 256 MiB configurable admission budget |
| Retained CSV field text | None | 64 Mi UTF-16 units, checked before retaining the next row |
| Distinct hashes | None | 250,000; matches default chart ceiling and executed unique-hash load |
| Folder/hash memberships | None | 1,000,000 finite growth bound; default charts imply at most 250,000 |
| Materialized candidate pairs | None | 100,000; independent of contributions; 99,681 exact pairs executed |

64 Mi retained field units bound decoded field text to about 128 MiB before
string/object overhead; the 256 MiB stream budget permits substantial UTF-8
encoding/CSV overhead. These are admission choices, not measured safe maxima:
the full 256 MiB, worst-case field lengths and million-membership limit were not
stress-tested. Counts and units do not enforce total managed/native process memory.
No int.MaxValue/long.MaxValue sentinel, removed check or unlimited policy.
500,000 and 1,000,000 low-hash chart tests explicitly set MaxChartRecords above
the default. Other shape budgets remain enforced. Millions of high-cardinality
charts and million-row reports are not certified by those successful tests.

Per-run stress admission used a conservative retention estimate <=1 GiB and
<one third of GC-reported available memory (34,280,943,616 bytes on this runner).
This is a pre-allocation estimate, not a hard OS memory cap. Initial uncalibrated
million-chart estimate 1,638,400,000 bytes was NOT EXECUTED. A separate 500k
calibration measured 269,101,312 sampled managed / 359,452,672 working-set bytes;
the revised low-hash million profile reserved 900,000,000 bytes with headroom.
High-hash 250k admission reserved 750,000,000 bytes. No OOM or uncontrolled retry.

## J. Synthetic fixtures and independent oracles

All fixtures are authored in memory. No production writer/analyzer is invoked.
Chart arrays are generated once, with shared folder/hash strings where appropriate.
Large CSV providers generate one line at a time and never materialize the entire
report. Independent expected SHA/counts are computed in a separate byte pass;
literal expected typed rows, physical multiplicities, pair mathematics and full
ordered component memberships are asserted. The existing nine-chart oracle and
external EXACT_ONLY neighbor test remain intact. No disk fixtures or SQLite.

Functional tests cover first-error versus first-present spelling, three Unicode/
nested variants, reversal/multiple spellings, immutable byte identities, different
bytes with equal rows, raw-summary substitution, malformed/duplicate/truncated
CSV, EOF cancellation, byte/row/text/hash/membership/unique-pair boundaries and
scope/history precedence. Chunk sizes 1/2/3/4/7/16/65,536 exercise strict decoding,
quotes, embedded line breaks and exact byte accounting. Input order and every
duplicate physical row are preserved.

## K. Actual test and stress results

Final evidence: [corrective-complete-validation.trx](../.artifacts/source-inspector-offline/corrective-complete-validation.trx).
**192 executed, 192 passed, 0 failed, 0 skipped/not executed**, across 80 methods.
All 137 original cases remain in the suite. Classes reconcile to these totals:

| Class | Executed / passed |
| --- | ---: |
| LogicalTests | 55 / 55 |
| ReportTests | 52 / 52 |
| ResourceTests | 30 / 30 |
| CorrectiveTests | 12 / 12 |
| StreamingTests | 29 / 29 |
| ScalabilityTests | 14 / 14 |

The 14 scalability test cases include 13 actually executed workload experiments
and one passing initial-admission planner test whose uncalibrated million-chart
allocation was NOT EXECUTED. Resource/cancellation/malformed cases pass because
they explicitly produce the expected incomplete stop, not complete analysis.
No workload silently took the environment-insufficient branch on this runner.

Actual final-run measurements below are decimal bytes and milliseconds; P/E are
contributions/materialized pairs. CSV has no graph work. All rows have independent
expected counts, tuples, order or explicit stop assertions, not just a timer.

| Workload | Charts or CSV rows | Folders / hashes | P / E | ms | Managed allocated bytes | Sampled managed peak | Sampled working-set peak | Result |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | --- |
| Many folders, in-folder duplicates | 250000 | 25000 / 25000 | 0 / 0 | 1500 | 2459015144 | 878585688 | 964960256 | Complete PASS |
| Few folders, many duplicates | 500000 | 128 / 16 | 130048 / 8128 | 2753 | 4766277936 | 784932888 | 883707904 | Complete PASS, configured chart budget |
| Calibrated million profile | 1000000 | 128 / 16 | 130048 / 8128 | 5926 | 9536145128 | 531319512 | 625188864 | Complete PASS, configured chart budget |
| Distinct-hash default envelope | 250000 | 25000 / 250000 | 0 / 0 | 1794 | 2615238152 | 298616984 | 717017088 | Complete PASS |
| Sparse chain | 39998 | 20000 / 19999 | 19999 / 19999 | 335 | 209145488 | 61274040 | 136298496 | Complete PASS, full RELATED closure |
| Four exact components | 800 | 400 / 8 | 39600 / 19800 | 26 | 9819400 | 188964216 | 449904640 | Complete PASS, 4 RELATED + 4 EXACT_ONLY |
| Dense admitted exact graph | 447 | 447 / 1 | 99681 / 99681 | 31 | 28280912 | 279837400 | 377942016 | Complete PASS |
| Highly shared hash | 10000 | 10000 / 1 | 49995000 / 0 | 78 | 114414064 | 258481184 | 401776640 | RESOURCE_BLOCKED before pair-map population |
| Cluster CSV, valid | 250000 | N/A | 0 / 0 | 637 | 318719472 | 102971208 | 197332992 | Complete PASS, 11500041 bytes |
| Duplicate CSV, valid | 250000 | 1 / 1 | 0 / 0 | 836 | 552351672 | 323155912 | 632315904 | Complete PASS, 50500093 bytes |
| Cluster CSV, late malformed | 250000 + bad suffix | N/A | 0 / 0 | 278 | 199739200 | 140096208 | 244699136 | Expected MALFORMED; no value/digest; 11500078 bytes read |
| Cluster CSV cancellation | Planned 250000 | N/A | 0 / 0 | 23 | 18352376 | 298299688 | 395542528 | CANCELLED after 1048576 consumed bytes |
| Chart cancellation | Planned 150000 | 1000 / 1000 | 0 / 0 | 258 | 319565760 | 247794392 | 464527360 | CANCELLED at visited chart 100001 |

Sampling interval 100 ms plus start/finish observations; short spikes may be
missed. Managed allocation is process-wide cumulative allocation during the
measurement, not retained memory. Working set includes host/GC state inherited
from earlier cases. Process lifetime peak working set reached **979681280 bytes**
in this final host, below the 1 GiB admission estimate budget; it is not a per-case
native allocation peak or a future hard cap. GC memory is not total native memory.
The notably larger later working sets than calibration show heap/process reuse
and input distribution matter. No unlimited capacity, real HDD, SQLite, runtime
forecast or million-distinct-hash certification follows from these results.

Earlier separately preserved runs: [C3 functional, 173 passed](../.artifacts/source-inspector-offline/corrective-c3-functional.trx),
[stress calibration, 9 passed](../.artifacts/source-inspector-offline/corrective-stress-calibration.trx),
[expanded stress, 13 passed](../.artifacts/source-inspector-offline/corrective-stress-expanded.trx).
The first new frozen-length test used an incorrect hardcoded 38-byte slice;
[that run](../.artifacts/source-inspector-offline/corrective-final-functional.trx)
recorded 175 passed/1 failed, exit 1. The intended complete-row prefix was authored
explicitly, without changing the independently expected INPUT_LENGTH_MISMATCH
outcome. This was a fixture arithmetic error, not hidden as infrastructure.
[Release validation](../.artifacts/source-inspector-offline/corrective-release-validation.trx)
and [EOF metadata validation](../.artifacts/source-inspector-offline/corrective-release-final.trx)
subsequently passed 191/191. The final 192-case run adds the large duplicate CSV.
Historical failures and all earlier artifact files remain available.

## L. Build, NuGet and testhost evidence

C1 used normal Windows host execution outside the restrictive sandbox for local
VSTest IPC, as authorized by the task's controlled execution context. No UAC,
administrator privileges or configuration changes. This task has not retried the
previous historical NuGet/IPC failures or erased their evidence. A read-only
Get-CimInstance memory query in the sandbox reported unavailable CIM access;
managed/process measurements and conservative per-run budgets will be used.
The memory query was optional: no elevation or repeated bypass was needed.
SDK **10.0.401**. Final commands, all exit 0:

```powershell
dotnet restore tests/BmsLibraryAudit.SourceInspector.Offline.Tests/BmsLibraryAudit.SourceInspector.Offline.Tests.csproj --locked-mode
dotnet build tools/BmsLibraryAudit.SourceInspector.Offline/BmsLibraryAudit.SourceInspector.Offline.csproj -c Release --no-restore
dotnet build tests/BmsLibraryAudit.SourceInspector.Offline.Tests/BmsLibraryAudit.SourceInspector.Offline.Tests.csproj -c Release --no-restore
dotnet test tests/BmsLibraryAudit.SourceInspector.Offline.Tests/BmsLibraryAudit.SourceInspector.Offline.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=corrective-complete-validation.trx" --results-directory .artifacts/source-inspector-offline
git diff --check
git diff --stat
git diff --cached --stat
git status --short
```

Both explicit Release builds reported zero warnings/errors. The final test
command built both projects again before executing 192 cases. Locked restore
reported current dependencies; NuGet auditing was not disabled. No NU1900 or
testhost IPC failure occurred in this task. Earlier historical failures recorded
in the foundation remain unchanged. No functional change was made to conceal
an infrastructure failure. Windows token administrator-role check returned false.

Library has no NuGet/project dependencies; deps.json contains only the offline
project and framework runtime. Test references remain Microsoft.NET.Test.Sdk
17.14.1, xunit 2.9.3 and xunit.runner.visualstudio 3.1.1, plus only the offline
library. Both project/lockfiles and all approved package hashes are unchanged.
The assembly-reference independence test passed; scoped source inspection found
no SQLite/Core/CLI reference, P/Invoke, reflection loading or filesystem-opening
API. Stream consumption is caller supplied; Process measurement exists in tests
only. No production suite/CLI smoke was executed because this task explicitly
forbids Phase 1/2A and any SQLite opening, including synthetic.

Final auxiliary Release binary SHA-256:

- Library: `0DBCBA2CE9568A2DF923207DDD89EE2A5F6075DB926678EBE83C82FAC099925D`.
- Tests: `BFA304715EDE7E9FBA24E87CE0AC90653A7775F980AB3B4EC6A30FDFE7EB2557`.

These are auxiliary dirty-working-tree artifacts, not clean/native provenance.
No committed revision was manufactured. Historical BLOCKED report SHA-256 remains
`54ED2BABB4A42A7093B717F57959CB384619E4A67C1CF25EE84A34A90AF1CCBC`.

## M. Remaining limitations and unimplemented work

All origin/native/SQLite verification remains unimplemented. Incremental CSV
parsing does not by itself remove retained typed rows, chart models, candidate
graphs or bounded summary DOM costs. Large dense complete output needs a separately
reviewed architecture if finite current budgets cannot accommodate it.
Summary JSON still has the 8 MiB buffer/DOM limit; large diagnostic/incomplete
arrays may exceed it and correctly stop as resource-limited. Typed CSV lists,
duplicate detection sets, source chart records, candidates and BFS adjacency
remain materialized. Generic ParseCsv also retains its string-array result by
contract. No hard total-memory/time enforcement, blocking-stream interruption or
cancellation inside framework sorts is claimed; cancellation is cooperative.

Concrete future proposal, NOT IMPLEMENTED: independently review a per-row report
cursor and a merge comparator consuming complete sorted expected/actual tuples
with multiplicity/order checks, plus a streaming summary projection. Full chart
or dense-pair scale may require a separate externally ordered acquisition/graph
representation. Disk-backed ordering/storage or replacement of evidence contracts
requires explicit review/authorization before implementation. Nothing is quietly
swapped in here. Source-model lists must still be stable during calls.

## N. Native/SQLite integration blockers

The historical observer, opening/guard, engine/VFS identity, sidecar/header,
schema/integrity/FK, race, cancellation and B1–B6 gates remain pending. No real
Phase 1 report, DB or library was used for this work. Phase 2A/2B not executed.

## O. Final recommendation

**`SOURCE_INSPECTOR_OFFLINE_CORRECTIVE_READY_FOR_REVIEW`**.
The authorized offline corrective scope is implemented and tested. Full native
inspector acceptance and unrestricted scalability remain unverified. Every offline
result still reports INSPECTOR_PARTIAL, NativeInspectionComplete=false and
Phase2ExecutionAuthorized=false. Library size alone is not a corruption finding.

Final audit: inspected every changed/new source and test file, public result/status
semantics, binding failure paths, exact pair/component ordering and independent
stress oracles. Existing production, scripts, build settings, projects, package
locks and historical docs have empty diffs against the required baseline. No
protected path was opened, statted, enumerated, hashed, copied or modified.
No SQLite, real reports, tracing, privilege/configuration changes, Phase 2A/2B,
production execution, staging, commit or push occurred. Generated results only
occupy approved project build/test locations. Documentation links, Markdown table
widths, strict UTF-8 and tracked/untracked whitespace were checked separately.

Final HEAD and origin/main remain the required SHA. Cached diff empty.
Tracked diff covers seven files (see B); six new files are untracked. Final status:

```text
 M tests/BmsLibraryAudit.SourceInspector.Offline.Tests/Fixtures.cs
 M tests/BmsLibraryAudit.SourceInspector.Offline.Tests/LogicalTests.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/Evidence.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/Provenance.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/Reconciliation.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/Relationships.cs
 M tools/BmsLibraryAudit.SourceInspector.Offline/Reports.cs
?? docs/PHASE_2A_SOURCE_INSPECTOR_OFFLINE_CORRECTIVE_HARDENING_AND_SCALABILITY.md
?? tests/BmsLibraryAudit.SourceInspector.Offline.Tests/CorrectiveTests.cs
?? tests/BmsLibraryAudit.SourceInspector.Offline.Tests/ScalabilityTests.cs
?? tests/BmsLibraryAudit.SourceInspector.Offline.Tests/StreamingTests.cs
?? tools/BmsLibraryAudit.SourceInspector.Offline/IncrementalCsv.cs
?? tools/BmsLibraryAudit.SourceInspector.Offline/ReportEvidence.cs
```

STOP for independent review. No next-stage integration or real access initiated.
