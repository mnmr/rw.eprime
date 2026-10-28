---
title: Cómo funciona la automatización
---
<!-- l10n:b-44e89be1e85f -->
La automatización revisa la colonia varias veces por hora del juego; cada edición provoca además una revisión inmediata:

<!-- l10n:b-6eb19bb9f96f -->
1. Cuenta lo que aún falta a los colonos asignados.
2. Encarga implantes que la colonia pueda fabricar (consulta [Opciones de producción](topic:production-options)).
3. Reserva objetos terminados del almacén para colonos concretos, según prioridades y niveles.
4. Cuando todo lo necesario para el siguiente paso está reservado y en la colonia, añade las operaciones juntas al final de su lista, después de las existentes.

<!-- l10n:b-a747349bea45 -->
El resumen superior de Resumen muestra si la automatización está activa, qué se fabrica o espera, cuántos implantes hay y faltan por producir y quién es el siguiente para cirugía. Pasa el cursor sobre producción o cirugía para un desglose por implante, incluidos sus motivos de espera.

<!-- l10n:b-8fea0b171c65 -->
![Resumen de la colonia con producción y cirugía](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Los lotes retrasan la cirugía hasta poder instalar varios implantes con una sola anestesia. El lote es lo que el colono necesita a continuación: su nivel actual o todo su plan, según la estrategia (consulta [Opciones de cirugía](topic:surgery-options)). Una estrategia omite los lotes y programa cada implante en cuanto esté reservado y presente.

<!-- l10n:b-6b9f966652bc -->
Las operaciones esperan a que el colono esté en condiciones. Las colonias nunca toman objetos del almacén de otra: se reservan e instalan donde vive el colono.
