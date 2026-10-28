---
title: Objetivos y límites de existencias
---
<!-- l10n:target -->
La **{k:QJ_MinQualityLabel}** hace que una orden con un número fijo de repeticiones siga intentándolo hasta producir suficientes objetos de esa calidad o superior. Los objetos de calidad inferior se conservan, pero no consumen ninguna de las repeticiones restantes de la orden. Cada intento adicional consume más materiales. **{k:QJ_AnyQuality}** acepta todos los resultados.

<!-- l10n:repeat-modes -->
Estos reintentos se aplican a **Hacer X veces**. Para **Hacer hasta tener X**, usa el intervalo de calidad habitual de la orden para decidir qué objetos cuentan para el objetivo de existencias. **Hacer para siempre** continúa como de costumbre. El objetivo de calidad no selecciona por sí solo a alguien más cualificado para terminar los objetos; configura también los requisitos para terminarlos.

<!-- l10n:stock -->
El **Límite de existencias** limita el trabajo nuevo para un producto en este mapa. Sus objetos sin terminar se cuentan conjuntamente entre todas las órdenes y mesas de trabajo, pero solo las órdenes gestionadas respetan el límite. **0 significa sin límite.** Los objetos terminados que haya almacenados no cuentan.

<!-- l10n:cap-behavior -->
Al alcanzar el límite, las órdenes gestionadas dejan de iniciar objetos nuevos. Se permite reanudar o terminar los objetos existentes para ir vaciando la cola. Si nadie cumple los requisitos para terminarlos, puedes desbloquear la cola reduciendo los requisitos o haciendo que vuelva a estar disponible un trabajador apto.

