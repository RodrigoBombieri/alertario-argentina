# ADR-007 — Fuentes verificadas y funciones condicionadas

Estado: propuesto; no inventar interfaces/alertas es requisito confirmado. Fecha: 2026-09-29.

Contexto: INA y GeoRef respondieron; XML CAP SMN fue leído, pero feed estable/completo no se confirmó. Permisos por red y umbrales requieren validación.

Corrección 4/10/2026: el gate SMN es de términos aplicables y operación del feed, no de autorización individual por defecto. Si el CAP está cubierto por una licencia abierta, se aplica esa licencia. La evidencia de otro dataset del SMN no se traslada automáticamente al feed CAP.

Decisión: registry de fuentes approved/pending/denied, allowlist de series, gate SMN y evidencia versionada. No producción con datos no aprobados ni scraping operativo de HTML como sustituto improvisado. No usar endpoint interno descubierto por tokens de web.

Alternativas: asumir permiso por HTTP 200 o continuidad por una muestra se rechaza. Una beta limitada puede enlazar sitio oficial e informar indisponibilidad; no equivale al MVP completo solicitado.

Consecuencias: posible bloqueo externo del lanzamiento completo; trabajo independiente de UI/motores puede avanzar con fixtures sintéticas. Configuración no confunde fuente vacía con caída.

Validación: contratos/permiso por escrito o licencia explícita, feed/cancelaciones y 14 días de observación. Revisión ante cambios upstream o de titularidad.
