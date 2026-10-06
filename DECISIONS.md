# Decisiones vigentes

Revisión documental: 5/10/2026. Los ADR explican las razones; el [acta de cierre](docs/implementation/TECHNICAL-CLOSURE.md) fija el alcance aceptado.

| ID | Decisión y estado |
|---|---|
| D01–03 | Implementado: API propia, monolito modular .NET 10, hosts API y worker separados |
| D04 | Implementado: PostgreSQL/PostGIS con Npgsql y SQL; sin Redis ni TimescaleDB |
| D05 | Implementado: Flutter/Riverpod/Dio y sqflite; reemplaza la propuesta Drift/Freezed |
| D06 | Vigente: medición, cálculo, umbral y aviso oficial son conceptos distintos |
| D07 | Consulta sin cuenta; propuesta de instalación/token push sustituida por D19 |
| D08 | Lista espacial implementada; MapLibre opcional, proveedor de tiles sin contratar |
| D09 | SMN: no inventar feed ni cobertura; consulta manual hasta verificar integración |
| D10 | Producto gratuito, sin IA, predicción, monetización ni multiempresa |
| D11–12 | Hosting sin contratar; piloto hidrológico y cobertura por aprobar |
| D13 | Titular confirmó Android primero y tope de referencia US$100/mes (4/10/2026); no autoriza gastos |
| D14–15 | Demo inmediata con 14 días ficticios, separados de datos reales; sin espera previa para abrirla |
| D16 | Recolección INA y publicación separadas: derechos permiten recopilar; revisión hidrológica permite publicar/calcular |
| D17 | DigitalOcean: primera opción provisional a cotizar; [ADR-008](docs/adr/008-hosting-beta.md) |
| D18 | Cierre técnico separado de activación de fuentes y operación pública |
| D19 | Alcance Android simplificado, autorizado por el titular el 4/10/2026 |

## D19: alcance de cierre

- Android 8+; iOS fuera de este corte.
- Avisos locales mediante JobScheduler; sin FCM/APNs ni dispatcher activo. No garantiza entrega puntual.
- SMN manual con cobertura no confirmada; no equivale a avisos CAP integrados.
- Lista por zona sin tiles obligatorios; histórico crudo paginado hasta 30 días.
- Verificación local y emulador; evaluación física/ciudadana y hosting se realizan al activar la beta.
- Health, consola, backups y runbooks; guardia humana y copias externas requieren operación.

F1–F12 cerradas técnicamente; F13 pendiente. Requisitos posteriores de tienda se registran en [ROADMAP](ROADMAP.md), sin presentarlos como cumplidos.

Para cambiar una decisión: registrar fecha, motivo, impacto, evidencia y ADR afectado. No sustituir derechos o revisión hidrológica por un cambio de alcance.
