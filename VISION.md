# Visión

AlertaRío busca reducir el esfuerzo necesario para interpretar información oficial sobre ríos. Una persona debería identificar localidad, estación consultada, nivel, cambio y antigüedad en menos de diez segundos, sin saber leer un portal hidrométrico.

## Problema y usuarios

Los datos relevantes pertenecen a organismos y redes diferentes. Sus unidades, referencias verticales, frecuencias y cobertura no son homogéneas. La cercanía geográfica no garantiza representatividad hidrológica. Un gráfico convincente puede producir una interpretación equivocada si oculta estas diferencias.

El público inicial son residentes de localidades ribereñas y personas que siguen la situación de familiares o propiedades. Campings, clubes, productores y pequeños operadores son usuarios secundarios de consulta. Municipios y Defensa Civil son futuros usuarios profesionales; el MVP no constituye un sistema de comando de emergencias.

## Propuesta de valor

Información oficial reunida alrededor de una localidad, con estación explícita, variaciones comprensibles, trazabilidad y límites visibles. La diferenciación propuesta consiste en explicar qué se conoce y con qué antigüedad, separar avisos emitidos de cálculos propios y funcionar parcialmente sin conexión. Las hipótesis competitivas están en [COMPETITORS](docs/research/COMPETITORS.md); no se afirma exclusividad de funciones.

La ambición es nacional; el lanzamiento debe cubrir un conjunto pequeño de estaciones verificadas en la Cuenca del Plata. Buscar una localidad de otra región será posible, pero no se prometerán datos que no existan. Propuesta inicial: 10–30 estaciones y 3–5 localidades piloto, sujetas a fase 1; no es un recuento actual de cobertura.

## Principios de confianza

1. Fidelidad antes que sensación de tiempo real.
2. La estación y su escala son la referencia, no la vivienda del usuario.
3. La ausencia de aviso confirmado no demuestra seguridad.
4. El usuario puede consultar sin registrarse y sin compartir GPS.
5. No se venden promesas de evacuación o protección garantizada.

## Validación de valor

Probar con 8–12 personas no técnicas de localidades piloto: objetivo propuesto de al menos 80% identificando nivel, tendencia temporal y antigüedad en diez segundos; ninguna debe interpretar el cruce de umbral como una orden emitida por la app. Medir tareas y entrevistas con consentimiento, sin seguimiento individual permanente. Repetir tras corregir problemas de comprensión.

Métricas de producto: tiempo hasta primera estación útil, consultas repetidas voluntarias, favoritos usados, comprensión de dato viejo y desactivación de notificaciones. No optimizar cantidad de pushes.

## Negocio futuro

| Segmento | Hipótesis a validar | Condición |
|---|---|---|
| B2C | Históricos avanzados, comparaciones, exportaciones, múltiples reglas | MVP gratuito; no ocultar procedencia ni avisos oficiales por un pago |
| B2B | Monitoreo de varios sitios, reportes para campings, clubes y productores | Entrevistas y disposición a pagar antes de desarrollar |
| B2G | Paneles, auditoría, integración API y seguimiento institucional | Convenios, responsabilidades y SLA propios separados de los proveedores |

No agregar pagos, multitenancy ni módulos empresariales en el MVP. La API versionada y los identificadores internos facilitan evolución sin preconstruir ese producto.

## IA

Variaciones, tendencias, cruce de umbrales, geofencing, deduplicación y textos básicos se resuelven con algoritmos y plantillas. No se necesita IA en el MVP. En una etapa futura, un resumen o consulta en lenguaje natural podría utilizar exclusivamente datos estructurados recuperados, citar sus fuentes y abstenerse cuando falten. Los números se insertarían desde objetos verificados, sin generación libre. Prohibidas recomendaciones de evacuación generadas y alertas inventadas; cualquier pronóstico requerirá validación científica propia y etiquetado separado.
