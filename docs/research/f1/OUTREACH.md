# Consultas preparadas para organismos — F1

**Borradores del 1 de octubre de 2026. No enviados.** Revisar identidad del remitente, destinatario institucional y alcance del proyecto antes del envío. Contexto: [F1](README.md).

## INA / red de escalas PNA difundida por A5

Canales oficiales de referencia: [INA](https://www.argentina.gob.ar/ina), [catálogo hidrológico](https://www.argentina.gob.ar/ina/recursos/catalogo-informacion-hidrologica).

**Asunto propuesto:** Consulta técnica y de reutilización de datos A5 para piloto ciudadano AlertaRío

> Estamos evaluando un piloto ciudadano en localidades de la Cuenca del Plata. La aplicación mostraría altura/caudal con estación, serie, unidad, hora de medición y procedencia, y distinguiría datos medidos de cálculos propios y avisos oficiales. Aún no publicamos datos de INA.
>
> ¿Qué condiciones rigen para consultar periódicamente, almacenar, mostrar, redistribuir y conservar historia de las series públicas de la red `alturas_prefe` con procedencia PNA a través de A5? ¿Debe intervenir o autorizar también PNA? Agradeceríamos licencia/permiso, texto de atribución, restricciones por red, retención, uso comercial eventual y contacto responsable.
>
> Para diseñar una prueba de 14 días de baja carga, ¿qué cuota y frecuencia recomiendan por estación/serie? La propuesta inicial es cuatro series de altura, una consulta diaria por serie sobre ventana de tres días, sin pruebas de carga. ¿Existe un endpoint agregado recomendado?
>
> Para las series candidatas, ¿cómo se definen zona horaria de `date_range` sin offset, hora/intervalo de medición, semántica de `timeupdate`, unidad cuando `unit_id` es nulo, datum y cambios de escala? ¿Hay documentación de vigencia, autoridad, unidad y datum de `nivel_alerta`/`nivel_evacuacion`? ¿Los valores en tiempo real tienen marcas de calidad o revisión posterior?

## SMN / CAP y avisos zonales

Canales oficiales de referencia: [SMN](https://www.argentina.gob.ar/node/223486), [índice CAP](https://ssl.smn.gob.ar/CAP/AR.php), [SAT](https://ws2.smn.gob.ar/alertas).

**Asunto propuesto:** Consulta sobre feed CAP oficial para alertas zonales en AlertaRío

> Estamos diseñando una aplicación ciudadana que mostraría avisos meteorológicos oficiales del SMN por localidad, conservando texto, emisor, vigencia y enlace original. El índice CAP público que encontramos devuelve HTML y enlaza archivos XML individuales; necesitamos confirmar el canal de integración adecuado antes de implementarlo.
>
> ¿Existe una URL documentada y estable de feed machine-readable para consultar el conjunto vigente y sus cambios? ¿Cubre SAT, advertencias y avisos a muy corto plazo, o hay canales separados? ¿Cómo se comunican Alert, Update y Cancel, y qué significa una respuesta vacía o parcial? Agradeceríamos ejemplos de cada transición, geometrías/áreas, retención y mecanismo de reconciliación tras una caída.
>
> ¿Qué licencia y atribución corresponden al consumo automático, almacenamiento, historial y redistribución de CAP? ¿Qué frecuencia/cuota de consulta permiten? ¿Existe un perfil oficial que relacione `severity` u otros campos CAP con colores y categorías del SAT argentino?

Registrar fecha, persona/área, respuesta textual o enlace verificable y decisión resultante en la ficha F1 antes de cambiar estados a `approved`.
