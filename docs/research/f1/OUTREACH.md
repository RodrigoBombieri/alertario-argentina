# Consultas preparadas para organismos — F1

**Borradores del 1 de octubre de 2026, con canales verificados el 4 de octubre. No enviados.** Completar nombre, correo y condición del remitente; no afirmar que una solicitud ya fue aprobada. Contexto: [F1](README.md).

## Cómo presentarlos y guardar la respuesta

1. Para pedir **documentos ya existentes** (licencias, condiciones, manuales, convenios, especificaciones), usar el trámite gratuito [Solicitar información pública](https://www.argentina.gob.ar/solicitar-informacion-publica): ingresar a TAD, buscar «Acceso a la Información Pública», elegir el organismo y adjuntar la nota. Puede presentarlo una persona física; no exige explicar el motivo ni patrocinio letrado. El plazo informado es 15 días hábiles, prorrogable excepcionalmente por otros 15. Guardar número de expediente, fecha y respuesta.
2. En paralelo, enviar preguntas técnicas a la mesa de entradas. Para **INA/PNA**, pedir también condiciones de uso de la red concreta cuando no haya una licencia publicada. Para **SMN CAP**, preguntar por la licencia o términos aplicables y el contrato técnico del feed: si una licencia abierta ya cubre ese recurso y uso, no hace falta solicitar un permiso individual. TAD sirve para pedir información que ya existe; no asumir que tramita una licencia nueva.
3. Para INA: [Mesa de Entradas del INA](https://www.argentina.gob.ar/ina/transparencia/gestion-de-la-informacion), `ina@ina.gob.ar`. Para SMN: [transparencia del SMN](https://www.argentina.gob.ar/smn/transparencia-activa), `mesa@smn.gob.ar`; publica `lsromero@smn.gob.ar` como responsable de solicitudes de información. Si INA remite la autorización de datos de Prefectura a PNA, presentar la pregunta específica ante [PNA](https://www.argentina.gob.ar/node/233651), `sgen-div.acc.infor.publica@prefecturanaval.gob.ar`, o por TAD.
4. Registrar texto íntegro, fecha, remitente, alcance, condiciones, cuota y enlace/expediente en la ficha F1. Para SMN, una licencia pública verificable puede ser la evidencia de uso, sin respuesta individual. La activación de avisos exige además demostrar que el feed vigente cubre el alcance prometido y sus cambios/cancelaciones. La allowlist hidrológica requiere una revisión separada.

Los textos de abajo están listos para copiar en una nota o correo. Antes de enviarlos, completar `[Nombre y apellido]`, `[Correo de respuesta]`, `[Persona o entidad titular del proyecto]` y, si existe, URL pública del proyecto. Una solicitud informativa puede presentarse a título personal; para celebrar acuerdos o asumir obligaciones por una entidad debe actuar su representante.

## INA / red de escalas PNA difundida por A5

Canales oficiales de referencia: [INA](https://www.argentina.gob.ar/ina), [catálogo hidrológico](https://www.argentina.gob.ar/ina/recursos/catalogo-informacion-hidrologica).

**Asunto propuesto:** Consulta técnica y de reutilización de datos A5 para piloto ciudadano AlertaRío

> Soy [Nombre y apellido], [persona o representante de entidad] responsable de AlertaRío Argentina. Mi correo de respuesta es [Correo de respuesta]. Estamos evaluando un piloto ciudadano en localidades de la Cuenca del Plata. La aplicación mostraría altura/caudal con estación, serie, unidad, hora de medición y procedencia, y distinguiría datos medidos de cálculos propios y avisos oficiales. Aún no publicamos datos de INA.
>
> ¿Qué condiciones rigen para consultar periódicamente, almacenar, mostrar, redistribuir y conservar historia de las series públicas de la red `alturas_prefe` con procedencia PNA a través de A5? ¿Debe intervenir o autorizar también PNA? Agradeceríamos licencia/permiso, texto de atribución, restricciones por red, retención, uso comercial eventual y contacto responsable.
>
> Para diseñar una prueba de 14 días de baja carga, ¿qué cuota y frecuencia recomiendan por estación/serie? La propuesta inicial es cuatro series de altura, una consulta diaria por serie sobre ventana de tres días, sin pruebas de carga. ¿Existe un endpoint agregado recomendado?
>
> Para las series candidatas, ¿cómo se definen zona horaria de `date_range` sin offset, hora/intervalo de medición, semántica de `timeupdate`, unidad cuando `unit_id` es nulo, datum y cambios de escala? ¿Hay documentación de vigencia, autoridad, unidad y datum de `nivel_alerta`/`nivel_evacuacion`? ¿Los valores en tiempo real tienen marcas de calidad o revisión posterior?
>
> Agradeceré una respuesta escrita con enlaces a documentos vigentes y, si otra área u organismo debe autorizar el uso, la derivación correspondiente. Por favor indiquen un número de expediente o referencia para seguimiento.

## SMN / CAP y avisos zonales — aclaración técnica y de términos

Canales oficiales de referencia: [SMN](https://www.argentina.gob.ar/node/223486), [índice CAP](https://ssl.smn.gob.ar/CAP/AR.php), [SAT](https://ws2.smn.gob.ar/alertas).

**Asunto propuesto:** Consulta sobre feed CAP oficial para alertas zonales en AlertaRío

> Soy [Nombre y apellido], [persona o representante de entidad] responsable de AlertaRío Argentina. Mi correo de respuesta es [Correo de respuesta]. Estamos diseñando una aplicación ciudadana que mostraría avisos meteorológicos oficiales del SMN por localidad, conservando texto, emisor, vigencia y enlace original. El índice CAP público que encontramos devuelve HTML y enlaza archivos XML individuales; necesitamos confirmar el canal de integración adecuado antes de implementarlo.
>
> ¿Existe una URL documentada y estable de feed machine-readable para consultar el conjunto vigente y sus cambios? ¿Cubre SAT, advertencias y avisos a muy corto plazo, o hay canales separados? ¿Cómo se comunican Alert, Update y Cancel, y qué significa una respuesta vacía o parcial? Agradeceríamos ejemplos de cada transición, geometrías/áreas, retención y mecanismo de reconciliación tras una caída.
>
> ¿Qué licencia o términos públicos se aplican a este recurso CAP para consumo automático, almacenamiento, historial y redistribución? Si el feed ya está cubierto por una licencia abierta, agradeceré el enlace; no solicito una autorización individual innecesaria. ¿Qué frecuencia/cuota de consulta recomiendan? ¿Existe un perfil oficial que relacione `severity` u otros campos CAP con colores y categorías del SAT argentino?
>
> Agradeceré una respuesta escrita con documentación vigente, condiciones por tipo de aviso y un contacto técnico. Por favor indiquen un número de expediente o referencia para seguimiento.

Registrar fecha, persona/área, respuesta textual o enlace verificable y decisión resultante en la ficha F1 antes de cambiar estados a `approved`.

## Aprobación hidrológica y geoespacial del piloto

No es un permiso que se tramite ante INA o SMN por defecto. Es una revisión técnica del proyecto por una persona con competencia hidrológica y otra geoespacial. Si algún organismo acepta revisar, conservar el alcance preciso de su respuesta; una aclaración de datos no implica aval del producto.

**Pedido para un/a especialista:** «Solicito revisar la [matriz por serie](README.md), el informe de 14 días y las relaciones localidad–estación candidatas de AlertaRío. Para cada serie, confirmar o rechazar unidad, datum/época, cadencia, retraso tolerable, ventanas de tendencia y vigencia/autoridad de umbrales. Para cada localidad, confirmar o rechazar la estación representativa y documentar límites de cobertura. Registrar nombre, especialidad, fecha, versión de los datos revisados, observaciones y conclusión por elemento. Ninguna observación se presentará como orden de evacuación.»

La revisión se solicita **después** de contar con respuesta INA/PNA y 14 días de muestreo permitido. Hasta entonces, la matriz permanece candidata y los umbrales deshabilitados.
