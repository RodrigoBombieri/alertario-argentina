# AlertaRío móvil

Flutter Android/iOS en desarrollo. La app usa exclusivamente la API propia y no incluye datos oficiales.

Desde esta carpeta: `flutter analyze` y `flutter test`. Para el emulador Android, iniciar la API de Development con fixture sintética y ejecutar `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:<puerto>`.

En release, `API_BASE_URL` debe ser HTTPS. Favoritos y últimas fichas válidas se guardan en SQLite del dispositivo; la pantalla señala explícitamente cuándo muestra una copia offline. La exploración espacial usa lista mientras se elige un proveedor autorizado de tiles. Estado y pendientes: [F6](../../docs/implementation/F6.md) y [F7](../../docs/implementation/F7.md).
