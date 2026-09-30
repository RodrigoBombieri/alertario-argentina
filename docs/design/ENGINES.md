# Motores determinísticos

Especificación propuesta v1, independiente de UI, HTTP y DB. Recibe datos normalizados y reloj inyectado. Todos los parámetros numéricos siguientes son políticas de AlertaRío **por validar**, no criterios oficiales de peligro.

## 1. Variaciones y tendencia

Entrada: serie observada homogénea, unidad, datum/época, precisión conocida, cadencia aprobada, calidad de muestras, instante `now`. Salida por ventana: valor actual, referencia, Δ, tiempos efectivos, método, suficiencia, tendencia y versión.

### Algoritmo v1

1. Ordenar por instante observado; resolver duplicados/revisiones previamente. Excluir registros inválidos y cuarentenados. No mezclar promedios diarios con lecturas instantáneas.
2. Seleccionar última observación aceptable `x0,t0` no futura. Si hay una observación posterior sospechosa, mantener la aceptada como última válida **con advertencia visible**; no ocultar la más reciente sospechosa en avanzado. Sin dato válido ⇒ indisponible. Si stale ⇒ mostrar dato histórico, no afirmar tendencia actual.
3. Para cada W de 1,3,6,12,24 h definir objetivo `t*=t0-W`. No usar hora de ingesta ni `now-W` como si la medición fuese de ahora.
4. Buscar observación válida más próxima a `t*`, distinta de `t0`, dentro de tolerancia `τ=min(W/10,C/2)` si C es cadencia validada. En empate elegir anterior. Si C desconocida, permitir solamente referencia exacta hasta configurar tolerancia. Una serie diaria no tendrá τ suficiente para inventar Δ6 h.
5. Exigir mismo datum/época/unidad/procedimiento, intervalos comparables y ningún cambio de referencia dentro del tramo. Si no hay referencia, devolver `insufficientData` y razón; nunca relleno con cero. V1 no interpola ni extrapola. Si referencia no exacta, devolver duración real y UI “aprox. 6 h”.
6. Δm = x0 − xr; Δcm = 100·Δm. Calcular con valores normalizados sin redondeo previo. Presentación redondea según precisión; el motor compara sin redondear.
7. Tendencia **por ventana**: `creciente` si Δ > ε; `bajante` si Δ < −ε; `estable` si |Δ| ≤ ε. Ejemplo piloto ε=0,02 m solo para series cuya resolución/error justifiquen ese valor; configuración por serie `ε=max(2·resolución, tolerancia_aprobada)`. Si precisión desconocida, no habilitar estable/creciente automáticamente con un ε arbitrario; validar preset y marcar metodología.
8. Tendencia principal intenta 6 h; si insuficiente, prueba 12 y 24 h y muestra siempre ventana elegida. Sin ninguna suficiente, “Tendencia no disponible”. “Estable” solo es resultado de comparación válida.

Una etiqueta creciente 24 h puede coexistir con bajante 1 h; no es contradicción. No ocultar la ventana. Una flecha muestra variación de nivel, **no dirección de corriente**.

### Ruido, huecos y outliers

No suavizar el valor actual. V1 muestra Δ de extremos válidos, no una pendiente de regresión pretendidamente predictiva. El gráfico corta líneas cuando hay huecos mayores que el máximo aprobado por serie (propuesta 2C); no une períodos sin datos. Una variación de extremos puede ser válida con huecos intermedios, pero se etiqueta `interiorGaps=true`: describe diferencia, no trayectoria continua.

Registros fuera de rangos físicos documentados se cuarentenan. Sin límites físicos confirmados, no inventar máximos universales. Un salto respecto de vecinos o filtro robusto MAD puede marcar `suspect` para revisión; una crecida real también puede ser rápida. No reemplazar automáticamente el salto por una mediana. Cuando una lectura sospechosa amenaza invalidar un cruce, detener reglas calculadas e informar calidad; mantener avisos oficiales independientes. Validar este compromiso con especialista hidrológico.

Un suavizado de mediana de tres puntos puede evaluarse después solo para visualización claramente rotulada, conservando la curva observada y sin afectar umbrales. No es parte del motor v1.

### Porcentajes

No usar porcentaje de altura: el cero hidrométrico es una referencia local y hace engañosa la razón porcentual. Para caudal, opcional en vista avanzada: `100*(Q0-Qr)/abs(Qr)` únicamente si Qr está lejos de cero según resolución y dentro de una serie comparable; con Qr=0, dirección reversible o significado dudoso ⇒ no disponible. La variación absoluta es la principal.

### Casos de prueba de aceptación

En ejemplos de tendencia ε=0,02 m ya aprobado, tiempos exactos y frescura válida salvo indicación.

