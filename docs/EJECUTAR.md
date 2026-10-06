# Ejecutar y ver la app

En Windows, necesitás **.NET SDK 10**, **Flutter 3.29.2** y **Android Studio con un emulador iniciado**. Para esta demo no necesitás base de datos ni cuentas.

## 1. Iniciar la API

Abrí PowerShell en la raíz del repositorio y dejá esta terminal abierta:

```powershell
$env:ColdStart__Enabled='true'
$env:SyntheticData__Enabled='false'
$env:PublishedData__Enabled='false'
dotnet run --project backend/src/AlertaRio.Api --no-launch-profile --urls http://127.0.0.1:51737
```

## 2. Abrir la app

En otra terminal, desde la raíz:

```powershell
cd apps/mobile
flutter pub get --enforce-lockfile
flutter devices
```

Copiá el ID del emulador y reemplazá `<ID>` en ambos comandos:

```powershell
& "$env:LOCALAPPDATA/Android/sdk/platform-tools/adb.exe" -s <ID> reverse tcp:51737 tcp:51737
flutter run -d <ID> --dart-define=API_BASE_URL=http://127.0.0.1:51737
```

Si instalaste el SDK en otra carpeta, ajustá la ruta de `adb.exe`. El túnel ADB conecta el emulador con la API de tu PC.

## 3. Verla

En el **emulador**, buscá **ejemplo**, elegí la localidad y abrí la estación. Probá el historial y la estrella de favoritos. Los datos están identificados como ficticios.

Si no conecta, comprobá [estado de la API](http://127.0.0.1:51737/v1/status) y repetí el comando `adb reverse`. La raíz de esa URL no muestra la interfaz: **la app se ve en Android**.

Para detener: `q` en la terminal Flutter y `Ctrl+C` en la API.
