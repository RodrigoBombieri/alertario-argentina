# Backlog profesional

Jerarquía: **EPIC → FEATURE → USER STORY → TASK**. Estado inicial de implementación: todo **pendiente**. Fase 0 produjo investigación/documentos, no cierra historias de software. Tallas relativas S/M/L; orden por fases del [ROADMAP](ROADMAP.md), no por atractivo visual. Responsables son roles a asignar.

**F1 no aceptada:** la evaluación exploratoria del 2/10/2026 terminó con [resultado NO-GO](docs/research/f1/CLOSURE.md). US-01, US-02 y US-03 no cumplen aceptación y siguen abiertas; EXT-01–05 registran los desbloqueos externos. El inventario y las sondas no son una allowlist de producción.

**F2 no aceptada:** [corte de backend sintético y contrato HTTP](docs/implementation/F2.md) para US-04. Las rutas, OpenAPI y pruebas están implementadas, incluida una prueba de aislamiento con wrappers INA sintéticos. Falta revisión de producto/datos del contrato mínimo; los faltantes F1 por motivos externos siguen abiertos y no habilitan datos reales.

**F3 iniciada:** [normalización INA y GeoRef con fixtures sintéticas](docs/implementation/F3.md) para US-05/US-07. Conserva identidad de estación/red/serie y localidades, y ensambla snapshots completos en memoria; no hay adapter HTTP, intercambio persistente ni series aprobadas para la API pública.

## E01 — Fuentes y confianza

### F01 — Contratos verificables

**US-01 · MUST · L · F1 · Dependencia F0 · Responsable integraciones/legal**

Como responsable del producto quiero conocer permisos, contratos y restricciones de cada fuente para publicar solo datos habilitados.

- TASK-01.1: crear ficha por red/dataset con titular, licencia, atribución, cuota y retención.
- TASK-01.2: preparar consulta al organismo sobre pendientes; enviar solo mediante acción explícitamente autorizada.
- TASK-01.3: registrar decisión approved/pending/denied y allowlist de series; excluir `public=false`.
- Acceptance: **Given** una fuente sin permiso resuelto, **When** se intenta habilitarla en producción, **Then** la validación la rechaza. **Given** permiso vigente documentado, **Then** fuente y atribución aparecen en ficha pública.
- Tests: configuración y filtros de publicación; evidencia documental revisada. Tamaño L por dependencias externas.

**US-02 · MUST · L · F1 · Dep US-01 · Responsable datos**

Como responsable de datos quiero un piloto con series medibles y referencias conocidas para que las comparaciones sean válidas.

- TASK-02.1: inventariar 10–30 estaciones candidatas, series/variables/procedimientos y cobertura.
- TASK-02.2: medir 14 días de cadencia, retrasos, nulos y correcciones con presupuesto aprobado.
- TASK-02.3: confirmar datum, unidad, epoch y vigencia de umbrales; definir C/L/ε por serie.
- Acceptance: **Given** serie diaria, **When** se aprueba su capacidad, **Then** no se promete Δ1 h. **Given** datum desconocido, **Then** comparación de umbral permanece deshabilitada.
- Tests: informe reproducible y fixtures de casos observados; no benchmark de carga al organismo.

**US-03 · MUST condicionado · L · F1/F3 · Dep US-01 · Responsable integraciones**

Como ciudadano quiero avisos meteorológicos oficiales para mi localidad para consultar lo emitido por SMN.

- TASK-03.1: ratificar feed machine-readable actual, ámbito SAT/ACP, cancelaciones y perfil de severidad/color.
- TASK-03.2: documentar esquema y probar mensaje, update, cancel y consulta vacía completa.
- TASK-03.3: implementar adapter solo del canal autorizado; no fijar archivos CAP fechados.
- Acceptance: **Given** feed no confirmado, **When** se publica beta reducida, **Then** informa indisponibilidad y no se cierra el MUST. **Given** aviso vigente aplicable, **Then** conserva texto, emisor y fechas.
- Tests: CAP fixtures, lifecycle y completitud; gate obligatorio del MVP completo.

## E02 — Plataforma de datos

### F02 — Backend propio y normalización

**US-04 · MUST · S · F2 · Dep US-02 contrato mínimo · Responsable backend**

Como desarrollador móvil quiero un contrato propio estable para no depender del schema gubernamental.