| ID | Entrada | Resultado |
|---|---|---|
| TR-01 | 7,48 m ahora; 7,32 m hace 6 h | +16 cm, creciente 6 h |
| TR-02 | 7,48 / 7,50 | −2 cm, estable en borde |
| TR-03 | 7,48 / 7,51 | −3 cm, bajante |
| TR-04 | Solo lectura actual | todas las ventanas insuficientes |
| TR-05 | Lecturas diarias | 1/3/6/12 h insuficientes; 24 h solo si referencia válida |
| TR-06 | Objetivo 6 h, C=1 h, referencia a 5 h 45 min | Aceptar; duración efectiva 5 h 45 min y aproximada |
| TR-07 | Mismo caso, referencia a 5 h 20 min | Fuera de τ=30 min; insuficiente |
| TR-08 | Duplicados, orden invertido | Mismo resultado que serie ordenada sin duplicados |
| TR-09 | Unidad cm y m en normalización autorizada | Mismo Δ; unidad desconocida bloquea |
| TR-10 | Cambio de datum entre extremos | No comparable, nunca tendencia |
| TR-11 | Última medición demasiado antigua | Δ histórico visible; tendencia actual suspendida |
| TR-12 | Salto marcado sospechoso | Advertencia de calidad; no notificación calculada |
| TR-13 | Alturas −0,10 y −0,20 m válidas | +10 cm, creciente; porcentaje no disponible |
| TR-14 | Timestamp sin zona/no resuelto o futuro >5 min | Cuarentena; no edad negativa |
| TR-15 | Δ24 h positivo, Δ1 h negativo | Dos resultados válidos con ventana explícita |
| TR-16 | Mismos valores y tiempos UTC convertidos a −03 | Resultado idéntico |
| TR-17 | Revisión corrige dato de ayer | Recalcular histórico versionado; no push retroactivo |
| TR-18 | Hueco interior, extremos válidos | Δ con interiorGaps; curva partida |
| TR-19 | Qr=0 | Δ absoluto sí; porcentaje no |

## 2. Estado: tres ejes, no un semáforo único de riesgo

Se modelan por separado `dataStatus`, `calculatedCondition`, `officialNotices`. Una fuente meteorológica caída no anula el nivel del río; un nivel viejo no oculta una alerta oficial vigente.

| Condición | Texto | Origen |
|---|---|---|
| Sin medición aceptable | SIN DATOS | Disponibilidad |
| Medición stale | DATOS DESACTUALIZADOS | Antigüedad calculada |
| Cadencia/calidad sin validar | Antigüedad/calidad por verificar | Disponibilidad incierta |
| Datos suficientes y ninguna condición calculada relevante | Sin cambios destacados | Cálculo; evita promesa “seguro” |
| Crecimiento significativo configurado | Tendencia que merece seguimiento | Análisis AlertaRío |
| Cruce de umbral oficial verificado | Nivel por encima del umbral oficial de alerta/evacuación | Comparación calculada con referencia oficial |
| Aviso oficial activo, ámbito aplicable | ALERTA OFICIAL · emisor · fenómeno | Documento/registro oficial |
| Orden oficial de evacuación vigente y específica | Orden de evacuación publicada por [autoridad] | Solo fuente habilitada; fuera del feed automático MVP si no existe |
| Consulta avisos fallida | No pudimos verificar los avisos actuales | Disponibilidad, no “sin alertas” |

No usar **EVACUACIÓN** como resultado de `height >= evacuationThreshold`. El nombre del umbral se conserva como referencia, junto a “No constituye una orden”. Tampoco usar “NORMAL” como declaración de riesgo: si el diseño final mantiene ese rótulo, debe limitarlo a “por debajo de umbrales disponibles”, jamás cuando faltan umbrales o avisos. La propuesta de UX adopta “Sin cambios destacados” o “Nivel bajo el umbral”, que dicen qué se calculó.

Atención propuesta: aumento ≥0,20 m en 6 h con datos aptos; parámetro de seguimiento a validar por serie, no riesgo de inundación. No habilitarlo nacionalmente con un único corte.

Prioridad visual: aviso/orden oficial vigente en tarjeta independiente arriba; después limitaciones del dato y ficha hidrológica. Si no hay aviso confirmado y el feed está actualizado/completo, decir “Sin avisos vigentes en la consulta de [hora] para este punto”. Un valor bajo no degrada una alerta oficial. Umbral vencido no participa de comparación; se puede mostrar como referencia histórica en avanzado.

Pruebas: cruce 11 m sin aviso nunca produce ALERTA OFICIAL; nivel 12,50 no produce orden; aviso activo + hidrometría stale conserva ambos; feed fallido no limpia avisos vigentes; expiración elimina vigencia pero no historial; null threshold no muestra NORMAL; Cancel antes de Alert no resucita el aviso.

## 3. Reglas de notificación

### Tipos configurables

