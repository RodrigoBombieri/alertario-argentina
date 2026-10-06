# ADR-008 · Hosting de beta

**DigitalOcean: opción provisional, sin contratación.** Decisión técnica: 4/10/2026.

El titular confirmó Android primero y US$100/mes como tope de referencia. La propuesta combina una VM para API/worker, PostgreSQL con PostGIS y almacenamiento separado para backups. Render queda como alternativa.

Antes de elegir: cotizar costo total, confirmar extensión/región, medir latencia desde Argentina, revisar privacidad y ensayar restore. Los precios del análisis original son antecedentes, no presupuesto vigente.

Referencias para cotizar: [DigitalOcean](https://www.digitalocean.com/pricing), [Render](https://render.com/pricing). No se contrata ni autoriza gasto mediante este ADR.

[Base operativa](../implementation/F12-OPERATING-BASELINE.md) · [Despliegue](../../DEPLOYMENT.md)
