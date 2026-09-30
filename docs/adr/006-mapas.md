# ADR-006 — MapLibre y cartografía OSM con proveedor configurable

Estado: propuesto, proveedor pendiente. Fecha: 2026-09-29.

Contexto: mapa de estaciones y áreas oficiales, bajo coste y portabilidad. OSM es fuente de datos; MapLibre es motor, no servicio gratuito de tiles.

Decisión: spike MapLibre Flutter, estilo/tiles configurables y atribución visible. Contratar/elegir servicio según uso; lista funcional si el mapa falla. Sin offline de tiles comunitarios.

Alternativas: Google Maps facilita servicios integrados pero implica contrato/SDK específico; flutter_map puede ser suficiente si falla spike nativo. No alojar infraestructura de tiles propia prematuramente.

Consecuencias: mantener estilo, fuentes/sprites y presupuesto de proveedor. Clusters no calculan riesgo ni promedian niveles.

Validación: Android/iOS reales, licencia/privacidad, picos de tráfico y política tiles. [Comparación](../../ARCHITECTURE.md).
