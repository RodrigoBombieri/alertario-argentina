# Evaluación exploratoria de F1 — resultado NO-GO

**Fecha:** 2 de octubre de 2026 (Argentina). **Estado de la fase:** F1 no aceptada; concluyó solamente la evaluación inicial de fuentes. **Resultado:** NO-GO para aprobar el piloto con datos oficiales y para declarar completo el MVP. Esta acta registra el resultado de la investigación; **no satisface todos los criterios de aceptación de F1** del [roadmap](../../../ROADMAP.md) ni cierra las historias US-01/02/03.

**Corrección del 4/10/2026:** la versión inicial trataba una autorización individual del SMN como obligatoria. No hay evidencia de que lo sea para datos cubiertos por una licencia abierta. EXT-02 pide identificar los términos aplicables al CAP concreto y resolver un feed operativo completo; una licencia pública verificable puede satisfacer la parte de uso sin respuesta individual. El NO-GO se mantiene por el feed y las pruebas pendientes.

**Decisión posterior del titular, 4/10/2026:** se admite iniciar la app sin mediciones oficiales. Los 14 días de observación pasan a ser una etapa de maduración **posterior al arranque** para cada serie y ya no impiden abrir una beta vacía. Esta decisión modifica el criterio del [roadmap](../../../ROADMAP.md), no reescribe el resultado histórico de esta acta. EXT-03 sigue pendiente para habilitar cadencia/frescura, variaciones, umbrales y reglas calculadas; transcurrir el plazo sin revisión de datos no los aprueba. Los derechos de fuente, la semántica de estaciones y el feed de avisos continúan pendientes para publicar esas funciones.

**Ajuste D15, 4/10/2026:** la primera interfaz muestra 14 días horarios sintéticos de una estación ficticia para explorarla; esta muestra no cuenta para EXT-03. Se eliminó la espera fija de 14 días de operación de beta. La serie real se recopilará por separado cuando se habilite su fuente.

## Evidencia obtenida

