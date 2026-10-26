---
title: Funcionamiento interno
---
Los roles ordenados de cada colono se compilan en un orden estricto de trabajos y se entregan al juego mediante las listas que ya lee. El comportamiento en el trabajo es el mismo que con una tabla ajustada a mano, sin parches por tick ni trabajo por fotograma.

Se recalcula al cambiar roles o asignaciones y después se guarda en caché. Los condicionales también se actualizan al cambiar la hora o la ubicación. Las emergencias usan la pasada especial del juego si las cubre uno de los primeros cinco roles activos; las demás ocupan su lugar normal. Desactivar «Solo los 5 primeros roles interrumpen por emergencias» hace que todas las emergencias cubiertas usen esa pasada.

WorkRoles mantiene los valores de prioridades originales a partir de tus roles, convertidos al intervalo 0–4 si está activa la opción correspondiente, por defecto. Por eso se puede retirar con seguridad: vuelve la pestaña Trabajo con los roles convertidos en prioridades. Añadirlo a una partida existente convierte en sentido inverso.
