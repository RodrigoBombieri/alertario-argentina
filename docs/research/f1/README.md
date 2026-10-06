# F1 — validación controlada de fuentes y piloto

**Evidencia histórica del 1–2/10/2026. Investigación cerrada técnicamente bajo D19; fuentes pendientes con resultado NO-GO para activación.** [Acta de evaluación y trabajos de desbloqueo](CLOSURE.md). Las sondas se ejecutaron entre el 1 y el 2 de octubre UTC. Son consultas puntuales, no monitoreo de continuidad. [F0 conserva su fecha y evidencia originales](../VERIFICATION.md).

## US-01 — permisos y contratos

| Fuente/dataset | Titular y evidencia | Uso/atribución documentados | Cuota documentada | Estado para AlertaRío |
|---|---|---|---|---|
| INA A5, red `alturas_prefe` (procedencia PNA) | [Catálogo y API INA](https://www.argentina.gob.ar/ina/recursos/catalogo-informacion-hidrologica), [Swagger](https://alerta.ina.gob.ar/a5/apiUI) | Acceso de lectura comprobado; no se encontró licencia que autorice expresamente almacenar y redistribuir datos de esta red en el producto. INA advierte que datos en tiempo real no están consistidos ni validados. | Sin cuota contractual confirmada. | **pending** para publicación, historia y monitoreo repetido. `public=true` es filtro necesario, no permiso suficiente. |
| GeoRef v2, localidades y centroides | [Condiciones específicas](https://www.argentina.gob.ar/georef/condiciones-de-uso-y-licencia), consultadas el 1/10/2026 | CC BY 4.0; atribuir “Servicio Georef – argentina.gob.ar/georef” e indicar modificaciones al redistribuir. La página autoriza uso, reutilización e integración. | Para usuarios fuera de APN: 10/s, 40/min, 2000/h, 10000/día. | **documented** para estas capas; falta revisión del responsable legal antes de publicar y registrar versión/atribución en producto. |
| SMN CAP / SAT / ACP | [Índice CAP oficial](https://ssl.smn.gob.ar/CAP/AR.php), [página SAT](https://ws2.smn.gob.ar/alertas) | Existe CAP público. No se identificaron términos específicos para el feed actual; el [catálogo histórico de alertas en datos.gob.ar](https://datos.gob.ar/dataset/smn-alertas-meteorologicas-365-dias) dice «Licencia: No se especificó», lo que tampoco define el uso del CAP actual. Una licencia abierta aplicable bastaría; no se exige permiso individual por defecto. | Sin cuota del feed confirmada. | **pending técnico/documental**; no declarar cobertura completa hasta verificar feed, ciclo y términos aplicables. |

No se enviaron consultas a organismos. [Borradores para INA y SMN](OUTREACH.md). Para INA/PNA faltan condiciones por red; para SMN falta identificar términos aplicables al feed concreto y verificar su funcionamiento. Esto no presupone que SMN deba otorgar una autorización individual. GeoRef exige atribución y señalización de modificaciones.

## US-02 — inventario candidato

Las lecturas del catálogo INA `alturas_prefe` devolvieron 28 estaciones públicas en el filtro Uruguay y 37 en Paraná (30 + segunda página de 7, `is_last_page=true`). La segunda página se pidió reconstruyendo `offset=30`, sin seguir el `next_page` externo. Estos filtros no equivalen a un censo de toda la cuenca o de todas las redes. Se eligieron 12 estaciones para comparar dos tramos. Todas las estaciones y redes de esta tabla tenían `public=true` en la respuesta; ninguna queda autorizada aún para producción. Los IDs INA son externos. La fecha final del catálogo de serie carece de offset y no se interpreta aquí como un instante UTC.

| Río | Estación INA (ID) | Altura observada (serie, fin de catálogo) | Caudal no simulado (serie, fin de catálogo) | Localidad GeoRef candidata |
|---|---|---|---|---|
| Paraná | Posadas (14) | 14 · 1/10/2026 | 863 · 8/9/2017 | 54028030, componente Posadas |
| Paraná | Corrientes (19) | 19 · 1/10/2026 | 868 · 1/10/2026 | 18021020 |
| Paraná | Goya (23) | 23 · 1/10/2026 | 872 · sin fecha final | 18070020 |
| Paraná | Paraná (29) | 29 · 1/10/2026 | 878 · 10/7/2026 | 30084160, componente Paraná |
| Paraná | Rosario (34) | 34 · 1/10/2026 | 883 · 10/7/2026 | 82084270, componente Rosario |
| Paraná | San Nicolás (36) | 36 · 1/10/2026 | 885 · sin fecha final | 06763050, San Nicolás de los Arroyos |
| Uruguay | El Soberbio (61) | 61 · 1/10/2026 | 910 · 1/10/2026 | 54056010 |
| Uruguay | Santo Tomé (68) | 68 · 1/10/2026 | 917 · 1/10/2026 | 18168040 |
| Uruguay | Paso de los Libres (72) | 72 · 1/10/2026 | 921 · 1/10/2026 | 18119030 |
| Uruguay | Monte Caseros (74) | 74 · 1/10/2026 | 923 · 15/7/2026 | 18112050 |
| Uruguay | Concordia (79) | 79 · 1/10/2026 | 928 · 15/7/2026 | 30015060, localidad simple |
| Uruguay | Concepción del Uruguay (81) | 81 · 1/10/2026 | 930 · sin fecha final | 30098040 |

Los ID GeoRef son **correspondencias nominales preliminares**. Posadas, Paraná, San Nicolás y Concordia tuvieron múltiples resultados en GeoRef; la tabla selecciona una entidad plausible por nombre, provincia y categoría. Paraná devolvió incluso Villa Paranacito y San Nicolás incluyó una entidad de CABA. Antes de aprobar cualquier relación localidad/estación, hay que revisar ubicación, curso, escala y representatividad con producto/geoespacial; proximidad de centroides no basta. [Resultados completos](evidence/2026-10-01-georef-candidates.json).

### Muestra de observaciones

Las series de altura 14, 19, 34 y 79 respondieron HTTP 200 para 28–30/9 UTC: **tres observaciones por serie, separadas por 24 h y sin valores nulos en esas ventanas**. El retraso mediano entre `timestart` y `timeupdate` en la muestra fue ~11,56 h. Es una muestra de tres días y una sola consulta por serie; no fija cadencia oficial, estabilidad, retraso permitido ni `C/L/ε`. Tampoco permite Δ1/3/6/12 h. No se validaron datum, épocas ni umbrales; toda comparación de umbral sigue deshabilitada.

### Plan de medición de 14 días, condicionado a presupuesto

Tras D14, este plan es de maduración por serie **después** del arranque de la app con muestra ficticia D15. No es una espera previa para abrir la interfaz. Solo puede consultar fuentes dentro de las condiciones y cuotas aplicables; al completar el período se evalúa la evidencia antes de activar cálculos de esa serie.

1. Obtener del INA permiso/condiciones de monitoreo y presupuesto de consultas por red. Para las cuatro series iniciales, un muestreo diario durante 14 días supone **56 GET** más eventuales reintentos; evaluar frecuencia adicional solo si se busca certificar resolución subdiaria.
2. Ejecutar sondas seriales con ventana móvil máxima de tres días, guardando hora de consulta, status, tipo, hash de respuesta y tiempos/huellas de observaciones, sin valores medidos en el reporte. Comparar IDs repetidos para detectar cambios de payload. No seguir `next_page` provisto por la fuente.
3. Extender a las otras ocho series de altura y caudales elegibles solo tras revisar permiso y presupuesto. Registrar errores y ausencias como tales, no como cero ni como ausencia confirmada de datos.
4. Tras 14 días, revisar el informe con hidrología: cadencia observada, retrasos, nulos, correcciones, unidades, datum/épocas, ruido y ventanas realmente habilitadas. La aprobación debe ser por **serie**, no por estación.

Sondas reproducibles:

```powershell
./scripts/probes/f1-probe.ps1 -Mode Catalog -OutputFile docs/research/f1/evidence/catalog-nueva-fecha.json
./scripts/probes/f1-probe.ps1 -Mode Series -Ids 14,19,23,29,34,36,61,68,72,74,79,81 -OutputFile docs/research/f1/evidence/series-nueva-fecha.json
./scripts/probes/f1-probe.ps1 -Mode Observations -Ids 14,19,34,79 -FromUtc '2026-09-29T00:00:00Z' -ToUtc '2026-10-02T00:00:00Z' -OutputFile docs/research/f1/evidence/observaciones-nueva-fecha.json
$files = @(Get-ChildItem docs/research/f1/evidence -Filter '*ina-observations.json' | ForEach-Object FullName)
python scripts/probes/f1-analyze.py $files
```

Ejecutar desde la raíz del repositorio, actualizando rango y nombre en cada corrida. No hay tarea recurrente configurada. El analizador distingue cambios de payload por hash; no demuestra por sí solo que cambió el valor ni que la medición fue corregida oficialmente.

## US-03 — avisos SMN

El índice CAP oficial respondió HTTP 200 el **2/10/2026 01:26 UTC** como `text/html; charset=UTF-8`, ~101 kB, con enlaces a XML; no contenía mención a RSS en su HTML. Esta comprobación puntual **no establece un feed machine-readable completo ni su ciclo Alert/Update/Cancel**. No se extrajeron mensajes en forma operativa. SAT/ACP, política de consulta, completitud, términos aplicables y perfil color/severidad siguen pendientes de verificación documental o consulta técnica con SMN. No se ha demostrado que haga falta permiso individual para usar datos abiertos. La integración automática queda fuera del alcance Android D19; la consulta manual no equivale a cobertura oficial.

## Evidencia de esta corrida

- [Catálogo INA](evidence/2026-10-01-ina-catalog.json): primeras páginas, status, fecha, hash de contenido UTF-8 y metadatos de estaciones públicas. Segunda página Paraná: HTTP 200, 7 registros públicos, IDs extremos 45 y 131, `is_last_page=true`, 2/10/2026 01:32 UTC; comprobación puntual sin cuerpo conservado.
- [Series INA](evidence/2026-10-01-ina-series.json): 12 respuestas y metadatos de series públicas.
- [Observaciones INA](evidence/2026-10-01-ina-observations.json): cuatro respuestas, tiempos y huellas; no valores medidos.
- [GeoRef formato/paginación](evidence/2026-10-01-georef.json): `inicio=0` devuelve 2/2; `inicio=2` devuelve 0/2; GeoJSON devuelve un objeto con dos features.
- [Búsqueda GeoRef de candidatas](evidence/2026-10-01-georef-candidates.json): 12 consultas por nombre/provincia, sin aprobación automática de relación.
- INA `format=geojson&limit=1`: HTTP 200 `application/json`, objeto con `type`/`features` y un `Point`, 2/10/2026 01:27 UTC. La muestra de un punto no valida toda la red ni áreas de drenaje.

## Gates posteriores al cierre exploratorio

El [acta de evaluación F1](CLOSURE.md) registra un resultado **NO-GO** para publicar datos oficiales sin condiciones, feed y revisión de representatividad. D14/D15 movieron los 14 días reales a la maduración posterior al arranque con muestra ficticia; EXT-03 bloquea cálculos por serie, no la apertura de una beta sin datos. Los demás trabajos EXT siguen abiertos. GeoRef tiene licencia y cuota publicadas; falta aplicar su atribución y revisión legal del producto. Ningún archivo de esta carpeta es una allowlist de producción.
