---
title: Bionic modularity
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Veredicto** | ![Totalmente compatible](verdict-blue.png|16) Totalmente compatible (soporte específico) |
| **Evidencia** | Análisis del código fuente, prueba automatizada en el juego |
| **Comprobado** | 2026-09-05 |
| **Versiones** | Implanner 1.1.0, Bionic modularity [Workshop 3589154549](https://steamcommunity.com/sharedfiles/filedetails/?id=3589154549), archivos 2026-09-05 |

<!-- l10n:summary -->
Los módulos comparten con la extremidad biónica su parte del cuerpo, y un módulo de extremidad pide su anfitrión.

<!-- l10n:b-c7cd0a958354 -->
![El aviso de requisito para un módulo de pierna](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![El plan tras Añadir al plan: la pierna biónica y el módulo están seleccionados](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
Cada módulo ocupa una ranura de su parte del cuerpo, así que un módulo de combate y uno de trabajo caben en el mismo brazo, pero dos de combate no. Los módulos de extremidades se instalan sobre una extremidad biónica y comparten la parte con ella, de modo que un brazo biónico planificado y un módulo de brazo planificado conviven. Si marcas un módulo de extremidad cuando el plan no tiene ningún biónico en esa parte, se abre un aviso que enumera los biónicos capaces de alojarlo, con el menos avanzado seleccionado por defecto. **Añadir al plan** añade tu elección y el módulo a la vez. **Cancelar** no añade nada.

