# ADR-005 — Consulta anónima y push por instalación

Estado: propuesto. Fecha: 2026-09-29.

Contexto: consultar y guardar favoritos no requiere cuenta, pero editar reglas remotas exige autorización.

Decisión: favoritos locales; instalación seudónima con credencial opaca y token FCM protegido. Reglas/evaluación/outbox propios; IPushSender para transporte. JWT y refresh de cuentas quedan para futura necesidad OIDC.

Alternativas: cuenta obligatoria añade fricción y datos personales; confiar en token FCM como autenticación es insuficiente; OneSignal agrega tercero y funciones de campañas no requeridas.

Consecuencias: reinstalación pierde recuperación automática de reglas; push no garantiza entrega. Borrado y retención son necesarios aunque no exista nombre/email.

Validación: ownership, revocación, idempotencia de alta, duplicación de transporte y TTL; privacy review antes de beta.
