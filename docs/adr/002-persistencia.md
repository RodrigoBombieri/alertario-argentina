# ADR-002 — PostgreSQL y PostGIS sin extensiones temporales iniciales

Estado: propuesto. Fecha: 2026-09-29.

Contexto: consultas espaciales reales, observaciones históricas, transacciones y claves de deduplicación.

Decisión: PostgreSQL + PostGIS, índices serie/tiempo y geometría, latest reconstruible; outbox/leases en DB. Cache limitada en proceso. Sin TimescaleDB ni Redis al inicio.

Alternativas: SQLite servidor insuficiente para operación concurrente/espacial planteada; TimescaleDB podría facilitar retención/compresión a escala pero agrega restricciones de hosting/licencia; Redis no reemplaza durabilidad.

Consecuencias: menos componentes; requiere medir crecimiento, vacuum y planes. Particionado nativo o TimescaleDB se reconsideran si pruebas de volumen justifican coste.

Validación: carga representativa, migraciones y restore con PostGIS real; hosting compatible. [Sizing](../../ARCHITECTURE.md).