- GeoRef publica condiciones específicas CC BY 4.0, atribución y cuotas; se probaron búsqueda nominal, paginación y salida GeoJSON. Su uso en el producto requiere aplicar esas condiciones y revisión legal final.
- INA respondió consultas acotadas de catálogo, series, observaciones y GeoJSON. Se inventariaron 12 estaciones candidatas sobre Paraná y Uruguay, con series de altura/caudal y localidades GeoRef nominales. Cuatro series de altura tuvieron tres observaciones diarias en la ventana muestreada. Esto no certifica frecuencia estable, calidad ni representatividad.
- El índice CAP oficial de SMN devolvió HTML con enlaces a XML, sin un feed completo y estable identificado para automatizar Alert/Update/Cancel. La consulta fue puntual.
- Hay [sondas reproducibles](../../../scripts/probes/f1-probe.ps1), [analizador](../../../scripts/probes/f1-analyze.py), [evidencia fechada](README.md#evidencia-de-esta-corrida) y [consultas preparadas](OUTREACH.md). Ninguna consulta a organismos fue enviada.

## Criterios de salida y decisión

| Criterio F1 | Resultado al cierre | Consecuencia |
|---|---|---|
| Condiciones por fuente y red | GeoRef documentado; INA/PNA sin términos por red; SMN CAP sin términos específicos identificados | Documentar licencia o condiciones del recurso concreto; no presumir permiso individual SMN ni inferir licencia del CAP desde otros datasets |
| Feed SMN machine-readable y ciclo de vida | No confirmado | Avisos zonales MUST y pushes oficiales bloqueados |
| 14 días de cadencia, retrasos, nulos y revisiones | No realizado; hay una muestra puntual de tres días | Maduración posterior al arranque vacío; no aprobar `C/L/ε`, frescura ni variaciones horarias por serie mientras falte |
| Datum, unidad, época y umbrales | Unidad de catálogo observada; datum/vigencia no confirmados | Comparación de umbrales deshabilitada |
| Estaciones y localidades representativas | 12 candidatas y coincidencias nominales; sin revisión geoespacial/hidrológica | No prometer cobertura del piloto |
| GeoJSON y paginación acotada | Comprobadas puntualmente para INA/GeoRef | Válido para diseño de adapter, sin garantía de continuidad |

**Decisión histórica:** dar por concluida la investigación exploratoria con resultado NO-GO, sin cerrar ni aceptar F1. El contrato propio y las pruebas con datos sintéticos se pueden preparar para revisión. La decisión posterior D14 permite una beta vacía sin esperar 14 días; no habilita por sí misma datos reales, tendencias, umbrales ni avisos. D12 (alcance del piloto) sigue propuesta.

## Trabajo de desbloqueo trazable

1. **EXT-01 · Integraciones/legal:** obtener respuesta institucional INA/PNA sobre almacenamiento, redistribución, historia, atribución y presupuesto de consultas; registrar permiso o denegación por red/dataset. Ver [borrador](OUTREACH.md#ina--red-de-escalas-pna-difundida-por-a5).
2. **EXT-02 · Integraciones/SMN:** identificar un feed completo y estable, ejemplos de Alert/Update/Cancel, cobertura SAT/ACP y política de consulta; documentar los términos de uso del CAP concreto. Usar documentación pública cuando exista y consultar al SMN para lo que falte, sin exigir permiso individual por defecto. Ver [borrador](OUTREACH.md#smn--cap-y-avisos-zonales--aclaración-técnica-y-de-términos).
3. **EXT-03 · Datos/hidrología:** después de iniciar la recopilación autorizada, completar 14 días de datos reales por serie y revisar cadencia, retrasos, nulos, revisiones, unidades, datum/épocas y umbrales. No es un gate de arranque vacío; sí de habilitación de cálculos por serie. [Método](README.md#plan-de-medición-de-14-días-condicionado-a-presupuesto).
4. **EXT-04 · Producto/geoespacial/hidrología:** justificar relaciones localidad/estación, descartar homónimos y aprobar una lista piloto con cobertura y limitaciones explícitas. [Inventario](README.md#us-02--inventario-candidato).
5. **EXT-05 · Legal/producto:** revisar aplicación concreta de CC BY 4.0 de GeoRef, atribución e indicación de modificaciones en la API/UI antes de publicación.

Cada EXT debe adjuntar evidencia fechada y actualizar [DECISIONS](../../../DECISIONS.md), [DATA-SOURCES](../../../DATA-SOURCES.md) y la matriz por serie antes de cambiar un gate a aprobado. La fase F1 no se reabre solo por una nueva respuesta HTTP: se requiere resolver las condiciones semánticas y de uso que bloquean el piloto.

## Faltantes por motivos externos

Estos faltantes no se pueden resolver escribiendo código ni con una decisión unilateral del proyecto. Se mantienen abiertos con una vía de resolución y evidencia exigida; **registrarlos no equivale a aprobarlos**. El desarrollo aislado con datos sintéticos puede continuar, pero no se habilita publicación de datos oficiales ni se declara F1 aceptada.

| ID | Dependencia externa | Cómo resolverla | Evidencia para cambiar el estado |
|---|---|---|---|
| EXT-01 | INA y, si corresponde, PNA deben aclarar derechos y presupuesto de consultas de la red candidata | Presentar la [consulta preparada](OUTREACH.md#ina--red-de-escalas-pna-difundida-por-a5) desde un representante identificado; registrar respuesta por red/dataset y revisar sus condiciones con legal | Respuesta institucional fechada, alcance de uso/retención/atribución y cuota; allowlist aprobada por serie |
| EXT-02 | Falta demostrar un feed CAP vigente y completo para el alcance prometido; no se identificaron términos específicos del feed | Revisar documentación pública y, si es insuficiente, enviar la [consulta técnica preparada](OUTREACH.md#smn--cap-y-avisos-zonales--aclaración-técnica-y-de-términos); verificar Alert/Update/Cancel, cobertura SAT/ACP, cuota y términos aplicables | Documentación pública o respuesta fechada sobre el recurso concreto, más pruebas de completitud/ciclo de vida; no se exige permiso individual cuando la licencia abierta ya cubre el uso |
| EXT-03 | La maduración posterior al arranque requiere 14 días de datos reales con permiso/cuota INA; la interpretación requiere especialista | Tras EXT-01, ejecutar el [muestreo acotado](README.md#plan-de-medición-de-14-días-condicionado-a-presupuesto), registrar fallos y revisiones, y someter el informe a hidrología | 14 días de corridas fechadas, informe por serie y aprobación de cadencia, retraso, unidad, datum y ventanas habilitadas; no bloquea la beta vacía |
| EXT-04 | La representatividad de estaciones para localidades requiere revisión geoespacial e hidrológica | Revisar el [inventario candidato](README.md#us-02--inventario-candidato) con especialistas y producto; justificar cada relación y excluir homónimos o cursos incompatibles | Lista piloto firmada o registrada con relación, vigencia, limitaciones y responsable |
| EXT-05 | La aplicación de la licencia GeoRef al producto requiere revisión legal | Revisar [condiciones de GeoRef](https://www.argentina.gob.ar/georef/condiciones-de-uso-y-licencia), texto de atribución y señalización de modificaciones antes de publicar | Conformidad legal fechada y atribución comprobada en API/UI |

El envío de consultas a organismos y las aprobaciones de especialistas requieren representantes identificados. Los borradores existentes no se consideran enviados.
