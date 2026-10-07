# Identidad y capturas

Marca propia: [logo vectorial](logo.svg), compartido visualmente con el encabezado y el ícono Android. Íconos Material, sin nuevas dependencias.

Capturas reales del 6–7/10/2026, tomadas con ADB en Pixel 9 Pro emulado, Android 16. PNG de 1080 × 2160, sin retoque:

- [Búsqueda](busqueda.png): marca, presentación y localidad de ejemplo.
- [Estación](estacion.png): nivel, fuente y gráfico con escala.
- [Historial](historial.png): lecturas paginadas y evolución del nivel.

App 0.1.0+1 debug, API local en modo ColdStart y datos ficticios. Sin fuentes reales habilitadas. APK: `apps/mobile/build/app/outputs/apk/debug/app-debug.apk`.
SHA-256: `692634584D7ADC7F0F43508004EDF85F1B2FEDE55FAC65A6715B10E9CFBF2B1A`.

Verificación: análisis Flutter sin observaciones, 15 pruebas aprobadas y recorrido manual en Android. Compilación con Flutter 3.29.2, JDK de Android Studio y caché Gradle offline; se usó una copia local del SDK por falta del lanzador global `flutter.bat`.
