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

## Mixamo animasyon seti

`Art/Generated/Animations/Mixamo_*.fbx` (30 fps, "Without Skin", Humanoid). `Passenger.controller` ve `Crew.controller` durum adlarıyla `PassengerPose`/`CrewPose` enum'larını eşler; yeni durum eklemek için enum'a ekle, builder'daki eşlemeye klip adını yaz ve `Upgrade Prototype (Human Passengers/Crew)` çalıştır.

| Kim | Durum (klip) | Ne zaman |
|---|---|---|
| Yolcu | `Seated` (SittingIdle), `Fidgeting` (Talking), `Pleading` (Waving), `Panicked` (Terrified) | Sakin, şüpheli davranış, sabırsız, taşınma/fırlatılma |
| Yolcu | `Delighted` (CheeringWhileSitting), `Yelling` (SittingYell), `Disbelief` (SittingDisbelief), `Laughing` (SittingLaughing) | Teşekkür, şikâyet, bilet itirazı/ücret, kurtarılma |
| Yolcu | `Drinking` (SittingDrinking), `Clapping` (SittingClap) | İçecek / yiyecek servisi sonrası (tek seferlik) |
| Yolcu | rastgele `Laughing/Clapping/Fidgeting` | Sakin otururken 12–30 sn'de bir, 2,5–4 sn |
| Mürettebat | `Idle`, `Walking`, `Carrying`, `Jump`, `Interact` (ButtonPushing: top/vinç başı) | Sürekli durum (`CrewPresentation.PoseFor`) |
| Mürettebat | `Dance` (HipHop) / `Dance2` (Samba) | Sefer bitince (varış) |
| Mürettebat | `Defeated` | Gemi kaybedilince |
| Mürettebat | `PickUp`, `Throw`, `Clap`, `Victory`, `Salute`, `LookAround` | Eşya alma, yolcu fırlatma, olay çözüldü, korsan yenildi, yükseltme, boştayken 9–22 sn'de bir |

Tek seferlik jestler `PlayOneShot` ile belirli süre oynar, sonra sürekli duruma döner; replike duruma hiç dokunulmaz.

## Gemi yükseltmeleri ve hasar

`Runtime/Ship/`: `ShipProgression` (saf kurallar), `PrototypeShipUpgradeView` (modülleri gösterir), `PrototypeShipEffects` (duman/alev). Oyuncu yükseltme aldığında modül zıplayarak belirir; sahne başlarken ya da istemci katılırken mevcut seviyeler animasyonsuz görünür.

| Yükseltme | Görünüm |
|---|---|
| Koltuk (kapasite 4 → 6 → 8) | Kullanılmayan bankolar gizli, açılınca belirir |
| Motor 1 | Baca (`ShipFunnel`), hızla artan duman |
| Motor 2 | İkinci baca + kıç dümen itici roketleri (`ShipBoost`) ve alev |
| Konfor 1 | Halı (`ShipCarpet`), 4 çiçeklik (`ShipPlanter`), iki yan flama (`ShipBunting`) |
| Hasar < %60 / < %30 | Güvertede duman / duman + alev + kıvılcım |

Modeller `Tools/BlenderAssets/assets/Ship*.py` ile üretilir; `Ship Mayham → Upgrade Prototype (Ship Upgrades)` bunları `Ship_Player/Visuals/Upgrades` altına yerleştirir (statik FBX'lerde Blender +Y Unity -Z'dir; itici lüleleri geriye bakar).

## Hareket, gemi sürüşü ve kamera

Eskiden yürüme ve dümen anlık hız değiştiriyordu, ağdan gelen konumlar 20 Hz basamaklarla üstel interpolasyona sokuluyordu ve mürettebat dünya uzayında yumuşatıldığı için gemi hareket ederken güvertede kayıyordu. Üç katmanda düzeltildi:

