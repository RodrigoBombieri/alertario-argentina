# Despliegue

## Desarrollo

La demo sin base se ejecuta según el [README](README.md). Para persistencia local:

```powershell
docker compose -f infrastructure/compose.yaml up -d
./infrastructure/db/apply-local-migrations.ps1
```

El Compose local usa credenciales de desarrollo y expone PostgreSQL solo en `127.0.0.1:5433`. No es configuración de producción.

## Beta

1. Cotizar API + worker + PostgreSQL/PostGIS + respaldo externo + dominio/tráfico/impuestos. Tope de referencia: US$100/mes; [selección provisional](docs/adr/008-hosting-beta.md).
2. Copiar [`.env.example`](infrastructure/environments/beta/.env.example) a `.env` privado en esa carpeta y completar dominio y configuración.
3. Ejecutar `docker compose up -d --build` desde `infrastructure/environments/beta`, con DNS y puertos de HTTPS preparados.
4. Para datos reales, provisionar DB, aplicar [migraciones](docs/runbooks/F4-MIGRATIONS.md) y completar [activación de fuentes](API-INTEGRATIONS.md).
5. Conectar monitor, backup externo y responsable siguiendo [operación](docs/runbooks/F11-OPERATIONS.md).

`ColdStart__Enabled` y `PublishedData__Enabled` son modos excluyentes. Los workers oficiales permanecen apagados hasta su activación explícita.

## Verificar antes de abrir acceso

Comprobar HTTPS, `/health/live`, `/health/ready`, `/v1/status`, búsqueda, ficha e histórico. Con datos persistidos, comprobar también `/health/storage`, `/health/worker` y `/health/data`. Ensayar restore aislado y actualización/retorno a una imagen compatible.

[Entrega y aceptación beta](docs/runbooks/F12-RELEASE.md) · [Publicación Android](docs/PLAY-STORE.md)
