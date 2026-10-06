# Cálculos y seguimiento

Implementación de referencia: [TrendEngine](../../backend/src/AlertaRio.Core/Trends/TrendEngine.cs). No usa IA, interpolación ni predicción.

## Tendencias

- Usa la última revisión de cada instante y solo mediciones aceptadas no futuras.
- Compara la última observación con 1, 3, 6, 12 y 24 horas antes.
- Busca referencia dentro de `min(ventana/10, cadencia/2)`; sin referencia informa insuficiencia.
- Exige unidad, datum, época y procedimiento compatibles en el tramo.
- Frescura depende de cadencia + retraso permitido. Sin política queda desconocida.
- Clasifica suba/baja/estable con epsilon aprobado. Prioriza ventana disponible de 6, luego 12 y 24 horas.
- Identifica huecos internos mayores a dos cadencias; el gráfico corta trazos mayores a 1,5 cadencias.

## Estado

Calidad, frescura, condición calculada y cobertura oficial son ejes distintos. Los umbrales requieren fuente, vigencia y referencia compatibles. Una superación no crea una orden de evacuación.

## Avisos Android

La API calcula la condición; Android decide el aviso local. Primera consulta sin aviso, nueva observación para abrir episodio, doble lectura antes de mostrar y caducidad. Datos sintéticos, viejos o no aptos no disparan avisos.

La especificación de entrega y sus límites está en [avisos Android](../runbooks/F8-ANDROID-NOTICES.md). El motor/outbox del servidor permanece inactivo.

[Pruebas de contrato](../../backend/tests/AlertaRio.ContractTests) · [Dominio](../../DOMAIN.md)
