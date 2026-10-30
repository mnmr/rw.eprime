---
title: Tamamlayıcıları seçme
---
<!-- l10n:requirements -->
Bu ayarları bir iş emrinde veya yapıda kullanın. Önceki aşamaları diğer uygun çalışanlar yapabilir; gereksinimlerinizi yalnızca son dokunuşu yapan kişinin karşılaması gerekir.

<!-- l10n:controls -->
- **Tamamlayıcı becerisi**, asgari beceri düzeyini belirler.
- **{k:QJ_AutoBest}**, çalışanları elle belirlediğiniz asgari beceri düzeyi yerine beklenen kaliteye göre seçer. Bu seçeneği kapattığınızda kullanmanız için elle ayarlama kaydırıcısı korunur.
- **{k:QJ_RequireInspired}**, bu ilhamın gelmesini bekler. İşin tamamlanması, her zamanki gibi ilhamı tüketir.
- **{k:QJ_RequireSpecialist}**, üretim kalitesine bonus veren bir Ideology rolü gerektirir. Bu seçenek Ideology ile görünür.

<!-- l10n:odds-image -->
![Tamamlayıcı gereksinimleri ve kalite olasılıkları önizlemesi](finisher-options.png)

<!-- l10n:best -->
Otomatik seçim, uygun kolonicileri, koloni mekanoidlerini ve desteklenen robotları karşılaştırarak beklenen kalitesi en yüksek çalışanı seçer. Beceri, yaratıcılık ilhamı, üretim uzmanı bonusları ve desteklenen modların kalite bonusları hesaba katılır. Bu, ortalama kaliteyi en yüksek düzeye çıkarır; ancak tek bir rastgele kalite sonucunu garanti etmez.

<!-- l10n:availability -->
Diğer koşulları karşılayan çalışanlar uzaktayken, yere düşmüşken, uyurken, ruhsal çöküntü yaşarken veya şarj olurken karşılaştırmada kalır. İstasyonlarındaki robotlar da buna dahildir. En üst sıradaki çalışanların hiçbiri bu haritada işi bitiremiyorsa iş bekler. Örneğin, bir kervanda bulunan daha iyi bir zanaatkâr, kolonideki daha alt sırada yer alan bir çalışanın işi tamamlamasını engelleyebilir. Bekleme süresi dolunca kalite gereksinimini düşüren bir zaman aşımı yoktur.

<!-- l10n:manual-threshold -->
**{k:QJ_AutoBest}** kapalıyken, elle belirlediğiniz asgari beceri düzeyini ve diğer gereksinimleri karşılayan, işi yapabilecek herhangi bir çalışan işi tamamlayabilir. Koloninin başka bir yerindeki daha iyi bir çalışan onu engellemez.

<!-- l10n:robot-finishers -->
Mekanoidler ve desteklenen robotlar, kolonicilerle aynı tamamlayıcı gereksinimlerini karşılamalıdır. Yaratıcılık ilhamı veya üretim uzmanı rolü gerektiren ayarlar, bunlara sahip olmayan çalışanları yine kapsam dışında bırakır. **{k:QJ_SettingsAllowRobotPreparation}** seçeneğini kapatmak, önceki aşamaları kolonicilere ayırır; uygun mekanoidler ve robotlar yine de Quality Jobs tamamlama görevlerini üstlenebilir.

<!-- l10n:odds -->
Bir iş emrinin **{k:QJ_OddsHeader}** tablosundaki **{k:QJ_OddsColConfig}**, asgari tamamlayıcı beceriniz ve gerekli bonuslar için olasılıkları tahmin eder. **{k:QJ_AutoBest}** etkinken uygun bir aday bulunursa, bunun yerine koloni genelinden seçilen bu çalışanın olasılıklarını gösterir. Aday yoksa elle belirlediğiniz ayarları kullanır; bu, uygun bir tamamlayıcının mevcut olduğu anlamına gelmez.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}**, ek tamamlayıcı gereksinimlerinizi dikkate almadan bu haritada işi yapabilecek en üst sıradaki çalışanın olasılıklarını gösterir. Bu nedenle iki sütun farklı çalışanları gösterebilir. Bunlar olasılıktır, garanti değildir; sonucu, işi gerçekten tamamlayan çalışan belirler.

<!-- l10n:eligible-finishers -->
**Uygun tamamlayıcılar**, bu haritada şu anda koşulları karşılayabilen çalışanları listeler; uzakta olan, yere düşmüş veya zihinsel çöküntü yaşayan çalışanlar bu listeye dahil edilmez. Üretim emirlerinde liste, seçili emrin gereksinimlerini kullanır; ancak kuyruk sayıları, bu haritada aynı ürünü üreten diğer emirleri de kapsar.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}**, atanan tamamlama işlerini yangın söndürme, tıbbi bakım, kurtarma ve yatak istirahati dışındaki işlerin önüne alır. Mevcut işi kesintiye uğratmaz. Kapalıyken tamamlama işi, daha yüksek öncelikli iş türlerinden sonra, kendi iş türü içinde ilk sırada gelir. Çalışan, eşyanın normal iş türünü yapabilmelidir.

