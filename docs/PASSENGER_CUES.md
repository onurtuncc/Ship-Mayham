# Yolcu davranış ipuçları — geliştirme dilimi 15

Bu dilim, bilet/kaçak eşya kontrolünü daha okunur kılan **görsel davranışlar** ekler. Yolcuların tam dolaşma/kişilik simülasyonu veya yeni bir suç sistemi değildir.

## Oyuncunun gördüğü

- Aktif kaçak eşya olayındaki hedef, valizini göğsüne çekip öne eğilir. Olay HUD'ı yolcu numarası vermek yerine bu tutuşu aramayı önerir. Yakından bakınca gözlem cümlesi görünür; **F / Y** yine gerçek, sunucu doğrulamalı bilet/olay kontrolüdür.
- Kontrol edilmemiş yolcu bazen ceplerini yoklar. Ücreti ödemeyen daha uzun süre yapar; bazı masum yolcular da aynı jesti kısa süreli yapar. Jest suç hükmü değildir, `Ticket: unchecked` kontrol yapılmadan PAID/DUE'ya dönüşmez. Kontrol sonrası gerçek ödeme durumu mevcut kuralla gösterilir.
- Sipariş beklerken memnuniyeti 55 altına düşen yolcu sabırsız sallanır; diğerleri hafifçe hareket eder. Para, memnuniyet, sipariş veya olay ödülü bu animasyonlar tarafından değiştirilmez.
- Olay çözülünce/başarısız olunca veya sefer bitince valiz normal konuma döner. Sefer bitişi HUD'ı artık eski aktif olayın donmuş talimatını göstermez. Yolcu taşınırken, uçarken veya kurtarılırken bagaj gizlenir ve ek gövde pozu sıfırlanır; mevcut taşıma/fırlatma dönüşü korunur.

`PassengerPresentation.CueFor` mevcut yolcu/olay/sefer snapshot'ından, ortak elapsed zamanı ve sabit yolcu indeksiyle türetilir. Yeni RNG tüketilmez, gameplay konumlarına yazılmaz. **NGO/snapshot protokolü 5 olarak kalır.** Bu, gizli veriyi kriptografik olarak saklayan bir anti-cheat sistemi değildir; normal oyun UI'sinin ne zaman ne gösterdiği kuralıdır.

## Sanat ve sahne

Sekiz `Suitcase.fbx` wrapper'ı, iki yeni URP/Lit deri paleti ve yalnız görsel çocukları yöneten `PrototypePassengerCueView` eklendi. Koltuk/interaction anchor'ları taşınmaz; body hâlâ primitive capsule'dür. Karakter modellerinin entegrasyonu ayrı iştir.

`Ship Mayham → Upgrade Prototype (Passenger Cues)` eklemeli/idempotenttir. Eski **916** serialized kayıt ve kontrol edilen ad/parent/local transform/anchor alanları korunmuştur; **33** kayıt eklenmiştir. Tekrar güncelleme ve preview sonrası SHA256 aynı kaldı: `037F51AF4D6C73C8978DFBAF963F0E78BBE84BB1602A94095228AA654A9FF4BE`. Önceki sahne `Logs/ShipPrototype-PrePassengerCues.unity` altında tutulur.

## Doğrulama

- Unity **6000.4.6f1**, ilk **164/164**; sefer kapanışı ve snapshot sonrasında bir tick ilerleme kapsamıyla son **167/167**, exit 0 (`Logs/PassengerCues-Final.xml`). Testler masum false-positive jestini, kontrol öncesi bilet bilgisini, cue önceliğini, kapanış durumlarını ve gerçek sahnede F/G RPC'leriyle valiz/poz temizliğini sınar.
- Windows development build exit 0 (`Logs/PassengerCues-Final-WindowsBuild.log`). İlk dört süreçli sefer de geçti (`Logs/VoyageSmoke-20261022-065734-45f5a5/`, dört exit 0; 184 kasa/23 yiyecek/24 içecek/20 koruma). Son kapanış değişikliği sonrası `Logs/VoyageSmoke-20261022-070646-d9c0e3/` tekrarında da dört `SHIP_SMOKE_PASS`/exit 0 ve supervisor `SHIP_VOYAGE_TEST_PASS`/exit 0: ortak son durum **166 kasa / 23 yiyecek / 24 içecek / 20 koruma**, varış ödülü 220, bounty 50. Değişen olay/bahşiş hesabı nedeniyle iki ayrı koşunun para miktarları aynı olmak zorunda değildir; her koşuda dört peer aynı server hesabını doğrular.
- `Logs/PassengerCues-Layout.png` gerçek Unity `Camera.Render` çıktısıdır, fakat **sahnelenmiş sanat önizlemesidir**; canlı RPC veya insan testi görüntüsü değildir. `CapturePreview --prototype-passenger-cues-preview` ile üretilir; örnek aktivasyon/pozlar kaydedilmez. İlk arka kadraj ve büyük önizleme etiketleri düzeltildi; render artık runtime etiket ölçekleme kuralını kullanır.

İpuçlarının dört insan tarafından fark edilmesi, masumlara haksız kesinlik vermemesi ve yakın etiketlerin bakışta okunması manuel kabul kapılarıdır. Otomatik bir poz testi, sosyal çıkarımın eğlenceli veya dengeli olduğunu kanıtlamaz. Son yayımlanmış web paketi kaynak sürümünden ayrı [PROTOTYPE.md](PROTOTYPE.md) içinde izlenir.
