---
title: Objetivos y límites de existencias
---
<!-- l10n:target -->
La **{k:QJ_MinQualityLabel}** hace que una orden con un número fijo de repeticiones siga intentándolo hasta producir suficientes objetos de esa calidad o superior. Los objetos de calidad inferior se conservan, pero no consumen ninguna de las repeticiones restantes de la orden. Cada intento adicional consume más materiales. **{k:QJ_AnyQuality}** acepta todos los resultados.

<!-- l10n:repeat-modes -->
Estos reintentos se aplican a **Hacer X veces**. Para **Hacer hasta tener X**, usa el intervalo de calidad de la orden normal para decidir qué objetos cuentan para la cantidad objetivo de existencias. **Hacer indefinidamente** continúa como siempre. Una calidad objetivo no selecciona por sí sola a un mejor trabajador para terminar los objetos; configura también los requisitos para terminarlos.

<!-- l10n:stock -->
El **Límite de existencias** restringe el trabajo nuevo de un producto en este mapa. Sus objetos sin terminar se cuentan en conjunto entre todas las órdenes y mesas de trabajo, pero solo las órdenes gestionadas aplican el límite. **0 significa sin límite.** Los objetos terminados almacenados no cuentan.

<!-- l10n:cap-behavior -->
Al alcanzar el límite, las órdenes gestionadas dejan de iniciar objetos nuevos. Aún se permite reanudar o terminar los objetos existentes, de modo que la cola pueda vaciarse. Si ningún trabajador cumple los requisitos para terminarlos, reducir los requisitos o volver a disponer de un trabajador apto puede desbloquear la cola.

