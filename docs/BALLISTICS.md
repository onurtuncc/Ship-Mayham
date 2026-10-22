# Uçuşlu gülle — geliştirme dilimi 14

Top ateşi artık anlık ray hasarı değildir. Mürettebat ve korsan mermisi host simülasyonunda konum, hız ve kalan uçuş süresiyle ilerler. Hasar yalnız çarpışmada uygulanır; çizim hiçbir zaman hasar otoritesi değildir.

## Kurallar

- Mürettebat gülle hızı 45 m/s, yerçekimi 4 m/s², azami uçuş 2 saniyedir. Nişan gemiye göre verilir; mermi çıktıktan sonra dünya uzayında yol alır. Uzaktaki hedef için yükseliş, hareketli hedef için önleme gerekir. Başlangıçta gemi hızını miras almaz.
- Korsan atışı mevcut gemi hızına önleme ve yerçekimi telafisi yapar. Atıştan sonra yön/hız değiştirmek isabeti etkiler; artık ateş animasyonu tek başına hasar değildir. Elektrik kesintisini ilk denenen atış değil ilk gerçek isabet tetikler. 22. saniyedeki ayrı, planlı elektrik arızası korunur.
- Mürettebat mermisi hareketli korsanın 3,5 m yarıçaplı yaklaşık gövdesine; korsan mermisi geminin yönlendirilmiş blokout kutusuna çarpar. Her simülasyon adımının iki ucu arasında segment taranır; tek karede hedefin içinden geçmek isabeti atlatmaz. Su düzlemi hedeften önceyse gülle suya düşer.
- Gemi kutusu yerel x ±5,4 / z ±8,4 / y 0,1–3,2'dir. Hareket iki uç arasında hesaba katılır; yön kutusu adımın son heading'ini kullanır. Bu, gerçek model mesh'i veya tam dönen rigidbody çarpışması değildir.
- Her tarafta bir aktif gülle yeterlidir: 2 saniye ömür, topun 2,5 saniye doldurmasından ve korsanın 8 saniye aralığından kısadır. Ayrıca dolu bir slotun üstüne yeni mürettebat mermisi yazılmaz. Ateşte harcanan gülle geri verilmez; [tedarik invariant'ı](AMMO_FLOW.md) korunur.
- Başarılı/başarısız sefer kapanışı ve yeni sefer uçuşları temizler. Oyuncunun istasyonu bırakması havadaki gülleyi geri çağırmaz.

## Görsel ve ağ

İki eklemeli sphere görseli ve 0,8 m kısa iz aktif uçuş snapshot'ından çizilir. Eski 90 m anlık çizgi kaldırıldı. Sonradan giren oyuncu geçmiş atışı yeniden oynatmaz; varsa mevcut uçuşu görür. Konumlar mevcut 20 Hz yayın döngüsünü kullanır; client-side isabet hesabı yoktur.

Snapshot ve NGO protokolü **5**'tir. İki merminin aktifliği, konumu, hızı, kalan süresi ve gerçek korsan isabet sayısı taşınır. V4 snapshot reddedilir; bütün oyuncular aynı güncel build'i kullanmalıdır. `Upgrade Prototype (Ballistic Cannonballs)` eklemeli/idempotenttir. Eski **908** nesne kaydı ve kontrol edilen ad/parent/local transform/anchor bağları korunmuş, iki sphere için **8** kayıt eklenmiştir. Tekrar güncellemede sahne SHA256 değişmez: `5EC11CACB9904BFBBBA62FCE887BB71EBB782111F78D0C419C219C5817CB7CC9`.

## Doğrulama durumu

WebGL kaynak `d091e03`, `gh-pages` paketi `0115215`, Pages deployment `39719323498` success. Unity WebGL build exit 0 (`Logs/Ballistics-WebGLBuild.log`). Canlı tarayıcıda açılış, Relay host başlangıcı ve Leave ile menüye dönüş doğrulandı; error kaydı boştu (`Logs/WebGL-Ballistics-Relay.png`). Bu tarayıcı kontrolü uçuşlu atışın elle oynanması veya dört tarayıcı kabulü değildir; hareketli mermi kanıtı aşağıdaki sahne/RPC ve dört Windows sürecindedir.

İlk koşu 147/150'ydi: iki yeni testin topçusu istasyondan uzaktaydı; bir eski test hâlâ anlık korsan hasarı bekliyordu. Test kurulumu ve çarpışma zamanı beklentisi düzeltildi. Dönmüş gemi ve iki tarafın uçuştaki mermilerini taşıyan snapshot kontrolleri dahil son paket **151/151**, Unity exit 0 (`Logs/Ballistics-Final.xml`). Windows development build exit 0 (`Logs/Ballistics-WindowsBuild.log`); bu build'den sonra yalnız test kapsamı genişledi, runtime değişmedi.

Dört gerçek Windows süreci `Logs/VoyageSmoke-20261022-064040-f4cdbf/` koşusunda hareketli mermiyi replikasyonla gördü (`SHIP_SMOKE_BALL_FLIGHT` dört logda). Tedarikçi iki gülle teslim etti, topçu korsanı üç isabetle yendi, yolcu/liman/yeni sefer tamamlandı. Dördünde `SHIP_SMOKE_PASS`/exit 0; ortak son durum **166 kasa / 23 yiyecek / 24 içecek / 20 koruma**, varış ödülü 220 ve bounty 50. Supervisor `SHIP_VOYAGE_TEST_PASS`, exit 0.

V5 Relay/WSS regresyonu `Logs/RelayMigration-20261022-063834-e85864/` üç survivor ve supervisor exit 0 ile geçti: client3 slot 2'de yeni host, diğerleri 1 ve 3; kasa 321/olay 777/mühimmat 12/11/0 korunup yeni host'a zıplama RPC'si ulaştı. Bu internet probe'u aktif mermi taşımasını değil genişleyen v5 şemasında mevcut host migration senaryosunu doğrular; aktif uçuş devamı deterministik snapshot testindedir.

Kapsam dışında: kara/diğer dekor mesh'lerine gülle çarpması, mürettebata friendly fire, yıkılabilir modeller, ses/patlama/su sıçrama efektleri ve lag compensation. İnsanlarla top nişan hissi/denge kabulü hâlâ gereklidir. Uçuştaki iki merminin aktarımı deterministik snapshot testindedir; gerçek Relay probe'u şu an host kaybını aktif gülle olmadan sınar.
