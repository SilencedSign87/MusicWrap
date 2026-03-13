# Copilot Instructions

## Project Guidelines
- La mas alta prioridad el proyecto es minimizar el uso de recursos y maximizar la velocidad de la app.
- El codigo debe ser limpio, legible y mantenible, siguiendo las mejores prácticas de desarrollo.
- Prefiere evitar guardados innecesarios a disco; priorizar mantener estado en RAM y persistencia diferida para rendimiento.
- Para indexación, prefiere procesamiento por batches, notificación de progreso agrupada y minimizar overhead para maximizar velocidad.
- Evita operaciones de bloqueo o síncronas que puedan afectar la experiencia del usuario; prioriza asincronía y procesamiento en segundo plano.