# API propia propuesta v1

**Diseño de AlertaRío, no endpoints de organismos.** El [OpenAPI ejecutable del corte F2](../../contracts/openapi/v1.json) cubre un subconjunto sintético; las rutas restantes de esta página son diseño pendiente. HTTPS + JSON, IDs internos opacos, fechas RFC 3339 UTC, números JSON con punto y unidades canónicas. UI traduce formatos e idiomas. [Alcance F2](../implementation/F2.md).

## Recursos

| Método / ruta propia | Propósito / entrada | Acceso |
|---|---|---|
| GET `/v1/locations?query=&provinceId=&cursor=&limit=` | Búsqueda local; propuesta query 2–80 caracteres, limit 1–50 | Público |
| GET `/v1/locations/{id}/stations?radiusKm=` | Candidatos y asociaciones revisadas; radio máximo propuesto 100 km | Público |
| GET `/v1/stations?bbox=&riverId=&provinceId=&cursor=&limit=` | Lista/mapa; bbox validado; máximo 500 registros por página propuesto | Público |
| GET `/v1/stations/{id}` | Metadatos, series habilitadas y procedencia | Público |
| GET `/v1/stations/{id}/summary` | Latest por variable, variaciones, ejes de estado y umbrales | Público |
| GET `/v1/series/{id}/measurements?from=&to=&resolution=&cursor=` | Valores e intervalos, huecos, calidad y agregación declarada | Público |
| GET `/v1/notices?locationId=` | Avisos para centroide/localidad más estado de feed | Público |
| GET `/v1/notices?lat=&lon=` | Consulta puntual opcional, no retener coordenadas en logs | Público |
| GET `/v1/notices/{id}` | Documento, ciclo de vida y fuente | Público |
| GET `/v1/sources` | Procedencia, licencias, última consulta y limitaciones públicas | Público |
| POST `/v1/installations` | Alta seudónima; devuelve credencial opaca una sola vez | Público con antiabuso |
| PUT `/v1/installation/push-token` | Registrar/rotar token del dispositivo | Credencial instalación |
| GET/POST `/v1/notification-rules` | Listar/crear reglas propias | Credencial instalación |
| PUT/DELETE `/v1/notification-rules/{id}` | Modificar/desactivar regla, versión requerida | Credencial instalación |
| GET `/v1/notification-events?cursor=` | Historial propio | Credencial instalación |
| POST `/v1/notification-events/{id}/read` | Marcar leído idempotentemente | Credencial instalación |
| DELETE `/v1/installation` | Revocar token, reglas y borrar datos según política | Credencial instalación |

Favorites no requieren endpoints MVP: se guardan en SQLite. Endpoints administrativos de importación/umbrales no públicos, fuera del móvil; pueden ser comandos operativos autenticados antes de crear una consola.

## Contrato semántico de summary

Ejemplo ilustrativo de estructura propia (no respuesta gubernamental ni valores reales):

```json
{
  "stationId": "internal-id",
  "generatedAt": "2026-09-29T12:05:00Z",
  "height": {
    "seriesId": "internal-series-id",
    "value": 7.48,
    "unit": "m",
    "observedAt": "2026-09-29T12:00:00Z",
    "sourceUpdatedAt": "2026-09-29T12:02:00Z",
    "ingestedAt": "2026-09-29T12:04:00Z",
    "quality": "accepted",
    "freshness": "fresh",
    "sourceId": "internal-source-id"
  },
  "discharge": null,
  "dischargeUnavailableReason": "noApprovedSeries",
  "changes": [
    {
      "windowHours": 6,
      "delta": 0.16,
      "unit": "m",
      "referenceAt": "2026-09-29T06:00:00Z",
      "actualDurationSeconds": 21600,
      "method": "observedEndpoints",
      "trend": "rising",
      "availability": "available"
    }
  ],
  "calculatedCondition": "noNotableChange",
  "officialThresholds": [],
  "notices": [],
  "noticeCoverage": {
    "status": "unavailable",
    "lastSuccessfulCheckedAt": null
  },
  "methodologyVersion": "trend-v1",
  "dataVersion": "opaque-version"
}
```

La salida real incluirá las cinco ventanas; cada una puede tener `delta:null` con `unavailableReason`. `noNotableChange` no impide tendencia creciente: depende del corte de seguimiento validado. `notices:[]` sin `noticeCoverage=complete` **no prueba ausencia de avisos**. Altura y caudal tienen relojes/calidad independientes. Umbrales incluyen emisor, evidencia, datum, vigencia y `comparisonStatus`; nunca solo dos números desnudos.

`noticeCoverage`: `complete`, `partial`, `unavailable`, `notConfigured`, con `lastSuccessfulCheckedAt`. “Complete” solo se asigna si el contrato de feed garantiza cobertura del alcance solicitado. Avisos pueden estar activos aunque el estado del último fetch sea unavailable.

## Históricos, paginación y límites propios

Cursor opaco por `(observedAt,id)` y versión de snapshot para evitar duplicados en datos revisados. Orden temporal explícito. Máximo propuesto 2.000 puntos por respuesta, 31 días raw; intervalos mayores agregados o paginados según política. Estos son límites **nuestros**, pendientes de carga, no restricciones atribuidas a INA.

Agregación de gráfico informa `resolution`, `aggregation`, `sampleCount`, mínimos/máximos y huecos. Conservar picos; no usar valores simplificados para tendencias o reglas. “Dato diario” fuente y “promedio diario calculado” no comparten etiqueta.

## HTTP, cache y consistencia

200 con estado parcial explicable cuando existe representación útil; 200 con lista vacía solo búsqueda sin coincidencias válida. 400 entrada inválida; 401 credencial inválida; 403 permiso insuficiente; 404 recurso inexistente o no accesible; 409 conflicto de versión/idempotencia; 422 regla semánticamente inválida; 429 con Retry-After por límite propio; 503 cuando no puede servirse representación confiable. Errores `application/problem+json` con code, title, traceId y detalles seguros.

ETag de versión de representación y caducidad corta: no devolver 304 indefinidamente mientras cambia frescura/expiración. Recalcular estado dependiente del tiempo o incluir `validUntil` y hacerlo expirar; cliente actualiza edades con reloj y hora servidor. Cache público solo GET sin datos de instalación; token/eventos/reglas `private,no-store`. Lat/lon no se incluyen en logs de URL.

POST de reglas/instalación acepta Idempotency-Key por recurso y ámbito, almacenada con hash del request y expiración propuesta 24 h. PUT usa versión/If-Match para evitar sobrescritura entre reintentos. En alta, el reintento seguro requiere recuperar temporalmente la misma credencial cifrada para esa clave; después de la ventana no se vuelve a revelar. Evaluar alternativamente proof-of-possession de clave generada en móvil si el spike de seguridad justifica la complejidad.

Nunca admitir URL upstream desde el cliente. Revisar límites de bbox, ventanas y tamaños para evitar amplificación. OpenAPI pública documenta enum desconocido: Flutter debe mostrar estado conservador ante valores nuevos.