- TASK-04.1: hosts .NET/DI/health, DTO y OpenAPI propia.
- TASK-04.2: response summary fixture, errores ProblemDetails y versionado.
- TASK-04.3: CI backend aislada sin red oficial.
- Acceptance: **Given** fixture INA cambia de wrapper, **When** se modifica adapter, **Then** el contrato mobile permanece igual.
- Tests: API contract; test de arquitectura Core sin infraestructura.

**US-05 · MUST · M · F3 · Dep US-04/US-02 · Responsable backend/datos**

Como usuario quiero que los valores tengan unidades y fechas consistentes para poder interpretarlos.

- TASK-05.1: implementar Ina adapter y modelos externos privados.
- TASK-05.2: unidad heredada validada, timestamps, nulos y cuarentena.
- TASK-05.3: diferencia observada/simulada/promedio; detectar schema drift.
- Acceptance: **Given** `unit_id:null` y serie aprobada en m, **Then** hereda m; **Given** conflicto de unidad, **Then** cuarentena y no valor engañoso.
- Tests: adapter fixtures y invariantes de tiempo/unidad.

### F03 — Persistencia e ingesta

**US-06 · MUST · M · F4 · Dep US-05 · Responsable backend**

Como operador quiero ingesta idempotente y recuperable para no perder ni duplicar información.

- TASK-06.1: DB/PostGIS, constraints, migraciones y latest.
- TASK-06.2: worker, lease, checkpoints y reconciliación; revisar `next_page`.
- TASK-06.3: revisiones, cuarentena durable, retries/circuit breaker.
- Acceptance: **Given** crash tras persistir un lote, **When** se reinicia, **Then** no duplica filas ni avanza sobre datos perdidos. **Given** fuente sin nuevas lecturas, **Then** no rejuvenece datos.
- Tests: integración real DB, concurrencia, rollback y replay.

## E03 — Localidades y seguimiento hidrológico

### F04 — Búsqueda y selección

**US-07 · MUST · M · F3/F6 · Dep US-04 · Responsable backend/mobile**

Como ciudadano quiero buscar mi localidad y provincia para evitar confusiones con nombres iguales.

- TASK-07.1: importer GeoRef snapshot versionado y búsqueda normalizada.
- TASK-07.2: UI resultados con provincia/categoría y selección.
- Acceptance: **Given** localidades homónimas o entidad/localidad simple, **When** busco, **Then** puedo distinguirlas y no se fusionan por nombre.
- Tests: IDs textuales, acentos, paginación fallida conserva catálogo.

**US-08 · MUST · L · F3/F4/F6 · Dep US-02/US-07 · Responsable geoespacial**

Como usuario quiero ver estaciones cercanas y su río para elegir una referencia adecuada.

- TASK-08.1: consulta ST_DWithin y distancias en metros.
- TASK-08.2: asociación curada localidad/estación con vigencia y justificación.
- TASK-08.3: UI distancia/advertencia de representatividad; GPS opt-in.
- Acceptance: **Given** estación más cercana en otro curso, **Then** no asigna automáticamente el estado del río a mi localidad. **Given** ningún sensor aprobado en radio, **Then** explica ausencia.
- Tests: geografía, bordes, GPS denegado y punto lejos de cuenca.

### F05 — Ficha, variaciones y estado

**US-09 · MUST · M · F5/F6 · Dep US-05/US-06 · Responsable backend/mobile**

Como ciudadano quiero ver altura, caudal disponible, fuente y fecha para saber qué se está midiendo.

- TASK-09.1: summary por variable, resolución/unidad y clocks independientes.
- TASK-09.2: UI primaria, detalle avanzado y estados vacíos/stale.
- Acceptance: **Given** altura reciente y caudal viejo, **Then** no presentan la misma frescura; **Given** valor null, **Then** no muestra 0 m.
- Tests: contract/widget y combinación de estados.

**US-10 · MUST · M · F5 · Dep US-02/US-06 · Responsable backend**

Como usuario quiero conocer cuánto cambió el nivel en 1/3/6/12/24 h para entender su evolución.

- TASK-10.1: motor puro Δ, tolerancia y motivos de insuficiencia.
- TASK-10.2: tendencia por ventana y principal con fallback explícito.
- TASK-10.3: casos TR completos y metodología versionada.
- Acceptance: **Given** 7,48 y 7,32 hace 6 h comparables, **Then** +16 cm; **Given** solo muestras diarias, **Then** Δ6 h no disponible.
- Tests: TR-01 a TR-19.

**US-11 · MUST · M · F5/F6 · Dep US-02/US-09 · Responsable producto/datos**

Como usuario quiero distinguir umbrales y avisos emitidos para no confundir un cálculo con una decisión de autoridades.

