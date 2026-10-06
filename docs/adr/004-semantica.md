# ADR-004 · Semántica hidrológica

**Vigente e implementado en los cálculos.** Original: 29/9/2026; revisión: 5/10/2026.

Separar medición, calidad, frescura, tendencia, umbral y aviso oficial. Un umbral necesita referencia/vigencia compatibles; superarlo no implica evacuación.

Las tendencias son determinísticas, sin interpolación ni pronóstico. Datos insuficientes se muestran como no disponibles. Se rechaza un semáforo único que atribuya autoridad a un cálculo propio.

[Motores](../design/ENGINES.md) · [Dominio](../../DOMAIN.md)
