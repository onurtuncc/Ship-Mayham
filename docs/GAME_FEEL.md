# Oyun hissi: geri bildirim, ses ve canlılık

Bu doküman, bir iş tamamlandığında oyuncuya verilen tepkileri ve dünyayı canlı tutan katmanı anlatır. Her şey yalnızca **görsel/işitsel**tir: replike durumu (`VoyageState`, `PassengerState`, `CrewState`, `EncounterState`) okur, hiçbir zaman durum yazmaz ve ağ mesajı göndermez. Bu yüzden host, istemci ve solo oyunda aynı çalışır; ortadan katılan oyuncu eski olayların havai fişeklerini görmez.

## Parçalar

| Dosya | Görev |
|---|---|
| `Runtime/Feedback/FeedbackDetector.cs` | İki kare arasındaki durum farkından `FeedbackEvent` listesi üretir. Saf veri, birim testli. İlk kare/yayınlanmamış oturum hiçbir şey üretmez. |
| `Runtime/Feedback/PrototypeFeedback.cs` | Olayları ses, parçacık, yüzen yazı, kamera sarsıntısı, yolcu zıplaması, ekran flaşı ve büyük başlıklara çevirir. `Play(FeedbackEvent)` genel olduğu için testler/araçlar doğrudan tetikleyebilir. |
| `Runtime/Feedback/FeedbackFx.cs` | Havuzlu parçacık patlamaları (kıvılcım, kalp, öfke, konfeti, bozuk para, sıçrama, duman, yıldız, köpük) ve yüzen yazılar. Dokular çalışma anında çizilir. |
| `Runtime/Feedback/ProceduralSfx.cs` | Ses dosyası yok: kısa sesler (para, pop, çan, fanfar, gümleme, top, sıçrama, korna, martı…) ve dikişsiz deniz döngüsü ilk kullanımda sentezlenir. İndirme boyutunu etkilemez. |
| `Runtime/Feedback/PrototypeAmbientLife.cs` | Hız ile büyüyen köpük izi, gövdenin hafif sallanması ve gemi etrafında dönen martılar (arada çığlık atarlar). |
| `Runtime/CrewPresentation.cs`, `PassengerPresentation.PoseFor` | Mürettebat/yolcu animasyon durumu seçimi. |
| `Editor/PrototypeFeedbackBuilder.cs` | `Ship Mayham → Upgrade Prototype (Game Feel)`: bileşenleri, parçacık materyalini ve yazı şablonunu sahneye ekler (idempotent). |

## Hangi olay ne yapar

| Olay | Tepki |
|---|---|
| Kasa artışı | Altın patlaması, `+N` yazısı, para sesi (miktarla incelen perde) |
| Yolcu siparişi teslim | Kıvılcım + pop; yolcu teşekkür ederse kalpler, `Thanks!`, çan, zıplama ve alkış animasyonu (`Delighted`) |
| Şikâyet / bilet itirazı | Kırmızı öfke dumanı, `!!`, gümleme |
| Kurtarma | Sıçrama, `Phew!`, zıplama |
| Gövde hasarı | Kamera sarsıntısı (hasarla ölçekli), kırmızı flaş, duman, `-N`, gümleme |
| Sızıntı başladı / onarıldı | Alarm + flaş / kıvılcım + `Fixed!` |
| Sefer başladı / bitti / battı | Korna + başlık / fanfar + konfeti + `+N coins` başlığı / hüzünlü motif |
| İtibar, yükseltme, iskele işi, olay çözüldü | Yıldız yağmuru, çan ve ilgili başlık |
| Top atışı | Namlu dumanı, kıvılcım, sarsıntı, gümleme; mermi denize düşünce sıçrama |
| Korsana isabet / yenilgi | Yıldız, `HIT!` / konfeti + fanfar |
| Eşya alma / bırakma | Küçük kıvılcım ve pop |

Ses, `AudioSource` havuzuyla (8 ses) çalınır; dünya olayları kamera uzaklığına göre kısılır. Tarayıcılar kullanıcı etkileşiminden önce sesi engeller; oyun menüden başladığı için ilk tıklama bunu açar.

Kamera sarsıntısı yalnızca kamera çizilirken uygulanır (`RenderPipelineManager.beginCameraRendering/endCameraRendering`) ve hemen geri alınır; oyun mantığı kamera dönüşümünü sarsıntısız görür.

## Görsel cila

- **Z-fighting:** `Ship Mayham → Audit Coplanar Surfaces` aynı üst yüzeyi paylaşan ince plakaları `Logs/OverlapAudit.txt`'ye yazar. Yaya geçidi/iskele/karşılama paspası aynı yükseklikteydi; katmanlar artık 16 mm arayla (`PrototypeVisualPolishBuilder`, `PrototypeHarborTownBuilder`). Kalan "BOTTOM" satırları gömülü alt yüzlerdir, görünmez.
- **Gökyüzü:** düz lacivert arka plan yerine `ShipMayham/CartoonSky` (üç renkli gradyan, güneş parıltısı, akan bulutlar) ve açık mavi sis (`Upgrade Prototype (Visual Polish)`).
- **Yolcu malzemeleri:** Blender malzeme adları varyanta özeldir (`Shirt_V2`…); aksi halde Unity'nin ada göre eşleştirdiği malzemeler yüzünden tüm yolcular aynı renkte görünüyordu.
- **Gemi:** top ağızları, su hattı şeridi ve gövde şeridi artık gövde profilinin üzerinde (`hull_x`), havada asılı kalmaz.
- **Animasyonlar** yolcu/mürettebat başına farklı hız ve başlangıç fazıyla oynar; senkron robot görüntüsü yok.

## Doğrulama

`GameFeelTests`: algılayıcı kuralları (kasa, hasar, sızıntı, yolcu slotları, taşıma, top, olay çıktıları), ses üretimi (NaN/sessizlik/kırpma yok), deniz döngüsü dikişi ve gerçek sahnede olayların parçacık/yazı üretmesi. `SHIPMAYHAM_CAPTURE=1` ortam değişkeniyle son test `Logs/GameFeel.png` yazar (efektli görüntü).
