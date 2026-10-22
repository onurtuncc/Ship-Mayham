# Relay/WSS host devri testi

Bu entegrasyon testi, dört ayrı Unity Authentication profiliyle gerçek internet oturumu açar. Yerel UDP smoke testinin veya dört insanla oynanış testinin yerine geçmez. Unity servisleri için normal kullanım kotası geçerlidir.

## Çalıştırma

Güncel **Windows development** çıktısı gerekir. Normal oyun açılışında test bileşeni kurulmaz; yalnızca `--ship-online-host` / `--ship-online-client` bayraklarıyla çalışır. Önce `ShipMayham.Editor.PrototypeSceneBuilder.Build` entry point'iyle development build al, sonra:

```powershell
.\Scripts\Test-RelayMigration.ps1
# Alternatif çıktı:
.\Scripts\Test-RelayMigration.ps1 -ExecutablePath 'D:\Builds\ShipPrototype.exe'
```

Varsayılan çıktı, açık ana Editor'ü etkilemeden kullanılan `Builds/ShipPrototypeValidation/Builds/ShipPrototype/ShipPrototype.exe` dosyasıdır. Script yalnızca kendi başlattığı test süreçlerini kapatır; kullanıcı Editor'ünü veya başka oyun süreçlerini seçmez. Test kanıtları `Logs/RelayMigration-<tarih>-<benzersiz kimlik>/` altında korunur. Batch testleri kariyer kaydını değiştirmez.

## Kabul koşulları

Mühimmat v4 genişletmesinde ilk host, gerçek istasyondan aldığı bir gülleyi de taşır. Host ayrıldığında bu gülle ambara dönmeli; üç sonuç dosyasında toplam 12, ambar 11 ve top yanı 0 olmalıdır. Snapshot/protokol 4, tanı kayıtları, ilk timeout ve yeni kanıtlar [AMMO_FLOW.md](AMMO_FLOW.md) içinde tutulur. `-TimeoutSeconds` uygulamaya da iletilir; varsayılan 240 saniyedir. Bu süre Lobby ayarını değiştirmez.

1. Dört oyuncu aynı Relay/WSS oturumunda seyre başlar. Kasa 321, itibar 9 ve kısmen tamamlanmış bilet denetimi cloud snapshot'a yüklenir.
2. Üç istemci de hazırlanan olayı gözlemledikten sonra script yalnızca ilk test host'unu zorla kapatır.
3. Kalan üç oyuncudan tam biri host olur. Her oyuncu kendi eski slotuna döner; kasa, seyir aşaması ve denetimin seri, ilerleme, kontrol maskesi ve ödülü korunur.
4. Her oyuncunun yeni host'a gönderdiği gerçek zıplama RPC'si yükselme üretir. Üç JSON sonuç dosyası ve üç başarılı süreç çıkışı gerekir.

Geliştirme probe'u JSON yazdıktan sonra supervisor'ın `all-clients-passed.txt` bariyerini bekler. Özellikle yeni host, daha yavaş peer'ler henüz yeniden bağlanırken sabit bir bekleme sonunda kapanmaz. Supervisor üç sonucu doğruladıktan sonra bariyeri açar; uygulama/supervisor süre sınırları korunur. `SHIP_CREW_BIND` tanısı yalnız online development/editor akışında kimliğin mevcut olup olmadığını ve önceki/yeni slot numaralarını yazar; Authentication kimlik değerlerini yazmaz.

İlk host'un zorunlu çıkışı beklenen hata enjeksiyonudur; başarılı katılımcılarda `SHIP_ONLINE_SMOKE_PASS`, test yöneticisinde `SHIP_RELAY_MIGRATION_PASS` aranır. Yalnızca yeni host'un başarılı olması tüm testin geçtiği anlamına gelmez.

## Host seçiminin bekleme süresi