- TASK-11.1: umbrales versionados con evidencia/datum.
- TASK-11.2: estado de tres ejes y copys accesibles.
- Acceptance: **Given** cruce del nivel de evacuación sin orden oficial, **Then** muestra comparación y nunca una orden de evacuar.
- Tests: matriz de estados y revisión de comprensión ciudadana.

### F06 — Favoritos y offline

**US-12 · MUST · S · F6 · Dep US-07/US-09 · Responsable mobile**

Como usuario quiero guardar una localidad o estación para consultar rápidamente su estado.

- TASK-12.1: tabla SQLite Favorite tipada y orden local.
- TASK-12.2: agregar/eliminar desde búsqueda/detalle y lista.
- Acceptance: **Given** sin cuenta, **When** guardo estación y reinicio app, **Then** permanece; eliminar repetidamente no falla.
- Tests: persistencia y widget favoritos vacíos/retirados.

**US-13 · MUST · M · F6 · Dep US-09/US-12 · Responsable mobile**

Como usuario con conectividad limitada quiero consultar lo último descargado sabiendo su antigüedad.

- TASK-13.1: repositorio API+cache, migración DTO/cache y clock.
- TASK-13.2: banner offline, hora absoluta y gráfico cacheado.
- Acceptance: **Given** avión activado, **Then** último dato es visible con cache/edad; no se registra una actualización exitosa ni se inventan avisos nuevos.
- Tests: desconexión, cache incompatible, hora del dispositivo alterada.

## E04 — Visualización

### F07 — Históricos

**US-14 · MUST reciente / SHOULD extendido · M · F6/F9 · Dep US-06/US-09 · Responsable backend/mobile**

Como usuario quiero ver la evolución para comprender el cambio de nivel.

- TASK-14.1: consulta 24 h/7 d/30 d limitada y ordenada.
- TASK-14.2: gráfico accesible, huecos, umbrales válidos por período.
- TASK-14.3: backfill y agregados con min/max/count cuando se amplíe.
- Acceptance: **Given** hueco de observaciones, **Then** curva no lo atraviesa como si hubiera datos; **Given** backfill, **Then** no envía push histórico.
- Tests: paginación, picos y rango vacío; lectura con lector de pantalla.

### F08 — Mapa

**US-15 · SHOULD · M · F7 · Dep US-08/US-11 · Responsable mobile/geoespacial**

Como usuario quiero explorar estaciones en un mapa y filtrar por río o provincia.

- TASK-15.1: spike MapLibre + proveedor de tiles autorizado.
- TASK-15.2: bbox, clusters, filtros, selección y fallback lista.
- Acceptance: **Given** cluster con datos viejos y aviso oficial, **Then** no presenta nivel promedio ni estado seguro; al abrir permite inspeccionar estaciones.
- Tests: zoom/cluster/dispositivo, tiles caídos y atribución visible.

## E05 — Avisos y notificaciones

### F09 — Avisos por zona

**US-16 · MUST condicionado · L · F3–F6 · Dep US-03/US-07/US-06 · Responsable backend/geoespacial**

Como ciudadano quiero consultar avisos vigentes aplicables al punto de mi localidad.

- TASK-16.1: CAP normalizado, áreas y relaciones Update/Cancel.
- TASK-16.2: intersección/cobertura, expiración y feed health separado.
- TASK-16.3: pantalla fuente/texto/vigencia y ámbito explícito.
- Acceptance: **Given** cancelación fuera de orden, **Then** no resucita aviso; **Given** feed fallido, **Then** no dice “sin alertas”.
- Tests: CAP/geografía/tiempo y E2E sin red externa.

### F10 — Configuración y entrega

**US-17 · MUST · M · F8 · Dep US-04/US-06 · Responsable seguridad/mobile**

Como usuario quiero activar notificaciones sin crear cuenta y poder borrar mis datos.

- TASK-17.1: instalación, secreto/rotación, consentimiento y token push.
- TASK-17.2: endpoints propios con ownership y borrado.
- Acceptance: **Given** dos instalaciones, **Then** no pueden leer reglas ajenas; **When** revoco, **Then** token/reglas dejan de enviar.
- Tests: autorización, idempotencia alta, revocación y reinstalación.

**US-18 · MUST · L · F8 · Dep US-10/US-11/US-17 · Responsable backend**

Como usuario quiero configurar aumento o cruce de nivel para recibir novedades relevantes sin mensajes repetidos.

