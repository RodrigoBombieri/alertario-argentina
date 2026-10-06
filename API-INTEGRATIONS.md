# Integraciones

La app consulta nuestra API. El worker concentra las consultas a organismos, con activación explícita y límites de uso.

| Fuente | Implementación actual | Activación |
|---|---|---|
| INA A5 | Catálogo, selección exacta de serie y observaciones; paginación acotada, normalización y cuarentena | [Recolección INA](docs/runbooks/F4-INA-COLLECTION.md) |
| GeoRef v2 | Catálogo completo versionado; fallo parcial conserva la versión anterior | [Catálogo GeoRef](docs/runbooks/F4-GEOREF-CATALOG.md) |
| SMN | Enlace al sitio oficial; sin adapter/feed CAP operativo | Verificar feed y términos antes de una futura integración |

## Orden de habilitación

1. Documentar derechos y atribución del dataset exacto.
2. Preparar base y [migraciones](docs/runbooks/F4-MIGRATIONS.md).
3. Activar importación/recopilación mediante el runbook de cada fuente.
4. Revisar serie, políticas y asociación localidad–estación.
5. Activar la [publicación](docs/runbooks/F5-PUBLICATION.md) y probar revocación.

Los interruptores de recolección y publicación son independientes. Guardar una serie real no la hace visible automáticamente.

[Condiciones y evidencia](DATA-SOURCES.md) · [Contrato propio](docs/design/OWN-API.md)
