---
title: Nelere dokunulmaz?
---
Uygula gitsin, Kolonimi düzenle ve Otomatik iyileştir normalde dört tür atamayı korur:

- Açıkça "dokunma" dediğiniz [sabitlenmiş](topic:pinning) atamalar.
- Bir şeyi yasaklamanın bilinçli seçim olduğu [engelleyici roller](topic:blocker-roles).
- Zaten kurallarıyla yönetilen [koşullu roller](topic:conditional-roles).
- Yere düşmüş kolonicinin tüm rolleri.

**Sabitlenmiş atamalar hariç tutulsalar bile korunur.** Sabitlenmemiş rol veya bileşik üyelerinden biri hariç tutulmuşsa önerileri uygulamak o atamayı kaldırır. [Öneri sırası](topic:recommendation-order) konusuna bakın.

Yere düşmüş kolonicinin satırında kırmızı çizgi görünür:

![Yere düşmüş kolonicinin satırı](downed-row.png)

Hariç tutulmayan rolleri ve sabitlenmiş atamaları iyileşene kadar aynı kalır. Taşıdığı hiçbir rol koloni ihtiyaçlarına sayılmaz; motor bunları diğer kolonicilerle karşılar. Önerilen roller listesindeki her etiket bunu söyler:

![Etiket ipucunda yere düşme açıklaması](downed-tooltip.png)
