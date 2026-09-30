# DevOps y despliegue

**No se elige ni contrata hosting en esta etapa.** Comparación documental al 2026-09-29; cotizar con región, recursos, respaldo, impuestos y tráfico al iniciar beta. Ninguna cifra de presupuesto siguiente es oferta contractual.

## Perfil comparable

Piloto: una API .NET, un worker siempre activo, PostgreSQL/PostGIS, ~5–20 GB iniciales de disco con crecimiento medido, backups separados, logs 30 días y HTTPS. Staging más pequeño y aislado; no duplicar datos personales de producción. Medir CPU/RAM antes de fijar plan. No depender de créditos temporales para la ingesta.

| Opción | Documentación verificada | Encaje y costes que cotizar | Riesgo / criterio |
|---|---|---|---|
| Azure Container Apps + PostgreSQL | [Precio oficial Container Apps](https://azure.microsoft.com/en-us/pricing/details/container-apps/) | Integración .NET/identidades; sumar DB, almacenamiento, logs, red y worker activo. Un API que escala a cero no mantiene un worker continuo | Mayor superficie operativa y presupuesto; verificar región y extensión PostGIS de la oferta elegida |
| Railway | [Precios](https://railway.com/pricing) | Hobby mínimo US$5, Pro mínimo US$20 con consumo incluido según página; no son precio total de API+DB+worker. Recursos y egress se suman | Conveniente para piloto; probar backup, restore y responsabilidad de operar PostgreSQL de la plantilla |
| Render | [Planes/servicios](https://render.com/pricing), [extensiones PostgreSQL](https://render.com/docs/postgresql-extensions) | Web service + background worker + DB; PostGIS documentado. La página dinámica no expuso importes fiables en esta consulta: cotización pendiente | Validar ausencia de suspensión, disco y retención/PITR del plan; no asumir plan gratuito apto |
| Fly.io | [Precios de recursos](https://docs.fly.io/about/pricing/) | Machines, volúmenes, red y DB por separado; comparar Postgres gestionado con autogestionado | Más decisiones de operación/región/volúmenes; HA no surge de tener dos contenedores |
| VPS (ej. Hetzner) | [Servidor cloud](https://docs.hetzner.com/cloud/servers/overview/), [cambio de tarifas 2026](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) | Control Docker/PostGIS y gasto base de VM; sumar IP, disco, backups externos y horas de mantenimiento | Un nodo es punto único de fallo; parches, DB, TLS y recuperación quedan a cargo del equipo |

Azure/Fly/Render ofrecen tablas/calculadores que dependen de configuraciones; no copiar precios antiguos de artículos. Hetzner indica ubicaciones fuera de Argentina en su documentación: medir latencia y evaluar transferencia internacional de datos. No elegir región solo por precio.

**Presupuesto orientativo interno**, no comparación de tarifas: reservar US$30–100/mes para un piloto pequeño con recursos administrados, o US$15–60/mes de infraestructura VPS y backups **más trabajo operativo**. Puede excederse; un servicio con alta disponibilidad/PITR o mapas intensivos cambia sustancialmente el coste. Revisar con cotizaciones y prueba de 7 días antes de aprobar. No se encontró base suficiente para asignar total exacto a cada proveedor.

Fórmula de evaluación: cómputo API + worker + DB + almacenamiento + snapshots/backups + egress + logs/observabilidad + tiles + CI/macOS + dominio/tiendas + impuestos + horas operativas. Sensibilizar a 1.000/10.000 usuarios y 30/200 estaciones. FCM figura sin coste en [Firebase](https://firebase.google.com/pricing), pero no elimina componentes restantes.

Ponderación propuesta para decisión fase 12: operación/recuperación 30%, coste total 25%, PostgreSQL/PostGIS y portabilidad 20%, latencia/región 15%, privacidad/contratos 10%. Descartar primero ofertas que no cumplan permisos, backup restaurable o worker continuo; no compensar un requisito obligatorio con puntaje barato. Comparar dos finalistas con misma carga y un restore.

## Contenedores y ambientes

Dockerfiles multi-stage para API/worker, usuario no root y versiones/digests fijados. Compose local: `api`, `worker`, `postgres-postgis` y proveedor mock; colector OpenTelemetry opcional. No Redis por defecto. Volúmenes de DB explícitos; seed sintético identificable. Puertos DB solo locales, nunca públicos en producción.

Development usa fixtures y proveedor real deshabilitado por defecto. Staging tiene credenciales propias, datos públicos autorizados y push solo a dispositivos de prueba. Production tiene base/secretos propios, listas aprobadas y límites. Ningún job comparte checkpoint entre ambientes.

Variables **propuestas propias**, documentar luego en `.env.example` sin valores secretos:

| Variable conceptual | Uso |
|---|---|
| `ConnectionStrings__Main` | DB con usuario de aplicación |
| `Providers__Ina__BaseUrl` | Base oficial validada y allowlist |
| `Providers__Smn__FeedUrl` | Sin valor por defecto hasta verificar feed |
| `Providers__GeoRef__BaseUrl` | Base v2 validada |
| `Ingestion__Enabled`, `Notifications__Enabled` | Interruptores por entorno |
| `Ingestion__MaxConcurrency`, `Ingestion__PollInterval` | Presupuesto aprobado de consulta |
| `Push__ProjectId`, `Push__CredentialReference` | Identidad de servicio fuera de repo |
| `Maps__StyleUrl` | Estilo/tiles bajo contrato, restringido |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Observabilidad de la infraestructura |

Validar configuración al arrancar; falla explícita si producción intenta usar mocks o si se habilita SMN sin contrato aprobado. Configuración de cadencia/umbral versionada en DB o archivo aprobado, no secretos.

## CI/CD propuesto con GitHub Actions

No se crean workflows ejecutables aún. En fase 2: paths backend/mobile/docs; checkout, toolchains fijadas, restore lockfile, formato, lint, tests, contratos y scan de secretos/dependencias. No red hacia organismos en PR. PostgreSQL/PostGIS de integración en service/container; cache de paquetes por lockfile. Permisos mínimos del workflow y acciones fijadas a revisión.

Main construye imágenes etiquetadas por commit y SBOM, analiza vulnerabilidades, publica al registry elegido y despliega staging. Smoke consulta datos sintéticos conocidos y readiness. Promover el **mismo digest** a producción tras gate de release. Android usa keystore protegido; iOS requiere macOS y signing fuera de repo; distribución de pruebas antes de tiendas. No acoplar publicación mobile a cada cambio del adapter.

## Migraciones y rollback

Migraciones EF revisadas como SQL y probadas desde base vacía y versión anterior. Job único con lock y credencial DDL; no cada réplica ejecutando `Migrate` al iniciar. Patrón expand → backfill → switch → contract en releases separadas para cambios destructivos. API y worker compatibles con versión anterior durante despliegue.

Rollback de imagen no revierte mágicamente esquema/datos. Preferir cambio correctivo hacia delante y columnas compatibles; backup previo a cambio riesgoso y restore ensayado. Actualizar schema version y detener host incompatible en readiness. No perder correcciones ni eventos de usuario por un down migration improvisado.

## Backups y operación mínima

Backup diario cifrado + copia externa/otro dominio de fallo. Retención candidata 7 diarios y 4 semanales, documentando efecto en borrado personal. Si producción exige RPO ≤1 h, cotizar PITR/WAL o equivalente gestionado y probarlo; no afirmar cumplimiento con un dump diario. Restore mensual en ambiente aislado con conteos/checks y métricas de tiempo. Controlar disco, retención WAL y expiración de certificados.

Runbooks de fuente caída, token push inválido, replay no-push, restauración y rotación en fase 11, de acuerdo con [OPERATIONS](docs/design/OPERATIONS.md). Beta requiere responsable de incidentes y presupuesto aprobado. No Kubernetes, multirregión ni autoescalado complejo hasta evidencia de necesidad.
