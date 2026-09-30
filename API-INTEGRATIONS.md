# Integraciones e ingesta

Este archivo define **decisiones de AlertaRío**, no límites ni garantías de los organismos. Rutas externas verificadas en [DATA-SOURCES](DATA-SOURCES.md); contrato interno en [OWN-API](docs/design/OWN-API.md).

## Límites y puertos

- `IHydrologyProvider`: catálogo de estaciones/series y lotes de observaciones por intervalo. Devuelve objetos normalizados y procedencia; solo el adapter conoce nombres externos como `valor` o `rows`.
- `IAlertProvider`: lotes de avisos emitidos, revisiones, cancelaciones, geometrías y estado de completitud de la consulta. Implementación SMN cuando se valide feed. No mezcla umbrales con avisos.
- `IGeographicProvider`: importa un snapshot o página de localidades. Búsqueda ciudadana se realiza sobre catálogo propio.
- `IWeatherProvider`: **no crear aún**; tendrá sentido si se incorpora precipitación o pronóstico, que no son el mismo contrato que avisos CAP.
- `IPushSender`: envío de un evento ya resuelto; FCM no decide cuándo ni qué notificar.

Contratos independientes del HTTP externo: catálogo, lote con `items`, `coverage`, `cursor`, `fetchedAt`, `sourceVersion`, `warnings` y resultado discriminado `success/partial/unavailable/unauthorized/schemaMismatch`. Nunca convertir una excepción en un lote vacío exitoso.

## A: consulta en vivo versus B: ingesta periódica

| Dato | Decisión | Motivo | Fallback |
|---|---|---|---|
| Altura/caudal reciente | B | Una llamada por serie sirve a muchos usuarios; cálculos y notificaciones reproducibles | Último dato con antigüedad, nunca consulta externa por cada refresh |
| Catálogo estaciones/series | B | Cambia menos; necesita validación y asociación | Último snapshot válido |
| Histórico | B, carga incremental + backfill acotado | Evita descargar décadas por teléfono | Informar rango realmente disponible |
| Avisos SMN | B | Geometría, ciclo de vida, deduplicación y desacople | Avisos conocidos con vigencia + estado “consulta no disponible” |
| GeoRef | B, snapshot periódico | Búsqueda local rápida, independiente de disponibilidad | Catálogo anterior versionado |
| Fase 1 | A solo en prueba técnica de lectura | Permite observar el contrato sin una DB de producción | Capturas revisables |

No se ofrece proxy público arbitrario a los organismos. La API propia sirve datos persistidos; un cache miss no produce una tormenta de consultas externas.

## Programación inicial propuesta

Son **parámetros operativos tentativos**; se ajustarán a permisos y mediciones de fase 1, nunca se presentan como frecuencia oficial.

| Trabajo | Frecuencia candidata | Límite inicial |
|---|---|---|
| Observaciones activas | 15 min para piloto; 60 min o más en series diarias | 1 solicitud simultánea por proveedor, jitter ±20% |
| Catálogos hidrológicos | Cada 24 h | Recorrer páginas secuencialmente; no borrar ausentes tras una página fallida |
| CAP | 5 min si SMN lo admite | Un catálogo; recuperar solo mensajes nuevos o modificados |
| GeoRef | Snapshot mensual + revisión semanal de versión | Importación fuera de horas pico; intercambio atómico |
| Backfill | Ventanas de 7 días; luego ampliar según tamaño real | Un job separado de baja prioridad |
| Reconciliación | Últimas 72 h por ciclo y últimos 30 días diariamente | Ajustable por cadencia y correcciones reales |

Si hay 30 series consultadas cada 15 minutos, son 2.880 consultas/día antes de reintentos. Eso **no significa que la cuota lo permita**. Priorizar consultas agregadas solo cuando estén documentadas y verificadas; negociar presupuesto de consultas. Medir tráfico y respetar `Retry-After` si se recibe.

## Flujo y durabilidad

1. Worker adquiere lease PostgreSQL por proveedor/job; permite recuperación tras caída sin duplicar trabajo simultáneo.
2. Carga checkpoint y lista aprobada de series públicas. Aplica presupuesto, timeout y cancelación.
3. Descarga y registra resultado HTTP, hora, tamaño y hash; guarda cuerpo de evidencia solo con licencia adecuada y retención acotada.
4. Adapter verifica formato, parsea, normaliza y clasifica problemas. Un HTML 200 no es JSON válido.
5. Valida unidad, intervalo, datum y procedencia. Manda anomalías a cuarentena con causa.
6. Upsert idempotente de observaciones y revisión; calcula latest con máximo tiempo de observación, no orden de llegada.
7. Dentro de una transacción actualiza snapshot, checkpoint y eventos pendientes en outbox. Un crash no adelanta el checkpoint sin persistir.
8. Evalúa reglas con versión del algoritmo; procesa solo nuevos eventos elegibles para actualidad. Reprocesos históricos no envían pushes retroactivos.
9. Dispatcher toma eventos con lease, entrega a FCM y registra intentos. App consulta la API propia y reconcilia por ID.

Para un lote parcialmente inválido: persistir los válidos con cuarentena durable y contabilizada; no marcar snapshot de catálogo completo ni avanzar sobre una página perdida. Checkpoint separado por serie y stream; comparar checkpoint de ingesta con último dato observado.

## Normalización INA

