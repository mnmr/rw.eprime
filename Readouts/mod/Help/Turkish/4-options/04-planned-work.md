---
title: Planlanan işler
---
Planlanan iş seçenekleri, sipariş veya inşaat planlarının ayırdığı malzemeleri çıkarır.

- Siparişler için kaynak ayır, bekleyen siparişlerin henüz tüketeceği malzemeleri çıkarır. Bir sipariş, gereksinim tam olarak tek biçimde karşılanabiliyorsa malzeme ayırır. "Herhangi bir deri" gibi açık seçenekler hiçbir şey ayırmaz. "X kez yap" X üretim, "X olana kadar yap" hedefe ulaşacak kadar, "sonsuza kadar yap" bir üretim ayırır. Duraklatılmış siparişler ayırmaz.
- Yapılar için kaynak ayır, plan ve yarım iskeletlerin kalan malzemelerini çıkarır. İskelete teslim edilmiş veya kolonici tarafından teslim edilmek üzere alınmış malzeme harcanmış sayılır, ikinci kez çıkarılmaz. Yasaklı planlar yok sayılır.
- Eksikleri negatif sayılarla göster, planlanan iş elinizdekini aşarsa sıfırda durmak yerine açığı gösterir.
- Quality Jobs desteği, kalite hedefine ulaşmak için gereken denemeleri tahmin eder ve hepsine kaynak ayırır. EPrime's Quality Jobs kurulu değilse veya çok eskiyse satır soluktur; nedeni üzerine gelince görünür.

Eksikleri negatif sayılarla göster açıkken planlanan işin sıfır altına ittiği sayaç açığı turuncu gösterir:

![Negatif sayılar açıkken malzemeler](negatives-on.png)

Kapalıyken aynı yuvalar sıfırda durur:

![Negatif sayılar kapalıyken aynı yuvalar](negatives-off.png)

Planlanan işler yaklaşık her on yedi saniyelik oyun zamanında taranır; yeni siparişin sayılarda görünmesi bu kadar sürebilir.
