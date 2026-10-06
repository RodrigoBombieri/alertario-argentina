# Seguridad y privacidad

## Comportamiento actual

- Consulta sin cuenta, GPS, identificador de instalación ni token push.
- Favoritos y caché en SQLite; reglas e historial de seguimiento en el dispositivo.
- Android solicita permiso para avisos. La entrega es local y no incluye FCM/APNs.
- La versión release exige API HTTPS y clave privada de firma. El manifiesto desactiva backup automático de la app.
- Un 403/404 invalida copias afectadas; sin red, una copia puede persistir hasta volver a consultar.
- Fuentes y asociaciones se revisan en cada lectura publicada; revocar derechos deja de exponer esos datos.

## Operación

Guardar cadenas de conexión, `.env`, `key.properties` y keystores fuera de Git. Restringir acceso a PostgreSQL y a backups. Conservar copias cifradas fuera del servidor y ensayar restore.

Revisar logs y retención del hosting: la ausencia de cuentas no significa ausencia de datos técnicos, como IP. Si se activa cartografía externa, revisar también las prácticas del proveedor.

## Antes de publicar

Definir responsable/contacto, política de privacidad pública y acceso desde la app. Completar Data safety según el binario, SDK y servicios realmente usados. Esta documentación técnica no sustituye esa política.

Borrar datos de Android o desinstalar elimina el estado local. [Preparación de Google Play](docs/PLAY-STORE.md) · [Operación](docs/runbooks/F11-OPERATIONS.md)
