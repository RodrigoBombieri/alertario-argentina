# Fuentes de información

Fecha de revisión: **2026-09-29**. Convenciones: **D** = documentación consultada; **H** = respuesta HTTP comprobada; **P** = pendiente. H no demuestra SLA, exactitud científica ni autorización de redistribución. [Registro reproducible](docs/research/VERIFICATION.md).

**Actualización F1, 1/10/2026:** [registro y nuevas evidencias](docs/research/f1/README.md). La licencia y cuotas de GeoRef quedaron documentadas en su página específica; INA/SMN y la selección de series siguen condicionados.

**Evaluación F1, 2/10/2026:** [resultado NO-GO](docs/research/f1/CLOSURE.md). F1 no está aceptada. Esta ficha sigue como inventario de fuentes, no como autorización de publicación.

## 1. Instituto Nacional del Agua

El [organismo describe su aplicación REST](https://www.argentina.gob.ar/node/228063) como acceso a estaciones, áreas, series observadas y simuladas, históricas y recientes, principalmente de la Cuenca del Plata. Incluye altura, caudal y precipitación, datos vectoriales y ráster. Esta capacidad general no demuestra que cada estación tenga todas las variables o pronóstico.

La [interfaz oficial A5](https://alerta.ina.gob.ar/a5/apiUI) enlaza Swagger. Su HTML configura la [especificación oficial](https://alerta.ina.gob.ar/a5/yaml/apidocs_v3_1.yml), guardada en [evidence/ina-openapi.yaml](docs/research/evidence/ina-openapi.yaml). Declara OpenAPI 3.1.0, versión de aplicación 1.0.0 y servidor relativo; para las consultas verificadas se usó `https://alerta.ina.gob.ar/a5`. No usar `localhost` de la especificación como servidor público. El inicializador genérico contiene Petstore, pero el HTML incluye una configuración INA posterior: se revisaron ambos, evitando confundir esa plantilla con la API.

### Recursos documentados

Todas las rutas de esta tabla provienen del contrato leído. Son externas, no propuestas de AlertaRío.

| GET relativo a `/a5` | Utilidad | Verificación |
|---|---|---|
| `/obs/puntual/estaciones` | Catálogo; nombre, río, red, geometría y paginación | D + H con Concordia |
| `/obs/puntual/estaciones/{id}` | Estación individual | D; no ejecutado |
| `/obs/puntual/series` | Series por estación, variable, procedimiento y unidad | D + H, estaciones 1 y 79 |
| `/obs/puntual/series/{id}` | Serie individual | D; no ejecutado |
| `/obs/puntual/series/{series_id}/observaciones` | Intervalo con `timestart`, `timeend`; filtro `timeupdate`, `skip_nulls` | D + H, series 1 y 79 |
| `/obs/variables` | Diccionario de variables | D; no ejecutado |
| `/obs/unidades` | Diccionario de unidades | D; no ejecutado |
| `/obs/puntual/fuentes` | Redes/fuentes | D; no ejecutado |
| `/sim/modelos` | Modelos de simulación | D, declara cookieAuth; fuera de MVP |

El [visualizador de series](https://alerta.ina.gob.ar/a5/secciones) también documenta JSON, CSV, GeoJSON y alternativas de exportación. Adoptar JSON para observaciones; verificar GeoJSON en fase 1 antes de depender de él. No mezclar la interfaz antigua `/pub` o `/res` con A5: la [documentación de servicios antiguos](https://alerta.ina.gob.ar/pub/gui/series) fue localizada, pero no se certificó equivalencia ni vigencia operativa.

### Qué demostraron las consultas

- Catálogo de Concordia: estación `id=79`, `tabla=alturas_prefe`, `id_externo=700`, propietario `PNA`, coordenadas GeoJSON `[-58.0166666666667,-31.4]`. Metadatos `nivel_alerta=11`, `nivel_evacuacion=12.5`, `cero_ign=1.29`. Son valores observados en el catálogo, **no certificación de vigencia normativa ni orden emitida**. La unidad y compatibilidad del umbral con la serie deben ratificarse antes de activar comparaciones.
- Serie 79: altura hidrométrica en metros. Serie 928: caudal en `m^3/s`; su rango de catálogo terminaba el 15 de julio de 2026. No mostrar caudal como actual usando la frescura de altura.
- Para la serie 79 se recibieron tres lecturas separadas por 24 horas entre el 27 y el 29 de septiembre. Esto **no habilita variaciones 1/3/6/12 h** ni demuestra que toda la historia sea diaria. La observación del 29 tenía `timestart=03:00Z` y `timeupdate=13:33:33.687Z`: la diferencia no debe ocultarse tras “actualizado”.
- La serie 1 respondió con observaciones históricas cada cuatro horas en el intervalo consultado. El registro de catálogo decía `has_obs=false` pese a devolver historia; ese indicador no basta para decidir disponibilidad.
- Se recibieron `unit_id:null` en observaciones, mientras la serie tenía unidad. También nulos JSON y texto `"null"` en metadatos; fechas `date_range` sin offset; estaciones de varias redes bajo un mismo nombre.
- La respuesta de series usa `{rows:[...]}`, aunque la sección GET del contrato describe un array. La paginación de estaciones devolvió un `next_page` HTTP sin `/a5`. El adapter no debe seguirlo ciegamente.
- El catálogo incluyó registros `public=false`; **no incorporarlos al producto**. Que aparezcan en una respuesta pública no autoriza su uso. El campo `access_level` no concede permiso a AlertaRío.

### Autenticación, límites, permisos y calidad

Los GET ejecutados respondieron 200 sin credenciales ni sesión. La especificación incluye cookieAuth (cookie `id`, obtenida por login) en otras operaciones. No concluir que toda la API es anónima. No se ejecutó login ni ninguna escritura.

No se encontró un rate limit contractual, SLA, calendario general de publicación ni licencia específica que cubra inequívocamente todas las redes redistribuidas por INA. Marcar **pendiente** el permiso de almacenamiento, publicación, historial y eventual uso comercial por red. Los [términos de Argentina.gob.ar](https://www.argentina.gob.ar/terminos-y-condiciones) no se trasladan automáticamente a un servicio INA o a datos de terceros. La licencia del código A5 tampoco es licencia de cada dato.

**Decisión:** proveedor principal MVP, condicionado a una lista de series públicas aprobadas. Solicitar confirmación al canal oficial del organismo sobre uso, cuotas, zona horaria, datum, umbrales y contacto técnico. No afirmar confiabilidad de largo plazo con una consulta. Medir al menos 14 días en fase 1/beta con poca carga.

## 2. Servicio Meteorológico Nacional

La [página oficial de alertas](https://ws2.smn.gob.ar/alertas) distingue alertas, advertencias y avisos a muy corto plazo. Su pie declara CC BY 2.5 Argentina para contenidos del sitio. Confirmar alcance sobre cada feed y mantener atribución; no equiparar licencia editorial con SLA.

| Producto | Evidencia | Consumo legítimo propuesto | Alcance |
|---|---|---|---|
| Alertas meteorológicas CAP | Registro OMM + índice SMN y un XML real leídos | Feed oficial estable una vez ratificado | MUST condicionado |
| Avisos a muy corto plazo (ACP) | Existencia oficial documentada; operación de un feed ACP actual no comprobada | Confirmar feed y ciclo de vida propio | SHOULD; no fingir cobertura ACP |
| Precipitación observada | Portal de descarga localizado; catálogo completo no recuperado | Archivos oficiales con contrato por dataset | Futuro |
| SQPE-OBS | Anuncio oficial de producto experimental diario | Dataset autorizado; distinguir estimación de observación puntual | Futuro |
| Pronóstico WRF | Registro AWS gestionado por SMN, documentación enlazada | S3 público según instrucciones del dataset | Futuro, no alertas |
| Radar | Portal oficial de imágenes localizado | Confirmar servicio geoespacial/licencia; no extraer valores de colores | Futuro |

### CAP: hallazgo y limitación concreta

El [registro de autoridades de la OMM](https://alertingauthority.wmo.int/authorities.php?recId=4) publica `https://ssl.smn.gob.ar/CAP/AR.php`. Se comprobó HTTP 200, pero devuelve **HTML**, no un feed XML de catálogo. Un enlace explícito de esa página devolvió un archivo CAP 1.2 (`text/xml`) con `identifier`, `sender`, `sent`, `status=Actual`, `msgType=Update`, `scope=Public`, `references`, `info`, `onset`, `expires` y `polygon`. `areaDesc` estaba vacío. El ejemplo no incluía un color argentino explícito: **no convertir `severity=Moderate` automáticamente en amarillo** sin perfil oficial verificado.

El XML real está guardado como [ejemplo histórico](docs/research/evidence/smn-cap-example.txt). No fijar esa URL fechada en un adapter. Se siguió un enlace público para inspeccionar el formato; no se implementó un scraper de la página. La ruta `/rss` del dominio principal dio 403; el intento en `ws2` falló por DNS en el entorno. Esto no demuestra que SMN esté caído.

**Pendiente crítico:** confirmar con SMN la URL del feed machine-readable vigente, su exhaustividad (SAT/ACP), las actualizaciones/cancelaciones, retención, cuotas y perfil de colores. No automatizar extracción de HTML mientras exista o se esté validando la alternativa oficial. Si no se resuelve, la beta puede mostrar enlace oficial y “No pudimos consultar los avisos”; el MVP completo solicitado **no se considerará aceptado** hasta integrar alertas zonales.

La [publicación SQPE-OBS](https://ws2.smn.gob.ar/noticias/nueva-herramienta-estimacion-lluvia-por-satelite-datos-sqpe) describe una estimación diaria combinada. El [dataset WRF en AWS](https://registry.opendata.aws/smn-ar-wrf-dataset/) documenta pronóstico, licencia CC BY 2.5 AR, ciclos 00/12 UTC y acceso S3 sin cuenta. Se verificó documentación, **no descarga ni continuidad de objetos S3**. No usar precipitación pronosticada como lluvia ya ocurrida.

## 3. GeoRef Argentina

[Documentación oficial v2.1](https://www.argentina.gob.ar/georef/documentacion-y-recursos-georef-v21) y [referencia v2](https://www.argentina.gob.ar/georef/referencia-completa-de-la-api-georef-v-2). La referencia enlaza [OpenAPI en el repositorio oficial](https://raw.githubusercontent.com/datosgobar/georef-ar-api/refs/heads/development/docs/open-api/spec/openapi.json), guardado en el repositorio. El documento dice `info.version=0.5.X` y `servers.url=https://apis.datos.gob.ar/georef/api/v2.0`: los tres rótulos no son equivalentes.

GET `/localidades?nombre=Concordia&max=3` sobre esa base respondió 200 sin autenticación. Incluyó `cantidad`, `inicio`, `total`, `parametros` y `localidades`; cada localidad contiene id textual, nombre, categoría, centroide, provincia, departamento, gobierno local y localidad censal. Devolvió una localidad simple y una entidad homónima: no fusionarlas por nombre.

El contrato documenta `/provincias`, `/departamentos`, `/gobiernos-locales`, `/municipios`, `/localidades`, `/localidades-censales`, `/asentamientos`, `/direcciones`, `/ubicacion` y otros recursos. Solo se ejecutó búsqueda de localidades. Descargas masivas, geometrías, geocodificación inversa y paginación completa quedan para fase 1. El portal marca v1 como discontinuada: no arrancar con la base antigua por familiaridad.

**Uso MVP:** búsqueda, nombres e IDs oficiales y centroides. Importación periódica de catálogo local; no pedir dirección personal. Guardar IDs como texto con tipo y versión. GeoRef no provee por sí mismo una relación localidad/río representativo ni cuenca aguas arriba.

La [página específica de condiciones de GeoRef](https://www.argentina.gob.ar/georef/condiciones-de-uso-y-licencia), consultada el 1/10/2026, declara CC BY 4.0 para la información publicada por el servicio, atribución obligatoria como “Servicio Georef – argentina.gob.ar/georef” o enlace oficial, e indicación de cambios al redistribuir. Para usuarios fuera de APN publica cuotas de 10 consultas/s, 40/min, 2000/h y 10000/día. Autoriza uso, reutilización e integración, sin garantía de continuidad. Registrar esta evidencia y la revisión legal del producto antes de publicar; no trasladar la licencia de GeoRef a datos INA o SMN. En F1 se comprobaron GeoJSON y paginación acotada; ver [registro](docs/research/f1/README.md).

## 4. Complementarias: no agregarlas automáticamente

| Fuente | Evidencia oficial consultada | Clasificación | Razón / condición |
|---|---|---|---|
| Prefectura Naval | [Consulta de ríos](https://www.argentina.gob.ar/prefecturanaval/consulta-el-estado-de-los-rios), [mareógrafos](https://www.argentina.gob.ar/node/85894) | MVP como procedencia de series PNA vía INA; integración directa futura | API pública documentada no verificada; evitar duplicar la misma medición |
| SNIH / Red Hidrológica Nacional | [Condiciones y revisiones](https://www.argentina.gob.ar/node/439689), [red](https://www.argentina.gob.ar/node/43071) | Futura | Descarga libre con atribución indicada; revisar cambios de escala y homogeneidad; sin feed operativo verificado |
| Santa Fe / IDESF | [Situación hídrica](https://www.santafe.gob.ar/idesf/geoportal/paginas/situacion-hidrica), [red provincial](https://www.santafe.gob.ar/index.php/web/content/view/full/250459) | Futura | Portal e históricos documentados; contrato de consumo automático pendiente |
| SINAGIR / SINAME | [Sitio institucional](https://www.argentina.gob.ar/sinagir) | Futura | Coordinación y recomendaciones; no se verificó API de incidentes públicos |
| Defensa Civil provincial/municipal | No se verificó un contrato uniforme nacional | Futura, por convenio | Validar emisor, territorio, vigencia, revocación y autorización por jurisdicción |
| Salto Grande | Red y enlace de propietario encontrados en catálogo INA | Futura integración directa | No se verificó contrato propio; primero evaluar serie pública vía INA |
| SHN / INTA | Publicaciones y enlaces oficiales localizados | Futura | Mareas/radares requieren producto y referencia específicos |
| Endpoints internos con tokens extraídos, espejos no oficiales, OCR automatizado de boletines | Sin contrato público comprobado | No recomendable | Fragilidad, derechos inciertos y riesgo semántico |

## 5. Resultado legal/técnico

Técnicamente comprobados: lectura pública acotada INA, localidad GeoRef y un CAP SMN. GeoRef tiene condiciones de uso específicas documentadas; autorización de distribución del producto con INA/SMN: **no resuelta**. Exigir una ficha por dataset con titular, licencia o permiso, atribución, usos, retención, versión y fecha de revisión. No utilizar datos marcados no públicos. Las verificaciones no reemplazan revisión jurídica de publicación y tratamiento de datos personales.
