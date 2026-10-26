---
title: Stok koruma
---
İki ayar otomasyonun stoklarınızı tüketmesini önler.

**Stokta tut**, üretim için kaynak alt sınırlarını belirler. Bir üretim siparişi ancak koloni, sıradaki siparişlerinin kalan malzemelerine ek olarak onun maliyetini karşılayıp her malzemeden ayarlanan miktarı tutabiliyorsa eklenir. Çelik için 2000, implant üretiminin çeliği asla 2000'in altına indirmemesi demektir. Malzeme harcarsanız ve kimsenin başlamadığı bir sipariş artık bu koşula uymazsa Implanner onu kaldırır; stok geri geldiğinde yeniden ekler.

![Stokta tutma kaynak alt sınırları](keep-in-stock.png)

**Manuel kullanım için ayrılan implantlar**, bitmiş implantları manuel kullanıma saklar. Bir biyonik kol ayırırsanız otomasyon yalnızca ilkinden fazlasını atar; biri acil ameliyat veya ticaret için serbest kalır. Üretim, manuele ayrılmış eşyayı hâlâ eksik sayarak stoğu yeniler. Yükseltme siparişleri de manuele ayrılmış veya planlı ameliyatlara gereken implantlara dokunmaz. Yalnızca fazlalık implantlar üretim malzemesi olabilir.

![Bir biyonik kolu manuele ayıran implant rezervi](implant-reservations.png)
