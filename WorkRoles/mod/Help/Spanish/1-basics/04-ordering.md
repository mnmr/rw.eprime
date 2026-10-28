---
title: Orden de asignaciones
---
<!-- l10n:b-32c8a631526a -->
El colono recorre su fila de izquierda a derecha, contando solo los roles activos:

<!-- l10n:b-75c264ac5015 -->
![Fichas ordenadas de un colono](colonist-row.png)

<!-- l10n:b-617ab83d7eb0 -->
1. Cada trabajo de un rol anterior tiene prioridad sobre los posteriores.
2. Dentro del rol, van primero los trabajos anteriores.
3. Si un trabajo aparece en varios roles, cuenta su primera posición.

<!-- l10n:b-be52fb18f81e -->
Solo se realizan trabajos de los roles asignados. No asignar un rol equivale a dejar vacía la casilla de prioridad original.

<!-- l10n:b-db1bdd5d561c -->
Arrastra para cambiar la prioridad:

<!-- l10n:b-90462aacb6d4 -->
@demo:chip-drag

<!-- l10n:b-be64d575edc7 -->
También puedes arrastrar a otra fila para trasladar la asignación. La papelera de la ficha la elimina de la fila.

<!-- l10n:b-6bbb9dc33edb -->
RimWorld mantiene una lista aparte de emergencias: apagar incendios, atención urgente y acostarse para tratamiento urgente. Interrumpen el sueño, las comidas y cualquier trabajo. Por defecto, solo entran si las cubre alguno de los cinco primeros roles activos. Los posteriores realizan esas emergencias en su posición normal. Con Esencial primero y Médico sexto, el colono interrumpe todo por un incendio, pero trata la atención urgente como trabajo médico normal en su posición. Si bajas Esencial más allá del quinto, apagar incendios también pasa a ser trabajo normal.

<!-- l10n:b-a52fa63b5067 -->
Desactivar «Solo los 5 primeros roles interrumpen por emergencias» en Opciones elimina ese límite: cualquier emergencia cubierta por un rol activo entra en la lista, esté donde esté.
