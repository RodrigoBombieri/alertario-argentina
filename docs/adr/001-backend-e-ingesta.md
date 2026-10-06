# ADR-001 · Backend e ingesta

**Implementado.** Propuesta original: 29/9/2026; estado revisado: 5/10/2026.

API y worker .NET 10 separados, dentro de un monolito modular. El worker normaliza y persiste; la API lee datos propios. Core/Application definen reglas y puertos; Infrastructure integra proveedores y SQL.

Esto desacopla la consulta móvil de la disponibilidad externa. Implica operar un worker y aceptar latencia de recolección. No se necesitan microservicios, broker ni MediatR para este alcance.

[Arquitectura](../../ARCHITECTURE.md) · [Pruebas](../../TESTING.md)
