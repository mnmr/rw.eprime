---
title: Bionic modularity
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Veredicto** | ![Totalmente compatible](verdict-blue.png|16) Totalmente compatible (soporte personalizado) |
| **Evidencia** | Análisis del código fuente, prueba automatizada en el juego |
| **Verificado** | 2026-09-05 |
| **Versiones** | Implanner 1.1.0, Bionic modularity [Workshop 3589154549](https://steamcommunity.com/sharedfiles/filedetails/?id=3589154549), archivos 2026-09-05 |

<!-- l10n:summary -->
Los módulos comparten una extremidad biónica con ella, y un módulo de extremidad pide su anfitrión.

<!-- l10n:b-c7cd0a958354 -->
![El aviso de requisito para un módulo de pierna](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![El plan después de Añadir al plan: la pierna biónica y el módulo están ambos marcados](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
Cada módulo ocupa una ranura en su parte del cuerpo, así que un módulo de combate y uno de trabajo caben en el mismo brazo, pero dos módulos de combate no. Los módulos para extremidades se instalan sobre una extremidad biónica y comparten la parte con ella, de modo que un brazo biónico planificado y un módulo de brazo planificado coexisten. Al marcar un módulo de extremidad cuando el plan no tiene ninguna biónica en esa parte, se abre un aviso que enumera las biónicas capaces de alojarlo, con la menos avanzada seleccionada de forma predeterminada. **Añadir al plan** agrega tu elección y el módulo juntos. **Cancelar** no agrega nada.

