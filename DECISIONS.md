# Decisiones y validaciones

Fecha: 2026-09-29. **Propuestas de diseño** salvo restricciones solicitadas por el usuario. No equivalen a aprobación de contratos externos, presupuesto o despliegue.

**Seguimiento 1/10/2026, corregido 4/10/2026:** [F1 en curso](docs/research/f1/README.md). GeoRef publicó licencia CC BY 4.0 y cuotas en condiciones específicas; faltan revisión legal de publicación, condiciones INA/PNA, contrato técnico y términos del CAP del SMN, 14 días de datos y aprobación hidrológica del piloto. Una licencia abierta aplicable al CAP no requeriría autorización individual del SMN. D12 permanece propuesta.

**Evaluación 2/10/2026:** [la investigación inicial de F1 terminó con resultado NO-GO](docs/research/f1/CLOSURE.md); F1 no está aceptada. No se aprobaron series, umbrales, feed SMN ni cobertura del piloto. D12 y los pendientes de la tabla conservan su estado.

**Spike técnico 3/10/2026:** [F7](docs/implementation/F7.md) integra `maplibre_gl` 0.22.0 con URL de estilo opcional y lista de respaldo. La versión 0.27.1 exigió actualizar Kotlin/Gradle/SDK Android; se mantuvo el toolchain actual para este prototipo. D08 sigue condicionada a pruebas reales, condiciones del proveedor de tiles, atribución y presupuesto.

| ID | Decisión | Estado | Motivo / ADR |
|---|---|---|---|
| D01 | Mobile consume API propia para hidrología, avisos y geografía | Requisito confirmado | [ADR-001](docs/adr/001-backend-e-ingesta.md) |
| D02 | Monolito modular + dos hosts API/worker + ingesta B | Propuesta recomendada | Desacople sin microservicios |
| D03 | .NET 10 LTS + Minimal APIs por feature; sin MediatR/CQRS distribuido | Propuesta recomendada | Simplicidad y soporte; ADR-001 |
| D04 | PostgreSQL/PostGIS; sin TimescaleDB ni Redis inicial | Propuesta recomendada | [ADR-002](docs/adr/002-persistencia.md) |
| D05 | Flutter/Riverpod, repositories y cache SQLite | Propuesta recomendada | [ADR-003](docs/adr/003-mobile.md) |
| D06 | Umbral ≠ aviso ≠ orden; motores determinísticos | Requisito confirmado y diseño propuesto | [ADR-004](docs/adr/004-semantica.md) |
| D07 | Consulta sin cuenta, instalación seudónima para push | Propuesta recomendada | [ADR-005](docs/adr/005-identidad-y-push.md) |
| D08 | MapLibre + datos OSM; tiles por elegir | Propuesta condicionada a spike/términos | [ADR-006](docs/adr/006-mapas.md) |
| D09 | SMN CAP oficial; no scraping operativo ni endpoint interno | Requisito y gate | [ADR-007](docs/adr/007-fuentes-y-gates.md) |
| D10 | MVP gratuito; sin IA, predicción, monetización ni multiempresa | Requisito confirmado | VISION/PRODUCT |
| D11 | Hosting no elegido; comparar antes de contratar | Requisito confirmado | DEPLOYMENT |
| D12 | Piloto limitado Cuenca del Plata, alcance de búsqueda nacional | Propuesta de alcance | Necesita aceptación de producto tras F1 |
| D13 | Primera beta Android; US$100/mes como tope de referencia para cotizar infraestructura | Confirmado por el titular el 4/10/2026 para planificación, sin contratación ni autorización de pago | [Base operativa F12](docs/implementation/F12-OPERATING-BASELINE.md); hosting y dispositivos siguen pendientes |
| D14 | Arranque en frío: la app puede abrirse sin observaciones y reunir datos reales durante los primeros 14 días | Confirmado por el titular el 4/10/2026; sustituye los 14 días como requisito previo de lanzamiento | [F1](docs/research/f1/CLOSURE.md) y [F12](docs/implementation/F12.md). Cada serie se evalúa después; el plazo solo no valida cadencia, datum, umbrales ni derechos de uso |
| D15 | Mostrar 14 días de lecturas horarias simuladas desde el inicio y empezar el historial real por separado cuando la fuente quede habilitada | Confirmado por el titular el 4/10/2026; modifica la presentación vacía de D14 | La muestra conserva marca sintética y estación ficticia. No se mezcla con mediciones reales, no cuenta como 14 días de observación, no habilita alertas. La recolección real depende de derechos de fuente, selección exacta de serie y worker operativo; su publicación requiere aprobación hidrológica posterior |
| D16 | Separar permiso de recopilar INA de aprobación hidrológica para publicar o calcular | Decisión técnica implementada el 4/10/2026, pendiente de activación con evidencia externa | El worker exige derechos registrados, serie pública idéntica al catálogo y activación explícita; puede almacenar observaciones con `approved=false`. Solo una revisión hidrológica posterior habilita publicación. Ver [runbook](docs/runbooks/F4-INA-COLLECTION.md) |
| D17 | DigitalOcean como primera opción técnica para staging Android, dentro del tope de referencia | Provisional; sin contratación ni despliegue | [ADR-008](docs/adr/008-hosting-beta.md) estima US$59,45/mes de base para VM, PostgreSQL/PostGIS y Spaces; faltan checkout, región/latencia, privacidad, restore y responsable |
| D18 | Separar cierre técnico de fase de la habilitación operativa de fuentes y beta | Alcance técnico aprobado por pedido del titular el 4/10/2026 | Una fase puede cerrar su implementación verificable con fixtures y gates apagados. Derechos, revisión hidrológica, feed SMN, tiles, dispositivos y despliegue conservan gates de release; no se interpretan como aprobados. El adapter SMN pasa del criterio F3 al trabajo F8; clusters y agregación quedan fuera del piloto de 10–30 estaciones y rango de 30 días salvo que una prueba de volumen los exija. Ver [cierre técnico](docs/implementation/TECHNICAL-CLOSURE.md) |

