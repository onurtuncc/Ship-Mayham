# Limanda bagaj ayırma — geliştirme dilimi 16

Karada sözleşme panosundan CARGO veya BAG SORT seçilir. Aynı liman ziyaretinde yalnız bir iş tamamlanabilir. Bagaj işinde turuncu tezgahtan farklı A/B/C valizleri alınır, panoda ve eldeki eşya metninde belirtilen mavi/yeşil tezgaha götürülür. Hedefler liman ziyaretleri arasında değişir; rota seçimi hedefleri değiştirmez. Üç doğru teslimat ortak kasaya 30 para ve 1 itibar verir. İş süreli değildir.

Yanlış tezgah valizi tüketmez veya alışveriş yapmaz. Turuncu tezgah iade noktasıdır; bütün eldeki iş eşyaları iade edilmeden iptal edilemez. Elde valizle gemiye binilemez. Ayrılan oyuncunun teslim edilmemiş valizi yeniden alınabilir. Gecikmiş teslimat RPC'si, iş kapanmış/değişmişse veya elde teslimat yoksa normal satın almaya dönüşmez.

Valiz modeli mevcut Blender `Suitcase.fbx` varlığıdır; renkli etiketi ve HUD'daki harf/hedef birbirini tamamlar. Etiketin üzerinde basılı harf yoktur. Sahne güncellemesi `Ship Mayham/Upgrade Prototype (Dock Baggage Sorting)` ile eklemelidir; yeni sahne üretmez. Snapshot ve bağlantı protokolü **6**: eski açık sekmeleri yenileyin.

## Doğrulama — 22 Ekim 2026

- Unity 6000.4.6f1 EditMode: **182/182**, `Logs/DockSorting-Intent.xml`; son etiket yerleşimiyle tekrar **182/182**, `Logs/DockSorting-Release.xml`; Unity çıkışları 0. Benzersiz valizler, yanlış hedef/iade/iptal, mesafe/cooldown, snapshot, ayrılma, gecikmiş RPC ve gerçek sahne/RPC sunumu kapsanır.
- Windows build çıkışı 0: `Logs/DockSorting-Intent-WindowsBuild.log`.
- Dört gerçek yerel süreç tam sefer + üç bagaj teslimatı + ortak ödeme + yeni sefer: `Logs/VoyageSmoke-20261022-074158-08f7eb`, tüm süreçlerde `SHIP_SMOKE_BAG_SORT` ve başarılı sonuç. Son ortak para 191, yiyecek 23, içecek 24, koruma 20.
- Gerçek Relay/WSS v6 host kaybı: `Logs/RelayMigration-20261022-074538-73a9f1/all-clients-passed.txt`; kalan üç oyuncu yeni host altında devam etti. Bu test mühimmat taşıma probudur, internet üzerinden aktif bagaj işi ortasında host kaybı testi değildir. Bagaj/mask snapshot ve ayrılan taşıyıcı toparlanması EditMode'da, mask replikasyonu yerel dört süreçte sınandı.
- `Logs/DockSorting-Layout.png` editör Camera.Render önizlemesidir; canlı tarayıcı oynanışı kanıtı değildir. Son etiket yerleşimi ayrıca render edilir.

Art dalı `art/blender-generated` / `13c8912`, main'in atasıdır; uzak dallar yenilendikten sonra yeni art commit'i veya art worktree'sinde bekleyen değişiklik bulunmadı. Modeller zaten main'dedir; bütün karakter modellerinin sahnede kullanıldığı anlamına gelmez.

İnsanlarla denge testi, farklı cihazlarda tarayıcı kabulü ve aktif bagaj işi sırasında internet host kaybı kabulü açıktır. Lobby host seçimi gecikmesi devam eder; kesintisiz geçiş iddiası yoktur.

## Canlı yayın

Kaynak `1e9d53d` → WebGL `e3e2bdd1161c8b6035ee8910ef8da794b0eb60c8`. [Pages işi 39725524917](https://github.com/onurtuncc/Ship-Mayham/actions/runs/39725524917) başarılı. Unity build çıkışı 0 ve `Build Finished, Result: Success` (`Logs/DockSorting-WebGLBuild.log`). Canlı HTML, yeni loader/data/framework/wasm sürüm adresleriyle yerel yayın çıktısına eşleşti. [Oyun](https://onurtuncc.github.io/Ship-Mayham/) tarayıcıda açıldı, Relay host ve BAG SORT arayüzü görüldü; error kaydı boştu. Kanıt: `Logs/WebGL-DockSorting-Relay.png`. Bu, tarayıcıda bütün bagaj işinin oynandığı anlamına gelmez; tam teslimat dört yerel native süreçte doğrulandı.
