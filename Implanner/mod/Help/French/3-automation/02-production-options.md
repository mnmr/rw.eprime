---
title: Options de production
---
Avec **Ajouter automatiquement des ordres pour les implants manquants**, Implanner place des ordres sur les établis capables de les fabriquer et retire ses propres ordres lorsque le besoin disparaît. Vos ordres manuels ne sont jamais retirés. Chaque ordre fabrique un objet, dans l'ordre de pose prévu : avec Ensembles complets, les jambes d'un colon précèdent ses bras si elles sont mieux classées dans le plan.

Si plusieurs recettes produisent le même implant, Implanner préfère une voie disponible qu'il peut payer immédiatement. Un implant de base libre peut être amélioré ; sinon, une recette directe peut fabriquer l'implant avancé à partir de matériaux. Si seule la voie d'amélioration est activée, la production d'intermédiaires peut fabriquer la base d'abord. Les recettes doivent être recherchées et disposer d'un établi admissible.

Pour dédier un établi aux implants, cochez **Établi Implanner** dans son onglet Ordres. Cette case apparaît uniquement sur les établis capables de fabriquer des implants. Les ordres existants sont suspendus et Implanner y place ses ordres avant tout autre établi. Tant que la case est cochée, vous ne pouvez pas y ajouter, coller, suspendre ou reprendre des ordres.

![Établi de fabrication confié à Implanner](implanner-bench.png)

Décochez pour récupérer l'établi. Les ordres déjà suspendus à l'activation le restent ; les autres reprennent. Si vous retirez Implanner alors qu'un établi est désigné, celui-ci fonctionne toujours, mais vous devez reprendre ses ordres vous-même.

![Options de production](production-options.png)

- **Établis pouvant recevoir des ordres** limite le nombre d'établis travaillant sur les implants simultanément par colonie. Chacun contient jusqu'à deux ordres Implanner, afin que le suivant attende déjà à la fin du premier. Les établis Implanner comptent dans cette limite.
- **Créer des ordres uniquement sur les établis inactifs** évite ceux ayant déjà du travail en attente, pour donner la priorité à vos propres files.
- **Utiliser uniquement les établis Implanner** y limite les ordres ; sans établi désigné, la colonie ne fabrique aucun implant. Désactivé, les autres établis sont utilisés une fois que chaque établi Implanner contient deux ordres.
- **Compétence d'artisanat requise pour les ordres de production** évite de confier les matériaux coûteux aux artisans peu qualifiés.
- **Autoriser les ordres de production des intermédiaires manquants** suit les manques dans la chaîne : un composant avancé manquant pour la prochaine jambe bionique entraîne d'abord un ordre de composant avancé. Pendant qu'un implant attend une ressource brute comme l'plastacier, les établis libres fabriquent les composants avancés nécessaires. Les produits manufacturés, comme composants et implants de base, conviennent. Les ressources brutes comme l'acier ne sont jamais fabriquées, même si une recette de fonte le permet ; vous devez les récolter vous-même.

Chaque ordre respecte aussi les stocks minimaux (voir [Conserver du stock](topic:keeping-stock)).
