# ADR-004 — Datos, umbrales, cálculos y avisos separados

Estado: propuesta técnica; distinción de categorías es requisito confirmado. Fecha: 2026-09-29.

Contexto: un nivel por encima de una referencia no demuestra que exista alerta u orden de autoridad. Datos viejos y avisos vigentes pueden coexistir.

Decisión: estado multidimensional; umbral versionado con datum/vigencia; aviso con emisor/identidad/ciclo de vida. Tendencias determinísticas por ventana con insuficiencia explícita, sin IA ni interpolación v1.

Alternativas: semáforo único “NORMAL/ALERTA/EVACUACIÓN” inferido del nivel se rechaza por ambigüedad y falsa atribución. Porcentaje de altura se excluye por referencia arbitraria del cero.

Consecuencias: más precisión de lenguaje y algunos resultados no disponibles. Se prioriza honestidad frente a completar todas las cifras.

Validación: casos TR/NT y prueba ciudadana; parámetros por serie con especialista. Fuente de una eventual orden de evacuación debe incorporarse explícitamente, no derivarse de INA por umbral.
