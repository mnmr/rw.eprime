---
title: Üretim emri ayarlama
---
<!-- l10n:open -->
Kalite derecesi olan bir eşyanın iş emrinde **Ayrıntılar** bölümünü açın. Bitmemiş eşya bırakan desteklenen tariflerde **{k:QJ_QualityPanelTitle}** paneli, normal iş emri ayarlarının yanında görünür. Kalite derecesi veya bitmemiş iş aşaması olmayan tarifler normal şekilde çalışmaya devam eder.

<!-- l10n:bill-image -->
![Quality Jobs paneliyle birlikte bir üretim tezgâhı iş emri](crafting-bill.png)

<!-- l10n:setup -->
**{k:QJ_ManageBill}** seçeneğini etkinleştirin, ardından [tamamlayıcı gereksinimlerini](topic:finishers) seçin. Üretimin çoğunu sıradan çalışanlar yapabilir. Mevcut çalışan gereksinimleri karşılıyorsa işi doğrudan tamamlar; karşılamıyorsa bitmek üzere olan eşya bir kenara bırakılır ve iş emri, stok sınırı içinde başka eşyaların üretimine başlayabilir.

<!-- l10n:dispatch -->
Bir tamamlayıcı gereksinimleri karşıladığında Quality Jobs, ona atanmış geçici bir **Tamamla …** iş emri oluşturur. Tamamlayıcı, bitmemiş eşyayı alıp tamamlar. Ardından geçici iş emri kendiliğinden silinir. Bekleyen eşyalar, diğer bitmemiş eşyalar gibi taşınıp depolanabilir.

<!-- l10n:finisher-bill-restrictions -->
Asıl iş emrinin azami beceri seviyesi ve belirli bir çalışana atanmış olması, eşya tamamlanmak üzere bir kenara ayrılana kadar geçerlidir. İşi bir acemiye atayabilir, özel tamamlama emrini ise daha yetenekli bir koloniciye, koloni mekanizmasına veya desteklenen bir robota bırakabilirsiniz. Tamamlayıcıların yine de tarifi uygulayabilmesi ve Quality Jobs gereksinimlerini karşılaması gerekir.

<!-- l10n:status -->
**{k:QJ_StatusHeader}** bölümü, bu haritadaki tüm iş emirleri ve tezgâhlarda bu ürünün takip edilen bitmemiş eşyalarını sayar: paylaşılan **Yarım** işler, tamamlayıcı **Bekleyen** eşyalar ve **Bitirilen** aşamasına atanmış eşyalar. Tamamlayıcı listesi ve uyarı, seçili iş emrinin gereksinimlerini esas alır; aynı ürünü üreten diğer iş emirlerinin gereksinimleri farklı olabilir. Ayrıntılar için durumun ve tamamlayıcı listesinin üzerine gelin. Hiçbir iş ilerlemiyorsa [işlerin ne zaman beklediğine](topic:waiting) bakın.

<!-- l10n:disable -->
İş emrinin yeniden normal şekilde tamamlanması için **{k:QJ_ManageBill}** seçeneğinin işaretini kaldırın. Paylaşım etkinken, bu emrin paylaşılan işlerine diğer çalışanlar devam edebilir.

