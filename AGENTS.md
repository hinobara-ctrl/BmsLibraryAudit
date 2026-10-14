# Repository policy

Read MASTER_CONTEXT.md, ROADMAP.md, docs/SOURCES.md and README.md before implementation.
Implement what is sufficiently understood; research only unresolved questions
material to an authorized capability. Prefer small, usable, independently tested
increments; reuse project mechanisms and avoid speculative complexity, duplicate
documentation and repeated investigation of established behavior.
Distinguish implementation, verification and real-data authorizations. Failed or
UNKNOWN/BLOCKED conditions are not safe; block affected operations without
unnecessarily blocking unrelated safe work. Preserve genuine security, provenance
and source-preservation requirements with validation proportionate to risk.
Phase 1 remains chart indexing and read-only relationship discovery. All currently
implemented analysis phases must leave source libraries and simulator databases
unchanged; application writes are limited to owned audit/evidence databases and
requested reports. Future controlled publication and cleanup require a separately
implemented, explicitly authorized executor under the verified mutation protocol.
The long-term fusion/removal goal is not present source-mutation permission.
Use independent code; do not copy third-party implementations.
Do not commit or push without explicit user authorization.
Preserve unrelated tracked, staged and untracked changes. Validate applicable work
with locked restore, Release build/tests, safe synthetic CLI smokes and
git diff --check; check new untracked files separately. Test success grants no
real-data or next-phase permission.
