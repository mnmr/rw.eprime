---
title: Desteklenen modlar
---
<!-- l10n:b-3d8630b508cc -->
Oyunun ameliyat verisini kullanan her implant modu ayarsız çalışır. Oyuncular Expanded Prosthetics and Organ Engineering ve CeleTech'i doğruladı; implantları temel oyun içeriği gibi okunur. Aşağıdaki modlar verilerine göre tek tek kontrol edildi.

<!-- l10n:b-fc34121549e7 -->
![Gövde listesinde seçilmiş mekanit türü](mechanites-picked.png)

<!-- l10n:b-8e0b09ad2325 -->
![Seçilmiş türün yerini alacağını belirten diğer tür satırları](mechanites-overrides.png)

<!-- l10n:b-8115e58333ac -->
**[FSF] Advanced Bionics Expansion**: mekanit türleri birbirini dışlar. Birini enjekte etmek vücuttaki diğer türü çıkarır; seçici hepsini tek yuva kabul eder ve yerine geçme açıklaması çıkarılacak türü adlandırır. Bağışıklık artırıcı mekanitler tür sayılmaz, her türle birlikte bulunabilir. İleri biyonik mesane ve ileri hijyen geliştirici, Dubs Bad Hygiene ile birlikte mod uyumluluğu seçeneklerinin kapsamındadır.

<!-- l10n:b-951da050479a -->
**Dubs Bad Hygiene**: biyonik mesane ve hijyen geliştirici, oyunun [FSF] Advanced Bionics Expansion'ın ileri sürümleriyle birlikte takılmasına izin verdiği gövde implantlarıdır. İki mod uyumluluğu seçeneği Implanner'ın da buna izin verip vermeyeceğini belirler.

<!-- l10n:b-41bdd507c8fd -->
![Seçili modüler biyonik gözün yerini alma açıklamalı normal ve ileri göz satırları](modular-eye.png)

<!-- l10n:b-d5d8d7431467 -->
**Integrated Implants**: her yana bir ek kol takılabilir; aynı yanda ikinci türü seçmek ilkinin yerini alır. Deri altı zırh ve prestij sürümü, omuz taretleri, kuyruklar ve yerleştirilmiş patlayıcılar, her biri yalnızca birinin takılabileceği gruplar oluşturur. Modüler biyonikler yerinde yükseltmedir: modüler biyonik göz seçerseniz otomasyon, kolonicide yoksa önce biyonik göz takar; ardından bir bileşen tüketen ve ikinci göz gerektirmeyen yükseltme ameliyatını yapar. Bu yükseltmeler EBSG Framework gerektirir ve Medical System Expansion 2 yüklüyken sunulmaz. Modüllerin kendileri envanterden ameliyatsız takıldığından listelenmez.

<!-- l10n:b-261a620cdad5 -->
**Vanilla Genetics Expanded**: kolonicilere yönelik melez implantları diğerleriyle listelenir. Implanner yalnızca kolonicileri planladığından hayvan implantları listelenmez.

<!-- l10n:b-8b2ffcc18549 -->
Bazı melez implantlar vücut parçasının yerini alır: kış uykusu modülü beynin yerini alır. Bazıları parçanın üstünde durur: nöron takviyesi beyinde bulunur. Normal implantlar, üstünde melez implant bulunan parçaya takılabilir; ancak değiştirilmiş parçaya takılamaz.

<!-- l10n:b-cd881c381961 -->
Herhangi bir melez implantın takılması, o parçadaki tüm takılı implantları dışarı çıkarır. Çıkanlar doktorun yanına düşer.

<!-- l10n:b-ad1a61d0da90 -->
- Implanner önce melez implantı takar; böylece çıkarılacak bir şey olmaz.
- Kolonicinin orada implantları zaten varsa Implanner düşmelerine izin verir, aynı kolonici için ayırır ve hemen ardından yeniden takar.
- O parçadaki bir şey kalıcı kaybolacaksa Implanner melez implantı asla takmaz.

<!-- l10n:b-c282558e897d -->
Melez implantlar, yapıldıkları gen iskeletinin kalitesini alır ([asgari kalite](topic:minimum-quality) konusuna bakın).

<!-- l10n:b-721ad339d2c2 -->
**Quality Bionics Remastered**: biyoniklerin kalitesi vardır; iyi biyonik daha iyi çalışır ve daha fazla cana sahiptir. İmplant ipucu berbat ile efsanevi arasındaki parça verimliliğini gösterir; her plan takacağı en düşük kaliteyi belirler ([asgari kalite](topic:minimum-quality) konusuna bakın).

<!-- l10n:b-7a2e3799838c -->
**Elite Bionics Framework**: vücut parçasının canını artıran veya azaltan implantlar değişikliği ipuçlarında gösterir.

<!-- l10n:b-c7cd0a958354 -->
![Bacak modülü gereksinim penceresi](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![Plana ekle sonrası biyonik bacak ve modülün birlikte seçimi](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
**Bionic modularity**: her modül parçasında bir yuva kaplar; aynı kola bir savaş ve bir iş modülü sığar, iki savaş modülü sığmaz. Uzuv modülleri biyonik uzva takılıp parçayı paylaşır; planlanan biyonik kol ve kol modülü birlikte bulunabilir. Planda o parçada biyonik yokken uzuv modülü seçmek, onu taşıyabilecek biyonikleri gösteren pencere açar; varsayılan en az gelişmiş taşıyıcıdır. **Plana ekle** seçiminizi modülle birlikte ekler. **İptal** hiçbir şey eklemez.
