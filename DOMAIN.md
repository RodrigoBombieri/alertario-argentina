# Dominio

| Concepto | Significado |
|---|---|
| Fuente | Organismo/dataset, condiciones de uso, atribución y revisión |
| Localidad | Identidad GeoRef en un catálogo versionado |
| Estación | Punto de observación; no representa automáticamente la localidad más cercana |
| Asociación | Relación localidad–estación revisada y vigente |
| Serie | Variable, unidad, datum, época y procedimiento de una estación |
| Medición | Valor y hora observada, procedencia, calidad y revisión |
| Política | Cadencia, retraso tolerado y parámetros aprobados para una serie |
| Umbral | Referencia hidrológica versionada, comparable y vigente |
| Aviso local | Seguimiento calculado por la app; distinto de un aviso oficial |
| Cuarentena | Registro no publicable hasta resolución documentada |

## Invariantes

- Altura y caudal son variables independientes. `null` no se convierte en cero.
- La hora de ingesta no rejuvenece una lectura.
- Se comparan observaciones con unidad, datum, época y procedimiento compatibles.
- Una nueva revisión conserva trazabilidad; no se corrige manualmente para producir una alerta.
- Superar un umbral no crea una orden de evacuación ni un aviso oficial.
- La muestra sintética permanece separada de series reales y no dispara seguimiento.
- Sin cobertura oficial verificada, una lista vacía no demuestra ausencia de alertas.

[Motores](docs/design/ENGINES.md) · [Esquema SQL](infrastructure/db/migrations)
