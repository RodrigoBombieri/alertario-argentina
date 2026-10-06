# ADR-002 · Persistencia

**Implementado.** Propuesta original: 29/9/2026; estado revisado: 5/10/2026.

PostgreSQL 17/PostGIS 3.5 con Npgsql y SQL directo: transacciones, revisiones, índices espaciales, leases y checkpoints. Doce migraciones versionadas; latest reconstruible.

No se incorporaron EF Core, Redis ni TimescaleDB. Agregar particionado o extensiones solo si volumen y operación lo justifican.

[Procedimiento de migración](../runbooks/F4-MIGRATIONS.md) · [Recuperación](../runbooks/F11-OPERATIONS.md)
