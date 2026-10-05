# F5 — habilitar lectura pública de datos aprobados

`PublishedData__Enabled` está apagado por defecto. El modo publicado consulta PostgreSQL en cada pedido y no sirve la muestra D15. La API rechaza la combinación de este modo con `ColdStart__Enabled`, `SyntheticData__Enabled` o el preview `PersistedSummary__Enabled`.

Antes del cambio, comprobar que la fuente INA tenga permiso y decisión de derechos vigentes; la serie observada instantánea tenga aprobación hidrológica, unidad/datum y política revisadas; la localidad GeoRef pertenezca al catálogo activo con derechos vigentes; y la asociación localidad–estación esté aprobada y en fecha. Verificar migraciones, backup y `/health/storage`. Los avisos SMN siguen con cobertura `notConfigured` hasta que exista su integración completa: no presentar la lista vacía como ausencia de alertas.

Configurar `ConnectionStrings__Ingestion`, `ColdStart__Enabled=false` y `PublishedData__Enabled=true`; reiniciar la API. Consultar `/v1/status`: `officialDataAvailable=true` solo si hay una medición aceptada en una serie publicable cuya estación tiene una asociación activa aprobada. Probar búsqueda, estaciones, summary, mapa e histórico con una serie aprobada. Las respuestas reales deben tener `synthetic=false`. Probar además un ID sin aprobación: debe devolver 404 o lista vacía.

Ante revocación de derechos o asociación, la lectura vuelve a comprobar las puertas en DB por pedido. El operador puede volver a `PublishedData__Enabled=false` y `ColdStart__Enabled=true` para mostrar la muestra etiquetada, sin mezclarla con las mediciones reales. La recolección del worker y la publicación son interruptores distintos.
