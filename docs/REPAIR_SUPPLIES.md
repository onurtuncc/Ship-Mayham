# Fiziksel hızlı yama kitleri — dilim 17

GDD Faz 4'teki tamir malzemesi akışı: her seferde **3 kit**, geminin sağ arka tarafındaki **KITS** deposunda başlar. E / A ile bir alet çantası alınır; **REPAIR** tezgâhına taşınıp teslim edilir. Tezgâh kapasitesi **2**'dir. Bir ekip üyesi stok yaparken diğeri tamiri uygulayabilir. Teslim etmek otomatik tamir etmez.

Sızıntı varken boş elle REPAIR'a tekrar E / A, bir hazır kiti tüketerek sızıntıyı kapatır ve gövdeyi en fazla **8 puan**, en çok 100 sağlığa kadar onarır. Kuru gövdede kit/sağlık harcanamaz veya kazanılamaz. Hazır kit yoksa mevcut **4 adımlı elle yama** çalışır; kaynakların bitmesi oyunu kilitlemez. Tamirden sonra bir sonraki planlı sızıntı 45 saniye ötelenir. Kitler elektrik kesintisinde de taşınabilir; elektrik tamirine veya olay çözümlerine dönüştürülmez.

Kiti depoya iade etmek stok kaybını geri alır. Elde kitle karaya çıkılamaz veya diğer istasyonlara geçilemez. Tamir tezgâhı doluysa kit elde kalır. Bağlantıdan ayrılan/askıya alınan taşıyıcının kiti bir kez depoya döner. Sefer sonuçlanırken eldeki kitler iade edilir; yeni sefer hazır/harcanmış sayaçları sıfırlar ve yeniden 3 kit verir. Bunlar seferlik kaynaklardır, kalıcı kariyer stoğu değildir.

Korunum: **depo + tezgâh + aktif ekip elleri + harcanmış = 3**. Mantık sunucu simülasyonundadır; görseller kaynak oluşturmaz veya tüketmez. Yeni `PrototypeRepairSupplyView`, çoğaltılan sayaçlardan depo/tezgâh modellerini ve etiketleri günceller. Elde görünen varlık mevcut Blender `Toolbox.fbx`'tir; malzeme paleti korunur.

## Uyumluluk ve sahne

Snapshot/NGO protokolü **7**. Eski v6 oturumlarıyla karıştırmayın. `RepairKit` ve `RepairSupply` enum sonuna eklendi; eski değerler sabittir. Depo istasyon dizisinin sonuna (12. istasyon, indeks 11) eklenir. `Ship Mayham/Upgrade Prototype (Repair Supplies)` mevcut sahneye eklemeli güncelleme yapar; `Create` mevcut sahneyi yeniden oluşturmaz.

Sahne koruma kontrolü: önceki **981** serialized kayıttan eksilen **0**, kontrol edilen eski isim/parent/transform/oynanış anchor değişimi **0**. Yeni kayıt sayısı **1043**. Bu kontrol tüm görsel özelliklerin birebir aynı olduğunu iddia etmez.

## Doğrulama kaydı

İlk tur `Logs/RepairSupplies-First.xml`: **195/198** geçti. Üç hata, sahnede 11 istasyon bekleyen eski testlerdi. Beklenti 12 olarak güncellendi ve eski CANNON/POWER/AMMO indekslerinin sabitliği korunarak yeni RepairSupply indeksi kontrol edildi. İlk rapor saklanır.

Son mantık turu `Logs/RepairSupplies-Final.xml`: **202/202**, Unity çıkışı 0. İlk Windows build çıkışı 0 (`Logs/RepairSupplies-WindowsBuild.log`); dört yerel süreçte kit alımı/teslimatı/uygulama ve tüketim replikasyonu, korsan/sefer, liman bagaj işi ve yeni seferde yeniden stoklama geçti (`Logs/VoyageSmoke-20261022-081314-9c89f9`). Tüm süreçler çıkış 0; son ortak para 206, yiyecek 23, içecek 24, koruma 20.

Render'da fazla büyük görünen eldeki Toolbox ölçeği **0.7**'ye düşürüldü; metre ölçeğindeki gerçek renderer bounds testi eklendi. Bu son sahneyle tam paket tekrar **202/202** geçti (`Logs/RepairSupplies-Release.xml`, Unity çıkışı 0). `Logs/RepairSupplies-Layout.png`, sahne kaydını değiştirmeyen editör Camera.Render önizlemesidir; canlı tarayıcı oynanışı değildir. Son kaynak sahne SHA-256: `BBBE8A55B98282B0F3BB2A3969456693F607ECA5C325169AEC7BEE9E1A36080C`.

Son sahneyle idempotence çıkışı 0 ve byte-hash değişimi 0 (`Logs/RepairSupplies-Release-Idempotence.log`); Windows build çıkışı 0 (`Logs/RepairSupplies-Release-WindowsBuild.log`). Son dört süreçli sefer tekrarı `Logs/VoyageSmoke-20261022-082027-6a82e7`: tüm dört süreç çıkış 0, tümünde `SHIP_SMOKE_QUICK_PATCH`, `SHIP_SMOKE_BAG_SORT` ve `SHIP_SMOKE_PASS`; son ortak para 206, yiyecek 23, içecek 24, koruma 20. Yeni seferde depo 3 / tezgâh 0 / kullanılan 0 kontrol edildi.

Gerçek Relay/WSS v7 host kaybı geçti: `Logs/RelayMigration-20261022-081508-484043/all-clients-passed.txt`; kalan üç istemci kendi slotuna bağlanıp yeni otoriteye zıplama komutu gönderdi. Mevcut internet probu host'un elindeki **mühimmatı** toparlar; elinde **tamir kiti** olan host'un kaybı yalnız deterministik snapshot testindedir. Kit akışının gerçek uzak istemci RPC/replikasyonu dört yerel Windows sürecinde doğrulanır.

Gerçek tarayıcılar, dört insanla denge ve kit taşıyan host'un internet oturumunda kaybı ayrıca kabul gerektirir; deterministik snapshot testi bunların yerine geçmez. Güncel Pages `e3e2bdd` hâlâ v6'dır; bu dilimin Windows v7 çıktısını v6 web oturumuyla karıştırmayın.
