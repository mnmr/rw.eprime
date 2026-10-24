---
title: Opciones de producción
---
Con **Añadir automáticamente tareas para los implantes que faltan**, Implanner encarga los implantes en bancos capaces y retira sus tareas cuando dejan de ser necesarias. Nunca retira las tuyas. Cada tarea realiza una fabricación, siguiendo el orden de instalación: con Conjuntos completos, las piernas de un colono se hacen antes que sus brazos si el plan las coloca primero.

Si varias recetas producen el mismo implante, se prefiere una ruta disponible que pueda pagarse ahora. Puede mejorarse un implante base libre o fabricar directamente el avanzado con materiales. Si solo está habilitada la mejora, la producción intermedia puede fabricar primero la base. Las recetas deben estar investigadas y disponer de un banco apto.

Para dedicar un banco, marca **Banco de Implanner** en su pestaña de tareas. Solo aparece en bancos capaces de fabricar implantes. Suspende las tareas existentes y recibe encargos antes que los demás bancos. Mientras esté marcado no puedes añadir, pegar, suspender ni reanudar tareas.

![Banco de fabricación cedido a Implanner](implanner-bench.png)

Desmárcalo para recuperarlo. Las tareas ya suspendidas antes de cederlo siguen así; las demás se reanudan. Si desinstalas Implanner mientras está marcado, el banco funciona, pero tendrás que reanudar sus tareas manualmente.

![Opciones de producción](production-options.png)

- **Bancos que pueden tener tareas** limita los bancos simultáneos por colonia. Cada uno admite dos tareas de Implanner, para tener la siguiente lista al terminar la primera. Los bancos dedicados cuentan para el límite.
- **Crear tareas solo en bancos inactivos** evita ocupar bancos con trabajo pendiente y da prioridad a tus colas.
- **Usar solo bancos de Implanner** impide fabricar implantes si la colonia no tiene ninguno. Desactivado, se usan otros cuando todos los dedicados ya tienen dos tareas.
- **Habilidad de fabricación exigida para producción** protege los materiales caros de artesanos poco cualificados.
- **Permitir tareas para productos intermedios que falten** sigue las carencias por la cadena: si falta un componente avanzado para la siguiente pierna biónica, se encarga primero. Mientras un implante espera una materia prima como plastiacero, los bancos libres fabrican los componentes avanzados que necesitará. Se admiten productos manufacturados, como componentes e implantes base. Nunca materias primas como acero, aunque exista una receta de fundición para producirlas: debes reunirlas tú.

Toda tarea respeta también los mínimos de existencias (consulta [Mantener existencias](topic:keeping-stock)).