| Regla | Semántica exacta |
|---|---|
| Aumento 20/50 cm | ΔW ≥ umbral, con ventana elegida explícitamente por usuario; no “desde alguna hora” |
| Supera X m | transición de valor <X a ≥X en la misma serie/datum |
| Alcanza umbral oficial | mismo cruce contra versión aprobada del umbral; texto informa comparación calculada |
| Nuevo aviso meteorológico | mensaje oficial vigente y aplicable al objetivo; identidad/revisión verificadas |
| Cambio significativo de tendencia | transición entre categorías confirmada por dos nuevas observaciones consecutivas aptas; ventana fija |

Crear regla con condición ya verdadera no genera alarma sorpresiva: mostrar situación actual y comenzar monitoreo; ofrecer confirmación explícita de un aviso inicial informativo si se incorpora. Reglas por altura no se aplican al favorito localidad hasta elegir una estación concreta.

### Máquina de episodios y deduplicación

`armed → triggered → waitingForReset → armed`. Cruce numérico rearma tras dos observaciones válidas por debajo de `X-hysteresis`; propuesta histeresis `max(2·resolución,0,02 m)` y persistencia configurable. Para ΔW, rearmar tras condición falsa en dos observaciones. Cada rearmado crea nuevo episodeId. No disparar cada sondeo mientras continúa arriba.

Cooldown por regla calculada propuesto 6 h, validable y configurable; agrupar cambios menores por estación. Avisos oficiales usan ID/revisión y cambios materiales (severidad, ámbito, vigencia o instrucciones) y no quedan bloqueados por cooldown hidrológico. Idéntico Update con otra hora de descarga no es nuevo evento. Un cambio de umbral causa notificación informativa de configuración si corresponde, no falso cruce hidrológico.

Clave lógica: `(installation,ruleId,ruleVersion,episodeId,causeRevision)`; para CAP, identidad del mensaje y revisión material. Restricción única en DB más transacción de rule_state+event+outbox. Reinicio/reintento no crea otro evento. FCM puede entregar duplicados: mobile deduplica eventId. No prometer exactly-once end-to-end.

Prioridad ordinaria para cambios; prioridad alta de transporte solo para aviso vigente relevante según reglas de plataforma. Respetar permisos y horario silencioso configurado; si se permite excepción para avisos oficiales, debe ser elegida por el usuario, no asumida. Orden oficial no se fabrica por aumentar prioridad del push.

TTL calculada: mínimo entre vigencia restante del aviso y máximo de producto; propuesta hasta 1 h para novedades hidrológicas. Al despachar revalidar vigente, no cancelado, regla habilitada, token vigente y frescura. Cuando vence, archivar sin reenviar. Reintentos de transporte con backoff solo dentro de TTL. No enviar backlog histórico tras volver de una caída.

Historial in-app disponible sin permiso push, asociado a instalación. “Leído” es acknowledgment de interfaz opcional, no prueba de que la persona esté a salvo. Diferenciar `queued`, `acceptedByProvider`, `failed`, `expired`, `opened`; no mostrar “entregado” con solo una aceptación FCM.

### Pruebas de notificaciones

NT-01: misma observación 20 veces ⇒ un evento. NT-02: dos workers compiten ⇒ una fila outbox. NT-03: reinicio tras aceptación FCM antes de commit ⇒ posible reentrega, app deduplica. NT-04: valor oscila en borde ⇒ no tormenta de pushes. NT-05: dato stale/cuarentenado ⇒ no regla calculada. NT-06: replay histórico ⇒ cero pushes. NT-07: alerta actualizada/cancelada ⇒ ciclo consistente. NT-08: cancelación antes de despacho ⇒ suprimir. NT-09: revocar instalación ⇒ no futuros envíos. NT-10: nuevo episodio tras rearme y cooldown ⇒ nuevo evento. NT-11: regla editada offline ⇒ versión previa sigue vigente hasta sync. NT-12: aviso en punto fuera del polígono ⇒ no aplica. NT-13: cuenta inexistente ⇒ configuración funciona por instalación. NT-14: GPS negado ⇒ suscripción por localidad funciona.

## 4. Transporte

[FCM publica mensajería sin coste](https://firebase.google.com/pricing), pero hosting, almacenamiento y otros servicios no son gratuitos por eso. Su [TTL y entrega](https://firebase.google.com/docs/cloud-messaging/customize-messages/setting-message-lifespan) requieren tratamiento explícito: no es una garantía de recepción inmediata. Android sin servicios Google requiere estrategia separada si se decide soportarlo; declarar compatibilidad en beta. APNs/iOS requiere configuración y pruebas con credenciales propias.

OneSignal ofrece gestión adicional ([planes](https://onesignal.com/blog/which-onesignal-plan-is-best-for-you/amp/)); no se selecciona porque el MVP necesita reglas de dominio propias y evitar otro procesador de datos. SMS/email quedan para convenios futuros; notificaciones locales sirven para recordatorios opt-in, no reemplazan monitoreo del servidor.
