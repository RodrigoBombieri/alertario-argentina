# Operación simple del piloto

Los health y los scripts del repositorio son la alternativa de D19 a contratar una plataforma de métricas. No entregan avisos a una persona hasta que el operador conecte la salida a su monitor o supervise la consola. No hay mensajes ni webhooks configurados.

## Supervisión

Desde un equipo con PowerShell 7, ejecutar:

```powershell
./infrastructure/ops/check-beta.ps1 -BaseUrl https://api.example.org -PersistedData -BackupDirectory /srv/alertario/backups -Watch
```

En la demo omitir `-PersistedData`. Cada minuto comprueba API; en publicación agrega esquema, pulso del worker y calidad/frescura según la cadencia aprobada. Emite JSON cuando cambia el estado o pasan 15 minutos. Sin `-Watch` sirve para un monitor externo: salida 0 sana, 1 requiere intervención. El código no incluye secretos ni estaciones en la respuesta pública de salud. Una fuente caída conserva la última medición, pero su frescura y cálculos degradan.

`/health/data` señala series aprobadas sin última lectura vigente, checkpoints incompletos y cuarentenas abiertas. Un worker vivo no implica datos vigentes. El outbox de servidor queda inactivo en D19; los avisos Android usan su propio diagnóstico por estación y no dependen de un dispatcher.

## Respaldo y recuperación

Usar PostgreSQL cliente de la misma versión mayor que el servidor. Definir `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGSSLMODE=verify-full` y `PGPASSFILE` privado. Ejecutar `backup-postgres.ps1 -Destination <directorio privado>` diariamente desde el planificador del host. El script no imprime credenciales, genera archivo nuevo, verifica catálogo y registra SHA-256; ese control **no sustituye un restore**. Copiar dump y manifiesto a almacenamiento cifrado separado del servidor. Conservar 7 diarios y 4 semanales; la eliminación requiere comprobar antes la copia externa. No se borra automáticamente ningún respaldo.

El chequeo marca falta de backup después de 26 h. Objetivos iniciales: RPO 24 h, RTO 2 h, pendientes de medir en el hosting elegido. Ensayar restauración en una base nueva aislada y aplicar el procedimiento de [restore local](../../infrastructure/db/verify-local-restore.ps1): comparar migraciones y conteos, probar búsquedas/fichas y recién entonces planear un corte. No restaurar sobre la base en uso. Registrar hora inicio/fin, versión de dump, conteos, resultado y responsable en el registro beta.

## Respuesta a incidentes

| Señal | Acción y criterio de recuperación |
|---|---|
| `health/live` / `ready` | Revisar logs y configuración; reiniciar servicio o volver a imagen anterior compatible con esquema; recuperar 200 y smoke de consulta |
| `health/storage` | Revisar conexión/TLS y migraciones; no deshacer DB destructivamente; restaurar en base aislada si hay pérdida |
| `health/worker` | Revisar proceso, conexión y reloj; pulso cada 30 s, se considera perdido a los 2 min; reiniciar worker y comprobar avance de ingesta |
| `health/data` | Revisar checkpoint, normalizador, proveedor y cuarentena; no aprobar automáticamente registros; restaurar cobertura completa y frescura |
| Umbral incorrecto | Desactivar su aprobación en DB; nueva consulta deja de publicarlo; revisar referencia/datum/época y registrar nueva decisión |
| Corrección histórica | Ingerir como revisión; nunca editar una lectura manualmente para forzar una alerta; verificar versión/history y ausencia de aviso retroactivo |
| SMN parcial | Mantener cobertura no confirmada, consultar sitio oficial; no presentar lista vacía como ausencia de alertas |
| Aviso Android ausente | Revisar permiso, canal, ahorro de batería, última consulta y episodio; usar “Consultar ahora”; no prometer entrega puntual |
| `backupMissing` / `backupOverdue` | Generar respaldo, comprobar catálogo y copia externa; ensayar restore si falla la integridad |

Antes de operación pública: titular designa responsable y reemplazo, monitor externo, almacenamiento externo y contacto de soporte. Es activación operativa externa; no se simula como realizada.

El backup admite también herramientas del contenedor local con los parámetros ContainerName, Database y Username. El monitor comprueba existencia, tamaño y SHA-256 además de antigüedad.
