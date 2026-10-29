---
title: Configurar una orden
---
<!-- l10n:open -->
Abre **Detalles** en una orden de trabajo para un objeto con calidad. El panel **{k:QJ_QualityPanelTitle}** aparece junto a los controles habituales de la orden en las recetas compatibles que dejan un objeto sin terminar. Las recetas sin calidad o sin trabajo a medio terminar mantienen su funcionamiento habitual.

<!-- l10n:bill-image -->
![Una orden de trabajo de un banco de trabajo con su panel de Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Activa **{k:QJ_ManageBill}** y elige los [requisitos de los encargados del acabado](topic:finishers). Los trabajadores normales pueden realizar la mayor parte de la fabricación. Si el trabajador actual cumple los requisitos, termina el objeto directamente; de lo contrario, el objeto casi terminado se aparta y la orden puede empezar a fabricar más objetos sin superar su límite de existencias.

<!-- l10n:dispatch -->
Cuando un encargado del acabado cumple los requisitos, Quality Jobs crea una orden temporal **Finalizar …** y se la asigna. Este recoge el objeto sin terminar y lo completa. Después, la orden temporal se elimina automáticamente. Los objetos en espera se pueden transportar y almacenar como cualquier otro objeto sin terminar.

<!-- l10n:finisher-bill-restrictions -->
El nivel máximo de habilidad y la asignación a un trabajador concreto de la orden original se aplican hasta que el objeto se aparta para el acabado. Puedes asignar el trabajo a un aprendiz mientras un colono más capacitado, un mecanoide de la colonia o un robot compatible se encarga de la orden específica de finalización. Los encargados del acabado deben ser capaces de realizar la receta y cumplir los requisitos de Quality Jobs.

<!-- l10n:status -->
La sección **{k:QJ_StatusHeader}** muestra el trabajo compartido **Sin terminar**, los objetos **En espera** de alguien que los termine y los ya asignados para su **Finalización**. Pasa el cursor por el estado y la lista de trabajadores encargados de terminar los objetos para ver más detalles. Consulta [cuándo queda trabajo en espera](topic:waiting) si nada avanza.

<!-- l10n:disable -->
Desmarca **{k:QJ_ManageBill}** para que vuelva a completarse de la forma habitual. Otros trabajadores podrán seguir realizando el trabajo compartido de esta orden si la opción de compartir está activada.

