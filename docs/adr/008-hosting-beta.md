# ADR-008 — hosting candidato para beta Android

**Fecha:** 4/10/2026. **Estado:** selección técnica provisional; sin compra, cuenta configurada ni despliegue. **Tope de referencia:** US$100/mes (D13).

## Contexto

La beta requiere API y worker siempre activos, PostgreSQL con PostGIS, TLS, backup restaurable y una forma de limitar el gasto. La muestra D15 puede verse localmente, pero no hay staging ni sondeo oficial activado. Los precios siguientes son publicados por proveedores y deben comprobarse en el checkout antes de contratar.

## Decisión provisional

Usar **DigitalOcean** como primera opción de staging: Droplet Basic de 4 GiB/2 vCPU para API y worker (US$24/mes), PostgreSQL Standard administrado de 2 GiB (precio de tabla US$30,45/mes) y Spaces para copia lógica externa (US$5/mes). **Subtotal base: US$59,45/mes**. Reservar hasta US$6,45 para 30 GiB si el almacenamiento de la base se factura aparte, y el resto del tope para impuestos, tráfico adicional, dominio, logs y tiles. No asumir que el presupuesto final está cerrado sin la cotización completa. Las fuentes son [precios de Droplets](https://www.digitalocean.com/pricing/droplets), [bases administradas](https://www.digitalocean.com/pricing/managed-databases) y [Spaces](https://www.digitalocean.com/pricing/spaces-object-storage).

PostgreSQL Standard admite [PostGIS](https://docs.digitalocean.com/products/databases/postgresql/details/supported-extensions/). El servicio documenta [backups diarios con recuperación puntual](https://docs.digitalocean.com/products/databases/postgresql/details/features/), aunque el restore debe ensayarse con este esquema. Configurar [alertas de gasto](https://docs.digitalocean.com/platform/billing/spend-alerts/) al 50% y 80% de US$100; una alerta informa el consumo, no limita automáticamente la factura.

## Alternativas y límites

- **Render** era el candidato inicial de [F12](../implementation/F12-OPERATING-BASELINE.md): menor precio base, pero la propuesta presupuestaba solo 512 MiB por proceso y requiere medir si API/worker .NET caben con margen. Sigue como alternativa si la cotización de DigitalOcean excede el tope o la región/latencia no resulta aceptable.
- **Hetzner VPS** ofrece menor precio de cómputo según sus [tarifas 2026](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/), pero trasladaría operación, parches, backup y recuperación de PostgreSQL al equipo. Sin responsable operativo confirmado, ese ahorro no es suficiente para elegirlo.

La primera beta es de nodo único para API/worker y base administrada sin standby dedicado; no promete alta disponibilidad. Faltan medición de latencia desde Argentina, requisitos de privacidad/transferencia, cotización final de almacenamiento y tráfico, prueba PostGIS/restore y responsable operativo. No se contratará ni publicará antes de resolver esos puntos y revisar el costo total.
