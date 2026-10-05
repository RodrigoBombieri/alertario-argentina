# Avisos Android sin proveedor push

Decisión D19: el piloto usa JobScheduler de Android cada 15 minutos, sujeto a red y al sistema operativo; no garantiza puntualidad ni consulta continua. Permanece después de reiniciar el teléfono, pero no tras desinstalar. El cierre forzado suspende el trabajo hasta reabrir la app. El usuario puede consultar manualmente, revisar la última consulta, desactivar una estación y borrar historial. Hasta 20 estaciones, historial local de 100 entradas, sin cuentas ni envío de identificadores al backend.

Se consulta exclusivamente `/v1/stations/{id}/summary` de la API propia, HTTPS en release, sin redirecciones ni caché HTTP. Cada respuesta se limita a 64 KiB y 8 s de conexión/lectura. La app exige respuesta real, dato `current`, calidad `accepted`, frescura `fresh`, observación de como máximo una hora respecto del reloj del servidor y condición calculada conocida. Este límite conservador puede omitir avisos de series lentas: no se relaja para aparentar cobertura.

La primera respuesta válida establece una referencia, sin avisar por episodios anteriores. Un cambio a `followUp`, `aboveAlertThreshold` o `aboveEvacuationThreshold` solo prepara aviso si la observación es posterior tanto a la observación anterior como a la consulta anterior. Repeticiones, correcciones y backfill anterior a la consulta no abren episodios. Una condición nueva más alta puede avisar; el retorno a `noNotableChange` rearma. Una respuesta stale/desconocida no rearma ni genera aviso.

Antes de mostrar se repite la petición y se compara versión/condición; se cancela si cambian, si se desactiva la regla o si tarda más de 20 s el par de consultas. Siempre existe la carrera inevitable entre respuesta de red y presentación; no se promete retractación instantánea offline. Un 403/404 elimina la regla y retira la notificación; un fallo o dato no apto retira el aviso visible, conserva el episodio y reintenta en la próxima ejecución. Una notificación pendiente vence a la hora. Tocar abre la app para consultar estado actual.

Se guarda episodio antes de llamar a Android: un crash en ese instante puede perder un aviso; se prefiere esto a duplicarlo. El historial indica avisos preparados, no entrega/lectura garantizada. Permisos o canal bloqueado impiden preparar avisos. Desactivar y activar nuevamente establece otra referencia sin replay.

No se integra CAP SMN sin feed completo verificable. La app muestra cobertura no confirmada y un acceso al [sitio oficial SMN](https://www.smn.gob.ar/alertas). Resolver EXT-02 habilitará una ampliación de avisos oficiales; no se presenta un umbral como orden ni una lista vacía como ausencia de alertas.

El outbox y motor del servidor se conservan probados como base alternativa; no hay dispatcher activo, endpoints de instalación ni tokens que custodiar. La entrega iOS y push en tiempo cercano al real quedan fuera del alcance Android D19. No hay que operar Firebase para esta versión.

Referencias técnicas: [JobScheduler periódico](https://developer.android.com/reference/android/app/job/JobInfo.Builder#setPeriodic(long)), [permiso de notificaciones](https://developer.android.com/develop/ui/compose/notifications/notification-permission).

Pruebas: política Java ejecutable en `android/policy-tests/NoticePolicyTest.java`, widget Flutter y `integration_test/local_notices_test.dart`. La integración levanta una API ficticia dentro del emulador y puede emitir notificaciones identificadas como PRUEBA SINTÉTICA; limpia reglas/historial al finalizar. Requiere permiso POST_NOTIFICATIONS otorgado al APK de prueba.
