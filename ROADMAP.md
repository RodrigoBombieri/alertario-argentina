# Estado y próximos pasos

**F1–F12 cerradas técnicamente para Android D18/D19.** [Alcance y evidencia](docs/implementation/TECHNICAL-CLOSURE.md). La beta pública y F13 (producción) no se ejecutaron.

| Paso pendiente | Resultado esperado |
|---|---|
| Compatibilidad de Google Play | Actualizar el objetivo Android de API 35 a 36 o superior y verificar bibliotecas nativas con páginas de 16 KB |
| Preparación de publicación | AAB firmado, política de privacidad pública y accesible en la app, ficha de tienda y pruebas de distribución |
| Operación | API HTTPS, responsable, dominio, monitor y backup externo; presupuesto de referencia US$100/mes |
| Activación de datos | Derechos por fuente, catálogo y series revisadas; recolección y publicación activadas por separado |
| Prueba con usuarios | Android físico, TalkBack, batería/red y comprensión de datos ficticios/reales |

Los dos primeros pasos incluyen trabajo técnico de publicación: **no son faltantes exclusivamente externos**. Procedimiento en [Google Play](docs/PLAY-STORE.md).

Los [faltantes externos EXT-01–07](docs/implementation/TECHNICAL-CLOSURE.md#faltantes-por-motivos-externos-y-resolución) conservan responsables y resolución. No hace falta esperar 14 días para abrir la demo; la validación hidrológica de datos reales y las condiciones de pruebas de Google Play son procesos distintos.

Ampliaciones sin compromiso de entrega: iOS, feed SMN integrado, push remoto, tiles contratados, agregación y mayor cobertura.
