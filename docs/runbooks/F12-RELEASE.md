# Entrega Android y activación de beta

D19 cierra el paquete técnico Android; este procedimiento es la entrada a la operación externa, no evidencia de una beta pública ya realizada.

## Probar sin contratar infraestructura

Desde la raíz del repositorio, PowerShell:

```powershell
$env:ColdStart__Enabled='true'
dotnet run --project backend/src/AlertaRio.Api --urls http://127.0.0.1:51737
```

En otra terminal:

```powershell
cd apps/mobile
flutter run -d emulator-5556 --dart-define=API_BASE_URL=http://10.0.2.2:51737
```

La app es Android, la raíz de la API no sirve una web. Android 8+ es el alcance actual. La muestra de 14 días está etiquetada y no produce avisos. Los avisos locales necesitan permiso Android y una API publicada con observaciones aptas; “Consultar ahora” permite comprobar conexión sin esperar al planificador. La app no usa GPS, cuenta ni tokens push. Reinstalar o borrar sus datos elimina favoritos, caché y reglas; desinstalar elimina la tarea periódica. Android detiene el trabajo al forzar cierre hasta volver a abrir la app.

## Firma de distribución

El ID Android es `ar.alertario.mobile`. Antes de la primera distribución, su titular debe custodiar una clave de firma y copia segura. Crear `apps/mobile/android/key.properties` privado con `storeFile`, `storePassword`, `keyAlias` y `keyPassword`. `storeFile` se resuelve respecto de `android/app`; usar ruta absoluta o `../archivo.jks`. Tanto propiedades como keystores están ignorados por Git. El build release falla expresamente sin este archivo: no utiliza la clave de debug.

```powershell
flutter build appbundle --release --dart-define=API_BASE_URL=https://api.example.org
```

Usar una URL HTTPS propia real. No subir claves, contraseñas ni `.env`. La compatibilidad de firma con una instalación anterior debe comprobarse antes de actualizar. La beta local usa APK debug; la validación de Android físico, firma del titular y eventual cuenta de tienda son **faltantes por motivos externos**, con resolución a cargo del titular.

## Despliegue cuando haya hosting

Copiar `.env.example` a `.env` en `infrastructure/environments/beta`, configurar dominio propio y ejecutar Docker Compose allí. API, worker y Caddy tienen imágenes reproducibles; el worker permanece ocioso hasta activación. Para muestra no se requiere DB. Para datos reales crear PostgreSQL/PostGIS, aplicar las 12 migraciones y usar los runbooks de GeoRef, INA y publicación. Mantener `ColdStart__Enabled=false` al habilitar `PublishedData__Enabled=true`. Ninguna migración ni script aprueba una fuente real.

Hacer smoke de `/health/live`, `/health/ready`, `/v1/status`, búsqueda y ficha; con persistencia agregar health de storage/worker/data y [operación](F11-OPERATIONS.md). El health no acredita derechos de datos ni usabilidad.

## Registro de aceptación operativa

Registrar fecha, versión/hash de APK e imágenes, modo de datos, modelo/versión Android, responsable, prueba offline/reinicio, permiso aceptado/denegado, ahorro de batería, consulta <10 s, lectura con TalkBack y texto grande, incidentes, feedback y acción correctiva. No completar este registro con resultados ficticios. No hay plazo de espera de 14 días para la demo; los datos reales maduran por separado.

Hosting dentro del tope de US$100/mes, región, claves de firma, contacto público, responsable y usuarios requieren recursos o decisiones fuera del repositorio. La propuesta DigitalOcean no contrata ni despliega nada por sí sola. F13 producción se inicia después de esa aceptación operativa.

