---
title: Üretim seçenekleri
---
<!-- l10n:b-bdd2b459773e -->
**Eksik implantlar için otomatik üretim siparişi ekle** açıkken Implanner, eksik implantları yapabilen tezgâhlara sipariş koyar ve ihtiyaç kalmayınca kendi siparişlerini kaldırır. Sizin oluşturduğunuz siparişleri asla kaldırmaz. Her sipariş, ameliyatta takılma sırasına göre bir adet üretir: Tam setlerde, planda bacaklar üst sıradaysa kolonicinin bacakları kollarından önce gelir.

<!-- l10n:b-8effc9e9b828 -->
Aynı implantı birkaç tarif üretebiliyorsa Implanner şu anda maliyeti karşılanabilen kullanılabilir yolu seçer. Serbest bir temel implant yükseltilebilir; yoksa doğrudan tarif, malzemelerden ileri implantı yapabilir. Yalnızca yükseltme yolu açıksa ara ürün üretimi önce temel implantı yapabilir. Tarif araştırılmış olmalı ve uygun tezgâh tarafından desteklenmelidir.

<!-- l10n:b-0aa3ded1d655 -->
İmplant üretimine özel tezgâh ayırmak için tezgâhın Siparişler sekmesinde **Implanner tezgâhı** kutusunu işaretleyin. Kutu yalnızca implant üretebilen tezgâhlarda görünür. Mevcut siparişler duraklatılır ve Implanner kendi üretim siparişlerini diğer tezgâhlardan önce buraya koyar. İşaretliyken sipariş ekleyemez, yapıştıramaz, siparişleri duraklatamaz veya sürdüremezsiniz.

<!-- l10n:b-a4d04c1bceb2 -->
![Implanner'a devredilmiş üretim tezgâhı](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Kontrolü geri almak için işareti kaldırın. İşaretleme sırasında zaten duraklatılmış siparişler öyle kalır, diğerleri sürdürülür. Tezgâh işaretliyken Implanner'ı kaldırırsanız tezgâh çalışır ama siparişleri kendiniz sürdürmelisiniz.

<!-- l10n:b-c9f0dad1a5c5 -->
![Üretim seçenekleri](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Sipariş verilebilecek tezgâh sayısı**, koloni başına aynı anda implant üzerinde çalışan tezgâhları sınırlar. Bir tezgâh en fazla iki Implanner siparişi tutar; ilki bitince sıradaki hazırdır. Implanner tezgâhları da sınıra dâhildir.
- **Yalnızca boş tezgâhlarda sipariş oluşturmaya izin ver**, bekleyen işi olan tezgâhlara implant siparişi koymaz; kendi kuyruklarınız önceliğini korur.
- **Yalnızca Implanner tezgâhlarını kullan**, implant siparişlerini bunlarla sınırlar; böyle tezgâhı olmayan koloni implant üretmez. Kapalıyken her Implanner tezgâhında iki sipariş olduktan sonra diğerleri kullanılır.
- **Üretim siparişleri için gereken zanaat becerisi**, pahalı malzemeleri düşük becerili zanaatkârlardan korur.
- **Eksik ara ürünler için üretim siparişine izin ver**, üretim zincirindeki eksikleri izler: sonraki biyonik bacak için bir ileri bileşen eksikse önce bileşen siparişi verilir. İmplant plastiçelik gibi ham kaynak beklerken boş tezgâhlar ihtiyaç duyacağı ileri bileşenleri üretir. Bileşenler ve temel implantlar gibi mamuller uygundur. Çelik gibi ham kaynaklar, eritme tarifiyle üretilebilseler bile üretilmez; onları kendiniz toplamalısınız.

<!-- l10n:b-24089426e9a9 -->
Her sipariş stokta tutma alt sınırlarına da uyar ([stok koruma](topic:keeping-stock) konusuna bakın).