İki saniyelik snapshot yükleme aralığı ve sekiz saniyelik veri işleme timeout'u, Lobby'nin kopan host'u çıkarma süresi **değildir**. Mevcut proje ayarında ani host kaybı sonrası seçim yaklaşık iki dakika sürebilir; bu sırada sefer devam ediyor gibi görünmemelidir. Daha kısa kesinti için Unity Dashboard'daki Lobby disconnect/removal ayarının ayrıca gözden geçirilmesi gerekir. Dashboard'a doğrulanmış hesap erişimi olmadan bu bulut ayarı değiştirilmez.

Kaynaklar: [Unity MPS host migration](https://docs.unity.com/en-us/mps-sdk/session-host-migration), [Unity'nin Lobby timeout açıklaması](https://discussions.unity.com/t/lobby-relay-integrations-dont-function-as-described-on-the-documentation/1545423/2). Relay tek başına oyun durumunu veya oyuncu slotlarını taşımaz; proje kendi `VoyageMigrationDataHandler` snapshot'ını uygular.

## Bağlantı kimliği regresyonu

NGO yeni host altında bağlantı kimliklerini yeniden dağıtır. Eski snapshot'taki `ClientId` değerleri yeniden kullanılabilir kimlikler değildir. İçe aktarım sırasında aktif slotların bağlantıları geçersizleştirilir; her oyuncu kalıcı Authentication `PlayerId` ile kendi slotuna yeniden bağlanır. Aksi halde yeni bağlantı ID'si henüz bağlanmamış başka oyuncunun eski slotuna denk gelebilir.

`HostMigrationTests.ReassignedConnectionIdsCannotControlAnUnboundRestoredPlayer` bu çakışmayı deterministik olarak sınar. Yeni test eski kodla başarısız, düzeltmeyle tüm EditMode paketi **115/115 başarılıdır** (`Logs/MigrationIds-Before.xml`, `Logs/MigrationIds-After.xml`; Unity 6000.4.6f1).

## 22 Ekim 2026 internet doğrulaması

Windows development build başarılı, Unity exit 0 (`Logs/OnlineMigration-FixedBuild.log`). Gerçek Relay/WSS testi `Logs/RelayMigration-20261022-035259-b05c16/` altında **başarılıdır**: client2 slot 3'te yeni host oldu; client1 slot 2'ye ve client3 slot 1'e döndü. Üçünde de kasa 321, olay seri 777 ve aktif ekip sayısı 3; her biri yeni authority üzerinden zıpladı, JSON üretti ve exit 0 ile kapandı. Supervisor `SHIP_RELAY_MIGRATION_PASS` ve exit 0 verdi. İlk host'un -1 çıkışı kasıtlıdır.

SDK, bağlantı kesilirken ve süreç kapanırken `NetworkManagerSession.OnStopCompleted: Called from outside of the session.` uyarıları üretti; bu nedenle günlükler tamamen uyarısız olarak raporlanmaz. Test dört ayrı headless Windows süreciyle internet Relay'inde çalıştı; dört farklı cihaz, tarayıcı sekmelerinin askıya alınması, tekrarlı host kayıpları ve dört insanla tempo/dengenin kabulü bu kanıtın kapsamında değildir.

### v4 güncel üyelik uzlaştırması

Aralıklı yeniden bağlama hatası `060930-5e5fc8` koşusunda, Lobby ayrılma callback'inin yeni host dahil hâlâ mevcut kişilerin kalıcı kayıtlarını sildiği yola daraltıldı. Artık callback tek başına silme yetkisi değildir: güncel `ISession.Players` içinde kalan kişinin slot/admission kaydı tutulur. SDK kaynakları değiştirilmez. Ayrıntı ve negatif test [AMMO_FLOW.md](AMMO_FLOW.md) içindedir.

Doğru kaynakla **140/140** EditMode, Windows build exit 0 ve gerçek `RelayMigration-20261022-061932-a64f7b` koşusunda üç survivor/supervisor exit 0. Bu koşuda client2 slot 3'te host oldu, diğerleri 2 ve 1'de kaldı; kasa, olay ve host'un elinden dönen gülle korundu. Tek başarılı tekrar, uzun süreli soak veya farklı cihaz kabulü değildir.
