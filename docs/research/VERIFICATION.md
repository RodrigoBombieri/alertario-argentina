# Registro de verificación

Fecha: **2026-09-29**. Consultas reales puntuales de solo lectura, sin login, sin cookies de autenticación y sin escrituras externas. Los horarios exactos están en los archivos `*-result.json`; se registran en UTC. Las respuestas se guardaron como texto UTF-8 mediante PowerShell, no como captura byte-a-byte del transporte. [Manifest SHA-256](evidence/manifest.json) identifica los archivos locales.

## Método y límites

Se localizaron recursos con búsqueda web y páginas oficiales; se siguieron enlaces publicados hasta contratos o datos. Las herramientas web no siempre pudieron leer contenido dinámico o dominios oficiales: cuando correspondió se verificó con una petición HTTP directa. Un error de herramienta no se convirtió en diagnóstico de caída del proveedor. La primera consulta desde sandbox falló TLS; fuera del sandbox, con autorización, respondió sin desactivar validación de certificado.

No se implementó aplicación, worker periódico ni scraper. Se inspeccionaron enlaces HTML SMN para comprobar un archivo CAP publicado, sin diseñar extracción operativa de HTML. No se realizaron pruebas de carga. No se verificó disponibilidad continua ni todas las redes/variables.

## Consultas HTTP y evidencia

| Recurso oficial | Resultado comprobado | Archivo |
|---|---|---|
| INA Swagger HTML e inicializadores | 200; HTML inline enlaza contrato INA; script genérico contiene plantilla Petstore | URL documentada en DATA-SOURCES; HTML no archivado |
| `https://alerta.ina.gob.ar/a5/yaml/apidocs_v3_1.yml` | 200; OpenAPI leído | [ina-openapi.yaml](evidence/ina-openapi.yaml) |
| `https://alerta.ina.gob.ar/a5/obs/puntual/series?estacion_id=1` | 200 JSON; wrapper rows y unidades de serie | [ina-series.txt](evidence/ina-series.txt), [resultado](evidence/ina-series-result.json) |
| `https://alerta.ina.gob.ar/a5/obs/puntual/estaciones?nombre=Concordia&pagination=true&limit=5` | 200 JSON; múltiples redes, umbrales y nulos; página parcial | [ina-concordia.txt](evidence/ina-concordia.txt), [resultado](evidence/ina-concordia-result.json) |
| `https://alerta.ina.gob.ar/a5/obs/puntual/series?estacion_id=79` | 200 JSON; altura, caudal y estadísticas con coberturas diferentes | [ina-concordia-series.txt](evidence/ina-concordia-series.txt), [resultado](evidence/ina-concordia-series-result.json) |
| Serie 1, observaciones 29/06–01/07/2026 UTC, ruta exacta en resultado | 200 JSON; `timeupdate` posterior, unidad null y tiempos con Z | [ina-observaciones.txt](evidence/ina-observaciones.txt), [resultado](evidence/ina-observaciones-result.json) |
| Serie 79, observaciones 27/09–29/09 19:00 UTC, ruta exacta en resultado | 200 JSON; 3 muestras diarias, separación entre medición y actualización | [ina-concordia-observaciones.txt](evidence/ina-concordia-observaciones.txt), [resultado](evidence/ina-concordia-observaciones-result.json) |
| GeoRef OpenAPI enlazado por referencia oficial v2, rama development | 200; servidor v2.0, info.version 0.5.X | [georef-openapi.json](evidence/georef-openapi.json) |
| `https://apis.datos.gob.ar/georef/api/v2.0/localidades?nombre=Concordia&max=3` | 200 JSON; localidad simple y entidad homónima, dos resultados | [georef-localidades.txt](evidence/georef-localidades.txt), [resultado](evidence/georef-localidades-result.json) |
| `https://ssl.smn.gob.ar/CAP/AR.php` publicado por registro OMM | 200 HTML; catálogo visible de enlaces CAP, **no feed XML** | [smn-cap.txt](evidence/smn-cap.txt), [resultado](evidence/smn-cap-result.json) |
| XML CAP fechado enlazado por índice SMN, URL exacta en resultado | 200 text/xml; CAP 1.2, Update, Public, polígono, expiry, areaDesc vacío | [smn-cap-example.txt](evidence/smn-cap-example.txt), [resultado](evidence/smn-cap-example-result.json) |
| `https://www.smn.gob.ar/rss` | 403 Forbidden | [resultado](evidence/smn-rss-page-result.json) |
| `https://ws2.smn.gob.ar/rss` | Error DNS en consulta directa; web tool tampoco recuperó | Registro narrativo; sin captura de cuerpo |

