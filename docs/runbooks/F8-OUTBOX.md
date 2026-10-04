# Outbox de notificaciones

El outbox todavía no tiene dispatcher ni proveedor push configurado. Este procedimiento permite inspeccionar el estado en desarrollo/ensayo; no habilita envíos manuales.

1. Contar eventos por `state` y el más antiguo `created_at`, sin exportar credenciales ni IDs de instalación a métricas públicas. Revisar `expires_at`, `next_attempt_at`, `attempts`, `lease_owner` y `lease_until` de eventos atrasados.
2. Si hay leases vigentes, verificar el proceso dueño antes de intervenir. Un lease vencido se puede volver a reclamar con `claim_notification_outbox` cuando exista un dispatcher; nunca asumir que un envío aceptado por FCM/APNs llegó al dispositivo.
3. Ejecutar `SELECT expire_notification_outbox()` para marcar como expirados los pendientes o reclamados cuyo TTL terminó. El resultado es una cantidad de filas; no reabrir eventos vencidos ni enviarlos tarde.
4. Ante revocación de instalación o regla deshabilitada, la función de claim deja de entregar esos eventos. Antes de cualquier envío futuro, el dispatcher deberá revalidar vigencia de episodio, regla, instalación y permisos. La cancelación de un episodio marca sus eventos pendientes/reclamados como `cancelled` en la misma transacción.
5. Para recuperarse de un crash, esperar el vencimiento del lease y reclamar de nuevo. El estado `accepted` significa aceptación del proveedor, no lectura ciudadana. Registrar intentos y respuesta del proveedor sin token ni payload sensible.

Faltan dispatcher, revalidación inmediata, credenciales/almacenamiento cifrado de tokens, alertas operativas y ensayo de crash. No usar este runbook para notificaciones reales hasta cerrar F8/F10/F11.
