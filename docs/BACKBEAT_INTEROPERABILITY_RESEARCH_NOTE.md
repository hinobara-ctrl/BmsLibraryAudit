\# Backbeat Interoperability and Library Completeness — Deferred Research Note



\*\*Document date:\*\* 2026-10-11  

\*\*Status:\*\* RESEARCH ONLY — FUTURE CONSIDERATION  

\*\*BmsLibraryAudit reference baseline:\*\* `a300b54bf63d1e2077accb30e29c5bd40dacee2c`  

\*\*Backbeat repository:\*\* https://github.com/zkldi/backbeat  

\*\*Backbeat reference commit:\*\* `95523cdfdd2761f9831be38a3a868ce256dd06c7`



\*\*Implementation authorization:\*\* NOT AUTHORIZED.



> \*\*Heuristics discover. Exact evidence proves. Verified plans mutate.\*\*

>

> This document preserves future research opportunities. It does not modify the approved Phase 2 contract or authorize additional implementation.



\---



\## 1. Motivation and Long-Term Objective



BmsLibraryAudit originated from the need to analyze, organize, and eventually improve a large BMS/PMS rhythm-game library.



The ultimate goal is not merely to detect duplicate files or recover disk space.



The intended result is a library that is:



\- Complete relative to identifiable reference distributions.

\- Technically functional, with all necessary chart dependencies accounted for.

\- Organized without losing meaningful chart, sound, BGA, or difficulty variants.

\- Conservatively deduplicated using verifiable byte-level evidence.

\- Capable of identifying missing resources and possible recovery sources.

\- Protected from unsafe or unauthorized filesystem modifications.



The emergence of Backbeat presents a potentially valuable external source of chart and asset identities.



Four independent concepts must remain distinct:



1\. \*\*Dependency integrity:\*\* Whether a chart's required resources can be resolved.

2\. \*\*Distribution completeness:\*\* Whether all files expected from a specific reference distribution are present.

3\. \*\*Variant coverage:\*\* Which chart modes, difficulties, arrangements, and resource variants are available.

4\. \*\*Consolidation safety:\*\* Whether redundant or complementary files could eventually be reorganized without losing content.



A standalone 7K distribution can be complete without containing a 9K or 14K chart.



Likewise, two byte-identical charts can belong to different distributions with different audio or visual assets.



The governing distinctions remain:



```text

RELATED != EQUIVALENT != SAFE\_TO\_REMOVE



CHART\_BYTE\_EXACT != PACKAGE\_EXACT



SAME\_FILENAME != SAME\_CONTENT



SAME\_CONTENT != AUTOMATICALLY\_REMOVABLE

```



\## 2. Verified Backbeat Architecture



Backbeat is an open-source, Rust-based system for distributing and storing rhythm-game charts, assets, packs, difficulty tables, and courses.



It provides a content-addressed storage model that uses SHA-256 identities to avoid storing redundant copies of identical assets.



Backbeat is designed primarily for finished, immutable chart distributions.



Its authors explicitly distinguish Backbeat from conventional editable Songs folders.



\*\*References:\*\*



