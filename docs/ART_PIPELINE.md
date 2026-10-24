# Üretilmiş 3B varlıklar (Blender pipeline)

Bu doküman `Assets/ShipMayham/Art/Generated/Models/` altındaki FBX varlıklarının nasıl üretildiğini ve nasıl kullanılacağını anlatır. Hedef: "Dear Passengers" ve benzeri arkadaş/parti oyunlarının tonu: tombul, yuvarlatılmış, parlak ve doygun renkli, okunaklı silüetler.

## Stil kuralları

- **Yuvarlatılmış formlar:** Tüm kutular ve silindirler kenarlarından bevel'lıdır (`assetlib.Builder.box/cyl`); sert kenar yoktur. Bevel boyutu en küçük kenara oranlıdır.
- **Yumuşak gölgeleme:** Yüzeyler yumuşak, yalnızca 40°'den keskin kırılmalar sert kenardır (FBX `mesh_smooth_type=EDGE`).
- **Tombul oranlar:** İnce parçalar kalınlaştırılır; karakterler iri kafalı, kısa kalın uzuvlu "fasulye" gövdelidir (`Tools/BlenderAssets/charlib.py`).
- **Ortak palet:** `assetlib.std()` tüm varlıklarda aynı ahşap/metal/pirinç/krem/kırmızı tonlarını kullanır. Servis rengi kuralı: yiyecek turuncu, içecek mavi.
- **Bütçe:** Küçük proplar ~0,6–2 bin üçgen, büyük liman parçaları 1–4 bin, karakterler ~6–8 bin üçgen.

## Yapı

| Yol | İçerik |
|---|---|
| `Assets/ShipMayham/Art/Generated/Models/*.fbx` | Unity'ye alınan modeller (her biri tek mesh, malzeme başına submesh) |
| `Assets/ShipMayham/Art/Shaders/StylizedOcean.shader` + `Art/Materials/Ocean_Stylized.mat` | Çizgi film deniz shader'ı ve materyali |
| `Assets/ShipMayham/Runtime/OceanSurface.cs` | Deniz ızgara mesh'i (çalışma anında üretilir) ve gemiyi izleme |
| `Assets/ShipMayham/Editor/PrototypeOceanBuilder.cs` | `Ship Mayham → Upgrade Prototype (Stylized Ocean)` |
| `Assets/ShipMayham/Editor/OceanPreviewCapture.cs` | Boş sahnede deniz + modellerin önizleme render'ı |
| `Tools/BlenderAssets/assetlib.py`, `charlib.py`, `assets/*.py` | Varlık betikleri; betik değişirse FBX yeniden üretilir |
| `Tools/BlenderAssets/build_asset.sh` | Tek varlığı headless Blender ile üretir |
| `docs/art/*.png` | Önizleme levhaları |

Yeniden üretim (Blender 5.1+):

```bash
Tools/BlenderAssets/build_asset.sh Buoy ./out/Buoy
Tools/BlenderAssets/build_asset.sh Character_Crew3 ./out/Crew3   # Character_Crew1..4 ve Character_Passenger1..3 aynı betiği paylaşır
```

Çıktı: `<Ad>.fbx`, `<Ad>.png`, `<Ad>_back.png`, `<Ad>.json`. FBX'i `Assets/ShipMayham/Art/Generated/Models/` altına, aynı adla kopyala; `.meta` dosyaları sabit GUID'lidir, üzerine yazmak sahne referanslarını korur.

## Sözleşmeler

