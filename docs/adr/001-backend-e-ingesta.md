# ADR-001 — Backend modular e ingesta periódica

Estado: propuesto. Fecha: 2026-09-29.

Contexto: fuentes con estructura y disponibilidad independientes; mobile no debe depender de ellas. Notificaciones requieren procesamiento aun sin usuarios conectados.

Decisión: ASP.NET Core 10 LTS, Minimal APIs por feature, Application/Core/Infrastructure y hosts API/Worker. Worker persiste primero y API lee DB/cache. Llamadas directas por DI; no MediatR, broker ni CQRS físico.

Alternativas: proxy en vivo multiplica dependencia/carga y no resuelve monitoreo; microservicios agregan coordinación; Controllers son válidos si el equipo los prefiere, sin cambiar contratos.

Consecuencias: hay latencia de polling y operación de workers, pero consultas reproducibles y fallback. Un adapter aísla schema, no cambios de significado científico.

Validación: F1 permisos/cadencias; F4 crash/replay/checkpoint; F11 salud. Reconsiderar separación de servicios por necesidades operativas demostradas.
