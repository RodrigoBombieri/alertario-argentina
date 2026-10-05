**Implementación vigente D19:** [entrega Android/Compose](docs/runbooks/F12-RELEASE.md) y [operación simple](docs/runbooks/F11-OPERATIONS.md). CI backend incluye PostgreSQL; CI mobile verifica Flutter y APK. Configuración efectiva en infrastructure/environments/beta/.env.example; migraciones SQL 0001–0012. El resto conserva propuestas de despliegue futuro, incluida publicación/registry de F13, no pendientes del corte técnico.

# DevOps y despliegue

**No se contrata hosting en esta etapa.** Comparación documental al 2026-09-29 y [ADR-008 con DigitalOcean como primera opción técnica para cotizar](docs/adr/008-hosting-beta.md), bajo el tope de referencia confirmado el 4/10/2026. Cotizar región, recursos, respaldo, impuestos y tráfico al iniciar beta. Ninguna cifra de presupuesto siguiente es oferta contractual.

## Perfil comparable

Piloto: una API .NET, un worker siempre activo, PostgreSQL/PostGIS, ~5–20 GB iniciales de disco con crecimiento medido, backups separados, logs 30 días y HTTPS. Staging más pequeño y aislado; no duplicar datos personales de producción. Medir CPU/RAM antes de fijar plan. No depender de créditos temporales para la ingesta.

