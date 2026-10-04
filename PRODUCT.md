# Producto y UX

## MVP y alcance geográfico

MVP gratuito, consulta sin cuenta, Android e iOS como objetivo. El titular confirmó Android primero para la beta; la aceptación de iOS se verifica separadamente y necesita runner/macOS y dispositivos. Piloto de localidades de la Cuenca del Plata con series aprobadas; no prometer cobertura nacional uniforme. El país es el alcance de búsqueda, no una garantía de sensores en todas las localidades.

D14 permite abrir la app antes de disponer de observaciones oficiales. D15 muestra una estación ficticia con 14 días simulados y una etiqueta persistente de muestra; no infiere seguridad ni cobertura de avisos. Los 14 días de datos reales se reúnen por separado después de habilitar cada fuente. La muestra no cumple por sí sola el MVP de consulta hidrológica y avisos.

| MUST | SHOULD | COULD | NOT NOW |
|---|---|---|---|
| Buscar localidad y elegir estación cercana explícita | Mapa básico con filtros y clusters | Widgets | Predicción propia |
| Favoritos de localidad/estación locales | ACP, si se verifica feed específico | Exportar gráfico accesible | Riesgo por domicilio |
| Altura, caudal cuando disponible, fuente/unidad/tiempo | Históricos >30 días según disponibilidad | Comparación manual entre estaciones sin unificar escalas | Lluvia aguas arriba y modelado de cuencas |
| Variaciones 1/3/6/12/24 h o insuficiencia explicada | Información de cambios desde última consulta | Cuentas opcionales | Dashboard B2B/B2G y monetización |
| Tendencia con ventana y calidad | Ajustes avanzados de notificaciones | Recordatorio local para volver a consultar | IA, radar interactivo y AIS |
| Umbrales oficiales verificados, o “no disponible” | Caudal en gráfico avanzado | Modo alto contraste extra | Recomendaciones de evacuación propias |
| Avisos meteorológicos oficiales por zona; gate de feed | Ampliación a más cuencas con fuentes verificadas | Compartir enlace a fuente | Sensores ciudadanos sin validación |
| Push configurable, deduplicado; historial in-app |  |  |  |
| Gráfico reciente y offline parcial |  |  |  |

Mapa fase 7 e históricos extendidos fase 9 se diseñan ahora. Si el presupuesto obliga a un MVP de lista, se pueden posponer como SHOULD sin omitir altura, gráfico reciente ni alertas. No llamar completo al MVP si faltan los MUST de fuentes/avisos: una beta reducida debe comunicar esa limitación.

## Navegación y pantallas

Navegación inferior: **Inicio · Mapa · Avisos · Favoritos**. Búsqueda visible desde Inicio/Favoritos/Mapa. Ajustes y Fuentes accesibles desde menú. Histórico y vista avanzada pertenecen al detalle; no ocho pestañas competidoras.

| Pantalla | Información / acción principal | Casos vacíos o degradados |
|---|---|---|
| Inicio | Localidad, estación y río; nivel grande, variación, ventana, hora; tarjeta oficial separada | Elegir localidad; sin estación representativa; cache vieja |
| Buscar localidad | Nombre + provincia + categoría; selector de coincidencias; GPS opcional | Homónimos, sin resultados, GPS denegado, sin conexión |
| Detalle estación/río | Nombre exacto, nivel, tendencia, gráfico, umbrales, procedencia | Series múltiples, umbral desconocido, dato sospechoso |
| Mapa | Estaciones y clustering, filtros río/provincia, “más cercana” | Permiso denegado, tiles caídos, estaciones sin coordenada |
| Histórico | 24 h / 7 d / 30 d y rango disponible; ventanas seleccionables | Huecos, rango sin datos; no unir líneas artificialmente |
| Avisos | Emisor, fenómeno, vigencia, ámbito, texto oficial y fuente | Feed no verificable, expirados archivados, geografía incierta |
| Favoritos | Localidades y estaciones diferenciadas; ordenar/eliminar | Primer favorito; estación retirada conserva acceso histórico |
| Configuración | Reglas, umbrales personales, ventanas, permisos, quiet hours, borrar instalación | Cambios pendientes offline; push deshabilitado |
| Fuentes/metodología | Origen por dato, escala, cálculo, limitaciones, licencias y contacto | Fuente no validada o permiso en revisión |

