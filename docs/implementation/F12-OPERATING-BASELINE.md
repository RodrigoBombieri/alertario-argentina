# Propuesta operativa para la beta F12

**Preparada el 4/10/2026. El titular confirmó Android primero y US$100/mes como tope de referencia para cotizar el 4/10/2026; no es una contratación ni un despliegue.** El [ADR-008](../adr/008-hosting-beta.md) eligió DigitalOcean como primera opción técnica provisional tras comparar la propuesta Render. Los gates de [F12](F12.md) siguen pendientes.

## Propuesta de alcance y presupuesto

- **Tope de referencia confirmado para cotizar:** US$100 por mes para infraestructura del piloto, con alerta de gasto al 50% y al 80% del tope. No es un precio ni autorización de pago. Separar impuestos, dominio, proveedor de tiles, dispositivos, cuentas de tiendas y horas de operación; si alguno se necesita, presupuestarlo antes de comprometer el tope.
- **Primera opción técnica para cotizar:** DigitalOcean con Droplet 4 GiB/2 vCPU, PostgreSQL Standard 2 GiB con PostGIS y Spaces: **US$59,45/mes de base publicado**, más almacenamiento de DB si se factura aparte, impuestos y extras. Ver cálculo, fuentes y alternativas en [ADR-008](../adr/008-hosting-beta.md). No hay autorización de pago.
- **Alternativa:** Render Hobby con API web y worker siempre activo `0.5c-512mb` cada uno, PostgreSQL `0.5c-1g` y almacenamiento inicial de 5 GB. Según [precios publicados](https://render.com/pricing), los precios base son US$7 + US$7 + US$19 y US$0,30/GB de almacenamiento adicional a 1 GB incluido: alrededor de **US$34,20/mes de base** para 5 GB, antes de egreso, extras e impuestos. Confirmar en el checkout y medir si 512 MB alcanza para cada proceso; escalar cambia el costo. El plan gratuito no es base de beta persistente.
- Render documenta [workers continuos](https://render.com/docs/background-workers), [PostGIS](https://render.com/docs/postgresql-extensions) y [recuperación puntual de PostgreSQL pago](https://render.com/docs/postgresql-backups). En Hobby la ventana PITR publicada es de 3 días; el backup lógico de largo plazo y el ensayo de restore requieren diseño y costo adicionales. El [restore local](../../infrastructure/db/verify-local-restore.ps1) tampoco demuestra recuperación en DigitalOcean.
- Antes de elegir región y contratar, revisar latencia desde Argentina, términos de tratamiento/transferencia de identificadores de instalación y requisitos legales. Si no son aceptables, volver a comparar Render/Railway/Azure/VPS en [DEPLOYMENT](../../DEPLOYMENT.md). D17 es selección técnica provisional, no contratación.

La primera cotización debe incluir API + worker + DB/PostGIS + almacenamiento + backup externo + egress + logs + tiles + impuestos, con una semana de carga sintética y restore aislado. Si el costo mensual estimado supera US$100 o no cumple permisos/privacidad, decidir una alternativa antes de desplegar.

## Personas y operación

| Rol a cubrir | Tarea mínima | Evidencia antes de beta |
|---|---|---|
| Titular del proyecto / producto | Define alcance, autoriza presupuesto y representa al proyecto en consultas | Nombre y correo registrado; decisión de gasto/alcance fechada |
| Responsable operativo primario | Vigila ingesta, avisos, DB, backups y costos; ejecuta runbooks y pausa fuentes/push | Nombre, canal, horario de cobertura, acceso a consola y simulacro de incidente |
| Suplente operativo | Cubre ausencia del primario y puede recuperar servicio | Nombre, acceso y simulacro de restore |
| Revisión legal y de datos | Valida permisos por fuente, GeoRef, privacidad y textos de beta | Conformidad fechada y alcance por dataset |
| Revisión hidrológica/geoespacial | Aprueba matrices de serie, umbral y relación localidad–estación | Informe de 14 días, lista piloto y aprobación por elemento |

Hasta que haya primario y suplente identificados, mantener las notificaciones y la ingesta oficial desactivadas. El titular puede acumular roles si tiene competencia y cobertura efectiva; no asignar la guardia a una persona sin su aceptación.

## Dispositivos y prueba de aceptación

**Alcance confirmado para la primera beta: Android.** Se necesita un Android físico de gama media para pruebas repetidas; registrar modelo, versión de sistema, propietario/tenencia y posibilidad de probar red intermitente, notificaciones, accesibilidad y ahorro de batería. El emulador Android cubre desarrollo, pero no sustituye las pruebas de push y comportamiento de fondo. iOS queda para una fase posterior y requerirá iPhone físico, macOS y credenciales de firma; no se declara aceptado.

Conseguir 3–5 personas de prueba, con consentimiento y canal de reporte. La beta Android puede comenzar con la muestra sintética D15; registrar disponibilidad, costos, comprensión de la etiqueta y feedback sin imponer una espera fija de 14 días para lanzarla o aceptarla. Cuando una fuente se active con condiciones verificadas, registrar frescura y fallas; contar aparte 14 días de datos reales por serie antes de aprobar cálculos. Las lecturas sintéticas no cuentan como observación real ni validación hidrológica.

## Decisiones que debe registrar el titular

1. ¿Quién será titular/representante, responsable operativo y suplente? Indicar nombres y disponibilidad.
2. ¿Qué Android físico existe ya o se puede prestar? No hace falta comprar hasta evaluar inventario.
3. Tras cotización, latencia, privacidad y restore, registrar proveedor/región/plan definitivo en [DECISIONS](../../DECISIONS.md) y actualizar [F12](F12.md). El tope confirmado no autoriza gastos por sí mismo.