La estación/serie 1 se usó como sondeo acotado de contrato, no como selección aprobada del piloto. La estación 79 surgió del catálogo por nombre. Los IDs encontrados no se convierten en IDs internos del producto.

## Hallazgos de mayor impacto

1. **INA schema drift:** wrapper `rows` no coincide con array descrito; algunos campos útiles solo aparecen en payload. Diseñar tests de contrato con ambas evidencias.
2. **Paginación:** `next_page` recibido usa HTTP y pierde `/a5`. Verificar antes de seguir; reconstruir solo con parámetros documentados y host permitido.
3. **Datos no públicos en catálogo:** guardar evidencia de investigación no autoriza publicarlos. El snapshot es de uso interno y requiere saneamiento/permiso antes de publicarse o convertirse en fixture distribuida.
4. **Nulos/semántica:** null y `"null"`, metadata sin zona, observación con unidad heredable y bandera `has_obs` que no certifica historia vacía.
5. **Resolución:** la muestra Concordia permite comparar días, no las ventanas horarias cortas requeridas.
6. **SMN:** existe CAP oficial legible; no se ha validado feed machine-readable completo para seguimiento continuo. No confundir ambos logros.
7. **GeoRef:** servidor operativo v2.0 confirmado pese a distintos rótulos de documentación. Guardar referencia y no seguir ejemplos v1 discontinuados.

## Qué no se verificó

- Licencia/permiso integral de las redes INA y capas GeoRef para almacenar/redistribuir en esta app.
- Cuotas, SLA ni frecuencia contractual general de INA/SMN/GeoRef.
- Vigencia normativa, autoridad originaria y compatibilidad vertical de umbrales de Concordia.
- Todos los endpoints INA documentados; JSON sí, GeoJSON y simulaciones solo documentación.
- Cobertura nacional, disponibilidad longitudinal, rate limits observados, exhaustividad de históricos o correcciones profundas.
- Feed SMN estable completo, ACP operativo, mapping oficial de colores, esquemas de precipitaciones observadas y radar.
- Descarga S3 WRF ni continuidad del dataset: solo documentación oficial del publicador/registro.
- API directa PNA, SINAGIR/SINAME o provincial de producción.
- APIs de proveedor de mapas/push en cuenta propia, deploy, compatibilidad exacta SDKs ni tiendas.
- Usabilidad de competidores en dispositivo; app exacta del nombre ambiguo “Altura de los Ríos”.

## Bibliografía de consulta adicional

Fuentes específicas están enlazadas junto a cada afirmación en los documentos. Puntos de entrada técnicos: [INA presentación oficial](https://www.argentina.gob.ar/node/228063), [INA A5](https://alerta.ina.gob.ar/a5/apiUI), [GeoRef recursos](https://www.argentina.gob.ar/georef/documentacion-y-recursos-georef-v21), [SMN registro OMM](https://alertingauthority.wmo.int/authorities.php?recId=4), [SMN CAP](https://ssl.smn.gob.ar/CAP/AR.php), [SMN WRF](https://registry.opendata.aws/smn-ar-wrf-dataset/).

Las referencias no se presentan como autorización de operación. Fase 1 debe agregar fichas de permisos, pruebas de GeoJSON/paginación, identificación de series piloto y observaciones de continuidad. No actualizar capturas antiguas sin una nueva entrada fechada.
