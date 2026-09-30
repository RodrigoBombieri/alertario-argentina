# Roadmap de ejecución

Los números preservan las fases solicitadas; calidad y observabilidad mínima son transversales. Estado: **F0 documentación entregada; validaciones operativas/legal de F1 pendientes**. No hay fase de implementación realizada. Tamaños relativos S=acotado, M=varios componentes, L=riesgo/integración alta; no son días ni compromiso de calendario.

Cada fase se divide en PRs/historias revisables. “Archivos” son destinos futuros excepto documentos ya existentes. Tareas en [BACKLOG](BACKLOG.md).

| Fase / tamaño | Objetivo y tareas | Archivos afectados | Dependencias | Criterio de aceptación | Tests / evidencia | Riesgos |
|---|---|---|---|---|---|---|
| **0 Investigación / L** | Investigar organismos, competidores y opciones; registrar certeza; redactar plan/ADR | Documentos raíz, docs/research, docs/adr, docs/design | Ninguna | Plan completo con fuentes, decisiones propuestas y pendientes visibles | Links locales, consistencia, snapshots de consultas | Documentación externa cambiante; estudio puntual no continuidad |
| **1 Prueba de APIs / L** | Validar permisos; catálogo/series/observaciones; feed SMN SAT/ACP; cadencias, zona/datum; elegir piloto | docs/research, scripts/probes, backend/tests/Fixtures, DATA-SOURCES | F0 | Matriz por serie y fuente aprobada; feed machine-readable confirmado o gate explícito que impide MVP completo; 14 días de muestreo acotado | HTTP/content/fecha, nulls, revisiones, GeoJSON, paginación; no carga a organismos | R01/R03/R11/R17 |
| **2 Backend mínimo / S** | Crear solución .NET 10 y hosts, API propia de fixture, DI, OpenAPI, errores, CI | backend/src, backend/tests, contracts/openapi, .github/workflows | F1 contrato mínimo aprobado | API summary sintética reproducible sin acceso oficial desde cliente; build limpio | Unit smoke + API contract | Sobrearquitectura; no implementar features futuras |
| **3 Normalización / L** | Adapters INA/GeoRef/SMN habilitado, selección series, unidades/fechas/calidad, asociación localidad-estación | Infrastructure/Providers, Core/Hydrology, Application/Ports, Fixtures | F1, F2 | DTO propio estable ante nulos y wrappers; ninguna serie no aprobada publicada | Adapter tests y fixtures, lat/lon, conflictos | R04/R05/R06/R09 |
| **4 Base de datos / M** | PostgreSQL/PostGIS, migraciones, índices, ingesta/lease/checkpoint, revisión y latest | Persistence, Worker/Jobs, infrastructure/compose, IntegrationTests | F3 | Reimportar lote no duplica; crash recuperable; catálogo parcial no elimina válido | DB real, rollback operativo, replay/concurrencia | R07, pérdida de checkpoints |
| **5 Tendencias y estado / M** | Implementar TR, ejes de estado y umbrales aprobados; clock inyectado | Core/Trends, Core/Alerts, UnitTests, summary feature | F3, F4 | Casos TR y estados pasan; ninguna inferencia se presenta oficial | Límites, gaps, stale, datum, series diarias | R10/R18 |
| **6 Flutter MVP / L** | Bootstrap, búsqueda, favoritos, ficha, gráfico reciente, fuente/hora, offline, accesibilidad; build iOS temprano | apps/mobile/lib, test, integration_test, android, ios | F2 contrato; F4/F5 para vivo | Flujo principal sin cuenta y sin GPS; consulta en <10 s en evaluación UX | Unit/widget/integration Android + smoke iOS | R20, comprensión, caché |
| **7 Mapas / M** | Spike MapLibre, elegir tiles, estaciones por bbox, filtros/clusters, fallback lista | mobile/features/map, Api/Features/Stations, DEPLOYMENT | F4, F6, condiciones tiles | Estados accesibles y filtros; aviso oficial separado de dato; presupuesto map aprobado | UI/render en dispositivos, bbox, distancia | R09/R13 |
| **8 Notificaciones / L** | Instalación segura, reglas, episodios, outbox, FCM/APNs, historial, TTL y cancelación | Core/Notifications, Application/Subscriptions, Infrastructure/Notifications, Worker, mobile/notifications | F4/F5/F6; SMN aprobado para avisos | Un evento por episodio, expirados no enviados; sin cuenta personal | NT suite, ownership, crash, dispositivo real | R10/R12/R16 |
| **9 Históricos ampliados / M** | Backfill controlado, consulta paginada/agregada y vista avanzada | Worker/Backfill, Api/Measurements, mobile/history, Persistence | F4/F5/F6 | Rango disponible real; preserva picos/huecos y no dispara pushes históricos | Carga DB sintética, revisión tardía, agregación | R15, límites externos |
| **10 Testing sistema / M** | Consolidar suites, fallas encadenadas, seguridad y rendimiento; completar accesibilidad | backend/tests, mobile/test, integration_test, scripts | F6–F9 según alcance release | Todos MUST pasan; ningún defecto crítico semántico; fixture-only CI | E2E, soak propio, proveedor caído, permisos | Cobertura aparente sin casos reales |
| **11 Observabilidad / M** | Completar dashboards, SLO, alertas técnicas y runbooks; ensayar restore | Infrastructure/Observability, docs/runbooks, infrastructure | Mínima desde F2/F4; completar F10 | Distingue caída, dato sin novedades y schema roto; restore cumple RTO | Falla inyectada, logs sin tokens, backup restaurado | R02/R19, fatiga operativa |
| **12 Beta / L** | Comparar/cotizar hosting, elegir por ADR, desplegar staging/beta, prueba ciudadana, revisión legal/fuentes | infrastructure/environments, ADR hosting, docs/research/beta, PRODUCT | F10/F11 y permisos | Piloto autorizado; 14 días de operación observada; tests de comprensión y presupuesto; gates críticos cerrados | Dispositivos Android/iOS por alcance, feedback y errores | Cobertura, tiendas, uso durante emergencias |
| **13 Producción / M** | Promoción controlada, publicación, soporte y mantenimiento, límites de gasto | workflows, runbooks, release notes, metadatos tiendas | F12 aprobada | Rollback/restore probado, responsable operativo, privacidad/licencias, monitoreo y funciones MUST | Smoke prod acotado, revisión de versiones, borrado | Recaídas upstream; costos |

