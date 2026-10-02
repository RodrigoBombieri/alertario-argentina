# Estrategia de testing

Testing empieza en fase 1 y acompaña cada feature. Fase 10 es consolidación y prueba de sistema, no el inicio de calidad. CI ordinaria **sin llamadas a organismos**; fixtures revisadas y reloj inyectado.

## Capas y herramientas propuestas

| Capa | Pruebas | Evidencia esperada |
|---|---|---|
| Core | xUnit; tendencias, estado, comparabilidad, reglas, expiración | Tabla completa TR/NT de [ENGINES](docs/design/ENGINES.md) |
| Application | Casos de uso con puertos fake | Datos parciales, cancelación, permisos y clocks |
| Adapters | HTTP server/fake handler con JSON/XML capturado y sintético | Normalización, contratos y errores tipados |
| Persistencia | PostgreSQL/PostGIS real en contenedor de test | Índices, constraints, transacciones, geografía y migración |
| API | WebApplicationFactory + DB real para flujos relevantes | OpenAPI, HTTP, auth, ownership, paginación, ETag |
| Worker | Reloj/control scheduler, proveedor fake, DB real | Checkpoint, lease, replay, shutdown y recuperación |
| Dispatcher | FCM fake, outbox real; smoke opt-in en staging | Idempotencia, TTL, cancelaciones y token invalidado |
| Flutter unit | Repositorios, controllers/providers, cache y sincronización | Offline y modelos parciales sin SDK de fuente |
| Flutter widget | Loading/error/empty/stale/notice independiente; textos grandes | Fuente/hora visible y semántica accesible |
| Flutter integration | Buscar → elegir → favorito → offline → regla → evento | Android; iOS en runner/dispositivo separado |

No sustituir PostGIS por EF InMemory o SQLite en pruebas espaciales. No pruebas snapshot gigantes de JSON público como único contrato; verificar campos críticos y tolerancia a adiciones. Golden tests de UI limitados a estados de riesgo semántico, evitando fragilidad por píxel cuando no aporta.

## Fixtures de proveedor

Cada fixture tiene manifest: fuente, URL exacta, instante de descarga, hash SHA-256, licencia/permiso, finalidad, adapter version y si es real o sintética. [Evidencia](docs/research/evidence/) no se promueve automáticamente a fixtures redistribuibles hasta revisar permisos. Para catálogo INA, usar subset público o fixture sintética y no publicar registros `public=false` capturados en investigación.

INA: `rows` frente a array, `unit_id=null`, string `null`, valor cero/negativo, unknown unit, series medias frente a instantáneas, fechas sin offset, `next_page` defectuoso, revisión de valor, duplicados, catálogo parcial/vacío, HTTP 200 HTML, 401/403/429/500/timeout.

SMN: CAP Alert/Update/Cancel, references ausentes o fuera de orden, Test/Exercise, múltiples info/languages/areas, `areaDesc` vacío con polígono, `expires` vencido, polígono inválido, borde, lat/lon invertidos, geometría ausente, XXE, payload excesivo, feed parcial, mismo mensaje repetido, color no especificado.

GeoRef: homónimos, localidad simple/entidad, IDs con ceros, paginación completa, categoría desconocida, centroide nulo, Unicode/acentos y cambio de snapshot. Si falla una página no sustituir catálogo anterior.

## Invariantes y propiedades

- Reordenar observaciones o repetir lote no cambia latest ni duplica eventos.
- Δ(a,b) = −Δ(b,a) cuando los extremos son comparables; escala cm/m autorizada conserva valor.
- Un timestamp de ingesta nuevo no cambia edad de medición.
- Sin aviso oficial nunca hay `officialAlert` ni `evacuationOrder`.
- Un usuario/instalación no puede obtener datos privados de otra.
- Replay de histórico corrige gráfico, no envía notificaciones de actualidad.
- Cancel antes de Alert impide reactivación tardía; feed fallido no borra vigente.
- Reinicio a mitad de lote/outbox no pierde medición ni genera eventos lógicos duplicados.

## Flujos de API y resiliencia

Contrato propio: cinco ventanas con motivos de indisponibilidad; calidad independiente por variable; error ProblemDetails sin stack; búsqueda vacía 200; inexistente 404; ownership 404/403 según política uniforme. Probar límite máximo de histórico y bbox, 429 y ETag tras transcurrir tiempo de expiración. Reglas creadas con condición ya verdadera no notifican por sorpresa.

Simulaciones: proveedor caído 2 h; transporte exitoso sin nuevas lecturas; DB caída; worker detenido; respuesta corrupta; recuperación masiva; notificación aceptada y crash antes de commit; backlog expirado. Inyectar fallas solo en entornos propios.

## Rendimiento y volumen

Presupuesto candidato: 10.000 instalaciones, 30 estaciones piloto, 100 solicitudes/s en ráfaga de lectura a API propia; ajustar con uso real. Objetivo interno p95 summary <500 ms desde servidor, sin contar red móvil ni organismo. Gráfico limitado a 2.000 puntos; probar serie de un año con índice y paginación. Validar DB de tamaño representativo, costo de consultas espaciales y reconstrucción de snapshots. No hacer carga a APIs gubernamentales.

## Mobile y accesibilidad

Pruebas con red lenta, modo avión, proceso terminado, cambio de hora del dispositivo, reinstalación, permiso push revocado y GPS negado. Lectura de estado con TalkBack/VoiceOver y fuente grande; no depender del color. Push real opt-in en staging con un dispositivo Android/iOS, verificando apertura, TTL y ausencia de notificación antigua tras reconectar; no basta emulador.

## Puertas de calidad

Por PR: formato/análisis estático, unit/contract, integración de componentes cambiados y scan de secretos. Por release: suite completa, migración desde versión anterior, restauración de backup, smoke staging, accesibilidad y dispositivos. Los motores deben cubrir todos los casos semánticos enumerados; cobertura porcentual de líneas no es criterio único.

Checks vivos de contrato: tarea operativa acotada separada de CI, según permiso fuente, sin fallar PR por caída externa. Registrar HTTP, tipo, tiempo y schema diff; nunca interpretar un 200 como dato fresco. El [corte F2](docs/implementation/F2.md) incorpora pruebas HTTP de la API propia con datos sintéticos; CI no consulta organismos.
