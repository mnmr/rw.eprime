---
title: Opciones de producción
---
<!-- l10n:b-bdd2b459773e -->
Con **Añadir proyectos automáticamente para implantes faltantes** activado, Implanner encarga los implantes en bancos capaces de fabricarlos y retira sus propios proyectos cuando dejan de ser necesarios. Nunca retira los que hayas creado tú. Cada proyecto realiza una fabricación, siguiendo el orden de instalación: con Conjuntos completos, las piernas de un colono se fabrican antes que sus brazos si tienen mayor prioridad en tu clasificación compartida de implantes.

<!-- l10n:b-8effc9e9b828 -->
Si varias recetas producen el mismo implante, se prefiere una ruta disponible que pueda pagarse ahora. Puede mejorarse un implante base libre o fabricar directamente el avanzado con materiales. Si solo está habilitada la mejora, la producción intermedia puede fabricar primero la base. Las recetas deben estar investigadas y disponer de un banco apto.

<!-- l10n:b-0aa3ded1d655 -->
Para dedicar un banco a la fabricación de implantes, marca **Banco de Implanner** en la pestaña Proyectos del banco. La casilla solo aparece en bancos capaces de fabricar implantes. Los proyectos que ya hay en el banco se suspenden, y Implanner coloca allí sus proyectos de fabricación antes que en cualquier otro banco.

<!-- l10n:queue -->
Mientras esté marcada, la pestaña Proyectos muestra la cola de producción de Implanner en lugar de la lista de proyectos habitual del banco. Cada fila es un proyecto encargado por Implanner: el objeto, si un artesano ya lo ha empezado, el plan al que pertenece, el colono que sigue en la fila para recibirlo y cuánto tiempo lleva en cola. Un proyecto para un ingrediente indica el implante para el que se necesita. **Detalles** abre los ajustes del juego para ese proyecto. La línea inferior cuenta tus propios proyectos que están en pausa. No puedes añadir proyectos al banco hasta que desmarques la casilla.

<!-- l10n:b-a4d04c1bceb2 -->
![Banco de fabricación cedido a Implanner](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Desmárcalo para recuperarlo. Los proyectos ya suspendidos antes de cederlo siguen así; los demás se reanudan. Si desinstalas Implanner mientras está marcado, el banco funciona, pero tendrás que reanudar sus proyectos manualmente.

<!-- l10n:b-c9f0dad1a5c5 -->
![Opciones de producción](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Bancos con proyectos** limita los bancos simultáneos por colonia. Cada uno admite dos proyectos de Implanner, para tener el siguiente listo al terminar el primero. Los bancos dedicados cuentan para el límite.
- **Crear proyectos solo en bancos inactivos** evita ocupar bancos con trabajo pendiente y da prioridad a tus colas.
- **Usar solo bancos de Implanner** impide fabricar implantes si la colonia no tiene ninguno. Desactivado, se usan otros cuando todos los dedicados ya tienen dos proyectos.
- **Habilidad de fabricación exigida para producción** protege los materiales caros de artesanos poco cualificados.
- **Permitir proyectos para intermedios que falten** sigue las carencias por la cadena: si falta un componente avanzado para la siguiente pierna biónica, se encarga primero. Mientras un implante espera una materia prima como plastiacero, los bancos libres fabrican los componentes avanzados que necesitará. Se admiten productos manufacturados, como componentes e implantes base. Nunca materias primas como acero, aunque exista una receta de fundición para producirlas: debes reunirlas tú.

<!-- l10n:b-24089426e9a9 -->
Todo proyecto respeta también los mínimos de existencias (consulta [Mantener existencias](topic:keeping-stock)).
