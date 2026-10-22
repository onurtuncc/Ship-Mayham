# Liman kasabası — dilim 18

Dar iskele yerine iki limanda da yaklaşık **40 × 48 metrelik** gezilebilir kasaba var: pazar, Salty Gull tavernası, kuru havuz, boya iskelesi ve liman ofisi. Mevcut Blender modellerinden vinç, kayık, deniz feneri, kasalar, banklar, bayraklar ve altı dükkân sakini yerleştirildi. NPC'ler bu dilimde sabittir; yeni diyalog ağacı veya dolaşma AI'ı değildir. İki limanın kasaba düzeni aynıdır, isimleri Brass Quay / North Haven'dır.

Dükkân etkileşimleri yeni yerlerine taşındı; eski renkli küpler gizlendi. Server hareketi kasaba sınırlarını, bina/dekor/ağaç ayak izlerini ve tezgâhları kontrol eder. Karadayken mini harita dükkânları, ofisi, gemiyi, oyuncuyu ve görev noktalarını gösterir. Rota/iş UI seçenekleri ofise yaklaşınca, boya seçenekleri boya dükkânına yaklaşınca kullanılabilir. Mevcut seçim RPC kuralları sunucuda liman/karada olma/itibar kontrollerini korur; yeni UI mesafesi bütün eski seçim RPC'lerine yeni sunucu mesafe şartı eklemez.

## Town Salvage

Ofiste SALVAGE seçilir. Kasabanın üç ayrı ucundaki S1/S2/S3 halat demetleri E / A ile alınır ve PORT OFFICE'e taşınır. Üç ekip üyesi farklı demetleri eşzamanlı taşıyabilir; aynı demet iki kişiye verilemez. Üç teslimat **36 ortak para +1 itibar** verir. Liman ziyaretinde bir tamamlanmış iş sınırı kargo/bagaj/hurda işleri arasında ortaktır.

FOOD pazarı iade kabul eder. Elde eşya varken iptal edilemez; iade sonrası iptal kısmi ödeme yapmaz. Yanlış tezgâh demeti veya parayı tüketmez. Ayrılan taşıyıcının teslim edilmemiş demeti tekrar görünür/alınabilir. Teslim maskesi ve eldeki demet snapshot'a dahildir. Snapshot/bağlantı protokolü **8**.

## Kısa doğrulama

Kullanıcının üretim önceliği doğrultusunda bütün eski sefer/Relay paketleri yeniden çalıştırılmadı. Yeni kara alanı ve iş için **7/7 odaklı test** geçti (`Logs/HarborTown-Focused.xml`, Unity çıkışı 0): paralel toplama/tek ödeme, yanlış/uzak etkileşim, ayrılma+snapshot, geniş alanda hareket+bina engeli, iade/iptal, NPC işlemlerinin alışverişe dönüşmemesi, gerçek sahne/RPC görselleri. İlk Windows build çıkışı 0 (`Logs/HarborTown-WindowsBuild.log`).

`Logs/HarborTown-town.png` ve `Logs/HarborTown-town-street.png` editör Camera.Render önizlemeleridir, canlı tarayıcı oynanışı değildir. Tabela kontrastı/giriş yerleşimi ve ağaçlar eklendikten sonra son Windows build **başarılı**, çıkış 0 (`Logs/HarborTown-Final-WindowsBuild.log`). Bu son dekor ayak izleriyle yedi test tekrar çalıştırılmadı; son build derleme/sahne paketlemesini doğrular. Tam insanlı kasaba temposu, farklı cihazlar ve yeni yerleşimle bütün eski otomasyonlar ayrı kabul kapılarıdır.

## Web yayını — 22 Ekim

Kaynak `1afa86d` → WebGL `fa132b5ece68f5f5db3a1cf68ce184f9e1d0f66f`. Unity build çıkışı 0 (`Logs/HarborTown-WebGLBuild.log`); [Pages dağıtımı 39729740601](https://github.com/onurtuncc/Ship-Mayham/actions/runs/39729740601) başarılı. Canlı HTML yeni build'e eşleşti, açılışta kasaba geometrisi görüldü ve browser error kaydı boştu (`Logs/WebGL-HarborTown-Menu.png`). Eski Relay/sefer flow'u tarayıcıda tekrar çalıştırılmadı; bu kayıt bütün kasaba işinin internet üzerinden oynandığı iddiası değildir. [Oyna](https://onurtuncc.github.io/Ship-Mayham/). Bütün oyuncular sayfayı yenileyip **v8** kullanmalıdır.
