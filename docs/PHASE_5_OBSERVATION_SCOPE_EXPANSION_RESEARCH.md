# Phase 5 — Investigación de ampliación del ámbito de observación

**Fecha del encargo:** 2026-10-14. **Baseline inspeccionado:** `8690aa53531dfbe6255103cc46a722b6e9f58f51`, main.
**Fecha de cierre de la investigación:** 2026-10-15.
**Estado de este documento:** propuesta para revisión independiente; ningún contrato nuevo está implementado o admitido.
**Decisión recomendada:** pareja explícita, inicialmente de subconjunto estricto, en una base/perfil de evidencia nuevo; reutilización de primitivas verificadas, sin reinterpretación de componentes EXACT_ONLY.
La aceptación del clasificador sintético actual no autoriza esta implementación ni acceso real.

## 1. Objetivo y separación de garantías

Diseñar la menor observación física útil fuera de EXACT_ONLY. La señal de charts justifica
investigar una pareja; no demuestra su árbol, sus recursos ni su seguridad de consolidación.
Se preservan RELATED != EQUIVALENT != SAFE_TO_MERGE != SAFE_TO_REMOVE y
“Heuristics discover. Exact evidence proves. Verified plans mutate.”

Esta investigación termina en diseño. No incluye algoritmos nuevos, SQL de migración,
comandos disponibles, benchmarks personales ni planes ejecutables de mutación.

## 2. Baseline, aceptación y autorización

El clasificador se publicó en e9d8e9e597cb95bf65e874ba9825efaebbf87a47 y su hardening en
8690aa53531dfbe6255103cc46a722b6e9f58f51. La aceptación independiente
PHASE_5_BOUNDED_SYNTHETIC_CLASSIFIER_ACCEPTED proviene de la decisión del propietario
comunicada en este encargo, para el ámbito sintético declarado.

La Etapa A corrigió README/ROADMAP y cerró antes de iniciar esta investigación:
PHASE_5_ACCEPTED_BASELINE_DOCUMENTATION_CLOSED. Árbol inicial limpio e índice vacío.
MASTER_CONTEXT ya contiene correctamente los invariantes históricos y no se modifica.
La validación actual, separada de la histórica, se registra en el informe A–U local.

## 3. Evidencia inspeccionada y vocabulario de certeza

**I:** comportamiento implementado confirmado por lectura. **T:** invariante que comprueba una prueba existente.
**A:** inferencia arquitectónica de esas evidencias. **P:** propuesta futura que requiere aprobación.
**U:** cuestión abierta. Las referencias de código señalan el commit del baseline; los enlaces
se construyen sobre sus rutas y líneas inspeccionadas localmente, no sobre una rama futura.

