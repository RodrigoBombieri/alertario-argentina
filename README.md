# AlertaRío Argentina

Aplicación ciudadana para consultar información hidrológica oficial, entender sus cambios y seguir avisos de autoridades en Argentina.

**Estado: F1 y F2 no aceptadas como fases completas.** La evaluación exploratoria de F1 terminó con resultado NO-GO; sus [faltantes por motivos externos y pasos de resolución](docs/research/f1/CLOSURE.md#faltantes-por-motivos-externos) están registrados. F2 tiene un [backend sintético de revisión](docs/implementation/F2.md), sin infraestructura desplegada. Las consultas puntuales a fuentes no certifican disponibilidad continua ni cierran los permisos pendientes de INA/SMN.

F3 comenzó con [normalización aislada y fixtures sintéticas](docs/implementation/F3.md); todavía no hay adapters operativos ni datos oficiales publicados.

## Empezar aquí

1. [PLAN MAESTRO DE DESARROLLO — ALERTARÍO](PLAN-MAESTRO.md).
2. [Fuentes y verificaciones](DATA-SOURCES.md), [contratos e integraciones](API-INTEGRATIONS.md), [evidencia inicial](docs/research/VERIFICATION.md) y [evaluación F1](docs/research/f1/CLOSURE.md).
3. [Producto](PRODUCT.md), [arquitectura](ARCHITECTURE.md) y [dominio](DOMAIN.md).
4. [Roadmap](ROADMAP.md), [backlog](BACKLOG.md) y [decisiones](DECISIONS.md).

Para probar el backend sintético: `dotnet test backend/AlertaRio.sln --no-restore` después de restaurar dependencias según [F2](docs/implementation/F2.md).

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

## Condiciones para comenzar el desarrollo

La evaluación acotada de F1 concluyó con resultado NO-GO; la fase F1 no está aceptada. Para habilitar datos oficiales siguen pendientes permisos, cadencias por serie, correspondencia estación/serie/datum y descubrimiento estable de alertas SMN. El contrato propio y los componentes sintéticos existentes en F2 son material de revisión, sin aprobación de fase ni suposiciones sobre esas propiedades.

El stack recomendado es Flutter + Riverpod, ASP.NET Core 10, PostgreSQL + PostGIS y un worker del mismo backend. Redis, TimescaleDB, microservicios, cuentas ciudadanas y predicción quedan fuera del arranque. El hosting todavía no está elegido.

## Reglas permanentes

- Datos medidos, avisos oficiales, cálculos de AlertaRío y pronósticos son categorías distintas.
- Un umbral superado no es una orden de evacuación ni una alerta emitida.
- El celular consume únicamente la API propia para datos hidrológicos, meteorológicos y geográficos.
- Un dato faltante nunca vale cero. Una fuente inaccesible nunca significa ausencia de alertas.
- Todo número visible conserva origen, unidad, hora de medición y calidad.
- Las capturas de investigación son históricas: no utilizarlas como información vigente.

No se declara una licencia propia ni se relicencian los datos externos en esta etapa. Resolverlo antes de publicar el repositorio o redistribuir sus muestras.
