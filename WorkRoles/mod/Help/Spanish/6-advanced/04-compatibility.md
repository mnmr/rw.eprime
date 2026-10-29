---
title: Compatibilidad con mods
---
<!-- l10n:b-6110204fbb19 -->
El trabajo de otros mods se detecta automáticamente. Algunas integraciones son más profundas: las pericias de Vanilla Skills Expanded y Alpha Skills alimentan las señales; el trabajo odiado de More Than Capable produce señal Pésima; Colony Groups añade agrupación; los mods de varios pisos tratan las plantas como una ubicación.

<!-- l10n:b-c7669aaaba7d -->
Los mods opcionales de trabajo y habilidades pueden aportar perfiles que identifican las habilidades usadas y entrenadas, y ajustar la principal para recomendaciones cuando corresponda. WorkRoles deja elegibilidad, designaciones y ejecución al mod de origen. No añade dependencias obligatorias.

<!-- l10n:b-27d73501db9c -->
Las recomendaciones leen etiquetas laborales, capacidades exigidas, oposición de preceptos y negativas verificadas a la minería normal. Con versiones auditadas de Humanoid Alien Races en RimWorld 1.6 y 1.7 usan sus restricciones reales de proveedores y recetas; todas las razas que las emplean se benefician. Una receta bloqueada no elimina otras utilizables del rol ni cuenta como vía de formación.

<!-- l10n:b-15011508cb81 -->
Las versiones desconocidas conservan la elegibilidad anterior y avisan de que sus reglas no se admiten. No es seguro detectar restricciones ocultas intentando ejecutar trabajos al capturar recomendaciones. Las versiones compatibles de Vivi proporcionan velocidad, rendimiento y elegibilidad de recolección según el equipo. Las versiones desconocidas de Vivi, las reglas de recolección personalizadas y las específicas de Ratkin sin verificar conservan el comportamiento existente; nunca se deducen restricciones por el nombre de raza o xenotipo.

<!-- l10n:b-cc8167cf5cfc -->
Los complementos que modifican proveedores existentes conservan roles y orden. Por ejemplo, manejo de animales sigue recomendándose por Animales. La experiencia condicional en otra habilidad depende de la elegibilidad del complemento y no crea una ruta general para todos los asignados.

<!-- l10n:b-f6d4800368e1 -->
Dos familias chocan: otros reemplazos de Trabajo, como derivados de Work Tab o Better Work Tab, compiten por la interfaz. Los mods que establecen prioridades, como Free Will o PriorityMaster, compiten por los datos: WorkRoles controla a sus colonos gestionados y bloquea esas escrituras. Un aviso único identifica el mod y sugiere el equivalente con roles.

<!-- l10n:b-3e9c0a143130 -->
Los mods de prioridades de mecanoides, como Mech Work Tab, pueden gestionar trabajadores sin asignaciones de WorkRoles. Si activas la compatibilidad con robots y mecanoides, WorkRoles también controla el orden de trabajo de los trabajadores mecánicos con roles asignados. Desactiva esta compatibilidad en Opciones para restablecer el orden nativo de todos ellos; sus roles guardados quedan inactivos.

<!-- l10n:mechanical-workers -->
**Mostrar robots y mecanoides en Colonos** es una opción por partida, desactivada por defecto. Añade los mecanoides de Biotech y los robots de Misc. Robots y Misc. Robots++ a la tabla existente. Asigna roles ahí; usa sus pestañas originales para los grupos de control, las áreas, la activación y la recarga. Los roles solo ordenan trabajos que el trabajador pueda realizar. Los androides humanoides siguen las reglas habituales de los colonos.

<!-- l10n:mechanical-recharge -->
La recarga conserva su funcionamiento original y no necesita un rol de Recarga. Biotech ofrece límites de carga en cada grupo de control. Misc. Robots usa sus propios umbrales de regreso automático; al hacer clic en el porcentaje de carga de su pestaña Robots se solicita una recarga inmediata. WorkRoles no modifica esos umbrales.

<!-- l10n:b-3df0d9a43535 -->
Algunos mods exigen valores de 0 a 4. Si hay problemas, activa «Comunicar prioridades originales de 0 a 4 a otros mods».

<!-- l10n:b-99382dfb0e0e -->
Los que solo leen prioridades funcionan: ven los valores calculados por WorkRoles.
