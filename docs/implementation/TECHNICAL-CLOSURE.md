# Cierre técnico F1–F12 — 5/10/2026

**F1–F12 cerradas para el alcance Android D18/D19.** Termina el trabajo interno de ese corte, con alternativas explícitas. No acredita fuentes habilitadas, beta pública, entrega puntual de avisos ni producción. F13 no se inició.

El titular autorizó ajustar criterios y buscar soluciones simples que conserven consulta hidrológica, datos trazables, estados honestos, favoritos/offline, exploración espacial, histórico, seguimiento opt-in y recuperación.

## Criterios cumplidos

| Fase | Cierre técnico y evidencia |
|---|---|
| **F1 · Cerrada** | Investigación concluida con NO-GO para fuentes no habilitadas; sondas, inventario y EXT-01–05 trazables. No equivale a autorización |
| **F2 · Cerrada** | API v1, OpenAPI, errores y CI; 85 pruebas de contrato, demo/publicación separadas |
| **F3 · Cerrada** | INA/GeoRef: clientes acotados, selección exacta, normalización, paginación y fallos; workers opt-in. SMN manual hasta resolver feed |
| **F4 · Cerrada** | 12 migraciones, smokes SQL, 13 pruebas PostgreSQL/PostGIS; escritor idempotente, lease/checkpoint, cuarentena, GeoRef atómico y restore aislado |
| **F5 · Cerrada** | Ventanas, frescura, calidad y umbrales; seguimiento opcional por política aprobada en 0012; summary transaccional y publicación filtrada por derechos/asociación |
| **F6 · Cerrada** | Android 8+: búsqueda/ficha/gráfico, favoritos/caché, texto 200%, etiquetas/tamaños de controles, flujo en emulador; ID e icono propios; release exige firma privada |
| **F7 · Cerrada** | Bbox, filtros y lista accesible; MapLibre opcional, sin dependencia de tiles. Clusters no requeridos para piloto de 10–30 estaciones |
| **F8 · Cerrada** | JobScheduler Android, API propia, permisos, reglas por estación, episodios, doble lectura, TTL e historial local; política Java y prueba nativa. Sin tokens, FCM/APNs ni dispatcher activo |
| **F9 · Cerrada** | Histórico crudo paginado de hasta 30 días, huecos/correcciones/procedencia visibles. Muestra de 14 días separada, sin backfill anterior al inicio |
| **F10 · Cerrada** | Contratos, DB, migraciones desde cero, restore, Flutter y emulador; casos de fallos, permisos, replay, revisiones, revocación y salud |
| **F11 · Cerrada** | Health API/DB/esquema/worker/datos, heartbeat con retención, métricas/logs, monitor JSON con cooldown, backup y runbooks; restore local |
| **F12 · Cerrada** | Paquete técnico Android instalable, Dockerfiles/Compose, firma release, runbooks y registro de aceptación. Activación ciudadana pública separada |

## Alternativas y límites

- Los avisos son seguimiento periódico calculado en Android. Pueden demorarse o perderse por batería, red o crash; no son servicio de emergencia. La primera consulta establece referencia sin avisar sobre episodios anteriores. El servidor determina calidad/frescura/condición; Android decide entrega y conserva episodios. [Detalles](../runbooks/F8-ANDROID-NOTICES.md).
- SMN: acceso al sitio oficial y cobertura no confirmada. No hay integración CAP ni avisos zonales automáticos; el enlace no equivale a cobertura oficial del diseño original. EXT-02 conserva ese gate.
- No se habilitó ni consultó ninguna fuente real durante este cierre. Los 14 días de muestra son ficticios; los datos reales y sus políticas requieren derechos/revisión por serie. No hay espera para instalar la demo.
- Android 8+ es el corte. iOS, push con proveedor, tiles, agregaciones y escalado corresponden a ampliaciones. No se afirma capacidad de hosting, SLA ni comprensión de usuarios a partir de fixtures.
- Los scripts detectan problemas; un operador debe conectarlos a su supervisión. No se simuló guardia, contratación, copia externa, beta pública ni aceptación legal.

## Faltantes por motivos externos y resolución

Son recursos, validaciones o decisiones **fuera del repositorio**. Algunos corresponden al titular; otros dependen de organismos o especialistas.

| Código | Falta y cómo resolver | Alternativa vigente |
|---|---|---|
| EXT-01 · INA/PNA | Integraciones/titular documenta términos por red o consulta al organismo; [borrador listo](../research/f1/OUTREACH.md) | Muestra explícita; worker apagado |
| EXT-02 · SMN | Integraciones verifica feed completo, Alert/Update/Cancel y términos; licencia abierta aplicable basta sin permiso individual | Consulta manual del sitio oficial; cobertura no confirmada |
| EXT-03/04 · Hidrología | Especialista/titular revisa datos reales desde activación, datum, cadencia/retraso/ruido, umbrales y asociaciones; registra políticas | Sin cálculos/referencias reales aprobados |
| EXT-05 · GeoRef | Titular verifica aplicación de CC BY 4.0 y atribución, registra decisión/fuente y activa catálogo según runbook | Importador probado y apagado |
| EXT-06 · Distribución | Titular aporta Android físico/usuarios/TalkBack, custodia firma y eventual cuenta de tienda; ejecuta registro beta | APK debug y emulador; iOS fuera de corte |
| EXT-07 · Operación | Titular confirma hosting dentro de US$100/mes, dominio/contacto/responsable; operador despliega Compose, conecta monitor, prueba capacidad/restore/copia externa y privacidad | Demo local sin gasto; paquete listo |

Este cierre corresponde al corte local D19. La revisión documental del 5/10/2026 identifica trabajo técnico adicional para Google Play: objetivo API 36, verificación de bibliotecas con páginas de 16 KB y política de privacidad accesible en la app. Ver [preparación de tienda](../PLAY-STORE.md). No se clasifica ese trabajo como externo ni como ya cumplido.

## Procedimientos

[GeoRef](../runbooks/F4-GEOREF-CATALOG.md), [INA](../runbooks/F4-INA-COLLECTION.md), [publicación](../runbooks/F5-PUBLICATION.md), [avisos Android](../runbooks/F8-ANDROID-NOTICES.md), [operación](../runbooks/F11-OPERATIONS.md), [entrega y beta](../runbooks/F12-RELEASE.md).


[Resultados de verificación](VALIDATION-2026-10-05.md).