- TASK-18.1: reglas con ventana/unidad y explicación de suficiencia.
- TASK-18.2: máquina episodios, histeresis, cooldown y estados durables.
- TASK-18.3: outbox transaccional, dispatcher FCM, expiración y dedup mobile.
- Acceptance: **Given** misma condición en 20 polls, **Then** un evento; **Given** condición verdadera al crear regla, **Then** no alarma retroactiva.
- Tests: NT-01 a NT-14, carrera de workers y replay.

**US-19 · MUST · M · F8 · Dep US-16/US-17/US-18 · Responsable mobile/backend**

Como usuario quiero novedades oficiales con vigencia y un historial para revisar el origen.

- TASK-19.1: evento oficial deduplicado por revisión material y geografía.
- TASK-19.2: historial, deep link, leído opcional, quiet hours y permisos.
- Acceptance: **Given** aviso cancelado antes de envío, **Then** se suprime; **Given** FCM aceptó, **Then** UI no declara leído/recibido por persona.
- Tests: dispositivos Android/iOS de staging y lifecycle CAP.

## E06 — Calidad, operación y lanzamiento

### F11 — Operabilidad

**US-20 · MUST · M · F2/F4/F11 · Dep US-06 · Responsable operación**

Como operador quiero distinguir API caída de datos atrasados para actuar sobre la causa correcta.

- TASK-20.1: logs/trazas/métricas y health checks sin datos personales.
- TASK-20.2: tablero por proveedor/stream y runbooks.
- Acceptance: **Given** HTTP 200 sin nueva lectura durante 40 min, **Then** tablero distingue transporte, última ingesta y última observación.
- Tests: falla inyectada y verificación de redacción de secretos.

**US-21 · MUST · M · F10/F11/F12 · Dep US-06/US-20 · Responsable DevOps**

Como responsable del servicio quiero desplegar y restaurar de forma reproducible.

- TASK-21.1: Docker/Compose, CI, ambientes y migración con lock.
- TASK-21.2: cotizar dos hostings con misma carga y elegir ADR.
- TASK-21.3: backup/restore y rollout/rollback ensayado.
- Acceptance: **Given** backup de staging, **When** restauro, **Then** cumple RPO/RTO acordados y reaplica supresiones personales.
- Tests: restauración, integridad y smoke del mismo digest.

### F12 — Aceptación de producto

**US-22 · MUST · L · F10/F12/F13 · Dep todas las MUST anteriores · Responsable producto/QA**

Como ciudadano no técnico quiero comprender la pantalla inicial rápidamente sin interpretar una inferencia como alerta oficial.

- TASK-22.1: prueba con 8–12 usuarios consentida, lector pantalla y fuentes grandes.
- TASK-22.2: gate permisos/fuentes/privacidad/tiendas y presupuesto.
- TASK-22.3: documentar limitaciones y plan operativo; publicación gradual.
- Acceptance: **Given** prototipo funcional, **Then** ≥80% identifica nivel/ventana/antigüedad en diez segundos y nadie interpreta cruce como orden; si falla, corregir/repetir antes de producción.
- Tests: registro UX, suite E2E y checklist release con responsables.

## Cola futura, sin tareas de implementación MVP

| Épica futura | Feature / historia candidata | Prioridad / gate |
|---|---|---|
| E07 Cuencas | Como usuario quiero saber si llovió aguas arriba | NOT NOW; topología, tiempo de aporte y dataset autorizados |
| E08 Predicción | Como analista quiero evaluar pronóstico contra observaciones | NOT NOW; metodología científica y métricas out-of-sample |
| E09 Cuentas | Como usuario quiero sincronizar favoritos entre dispositivos | COULD; demanda demostrada y OIDC/privacidad |
| E10 Profesional | Como municipio quiero tablero de estaciones y auditoría | NOT NOW; discovery/convenio, permisos y SLA propios |
| E11 Premium | Como usuario avanzado quiero exportaciones/históricos ampliados | NOT NOW; validación comercial sin limitar avisos esenciales |
| E12 Asistencia IA | Como usuario quiero explicación verificable de datos | NOT NOW; plantillas insuficientes y guardrails medidos |

## Cómo ejecutar una historia con Codex

Indicar ID, fase, archivos autorizados, dependencias cerradas y criterios de aceptación. Solicitar implementar solo esa historia, actualizar evidencia y ejecutar pruebas pertinentes. No pedir “hacer todo el roadmap” en una sola iteración. Al encontrar una hipótesis externa sin verificar, mantener feature flag deshabilitado y registrar el bloqueo; continuar únicamente tareas independientes. Un PR debe enlazar historia, cambio observable y tests realizados.
