# ADR-005 · Consulta y avisos locales

**Propuesta de push remoto sustituida por D19 (4/10/2026).**

Consulta sin cuenta; favoritos, reglas e historial locales. Android usa JobScheduler para consultar la API propia y preparar avisos con permiso. No hay registro de instalación, token FCM/APNs ni dispatcher activo.

Reduce dependencias y datos personales en el backend. La contrapartida es que Android puede demorar o suspender consultas; no se promete entrega inmediata. Reinstalar pierde estado local.

[Especificación de avisos](../runbooks/F8-ANDROID-NOTICES.md). El outbox del servidor se conserva inactivo como posible ampliación.
