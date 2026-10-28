---
title: Hedefler ve stok sınırları
---
<!-- l10n:target -->
**{k:QJ_MinQualityLabel}**, sabit sayıda üretim yapan bir emrin, bu kalitede veya daha iyi yeterli sayıda eşya üretene kadar denemeye devam etmesini sağlar. Hedefin altındaki eşya saklanır ancak emrin kalan tekrar sayısını azaltmaz. Her ek deneme daha fazla malzeme tüketir. **{k:QJ_AnyQuality}**, tüm sonuçları kabul eder.

<!-- l10n:repeat-modes -->
Bu yeniden deneme davranışı **X kez yap** için geçerlidir. **X adet olana kadar yap** seçeneğinde, stok hedefine hangi eşyaların sayılacağını belirlemek için normal üretim emrinin kalite aralığını kullanın. **Sürekli yap** her zamanki gibi devam eder. Kalite hedefi tek başına daha iyi bir tamamlayıcı seçmez; tamamlayıcı gereksinimlerini de ayarlayın.

<!-- l10n:stock -->
**Stok sınırı**, bu haritada bir ürün için başlatılabilecek yeni işleri sınırlar. Ürünün yarım kalmış eşyaları, tüm üretim emirleri ve tezgâhlar genelinde birlikte sayılır; ancak sınırı yalnızca yönetilen üretim emirleri uygular. **0, sınırsız demektir.** Depodaki tamamlanmış eşyalar sayılmaz.

<!-- l10n:cap-behavior -->
Sınıra ulaşıldığında, yönetilen üretim emirleri yeni eşyaların üretimine başlamaz. Mevcut eşyalara devam edilebilir veya bunlar tamamlanabilir; böylece kuyruk boşalabilir. Hiçbir tamamlayıcı koşulları karşılamıyorsa gereksinimleri düşürmek veya uygun bir çalışanı yeniden kullanılabilir hâle getirmek kuyruğu açabilir.