## Jerarquía visual de Inicio

Wireframe conceptual; **valores ficticios**, no reporte actual:

```text
Concordia · Entre Ríos                 Buscar
Río Uruguay · Estación [nombre exacto]
[Tarjeta de aviso oficial vigente, si existe]

7,48 m
+16 cm en 6 h · Creciente
Medido el [fecha/hora] · hace [edad]
[Sin conexión / Datos desactualizados, si aplica]

[Gráfico 24 h, líneas cortadas en huecos]
Umbral oficial de alerta     [valor verificado / no disponible]
Umbral de evacuación         [valor verificado / no disponible]
Fuente [organismo original] · distribuido por [INA]

Ver detalle           Configurar notificación
```

No mostrar caudal, 5 variaciones, porcentajes y todas las fechas a la vez: variación principal y detalle desplegable. La vista avanzada incluye soporte temporal, datum si conocido, quality flags, tiempos de publicación/ingesta y series alternativas. Toda comparación tiene unidad visible.

La app no dirá “se registraron lluvias aguas arriba” en MVP: falta delimitación de aporte y fuente de precipitación. Si más adelante se agrega, la frase debe provenir de datos medidos/estimados etiquetados y cuenca validada, no de que llueva cerca.

## Lenguaje y accesibilidad

“Altura del río” con ayuda “respecto de la escala de esta estación; no es profundidad”. “Creciente en 6 h” describe cambio de nivel, no dirección de corriente. “Umbral oficial superado” indica un cálculo con una referencia; “Alerta oficial” requiere aviso emitido. No utilizar “estás a salvo”.

Español argentino, separador decimal coma en UI, metros y centímetros claros, fecha absoluta accesible además de relativa. Objetivo de contraste WCAG AA, tamaños táctiles adecuados y texto escalable; color siempre acompañado de forma/icono/texto. Gráfico con resumen textual/tabla accesible para lector de pantalla. Probar TalkBack y VoiceOver y fuentes grandes. No depender de gestos complejos para consultar valor.

## Estados de mapa

Azul/círculo: dato válido sin condición calculada destacada, leyenda aclara que no certifica seguridad. Ámbar/triángulo: seguimiento calculado o umbral cruzado, etiqueta explícita. Aviso oficial: insignia diferenciada con emisor y fenómeno; color oficial solo si el perfil fuente lo permite. Gris/hueco: sin datos; gris/reloj: desactualizado. Estación con aviso y nivel viejo conserva ambos símbolos.

Clustering por zoom para legibilidad; contador y presencia de avisos, no promedio de niveles ni color que convierta todo el cluster en área de peligro. Al tocar, zoom/lista. API por bbox y límite; selección del río persiste entre lista y mapa. Si fallan tiles, lista y datos siguen disponibles. No dibujar zonas de inundación inferidas alrededor de estaciones.

## Criterios transversales de aceptación

- Given un dato importado hace 1 min pero medido hace 30 h, When se abre Inicio, Then ambas fechas no se confunden y se aplica la política de esa serie.
- Given serie diaria, When se pide Δ6 h, Then informa insuficiencia; no aproxima con Δ24 h bajo etiqueta 6 h.
- Given nivel por encima de umbral y ningún aviso emitido, Then no aparece ALERTA OFICIAL ni orden de evacuación.
- Given SMN caído, Then no se muestra “sin alertas” como resultado confirmado.
- Given dos estaciones cercanas en cursos distintos, Then usuario puede ver río/distancia y no recibe asociación automática de riesgo.
- Given GPS denegado y sin cuenta, Then búsqueda/favoritos/consulta siguen funcionando.
- Given sin Internet, Then último gráfico muestra cache y tiempos; no muestra nuevas mediciones.
- Given aviso CAP cancelado, Then deja de estar vigente y no se despacha un push pendiente.

## Definition of Done de una feature

Historia y criterios aprobados; contrato propio documentado; casos de nulos/error/offline probados; fuente y licencia registradas cuando corresponda; logs sin identificadores personales; revisión de textos oficiales/calculados; migración reversible operativamente; evidencia de tests; manual actualizado. “Funciona con una API en vivo” no sustituye pruebas determinísticas.