## Registro de preguntas pendientes, responsable y salida

| Pendiente | Quién valida | Evidencia de cierre | Bloquea |
|---|---|---|---|
| Permisos/redistribución por red INA y revisión de condiciones GeoRef | Responsable legal + organismos cuando corresponda | Ficha/permiso fechado para INA; licencia CC BY 4.0, atribución y cuotas GeoRef documentadas en F1; revisión del producto | Publicación de datos afectados |
| Feed SMN estable y completo, SAT/ACP; términos aplicables | Integraciones; consulta a SMN si falta documentación | URL documentada, muestras Alert/Update/Cancel, política de consulta y licencia/términos del recurso | MUST avisos y pushes oficiales |
| Zona horaria de metadata sin offset | Datos + proveedor | Definición por campo/dataset | Interpretar esos campos como instante |
| Datum/unidad/vigencia/autoridad de umbrales | Especialista + fuente | Referencia comprobable y regla por serie | Comparación de umbrales |
| Cadencia/retraso/ruido por serie | Datos/hidrología | Informe posterior al arranque con 14 días de datos reales + aprobación de parámetros; la muestra D15 no cuenta | Frescura/tendencia/reglas calculadas; no bloquea la demo inicial |
| Estaciones/localidades piloto representativas | Producto/geoespacial | Lista curada con justificaciones | Promesa de cobertura piloto |
| Android/iOS iniciales, macOS y dispositivos | Producto/mobile | Presupuesto y smoke de ambas plataformas | Compromiso de lanzamiento iOS |
| Paquetes compatibles, gráficos accesibles y MapLibre | Mobile | Spike con versiones fijadas y licencia | Stack definitivo mobile |
| Hosting, región, DB/PostGIS, backups, privacidad y coste | Operación/seguridad/producto | Cotización comparable + restore + ADR | Producción |
| Responsable tratamiento y textos legales | Responsable legal | Política/contratos/canal de derechos aprobados | Beta pública con identificadores |
| App exacta “Altura de los Ríos” | Producto | Enlace/package/editor inequívoco | Solo benchmark; no arquitectura |
| Métricas UX y lenguaje “sin cambios destacados” | Producto + usuarios | Prueba de comprensión | Release público |

## Regla para cambiar decisiones

Modificar ADR con fecha, evidencia, alternativas y efectos en datos/contratos. No agregar una nueva tecnología solo para “escalar a futuro”. Redis requiere medición; TimescaleDB requiere prueba de volumen/operación; cuenta requiere necesidad del usuario; IA requiere evaluación de mejora frente a plantillas. Los cambios de derechos/semántica se revisan aunque no cambie un endpoint.

## D19 · Cierre técnico Android con alternativas simples (4/10/2026)

**Aceptada por instrucción del titular:** cerrar las fases realizadas, simplificando la implementación sin perder consulta hidrológica, estados honestos, histórico, seguimiento y recuperación. D19 sustituye los criterios técnicos originales de F1–F12 según la matriz de `docs/implementation/TECHNICAL-CLOSURE.md`; no aprueba datos ni simula una beta pública.

- F1 se cierra como investigación con resultado NO-GO para fuentes aún sin habilitación. Las acciones EXT-01–05 quedan como activación externa trazada, no implementación pendiente.
- Android 8+ es la entrega actual. iOS queda fuera de este corte por decisión Android primero; no se declara probado.
- F8 usa consultas periódicas a nuestra API y notificaciones locales Android, con permiso, reglas por estación, episodios, revalidación, caducidad e historial. JobScheduler persiste reinicios, pero Android puede demorar o suspenderlo. No es entrega inmediata, servicio de emergencia ni reemplazo de los canales oficiales. Primera consulta sin aviso; no replay de histórico ni datos simulados. FCM/APNs, registro de tokens y dispatcher no son necesarios para este alcance; el motor/outbox existentes quedan inactivos como base futura.
- SMN mantiene cobertura no confirmada y acceso al sitio oficial mientras se resuelve el feed completo. Esta alternativa permite consulta manual; **no equivale a avisos oficiales integrados ni a cobertura zonal verificada**. EXT-02 mantiene esos gates.
- F7 entrega lista espacial funcional sin contratar tiles; MapLibre queda opcional. F9 ofrece lecturas crudas paginadas hasta 30 días sin agregación ni backfill previo al inicio. Es suficiente para el piloto pequeño; medir antes de ampliar.
- F10 acepta suites reproducibles, política nativa y recorrido en emulador. Evaluación con usuarios, TalkBack/dispositivos físicos y capacidad del hosting son aceptación operativa externa; no se atribuyen resultados no medidos.
- F11 usa health de API/DB/worker/calidad, consola JSON con cooldown, logs, backups y restore ensayable, en lugar de contratar dashboards. Un operador debe conectar el monitor y custodiar copias externas; el código no presta guardia humana.
- F12 cierra el paquete de entrega, Compose, firma y runbooks con prueba local Android. Hosting, claves/cuentas del titular, responsables y participantes son faltantes por motivos externos con resolución documentada. **Beta pública no ejecutada; F13 producción no iniciada.** No autoriza gastos.

La matriz central es la referencia vigente de alcance, evidencia y límites. Los diseños previos de push y criterios históricos se conservan como antecedentes; no son promesas activas de esta versión. Derechos, semántica hidrológica y honestidad de estados permanecen obligatorios.
