# AlertaRío móvil

Flutter Android/iOS en desarrollo. La app usa exclusivamente la API propia y no incluye datos oficiales.

Desde esta carpeta: `flutter analyze` y `flutter test`. Para el emulador Android, iniciar la API de Development con fixture sintética y ejecutar `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:<puerto>`.

En release, `API_BASE_URL` debe ser HTTPS. Favoritos, últimas fichas y series recientes se guardan en SQLite del dispositivo; la pantalla señala explícitamente cuándo muestra una copia offline. El gráfico marca huecos de cadencia y ofrece lecturas en texto.

La exploración espacial conserva una lista accesible. Para probar MapLibre se puede pasar `--dart-define=MAP_STYLE_URL=https://<estilo-autorizado>`; en release se exige HTTPS. El estilo y sus tiles deben tener autorización y atribución antes de distribuir la app. Sin esa configuración, la app muestra solo la lista. Estado y pendientes: [F6](../../docs/implementation/F6.md) y [F7](../../docs/implementation/F7.md).

El flujo principal se prueba en Android con la API sintética local mediante `flutter test integration_test/main_flow_test.dart -d <id-android> --dart-define=API_BASE_URL=http://10.0.2.2:51737`. El smoke MapLibre usa `integration_test/map_smoke_test.dart` y `blank_style.json` servidos localmente como estilo sin tiles; necesita `MAP_STYLE_URL` apuntando al servidor accesible desde el emulador. Pasó en un emulador Android 16 con GPU por software; todavía faltan tiles de proveedor y dispositivos físicos.
