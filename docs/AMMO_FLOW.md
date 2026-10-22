# Kooperatif mühimmat akışı

Her sefer **12 gülleyle** başlar: biri topun içindedir, 11'i **AMMO** istasyonundadır. İskelede hazırlık sırasında da stok taşınabilir. Yeni sefer bütçeyi 12'ye sıfırlar; mühimmat kariyer kaydına girmez, canlı seferin migration snapshot'ına girer.

## Oynanış

- Boş ellerle AMMO'ya bakıp **E / A**: bir gülle al. Elde gülleyken aynı istasyon: ambara geri bırak.
- Gülle taşırken CANNON'a bakıp **E / A**: top yanındaki stoğa teslim et. Topçu görevini çalmaz; topçu doldurmaya devam edebilir.
- Top yanında üç stok yuvası vardır. Doldurulmakta olan gülle, iptal edilirse geri dönebilmesi için bir yuva ayırır. HUD bunu `+1 reserved` olarak belirtir.
- Topçu **R / X** ile teslim edilmiş bir gülleyi 2,5 saniyede doldurur. Stok boşsa R ambardan gülle çekmez. **LMB / RT** ateş eder; **E / A** istasyonu bırakır.
- Gülle taşıyan oyuncu %20 yavaş yürür; servis, dümen, tamir, yükseltme veya yolcu taşıma için önce gülleyi teslim/geri vermelidir. Gülleyle karaya geçilemez. Gülle atma veya elden ele fırlatma bu dilimde yoktur.
- Solo oyuncu limanda üç gülle hazırlayabilir veya topu bırakıp kendisi taşıyabilir. Birden fazla kişi aynı anda teslim edebilir; kapasite kontrolü host'ta atomiktir.

Hesap invariant'ı:

`kalan toplam = ambar + top yanı stok + oyuncuların elleri + doldurmada ayrılan 1 + topa yüklenmiş 1`

Toplam yalnız ateşte azalır. İptal/ayrılmada doldurulan gülle top yanı stoğa, eldeki gülle ambara döner. Yeni bir seferin ücretsiz restock'u dışında gülle yaratılmaz.

## Bağlantı ve sürüm

Geçici çevrimiçi kopmada kalıcı oyuncu kimliği/slotu korunur; eski bağlantı kimliği geçersizleştirilir, görevler ve yükler serbest bırakılır. Yeniden bağlanan oyuncu eski slotunu alabilir ama yerine geçen topçunun görevini çalmaz. Karadaki kopan oyuncu gemiye alınarak kalkışı kilitlemez. Saklanan/henüz yeniden bağlanmamış slotların dünya görselleri gizlidir. Lobby üyeliği hâlâ kendi removal ayarına tabidir; farklı cihazda yeniden bağlanma kabulü ayrıca yapılmalıdır.

Snapshot ve NGO protokolü **4**'tür. Eski v3 snapshot reddedilir; tüm oyuncular aynı güncel build'i kullanıp web sayfasını yenilemelidir. İstasyon 10'a AMMO eklenir; eski 0–9 istasyon sırası ve sahne kimlikleri korunur.

Bu dilim fiziksel **taşıma/teslim/doldurma** akışıdır. Atışların hasarı hâlâ anlık ray hesabıdır; uçuşlu/yerçekimli mermi ve çarpışma sonraki dilimin açık işidir.

## Doğrulama

- WebGL kaynak `c039e55`, `gh-pages` paketi `b7352e9`; Unity build exit 0 (`Logs/AmmoFlow-WebGLBuild.log`) ve Pages deployment `39714573200` success. Canlı tarayıcıda gülle modeli/HUD, Relay host açılışı ve Leave ile menüye dönüş görüldü; konsolda error yoktu (`Logs/WebGL-AmmoFlow-Verified.png`). Bu, dört tarayıcı veya host kaybı kabulü değildir.
- Unity **6000.4.6f1**, son departure-kuyruğu düzeltmesiyle **139/139 EditMode**, exit 0: `Logs/AmmoFlow-QueueFixed.xml`. Kuralların yanında gerçek sahnede gülle alma, görünür taşıma şekli, top stoğu ve doldurma RPC'leri sınanır.
- Bir host ve üç ayrı Windows istemcisi: `Logs/VoyageSmoke-20261022-045947-daa488/`. Uzaktaki tedarikçi iki gerçek gülle teslim etti, topçu korsanı yendi; yolcu/liman/yeni sefer akışı tamamlandı. Dört süreçte `SHIP_SMOKE_PASS`, exit 0 ve **168 kasa / 23 yiyecek / 24 içecek / 20 koruma**; yönetici `SHIP_VOYAGE_TEST_PASS` verdi. Tekrar çalıştırma: `.\Scripts\Test-Voyage.ps1`.
- Relay/WSS probe'u, ilk host'un **elinde gülle varken** onu zorla kapatır. `Logs/RelayMigration-20261022-050905-35f538/` koşusunda kalan üç oyuncu kendi slotlarına döndü, kasa 321/olay 777 korundu, toplam 12/ambar 11/top yanı 0 kaldı ve zıplama RPC'si yeni host'a ulaştı. İlk `050242-1f61a3` koşusu 180 saniyede zaman aşımına uğramıştır; nedeni kesinleştirilmedi ve raporu korunur. Sonraki koşu tanı kayıtlarıyla, değişmeyen oyun kurallarıyla geçti; bu, ilk timeout'un çözüldüğünü kanıtlamaz.
- SDK `MigrationFailed` bildirirse HUD açık hata ve oturumdan çıkış yolu gösterir; seferi sessizce sıfırlamaz. Başarılı sonraki bildirim hata durumunu temizler. Probe SDK hatasını ve ağ aşamasını kaydeder; supervisor ve uygulama timeout'u ortak `-TimeoutSeconds` değerini kullanır (varsayılan 240, sınır 60–300).
- Eski **859** serialized nesne/component ID'si ve ad/parent/local transform/anchor bağları korunmuştur; **49** kayıt eklendi. Genel kadraj `Logs/AmmoFlow-Layout.png` içinde incelendi. Elde taşıma şekli sahne/RPC testindedir; bu görüntü HUD veya eldeki güllenin yakın planı değildir.

