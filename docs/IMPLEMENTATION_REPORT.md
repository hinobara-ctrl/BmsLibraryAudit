# Entrega de Fase 1 — 2026-10-05

1. **Baseline:** no existe SHA de HEAD. El remoto `hinobara-ctrl/BMSAudit`
   estaba vacío al clonarlo; `git rev-parse HEAD` indicó revisión inexistente.
   Rama local sin primer commit: `main`. `git status --short` inicial: vacío.
2. **Fase:** `PHASE_1.CHART_INDEX_AND_RELATIONSHIP_DISCOVERY`, terminada en
   código y validación sintética. No se empezó la Fase 2.
3. **Archivos:** todos son nuevos, no había archivos previos. Configuración:
   `.gitattributes`, `.gitignore`, `Directory.Build.props`, `global.json`,
   `BmsLibraryAudit.slnx`. Contexto/documentación: `AGENTS.md`,
   `MASTER_CONTEXT.md` (copia íntegra del adjunto), `README.md`,
   `docs/SOURCES.md`, `docs/PHASE_1.md`, este informe. Core: proyecto,
   `Models.cs`, `PathPolicy.cs`, `ChartHasher.cs`, `AuditDatabase.cs`,
   `ChartScanner.cs`, `RelationshipAnalyzer.cs`, `ReportWriter.cs`.
   CLI: proyecto y `Program.cs`. Tests: proyecto, `AssemblyInfo.cs`,
   `PhaseOneTests.cs`. Tres `packages.lock.json` fijan dependencias.
   `scripts/Smoke-Test.ps1` permite repetir la prueba completa.
4. **Arquitectura:** Core independiente, CLI fina, tests sintéticos; C#,
   .NET 10, Microsoft.Data.Sqlite. Escaneo secuencial, transacción por scan,
   instantánea coherente para informes.
5. **SQLite:** versión 1, application ID `0x424D5341`; tablas
   `schema_metadata`, `scan_sessions`, `library_roots`, `chart_files`,
   `diagnostics`, con claves foráneas e índices de hash/raíz/scan.
6. **Comandos:** `scan --db <audit.db> --root <path>` admite múltiples raíces
   y `--rehash-all`; `report --db <audit.db> --output <directory>`.
   Códigos de salida 0/1/2/130 documentados en PHASE_1.md.
7. **Hash/caché:** MD5 y SHA-256 reciben los mismos bloques en una sola
   lectura; buffer acotado; stat antes/después y comprobación de bytes.
   Reintento único ante inestabilidad. Reuso por ruta normalizada+tamaño+mtime
   únicamente como optimización. Errores invalidan evidencia anterior;
   desapariciones confirmadas dejan de participar.
8. **Candidatos:** índice invertido SHA-256 → carpetas; se acumulan sólo
   pares con hashes compartidos. Coste aproximado
   `O(N + U + Σ choose(k_h,2))`, más ordenación. Un hash ubicuo todavía puede
   generar muchos pares; no se ocultan candidatos mediante límites.
9. **Clasificación:** EXACT_CHART_SET, A_SUBSET_OF_B, B_SUBSET_OF_A,
   HIGH_OVERLAP, MEDIUM_OVERLAP, SHARED_CHARTS, en ese orden. Umbrales inclusivos
   0.80/0.50 mediante aritmética entera; cobertura y Jaccard se exportan.
10. **Clusters:** Union-Find con compresión de rutas y unión por tamaño;
    componentes RELATED y EXACT_ONLY de al menos dos carpetas. Orden e IDs
    deterministas. No equivalen a grupos autorizados para consolidación.
11. **Seguridad:** sólo DB propia y salidas solicitadas; rechazo de bases
    ajenas, salidas dentro de raíces, ancestros reparse y colisiones. No se
    atraviesan junctions. Carpetas incompletas quedan fuera de clasificación.
    No hay funciones de mutación de bibliotecas ni acceso a bases de simulador.
12. **Validación final:** SDK local oficial 10.0.401; `dotnet restore`, exit 0;
    `dotnet build -c Release`, exit 0, **0 warnings, 0 errores**;
    `dotnet test -c Release`, exit 0, **35 pasadas, 0 falladas, 0 omitidas**.
    Incluye todas las relaciones, caché, removidos, rehash, Unicode/CSV,
    determinismo por bytes, errores reales de lectura, junctions, rollback,
    bases ajenas, versiones, raíces y conservación de bytes/mtime.
13. **Smoke CLI:** pasó, exit 0. Primer scan: 5 hashes; segundo: 5 reutilizados
    y 0 calculados. Resumen: 5 charts presentes/verificados, 2 SHA-256 únicos,
    2 grupos duplicados, 2 pares candidatos (un A_SUBSET_OF_B y un
    B_SUBSET_OF_A), 1 cluster de 3 carpetas, 0 warnings/errores. Se verificó que
    bytes/mtime de fuentes sintéticas permanecieron iguales y que una salida
    dentro de la biblioteca fue rechazada con exit 1 sin crear carpeta.
    Artefactos retenidos en
    `.artifacts/smoke-d0aa24bfdce74bab9eb8571219837dc9/reports/`:
    `summary.json`, `duplicate_charts.csv`, `folder_candidates.csv`, `clusters.csv`.
14. **Estado Git:** todo continúa sin seguimiento; no se hizo stage, commit
    ni push. SDK, cachés, builds y artefactos están ignorados.

```text
?? .gitattributes
?? .gitignore
?? AGENTS.md
?? BmsLibraryAudit.slnx
?? Directory.Build.props
?? MASTER_CONTEXT.md
?? README.md
?? docs/
?? global.json
?? scripts/
?? src/
?? tests/
```

15. **Whitespace:** `git diff --check` terminó con exit 0 y sin salida.
    Como el repositorio no tiene archivos versionados, también se revisan
    los archivos nuevos contra NUL con `git diff --no-index --check`.
16. **Límites:** validación ejecutada en Windows y bibliotecas sintéticas,
    no en la colección real de 1 TB. Caché tamaño/mtime puede ocultar cambios
    con metadatos preservados; `--rehash-all` lo evita. Identidad NTFS, alias
    hardlink/short-name/SUBST y protección completa contra carreras de rutas
    quedan pendientes. Escaneos largos conservan un bloqueo de escritura;
    sidecars existentes requieren inspección manual. Informes requieren
    directorio nuevo/vacío y pueden quedar parciales si falla su escritura.
17. **Fase 2 diferida:** comparación de filesystem, de sólo lectura, sobre
    candidatos completos fuertes. Antes de ampliar capacidades queda pendiente
    la revisión independiente prevista por el contexto maestro. Parsing,
    recursos/audio/BGA, imports, clasificación automática de contenedores,
    planes de merge, staging, publicación y limpieza permanecen fuera de alcance.
18. **Desviaciones justificadas:** al ser un remoto vacío no era posible
    imprimir un SHA ni leer documentos inexistentes antes de crearlos. Se
    conservó el MASTER_CONTEXT adjunto y se crearon las políticas/documentos
    faltantes. El SDK ausente se descargó desde Microsoft a `.tools` sin
    instalación global. Se añadieron rechazos conservadores de raíces
    superpuestas, sidecars, salidas no vacías y carpetas incompletas para
    mantener evidencia auditable. No se alteró el alcance de Fase 1.
