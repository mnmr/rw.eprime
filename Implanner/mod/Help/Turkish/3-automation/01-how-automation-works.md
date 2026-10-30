---
title: Otomasyon nasıl çalışır?
---
<!-- l10n:b-44e89be1e85f -->
Otomasyon koloniyi oyun saati başına yaklaşık iki kez kontrol eder. Bir düzenlemeden sonra, oyun zamanı yeniden ilerlediğinde tekrar kontrol eder. Otomasyon değişikliklerini uygulamak için oyunu duraklatmadan çıkarın. Her kontrolde şu adımlar izlenir:

<!-- l10n:b-6eb19bb9f96f -->
1. Atanmış kolonicilerin kalan eksiklerini sayar.
2. Koloninin üretebildiği implantlar için üretim siparişleri ekler ([üretim seçenekleri](topic:production-options) konusuna bakın).
3. Öncelik ve kademelere göre depodaki hazır eşyaları belirli kolonicilere ayırır.
4. Kolonicinin sonraki aşamada ihtiyaç duyduğu her şey ayrılıp yerinde olduğunda, ameliyatları tek blok olarak mevcut siparişlerin arkasına ekler.

<!-- l10n:b-a747349bea45 -->
Genel bakış sekmesinin üstündeki koloni özeti tüm bunları gösterir: otomasyon açık mı, üretim ne yapıyor veya ne bekliyor, kaç implant stokta ve kaçının hâlâ üretilmesi gerekiyor, ameliyatta sıradaki kim. Her eşyanın ne beklediği dâhil implant bazında döküm için üretim veya ameliyat sayılarının üzerine gelin.

<!-- l10n:production-counts -->
Üretim ipucunda **Üretilecek**, üretim siparişi henüz olmayanlar da dâhil, ihtiyaç duyulan ve stoktan karşılanamayan implantları sayar. **Üretim siparişlerinde** ise bekleyen veya devam eden mevcut Implanner siparişlerinden beklenen ürün sayısını gösterir.

<!-- l10n:b-8fea0b171c65 -->
![Üretim ve ameliyat durumuyla koloni özeti](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Her implant için yine ayrı bir ameliyat gerekir. Gruplama, birkaç implant tek anestezi uykusunda takılabilsin diye ameliyatı geciktirir; böylece ayrı ayrı anestezi uygulanma sayısı azalır. Grup, işleme stratejisine göre kolonicinin sonraki ihtiyacının tümüdür: mevcut kademe veya bütün plan ([ameliyat seçenekleri](topic:surgery-options) konusuna bakın). Bir strateji gruplamayı tamamen atlar ve her implantı ayrılıp yerinde olur olmaz planlar.

<!-- l10n:b-6b9f966652bc -->
Ameliyatlar kolonici uygun olana kadar bekler. Koloniler birbirinin stoğundan eşya almaz: eşyalar kolonicinin yaşadığı yerde ayrılıp takılır.
