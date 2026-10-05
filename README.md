# AlertaRío Argentina

Aplicación ciudadana para consultar información hidrológica oficial, entender sus cambios y seguir avisos de autoridades en Argentina.

**Estado al 4/10/2026:** F1–F12 cerradas técnicamente para el alcance Android D18/D19. El [acta de cierre](docs/implementation/TECHNICAL-CLOSURE.md) detalla evidencia, alternativas y faltantes por motivos externos. La beta pública y F13 producción no están ejecutadas; las fuentes reales permanecen apagadas hasta su habilitación.

Para avanzar con los bloqueos humanos y de organismos: [cómo pedir respuestas a INA/SMN y la revisión hidrológica](docs/research/f1/OUTREACH.md), y [base operativa de beta](docs/implementation/F12-OPERATING-BASELINE.md). El titular confirmó Android primero y US$100/mes como tope de referencia para cotizar; no hay contratación.

La app consulta localidades, estaciones, ficha, tendencias e histórico; guarda favoritos y caché; explora estaciones sin requerir tiles y ofrece avisos locales Android. La API puede publicar datos PostgreSQL aprobados; los workers INA/GeoRef son opt-in. Los health y runbooks cubren operación y recuperación. Ver [cierre por fase](docs/implementation/TECHNICAL-CLOSURE.md) y [entrega Android](docs/runbooks/F12-RELEASE.md).

## Empezar aquí

1. [PLAN MAESTRO DE DESARROLLO — ALERTARÍO](PLAN-MAESTRO.md).
2. [Fuentes y verificaciones](DATA-SOURCES.md), [contratos e integraciones](API-INTEGRATIONS.md), [evidencia inicial](docs/research/VERIFICATION.md) y [evaluación F1](docs/research/f1/CLOSURE.md).
3. [Producto](PRODUCT.md), [arquitectura](ARCHITECTURE.md) y [dominio](DOMAIN.md).
4. [Roadmap](ROADMAP.md), [backlog](BACKLOG.md) y [decisiones](DECISIONS.md).

Para probar el backend sintético: `dotnet test backend/AlertaRio.sln --no-restore` después de restaurar dependencias según [F2](docs/implementation/F2.md).

**Arranque con muestra (D15):** con `ColdStart:Enabled=true`, la app muestra una localidad y estación ficticias con 14 días de lecturas horarias simuladas. La interfaz y la API las marcan como sintéticas; no representan datos ni avisos oficiales. Los 14 días de datos reales por serie se cuentan por separado desde que se habilite la recolección. Sin condiciones de uso verificadas no se consulta ni publica una fuente, y el mero transcurso de 14 días no habilita tendencias, umbrales, avisos ni notificaciones.

### Ver la demo local

