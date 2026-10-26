---
title: Grundbegrepp
---
WorkRoles ersätter grundspelets prioritetsrutnät med ett rollsystem.
I stället för att ge arbetstyper prioritetstal tilldelar du kolonister
namngivna roller. Båda systemen ger i slutänden en ordnad lista över
jobben som kolonisterna ska utföra.

![En kolonist och dess rollbrickor](colonist-row.png)

Tänk på en roll som en behållare för jobb. Du kan redigera roller för
att välja jobbens inbördes ordning, eller behålla standarderna som
ligger nära grundspelets beteende.

De flesta färdiga roller motsvarar kolumnerna i grundspelets
prioritetsrutnät, en roll per arbetstyp. Resten är tillägg för mer
precisa tilldelningar: smalare roller som Slaktare och Växtskärare samt
samlingar som Grundarbete.

En arbetstyp är en kolumn i grundspelets rutnät; ett jobb är en bestämd
aktivitet inom den. Arbetstypen Läkare samlar exempelvis patientbehandling och
operationer:

![Arbetstypen Läkare och dess jobb](worktype-jobs.png)

Roller innehåller hela arbetstyper eller enskilda jobb. Den färdiga
rollen ![Läkarbricka](chip-doctor.png) omfattar hela arbetstypen Läkare, medan
Slaktare bara innehåller styckningsjobben från Kock.

Stjärnorna på en bricka är ett lämplighetsomdöme: hur väl kolonisten
passar rollen, från rött (dåligt) via grått (neutralt) till vitt och guld
(bra). [Omdömen](topic:verdicts) beskriver hela skalan.

Brickornas ordning avgör vad som görs först
([Tilldelningarnas ordning](topic:ordering)), och varje bricka kan
slås på och av utan att tappa sin plats ([Rolltillstånd](topic:states)).