| Katman | Dosya | Ne yapar |
|---|---|---|
| Otorite (sunucu) | `Runtime/Motion/CrewKinematics.cs` | Yürüyüş hızı 28 m/s² ile yükselir, 40 m/s² ile söner (tam hıza ~0,15 sn); engele çarpan eksenin hızı sıfırlanır, böylece duvara yaslanınca "birikmiş hız" kalmaz. Işınlanmalar (gemiye biniş, kıyıya çıkış) hızı sıfırlar. |
| Otorite (sunucu) | `Runtime/Motion/ShipSteering.cs` | Motor kolu yerinde kalır, itki su direncine karşı çalışır; dönüş hızı Nomoto modeliyle dümeni gecikmeli izler; dümen yalnız su akışıyla (hız + pervane akıntısı) etkilidir; dönüşte yana kayma. Dümen bırakılınca kol STOP'a, dümen ortaya iner. Ayrıntı: [RELEASE_PASS.md](RELEASE_PASS.md). |
| Sunum (istemci) | `Runtime/Motion/MotionSmoother.cs` | Ağ anlık görüntüleri arasında hız ve aralığı tahmin eder, "son görüntü + o görüntüden beri alınan yol" noktasını hedefler (varışlar arasında süreklidir) ve süzer; değişimler durunca tahmin sönümlenir, aşım olmaz. Gemi konumu/yönü ve mürettebat için kullanılır; mürettebat **güverte/iskele uzayında** yumuşatılıp sonra yerleştirilir. |

Yayın aralığı 25 Hz (`PrototypeSession.PublishInterval`), girdi gönderimi 30 Hz (`PrototypeView.InputSendInterval`). Görsel tarafı: bedenler gittikleri yöne döner (`PrototypeCrewAnimator`), hız animasyon seçimi artık güverte uzayındaki hızdan gelir (hareketli gemide duran denizci koşmaz), gemi dönüşlerde içe yatar ve hızla pruvası hafifçe kalkar (`PrototypeAmbientLife`).

### Dümen kamerası

Dümendeyken **C** (gamepad **Y**) birinci şahıs ile yüksekten takip kamerası arasında geçiş yapar; seçim hatırlanır (`PlayerPrefs`) ve dümen bırakılınca birinci şahsa dönülür. Takip kamerası geminin yönünü yumuşak bir gecikmeyle izler, hıza göre öne bakar ve uzaklaşır; fare ile yörüngede dönülür ve 2,5 sn sonra gemi arkasına yumuşakça geri gelir. Dümendeki oyuncunun bedeni bu görünümde görünür. Ayarlar: `PrototypeView.ChaseDistance / ChasePitch / ChaseFieldOfView`.

## Uyarlanabilir müzik ve gevezelik

Müzik de procedural: ses dosyası yok, indirme boyutu değişmez. `MusicSynth` dört katmanı sinüs seslerinden üretir (hepsi aynı
uzunlukta, 92 BPM'de 16 vuruş ≈ 10,4 sn): **Harbor** (çan ve yumuşak pad), **Pad** (nefes alan akor yastığı), **Pulse** (marimba
benzeri sekizlik arpej) ve **Tension** (alçak drone, tremolo, sinirli tık ve tritone vuruşları). Her perde döngüde tam sayıda
döngü yapacak şekilde yuvarlanır ve notaların kuyruğu döngü başına sarılır, böylece döngü tık sesi olmadan yeniden başlar.
Katmanlar `MusicDirector` tarafından aynı karede başlatılır (kendini kurar, sahne düzenlemesi gerekmez; sentez kare başına bir
katman yapıldığı için yükleme takılmaz).

`MusicLogic` ruh halini seçer: menü, lobi, liman (Boarding/Arrived) → **Harbor**; Sailing → **Sailing** (Pad + Pulse); Sailing
sırasında sızıntı, elektrik kesintisi, aktif Ship event ya da korsan uyarısı/saldırısı → **Tension**; gemi kaybı → sessizlik.
Katman sesleri saniyede 0,5 hızla yumuşakça kayar; toplam ses `FeedbackSettings.MusicGain` (master x music, `M` ile susturma)
ile çarpılır.

