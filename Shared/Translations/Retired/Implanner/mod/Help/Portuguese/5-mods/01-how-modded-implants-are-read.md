---
title: Como os implantes de mods são lidos
---
Implanner não precisa de uma lista de mods compatíveis. Ele lê todas as cirurgias carregadas do jogo base, expansões e mods. Uma cirurgia que instala um implante numa parte corporal vira entrada no seletor. Os mesmos dados definem a coexistência:

- **Uma substituição por parte**: um braço biónico substitui o ombro. Duas substituições não partilham a parte, e substituir remove implantes nela e nas partes abaixo.
- **Implantes na mesma parte**: coexistem, exceto quando a cirurgia declara incompatibilidade. Assim as glândulas de pele excluem-se no jogo base e mods definem espaços de módulos.
- **Remoção ao instalar**: alguns implantes retiram um rival de qualquer lugar do corpo. Dois que se removem mutuamente nunca coexistem.
- **Cirurgias que esvaziam uma parte**: algumas retiram tudo antes de acrescentar um implante que não substitui a parte. O que for instalado depois permanece. Implanner coloca esse implante primeiro e reinstala o que ele expulsar.
- **Módulos em biónicos**: uma cirurgia programada para instalar numa parte artificial partilha a parte com o biónico, em vez de substituí-lo.
- **Melhorias no local**: uma cirurgia que retira um implante e acrescenta outro melhora a parte instalada. A versão melhorada aparece separadamente.
- **Objetos não fabricáveis**: se nenhuma bancada produz o objeto, ele fica oculto até ligar a opção correspondente.

Há duas limitações: módulos inseridos pelo inventário sem cirurgia nunca aparecem, e regras existentes apenas no código de um mod, sem dados correspondentes, não podem ser lidas. Tudo que o mod expressa nas cirurgias funciona sem mudar Implanner.