Bağlantı-suspension ve hata bildirimi eklendikten sonraki ikinci dört süreçli sefer koşusu da geçti: `Logs/VoyageSmoke-20261022-052918-6270a6/`, dört exit 0/`SHIP_SMOKE_PASS`, tedarikçi iki teslimat, son ortak durum **167 kasa / 23 yiyecek / 24 içecek / 20 koruma**. Önceki koşudan bir coin fark, zamana bağlı yolcu bahşişidir; test her istemcinin aynı server hesabını aldığını ve alışverişte tam 54 düşüldüğünü doğrular.

Son incelemede, SDK ayrılma bildirimi sefer simülasyonu hazır olmadan geldiğinde kaydın uygulanmadan silinebildiği bir yarış penceresi bulundu. Yeni `DepartureBeforeVoyageSpawnIsQueued` testi eski davranışta **başarısızdır** (`Logs/DepartureQueue-Before.xml`: beklenen kayıt yerine boş kuyruk). Düzeltme kaydı tutar; host/scene/simulation hazır olduğunda sonraki Update veya migration bildirimiyle uygular. İlk Relay timeout'una kesin neden olduğuna dair kanıt yoktur. Bu son değişiklik için gerçek Relay tekrarı ayrıca kaydedilmelidir.

Son v4 tekrar koşusu `Logs/RelayMigration-20261022-055219-3c98a5/` **başarısızdır**: yeni host oluşup sefer/olay/mühimmat korunmasına rağmen client1 slot 3 yerine 0'a bağlandı; client3 henüz bağlanmamıştı. Bu sonuç önceki başarılı koşuyu geçersiz kılmaz fakat güvenilir migration kabulünü açık tutar. Kimlik eşleme tanısı eklendi. Probe artık üç JSON sonucu doğrulanmadan host'u kapatmayan supervisor bariyeri kullanır; bu test altyapısı düzeltmesi tek başına oyun hatasının giderildiği kanıtı değildir.

Tanılı `060630-49c5bb` tekrarında üç katılımcı/supervisor başarılı çıktı; hemen sonraki aynı build `060930-5e5fc8` koşusu zaman aşımına uğradı. Yeni host logunda sırasıyla slot 0, **2 ve kendi slotu 3** için Lobby `PlayerHasLeft` üzerinden silme; ardından geri gelen peer için `previousSlot=-1` ve yanlış slot 0 görülür. Bu, kayıp kimlik kaydının yeniden bağlanmadan önce ayrılma işleyicisinde silindiğini gösterir. SDK `LobbyHandler.OnLobbyChanged`, indeksli ayrılma bildirimlerini sürüm kontrolünden önce hesaplar; yalnız yeni sürümde roster'ı uygular. Bu yol eski/tekrarlı deltanın güncel bir üyeyi yanlış işaretlemesiyle uyumludur; wire-level delta kaydı alınmadığı için SDK içindeki kesin olay sırası kanıtlanmış sayılmaz.

Proje düzeltmesi SDK kaynaklarını değiştirmez: bekleyen ayrılmalar güncel `ISession.Players` listesiyle uzlaştırılır. Hâlâ oturumda olan oyuncunun kalıcı slotu ve admission kaydı korunur; gerçekten listeden ayrılmış kişi temizlenir. `DepartureReconciliationPreservesCurrentMembersAndRemovesAbsentPlayers` gerçek sahnede hem bağlı/henüz rebind olmamış üyeyi hem gerçek ayrılmayı sınar. Ayrı test kopyasında yalnız üyelik koruması kapatılınca yeni test **başarısız** (`Logs/RelayRoster-NoGuard.xml`, beklenen Active true, gerçek false; exit 2). Doğru kaynak geri yüklendikten sonra **140/140**, exit 0 (`Logs/RelayRoster-Final.xml`); Windows build exit 0 (`Logs/RelayRoster-Final-WindowsBuild.log`). İnternet tekrarı ayrıca gereklidir.

Üyelik korumalı Windows build'iyle `Logs/RelayMigration-20261022-061932-a64f7b/` **başarılıdır**: client2 slot 3'te yeni host, client1 slot 2, client3 slot 1; üç katılımcı exit 0, kasa 321/olay 777/aktif ekip 3, mühimmat 12/11/0 ve yeni authority üzerinden zıplama. Supervisor `SHIP_RELAY_MIGRATION_PASS`, exit 0. Bu koşuda eski üyeyi yanlış işaretleyen callback görülmedi; korumanın negatif regresyonu deterministik testte kanıtlandı, internet soak tekrarı sürdürülür.

Dört insanla tempo/denge, farklı cihazlar, gerçek yeniden bağlanma ve tarayıcı pointer-lock kabulü otomatik testlerin yerine geçmediği ayrı kapılardır.
