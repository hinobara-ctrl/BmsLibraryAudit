# Phase 6A virtual union — bounded contract

Date: 2026-10-15. Baseline: `e8db0bae8e5e33e646d4de6c81ebe3cdedd91a72`.
Implementation scope: synthetic, read-only, two legacy observation roots.
Status: `IMPLEMENTED_SYNTHETIC_PENDING_REVIEW`. Contract fixed before code;
validation results and independent acceptance are separate records.

## Input and command contract

`phase6 plan --db <phase2.db> --content-session <id>
--accept-content-manifest <sha256> --component <key> --root <key-A>
--root <key-B> --output <new-or-empty>` produces `virtual_union_plan.json`.
`phase6 verify` takes the same db/session/digest/component/two roots and
`--plan <virtual_union_plan.json>` instead of output, and writes only a CLI summary.
Selectors are exact sealed root keys; root order is canonicalized ordinally.
Exactly two distinct, nonnested, nonaliased roots from one ADMITTED COMPLETE,
coherent, sealed, comparison-eligible EXACT_ONLY component are required.
Only `PREFLIGHT_AND_CONTENT` / `phase2ab-content-1` is admitted through existing
read-only database, schema/integrity and CONTENT/parent/source seal verification
and reproduced per-path comparisons. No schema or historical evidence change.
Current native directory identity/resolution guards root overlap and report
exclusion; source payloads are never opened. This is not source-byte freshness.

## Plan data and digest

Envelope fields: `plan`, `plan_sha256`. Plan version `phase6a-virtual-union-1`.
The plan retains accepted provenance, two root inventories of PhaseFiveEntry,
virtual path/kind/digest entries with every contributing root/entry reference,
conflicts with both original entries, and one conservation disposition per source
appearance. Logical chart digest is carried by every chart entry; no appearance,
hardlink name or unknown/auxiliary file is filtered out. Empty directories remain.
Every appearance is represented virtually or blocked with a precise reason.
`chart_obligations` retains one UNKNOWN dependency obligation and original parent
per physical chart appearance, even for identical logical SHA-256 identities.

Digest domain: UTF-8 `BmsLibraryAudit.Phase6A.VirtualUnionPlan.v1`, then one zero
byte, then compact deterministic UTF-8 snake_case JSON of `plan` in model property
order. SHA-256 lowercase hex. Arrays have documented canonical order: roots by
key, inventories/virtual entries by ordinal relative path, sources by root ID then
entry ID, conflicts by path/type/root/entry, conservation by root/entry. No output
directory or current execution timestamp participates. Limits and validator build
revision are included. The envelope is indented UTF-8/LF with final newline.
The digest proves serialized integrity, not authenticity or current freshness.
Verification requires a matching declared current validator revision value in
provenance. `UNKNOWN_DIRTY` is unknown build identity, not executable authentication.
Cross-revision compatibility/authentication is not established by this first format.

## Decisions, verification and authority

Consume the existing physical comparison kernel and conflict annotations. Same
path/regular kind/SHA maps to one virtual entry with all origins; exclusives remain;
same SHA at different paths remains at different paths. Byte/kind/case/native
conflicts block affected entries, including descendants of a blocked ancestor;
no fake dual-content entry is created. A full independent conservation account
must cover every input entry once. No copy source is selected.

States: physical `UNION_PHYSICALLY_COMPATIBLE` or `UNION_BLOCKED_CONFLICT`;
conservation `COMPLETE_ACCOUNTING`; semantic `SEMANTIC_EVIDENCE_UNKNOWN`;
package `PACKAGE_BOUNDARY_UNKNOWN`; relocation `RELOCATION_NOT_PROVEN`;
construction `NOT_CONSTRUCTED`; verification `VIRTUAL_PLAN_CHECKED` (only plan
data, never destination verification); publication `NOT_PUBLISHED`; retirement
`NOT_RETIRED`. Dependency closure, merge authority and removal authority are false.
Known physical inventory is retained as conservation evidence, while all chart
dependency obligations remain unknown. Optional Phase 4 reports are **not accepted
in this first increment**; no report claims or PROVEN strings are trusted.

The verifier independently admits evidence, checks exact provenance/inventories,
derives expected per-path source sets directly from them, validates blocked paths,
ancestor and Windows conflicts, accounts for every appearance, checks all states,
limits, counts and digest. It does not call the planner or accept a valid digest
as proof of correct decisions. Sharing the existing physical conflict kernel does
not replace independent plan accounting and fixture-derived test expectations.

Exit 0: compatible physical plan generated/verified. Exit 2: valid conflict plan
generated/verified; not operational success. Exit 1: refused evidence/selection,
malformed/tampered plan, exceeded budget or publication failure. Exit 130:
cancellation. Rejection/limits publish no partial successful plan. Human CLI
summary includes roots, source/virtual/shared/exclusive counts, conflicts, states,
digest and explicit absence of operational permission.

## Bounds and publication

Fixed maxima, reducible through API tests only: two roots, 20,000 total source
entries, 20,000 virtual entries, 20,000 conflict annotations; zero imported semantic
reports/references; 16 MiB input/output JSON, 128 MiB managed-memory growth at
cooperative checkpoints and 60 seconds of consumer work. No sampling or truncation.
Legacy whole-session seal/comparison verification has preexisting scope-sized cost
before these consumer budgets; JSON serialization and blocked I/O are not hard
instantaneous memory/time bounds. Cancellation is cooperative.

Report publication reuses protected roots/input bindings and exclusive flushed
owned temporary/no-overwrite rename. Output excludes captured current/historical
library roots, evidence and inputs; undeclared external player roots cannot be
claimed protected. A crash/lost binding can leave an owned temporary for review.
No consolidation directory, staging, variants layout, relocation, mutation script,
recoverable-space promise, removal list or source change is produced. Phase 6B–9
remain outside scope. STOP for independent review after synthetic validation.

## Synthetic validation record

Locked restores and Release builds passed without warnings/errors. The main
suite passed 617/617 (576 historical cases retained plus 41 Phase 6A cases), and
the separate offline project passed 276/276, with no failures or skips. All eight
synthetic smokes passed, including the new scan→preflight→CONTENT→plan→verify
smoke with compatible/conflicting pairs, independent expected paths/hashes/counts,
exclusive source handles, deterministic reexport and unchanged evidence/sources.
The verifier rejects edited plans even when their digest is recomputed. Tests
also cover hardlink-name multiplicity, invalid/nested roots, protected plan-input
reads, budget refusal, cancellation and publication interference.

Logs, TRX, preservation inventories, complete diff and report A–N are retained in
`.artifacts/phase6a-20261015-8d9d457b`. This is synthetic validation pending
independent review, not real-library acceptance or authorization of a later phase.
