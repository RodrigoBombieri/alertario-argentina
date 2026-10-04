# Revisión de cuarentena de ingesta

Este procedimiento es para un operador con acceso controlado a la base. La cuarentena conserva motivo, identificador externo y hash de la fila; no conserva el contenido original. Revisar el registro fuente por el canal autorizado y no copiar tokens ni datos personales en `review_note`.

1. Consultar filas abiertas por `provider` y `stream_key`, ordenadas por `received_at DESC`. Registrar el ID, motivo, `source_record_id` y `payload_hash` en el ticket de revisión. No borrar la fila.
2. Comprobar contra la fuente si fue un error de normalización, una corrección posterior o un conflicto real. Verificar permisos, serie, unidad, fecha/datum y aprobación antes de cualquier nueva ingesta.
3. Si hay una observación corregida y autorizada, volver a procesarla mediante el escritor transaccional normal. Verificar medición/revisión, `series_latest`, checkpoint y cobertura. Después llamar `SELECT review_quarantined_record(<id>, 'reprocessed', '<operador>', '<ticket y evidencia>')`.
4. Si se confirma que la fila no corresponde a una medición publicable, llamar `SELECT review_quarantined_record(<id>, 'dismissed', '<operador>', '<ticket y motivo>')`. La operación devuelve `true` una sola vez; `false` indica ID inexistente o ya revisado. No usar `dismissed` para forzar publicación.
5. Comprobar que la fila quedó con `reviewed_at`, `reviewed_by` y `review_note`. El cierre de revisión no publica mediciones ni cambia la cobertura del checkpoint: si era parcial, un lote posterior válido debe restablecerla. Nunca modificar manualmente `measurements` o `series_latest` para “resolver” una alerta.

La consulta de la ficha suspende comparaciones calculadas si hubo una cuarentena **abierta** reciente o si la cobertura del stream no es completa. La revisión queda auditable; un replay no debe borrar esa evidencia. Este flujo fue probado con registros sintéticos y no autoriza datos oficiales mientras sigan pendientes F1/F3.
