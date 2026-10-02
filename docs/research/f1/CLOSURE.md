# Cierre de F1 — resultado NO-GO

**Fecha:** 2 de octubre de 2026 (Argentina). **Estado de la fase:** cerrada como evaluación inicial de fuentes. **Resultado:** NO-GO para aprobar el piloto con datos oficiales y para declarar completo el MVP. Este cierre registra el resultado de la investigación; **no satisface todos los criterios de aceptación de F1** del [roadmap](../../../ROADMAP.md) ni cierra las historias US-01/02/03.

## Evidencia obtenida

- GeoRef publica condiciones específicas CC BY 4.0, atribución y cuotas; se probaron búsqueda nominal, paginación y salida GeoJSON. Su uso en el producto requiere aplicar esas condiciones y revisión legal final.
- INA respondió consultas acotadas de catálogo, series, observaciones y GeoJSON. Se inventariaron 12 estaciones candidatas sobre Paraná y Uruguay, con series de altura/caudal y localidades GeoRef nominales. Cuatro series de altura tuvieron tres observaciones diarias en la ventana muestreada. Esto no certifica frecuencia estable, calidad ni representatividad.
- El índice CAP oficial de SMN devolvió HTML con enlaces a XML, sin un feed completo y estable identificado para automatizar Alert/Update/Cancel. La consulta fue puntual.
- Hay [sondas reproducibles](../../../scripts/probes/f1-probe.ps1), [analizador](../../../scripts/probes/f1-analyze.py), [evidencia fechada](README.md#evidencia-de-esta-corrida) y [consultas preparadas](OUTREACH.md). Ninguna consulta a organismos fue enviada.

## Criterios de salida y decisión

| Criterio F1 | Resultado al cierre | Consecuencia |
|---|---|---|
| Permiso/condiciones por fuente y red | GeoRef documentado; INA y SMN pendientes | No publicar ni redistribuir datos INA/SMN; no crear allowlist de producción |
| Feed SMN machine-readable y ciclo de vida | No confirmado | Avisos zonales MUST y pushes oficiales bloqueados |
| 14 días de cadencia, retrasos, nulos y revisiones | No realizado; hay una muestra puntual de tres días | No aprobar `C/L/ε`, frescura ni variaciones horarias por serie |
| Datum, unidad, época y umbrales | Unidad de catálogo observada; datum/vigencia no confirmados | Comparación de umbrales deshabilitada |
| Estaciones y localidades representativas | 12 candidatas y coincidencias nominales; sin revisión geoespacial/hidrológica | No prometer cobertura del piloto |
| GeoJSON y paginación acotada | Comprobadas puntualmente para INA/GeoRef | Válido para diseño de adapter, sin garantía de continuidad |

**Decisión:** cerrar el trabajo exploratorio F1 con resultado NO-GO. No se cambian los criterios de aceptación para declararlos cumplidos. Puede prepararse el contrato propio mínimo; una vez aprobado, F2 puede construir backend y pruebas **con datos sintéticos**. El uso de datos reales, tendencias aprobadas, umbrales, avisos y beta pública permanecen sujetos a los gates anteriores. D12 (alcance del piloto) sigue propuesta.

## Trabajo de desbloqueo trazable

1. **EXT-01 · Integraciones/legal:** obtener respuesta institucional INA/PNA sobre almacenamiento, redistribución, historia, atribución y presupuesto de consultas; registrar permiso o denegación por red/dataset. Ver [borrador](OUTREACH.md#ina--red-de-escalas-pna-difundida-por-a5).
2. **EXT-02 · Integraciones/SMN:** obtener feed documentado, autorizado y completo, ejemplos de Alert/Update/Cancel, cobertura SAT/ACP, cuotas y licencia. Ver [borrador](OUTREACH.md#smn--cap-y-avisos-zonales).
3. **EXT-03 · Datos/hidrología:** con presupuesto aprobado, completar 14 días de medición por serie y revisar cadencia, retrasos, nulos, revisiones, unidades, datum/épocas y umbrales. [Método](README.md#plan-de-medición-de-14-días-condicionado-a-presupuesto).
4. **EXT-04 · Producto/geoespacial/hidrología:** justificar relaciones localidad/estación, descartar homónimos y aprobar una lista piloto con cobertura y limitaciones explícitas. [Inventario](README.md#us-02--inventario-candidato).
5. **EXT-05 · Legal/producto:** revisar aplicación concreta de CC BY 4.0 de GeoRef, atribución e indicación de modificaciones en la API/UI antes de publicación.

Cada EXT debe adjuntar evidencia fechada y actualizar [DECISIONS](../../../DECISIONS.md), [DATA-SOURCES](../../../DATA-SOURCES.md) y la matriz por serie antes de cambiar un gate a aprobado. La fase F1 no se reabre solo por una nueva respuesta HTTP: se requiere resolver las condiciones semánticas y de uso que bloquean el piloto.
