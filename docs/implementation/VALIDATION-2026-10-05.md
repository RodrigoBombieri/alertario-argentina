# Verificación de cierre — 5/10/2026

Resultados locales del corte Android D19, con datos ficticios y fuentes oficiales apagadas:

- 85 pruebas de contrato .NET y 13 de integración PostgreSQL/PostGIS aprobadas.
- 12 migraciones desde base vacía y 5 smokes SQL aprobados.
- Restore en base aislada con versiones y conteos idénticos; base temporal eliminada.
- 15 pruebas Flutter aprobadas y análisis sin observaciones.
- 18 comprobaciones Java de episodios/antigüedad aprobadas.
- Recorrido Android de búsqueda, ficha, historial y favoritos aprobado en emulador.
- Prueba nativa de aviso único, replay, dato vencido/sintético y revocación aprobada.
- Monitor: demo sana devuelve 0; persistencia no configurada devuelve 1 con los health afectados.
- Script de backup probado con el contenedor local: catálogo legible, copia y manifiesto SHA-256.
- Carga sintética local: 100 consultas, concurrencia 5, cero fallos; p95 280,54 ms, máximo 399,25 ms. No acredita capacidad de hosting/DB reales.
- Workflows CI de backend/DB y Flutter/APK preparados; no se afirma ejecución remota sin push.

Accesibilidad automática: texto 200%, controles etiquetados y tamaño mínimo Android; no sustituye TalkBack con usuarios. El build local usa un almacén temporal de certificados públicos Windows/Java y mantiene TLS validado, sin alterar el JDK global. La prueba nativa identifica sus notificaciones como PRUEBA SINTÉTICA y limpia reglas e historial.

Los [faltantes externos](TECHNICAL-CLOSURE.md) conservan resolución explícita. No hay beta pública, gastos ni fuentes reales activadas.

Para reproducir el build con menos procesos y memoria se fijaron dos workers Gradle, heap máximo de 4 GiB y compilación Kotlin dentro del proceso Gradle. Esto evita depender de un daemon Kotlin separado.

APK final: apps/mobile/build/app/outputs/flutter-apk/app-debug.apk. Build debug normal de lib/main.dart aprobado el 5/10/2026, URL local de emulador http://10.0.2.2:51737. Tamaño: 254422843 bytes. SHA-256: E00EF49A7BB7D23CE371554855BFE779BA2599E489780F3B417C9C93A5667EE1. Prueba nativa final repetida y aprobada después de ajustar Kotlin y TTL.
