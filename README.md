# AlertaRío Argentina

Aplicación ciudadana para consultar información hidrológica oficial, entender sus cambios y seguir avisos de autoridades en Argentina.

**Estado: F1 y F2 no aceptadas como fases completas.** La evaluación exploratoria de F1 terminó con resultado NO-GO; sus [faltantes por motivos externos y pasos de resolución](docs/research/f1/CLOSURE.md#faltantes-por-motivos-externos) están registrados. F2 tiene un [backend sintético de revisión](docs/implementation/F2.md), sin infraestructura desplegada. Las consultas puntuales a fuentes no certifican disponibilidad continua ni aclaran las condiciones INA/PNA o el feed CAP del SMN. No se presupone que los datos abiertos del SMN requieran autorización individual.

Para avanzar con los bloqueos humanos y de organismos: [cómo pedir respuestas a INA/SMN y la revisión hidrológica](docs/research/f1/OUTREACH.md), y [base operativa de beta](docs/implementation/F12-OPERATING-BASELINE.md). El titular confirmó Android primero y US$100/mes como tope de referencia para cotizar; no hay contratación.

F3 continúa con [normalización aislada, selección revisada por serie y fixtures sintéticas](docs/implementation/F3.md). F4 tiene [esquema PostGIS, ingesta transaccional, renovación de lease y revisión auditable de cuarentena validados localmente](docs/implementation/F4.md). F5 tiene [motores de tendencias/estado, summary e historial reciente persistidos solo en Development](docs/implementation/F5.md). F6 tiene [Flutter con favoritos, caché y gráfico reciente](docs/implementation/F6.md); F7 tiene [consulta espacial PostGIS, vista MapLibre opcional y lista de respaldo](docs/implementation/F7.md). F8 tiene [decisión de episodios y outbox transaccional sintético](docs/implementation/F8.md), F9 la [consulta histórica paginada](docs/implementation/F9.md), F10 la [validación sistémica](docs/implementation/F10.md), F11 las [métricas de ingesta](docs/implementation/F11.md) y F12 la [preparación de gates de beta](docs/implementation/F12.md). Ninguna de F1–F12 está aceptada; todavía no hay sondeo operativo de organismos ni datos oficiales publicados.

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

Para probar el arranque inicial, iniciar la API con `SyntheticData__Enabled=false` y `ColdStart__Enabled=true`. `GET /v1/status` devuelve `mode=collecting` y `officialDataAvailable=false`; el modo presenta solo la muestra y **no ejecuta un colector real**. Buscar `ejemplo` para ver la serie simulada de 14 días. El [worker INA opt-in](docs/runbooks/F4-INA-COLLECTION.md) ya está preparado, pero sigue apagado hasta registrar derechos y la selección exacta de la serie. La publicación requerirá después la revisión hidrológica. Los [contenedores de beta](DEPLOYMENT.md#contenedores-y-ambientes) permiten validar el empaquetado local, sin desplegar ni habilitar fuentes.

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