- Birim metredir; FBX `apply_unit_scale` ve `bake_space_transform` ile Unity eksenlerine çevrilir (`+Z` ileri, `+Y` yukarı).
- Pivot taban merkezidir (en alçak nokta y=0, x/z ortalı). Etkileşim anchor'ları sahnede ayrıca kalır; bkz. [PROTOTYPE.md](PROTOTYPE.md) "Sahne ve model değiştirme sözleşmesi". Dosya adlarını ve boyutlarını değiştirme: sahne oluşturucu (`PrototypeGeneratedPropsBuilder`) onlara ada göre bağlanır.
- Karakterler Unity'de `+Z`'ye bakar; gemi başı `+Z`'dir. `DeckBench` oturma yüzü `+Z`'ye, `ShipWheel` oyuncunun durduğu `-Z` tarafına bakar.
- Malzemeler düz renklidir (doku yok); malzeme adları paylaşılır (`Wood`, `Wood_Dark`, `Brass`, `Red`…). Blender'dan gelen renk doğrusal uzaydadır ve Unity'de doğru okunur; parlaklık (smoothness) Unity malzemesinde ayarlanır.

## Deniz

`StylizedOcean` shader'ı (URP, unlit, WebGL uyumlu, derinlik dokusu istemez) üç çapraz dalga, kademeli (cartoon) gölgeleme, iki katmanlı hücresel köpük çizgileri, dalga tepesi köpüğü, güneş parıltısı ve mesafeyle derinleşen mavi içerir. `OceanSurface` ızgara mesh'ini çalışma anında üretir ve verilen `Follow` hedefini (gemi) ızgara adımına yuvarlayarak izler; dalga yüksekliği yalnızca görseldir, oynanış okumaz.

