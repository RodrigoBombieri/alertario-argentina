# F4 — activar y detener recolección INA

El worker INA está apagado por defecto y no tiene una allowlist de series distribuida con el software. La muestra D15 vive solo en el lector sintético de la API. Un lote INA guardado en PostgreSQL **no** aparece en la app mientras la serie no tenga aprobación hidrológica y publicación activada por separado.

## Antes de activar

1. Adjuntar una decisión fechada sobre derechos, atribución y cuota para la red/dataset concretos (EXT-01). Identificar el responsable y el presupuesto de consultas.
2. Registrar en PostgreSQL `data_sources` con código `ina-a5:<red>`, `permission_status=approved`, `rights_decision_id` y `reviewed_at`; registrar la estación, su `station_external_refs` pública y la `measurement_series` observada instantánea con metadatos del catálogo, `rights_decision_id` y `approval_version`. Mantener `measurement_series.approved=false` hasta la revisión hidrológica.
3. Verificar migraciones y backup, escoger `InternalSeriesId` existente y copiar del catálogo INA los IDs y campos exactos. No inferir unidad, red, procedimiento ni soporte temporal.
4. Configurar `ConnectionStrings__Ingestion` y `InaIngestion__Enabled=true`, `InaIngestion__ActivationAcknowledged=true`, `InaIngestion__InternalSeriesId`, `InaIngestion__ExternalSeriesId`, `InaIngestion__ExternalStationId`, `InaIngestion__NetworkId`, `InaIngestion__VariableCode`, `InaIngestion__ProcedureId` si aplica, `InaIngestion__ProcedureName`, `InaIngestion__UnitId`, `InaIngestion__Unit`, `InaIngestion__SelectionVersion` y `InaIngestion__RightsDecisionId`. `InaIngestion__PollIntervalHours` vale 24 por defecto; ajustar solo dentro de la cuota aprobada (1–24 h).

El worker comprueba la coincidencia de fuente/estación/serie y derechos en DB antes de consultar; vuelve a comprobarla antes de confirmar el lote. Consulta un día al iniciar por primera vez, solapa un día desde el último cursor y limita cada ventana a tres días. Una recuperación atrasada espera una hora entre ventanas; una petición incompleta o un cambio de catálogo no avanza el checkpoint. El cliente solo usa la base oficial `https://alerta.ina.gob.ar/a5/` y rechaza redirecciones.

## Comprobación y detención

Revisar logs de lotes, `ingestion_checkpoints`, `ingestion_runs`, cuarentena, revisiones y cuota real consumida. Comparar horas de observación, actualización en fuente e ingesta; registrar fallos sin convertirlos en ceros. Si cambian derechos, cuota o metadatos, poner `InaIngestion__Enabled=false` y reiniciar el worker; revocar `data_sources.permission_status` si corresponde. La vista publicable deja de exponer inmediatamente fuentes revocadas.

Al completar 14 días **reales** por serie, preparar informe de cadencia, retraso, nulos, revisiones, unidad y datum para hidrología (EXT-03/04). Solo después de su decisión explícita puede configurarse la aprobación/publicación. La muestra sintética no cuenta ni se copia a la serie real.
