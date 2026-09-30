# ADR-003 — Flutter feature-first con Riverpod

Estado: propuesto, sujeto a spike. Fecha: 2026-09-29.

Contexto: Android/iOS, estados async y offline, lectura accesible y gráficos.

Decisión: Flutter estable fijado, Riverpod, Dio, modelos inmutables/json_serializable y Freezed donde ahorre trabajo; Drift/SQLite para cache/favoritos. UI y datos separadas por feature; dominio local solo cuando tenga lógica real.

Alternativas: Bloc/Cubit válido con equipo experto; React Native si experiencia TS domina; nativo duplica trabajo para alcance actual. No existe necesidad de clean architecture ceremonial de muchas clases por pantalla.

Consecuencias: toolchain Dart y pruebas nativas para plugins; macOS/signing necesarios para iOS. Mobile nunca decide aviso oficial a partir de altura.

Validación: gráficos con huecos/lector pantalla, MapLibre, push y compilación iOS temprana. No fijar versiones de paquetes no ensayadas en documentación como si fueran lockfile.
