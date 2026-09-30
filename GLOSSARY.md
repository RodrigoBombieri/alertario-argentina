# Glosario

| Término | Significado en AlertaRío |
|---|---|
| Altura hidrométrica | Nivel de superficie respecto del cero de una escala; no profundidad navegable |
| Cota / datum | Referencia vertical; debe coincidir para comparar niveles/umbrales |
| Caudal | Volumen por tiempo, normalmente m³/s; no se deduce de altura sin relación válida |
| Estación | Sitio/instrumentación de observación; puede tener varias series |
| Serie | Variable + estación + fuente + procedimiento + unidad + soporte temporal + época |
| Época de estación | Intervalo con ubicación/cero/procedimiento compatibles |
| Soporte temporal | Intervalo representado por un dato: instante, promedio o acumulación |
| Cuenca | Área cuyo drenaje aporta a una salida; no círculo de proximidad |
| Aguas arriba | Relación de red de drenaje, no “al norte” ni cerca |
| Observación | Medición o registro fuente, con tiempo/calidad |
| Simulación / pronóstico | Resultado estimado para tiempos/escenarios; separado de mediciones |
| Umbral oficial | Referencia publicada por organismo; superar valor no prueba aviso emitido |
| Aviso/alerta oficial | Comunicación emitida por autoridad identificable con ámbito y vigencia |
| Orden de evacuación | Instrucción de autoridad competente; nunca cálculo automático de la app |
| Seguimiento calculado | Condición determinada por algoritmo de AlertaRío, no aviso oficial |
| Tendencia | Signo de cambio en ventana explícita con tolerancia de ruido aprobada |
| Stale | Dato que excede antigüedad admitida para su serie; no equivale a sensor averiado |
| Cadencia | Frecuencia observada/esperada de medición, diferente del polling propio |
| Retraso de publicación | Diferencia entre instante medido y disponibilidad en fuente |
| Ingesta | Lectura, normalización, validación y persistencia en backend propio |
| Backfill | Incorporación de historia faltante, sin notificar como novedad actual |
| Reconciliación | Relectura de intervalos para detectar correcciones/datos tardíos |
| Cuarentena | Registro preservado pero excluido de cálculos hasta resolver anomalía |
| CAP | Common Alerting Protocol; formato de mensajes oficiales, no garantía por sí solo de autenticidad |
| ACP | Aviso a muy corto plazo del SMN; no se asume mismo feed/semántica que SAT |
| SAT | Sistema de Alerta Temprana; verificar producto/fenómeno específico |
| GeoJSON | Formato geográfico; coordenadas usuales longitud, latitud |
| EPSG:4326 | Referencia geográfica usada para intercambio del diseño |
| GeoRef | Servicio argentino de normalización geográfica; no modelo hidrológico |
| Circuit breaker | Suspensión temporal de llamadas tras fallas repetidas |
| Idempotencia | Repetir operación no duplica su efecto lógico |
| Outbox | Evento persistido con cambio de estado para despacharlo después sin pérdida lógica |
| Cooldown | Intervalo mínimo de repetición por regla, no garantía de novedad |
| Histeresis | Banda para rearmar una condición evitando oscilación en el borde |
| TTL | Tiempo de vida; evento vencido no se envía como actual |
| FCM / APNs | Transportes push; aceptación no prueba entrega ni lectura humana |
| Instalación | Identidad seudónima de dispositivo/app, no cuenta ciudadana |
| SLA / SLO / SLI | Compromiso contractual / objetivo propio / indicador medido |
| RPO / RTO | Pérdida temporal de datos tolerada / tiempo objetivo de recuperación |
| MUST/SHOULD/COULD/NOT NOW | Necesario / deseable / opcional / fuera del alcance actual |
| ADR | Registro de contexto y decisión arquitectónica |
