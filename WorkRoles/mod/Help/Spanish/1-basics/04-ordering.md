---
title: Orden de asignaciones
---
El colono recorre su fila de izquierda a derecha, contando solo los roles activos:

![Fichas ordenadas de un colono](colonist-row.png)

1. Cada trabajo de un rol anterior tiene prioridad sobre los posteriores.
2. Dentro del rol, van primero los trabajos anteriores.
3. Si un trabajo aparece en varios roles, cuenta su primera posición.

Solo se realizan trabajos de los roles asignados. No asignar un rol equivale a dejar vacía la casilla de prioridad original.

Arrastra para cambiar la prioridad:

@demo:chip-drag

También puedes arrastrar a otra fila para trasladar la asignación. La papelera de la ficha la elimina de la fila.

RimWorld mantiene una lista aparte de emergencias: apagar incendios, atención urgente y acostarse para tratamiento urgente. Interrumpen el sueño, las comidas y cualquier trabajo. Por defecto, solo entran si las cubre alguno de los cinco primeros roles activos. Los posteriores realizan esas emergencias en su posición normal. Con Esencial primero y Médico sexto, el colono interrumpe todo por un incendio, pero trata la atención urgente como trabajo médico normal en su posición. Si bajas Esencial más allá del quinto, apagar incendios también pasa a ser trabajo normal.

Desactivar «Solo los 5 primeros roles interrumpen por emergencias» en Opciones elimina ese límite: cualquier emergencia cubierta por un rol activo entra en la lista, esté donde esté.
