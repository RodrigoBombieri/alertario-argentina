# Outbox del servidor — inactivo

D19 usa [avisos locales Android](F8-ANDROID-NOTICES.md). Este outbox no participa en la entrega actual y no tiene dispatcher ni proveedor push configurado.

Para diagnóstico de desarrollo: revisar estados, TTL, intentos y leases. `expire_notification_outbox()` expira eventos vencidos; no reabrirlos ni enviarlos manualmente. La cancelación de episodios cancela pendientes en la misma transacción.

Una futura activación requiere dispatcher, revalidación inmediatamente antes del envío, custodia de tokens y pruebas de crash. `accepted` significaría aceptación del proveedor, nunca lectura del usuario. No hay que completar esa ampliación para operar el alcance Android actual.