## Orden exacto recomendado

1. Resolver ficha de fuentes y permisos; confirmar feed SMN, seleccionar series/datum y comprobar frecuencia. Si un bloqueo impide la función oficial, documentarlo y trabajar solo partes independientes.
2. Congelar contrato propio mínimo con ejemplos sintéticos y casos de error.
3. Crear hosts backend/CI/health/logs y pruebas de contrato.
4. Construir normalización y fixtures; probar matemáticas sin DB si facilita feedback.
5. Implementar DB/PostGIS, migraciones e ingesta idempotente.
6. Integrar tendencias/estado y summary real con calidad.
7. Implementar Flutter búsqueda/ficha/favoritos/offline y gráfico reciente.
8. Validar tiles e implementar mapa si entra en release.
9. Implementar credencial de instalación/reglas/outbox/push y avisos zonales completos.
10. Ampliar históricos y vista avanzada, conservando ingesta de actualidad prioritaria.
11. Completar pruebas sistémicas y operación/restore; calidad y métricas ya existían desde pasos anteriores.
12. Elegir hosting con evidencia, lanzar beta y cerrar gates de permisos/comprensión/cobertura.
13. Publicar producción controlada y mantener contratos/parches/revisiones.

## Estimación y camino crítico

Camino crítico: permisos/feed → series/datum → normalización → persistencia → motores → interfaz/avisos → validación integral → beta. Feed SMN, permisos y revisión hidrológica son dependencias externas sin duración estimable aquí. Complejidad mayor: fuentes, geografía de avisos y notificaciones durables; menor: CRUD de favoritos locales.

No se calcula una fecha de lanzamiento sin conocer equipo, experiencia Flutter/.NET, acceso macOS, presupuesto y respuesta de organismos. Una estimación por sprint debe realizarse tras F1 usando velocidad del equipo, no convertir tallas en horas por fórmula. Proteger un piloto reducido y no expandir cobertura mientras falle calidad.
