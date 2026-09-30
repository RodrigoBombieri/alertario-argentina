# Investigación de competidores

Corte: 2026-09-29. Investigación de páginas oficiales de producto, documentación y fichas publicadas por sus desarrolladores en tiendas. **No se instalaron apps ni se hizo prueba de usabilidad en dispositivos.** UX y debilidades son evaluación del posicionamiento documentado, no mediciones. “No verificado” no significa que una función no exista. No se usan reseñas aisladas como prueba de calidad.

## Matriz de evidencia

| Producto y actividad pública | Funciones y UX declaradas | Cobertura / fuentes | Alertas, mapa e histórico | Negocio | Evaluación para AlertaRío |
|---|---|---|---|---|---|
| RiverApp; ficha Play actualizada 24/08/2026 | Consulta por río, favoritos, niveles/caudales, orientación a deportes de agua | Internacional; sitio enumera Europa, Norteamérica, Australia y Nueva Zelanda. Cobertura argentina no comprobada | Push por nivel/caudal, mapa estaciones y tramos; históricos/pronósticos seleccionados | Gratis con publicidad y compras Premium | Fortaleza: consulta integrada y profundidad funcional. Oportunidad local: lenguaje ciudadano argentino y trazabilidad de avisos; no afirmar que carece de toda función comparable |
| PegelAlarm / FloodAlert, SOBOS; fichas/FAQ disponibles | Monitoreo de umbrales altos y bajos, herramientas de seguimiento | Varios países; cantidad/cobertura exacta argentina no verificada | Umbrales por estación, mapas/historial según ficha; FAQ indica avisos con observaciones pasadas, no pronóstico como disparador | Freemium; funciones adicionales Pro | Fortaleza: personalización. Preguntas para benchmark: explicar antigüedad, colores y diferencia entre umbral/aviso. No inferir ausencia de monitoreo interno |
| Altura del río, Mastra/BugLand; Play 01/03/2026 | Nivel, tendencia, muelle ilustrado, viento, widget y consulta local | Delta/Tigre/Río de la Plata; mareógrafo propio y fuentes SHN/AySA declaradas según función | Notificaciones por nivel, gráficos 24 h y pronóstico mareológico; mapa de estaciones nacional no verificado | Funciones pagas/compras en app | Fortaleza: gran contexto local y visual cotidiano. Límite competitivo: alcance especializado; AlertaRío no necesita reproducir animación de muelle |
| Altura del Rio, Mauricio I. Gomez; Play 24/07/2025 | Alturas de ríos argentinos, enlaces/clima, buques, radar y calendario lunar | Declara web de Prefectura como fuente; periodicidad depende del puerto, según autor | Push, mapa hidrológico y gráficos avanzados no confirmados en ficha | Contiene anuncios | Fortaleza: propuesta directa y reconocible. Profundidad UX y operación real no probadas; oportunidad para normalización e insuficiencia temporal explícita |
| “Altura de los Ríos” | Nombre ambiguo; búsqueda no resolvió una app única con editor/package verificable | Se encontró [servicio web homónimo](https://ciberperiodismo.com.ar/index.php/servicios/servicios-menuarriba) que atribuye datos a PNA | No se verificaron notificaciones, mapa ni gráficos de una app con ese nombre exacto | No verificado | No confundir con las dos apps anteriores. Requiere URL/package para benchmark específico; no inventar características |
| Náutica RdP; sitio y política publicados en 2026 | Mareas, clima/viento, radioavisos, carta AIS, comunidad y asistente | Río de la Plata; declara INA, AGPSE, PNA y estaciones propias; carta OpenSeaMap | Mapa náutico, pronóstico/curvas y avisos de marea/viento declarados | Base gratuita + Nauti+ | Fortaleza: integra tareas del navegante. Alcance y densidad orientados a náutica; AlertaRío prioriza lectura ciudadana y no competirá en AIS/comunidad |

Fuentes: [RiverApp sitio](https://www.riverapp.net/en), [RiverApp Play](https://play.google.com/store/apps/details?hl=en-US&id=de.android.riverapp), [FloodAlert FAQ](https://www.pegelalarm.at/en/faqs.php), [FloodAlert ficha](https://apps.apple.com/at/app/pegelalarm-hochwasser-warnung/id1022182982?platform=ipad), [Altura del río sitio](https://www.alturadelrio.com/), [manual visual](https://www.alturadelrio.com/manual/basico/), [Mastra Play](https://play.google.com/store/apps/details?hl=es&id=com.molol.alturario), [Mauricio Gomez Play](https://play.google.com/store/apps/details?hl=es&id=appinventor.ai_mauriciogz.AlturaRio), [Náutica RdP](https://nauticardp.com.ar/), [privacidad Náutica](https://nauticardp.com.ar/privacidad).

## Competencia indirecta argentina

Los portales [INA A5](https://alerta.ina.gob.ar/a5/secciones), [PNA](https://www.argentina.gob.ar/prefecturanaval/consulta-el-estado-de-los-rios) y [Santa Fe](https://www.santafe.gob.ar/idesf/geoportal/paginas/situacion-hidrica) compiten por la tarea de consulta, aunque también sean proveedores. Su valor es la procedencia institucional; AlertaRío debe complementar acceso y comprensión, manteniendo enlace a ellos. No se verificó otra app argentina adicional con suficiente evidencia primaria para describirla como producto activo distinto; no inflar el mapa competitivo con APKs o nombres parecidos.

## Diferenciación propuesta y verificable

1. Iniciar por localidad argentina y explicar qué estación se consulta y por qué; no atribuir cobertura que no existe.
2. Presentar hora medida, hora consultada y suficiencia temporal. Mostrar Δ indisponible si la fuente no permite esa ventana.
3. Separar visual y semánticamente aviso oficial, referencia oficial y análisis calculado.
4. Mantener lectura offline con antigüedad y proveniencia, sin fingir actualización.
5. Notificaciones por episodios, silencio configurable y controles de calidad; no maximizar cantidad de mensajes.

Son prioridades de diseño, no afirmaciones de exclusividad mundial. Validar con usuarios que aporten utilidad frente a herramientas actuales.

## Benchmark pendiente antes de UX final

Probar en Android/iOS disponibles: buscar la misma localidad, encontrar altura/hora, configurar un aviso, simular desconexión y localizar fuente/metodología. Registrar pasos, tiempos y confusiones con capturas permitidas. Comparar coste según país/tienda vigente, sin usar precios de otro mercado. Evaluar también accesibilidad. Identificar primero la app exacta “Altura de los Ríos”.
