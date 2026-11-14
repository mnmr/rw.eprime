---
title: Multi-Doctor Surgery
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Veredicto** | ![Totalmente compatible](verdict-blue.png|16) Totalmente compatible (soporte específico) |
| **Evidencia** | Análisis del código fuente, prueba automatizada en el juego |
| **Comprobado** | 2026-11-14 |
| **Versiones** | Implanner 1.2.9, Multi-Doctor Surgery [Workshop 3356249192](https://steamcommunity.com/sharedfiles/filedetails/?id=3356249192) |

<!-- l10n:summary -->
Un equipo quirúrgico puede realizar las operaciones programadas por Implanner.

<!-- l10n:teams -->
La opción **Equipos quirúrgicos** de la pestaña Opciones elige quién realiza las operaciones de Implanner. **Individual**, el valor por defecto, hace que opere un solo médico, como sin Multi-Doctor Surgery. **Equipo predeterminado** usa el equipo quirúrgico predeterminado configurado en la pestaña Operaciones de Multi-Doctor Surgery. **Mejor equipo** hace que dirija el médico disponible con más habilidad, asistido por los médicos que más aumentan la probabilidad de éxito, hasta el límite de médicos de Multi-Doctor Surgery. Los médicos mecanoides pueden unirse al equipo cuando Multi-Doctor Surgery los incluye.

<!-- l10n:rules -->
Implanner asigna el equipo al programar una operación, y la operación aparece luego en la pestaña Operaciones de Multi-Doctor Surgery. Una operación en equipo espera a su cirujano principal, que puede operar incluso cuando la selección automática de médicos exigiría una habilidad mayor. Si el líder predeterminado no puede operar a ese colono, por ejemplo porque es el paciente o está en otra colonia, un solo médico realiza la operación sin equipo. Las operaciones excluidas en la configuración de Multi-Doctor Surgery se hacen en solitario. Los equipos no se usan en multijugador.

<!-- l10n:manual -->
Las operaciones que añades a mano también reciben el diálogo de equipo y las bonificaciones de Multi-Doctor Surgery, incluso en colonos que siguen un plan de Implanner.