Deniz `ShipPrototype` sahnesine uygulandı (`Ocean_Stylized`, `Follow` = gemi); eski `Ocean_Visual` yalnızca çizimi kapalı olarak duruyor. Başka bir kopyada uygulamak için (Editor'de, Play dışında): **Ship Mayham → Upgrade Prototype (Stylized Ocean)**. Eski `Ocean_Visual` kutusu silinmez, yalnızca çizimi kapanır; işlem tekrar çalıştırılabilir. Yedek `Logs/ShipPrototype-PreStylizedOcean.unity`. Renkler ve dalga parametreleri `Ocean_Stylized.mat` üzerindedir.

Doğrulama: `OceanSurfaceTests` (15 test) ve boş sahnede render alan `OceanPreviewCapture.Capture` (`-executeMethod ShipMayham.Editor.OceanPreviewCapture.Capture --ocean-preview-out <png>`; shader'ların eşzamanlı derlenmesi için gerekli). Önizleme: `docs/art/ocean_preview.png` (boş sahne), `docs/art/ocean_in_prototype_scene.png` (gerçek sahne, `OceanPreviewCapture.CaptureScene`). Tüm EditMode paketi (ShipMayham.Tests) temiz bir kopyada 225/225 geçti.

## Varlık listesi

Boyutlar Blender eksenindedir (x, y=ileri, z=yukarı), metre.

| Varlık | Üçgen | Boyut (m) | Kullanım |
|---|---:|---|---|
| `LifeRing` | 1408 | 0,77 × 0,77 × 0,16 | Kurtarma can simidi (`RescueFloat_Visual`) |
| `Suitcase` | 1296 | 0,63 × 0,24 × 0,48 | Yolcu bagajı |
| `DeckBench` | 1060 | 1,20 × 0,46 × 0,92 | Yolcu koltuğu (`SeatVisuals`) |
| `ShipWheel` | 3876 | 0,96 × 0,50 × 1,58 | Dümen istasyonu (Helm) |
| `Bollard` | 1540 | 0,60 × 0,60 × 0,72 | Liman bağlama babası |
| `Lantern` | 996 | 0,22 × 0,22 × 0,44 | Gemi/liman feneri |
| `RumBottle` | 1444 | 0,15 × 0,16 × 0,34 | İçecek (mavi) |
| `FoodPlatter` | 1192 | 0,45 × 0,40 × 0,17 | Yiyecek (turuncu) |
| `ServingTray` | 608 | 0,56 × 0,35 × 0,07 | Servis tepsisi |
| `Toolbox` | 1172 | 0,46 × 0,23 × 0,43 | Tamir istasyonu (Repair) |
| `FuseBox` | 2060 | 0,50 × 0,27 × 0,74 | Elektrik paneli (Power) |
| `CannonballPile` | 4788 | 0,62 × 0,62 × 0,44 | Top mühimmatı |
| `Buoy` | 1136 | 0,42 × 0,42 × 0,98 | Deniz şamandırası |
| `TicketCounter` | 1912 | 1,80 × 0,80 × 1,70 | Manifest/bilet tezgâhı |
| `MarketStall` | 3504 | 2,16 × 1,50 × 2,22 | Liman erzak tezgâhı |
| `DockPlank` | 3984 | 4,00 × 2,10 × 1,46 | İskele modülü |
| `Lighthouse` | 4012 | 4,56 × 4,56 × 9,15 | Liman/deniz feneri |
| `PirateShipSmall` | 3944 | 2,20 × 6,71 × 4,70 | Korsan gemisi (`Ship_Pirate_01`) |
| `Anchor` | 840 | 1,06 × 0,14 × 1,53 | Çapa (dekor) |
| `RopeCoil` | 1328 | 0,53 × 0,52 × 0,45 | Halat yumağı (iskele dekoru) |
| `Rowboat` | 1374 | 3,02 × 2,50 × 0,97 | Filika (liman dekoru) |
| `FlagPole` | 1200 | 1,04 × 0,26 × 2,85 | Bayrak direği (kozmetik/liman) |
| `Gangway` | 3060 | 1,32 × 3,00 × 0,79 | İskele geçiş tahtası (`Gangway_Visuals`) |
| `CrateStack` | 4300 | 2,08 × 1,57 × 1,27 | Sandık + varil yığını |
| `HarborCrane` | 1120 | 1,40 × 4,45 × 4,61 | Liman vinci |
| `HarborHut` | 1408 | 3,60 × 2,70 × 3,35 | Liman terminal binası (`Port_*/Visuals`) |
| `FishCrate` | 1464 | 0,70 × 0,45 × 0,28 | Balık sandığı (tezgâh dekoru) |
| `Character_Crew1` | 6458 | 0,80 × 0,61 × 1,44 | Mürettebat (kırmızı), `Crew_01` |
| `Character_Crew2` | 6458 | 0,80 × 0,61 × 1,44 | Mürettebat (mavi), `Crew_02` |
| `Character_Crew3` | 6458 | 0,80 × 0,61 × 1,44 | Mürettebat (yeşil), `Crew_03` |
| `Character_Crew4` | 6458 | 0,80 × 0,61 × 1,44 | Mürettebat (sarı), `Crew_04` |
| `Character_Passenger1` | 6148 | 0,99 × 0,80 × 1,56 | Yolcu (hasır şapka) |
| `Character_Passenger2` | 6148 | 0,99 × 0,76 × 1,51 | Yolcu (hasır şapka, sarı) |
| `Character_Passenger3` | 6468 | 0,99 × 0,59 × 1,58 | Yolcu (bere) |
| `Character_VIP` | 7516 | 0,90 × 0,86 × 1,75 | VIP yolcu (silindir şapka, baston) |
| `Character_Pirate` | 7600 | 0,86 × 0,72 × 1,51 | Korsan karakteri |
| `Character_Human1..3_Rigged` | ~8,6 bin | 1,84 × 0,51 × 1,75 | Yolcu (insan, büyük gözlü); Humanoid iskeletli, Mixamo kemik adları; `Passenger_01..08` |
| `PlayerShipHull` | 128 | 12,5 × 25,6 × 4,95 | Oyuncu gemisi gövdesi (livery boyaması bunu hedefler; renderer adı `Hull_Primitive`) |
| `PlayerShipDeco` | ~7,9 bin | 12,5 × 25,6 × 5,05 | Oyuncu gemisi güverte, korkuluk, top ağzı, pruva (`Ship_Player/Visuals`) |

## Durum ve sınırlar

- Varlıklar `PrototypeGeneratedPropsBuilder` ile kısmen sahneye bağlıdır; listedeki diğerleri yalnızca projeye eklidir. Deniz sahneye uygulandı ve gerçek sahne render'ıyla doğrulandı.
- `Character_Crew*`, `Character_Passenger*`, `Character_VIP` ve `Character_Pirate` statik pozdadır (iskelet yok) ve sahnede kullanılmaz. Yolcular artık `Character_Human*_Rigged` ile gösterilir: `Ship Mayham → Upgrade Prototype (Human Passengers)` kapsül gövdeyi gizler, insanı ekler ve `PrototypePassengerAnimator`'ı bağlar.
- Animasyonlar Mixamo'dan (FBX, 30 fps, "Without Skin") `Art/Generated/Animations/Mixamo_*.fbx` olarak alınmıştır; `HumanoidModelPostprocessor` hepsini Humanoid içe aktarır ve döngü ayarlarını yapar. `Passenger.controller` dört durumu eşler: `Seated` (SittingIdle), `Pleading` (Waving), `Fidgeting` (Talking), `Panicked` (Terrified). Durum seçimi `PassengerPresentation.PoseFor` içindedir. Mürettebat da aynı insan modelleriyle gösterilir (`Ship Mayham → Upgrade Prototype (Human Crew)`, `Crew.controller`: `Idle`, `Walking`, `Carrying`); durum `CrewPresentation.PoseFor` ile taşıma durumu ve görünür hızdan seçilir (`PrototypeCrewAnimator`). CheeringWhileSitting, SitToStand ve StandToSit klipleri projede hazırdır ancak henüz bir duruma bağlı değildir.
- Mixamo'nun otomatik iskeleti birbirinden bağımsız parçalardan oluşan bu gövdeyi reddeder ("Unknown error while generating motion"), bu yüzden iskelet ve ağırlıklar `Character_Human.py` içinde `RIG=1` ile üretilir (bölge tabanlı ağırlık; ısı difüzyonu parçaları karıştırdığı için kullanılmaz). Yeniden üretim: `RIG=1 VARIANT=1..3 Tools/BlenderAssets/build_asset.sh Character_Human1 ./out`.
- Oyuncu gemisi `Ship Mayham → Upgrade Prototype (Player Ship)` ile uygulanır: kutu hull/deck/korkuluklar gizlenir, `PlayerShipHull` + `PlayerShipDeco` eklenir. Statik FBX'lerde Blender +Y (pruva) Unity'de -Z'ye gider, bu yüzden model 180° Y döndürülür. Betik `PART=hull|rest` ile iki parçayı aynı orijinle üretir.
- FBX'ler ayrı bir minimal URP 17.4 Unity projesinde (Unity 6000.4.6f1) içe aktarılıp render alınarak doğrulandı; deniz ayrıca gerçek prototip sahnesinde render alınarak doğrulandı; model yerleşimlerinin görsel doğrulaması yapılmadı.
- Üretim yöntemi: modeller bu depodaki Blender betikleriyle elle yazıldı. Yerel LLM (llama.cpp + Blender MCP) denemeleri tutarlı sonuç vermedi; çıktılar LLM'den değil betiklerden gelir.

## Yerel LLM ile üretilenler ve yeni Mixamo klipleri (23 Ekim)

- `RockCluster` (1312 üçgen, 4,2 × 3,0 × 2,5 m, deniz kayalığı) ve `RepairHammer` (1900 üçgen, 0,21 × 0,06 × 0,59 m, tamir çekici) Gemma4 (llama.cpp + Blender MCP) ile üretildi; kaynak betikler `Tools/BlenderAssets/llm/` altındadır. Henüz sahneye bağlı değildir.
- LLM boru hattı: `Tools/BlenderAssets/llm/gen_asset.py` (sürücü), `llm_helpers.py` (modelin kullanmak zorunda olduğu `box/cyl/sphere/mat/finish` yardımcıları; serbest bmesh kodu parçaları havada bırakıyordu), `extract_script.py` (bir sürümün betiğini geri bildirim turu için çıkarır). Düşünme modu kapalıdır (`enable_thinking: false`); açık olduğunda yanıt başına ~20 kat token harcıyordu. Çalıştırma: `uv --directory D:\blender\blender_mcp\mcp run python gen_asset.py <Ad> "<tarif>" [--start N --prev-script F --feedback "…"]`. Blender'ın MCP eklentisi (:9876), llama (:8080) ve blender-mcp (:9191) açık olmalıdır.
- Yeni Mixamo klipleri (FBX, Without Skin, 30 fps): `Running`, `Drinking`, `Angry`, `Pointing`, `Punching`, `Pushing`, `PullingARope`, `FallingFlatImpact`, `StableSwordOutwardSlash`, `QuickFormalBow`, `SleepingIdle`, `DizzyIdle`, `CrouchIdle`, `ClimbingLadder`. `.meta` dosyaları Unity ilk içe aktarmada üretir (`HumanoidModelPostprocessor` Humanoid ve döngü ayarını yapar; döngü listesi genişletildi, sürüm 4). Mixamo'da çekiç, kürek ve yemek yeme klibi yoktur.

### Yerel LLM ile üretilen altı yeni prop (24 Ekim, kullanıcı onaylı)

Boyutlar Blender eksenindedir (x, y=ileri, z=yukarı), metre; henüz sahneye bağlı değiller. `llm_helpers.py` (`box/cyl/sphere/halfcyl`) ile kurulduklarından kaynak betikler `Tools/BlenderAssets/llm/<Ad>.py` altındadır.

| Varlık | Üçgen | Boyut (m) | Kullanım |
|---|---:|---|---|
| `Cannon` | 6124 | 0,61 × 1,15 × 0,54 | Topçu istasyonu topu (bütçenin üstünde; gerekirse segment sayısı düşürülür) |
| `ShipMast` | 644 | 2,80 × 0,89 × 4,20 | Yelkenli direk, kırmızı şeritli krem yelken, karga yuvası |
| `PalmTree` | 4400 | 2,73 × 2,90 × 4,17 | Liman/ada ağacı |
| `TreasureChest` | 1572 | 0,99 × 0,64 × 0,74 | Hazine sandığı (kapak hafif açık, altın paralar) |
| `TavernSign` | 1168 | 1,65 × 0,30 × 1,63 | Salty Gull tavernası tabelası (martı kanatları dik, ileride düzeltilebilir) |
| `FloatingCrate` | 1824 | 1,10 × 1,17 × 1,05 | Denizde sürüklenen sandık (fırsat olayı) |

### `Mixamo_RepairHammering` (24 Ekim, elle yazılmış animasyon)

Mixamo'da çekiç klibi olmadığından `Mixamo_Idle` iskeleti üzerinde `Tools/BlenderAssets/anim/hammer_clip.py` ile yazıldı: 40 kare, 30 fps, döngülü (kaldır → vur → sek → kaldır). Pozlar açı yerine "üst kol ve ön kol şu yöne baksın" hedefleriyle çözülür (Mixamo kemikleri kemik yönüne değil armatür eksenine hizalıdır, eklem konumu `head`'dir). Sağ el çekici kaldırıp göğüs-bel hizasında, ~40 cm önde vurur; sol kol çiviyi tutar; gövde 3° → 10° öne eğilir. Diğer kemikler Idle'ın ilk karesinden sabittir. Önizleme: `docs/art/repair_hammering_preview.png` (çubuk figür + `RepairHammer`). Çekiç el kemiğine takılı değildir; sahnede `RightHand` soketine, sap ön kol yönünde olacak şekilde bağlanmalıdır. Ayrı bir Unity projesinde Humanoid import (`animationType: 3`, `loopTime: 1`) hatasız doğrulandı; ana projede ve gerçek karakter üzerinde oynatma doğrulanmadı. `HumanoidModelPostprocessor` döngü listesi ve `GetVersion` (5) güncellendi.
