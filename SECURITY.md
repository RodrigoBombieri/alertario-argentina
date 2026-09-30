# Seguridad y privacidad

## Acceso sin cuenta

Lecturas hidrológicas, búsqueda, mapa, fuentes y favoritos funcionan anónimamente. No necesitan JWT. Favoritos se guardan localmente. Push necesita una **instalación seudónima**, no nombre/email: token del dispositivo, reglas y credencial propia revocable. “Sin cuenta” no significa que el backend no trate identificadores.

Alta de instalación crea secreto aleatorio de alta entropía, guarda hash servidor y entrega una vez por TLS; móvil lo conserva en Keychain/Keystore mediante storage seguro. Credencial opaca con alcance exclusivo de su instalación, expiración propuesta 90 días renovable con credencial válida y rotación. Reinstalar sin credencial no permite recuperar reglas por conocer el token FCM. Revocar instalación y purgar tras inactividad según política. FCM token no es autenticación.

No implementar JWT/refresh tokens por reflejo. Si se incorporan cuentas, utilizar OIDC/OAuth con Authorization Code + PKCE y proveedor evaluado; access tokens cortos, refresh rotativos/revocables en almacén seguro, validar issuer/audience/exp/algoritmo. No diseñar autenticación de contraseñas casera. Admin: identidad fuerte, MFA y roles de operador/revisor; cada cambio de umbral necesita auditoría y revisión separada. No exponer admin con un secreto embebido en Flutter.

## Amenazas y controles

| Amenaza | Control | Prueba |
|---|---|---|
| Leer/editar reglas ajenas por ID | Ownership obligatorio en cada query y comando | Instalación A no accede a B |
| Alta masiva y spam push | Límites por IP/instalación, máximo propuesto 20 reglas, cuota de alta; app attestation solo si abuso justifica | Carga controlada y 429 |
| Robo token FCM/credencial | TLS, secreto hasheado, token push cifrado, rotación, no logs | Escaneo de logs y revocación |
| Datos malformados de fuente | Schema/semántica, límites de bytes y cuarentena | JSON extra, truncado, nulos y Unicode |
| XXE / expansión XML CAP | DTD y resolución de entidades externas deshabilitadas; máximo de tamaño/profundidad | Fixture malicioso sin salida de red |
| SSRF por next_page / enlaces CAP | Allowlist HTTPS de host/ruta, redirects validados, sin direcciones privadas | URL arbitraria rechazada |
| SQL injection / consulta costosa | EF/SQL parametrizado, límites de rango/bbox, timeout | Inputs hostiles y rangos extremos |
| Manipulación de umbral | Permiso interno, fuente obligatoria, versiones y audit log | Ningún endpoint móvil puede publicarlo |
| Cadena de suministro | Versiones fijadas, revisión de licencias/CVE, imágenes mínimas y no root | CI de dependencias y SBOM |
| Datos incorrectos tratados como oficiales | Modelo de procedencia y pruebas de texto/estado | Caso de cruce sin aviso |

CORS no es autenticación ni protege un backend contra clientes nativos. Configurarlo solo para orígenes web necesarios. Limitar tamaño de body, query y headers; evitar detalles de stack en respuestas públicas. Reintentos de mutaciones solo con idempotencia.

## Datos mínimos

GPS opt-in para consulta puntual; no solicitar background location ni conservar recorridos. En servidor persistir lugar/estación elegida, no domicilio. Logs excluyen lat/lon de consulta, tokens, headers Authorization y cuerpo de reglas de usuario. IP puede necesitarse transitoriamente para antiabuso; truncar/rotar y definir retención propuesta ≤7 días. Analytics de producto opcional o agregado, separado del consentimiento push.

Botón “Eliminar mis datos de este dispositivo y del servicio”: borrar favoritos/cache local, revocar instalación, token y reglas, suprimir eventos personales. Backups expiran según retención y un registro de supresiones se reaplica si se restaura. Borrado y exportación requieren credencial o mecanismo de identidad seguro, no revelar información por email sin verificar.

Propuesta: eventos 90 días, logs operativos 30 días, instalaciones sin actividad 180 días con token revocado; justificar cada retención antes de beta. Las observaciones hidrológicas no son datos personales del usuario, pero un favorito asociado a instalación puede revelar interés geográfico.

## Marco argentino y pendientes

La [Ley 25.326 vigente](https://www.argentina.gob.ar/normativa/nacional/64790/actualizacion) y la [AAIP](https://www.argentina.gob.ar/aaip/datospersonales) son referencias para finalidad, seguridad y derechos. Hosting/push pueden implicar transferencia internacional: evaluar ubicación y contratos conforme a la [guía AAIP](https://www.argentina.gob.ar/transferencias-internacionales), sin asumir que contratar una nube cumple automáticamente. Determinar responsable del tratamiento, inscripción aplicable, canal de derechos y encargados antes de beta pública. Es una lista de validaciones del proyecto, no una certificación legal.

Se necesitan política de privacidad, términos de servicio independientes y atribuciones de datos; indicar que no hay afiliación oficial ni servicio de emergencia garantizado. Esa comunicación no reemplaza ingeniería segura ni garantiza exención de responsabilidad. Publicar textos revisados profesionalmente antes del lanzamiento.

## Secretos y operación

Development con user-secrets/archivo local ignorado; CI con secretos protegidos y OIDC donde el proveedor lo admita; producción con secret manager. Nunca claves de proveedores en mobile salvo claves públicas restringidas de SDK que explícitamente lo requieran. Identidades DB separadas: app sin DDL, worker con escrituras específicas, migrador con DDL temporal. Backups cifrados, acceso mínimo, rotación y ensayo de recuperación. HTTPS extremo a extremo y validación de certificados; no solucionar errores TLS deshabilitando verificación.
