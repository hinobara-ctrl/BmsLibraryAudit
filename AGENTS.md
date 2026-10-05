# Repository policy

Read MASTER_CONTEXT.md, docs/SOURCES.md and README.md before implementation work.
Phase 1 is limited to chart indexing and read-only relationship discovery.
Never modify source libraries or simulator databases. Only the project-owned
audit database and requested reports may be written by the application.
Use independent code; do not copy third-party implementations.
Do not commit or push without explicit user authorization.
Validate with restore, Release build, Release tests, a synthetic CLI smoke test
and git diff --check. Preserve unrelated working-tree changes.