| Campo externo leído | Representación interna | Regla |
|---|---|---|
| estación `id`, `tabla`, `id_externo`, `red.id` | `ExternalStationReference` | Guardar todos; no asumir `id` universal entre redes/proveedores |
| serie `id`, `var`, `procedimiento`, `unidades` | `MeasurementSeries` | Separar instantánea, media diaria/mensual, observada y simulada |
| `geom.coordinates` | Punto EPSG:4326 | GeoJSON usa longitud, latitud; validar rango |
| `timestart`, `timeend` | `observedStartAt`, `observedEndAt` | Parseo con offset; intervalo, no siempre instante |
| `timeupdate` | `sourceUpdatedAt` | Revisión/publicación según significado confirmado; no reemplaza observación |
| `valor` | `value`, `originalValue` | Altura m, caudal m³/s; no deducir unidad por magnitud |
| observación `unit_id:null` | unidad de serie validada | Solo heredar si inequívoca; conflicto ⇒ cuarentena |
| `nivel_alerta`, `nivel_evacuacion` vistos en payload | Umbral candidato | No estaban en esquema Estacion revisado; vigencia/datum/emisor requieren confirmación |
| nulo / `"null"` | Ausencia tipada | Limpieza solo de campos conocidos; conservar original para auditoría |

No convertir altura hidrométrica a cota absoluta sumando `cero_ign` sin conocer sistema vertical y época. No convertir altura a profundidad, ni caudal desde altura sin curva de descarga autorizada. No promediar series de distintas estaciones.

El adapter admitirá las envolturas documentadas/observadas explícitamente, no cualquier JSON arbitrario. Campos extra no críticos se toleran; faltantes críticos bloquean el registro. `next_page` externo no se sigue de forma libre: reconstruir consulta sobre base HTTPS permitida con parámetros documentados y corte por página repetida/máximo; registrar discrepancia para el proveedor.

## Tiempo, antigüedad y datos viejos

Persistir `timestamptz` en UTC; API RFC 3339 UTC; conservar timestamp original y regla de interpretación. Mostrar hora en `America/Argentina/Buenos_Aires` con fecha y zona, no sumar tres horas manualmente. Si el dato externo no tiene offset, mantenerlo sin resolver hasta confirmar zona por dataset. Los `date_range` INA sin offset no son prueba suficiente para fijarla.

Tres relojes: `observedAt` (medición), `sourceUpdatedAt` (publicación/revisión si se sabe), `ingestedAt` (nuestra consulta). UI: “Medido hace 16 h · consultado hace 8 min”. Evita llamar nuevo a un dato viejo recién importado.

Frescura por serie: con cadencia C y retraso permitido L aprobados, stale cuando `now - observedAt > C + L`. Ejemplos de configuración, no oficiales: serie horaria C=1 h/L=30 min; diaria C=24 h/L=14 h. La serie de Concordia requiere evaluar este segundo patrón; aún no se aprobó. Si C/L se desconocen, estado `freshnessUnknown`, no “actual”. Si reloj futuro supera tolerancia propuesta de 5 min, cuarentena; desfase menor se registra, no genera edades negativas.

Una observación puede estar dentro de su cadencia y ser demasiado antigua para determinada regla. Cada motor exige además `maxEventAge` y datos suficientes para su ventana. La comparación “desde mi última consulta” usa el último measurement ID visto localmente, con sus tiempos, sin confundirlo con Δ12 h.

## Resiliencia

Política candidata: timeout de conexión 5 s, total 20 s; máximo dos reintentos para GET idempotentes por errores transitorios con backoff/jitter. 429 respeta `Retry-After`; 401/403 no se reintentan en bucle; 400/schemaMismatch requieren intervención. Cortacircuito tras cinco fallas transitorias consecutivas, espera inicial 2 min, una prueba half-open. Todo configurable y sujeto a observación.

El worker sigue vivo aunque un proveedor falle. Un error de SMN no interrumpe INA. Conservar último estado y marcas de salud; al recuperar, reconciliar antes de enviar notificaciones. No asumir eliminación porque un catálogo llegue vacío. Revalidar umbrales como operación auditada; jamás actualizarlos en silencio ante un nulo.

Cache inicial en proceso para consultas compartidas (30–60 s propuestos) con límites de tamaño y coalescencia; la DB sigue siendo verdad. TTL no rejuvenece `observedAt`. Claves por recurso, filtros, versión de datos e idioma. Redis se evaluará solo por necesidad medida de múltiples réplicas, cache compartida o rate limit distribuido.

## Alertas y geografía

CAP: validar emisor permitido, `Actual`, `Public`, tipo de mensaje y fechas; excluir Test/Exercise. Procesar Alert, Update y Cancel y referencias incluso si falta el original. Un registro tombstone evita resucitar un aviso cancelado recibido fuera de orden. Ausencia en un feed truncado no equivale a cancelación. La expiración se calcula localmente aunque el proveedor falle, conservando aviso en historial.

CAP polygon usa pares latitud,longitud; convertir a longitud,latitud para PostGIS/GeoJSON. Validar anillos, múltiples áreas, geometría y CRS; no corregir silenciosamente un polígono incoherente. `areaDesc` vacío no invalida un polígono utilizable. Una localidad representada solo por centroide se etiqueta “aplica al punto de referencia de la localidad”; no cubre automáticamente todo el municipio. Para borde usar `ST_Covers` o distancia geográfica definida, no un radio arbitrario de alerta. Ubicación GPS opcional solo durante la consulta, sin seguimiento.