\- \[Backbeat repository](https://github.com/zkldi/backbeat)

\- \[How Backbeat Works](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/devs/how-it-works.md)

\- \[Integration Guide](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/devs/integrate-backbeat.md)



\### 2.1 Backbeat Bundle Model



A Backbeat `.bb` document represents one playable chart together with associated asset references.



Its principal information includes:



\- Chart filename.

\- Human-readable description.

\- Original chart bytes, encoded using GZIP and Base64.

\- Resource filenames associated with SHA-256 identities.



The fundamental relationship is:



```text

Chart bytes

&#x20;   +

Chart filename

&#x20;   +

Asset filename → Asset SHA-256 mapping

&#x20;   =

Backbeat bundle

```



Three identities are particularly relevant:



| Identity | Meaning |

|---|---|

| Chart SHA-256 | Identity of the original, uncompressed chart bytes |

| `combinedAssetsId` | Deterministic identity of the asset path-to-hash mapping |

| `bundleId` | Deterministic identity of chart filename, chart bytes, and associated asset mapping |



This means that two bundles can contain identical chart bytes while maintaining different assets.



A chart SHA-256 match therefore does not establish package equivalence.



\*\*References:\*\*



\- \[Backbeat File Specification](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/specs/backbeat-file.md)

\- \[Bundle ID Specification](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/specs/bundle-id.md)

\- \[Combined Assets ID Specification](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/specs/combined-assets-id.md)



\### 2.2 Content-Addressed Storage



Backbeat stores assets by their SHA-256 identities.



Its storage architecture includes:



```text

Backbeat Store/

├── backbeat.db

├── .assets/

│   ├── XX/

│   │   └── YY/

│   │       └── remaining-sha256

│   └── ...

└── .downloading/

```



The documented SQLite database includes tables relevant to future interoperability:



| Table | Potential Use |

|---|---|

| `chart\_data` | Chart SHA-256 and compressed chart bytes |

| `chart\_id` | Additional chart identifiers such as MD5 |

| `bundle` | Bundle identity and associated chart/asset identities |

| `asset\_map` | Resource path and expected SHA-256 |

| `downloaded\_asset` | Assets installed in the local Backbeat store |

| `pack` and related tables | Distribution and collection membership |

| `difftable` and related tables | Difficulty-table metadata |



The Backbeat specification permits read-only access to its documented database schema.



Direct writes to `backbeat.db` are explicitly prohibited.



Any future BmsLibraryAudit interoperability must preserve this restriction.



\*\*Reference:\*\* \[Backbeat Store Specification](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/specs/backbeat-store.md)



\### 2.3 Portable Packages



Backbeat supports `.bbzip`, a ZIP-based format for transporting bundles and their associated asset bytes.



The specification requires asset identities to be verified from actual file contents rather than trusting filenames.



This may eventually offer a portable source of reference distributions.



However, archive processing and extraction are outside the currently authorized BmsLibraryAudit phases.



\*\*Reference:\*\* \[Backbeat Zip Specification](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/docs/specs/backbeat-zip.md)



\## 3. Relevant BMS/PMS Dependency Findings



The inspected Backbeat packager supports `.bms`, `.bme`, `.bml`, and `.pms`.



It preserves the original chart bytes while separately interpreting resource references for packaging.



Observed dependency-handling capabilities include:



\- Used `#WAVxx` identifiers.

\- Used `#BMPxx` identifiers.

\- `STAGEFILE`.

\- `BANNER`.

\- `BACKBMP`.

\- `PREVIEW`.

\- Relative resource paths.

\- Alternative audio, image, and video extensions.



For example, a BMS chart might declare `sound.wav` while the actual distributed file is `sound.ogg`.



The packager attempts supported extension substitutions during resolution.



\### 3.1 Missing Resources



The inspected implementation can omit unresolved asset references from a generated bundle.



It also provides a separate `package\_with\_missing\_assets` function that exposes missing paths encountered during packaging.



Therefore:



\*\*A Backbeat bundle's asset map must not automatically be treated as an exhaustive list of everything required by the original chart.\*\*



A missing resource may have been omitted during packaging.



\### 3.2 Conditional Dependencies



The inspected BMS packager uses:



`BmsRandomStrategy::AlwaysFirstBranch`



This selects one interpretation of conditional BMS content.



Consequently, asset coverage obtained from that packaging pass cannot establish complete resource coverage across all possible `#RANDOM` and `#IF` branches.



This reinforces the need for independent conditional-dependency research in BmsLibraryAudit Phase 4.



\### 3.3 Simulator-Specific Resolution



Future research must also investigate:



\- Alternate audio/image extensions.

\- Unicode and Shift-JIS filename handling.

\- Case sensitivity.

\- Relative paths and parent-directory references.

\- Special BMS identifiers such as `#LNOBJ`.

\- Undefined resource identifiers.

\- Differences in resolution behavior between simulators.



\*\*References:\*\*



\- \[BMS Packager](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/rust\_extra/backbeat\_packager/src/format/bms.rs)

\- \[Dependency Resolution](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/rust\_extra/backbeat\_packager/src/deps.rs)

\- \[Missing Assets API](https://github.com/zkldi/backbeat/blob/95523cdfdd2761f9831be38a3a868ce256dd06c7/rust\_extra/backbeat\_packager/src/lib.rs)



These findings describe the inspected upstream implementation, not guaranteed behavior of every Backbeat version.



\## 4. Relationship to the BmsLibraryAudit Roadmap



| Phase | Relationship to Backbeat |

|---|---|

| Phase 1 | Existing chart SHA-256 identities could support future matching with Backbeat charts |

| Phase 2A | No change; independent filesystem metadata preflight |

| Phase 2B | No change; exact physical filesystem hashing and comparisons |

| Phase 3 | Independent BMS/PMS/BMSON resource declaration and dependency analysis |

| Phase 4 | Conditional resource reachability and alternative branch analysis |

| Future completeness audit | Analyze all charts, including those outside duplicate-candidate components |

| Future Backbeat interoperability | Optional, read-only matching against external bundle/resource information |

| Future recovery workflows | Verified suggestions for recovering missing files or complementary distributions |



\*\*Phase 2 must not acquire a Backbeat dependency.\*\*



The existing Phase 2 design is restricted to selected complete `EXACT\_ONLY` components.



A comprehensive library-integrity audit must eventually define a broader scope because charts without duplicates also need validation.



\## 5. Proposed Future Capabilities



These are research proposals, not authorized implementations.



\### 5.1 Chart-to-Bundle Cross-Referencing



Match verified local chart SHA-256 identities with available Backbeat bundles.



For every match, preserve:



\- Local chart identity and path.

\- Backbeat bundle ID.

\- Asset-set identity.

\- Source/store provenance.

\- Available collection metadata.

\- Associated resource paths and hashes.



Do not assume that only one bundle exists for a chart.



Do not arbitrarily select one distribution as canonical.



\### 5.2 Missing-Resource Detection



Future dependency analysis should distinguish:



| Proposed Status | Meaning |

|---|---|

| `PRESENT\_VERIFIED` | The expected resource has been located and verified |

| `MISSING\_RESOURCE` | A required resource is absent from a verified resolution scope |

| `UNDEFINED\_REFERENCE` | The chart uses an identifier without a valid declaration |

| `UNRESOLVED\_PATH` | The correct physical resolution path remains unknown |

| `CONDITIONAL\_RESOURCE` | Resource usage depends on unresolved conditional branches |

| `UNREADABLE\_RESOURCE` | A physical resource exists but cannot be safely verified |

| `UNUSED\_DECLARATION\_CANDIDATE` | A declaration appears unused under currently evaluated semantics |

| `REFERENCE\_VARIANT` | An external distribution references different resource bytes |



These names are provisional and do not exist in the current software.



A resource outside Phase 2's observation root must not automatically be classified as globally missing.



Likewise, an apparently unused declaration must never become automatic deletion authority.



\### 5.3 Resource Recovery Candidates



Consider the following hypothetical situation:



```text

Local library:

&#x20;   chart.bms → SHA256 A

&#x20;   kick.wav  → missing



Backbeat:

&#x20;   chart SHA256 A

&#x20;   bundle ID B

&#x20;   kick.wav → SHA256 X

```



Matching chart bytes and an external bundle's resource mapping could identify SHA-256 X as a candidate resource.



However, this does not prove that X is the only correct resource version.



Another distribution might legitimately provide different bytes.



The future system should report such evidence as a potential recovery opportunity rather than modifying the local collection.



A proposed report could include:



```text

Chart SHA-256:

&#x20;   A



Local resource:

&#x20;   kick.wav



Local status:

&#x20;   MISSING / UNRESOLVED



Reference bundle:

&#x20;   B



Expected reference SHA-256:

&#x20;   X



Reference provenance:

&#x20;   Backbeat server / store / collection



Recommended action:

&#x20;   Investigate candidate resource.



Automatic modification:

&#x20;   PROHIBITED

```



\### 5.4 Whole-Library Completeness Audit



A future audit should distinguish:



1\. Dependency integrity.

2\. Completeness relative to a nominated reference distribution.

3\. Availability of alternative chart difficulties and modes.

4\. Physical duplication.

5\. Organizational consistency.



These must not be reduced to one unqualified `COMPLETE` flag.



\## 6. Safety and Evidence Requirements



Any future Backbeat interoperability must comply with the following principles.



\*\*Read-only access\*\*



Do not modify Backbeat databases, installed assets, source libraries, or simulator databases.



\*\*Independent verification\*\*



Verify actual bytes before accepting a resource SHA-256 as physical evidence.



\*\*Explicit scope\*\*



No automatic network downloads, library scans, or access to Backbeat installations without separate authorization.



\*\*Distribution uncertainty\*\*



A particular bundle is one distribution of a chart. Its resources must not automatically be treated as universally authoritative.



\*\*Semantic independence\*\*



Backbeat metadata must not replace independent BMS/PMS/BMSON dependency analysis.



\*\*No automatic recovery\*\*



File copying, downloading, rewriting, merging, deletion, staging, or publication requires later contracts and explicit approval.



\*\*Safe input handling\*\*



Future implementations must validate paths, encodings, extension substitutions, archive contents, unsafe aliases, and resource limits.



\*\*Licensing\*\*



Backbeat uses GPL-3.0 licensing. Any potential SDK or code integration requires independent license review.



Prefer standards-based, read-only interoperability implemented independently where appropriate.



\*\*Versioned provenance\*\*



Record exact upstream specifications and reference commits. Refresh research before any development because Backbeat may evolve.



\## 7. Open Research Questions



Before implementing a Backbeat adapter, investigate:



1\. Which Backbeat servers and distributions should serve as candidate references?

2\. How stable is the documented `backbeat.db` schema?

3\. Can multiple bundles sharing a chart SHA-256 be preserved without ambiguity?

4\. How reliably do existing Backbeat packages represent the necessary BMS resources?

5\. How should conditional dependencies be represented across all possible branches?

6\. How do LR2, lr2oraja, beatoraja, and other relevant simulators resolve alternate filenames and paths?

7\. How can resource variants be distinguished from damaged or incomplete distributions?

8\. How should the complete local library be audited beyond Phase 1 duplicate candidates?

9\. What evidence is required before proposing recovery of a missing resource?

10\. How should potential Backbeat integration be implemented without compromising project independence or licensing compliance?



These questions remain deliberately unresolved.



\## 8. Decision and Resumption Conditions



\*\*Decision as of 2026-10-11:\*\*



Backbeat is a promising future source of exact chart and asset identity information.



Its content-addressed model aligns with BmsLibraryAudit's emphasis on reproducible physical evidence.



However, Backbeat does not replace independent dependency analysis, comprehensive library validation, or verified consolidation planning.



\*\*No immediate implementation changes are authorized.\*\*



Revisit this document when:



\- Designing Phase 3 dependency parsing.

\- Designing Phase 4 conditional reachability.

\- Developing a whole-library completeness audit.

\- Investigating external distribution matching.

\- Evaluating a read-only Backbeat adapter.

\- Planning verified resource recovery or library consolidation.



Refresh the upstream research before taking action.



\## 9. Project Status at Documentation Time



| Area | Status |

|---|---|

| Phase 1 | VALIDATED |

| Phase 2 design | REVIEWED AND COMMITTED |

| Phase 2A synthetic implementation | AUTHORIZED; results not yet reviewed |

| Phase 2B implementation | NOT AUTHORIZED |

| Phase 2 real-library execution | NOT AUTHORIZED |

| Phase 3/4 semantic analysis | FUTURE |

| Whole-library completeness audit | FUTURE |

| Backbeat interoperability | RESEARCH ONLY |

| Recovery and consolidation workflows | FUTURE, SEPARATELY AUTHORIZED |

| Destructive operations | NOT AUTHORIZED |



\---



\## Final Principle



Backbeat may eventually provide verifiable information about chart distributions and available resources.



BmsLibraryAudit must independently determine:



\- What exists locally.

\- Which dependencies are satisfied.

\- What is missing or uncertain.

\- Which distributions contain different content.

\- Which resources may be recoverable.

\- Which future changes would be safe.



\*\*This document is a research record, not an implementation or mutation authorization.\*\*



