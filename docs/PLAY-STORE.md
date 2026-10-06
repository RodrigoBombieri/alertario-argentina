# Preparar AlertaRío para Google Play

Revisado el **5/10/2026**. Hay una demo Android instalable; **todavía no hay un AAB validado para la tienda**.

## 1. Resolver lo técnico

- Actualizar Flutter/toolchain y `compileSdk`/`targetSdk` a **API 36 o superior**. El APK actual apunta a 35. Google exige 36 para nuevas apps/actualizaciones desde el 31/8/2026. [Requisito oficial](https://developer.android.com/google/play/requirements/target-sdk).
- Verificar las bibliotecas nativas de Flutter y MapLibre y ejecutar la app en un entorno de **16 KB**. No asumir compatibilidad solo por usar NDK 28. [Guía oficial](https://developer.android.com/guide/practices/page-sizes).
- Tener una API propia **HTTPS** disponible. Elegir demo claramente rotulada o datos reales con [activación completada](../API-INTEGRATIONS.md).
- Publicar política de privacidad con responsable/contacto y agregar acceso desde la app. Revisar SDK, logs del hosting y tiles al declarar datos. [Data safety](https://support.google.com/googleplay/android-developer/answer/10787469).

## 2. Preparar cuenta y firma

Crear/verificar la cuenta en [Play Console](https://play.google.com/console). Confirmar el identificador `ar.alertario.mobile` antes de la primera publicación.

Crear una clave de carga fuera del repositorio; guardar una copia segura y sus contraseñas:

```powershell
keytool -genkeypair -v -keystore "$env:USERPROFILE/alertario-upload.jks" -keyalg RSA -keysize 2048 -validity 10000 -alias upload
```

Crear `apps/mobile/android/key.properties` **privado**, reemplazando los valores:

```properties
storeFile=C:/Users/TU_USUARIO/alertario-upload.jks
storePassword=TU_PASSWORD
keyAlias=upload
keyPassword=TU_PASSWORD
```

El proyecto ya exige firma propia en release. No subir este archivo ni el keystore a Git. Activar Play App Signing al configurar la entrega. [Firma y build Flutter](https://docs.flutter.dev/deployment/android).

## 3. Generar el AAB

Aumentar el número después de `+` en `apps/mobile/pubspec.yaml` para cada entrega; debe superar el último usado en Play. Desde `apps/mobile`, con la URL real:

```powershell
flutter pub get --enforce-lockfile
flutter analyze
flutter test
flutter build appbundle --release --dart-define=API_BASE_URL=https://api.TU-DOMINIO
```

Archivo: `apps/mobile/build/app/outputs/bundle/release/app-release.aab`. Subir **ese AAB**, no el APK debug de las capturas. Comprobar API objetivo y bibliotecas nativas en el análisis del bundle.

## 4. Completar la ficha

Nombre, descripciones, contacto, política de privacidad, público objetivo, clasificación de contenido, anuncios y acceso a la app. Completar Data safety según el comportamiento real.

Preparar icono **512 × 512**, imagen destacada **1024 × 500** y al menos dos capturas reales. Para teléfono conviene capturar a **1080 × 1920**, sin marco ni datos personales. Las capturas del README documentan la demo; generar las de tienda desde el build de distribución y con el formato admitido. [Especificaciones](https://support.google.com/googleplay/android-developer/answer/9866151).

No presentar el proyecto como app oficial de organismos ni prometer alertas inmediatas o cobertura que no tiene.

## 5. Probar y enviar

Subir primero a prueba interna. Instalar desde Play y verificar inicio, búsqueda, historial, offline, permisos, batería, TalkBack y actualización firmada; revisar el informe previo al lanzamiento.

**Cuentas personales creadas después del 13/11/2023:** Google exige prueba cerrada con **12 testers durante 14 días continuos** antes de solicitar acceso a producción. Es un requisito de tienda, independiente de los 14 días de datos de ejemplo. [Condiciones oficiales](https://support.google.com/googleplay/android-developer/answer/14151465?hl=es).

Resolver errores, completar las declaraciones y enviar a revisión de producción cuando la cuenta tenga acceso. Registrar versión, hash, pruebas y responsable según el [registro de entrega](runbooks/F12-RELEASE.md). La aprobación corresponde a Google.