`Babble` yolcular ve NPC'ler için sözsüz konuşma üretir: karakter tohumundan gelen perde ve kısa ünlü benzeri heceler,
ruh haline göre (mutlu yükselen, huysuz alçalan, meraklı soru tonu) kontur. Yolcu teşekkür, şikâyet, kurtarılma olaylarında ve
NPC yaklaşana el salladığında üç boyutlu, mesafeyle kısılan kısa bir ses olarak çalar.

## Ses ayarı

`M` tuşu veya sağ alttaki düğme sesi kapatır/açar; tercih `PlayerPrefs`'te saklanır (depolama hatası oyunu durdurmaz).

## Görsel cila

- **Z-fighting:** `Ship Mayham → Audit Coplanar Surfaces` aynı üst yüzeyi paylaşan ince plakaları `Logs/OverlapAudit.txt`'ye yazar. Yaya geçidi/iskele/karşılama paspası aynı yükseklikteydi; katmanlar artık 16 mm arayla (`PrototypeVisualPolishBuilder`, `PrototypeHarborTownBuilder`). Kalan "BOTTOM" satırları gömülü alt yüzlerdir, görünmez.
- **Gökyüzü:** düz lacivert arka plan yerine `ShipMayham/CartoonSky` (üç renkli gradyan, güneş parıltısı, akan bulutlar) ve açık mavi sis (`Upgrade Prototype (Visual Polish)`).
- **Yolcu malzemeleri:** Blender malzeme adları varyanta özeldir (`Shirt_V2`…); aksi halde Unity'nin ada göre eşleştirdiği malzemeler yüzünden tüm yolcular aynı renkte görünüyordu.
- **Gemi:** top ağızları, su hattı şeridi ve gövde şeridi artık gövde profilinin üzerinde (`hull_x`), havada asılı kalmaz.
- **Animasyonlar** yolcu/mürettebat başına farklı hız ve başlangıç fazıyla oynar; senkron robot görüntüsü yok.

- **Gözler:** `Character_Human*_Rigged` modellerinde göz bebekleri gövdeden ayrı `Pupil_L` / `Pupil_R` nesneleridir (başlangıç noktası göz küresinin merkezi, baş kemiğinin çocuğu); döndürülünce göz küresi üzerinde kayarlar (en fazla 26°). `Runtime/Gaze/GazeDirector` kendini kurar, sahnedeki göz bebekli karakterleri bulur ve her biri için `GazeBrain` ile bakış hedefi seçer: ara sıra oyuncunun kamerasına (menzil 16 m, yüzünün önünde), yakındaki karakterlere (8 m), yürüdüğü yöne veya rastgele bir yana bakar; hiçbir hedef birkaç saniyeden fazla tutulmaz, bakarken küçük titremeler vardır. Arkadaki hedeflere bakılmaz. Modeli yeniden üretmek için `Tools/BlenderAssets/assets/Character_Human.py` (`RIG=1`, `VARIANT=1..3`); FBX dışa aktarıcısı kemik çocuklarını kemik kuyruğuna göre yazdığı için script bebekleri bir kemik boyu aşağıda kurar (testler Unity'deki konumu doğrular).

## Doğrulama

`GazeTests` (bakış mantığı, bebeklerin göz hizasında olması, hedefe dönüş ve sınır), `MotionTests`, `HelmCameraTests`, `GameFeelTests`, `ShipUpgradeTests`, `HumanCrewTests`, `HumanPassengerTests`: algılayıcı kuralları (kasa, hasar, sızıntı, yolcu slotları, taşıma, top, olay çıktıları), ses üretimi (NaN/sessizlik/kırpma yok), deniz döngüsü dikişi ve gerçek sahnede olayların parçacık/yazı üretmesi. `SHIPMAYHAM_CAPTURE=1` ortam değişkeniyle son test `Logs/GameFeel.png` yazar (efektli görüntü).
