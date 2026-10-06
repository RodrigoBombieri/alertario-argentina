# App Android

Cliente Flutter de la API propia. Android 8+ es la entrega actual; iOS no está validado.

- [Ejecutar la demo](../../docs/EJECUTAR.md)
- [Pruebas](../../TESTING.md)
- [Avisos locales](../../docs/runbooks/F8-ANDROID-NOTICES.md)
- [Preparar Google Play](../../docs/PLAY-STORE.md)

`API_BASE_URL` se configura al compilar mediante `--dart-define` y exige HTTPS en release. La caché usa sqflite; el mapa MapLibre es opcional.

La firma de distribución requiere `android/key.properties` privado. No guardar claves ni contraseñas en el repositorio.