| Tipo | Área | Archivo, clase/método y línea del baseline | Hecho observado |
| --- | --- | --- | --- |
| I | Phase 1 | [ChartScanner.Scan:9](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/ChartScanner.cs#L9); [ChartScanner.RecordChart:83](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/ChartScanner.cs#L83) | Recorre charts; persiste diagnósticos; reutiliza hash por tamaño/mtime salvo rehash explícito. |
| I | Hash | [ChartHasher.Hash:22](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/ChartHasher.cs#L22) | MD5/SHA-256 en un stream; dos intentos y stamps antes/después. No prueba snapshot simultáneo. |
| I | Descubrimiento | [RelationshipAnalyzer.Analyze:8](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs#L8); [RelationshipAnalyzer.Classify:66](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/RelationshipAnalyzer.cs#L66) | Excluye carpetas incompletas, invierte SHA→carpetas, cuenta hashes distintos y preserva PhysicalChartFiles; componentes son conectividad. |
| I | Captura | [PhaseOneSourceCapture.Capture:13](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs#L13); [PhaseOneSourceCapture.ValidateHeader:108](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseOneSourceCapture.cs#L108) | Fuente SQLite read-only, último scan COMPLETED exacto, fingerprint completo antes/después, todos los charts/roots y diagnósticos de ese scan; rollback 1/1 antes del proveedor. |
| I | Selección 2A | [PhaseTwoPreflightRunner.Run:9](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs#L9); [PhaseTwoPreflightRunner.StoreSource:118](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs#L118) | Solo EXACT_ONLY; materializa componentes, raíces, membresías y source_exact_edges; sella SOURCE con admission_policy EXACT_ONLY. |
| I | Revalidación | [PhaseTwoPreflightRunner.Revalidate:134](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs#L134) | Revalida pertenencia, spelling y SHA fresco de todos los charts directos, no solo el hash que originó una arista; fallo rechaza componente entero. |
| I | Inventario | [PhaseTwoPreflightRunner.Walk:182](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs#L182); [PhaseTwoPreflightRunner.Inventory:239](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs#L239); [PhaseTwoPreflightRunner.VerifyInventory:278](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoPreflightRunner.cs#L278) | Unión de raíces, entradas por path, membresías relativas por raíz; segundo recorrido. Fallos afectan todos los componentes dependientes. |
| I | Lecturas | [PhaseTwoFileSystem.Metadata:11](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs#L11); [PhaseTwoFileSystem.ReadChart:32](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs#L32); [PhaseTwoFileSystem.ReadContent:84](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoFileSystem.cs#L84) | Metadatos nativos, rechazo reparse, lecturas read-only; CONTENT verifica stamps/identidad, usa buffer 64 KiB, no cache por tamaño/mtime. |
| I | Seguridad | [PhaseTwoSafety.RequirePlatform:23](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoSafety.cs#L23); [PhaseTwoSafety.ValidateDestinations:111](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoSafety.cs#L111) | Windows obligatorio; identidades/resolved paths, sidecars/aliases, salidas fuera de todas las raíces conocidas y bases. |
| I | Apertura/esquema | [PhaseTwoDatabase.Open:40](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs#L40); [PhaseTwoDatabase.Validate:85](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs#L85) | Perfil PREFLIGHT_AND_CONTENT/phase2ab-content-1 y hash del esquema compilado exactos; una tabla añadida ya cambia la aceptación. |
| I | Modelo legado | [PhaseTwoDatabase.Schema:218](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs#L218); [PhaseTwoDatabase.Schema:237](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs#L237) | SOURCE exige EXACT_ONLY; source_exact_edges exige common=charts_a=charts_b; raíces tienen pertenencia única a componente por PREFLIGHT. |
| I | Inmutabilidad | [PhaseTwoDatabase.Initialize:151](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.cs#L151); [PhaseTwoDatabase.Content.InitializeContent:5](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.Content.cs#L5) | Triggers protegen identidad, fuentes selladas, estados terminales, hashes y resultados derivados. |
| I | CONTENT | [PhaseTwoContentRunner.Run:21](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentRunner.cs#L21); [PhaseTwoContentRunner.ValidateScope:140](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentRunner.cs#L140) | Acepta PREFLIGHT exacto; reproduce EXACT_ONLY con charts+diagnósticos y comprueba el inventario y pertenencias. |
| I | Fallos CONTENT | [PhaseTwoContentRunner.VerifyInventory:176](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentRunner.cs#L176); [PhaseTwoContentRunner.OutcomeCounts:191](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentRunner.cs#L191); [PhaseTwoContentRunner.ValidateEvidence:204](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentRunner.cs#L204) | ERROR/UNSTABLE locales inutilizan componentes dependientes; deriva preservación de otros completos. Drift de namespace/identidad causa STALE global de esa sesión. |
| I | Comparaciones | [PhaseTwoContentComparison.Derive:18](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentComparison.cs#L18); [PhaseTwoContentComparison.Store:60](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentComparison.cs#L60); [PhaseTwoContentComparison.Verify:80](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoContentComparison.cs#L80) | Primitiva pura ordinal con conteos y multisets; persistencia/verificador exigen todas las n(n−1)/2 parejas del componente completo. |
| I | Canonical | [PhaseTwoManifest.PhaseTwoCanonicalEncoding.Hash:12](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoManifest.cs#L12); [PhaseTwoManifest.ContentGroups:68](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoManifest.cs#L68); [PhaseTwoManifest.Source/Preflight/Content:85](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoManifest.cs#L85) | Dominios v1 y órdenes explícitos. CONTENT usa cuatro grupos; las comparaciones se reconstruyen independientemente, no integran directamente esos cuatro grupos. |
| I | Cierre/verificación | [PhaseTwoManifest.VerifyContent:88](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoManifest.cs#L88); [PhaseTwoDatabase.Content.content_parent/content_completion:75](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseTwoDatabase.Content.cs#L75) | Digest aceptado, etapa, padre y cobertura exacta. Triggers y VerifyContent no son reemplazables por flags de CSV. |
| I | Sintaxis | [PhaseThreeInspector.Inspect:17](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseThreeInspector.cs#L17); [PhaseThreeParser.Parse:8](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseThreeParser.cs#L8) | Inspector lee un chart con raíz declarada; parser puro conserva bytes/decodificaciones. No añade evidencia de recursos. |
| I | Resolución | [PhaseFourResolver.ResolveCore:14](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFourResolver.cs#L14); [PhaseFourResolver.AnalyzeVerified:129](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFourResolver.cs#L129) | Entrada SQL exige componente COMPLETE, fuente/chart CONTENT y reconstrucción; construcción pura reutilizable tras otra admisión válida. |
| I | Recursos/modelo | [PhaseFourPaths.Resolve:28](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFourPaths.cs#L28); [PhaseFourPaths.BmsonAudioSearch:74](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFourPaths.cs#L74); [PhaseFourSemantics.Analyze:148](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFourSemantics.cs#L148) | Ordinal, Windows y propuestas separados; prueba ordinal no vacía/modelos, con conservación e incertidumbre. Codec/selección no probados. |
| I | Phase 5 | [PhaseFiveClassifier.ClassifyCore:11](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFiveClassifier.cs#L11); [PhaseFiveClassifier.Discover:185](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFiveClassifier.cs#L185) | Rechaza perfiles ajenos; exige componente admitido/CONTENT completo. Reconstruye pendientes con diagnósticos del mismo padre. |
| I | Admisión semántica | [PhaseFiveAdmission.Verify:9](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFiveAdmission.cs#L9); [PhaseFiveClassification.ReferenceIndex:118](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFiveClassification.cs#L118) | Incluidos bytes/content, candidatos y grafo recompuestos; origen/política por candidato y revisión de build declarada, no autenticada. |
| I | Estados/coste | [PhaseFiveModels.PhaseFiveResult:40](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFiveModels.cs#L40); [PhaseFiveClassifier.ClassifyCore:129](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/PhaseFiveClassifier.cs#L129) | Global e individual separados; parejas acotadas, resultados/JSON materializados. No equivalencia de paquete ni mutación. |
| I | Publicación | [ObservedReportPublication.Publish:6](https://github.com/hinobara-ctrl/BmsLibraryAudit/blob/8690aa53531dfbe6255103cc46a722b6e9f58f51/src/BmsLibraryAudit.Core/ObservedReportPublication.cs#L6) | Temporal exclusivo, flush y rename sin sobrescritura; guards revalidados, cleanup solo del temporal propio verificable. |

**T, corpus existente:** PhaseOneTests.SameMetadataIsOnlyACacheHintAndRehashAllFindsChangedBytes
(línea 135) e IncompleteFolderCannotAppearAsAnExactOrSubsetCandidate (337);
PhaseTwoContentTests.PreservedMetadataEditsAreFreshContentNotCacheProof (77),
ActualContentOnlyReadDenialKeepsIndependentComponentComplete (173),
NestedOccurrencesAreReadOnceWhileHardlinkPathsAreEachFreshlyRead (282) y
ReplacedNativeIdentityWithSameSizeAndTimeIsRejectedBeforePayload (305);
PhaseFiveTests.DiscoveryUsesCapturedDiagnosticsWithoutChangingContent (205),
DiscoveryDoesNotMixDiagnosticsFromDifferentSealedSnapshots (223),
BmsonConflictReferencesPreserveEveryCandidatePolicyAndOrigin (245),
ResolverBuildRevisionIsDeclaredWhileSemanticsAreReconstructed (290) y
RepeatClassificationDeniesSourceOpensAndPreservesEveryNativeFact (454).
Rutas: tests/BmsLibraryAudit.Tests/PhaseOneTests.cs, PhaseTwoContentTests.cs y PhaseFiveTests.cs.
Son pruebas del contrato actual, **no pruebas ejecutadas de la propuesta**.

## 4. Limitación actual: una cadena cerrada, no un filtro aislado

**I:** source_snapshots.admission_policy acepta solo EXACT_ONLY; source_exact_edges exige
common=charts_a=charts_b; component_memberships y outcomes enlazan raíces a componentes.
El padre CONTENT debe contener componentes admitidos, sus hashes cubren exactamente esa unión,
y cada componente completo debe tener todas sus parejas. ValidateScope vuelve a derivar
los componentes EXACT_ONLY y las aristas desde el snapshot, incluidos sus diagnósticos.
Los consumidores 4/5 verifican además el perfil/esquema compilado.

**A:** eliminar solo Where(Kind == EXACT_ONLY) deja inconsistencias de claves,
constraints, grupos canónicos y prueba de cobertura. Cambiar esos constraints reinterpretaría
evidencia publicada. Las filas antiguas no constituyen observación de candidatos subset/overlap
que estén fuera de su selección. PendingDiscoveries conserva esa falta de admisión.

## 5. Alternativas de unidad de trabajo

| Alternativa | Ventaja | Coste/riesgo | Condición de aceptación |
| --- | --- | --- | --- |
| A. Pareja explícita | Dos raíces y una comparación; control de lectura y fallo; mínima superficie útil | Misma raíz puede leerse otra vez en otra pareja; coste acumulado; identidad de selección debe sellarse | Arista capturada reproducible, raíces completas por separado, budgets y manifiestos propios; sin cache entre sesiones |
| B. Componente RELATED completo | Inventario compartido y contexto de grupo | Componentes transitivos grandes; aristas débiles; n(n−1)/2 comparaciones; fallo de una raíz puede contaminar diseño ingenuo | Corte explícito de tamaño y relaciones, outcomes por raíz/pareja, prueba de cobertura; conectividad nunca equivale a igualdad |
| C. Selección escalonada de parejas sobre superficie compartida | Evita cierre transitivo; potencial reducción de lecturas dentro de una ventana sellada | Más claves, dependencias y estados; alias/raíces compartidas; reutilización y fallos parciales requieren otro contrato | Parejas seleccionadas explícitas, mapas de dependencia completos y reutilización únicamente demostrada dentro del mismo scope aceptado |

**P:** A para el primer incremento; B no es el valor por defecto ni se selecciona automáticamente.
C es una evolución posible si mediciones sintéticas demuestran que la duplicación de A es material.
No se paga ahora su complejidad ni se presume reutilización segura de hashes.
Un informe sobre una pareja no afirma que se haya observado todo su componente de descubrimiento.

## 6. Recomendación y alcance mínimo útil

**P:** un perfil nuevo de **observación por pareja explícita**. El primer incremento acepta
únicamente A_SUBSET_OF_B o B_SUBSET_OF_A estrictos, dos raíces disjuntas de observación,
una fuente Phase 1 capturada y una pareja seleccionada.
“Dos/una” define la unidad, no un objetivo inventado de rendimiento.
EXACT_ONLY continúa por la vía existente; HIGH/MEDIUM/SHARED quedan fuera de este primer permiso.

Se inventarían todas las entradas de ambas raíces y se leerían todos sus archivos regulares,
no solo charts comunes ni recursos referenciados. Es útil aunque el resultado físico sea conflicto
o ausencia de inclusión. Se exportan diferencias, multiplicidades y estados; paquetes/dependencias
universales y mutación permanecen desconocidos/deshabilitados.

No se recomienda habilitar en el primer cambio el consumo nuevo por Phase 4/5: es un contrato
de entrada distinto. Se entrega primero el flujo captura→PREFLIGHT nuevo→CONTENT nuevo→comparación
y exportación verificable, usando el mismo núcleo de comparación. El adaptador posterior al
clasificador existente está delimitado en §15; no se crea otro clasificador.

## 7. Modelo de admisión y saltos prohibidos

| Nivel | Evidencia necesaria | Lo que todavía no concede |
| --- | --- | --- |
| Descubrimiento | Charts y diagnósticos del mismo snapshot; hashes/set counts y arista reproducida | Inventario o contenido completo |
| Admisión física | Selección explícita ligada al digest SOURCE; raíces/identidades seguras; cobertura y revalidación completas | Igualdad de bytes de recursos |
| Admisión CONTENT | PREFLIGHT exacto aceptado, todos los nombres regulares requeridos con lectura fresca SUCCESS, stamps/identidad y cobertura coherentes | Necesidad o equivalencia funcional |
| Comparación física | Dos vistas admitidas, tipos/paths/SHA y multiplicidad; derivación reproducible | Identidad de paquete, unión segura |
| Semántica | Bytes de cada chart ligados a CONTENT y grafo reconstruido bajo contrato/modelos admitidos | Selección real, codecs, cierre universal |
| Paquete/mutación | Límites demostrados, obligaciones completas y futuro plan fresco/verificado más autorización separada | Nada de esto existe por defecto |

CHART_SUBSET→OBSERVED_TREE_SUBSET requiere inclusión real de **cada ruta, tipo y digest**.
HIGH_OVERLAP→CONTENT_OVERLAP_PROVEN requiere observar y hashear los recursos, no extrapolar charts.
SHARED_CHARTS→SAFE_UNION requiere además compatibilidad de paths, paquetes y dependencias y un plan;
no es una salida de esta propuesta.
CONTENT_MULTISET_EXACT→SAFE_TO_REMOVE requiere preservación por instancia/nombre y autoridad posterior;
hashes iguales no la suministran.
DEPENDENCY_PROVEN_WITHIN_NAMED_MODEL→GLOBAL_DEPENDENCY_CLOSURE exigiría resolver los ámbitos
expresamente excluidos del modelo; el estado nunca se renombra como cierre global.

## 8. Procedencia e identidades propuestas

**P, nombres conceptuales no registrados:**
EvidenceProfile=EXPLICIT_PAIR_OBSERVATION/1, SourceKey, ObservationUnitKey,
RootViewKey, SelectedPairKey, PreflightKey y ContentKey.
Un ID local solo tiene significado junto a perfil, base/sesión y clave canónica.
No se introduce un UnitId en un campo ComponentId histórico.

ObservationUnitKey liga digest SOURCE, política/versión de selección y las dos claves de raíz
ordenadas ordinalmente. SelectedPairKey conserva orientación canónica y etiqueta/dirección
de descubrimiento reproducida; invertir el orden del usuario no cambia qué pareja es.
Las claves de chart/nombre y hash conjunto permanecen distintas de multiplicidad y native identity.

Se conservan fuente/scan/ventana histórica, biblioteca propietaria, build de observador y validador,
límites aplicados, roots, capturas y diagnósticos. El build del resolver semántico seguiría siendo
DECLARED_UNVERIFIED_BUILD_REVISION sin un anclaje independiente; ningún nuevo sello autentica
retrospectivamente ese ejecutable por incorporar una cadena.

## 9. Unidad de observación y datos conceptuales

| Registro propuesto | Identidad/campos mínimos | Reutilización conceptual |
| --- | --- | --- |
| source_capture | SourceKey, fuente/scan exactos, digest/stamps, bibliotecas, charts y diagnósticos | Adquisición read-only y validación de PhaseOneSourceCapture |
| selection_unit / selected_pair | UnitKey, perfil, dos roots, arista y dirección, SOURCE aceptado, política, budgets | Reutilizar analizador; no candidate_components/source_exact_edges v1 |
| root_view / root_chart_membership | RootViewKey, biblioteca, paths/native facts, coverage, todos los charts capturados/observados | Roots y revalidaciones con ámbito nuevo |
| observed_entries / path_instances | PathKey, original spelling, tipo, size/mtime/attributes/native identity, chart role | preflight_entries/physical_files, con constraints específicos nuevos |
| root_memberships | RootViewKey+PathKey y ruta relativa ordinal | observation_memberships; una entrada por vista |
| read_results / root_outcomes | ContentKey+PathKey; resultado, SHA/stamps/bytes/intentos/tiempos; cobertura por root | Primitivas ReadContent, no outcomes ligados a componentes EXACT |
| pair_results / differences / content_groups | Pareja seleccionada, estados, flags/conteos, ambas entradas y multiplicidad | PhaseTwoContentComparison.Derive, fórmula sin cambios |
| audit_diagnostics / export_receipts | Scope exacto, causa, lower bounds/truncación; receipt aparte de evidencia | Separación evidencia inmutable/publicación |

Esquema conceptual, **sin SQL ejecutable ni migración**.
physical_files del legado identifica entradas regulares por path; native_identity puede repetirse.
El nuevo contrato debe evitar llamar “objetos físicos únicos” al conteo de esas filas.

## 10. Selección, raíces anidadas y superpuestas

**P:** seleccionar exactamente dos claves de carpetas Phase 1 presentes y completas del snapshot,
con bibliotecas configuradas conocidas. Verificar identidad/resolved paths y que no sean la misma
raíz, alias nativo ni ancestro/descendiente. Primer incremento: esas configuraciones BLOCKED,
sin normalizar una raíz dentro de la otra ni reducir implícitamente el scope.

Los subdirectorios dentro de cada vista sí se inventarían completos, incluidos charts y directorios
vacíos. Un contenedor conserva PACKAGE_BOUNDARY_UNKNOWN y cuesta su subtree real;
si excede el presupuesto se bloquea, no se convierte en una canción ni se muestrea.

**I/T:** las observaciones anidadas del contrato legado ya conservan membresías múltiples y leen
cada path de la unión una vez. No se les retira esa aceptación. La restricción propuesta solo
acota el contrato nuevo inicial. Una futura C deberá conservar PathKey compartida, cada ruta
relativa por raíz y cada nombre hardlink; no duplicar entradas dentro de la misma vista.

Carpetas sin charts no generan aristas del analizador actual. Los recursos contenidos en una
raíz elegida sí forman parte del inventario; una raíz hermana de recursos o un path externo
necesitan selección/descubrimiento propios, no una arista Phase 1 fabricada.

## 11. Relación con Phase 1 y conjuntos parciales

**P:** capturar la misma fuente íntegra con diagnósticos del scan exacto; reconstruir las carpetas
elegibles y la arista con RelationshipAnalyzer. La etiqueta/cached SHA es señal de selección.
A={1,2}, B={1,2,3} es subset de conjuntos y no igualdad de archivos: revalidar todos los
nombres de 1/2/3, incluidas copias duplicadas y charts exclusivos, no solo los comunes.

Para A={1,2,3}, B={2,3,4}, el diseño general conserva comunes {2,3} y exclusivos {1}/{4};
el primer incremento difiere esa etiqueta por política. Ni un subconjunto de un contenedor
ni A↔B↔C obliga a observar C o a comparar A/C.

La nueva admisión recursiva exige además que no haya diagnósticos históricos de cobertura
que intersecten la vista elegida (ancestro o subtree afectado), aunque la carpeta directa
aún fuera elegible para descubrimiento. Es una condición propuesta de observación,
no un cambio del algoritmo Phase 1. Exigir scan/captura nuevos para recuperar esa admisión.
Los hashes cacheados no se convierten en identidad física fresca por este control.

Phase 1 no persiste native identity del chart: su registro contiene paths, stamps y hashes.
El enlace nativo del archivo se establece fresco desde PREFLIGHT y sus handles; no se
autentica retrospectivamente qué objeto nativo existía durante el scan Phase 1.

## 12. Relación con Phase 2A: precondiciones y revalidación

**P:** usar la adquisición read-only/guardada, protección de todas las raíces y verificación
de fuente actuales, tras una extracción mínima de API si la firma ligada a options v1 lo exige.
No llamar StoreSource v1 para registrar la unidad nueva con admission_policy EXACT_ONLY.

1. Validar selección, SOURCE aceptado, cobertura e identidades antes de crear evidencia admisible.
2. Observar cada subtree completo y comparar pertenencia de charts contra el snapshot.
3. Releer SHA-256 de **todos los charts contenidos** que entren en las vistas, directos y anidados,
   y sus nombres/stamps/identidades; bloquear nuevos, faltantes, cambiados o no observables.
4. Conservar direct-parent discovery sets separados del conjunto recursivo observado.
5. Realizar inventario y verificación final de metadatos/membresías. Recursos no-chart no
   necesitan payload read en PREFLIGHT; readability sigue NOT_TESTED.
6. Publicar manifiesto del perfil nuevo solo con outcomes explícitos y cobertura consistente.

Revalidar todos los charts del subtree es un fortalecimiento propuesto respecto de la
revalidación directa v1; requiere pruebas/budgets propios, no se atribuye al baseline.
Un cambio de mtime desde el snapshot puede conservarse como drift documentado si todos
los bytes/SHA y nombres concuerdan; el stamp nativo fresco aceptado se fija en PREFLIGHT.
El cambio de ese stamp/identidad durante la adquisición o después del PREFLIGHT sí es STALE.
Si cambia contenido preservando size/mtime, el SHA fresco impide aceptar la relación obsoleta.
No se actualiza el snapshot ni se “repara” el sello.

## 13. Relación con Phase 2B y conservación de recursos

**P:** CONTENT acepta el nuevo PREFLIGHT exacto y recorre todos los path instances regulares
admitidos de ambas vistas: charts exclusivos, WAV/BGA, readmes, archives, recursos desconocidos
y no referenciados. Comparar namespace/stamps/identidad antes de leer y al final;
cada chart SUCCESS debe concordar con su revalidación PREFLIGHT.
No se acepta un hash de prefijo ni un digest por metadatos.

Reutilizar ReadContent y el núcleo puro Derive sobre vistas completas.
La persistencia Store/Verify actuales NO son reutilizables sin separar sus precondiciones
de componente y cobertura de todas sus parejas; el nuevo verificador exige exactamente
la pareja seleccionada y rederiva flags, diferencias y grupos.

Las diferencias ordinales y las anotaciones de colisión Windows/native alias deben
coexistir. La derivación de esas anotaciones se reutilizaría mediante una extracción
pura mínima del código aceptado de PhaseFiveClassification.Pair (líneas 27–51), con
contexto de vista tipado: no otro algoritmo ni admisión del perfil nuevo por el CLI
Phase 5 actual. Ningún FILE_ONLY se presenta automáticamente como complemento.
Ese pequeño contrato común de anotaciones también requiere revisión antes de implementarlo.

Por vista: path relativo ordinal único, tipo, SHA regular, tamaño e identidad/multiplicidad.
Hardlinks mantienen cada nombre aunque compartan native identity; el primer incremento
lee cada nombre fresco, sin cache cruzada por native ID.
Bytes iguales en paths diferentes, case collisions, exclusivos y archivos/directorios
siguen siendo dimensiones distintas. Ningún error de acceso prueba ausencia.

Un chart exclusivo B que comparte un WAV con un chart común conserva su entrada y obligación:
ni el recurso ni el chart se descartan al comparar solo conjuntos comunes.
La selección no filtra por uso semántico ni crea una distribución combinada.

## 14. Relación con Phase 3 y Phase 4

**I:** Parse es puro; Inspect es una adquisición de chart individual distinta de CONTENT.
ResolveCore y PhaseFiveAdmission exigen componente, raíz y profile legacy concretos.
Mismo SHA no permite importar sin más un dependency_resolution.json de otro scope.

**P:** el primer incremento físico informa semántica NOT_EVALUATED/UNKNOWN y preserva todos los
charts/recursos. La futura integración reutiliza parser y motor semántico, con nuevo sobre
de procedencia tipada perfil/unit/root/chart y bytes ligados al CONTENT nuevo.
No se suplanta ComponentId con UnitId. La firma AnalyzeVerified/procedencia necesita un
adaptador o separación mínima de construcción/payload antes de aceptar ese nuevo scope.

Cada chart, incluidos exclusivos y duplicados físicos, necesita su propia admisión.
Recursos compartidos por varios charts conservan todas sus declaraciones, usos, condiciones,
políticas, orígenes BMSON y obligaciones. Faltantes/UNKNOWN no impiden física independiente.
No se cambia RANDOM/SETRANDOM/SWITCH, prueba ordinal, codecs ni selección efectiva.
La versión de transporte semántico nueva deberá aprobarse antes de esa integración;
un grafo/modelo v2 conservado no autentica por sí solo un sobre de procedencia nuevo.

## 15. Interfaz con el clasificador Phase 5 existente

**P, posterior al primer incremento:** lector/adaptador explícito por perfil, nunca fallback.
Verificadores separados producen una vista validada común para comparación; IDs llevan
EvidenceScope=(perfil, selección/unit o componente, sesión, root).
Provenance y reportes nuevos distinguen componente histórico de pareja seleccionada,
expected pairs explícitas y estados por raíz/pareja.

Se conservan las fórmulas de comparación y clasificación independientes. Las partes de
PhaseFiveClassification.Pair que leen ComponentId para recursos necesitan contexto tipado
nuevo; no basta con rellenar un record antiguo. La admisión y la publicación vigentes
permanecen por su rama legacy con pruebas de no regresión.

La salida Phase 5 que admita perfiles nuevos requerirá una revisión de contrato distinta de
phase5-conservative-classification-2; no se asigna aquí una versión efectiva.
Los v2 actuales siguen significando exclusivamente el scope que ya aceptan.
PendingDiscoveries no cambia de confianza porque exista una nueva selección: solo una pareja
con CONTENT nuevo realmente admitido podrá recibir sus conclusiones físicas.
No se implementa un segundo clasificador ni se transforma conectividad en clases de equivalencia.

## 16. Versionado y conservación de sellos históricos

| Opción | Datos nuevos/reuso | Validadores y riesgo | Compatibilidad, fallo e inmutabilidad |
| --- | --- | --- | --- |
| 1. Extender v1 con nueva revisión | Nuevas selecciones/outcomes junto a tablas actuales | DDL exacto actual rechaza cambios; constraints/source domains asumen EXACT; alto riesgo de dispatcher ambiguo | Solo viable con branch estricta por versión. Nunca reabrir/migrar/reseñar terminales ni aceptar v1 con reglas nuevas |
| 2. Nuevo tipo de selección y canónicos separados dentro de una base evolutiva | Unit/pairs, raíces, hashes e índices compartidos | Facilita C; exige identificar cada fila por perfil y aislar triggers y fuente protegida | Datos fallidos/partiales segregados; más pruebas de contaminación cruzada y coexistencia de sesiones |
| 3. Nuevo perfil/esquema en base separada, primitivas comunes | Registros §9; adquisición/IO/canonical encoding/Derive compartidos | Verificadores propios de selección/root/unit; mantiene puerta legacy y su hash de esquema intactos | Coste de captura/lectura duplicado inicialmente; máxima separación de semántica histórica y fallos |

**P:** opción 3 para A. Un nuevo application/profile/schema identifier inequívoco se fija al aprobar
la implementación; no se asigna un número SQLite arbitrario en este documento.
La apertura antigua debe rechazar la base nueva y viceversa, antes de escrituras.
No se importan sesiones v1 bajo nombres nuevos ni se modifica su SchemaDigest.

Dominios propuestos, aún no registrados:
BmsLibraryAudit.ObservationPair.Source.v1,
BmsLibraryAudit.ObservationPair.Preflight.v1 y
BmsLibraryAudit.ObservationPair.Content.v1.
Reutilizar la codificación de tokens de longitud, null y UTF-8 con órdenes de grupos/campos/filas
fijados por el nuevo contrato. SOURCE nuevo sella captura/diagnósticos sin llamarse SOURCE v1.
PREFLIGHT sella digest SOURCE, selección, política/budgets, roots/membresías/revalidaciones/entries/outcomes.
CONTENT sella padre aceptado, lecturas/outcomes/diagnósticos, parejas seleccionadas y resultados
derivados completos; **también deben reconstruirse** esas comparaciones, aunque queden en el digest.
Esta inclusión adicional propuesta no cambia los cuatro grupos CONTENT v1.

Un terminal exitoso es inmutable; un fallido/cancelado queda como auditoría sin digest de éxito.
Outcomes incompletos pueden tener cierre auditado coherente, pero no comparaciones exactas.
No hay reanudación automática, resealing, reparación de datos ni cache intersesión.
Timestamps de adquisición son históricos; exportar otra vez la misma evidencia no añade
la hora de exportación a su payload determinista. Receipts independientes no cambian el sello.

## 17. Seguridad y superficie de lectura/escritura

**P:** Windows y las mismas garantías mínimas nativas/rollback de fuente; bases solo del proyecto.
Nunca escribir índices fuente/simuladores, preservar bytes/mtime/attributes/native IDs y evitar
sidecars persistentes nuevos. Lecturas fuente autorizadas solo durante captura/revalidación/hashing,
dentro de las dos vistas y el Phase 1 DB declarado; clasificar/reexportar solo evidencia histórica.

No seguir reparse points, aliases de destinos, rutas referenciadas por charts ni targets externos.
Proteger todas las raíces de biblioteca conocidas, source DB, evidence DB, inputs e informes;
destinos nuevos/vacíos y no-overwrite mediante la publicación común y bindings revalidados.
Errores de guardas globales son rechazo, no “incompletitud tolerable” para escribir en origen.
No se promete ausencia universal de TOCTOU, snapshot instantáneo ni seguridad destructiva.

## 18. Fallos parciales, estados y aislamiento

| Capa/causa | Estado propuesto y efecto | Qué puede conservarse |
| --- | --- | --- |
| Selección sin arista/scope fuera de política | BLOCKED antes de CONTENT | Señal histórica/diagnósticos, ninguna raíz admitida por inferencia |
| Captura o sello/esquema/namespace global incoherente | FAILED o STALE de unidad/sesión; nada nuevo aceptable | Solo auditoría; otras unidades ya selladas intactas |
| Chart nuevo/cambiado/faltante en PREFLIGHT | SOURCE_CANDIDATE_CHANGED; unidad no admitida | Snapshot original, no relación fresca inventada |
| Raíz con inventario incompleto | Root INCOMPLETE, pareja BLOCKED | Entradas observadas como lower bounds, charts y obligaciones retenidos |
| Recurso ilegible/lectura inestable con identidad y stamps estables | Root CONTENT INCOMPLETE; cierre auditado posible, pareja ineligible | Lecturas SUCCESS históricas y otra raíz completa, sin igualdad parcial de pareja |
| Drift de path/identidad/stamps antes/durante/final de CONTENT | STALE; primer incremento invalida la unidad entera | No se degrada a read error local para salvar una comparación |
| Cancelación | CANCELLED; sin digest de éxito ni reanudación | Auditoría terminal y temporales propios bajo guardas |
| Comparación/exposición excede presupuesto | INCOMPLETE o exportación rechazada | Evidencia sellada intacta; scope no clasificado explícito |
| Escritura/publicación falla después de sellar | FAILED_EXPORT independiente | Sello no se modifica; nueva exportación a otro destino tras verificación |

Root COMPLETE requiere cada entrada necesaria; Pair COMPLETE requiere ambas raíces completas
y comparación reproducida. Global Coverage puede ser INCOMPLETE con raíces/parejas independientes
válidas, como en el baseline. La publicación global no concede autoridad.

Con A cada pareja es una unidad independiente: fallo A/B no invalida un C/D ya sellado.
No aparece una comparación A/C por estar C completa. Para C escalonada, un fallo tendría que
propagarse por el mapa de membresías a todas sus parejas incidentes; pérdida de confianza en
fuente/namespace compartido afecta toda esa sesión. Esa optimización no forma parte del primer
contrato y no se promete aislamiento que la adquisición global actual no proporciona.

## 19. Presupuestos, coste y plan futuro de medición

No se fijan SLAs ni números normativos nuevos. La ejecución futura deberá declarar límites
positivos y finitos en cada capa, persistirlos y detenerse sin muestreo silencioso:

| Capa | Presupuestos/contadores | Costes a medir |
| --- | --- | --- |
| Descubrimiento/captura | Charts, carpetas, diagnósticos, bytes DB; aristas examinadas | Fingerprint fuente O(bytes DB); índice SHA; un hash en k carpetas genera k(k−1)/2 pares |
| PREFLIGHT | Raíces/parejas seleccionadas, charts a revalidar, entradas, profundidad, diagnósticos y bytes de charts | Recorridos O(N) en entradas, hashing de charts, ordenamiento/índices SQLite y guardas |
| CONTENT | Path instances, bytes previstos/reales, intentos, errores y cancelación | SHA O(bytes leídos); cada nombre hardlink fresco; sin dedup por size/mtime |
| Comparación | Parejas explícitas, entradas acumuladas, grupos/diferencias y memoria | Derive materializa vistas; ordenamientos y SQLite por pareja; general n² si se autorizara todo |
| Semántica/clasificación posterior | Informes/bytes, relaciones/candidatos, conflictos, referencias | Fan-out chart→resource; grafos incorporados e índices |
| Salida/operación | Bytes JSON/CSV, memoria pico, tiempo transcurrido, diagnósticos | Serialización puede duplicar memoria antes del cap; tiempo/pico no garantizados por límite de bytes |

**P, experimento futuro no ejecutado:** generador propio con ejes cantidad de charts, files,
payload bytes, densidad de aristas, duplicados/hardlinks y fan-out; comenzar con fixtures pequeñas
que tengan oráculos independientes y aumentar una variable por vez.
Comparar A repetida frente a C experimental en un banco separado, con mismo contenido conocido;
medir bytes/handles abiertos por path, CPU/wall clock, memoria pico, filas/consultas/ordenamientos
SQLite, tamaño de reportes, tiempo hasta cancelación y coste de VerifyContent completo frente
al clasificador acotado. Repeticiones y metadatos del entorno permiten explicar ruido; no se
prometen cifras hasta medir.

Aceptar cualquier reutilización futura solo si el experimento prueba membresía, mismo contexto
CONTENT aceptado y cobertura/stamps compatibles. Un hash anterior no demuestra frescura:
entre sesiones la opción inicial es releer. No se hace benchmark sobre bibliotecas personales.
Los números del baseline sintético no son límites de ingeniería para millones de archivos.

## 20. Matriz sintética propuesta (54 casos, NO EJECUTADOS como contrato nuevo)

**F:** primer incremento subset. **G:** contrato general futuro, fuera de F.
**L:** control del perfil legado ya implementado, no ampliar la admisión nueva para forzarlo.
**B:** BLOCKED. **S:** STALE. **I:** INCOMPLETE. **C:** CANCELLED. **S4:** adaptación semántica posterior.
Los casos exactos/multisets completos que no pueden surgir de un subset estricto usan L o
pruebas puras del núcleo, no fixtures imposibles ni evidencia CONTENT fabricada.

**X, prohibido en cada fila:** equivalencia de paquete, fusión/eliminación/autoridad o cualquiera
de los cinco saltos de §7 sin su prueba adicional; el resultado particular añade sus límites.
**R0, resultado ante incompletitud en cada fila:** antes de admisión BLOCKED; inventario/read
incompleto conserva UNKNOWN/lower bounds e impide comparación exacta; drift STALE; cancelación
CANCELLED. Nunca convertir truncación, ausencia de candidato o falta de reporte en prueba negativa.
Los scopes G/L siguen su política explícita y no se admiten por pasar el kernel de comparación.

| ID | Fixture controlada | Relación Phase 1 | Admisión esperada | Evidencia física requerida | Resultado permitido y límites específicos | Prohibido | Si incompleta |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C01 | A=B={1,2} | EXACT_CHART_SET | L / control G | Árbol exacto si todas las entradas coinciden | Igualdad física, no PACKAGE_EXACT | X | R0 |
| C02 | A={1,2}, B={1,2,3} | A_SUBSET_OF_B | F | Revalidar los cinco nombres de charts y todos los recursos | Subset físico solo si paths/tipos/digests lo prueban | X | R0 |
| C03 | A={1,2,3}, B={1,2} | B_SUBSET_OF_A | F | Orientación canónica más dirección conservada | Resultado direccional inverso, no redundancia | X | R0 |
| C04 | A={1..5}, B={1..4,6} | HIGH_OVERLAP | G; F difiere | Inventarios completos y chart exclusivo por lado | Solapamiento de charts no prueba contenido compartido de recursos | X | R0 |
| C05 | A={1,2,3}, B={2,3,4} | MEDIUM_OVERLAP | G | Comunes {2,3}; exclusivos {1}/{4}, recursos de todos | Ninguna distribución combinada inferida | X | R0 |
| C06 | A={1..4}, B={4..7} | SHARED_CHARTS | G | Un solo hash común; todas las entradas necesarias | Sin SAFE_UNION | X | R0 |
| C07 | A={1}, B={2} | Sin arista | B | No inventar selección Phase 1; kernel control separado | Relación por recursos requiere otro mecanismo | X | R0 |
| C08 | Dos nombres de chart 1 en A, uno en B más chart 2 | A_SUBSET_OF_B | F | Nombre/entry/native/hash y conteos de copias | Conjunto no es multiset; ninguna copia eliminable | X | R0 |
| C09 | A↔B↔C, A sin hash con C | Grafo relacionado | G; F una pareja | Solo aristas explícitamente seleccionadas | No comparación A/C implícita ni transitividad de igualdad | X | R0 |
| C10 | Archivo chart cambia bytes con mismo size/mtime | Señal cacheada anterior | S | SHA fresco PREFLIGHT/CONTENT contra snapshot | Bloquear antes de usar relación obsoleta | X | R0 |
| C11 | Charts legibles + DIRECTORY_UNREADABLE | Carpeta incompleta | B | Chart/diagnóstico histórico conservado | Sin afirmar subconjunto completo ni ausencia de relación | X | R0 |
| T01 | Dos árboles idénticos completos | EXACT | L / control G | Entradas regulares/directorios, paths y SHA iguales | TREE_EXACT no PACKAGE_EXACT | X | R0 |
| T02 | Charts subset y todos los paths A incluidos en B | SUBSET | F | Entradas B adicionales explícitas | OBSERVED_TREE_SUBSET no REDUNDANT | X | R0 |
| T03 | Mismos payloads y conteos, nombres distintos | EXACT control | L / control G | Multiset completo con misma multiplicidad | Multiset exacto, árbol distinto; no sustitución | X | R0 |
| T04 | Dos nombres del mismo SHA en A, uno en B | EXACT control | L / control G | Conteos 2/1 independientes de native IDs | Multiset distinto | X | R0 |
| T05 | kick.wav/KICK.WAV con iguales bytes | SUBSET o control EXACT | F / L | Spelling ordinal y case collision conservados | Mismos bytes no eliminan colisión ni conceden complemento | X | R0 |
| T06 | Misma ruta kick.wav con bytes distintos | SUBSET | F | Ambos SHA/identidades y referencias | Conflicto duro, ningún ganador | X | R0 |
| T07 | Audio exclusivo A, imagen exclusiva B | SUBSET | F | Exclusivos y todas las rutas comunes comprobados | Complemento observable solo sin conflictos reconocidos; no unión segura | X | R0 |
| T08 | Auxiliares notes.txt/archive exclusivos | SUBSET | F | Regular files completos, sin filtro por uso/extensión | Diferencia genérica, no dependencia inventada | X | R0 |
| T09 | Directorio vacío adicional | SUBSET | F | Entrada de directorio aunque no tenga bytes | Estructura diferente, nunca descartarlo por cero payload | X | R0 |
| T10 | Path recurso archivo vs directorio | SUBSET | F | Tipos por raíz completos | Conflicto de tipo, no complemento | X | R0 |
| T11 | Hardlinks con dos nombres | SUBSET | F | Dos path entries; native identity compartida; cada nombre leído | Mantener multiplicidad; no dedup por native ID | X | R0 |
| T12 | Raíces A y A/Sub seleccionadas | Cualquier arista | B en F; G necesita contrato | Rechazo explícito de selección superpuesta | No reinterpretar ancestro como paquete | X | R0 |
| T13 | Raíz contiene subcarpetas con más charts | SUBSET directo | F con cobertura recursiva | Revalidar todos los charts contenidos y observar recursos de todos | CONTAINER/BOUNDARY_UNKNOWN, no una canción inferida | X | R0 |
| T14 | Carpeta de recursos sin charts | No participa en Phase 1 | B | Descubrimiento separado pendiente | No arista de chart fabricada ni expansión por referencia | X | R0 |
| D01 | Chart común, recurso igual | SUBSET | F física; S4 después | Inventarios completos; admisión semántica por chart si se quiere vínculo | Igualdad de bytes no cierre universal | X | R0 |
| D02 | Chart común, WAV/BGA variante | SUBSET | F física; S4 después | Conflictos + declaraciones/usos admitidos para etiqueta potencial | Sin diferencia de reproducción demostrada | X | R0 |
| D03 | Chart exclusivo B usa recurso compartido con común | SUBSET | F física; S4 después | No excluir chart B ni recurso; conservar ambas obligaciones | No descartable por compartir hash o chart común | X | R0 |
| D04 | Chart exclusivo con recurso exclusivo | SUBSET | F física; S4 después | Hash chart/recurso y path de B | Ninguna promesa de redundancia | X | R0 |
| D05 | WAV declarado sin uso sintáctico | SUBSET | F física; S4 después | Declaración/candidato conservados | No UNUSED/removible | X | R0 |
| D06 | RANDOM alterna dos recursos | SUBSET | F física; S4 después | Ambas alternativas/modelos y candidatos | Sin elegir una rama para eliminar otra | X | R0 |
| D07 | Control o extensión semántica UNKNOWN | SUBSET | F física; sem UNKNOWN | Inventario completo independiente del grafo | UNKNOWN no prueba prescindibilidad | X | R0 |
| D08 | BMSON stem.wav + stem.ogg/.custom | SUBSET | F física; S4 después | Literal/propuesta, policy/origin separados | Sin codec/orden/selección efectiva | X | R0 |
| D09 | Faltan uno o más informes Phase 4 | SUBSET | F física; sem UNKNOWN | Todos los charts contabilizados, faltantes explícitos | Físico completo posible, cobertura semántica no completa | X | R0 |
| D10 | Referencia ../fuera o recurso de raíz no admitida | SUBSET | F inventario; sem OUT_OF_SCOPE | No abrir path referenciado; conservar obligación | No ampliar la superficie ni afirmar ausencia absoluta | X | R0 |
| S01 | Archivo aparece/desaparece tras PREFLIGHT | SUBSET previo | S | Verificación de namespace anterior/final | No completar CONTENT con subconjunto disponible | X | R0 |
| S02 | Objeto sustituido, mismo size/mtime | SUBSET previo | S | Identidad nativa y handle contra esperada | No aceptar por cache | X | R0 |
| S03 | Reparse/junction en raíz o subtree | Candidata | B/S | Nunca seguir target; diagnóstico y scope afectado | Target sin payload reads | X | R0 |
| S04 | Directorio actualmente inaccesible | Candidata | B | Cobertura desconocida y counts lower-bound | Sin inventario COMPLETE | X | R0 |
| S05 | Recurso ilegible con stamps estables | SUBSET admitido | I | ERROR sin SHA, raíz afectada INCOMPLETE | Sin comparación exacta de pareja; otra unidad sellada independiente sobrevive | X | R0 |
| S06 | Lectura parcial/EOF inesperado | SUBSET admitido | I o S si drift | Retry acotado solo mismo stamp; registrar todos los intentos | Nunca hash de prefijo como SUCCESS | X | R0 |
| S07 | Cancelación en captura/hash/antes de seal | Candidata | C | Token cooperativo, cierre de handles, terminal audit | Sin manifiesto de éxito ni reanudación automática | X | R0 |
| S08 | Base/esquema corrupto | Cualquier | B global | Header/schema/integridad antes de aceptar datos | Sin repair/migración | X | R0 |
| S09 | Digest aceptado incorrecto | Cualquier | B global | Recalcular y comprobar padre exacto | No iniciar lectura CONTENT | X | R0 |
| S10 | Root/hash/manifest de sesiones distintas | Cualquier | B global | FK/IDs/dominios y perfiles cerrados | Sin mezclar evidencia ni compilar un COMPLETE ficticio | X | R0 |
| S11 | Salida dentro de biblioteca o alias de DB | Cualquier | B antes de escribir | Exclusión nativa de todas las raíces/inputs | Fuentes e inputs intactos | X | R0 |
| S12 | Reporte/temporal ajeno preexistente | Cualquier | B publicación | CreateNew/no-overwrite, ownership revalidado | No borrar ni reemplazar archivo ajeno | X | R0 |
| P01 | Componente conectado grande con muchas aristas | HIGH/SHARED | F solo una pareja; G acotado | Seleccionar explícitamente, no expandir cierre | Sin trabajo n(n−1)/2 accidental | X | R0 |
| P02 | Muchas parejas comparten un chart/raíz | SUBSET/overlap | G futuro | Medir lecturas repetidas e índices; reuse deshabilitado en F | No reutilizar hash por timestamps | X | R0 |
| P03 | Muchos recursos compartidos por charts | SUBSET | F limitado | Contar entries/bytes y fan-out semántico separados | No colapsar nombres u obligaciones | X | R0 |
| P04 | Presupuesto de charts/entries/bytes agotado | Candidata | B/I | Límite explícito, no muestreo silencioso | Sin COMPLETE de observación parcial | X | R0 |
| P05 | Presupuesto de salida/comparación/diagnósticos | Admitida | I o rechazo de export | Conservar evidencia; scope pendiente y cutoff explícito | No enumeración truncada como ausencia | X | R0 |
| P06 | A/B/C, A/B falla y A/C no seleccionado | G futuro | Estado por unidad | C completa no autoriza una arista nueva | No comparación inferida; unidades disjuntas ya completas conservadas | X | R0 |
| P07 | Filas/roots/policies reordenados; mismo input | Todas admitidas | F/G | Orden canónico y reexport byte-identical | No first-wins ni timestamps de export en evidencia | X | R0 |

Los SHA esperados deben calcularse de payloads conocidos independientes; registrar nombres,
arrays de entradas/directorios, grupos hash→conteo y oráculos de conflictos. Las pruebas de
preservación comparan bytes/native facts antes/después y sidecars; las de clasificación niegan
apertura de originales después de adquirir CONTENT. Comparar exportaciones con filas e inputs
reordenados. Las pruebas del baseline ejecutadas ahora no sustituyen estos experimentos futuros.

## 21. Primer incremento implementable y límites de entrega

**P, especificación para un prompt posterior:**

1. **Baseline futuro:** SHA publicado de esta investigación tras revisión independiente; no inventarlo ni usar automáticamente 8690aa5 como autorización nueva.
2. **Entrada:** fuente Phase 1/último scan esperado y digest de captura aceptado; dos claves de folder, dirección subset reproducida, budgets explícitos y destinos protegidos. Los nombres de futuros argumentos se fijan al aprobar contrato; no hay comando nuevo disponible hoy.
3. **Precondiciones:** perfil nuevo seleccionado, fuente íntegra, diagnósticos de scope no bloqueantes, dos roots seguros/disjuntos y al menos un chart común con subset estricto; ninguna relación por score.
4. **Unidad:** una pareja, dos vistas completas. No cerrar componente RELATED ni descubrir recursos externos.
5. **Revalidación:** toda pertenencia/chart de ambas vistas, SHA fresco y native facts; snapshot/fresh relation coherentes, incluyendo charts exclusivos/nested.
6. **Lectura:** fuente declarada read-only, charts en PREFLIGHT y todos los regulares del inventario en CONTENT; nunca paths propuestos por charts.
7. **Persistencia:** registros §9 en base separada, source facts/diagnósticos, selección, miembros, lower bounds y outcomes, sin escribir fuentes.
8. **Sellado:** dominios/órdenes nuevos aprobados, padre/digest exactos, estados terminales y constraints. No modificar dominios, SchemaDigest o sesiones históricas.
9. **Comparación:** una pareja solo si dos vistas COMPLETE; Derive existente; verificar flags/drilldowns/grupos contra inventario/hash admitidos.
10. **Informe:** procedencia tipada, charts comunes/exclusivos con copias, árboles/multisets, diferencias/conflictos, estados individuales/globales, limits y autoridad false. No Phase 5 semántica positiva por defecto.
11. **Bloqueos:** corrupción/sellos/aliases/cobertura histórica insuficiente y configuraciones fuera de política; no convertirlos en ausencia.
12. **UNKNOWN:** paquete, función/codec, dependencia fuera de scope y semántica no evaluada; chart exclusivo nunca descartado por falta de Phase 4.
13. **Cancelación:** cooperativa por recorrido/buffer/verificación/publicación; terminal auditado sin éxito parcial ni resume.
14. **Fuentes:** exclusión nativa de todos los roots/inputs; read-only, stamps antes/después, no sidecars persistentes y publicación segura.
15. **Pruebas de corrección:** C02/C03/C08/C10/C11, T02/T05–T13 y controles de esquema/canonical/padre/pertenencias, con oráculos independientes.
16. **Pruebas de preservación:** S01–S12, hash/native facts, no-overwrite/cancel/falla, apertura de originales bloqueada al exportar, baseline legacy completo y seis smokes preservados más smoke específico futuro.
17. **Fuera de alcance:** overlap general/HIGH/MEDIUM/SHARED y no-chart discovery, batches C, cache cruzada, Phase 4/5 nuevo transporte/admisión, operaciones reales, paquetes, mutaciones y Phase 6.

**Aceptación útil:** el flujo completo de una pareja subset genera evidencia nueva reproducible y
puede demostrar inclusión física cuando sea verdadera o conservar conflictos/exclusivos cuando no;
el verificador rechaza faltantes/mezclas/adulteraciones y las sesiones viejas se abren igual.
No basta con un diseño, un CSV sin sello o un flag tree_exact deserializado.
Este flujo todavía no existe y no se implementa en esta intervención.

## 22. Riesgos que permanecen abiertos

Cierre universal; selección efectiva del simulador; codecs/fallback universales; referencias
implícitas/fuera de raíz; equivalencia de charts distintos; límites reales de paquete; frescura
instantánea de evidencia histórica; semántica de auxiliares; equivalencia acústica; seguridad
de fusión, bytes recuperables o eliminación; escalabilidad real.
Se conservan como límites, sin hacerlos prerequisitos imposibles para la comparación física útil
ni declararlos resueltos por omisión.

## 23. Preguntas materiales y decisiones de revisión

| Pregunta | Decisión propuesta / evidencia faltante |
| --- | --- |
| A o C inicialmente | A resuelve el caso útil con menor contrato. C requiere medición del coste repetido y modelo de reutilización/aislamiento |
| Subset u overlap a la vez | Solo subset estricto inicialmente; todos los demás quedan explícitamente fuera de su política |
| Nested roots | Bloquear selección superpuesta en perfil nuevo inicial; preservar soporte legacy y subtree completo |
| Cuánto hashear de charts | Todos los charts contenidos para ligarlos al snapshot; coste debe medirse y presupuestarse |
| Identificadores de perfil/canonical/application | Fijar registro cerrado durante aprobación del contrato antes de implementar; no reutilizar identificadores legacy |
| Límites numéricos | Provisionales de laboratorio, declarados por ejecución y revisados con mediciones; ningún SLA inventado |
| Hashes entre sesiones | Deshabilitados: no existe prueba de frescura que justifique evitar lecturas |
| Phase 4/5 | Adaptación posterior tipada, con revisión de transporte/contrato y mismo motor; no overload de ComponentId |

Estas decisiones no dependen de observar una biblioteca real. No queda una razón para otra
investigación genérica de duplicados; la revisión debe decidir este scope y sus gates concretos.

## 24. Criterios de aprobación independiente

Aprobar la propuesta solo si:
- Selección/arista y diagnósticos se vinculan a una sola captura; root completo no significa paquete.
- Identidad de nombres, native IDs, SHA, multiplicidad y views se mantienen separadas.
- Ambas raíces y todos sus charts/recursos entran en la obligación completa; no solo los comunes.
- Dominios/esquema nuevos no reinterpretan ni migran SOURCE/PREFLIGHT/CONTENT v1.
- Outcomes por root/pareja y fallos de namespace están definidos sin “salvar” igualdad parcial.
- Completo significa cobertura reproducida; ni SQL flags, CSV ni JSON arbitrario suministran prueba.
- Futuros adaptadores conservan precondiciones o las reemplazan explícitamente con un contrato aprobado.
- Budgets/diagnósticos/export deterministas no convierten UNKNOWN en ausencia/autoridad.
- La matriz tiene oráculos y failure injection, y se conserva íntegra la regresión legacy.
- Se autoriza por separado un baseline publicado y el primer incremento sintético preciso.

## 25. STOP y autorizaciones posteriores

Este documento y la actualización de ROADMAP no habilitan nuevas observaciones.
No se modificaron código, tests, smokes, contratos vigentes, versiones, esquema, sellos ni CLI.
No se accedió a datos reales ni se generó un plan destructivo.
Los smokes existentes ejecutados ahora son exclusivamente fixtures sintéticas del programa actual.

Siguiente secuencia obligatoria: revisión independiente de este diseño → publicación autorizada
de la investigación → nuevo SHA verificado → autorización explícita del incremento de §21.
No stage, commit, push ni Phase 6 en esta tarea. **STOP para revisión independiente.**

