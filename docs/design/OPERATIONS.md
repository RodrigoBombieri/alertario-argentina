# Operación, observabilidad y riesgos

## Indicadores que no deben confundirse

Por proveedor/stream guardar: último intento, último HTTP exitoso, última ingesta válida, última observación nueva, tiempo observado más reciente, última consulta completa de avisos, número de registros rechazados. Un HTTP 200 sin nuevos datos y un timeout son incidentes distintos.

Ejemplo de tablero: “INA: sin ingesta válida desde hace 40 minutos; último HTTP 200 hace 5 min; 12 registros en cuarentena; última lectura de serie X hace 18 h (cadencia diaria)”. No etiquetar toda INA caída si solo falla una serie.

## Logs, métricas y trazas

ILogger estructurado y OpenTelemetry como propuesta: correlation/trace ID, job ID, adapter version, duración, status HTTP y conteos. Sin payloads de usuario, tokens, GPS ni cuerpo completo CAP en logs rutinarios. Dimensiones de métricas por proveedor/resultado, evitando stationId/installationId de alta cardinalidad; detalle por serie en tabla operativa.

Métricas propuestas: `provider_request_duration`, `provider_request_failures`, `ingestion_valid_age`, `measurement_age`, `quarantine_records`, `schema_mismatch_count`, `active_series_stale_ratio`, `outbox_oldest_age`, `notification_failures`, `worker_heartbeat_age`, latencia/API errors, conexiones DB, disco y éxito de backup. Tracing API→DB y jobs→provider; no un span infinito por lote.

Health checks propios: liveness = proceso activo; readiness API = DB y migración compatibles; worker health = heartbeat + cola. No sacar de servicio API útil por caída de INA: exponer dependencia degradada y servir cache con estado. Endpoint detallado de operación autenticado, público solo resumen seguro.

## SLO internos propuestos, sujetos a presupuesto

| SLI | Objetivo inicial |
|---|---|
| API propia lecturas | 99,5% mensual, excluyendo no la falla propia sino registrándola correctamente; meta interna, no SLA comercial |
| Latencia summary | p95 <500 ms en carga acordada, servidor |
| Observación elegible recibida → normalizada | <2 min p95 desde fetch completado |
| Evento elegible → aceptado por push | <2 min p95, no promesa de entrega al dispositivo |
| Frescura externa | Medir por serie contra C+L; no garantizar lo que depende del organismo |
| Recuperación backup | RPO ≤24 h beta, RTO ≤4 h; producción buscar RPO ≤1 h si coste/servicio lo permiten |

Alertas operativas propuestas: no ingesta válida por tres ciclos; no heartbeat worker durante dos ciclos; outbox >5 min; schemaMismatch nuevo; crecimiento brusco de cuarentena; disco >75%; backup diario fallido. Un aviso operativo agrupado por incidente con cooldown, recuperación y responsable evita spam al equipo. Cadencias diarias usan umbrales correspondientes, no cinco minutos universales.

## Runbooks a implementar

| Incidente | Diagnóstico y actuación |
|---|---|
| INA falla 40 min | Revisar HTTP frente a parseo, última serie válida, circuit breaker y cambios de schema; pausar retry masivo; conservar dato viejo; contactar canal oficial si persiste |
| SMN incompleto | Marcar cobertura unavailable/partial; mantener solo vigencias conocidas; suspender mensajes de “sin avisos”; verificar feed sin scraping alternativo improvisado |
| Error en umbral | Deshabilitar comparación de versión afectada; auditar procedencia; corregir versionado; aclaración a usuarios si hubo notificaciones erróneas |
| Corrección histórica | Reprocesar intervalo con modo no-push; reconstruir snapshots/variaciones y conservar revisión |
| Outbox atascado | Revisar lease, token/credenciales y TTL; reintentar solo vigente; no vaciarla con envíos masivos |
| Backup / disco | Limitar backfill, restaurar en ambiente aislado, medir integridad y reaplicar supresiones de datos personales |

## Registro de riesgos

Probabilidad e impacto son estimaciones cualitativas del proyecto. Dueños son roles a asignar, no personas ya comprometidas.

| ID | Riesgo / P / impacto | Mitigación y señal | Dueño / puerta |
|---|---|---|---|
| R01 | API cambiada/discontinuada; media/alto | Adapter, contract snapshots, canary, feature flag; schema diff | Backend / fase 1 |
| R02 | Sin SLA/caídas; alta/alto | Ingesta B, DB/cache, frescura y circuit breaker | Operación / beta |
| R03 | Feed SMN no estable; alta/crítico | Confirmar canal CAP oficial; no aceptar MVP completo sin él | Integraciones / fase 1 |
| R04 | Datos incompletos/offline; alta/alto | Estado por serie, no rellenar, seleccionar piloto | Datos / fase 3 |
| R05 | Unidades/datum distintos; media/crítico | Serie homogénea, metadata aprobada, bloquear comparación | Hidrología / fase 3 |
| R06 | Hora sin zona/incorrecta; alta/alto | UTC original+interpretación, cuarentena, clock tests | Backend / fase 3 |
| R07 | Duplicados/revisiones; alta/alto | Unique keys, versiones, transacciones, replay no-push | Backend / fase 4 |
| R08 | Cobertura desigual; alta/alto | Región piloto y mapa de disponibilidad explícito | Producto / beta |
| R09 | Cercana ≠ representativa; alta/crítico | Asociación curada, río visible, no riesgo domiciliario | Geoespacial / fase 3 |
| R10 | Falsa interpretación/evacuación; media/crítico | Separar ejes, pruebas semánticas/usuarios, revisión profesional | Producto / beta |
| R11 | Derechos no claros; media/alto | Permiso/licencia por red, denylist public=false | Responsable legal / antes de publicar |
| R12 | Push perdido/retrasado; alta/alto | Historial, TTL, monitoreo, no canal único | Mobile / fase 8 |
| R13 | Costes mapas; media/medio | Proveedor configurable, cuotas y política tiles | Operación / fase 7 |
| R14 | Costes de push y ecosistema; baja-media/medio | FCM evaluado; medir costos de backend/otros servicios, reevaluar tarifa | Operación / beta |
| R15 | Crecimiento histórico/WAL; media/alto | Retención, sizing, índices, particiones si se justifican | Datos / fase 9 |
| R16 | Privacidad/abuso; media/alto | Instalación mínima, ownership, borrado y antiabuso | Seguridad / beta |
| R17 | Umbrales obsoletos; media/crítico | Vigencia, evidencia, revisión auditada, sin cruce si ambiguo | Datos / fase 1 |
| R18 | Outlier real filtrado; media/crítico | No eliminar silenciosamente; estado de calidad, revisión de saltos | Hidrología / fase 5 |
| R19 | Hosting barato con suspensión/pérdida; media/alto | Worker siempre activo, backups externos, ensayo restore | Operación / fase 12 |
| R20 | App store/macOS y mantenimiento; media/medio | Validar build iOS pronto, presupuesto de dispositivos y publicación | Mobile / fase 6 |

## Control de lanzamiento

Responsable de producto valida lenguaje, responsable técnico valida operación y especialista revisa representatividad/datum; permiso/licencia de cada fuente registrado. Cualquier pendiente crítico mantiene la versión como beta limitada y la función deshabilitada, nunca una simulación presentada como oficial. Debe existir contacto para reportar datos erróneos y una forma de suspender pushes por proveedor sin perder consulta histórica.
