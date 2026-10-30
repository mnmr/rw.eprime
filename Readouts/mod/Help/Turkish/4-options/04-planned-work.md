---
title: Planlanan işler
---
<!-- l10n:b-7aa1a59c7379 -->
Planlanan iş seçenekleri, siparişlerin veya inşaat planlarının tahmini malzeme gereksinimlerini görüntülenen sayılardan çıkarır.

<!-- l10n:b-ee40b81e925d -->
- Siparişler için kaynak ayır, bekleyen üretim siparişlerinin gerektirdiği malzemeleri çıkarır. Bir malzeme, yalnızca sipariş o malzeme için tam olarak tek bir kaynak türüne izin veriyorsa hesaba katılır. Örneğin, herhangi bir deriye izin verilirse hiçbir şey ayrılmaz; yalnızca tek bir deri türüne izin verilirse o derinin maliyeti hesaba katılır. "X kez yap" X üretim, "X olana kadar yap" hedefe ulaşacak kadar, "sonsuza kadar yap" bir üretim ayırır. Duraklatılmış siparişler kaynak ayırmaz.
- Yapılar için kaynak ayır, plan ve yarım iskeletlerin kalan malzemelerini çıkarır. İskelete teslim edilmiş veya kolonici tarafından teslim edilmek üzere alınmış malzeme harcanmış sayılır, ikinci kez çıkarılmaz. Yasaklı planlar yok sayılır.
- Eksikleri negatif sayılarla göster, planlanan iş elinizdekini aşarsa sıfırda durmak yerine açığı gösterir.
- Quality Jobs desteği, etkin sipariş ve yapı kaynak ayırma seçeneklerini Quality Jobs tarafından yönetilen kalite hedeflerine göre uyarlar. Tekrarlanan denemeler için gereken malzemeleri tahmin eder ve inşaat malzemesi iadelerini hesaba katar. Kullanmak için yukarıdaki ilgili kaynak ayırma seçeneğini etkinleştirin. EPrime's Quality Jobs kurulu değilse veya çok eskiyse satır soluktur; nedeni üzerine gelince görünür.

<!-- l10n:planned-work-tooltip-columns -->
Planlanan işler tablosunu görmek için planlanan iş kesintileri olan bir kaynağın veya kaynak havuzunun üzerine gelin. Kalan iş, tarifin kaç kez uygulanacağını veya inşa edilecek hedef sayısını gösterir. Deneme başına maliyet, tek bir denemenin malzeme maliyetidir. Çıkarılan, gösterilen sayıdan düşülen miktardır; inşaata teslim edilmiş veya taşınmakta olan malzemeleri ve Quality Jobs desteği açıkken tahmini kalite denemelerini hesaba katar.

<!-- l10n:b-a4ac1a2f64dc -->
Eksikleri negatif sayılarla göster açıkken planlanan işin sıfır altına ittiği sayaç açığı turuncu gösterir:

<!-- l10n:b-b209bc9139c1 -->
![Negatif sayılar açıkken malzemeler](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Kapalıyken aynı yuvalar sıfırda durur:

<!-- l10n:b-a997f13279d8 -->
![Negatif sayılar kapalıyken aynı yuvalar](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Planlanan işler, oyun zamanında birkaç saniyede bir kaynak sayılarıyla birlikte güncellenir. Yeni veya değiştirilmiş siparişler bir sonraki güncellemede görünür; oyun zamanının ilerlemesi için oyunu duraklatmadan çıkarın.
