---
title: Calidad mínima
---
<!-- l10n:b-d8fd3884b5cc -->
Con Quality Bionics Remastered o Vanilla Genetics Expanded, los implantes tienen calidades de horrible a legendaria que afectan a su funcionamiento. El plan muestra entonces **Calidad mínima** junto a **Eliminar plan**.

<!-- l10n:b-118873404542 -->
![Cabecera con el botón Calidad mínima](plan-min-quality.png)

<!-- l10n:b-84db128338c2 -->
El mínimo se aplica a las nuevas instalaciones de este plan, incluidas las elecciones heredadas. La fabricación automática busca alcanzar esa calidad y puede necesitar varios intentos. No afecta a los implantes sin calidad. Un plan nuevo que deriva de otro empieza con el mismo mínimo.

<!-- l10n:b-93af25d8591d -->
Implanner nunca sustituye una parte sana por otra peor. Un brazo biónico horrible funciona peor que uno natural, por lo que espera a un objeto mejor salvo que el brazo falte o esté gravemente dañado. Si varios objetos cumplen, el mejor va al colono cuyo turno llega primero.

<!-- l10n:b-0e7cf20ad9dc -->
Un implante ya instalado del tipo previsto sigue contando como completado, aunque no alcance la calidad mínima. Activa **Dar los mejores implantes a los colonos prioritarios** para permitir su sustitución por uno de mayor calidad, como se explica a continuación.

<!-- l10n:b-b163fd8a6054 -->
## Mejores implantes para colonos prioritarios

<!-- l10n:b-0c6c2d002995 -->
![La opción en Automatización](upgrade-by-priority.png)

<!-- l10n:b-8937cc3e8047 -->
Con **Dar los mejores implantes a los colonos prioritarios**, en las opciones de cirugía de Automatización, la calidad sigue la prioridad:

<!-- l10n:b-0d3ec0233e11 -->
- Las cirugías nuevas usan la menor calidad aceptada para dejar libres los mejores objetos.
- Un objeto mejor va al colono de mayor prioridad que tenga uno peor del mismo tipo. Se retira el antiguo, se instala el mejor y el retirado pasa al siguiente colono.
- También se cede un objeto reservado para alguien menos prioritario si su operación aún no está programada.

<!-- l10n:b-e246867de4e8 -->
Sustituir un implante instalado por otro de mayor calidad requiere dos operaciones: extracción e instalación. La sustitución solo empieza si no hay otra cirugía de Implanner pendiente para ese colono y queda sitio bajo el límite de cirugías simultáneas. Nunca se sustituye un implante si extraerlo retiraría también otra cosa de esa parte del cuerpo.

<!-- l10n:b-37015122025b -->
Al fabricar para el plan:

<!-- l10n:b-51f50276fd20 -->
- Con Quality Jobs, el proyecto apunta al mínimo y el acabado se asigna a un artesano capaz de alcanzarlo.
- Los híbridos de Vanilla Genetics Expanded toman la calidad de la estructura genética. El proyecto solo usa estructuras de la calidad mínima o superior.
- Sin ninguno de esos mecanismos, cualquier artesano puede terminarlo y un resultado inferior se fabrica de nuevo. Los mínimos de existencias (consulta [Mantener existencias](topic:keeping-stock)) frenan esto antes de agotar los materiales.
