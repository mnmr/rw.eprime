---
title: Cómo funcionan las recomendaciones
---
<!-- l10n:b-07818cd37217 -->
El motor lee las aptitudes de los colonos y las necesidades de la colonia y propone una fila de roles para cada uno.

<!-- l10n:b-d0c69b8a87f5 -->
Sus indicios se llaman señales: pasiones ardientes, aptitudes genéticas, rasgos, pericias y las mejores habilidades de la colonia. Además exige cobertura de necesidades, para que Médico y Cocinero recaigan en alguien, idealmente con talento.

<!-- l10n:b-45031c5441da -->
Los preceptos opuestos al trabajo reducen su puntuación. Es más probable elegir a alguien apto y dispuesto, aunque quien se opone aún puede cubrir tareas esenciales. Una negativa comprobada o falta de capacidad lo descarta. Las valoraciones de habilidades siguen describiendo aptitud; Mejores candidatos muestra esa aptitud entre capaces, mientras que la recomendación de colonia también considera aversiones y demanda.

<!-- l10n:b-e112aed1a354 -->
Son datos para recomendar. Los roles condicionales siguen decidiendo cuándo y dónde está activa una asignación.

<!-- l10n:b-2a25543f495f -->
En versiones compatibles de Vivi, la recolección usa la velocidad y rendimiento reales del trabajador. Un humano con una pajita Vivi puede recoger miel y polen; un cepillo multiusos permite recoger plantas. Equipar o retirar herramientas actualiza la capacidad incluso en pausa. Poder hacer algunas recetas mantiene la elegibilidad, pero las no disponibles se excluyen de las opciones de formación. Las versiones desconocidas y reglas personalizadas de Vivi conservan el comportamiento anterior.

<!-- l10n:b-fce545e1aa79 -->
El motor considera todos los roles aptos y los ordena por colono. Opciones de roles enumera todos salvo los excluidos. Para impedir siempre una recomendación, añade el rol a **Roles excluidos** arriba. La exclusión prevalece sobre necesidades y tratamientos especiales; las asignaciones ya fijadas siguen protegidas. Véase [Orden y exclusiones](topic:recommendation-order).

<!-- l10n:b-4cefba49c974 -->
Puedes consultar su criterio en todas partes. Las descripciones de la paleta enumeran los mejores candidatos:

<!-- l10n:b-0dd35388dc64 -->
![Mejores candidatos en la descripción](palette-tooltip.png)

<!-- l10n:b-f1a1a1196f55 -->
Dos roles tienen trato especial. Cazador se recomienda a quien lleve un arma a distancia, pues entrena Disparo; va antes para malos tiradores y después para expertos cuyo tiempo vale más en otros trabajos. La aversión a cazar también lo retrasa. Mecanitor solo se recomienda a quien tenga el mecaenlace, por buenos que sean otros artesanos; Mejores candidatos respeta la misma regla.

<!-- l10n:b-b241ec72dcda -->
El motor propone y tú aplicas, [rol por rol](topic:cherry-picking) o con [Arreglar mi colonia](topic:fix-my-colony). La excepción es [Optimización automática](topic:auto-optimize): al activarla se ajustan todas tus colonias cada hora del juego.

<!-- l10n:mechanical-suggestions -->
Al activar las filas de robots y mecanoides, sus recomendaciones se basan en sus habilidades actuales y en los trabajos que pueden realizar, sin pasiones ni vías de aprendizaje humanas. Su tarea principal aceptada se mantiene, incluso si corresponde a un rol excluido asignado explícitamente; debes cambiarla manualmente. Las recomendaciones de trabajo adicional respetan las exclusiones. Tú eliges sus asignaciones; Optimización automática sigue aplicando solo recomendaciones para humanos.

<!-- l10n:mechanical-coverage -->
**Usar la cobertura de robots y mecas en las recomendaciones para humanos** es una opción independiente para cada partida, también desactivada por defecto. Cuenta las asignaciones mecánicas aceptadas al calcular las necesidades de personal, de modo que una tarea totalmente cubierta puede prescindir de su último especialista humano. Cada trabajador aporta una tarea principal; los roles de respaldo no cuentan como personal adicional. Las áreas restringidas, los trabajos bloqueados, las habilidades insuficientes y las restricciones de Quality Jobs sobre el trabajo de los robots impiden la cobertura total. Un robot que solo puede limpiar no sustituye a un rol que combine limpieza y transporte.

<!-- l10n:mechanical-learning -->
La formación seleccionada para humanos que aún no haya alcanzado su objetivo y las asignaciones fijadas siguen protegidas.
La recarga habitual mantiene la cobertura; la desactivación, la pérdida de control o salir del modo Trabajo la eliminan. Las recomendaciones actualizadas proponen sustitutos humanos, y la Optimización automática los asigna en su siguiente ajuste horario si está activada. Con la integración desactivada, las filas de robots y mecas no afectan a las recomendaciones para humanos.