La interfaz es una app Flutter para Android; `http://127.0.0.1:51737/` es la raíz de la API y no sirve una página web. Con el emulador Android iniciado, ejecutar en dos terminales desde la raíz del repositorio:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/src/AlertaRio.Api --urls http://127.0.0.1:51737
```

```powershell
cd apps/mobile
flutter run -d emulator-5556 --dart-define=API_BASE_URL=http://10.0.2.2:51737
```

Si el emulador tiene otro ID, consultarlo con `flutter devices` y reemplazar `emulator-5556`. En la app, buscar `ejemplo` para abrir la localidad y estación ficticias. `/health/ready` indica si el backend de demo puede servir datos sintéticos; `/health/storage` verifica PostgreSQL y migraciones solo cuando se configura el modo persistido. Esta demo no representa cobertura ni alertas oficiales.

Para probar el arranque inicial, iniciar la API con `SyntheticData__Enabled=false` y `ColdStart__Enabled=true`. `GET /v1/status` devuelve `mode=collecting` y `officialDataAvailable=false`; el modo presenta solo la muestra y **no ejecuta un colector real**. Buscar `ejemplo` para ver la serie simulada de 14 días. Los workers [INA](docs/runbooks/F4-INA-COLLECTION.md) y [GeoRef](docs/runbooks/F4-GEOREF-CATALOG.md) son opt-in y están apagados hasta registrar sus derechos. La [lectura publicada](docs/runbooks/F5-PUBLICATION.md) requiere además revisión hidrológica y asociaciones aprobadas. Los [contenedores de beta](DEPLOYMENT.md#contenedores-y-ambientes) permiten validar el empaquetado local, sin desplegar ni habilitar fuentes.

## Documentación

| Documento | Contenido |
|---|---|
| [VISION](VISION.md) | Propósito, usuarios, diferenciación y negocio futuro |
| [PRODUCT](PRODUCT.md) | Alcance, prioridad, pantallas y criterios de producto |
| [ARCHITECTURE](ARCHITECTURE.md) | Stack, límites, diagramas, repositorio y evolución |
| [DOMAIN](DOMAIN.md) | Entidades, relaciones, esquema lógico e invariantes |
| [DATA-SOURCES](DATA-SOURCES.md) | INA, SMN, GeoRef y fuentes complementarias |
| [API-INTEGRATIONS](API-INTEGRATIONS.md) | Adapters, normalización, ingesta y resiliencia |
| [SECURITY](SECURITY.md) | Acceso anónimo, instalaciones, privacidad y amenazas |
| [TESTING](TESTING.md) | Matriz de pruebas y fixtures |
| [DEPLOYMENT](DEPLOYMENT.md) | Hosting comparado, CI/CD, backups y ambientes |
| [ROADMAP](ROADMAP.md) | Fases, dependencias y puertas de aceptación |
| [BACKLOG](BACKLOG.md) | Épicas, features, historias y tareas ejecutables |
| [DECISIONS](DECISIONS.md) | Decisiones propuestas y validaciones pendientes |
| [GLOSSARY](GLOSSARY.md) | Vocabulario compartido |
| [ADR](docs/adr/README.md) | Registros de decisiones de arquitectura |
| [Competidores](docs/research/COMPETITORS.md) | Evidencia pública y evaluación competitiva |
| [Motores](docs/design/ENGINES.md) | Tendencias, estado y notificaciones |
| [API propia](docs/design/OWN-API.md) | Contrato propuesto, errores y semántica |
| [Operación y riesgos](docs/design/OPERATIONS.md) | SLO internos, métricas y mitigaciones |

## Condiciones para publicar datos oficiales

La evaluación acotada de F1 concluyó con resultado NO-GO; la fase F1 no está aceptada. Eso no impide abrir una beta con muestra D15 claramente identificada. Para habilitar datos oficiales siguen pendientes las condiciones de la red INA/PNA y la correspondencia estación/serie/datum; la cadencia y los cálculos se aprueban por serie tras reunir datos reales. Los avisos requieren un feed SMN estable y completo con términos de uso identificados. El contrato propio y los componentes sintéticos existentes en F2 son material de revisión, sin aprobación de fase ni suposiciones sobre esas propiedades.

El stack recomendado es Flutter + Riverpod, ASP.NET Core 10, PostgreSQL + PostGIS y un worker del mismo backend. Redis, TimescaleDB, microservicios, cuentas ciudadanas y predicción quedan fuera del arranque. El hosting todavía no está elegido.

## Reglas permanentes

- Datos medidos, avisos oficiales, cálculos de AlertaRío y pronósticos son categorías distintas.
- Un umbral superado no es una orden de evacuación ni una alerta emitida.
- El celular consume únicamente la API propia para datos hidrológicos, meteorológicos y geográficos.
- Un dato faltante nunca vale cero. Una fuente inaccesible nunca significa ausencia de alertas.
- Todo número visible conserva origen, unidad, hora de medición y calidad.
- Las capturas de investigación son históricas: no utilizarlas como información vigente.

No se declara una licencia propia ni se relicencian los datos externos en esta etapa. Resolverlo antes de publicar el repositorio o redistribuir sus muestras.
