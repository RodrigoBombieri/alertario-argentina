# Migraciones PostgreSQL de ingesta

Este procedimiento define el corte de despliegue. Aún no se ejecutó en un entorno desplegado: faltan proveedor, credenciales operativas, almacenamiento de backups y objetivo de recuperación aprobados. No usar el aplicador local de Compose como procedimiento de producción.

## Antes de aplicar

1. Registrar versión de aplicación, versión actual de `schema_migrations`, responsable y ventana de mantenimiento. Confirmar que el proceso de ingesta está pausado y que ningún Worker mantiene un lease activo.
2. Crear un backup consistente de la base completa con `pg_dump --format=custom` usando un usuario autorizado. Guardar fuera del host de PostgreSQL, con cifrado y retención acordados; registrar checksum, tamaño y hora. No poner la contraseña en argumentos, logs ni tickets.
3. Restaurar ese archivo en **otra** base aislada con `pg_restore --exit-on-error`. Comparar `schema_migrations` y conteos de fuentes, estaciones, series, mediciones, revisiones, cuarentenas y checkpoints. Consultar una ficha sintética del entorno de ensayo si está disponible. Una copia no restaurada y verificada no sirve como rollback.
4. Revisar el SQL pendiente y sus bloqueos esperados. Las migraciones de este repositorio son ordenadas, transaccionales y registran su versión; no ejecutar una versión ya aplicada ni omitir una intermedia.
5. Si el despliegue agrega una migración, actualizar la lista esperada por `/health/storage` en `backend/src/AlertaRio.Api/Features/StorageHealth.cs` y probar que el binario nuevo rechace un esquema incompleto.

## Aplicación y verificación

1. Aplicar cada archivo pendiente en orden con `psql -v ON_ERROR_STOP=1 -f <archivo>`, usando conexión segura y rol de migración. Si falla, detener el despliegue: la transacción de ese archivo debe revertirlo. No editar manualmente `schema_migrations` para continuar.
2. Confirmar una fila por versión nueva en `schema_migrations`, restricciones e índices esperados. Ejecutar los smokes SQL de `infrastructure/db/tests` en una base **de prueba**, nunca en producción: hacen `ROLLBACK`, pero crean fixtures mientras corren.
3. Desplegar aplicación compatible; comprobar `/health/storage`, `/health/ready`, lectura pública y un lote sintético autorizado en ensayo. `/health/storage` debe responder `ready` en el modo persistido; `/health/ready` puede seguir en 503 si los datos públicos aún no están configurados. Reanudar Worker solo después de que el esquema y la aplicación pasen. Registrar cursores/leases, rechazos de normalización y latencia de ingesta durante el corte.

## Recuperación

Si una migración falla dentro de su transacción, corregir la causa y reintentar desde la versión no registrada. Si el esquema ya quedó aplicado y la aplicación no puede operar, pausar escrituras y volver a la versión de aplicación compatible cuando sea posible. Si hace falta revertir datos o esquema, restaurar el backup verificado en **una base nueva**, comprobar conteos y versiones, y cambiar la conexión en una ventana controlada. No ejecutar `DROP COLUMN` o un `down` improvisado sobre la base original: las migraciones pueden haber transformado datos y el rollback lógico no equivale a restaurar estado. Registrar el punto de pérdida de datos y la reconciliación de lotes posteriores al backup antes de reabrir ingesta.

El ensayo de restore, la medición de tiempo y la aprobación de RPO/RTO quedan pendientes hasta que exista entorno de despliegue. Esos son **faltantes por motivos externos**: resolverlos con la selección de hosting/operación, acceso a un entorno de ensayo y una ventana para simular falla y recuperación.
