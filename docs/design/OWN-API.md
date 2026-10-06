# API propia v1

Contrato vigente: [OpenAPI](../../contracts/openapi/v1.json). Implementación: [PublicDataEndpoints](../../backend/src/AlertaRio.Api/Features/PublicDataEndpoints.cs). Todas las rutas de producto actuales son de lectura.

| GET | Uso |
|---|---|
| `/v1/status` | Modo y disponibilidad de datos oficiales |
| `/v1/locations?query=&limit=` | Buscar localidades |
| `/v1/locations/{id}/stations` | Estaciones asociadas |
| `/v1/stations?limit=` | Catálogo de estaciones |
| `/v1/stations/map?bbox=&limit=` | Estaciones en oeste,sur,este,norte |
| `/v1/stations/{id}` | Metadatos |
| `/v1/stations/{id}/summary` | Lecturas, tendencias, calidad y cobertura |
| `/v1/series/{id}/recent` | Gráfico reciente |
| `/v1/series/{id}/history?from=&to=&cursor=&limit=` | Histórico paginado, hasta 30 días |
| `/v1/notices?locationId=` | Avisos y cobertura; SMN actualmente no configurado |
| `/v1/sources` | Procedencia |

No existen endpoints de cuentas, instalaciones, tokens push ni reglas remotas. Favoritos y seguimiento son locales.

La API diferencia datos sintéticos, no disponibles y publicados. Un resultado vacío no acredita cobertura oficial. No exponer al cliente DTO ni identificadores de proveedor como contrato propio.

Los endpoints `/health/live`, `ready`, `storage`, `worker` y `data` son de operación. OpenAPI dinámico se expone solo en Development.
