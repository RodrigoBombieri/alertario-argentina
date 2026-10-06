# Pruebas

Las pruebas ordinarias usan datos sintéticos y no consultan organismos. El [registro del 5/10/2026](docs/implementation/VALIDATION-2026-10-05.md) conserva resultados, límites y APK verificado.

## Backend

Desde la raíz:

```powershell
dotnet restore backend/AlertaRio.sln --locked-mode
dotnet test backend/tests/AlertaRio.ContractTests --configuration Release
docker compose -f infrastructure/compose.yaml up -d
./infrastructure/db/apply-local-migrations.ps1
dotnet test backend/tests/AlertaRio.DbIntegrationTests --configuration Release
./infrastructure/db/verify-local-restore.ps1
```

Los scripts locales requieren PowerShell 7 y el comando `docker-compose`. Usar la base local de pruebas; nunca ejecutar fixtures/smokes sobre producción. La integración necesita PostgreSQL/PostGIS real.

## App

Desde `apps/mobile`:

```powershell
flutter pub get --enforce-lockfile
flutter analyze
flutter test
flutter test integration_test/main_flow_test.dart -d <ID_DEL_EMULADOR> --dart-define=API_BASE_URL=http://127.0.0.1:51737
```

Para el recorrido, iniciar antes la API demo y el túnel ADB de la [guía de ejecución](docs/EJECUTAR.md). La prueba nativa de avisos usa una API ficticia dentro del emulador; requisitos en el [runbook Android](docs/runbooks/F8-ANDROID-NOTICES.md).

Los comandos de política Java y build están en el [workflow Android](.github/workflows/mobile.yml); las migraciones desde cero y smokes, en el [workflow backend](.github/workflows/backend.yml).

## Qué debe conservarse

- Reintentos idempotentes, revisiones trazables y revocación efectiva.
- Calidad, frescura y umbrales independientes; no avisar con datos ficticios/vencidos.
- Caché identificada; 403/404 purgan copias afectadas.
- Primera consulta sin replay; episodios sin duplicación y revalidación antes del aviso.
- Texto ampliado y controles accesibles.

La publicación agrega pruebas en Android físico, TalkBack, ahorro de batería, actualización firmada y compatibilidad API 36/16 KB. El emulador y la carga sintética local no acreditan esas condiciones.
