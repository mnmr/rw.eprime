---
title: Understøttede mods
---
Alle implantatmods, som bruger spillets operationsdata, virker uden opsætning. Spillere har bekræftet Expanded Prosthetics and Organ Engineering og CeleTech, hvis implantater læses ligesom grundspillets. Mods nedenfor er kontrolleret enkeltvis mod deres data.

![En valgt mekanitstamme i torsolisten](mechanites-picked.png)

![Andre stammer viser, hvilken valgt stamme de tilsidesætter](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion**: Mekanitstammer udelukker hinanden. Indsprøjtning af én fjerner alle andre i kroppen. Vælgeren behandler derfor stammerne som én plads, og teksten navngiver den erstattede stamme. Immunitetsforbedrende mekanitter er ikke en stamme og kan sameksistere med alle. Moddets avancerede bioniske blære og hygiejneforbedrer dækkes sammen med Dubs Bad Hygiene af kompatibilitetsindstillingerne.

**Dubs Bad Hygiene**: Bionisk blære og hygiejneforbedrer er torsoimplantater, spillet tillader sammen med de avancerede versioner fra [FSF] Advanced Bionics Expansion. De to kompatibilitetsindstillinger afgør, om Implanner også tillader det.

![Almindeligt og avanceret bionisk øje viser tilsidesættelse af det valgte moduløje](modular-eye.png)

**Integrated Implants**: Ekstra arme tillader én pr. side; en anden type på samme side tilsidesætter den første. Underhudspanser og prestigeversionen, skuldertårne, haler og indopererede sprængstoffer danner hver grupper med kun ét valg. Modulbionik opgraderer på stedet: Vælger du et modulært bionisk øje, indsætter automatikken først et almindeligt, hvis det mangler, og udfører derefter opgraderingen med én komponent og intet ekstra øje. Opgraderingerne kræver EBSG Framework og tilbydes ikke med Medical System Expansion 2. Selve modulerne indsættes fra inventaret uden operation og vises derfor ikke.

**Vanilla Genetics Expanded**: Hybridimplantater til kolonister vises sammen med øvrige implantater. Dyreimplantater vises ikke, da Implanner kun planlægger kolonister.

Nogle hybridimplantater erstatter en kropsdel: Dvalemodulet erstatter hjernen. Andre sidder på delen, f.eks. neuronforstærkning på hjernen. Almindelige implantater kan sættes på en del med et påsat hybridimplantat, men ikke på en erstattet del.

Indsættelse af ethvert hybridimplantat skubber alt på kropsdelen ud. De udstødte implantater falder ved siden af lægen.

- Implanner indsætter hybridimplantatet først, så intet skal ud.
- Har kolonisten allerede implantater dér, lader Implanner dem falde ud, holder dem til samme kolonist og indsætter dem straks igen.
- Implanner indsætter aldrig et hybridimplantat, hvis noget på delen ville gå permanent tabt.

Hybridimplantater får kvalitet fra den anvendte genoramme (se [Mindstekvalitet](topic:minimum-quality)).

**Quality Bionics Remastered**: Bionik har kvaliteter; bedre dele virker bedre og har flere hitpoint. Værktøjstippet viser effektivitet fra frygtelig til legendarisk, og hver plan angiver mindstekvaliteten (se [Mindstekvalitet](topic:minimum-quality)).

**Elite Bionics Framework**: Implantater, der ændrer en kropsdels hitpoint, viser ændringen i værktøjstippet.

![Kravdialog for et benmodul](requirement-prompt.png)

![Efter Tilføj til plan er både ben og modul valgt](requirement-added.png)

**Bionic modularity**: Hvert modul optager en plads på kropsdelen. Ét kampmodul og ét arbejdsmodul kan sidde på samme arm, men ikke to kampmoduler. Lemmoduler monteres på bioniske lemmer og deler delen med dem, så planlagt arm og armmodul sameksisterer. Vælger du et lemmodul uden bionik på delen i planen, vises mulige værtsdele med den mindst avancerede valgt som standard. **Tilføj til plan** tilføjer valg og modul sammen; **Annullér** tilføjer intet.
