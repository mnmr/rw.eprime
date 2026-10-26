---
title: Desteklenen modlar
---
Oyunun ameliyat verisini kullanan her implant modu ayarsız çalışır. Oyuncular Expanded Prosthetics and Organ Engineering ve CeleTech'i doğruladı; implantları temel oyun içeriği gibi okunur. Aşağıdaki modlar verilerine göre tek tek kontrol edildi.

![Gövde listesinde seçilmiş mekanit türü](mechanites-picked.png)

![Seçilmiş türün yerini alacağını belirten diğer tür satırları](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion**: mekanit türleri birbirini dışlar. Birini enjekte etmek vücuttaki diğer türü çıkarır; seçici hepsini tek yuva kabul eder ve yerine geçme açıklaması çıkarılacak türü adlandırır. Bağışıklık artırıcı mekanitler tür sayılmaz, her türle birlikte bulunabilir. İleri biyonik mesane ve ileri hijyen geliştirici, Dubs Bad Hygiene ile birlikte mod uyumluluğu seçeneklerinin kapsamındadır.

**Dubs Bad Hygiene**: biyonik mesane ve hijyen geliştirici, oyunun [FSF] Advanced Bionics Expansion'ın ileri sürümleriyle birlikte takılmasına izin verdiği gövde implantlarıdır. İki mod uyumluluğu seçeneği Implanner'ın da buna izin verip vermeyeceğini belirler.

![Seçili modüler biyonik gözün yerini alma açıklamalı normal ve ileri göz satırları](modular-eye.png)

**Integrated Implants**: her yana bir ek kol takılabilir; aynı yanda ikinci türü seçmek ilkinin yerini alır. Deri altı zırh ve prestij sürümü, omuz taretleri, kuyruklar ve yerleştirilmiş patlayıcılar, her biri yalnızca birinin takılabileceği gruplar oluşturur. Modüler biyonikler yerinde yükseltmedir: modüler biyonik göz seçerseniz otomasyon, kolonicide yoksa önce biyonik göz takar; ardından bir bileşen tüketen ve ikinci göz gerektirmeyen yükseltme ameliyatını yapar. Bu yükseltmeler EBSG Framework gerektirir ve Medical System Expansion 2 yüklüyken sunulmaz. Modüllerin kendileri envanterden ameliyatsız takıldığından listelenmez.

**Vanilla Genetics Expanded**: kolonicilere yönelik melez implantları diğerleriyle listelenir. Implanner yalnızca kolonicileri planladığından hayvan implantları listelenmez.

Bazı melez implantlar vücut parçasının yerini alır: kış uykusu modülü beynin yerini alır. Bazıları parçanın üstünde durur: nöron takviyesi beyinde bulunur. Normal implantlar, üstünde melez implant bulunan parçaya takılabilir; ancak değiştirilmiş parçaya takılamaz.

Herhangi bir melez implantın takılması, o parçadaki tüm takılı implantları dışarı çıkarır. Çıkanlar doktorun yanına düşer.

- Implanner önce melez implantı takar; böylece çıkarılacak bir şey olmaz.
- Kolonicinin orada implantları zaten varsa Implanner düşmelerine izin verir, aynı kolonici için ayırır ve hemen ardından yeniden takar.
- O parçadaki bir şey kalıcı kaybolacaksa Implanner melez implantı asla takmaz.

Melez implantlar, yapıldıkları gen iskeletinin kalitesini alır ([asgari kalite](topic:minimum-quality) konusuna bakın).

**Quality Bionics Remastered**: biyoniklerin kalitesi vardır; iyi biyonik daha iyi çalışır ve daha fazla cana sahiptir. İmplant ipucu berbat ile efsanevi arasındaki parça verimliliğini gösterir; her plan takacağı en düşük kaliteyi belirler ([asgari kalite](topic:minimum-quality) konusuna bakın).

**Elite Bionics Framework**: vücut parçasının canını artıran veya azaltan implantlar değişikliği ipuçlarında gösterir.

![Bacak modülü gereksinim penceresi](requirement-prompt.png)

![Plana ekle sonrası biyonik bacak ve modülün birlikte seçimi](requirement-added.png)

**Bionic modularity**: her modül parçasında bir yuva kaplar; aynı kola bir savaş ve bir iş modülü sığar, iki savaş modülü sığmaz. Uzuv modülleri biyonik uzva takılıp parçayı paylaşır; planlanan biyonik kol ve kol modülü birlikte bulunabilir. Planda o parçada biyonik yokken uzuv modülü seçmek, onu taşıyabilecek biyonikleri gösteren pencere açar; varsayılan en az gelişmiş taşıyıcıdır. **Plana ekle** seçiminizi modülle birlikte ekler. **İptal** hiçbir şey eklemez.