| Opción | Documentación verificada | Encaje y costes que cotizar | Riesgo / criterio |
|---|---|---|---|
| Azure Container Apps + PostgreSQL | [Precio oficial Container Apps](https://azure.microsoft.com/en-us/pricing/details/container-apps/) | Integración .NET/identidades; sumar DB, almacenamiento, logs, red y worker activo. Un API que escala a cero no mantiene un worker continuo | Mayor superficie operativa y presupuesto; verificar región y extensión PostGIS de la oferta elegida |
| Railway | [Precios](https://railway.com/pricing) | Hobby mínimo US$5, Pro mínimo US$20 con consumo incluido según página; no son precio total de API+DB+worker. Recursos y egress se suman | Conveniente para piloto; probar backup, restore y responsabilidad de operar PostgreSQL de la plantilla |
| Render | [Planes/servicios](https://render.com/pricing), [extensiones PostgreSQL](https://render.com/docs/postgresql-extensions) | Web service + background worker + DB; PostGIS documentado. [Cálculo base fechado](docs/implementation/F12-OPERATING-BASELINE.md) para un piloto pequeño; cotización final pendiente | Validar memoria, disco, región, privacidad y retención/PITR del plan; no asumir plan gratuito apto |
| DigitalOcean | [Droplets](https://www.digitalocean.com/pricing/droplets), [PostgreSQL](https://www.digitalocean.com/pricing/managed-databases), [PostGIS](https://docs.digitalocean.com/products/databases/postgresql/details/supported-extensions/), [Spaces](https://www.digitalocean.com/pricing/spaces-object-storage) | [ADR-008](docs/adr/008-hosting-beta.md): VM 4 GiB + DB 2 GiB + copia externa, US$59,45/mes base publicado, antes de extras | Primera opción provisional; falta checkout, región, restore, latencia y responsable |
| Fly.io | [Precios de recursos](https://docs.fly.io/about/pricing/) | Machines, volúmenes, red y DB por separado; comparar Postgres gestionado con autogestionado | Más decisiones de operación/región/volúmenes; HA no surge de tener dos contenedores |
| VPS (ej. Hetzner) | [Servidor cloud](https://docs.hetzner.com/cloud/servers/overview/), [cambio de tarifas 2026](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) | Control Docker/PostGIS y gasto base de VM; sumar IP, disco, backups externos y horas de mantenimiento | Un nodo es punto único de fallo; parches, DB, TLS y recuperación quedan a cargo del equipo |

Azure/Fly/Render ofrecen tablas/calculadores que dependen de configuraciones; no copiar precios antiguos de artículos. Hetzner indica ubicaciones fuera de Argentina en su documentación: medir latencia y evaluar transferencia internacional de datos. No elegir región solo por precio.

**Presupuesto orientativo interno**, no comparación de tarifas: reservar US$30–100/mes para un piloto pequeño con recursos administrados, o US$15–60/mes de infraestructura VPS y backups **más trabajo operativo**. Puede excederse; un servicio con alta disponibilidad/PITR o mapas intensivos cambia sustancialmente el coste. Revisar con cotizaciones y prueba de 7 días antes de aprobar. No se encontró base suficiente para asignar total exacto a cada proveedor.

Fórmula de evaluación: cómputo API + worker + DB + almacenamiento + snapshots/backups + egress + logs/observabilidad + tiles + CI/macOS + dominio/tiendas + impuestos + horas operativas. Sensibilizar a 1.000/10.000 usuarios y 30/200 estaciones. FCM figura sin coste en [Firebase](https://firebase.google.com/pricing), pero no elimina componentes restantes.

Ponderación propuesta para decisión fase 12: operación/recuperación 30%, coste total 25%, PostgreSQL/PostGIS y portabilidad 20%, latencia/región 15%, privacidad/contratos 10%. Descartar primero ofertas que no cumplan permisos, backup restaurable o worker continuo; no compensar un requisito obligatorio con puntaje barato. Comparar dos finalistas con misma carga y un restore.

## Contenedores y ambientes

Los [Dockerfiles de API y worker](backend/src/AlertaRio.Api/Dockerfile) son multi-stage y ejecutan el proceso como usuario no root. El [Compose de beta](infrastructure/environments/beta/compose.yaml) empaqueta API, worker inactivo y proxy Caddy para mostrar la muestra D15. Sus etiquetas de imagen son de versión mayor; antes de un despliegue real hay que fijar los digests verificados. Este Compose no crea PostgreSQL ni configura una fuente oficial. No hay redistribución de datos reales por iniciarlo.

Para comprobar el empaquetado localmente, copiar `infrastructure/environments/beta/.env.example` a `.env` en ese directorio y ejecutar desde la raíz:

```powershell
docker-compose -f infrastructure/environments/beta/compose.yaml config --quiet
docker-compose -f infrastructure/environments/beta/compose.yaml build api worker
```

El dominio `localhost` de ejemplo se usa solo para una prueba local. Para beta pública faltan dominio y DNS, secretos fuera de Git, base PostGIS gestionada y migrada, verificación de restore, monitoreo, y prueba TLS. El worker permanece sin consultar fuentes mientras `InaIngestion__Enabled=false`; habilitarlo solo con el [runbook de colección INA](docs/runbooks/F4-INA-COLLECTION.md). El presupuesto de US$100/mes es un tope de evaluación, no un control automático de gasto.

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

Existe [CI de backend](.github/workflows/backend.yml), pero aún falta el pipeline integral de publicación. Para completarlo: paths backend/mobile/docs; toolchains fijadas, restore lockfile, formato, lint, tests, contratos y scan de secretos/dependencias. No red hacia organismos en PR. PostgreSQL/PostGIS de integración en service/container; cache de paquetes por lockfile. Permisos mínimos del workflow y acciones fijadas a revisión.

Main construye imágenes etiquetadas por commit y SBOM, analiza vulnerabilidades, publica al registry elegido y despliega staging. Smoke consulta datos sintéticos conocidos y readiness. Promover el **mismo digest** a producción tras gate de release. Android usa keystore protegido; iOS requiere macOS y signing fuera de repo; distribución de pruebas antes de tiendas. No acoplar publicación mobile a cada cambio del adapter.

## Migraciones y rollback

Migraciones EF revisadas como SQL y probadas desde base vacía y versión anterior. Job único con lock y credencial DDL; no cada réplica ejecutando `Migrate` al iniciar. Patrón expand → backfill → switch → contract en releases separadas para cambios destructivos. API y worker compatibles con versión anterior durante despliegue.

Rollback de imagen no revierte mágicamente esquema/datos. Preferir cambio correctivo hacia delante y columnas compatibles; backup previo a cambio riesgoso y restore ensayado. Actualizar schema version y detener host incompatible en readiness. No perder correcciones ni eventos de usuario por un down migration improvisado.

## Backups y operación mínima

Backup diario cifrado + copia externa/otro dominio de fallo. Retención candidata 7 diarios y 4 semanales, documentando efecto en borrado personal. Si producción exige RPO ≤1 h, cotizar PITR/WAL o equivalente gestionado y probarlo; no afirmar cumplimiento con un dump diario. Restore mensual en ambiente aislado con conteos/checks y métricas de tiempo. Controlar disco, retención WAL y expiración de certificados.

Runbooks de fuente caída, token push inválido, replay no-push, restauración y rotación en fase 11, de acuerdo con [OPERATIONS](docs/design/OPERATIONS.md). Beta requiere responsable de incidentes y presupuesto aprobado. No Kubernetes, multirregión ni autoescalado complejo hasta evidencia de necesidad.
