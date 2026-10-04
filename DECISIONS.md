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

## Registro de preguntas pendientes, responsable y salida

| Pendiente | Quién valida | Evidencia de cierre | Bloquea |
|---|---|---|---|
| Permisos/redistribución por red INA y revisión de condiciones GeoRef | Responsable legal + organismos cuando corresponda | Ficha/permiso fechado para INA; licencia CC BY 4.0, atribución y cuotas GeoRef documentadas en F1; revisión del producto | Publicación de datos afectados |
| Feed SMN estable y completo, SAT/ACP; términos aplicables | Integraciones; consulta a SMN si falta documentación | URL documentada, muestras Alert/Update/Cancel, política de consulta y licencia/términos del recurso | MUST avisos y pushes oficiales |
| Zona horaria de metadata sin offset | Datos + proveedor | Definición por campo/dataset | Interpretar esos campos como instante |
| Datum/unidad/vigencia/autoridad de umbrales | Especialista + fuente | Referencia comprobable y regla por serie | Comparación de umbrales |
| Cadencia/retraso/ruido por serie | Datos/hidrología | Informe 14 días + aprobación de parámetros | Frescura/tendencia/reglas calculadas |
| Estaciones/localidades piloto representativas | Producto/geoespacial | Lista curada con justificaciones | Promesa de cobertura piloto |
| Android/iOS iniciales, macOS y dispositivos | Producto/mobile | Presupuesto y smoke de ambas plataformas | Compromiso de lanzamiento iOS |
| Paquetes compatibles, gráficos accesibles y MapLibre | Mobile | Spike con versiones fijadas y licencia | Stack definitivo mobile |
| Hosting, región, DB/PostGIS, backups, privacidad y coste | Operación/seguridad/producto | Cotización comparable + restore + ADR | Producción |
| Responsable tratamiento y textos legales | Responsable legal | Política/contratos/canal de derechos aprobados | Beta pública con identificadores |
| App exacta “Altura de los Ríos” | Producto | Enlace/package/editor inequívoco | Solo benchmark; no arquitectura |
| Métricas UX y lenguaje “sin cambios destacados” | Producto + usuarios | Prueba de comprensión | Release público |

## Regla para cambiar decisiones

Modificar ADR con fecha, evidencia, alternativas y efectos en datos/contratos. No agregar una nueva tecnología solo para “escalar a futuro”. Redis requiere medición; TimescaleDB requiere prueba de volumen/operación; cuenta requiere necesidad del usuario; IA requiere evaluación de mejora frente a plantillas. Los cambios de derechos/semántica se revisan aunque no cambie un endpoint.
