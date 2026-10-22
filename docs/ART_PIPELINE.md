# Üretilmiş 3B varlıklar (Blender pipeline)

Bu doküman `Assets/ShipMayham/Art/Generated/Models/` altındaki FBX varlıklarının nasıl üretildiğini ve nasıl kullanılacağını anlatır. Hedef: GDD'deki parti/arkadaş oyunu tonuna uyan, stilize, düşük poligonlu ve tek paletli varlıklar.

## Yapı

| Yol | İçerik |
|---|---|
| `Assets/ShipMayham/Art/Generated/Models/*.fbx` | Unity'ye alınan modeller (her biri tek mesh, malzeme başına submesh) |
| `Tools/BlenderAssets/assetlib.py` | Ortak Blender (bmesh) yardımcıları, paylaşılan palet (`std()`), FBX dışa aktarma ve önizleme render'ı |
| `Tools/BlenderAssets/assets/*.py` | Her varlığın betiği; betik değişirse FBX yeniden üretilir |
| `Tools/BlenderAssets/build_asset.sh` | Tek varlığı headless Blender ile üretir |
| `docs/art/generated_*.png` | Üretilen varlıkların önizleme levhaları |

Yeniden üretim (Blender 5.1+):

```bash
Tools/BlenderAssets/build_asset.sh Buoy ./out/Buoy
Tools/BlenderAssets/build_asset.sh Character_Crew3 ./out/Crew3   # Character_Crew1..4, Character_Passenger1..3 aynı betiği paylaşır
```

Çıktı: `<Ad>.fbx`, `<Ad>.png`, `<Ad>_back.png`. FBX'i `Assets/ShipMayham/Art/Generated/Models/` altına kopyalayınca Unity `.meta` dosyasını yoksa üretir; bu depodaki `.meta` dosyaları sabit GUID'lerle önceden yazılmıştır, bu yüzden dosya adını değiştirmeden üzerine yazmak referansları korur.

## Sözleşmeler

- Birim metredir (`1 Blender birimi = 1 m`), FBX `apply_unit_scale` ve `bake_space_transform` ile Unity eksenlerine (`+Z ileri, +Y yukarı`) çevrilir.
- Pivot taban merkezidir (en alçak nokta y=0, x/z ortalı). Etkileşim anchor'ları sahnede ayrıca kalır; bkz. [PROTOTYPE.md](PROTOTYPE.md) "Sahne ve model değiştirme sözleşmesi".
- Karakterler Unity'de `+Z` yönüne bakar. Gemi modelinde baş `+Z`'dir. Bank (`DeckBench`) oturma yüzü `+Z`'ye, dümen simidi (`ShipWheel`) oyuncunun durduğu `-Z` tarafına bakar.
- Tek mesh, malzeme başına submesh: `Wood`, `Wood_Dark`, `Metal`, `Brass`, `Red`, `Cream` gibi ortak isimler tüm varlıklarda aynıdır. Servis rengi kuralı korunur: yiyecek turuncu (`FoodPlatter`), içecek mavi (`RumBottle`).
- FBX malzemeleri yalnızca düz renk taşır; doku yoktur. URP için gerekirse proje materyalleriyle (`Assets/ShipMayham/Art/Materials`) eşlenebilir.

## Varlık listesi

Boyutlar Blender eksenindedir (x, y=ileri, z=yukarı), metre. Üçgen sayıları `deliver()` çıktısından alınmıştır.

| Varlık | Üçgen | Boyut (m) | GDD/prototip karşılığı |
|---|---:|---|---|
| `LifeRing` | 1408 | 0,77 × 0,77 × 0,16 | Kurtarma can simidi (`RescueFloat_Visual`) |
| `Suitcase` | 144 | 0,63 × 0,24 × 0,49 | Yolcu bagajı |
| `DeckBench` | 132 | 1,20 × 0,46 × 0,92 | Yolcu koltuğu (`SeatVisuals`) |
| `ShipWheel` | 636 | 0,96 × 0,50 × 1,58 | Dümen istasyonu (Helm) |
| `Bollard` | 636 | 0,60 × 0,60 × 0,72 | Liman bağlama babası |
| `Lantern` | 300 | 0,22 × 0,22 × 0,44 | Gemi/liman feneri |
| `RumBottle` | 320 | 0,15 × 0,16 × 0,35 | İçecek (mavi) |
| `FoodPlatter` | 508 | 0,45 × 0,40 × 0,17 | Yiyecek (turuncu) |
| `ServingTray` | 96 | 0,56 × 0,35 × 0,07 | Servis tepsisi |
| `Toolbox` | 276 | 0,46 × 0,23 × 0,44 | Tamir istasyonu (Repair) |
| `FuseBox` | 276 | 0,50 × 0,27 × 0,74 | Elektrik paneli (Power) |
| `CannonballPile` | 3612 | 0,62 × 0,62 × 0,44 | Top mühimmatı |
| `Buoy` | 356 | 0,44 × 0,44 × 0,98 | Deniz şamandırası |
| `TicketCounter` | 244 | 1,80 × 0,80 × 1,70 | Manifest/bilet tezgâhı |
| `MarketStall` | 516 | 2,16 × 1,50 × 2,23 | Liman erzak tezgâhı |
| `DockPlank` | 528 | 4,00 × 2,10 × 1,46 | İskele modülü |
| `Lighthouse` | 860 | 4,60 × 4,60 × 9,15 | Liman fenerinin kule modeli |
| `PirateShipSmall` | 592 | 2,20 × 6,71 × 4,70 | Korsan gemisi (`Ship_Pirate_01`); kavisli (loft) gövde |
| `Anchor` | 364 | 1,06 × 0,13 × 1,53 | Çapa (liman/gemi dekoru, demirleme) |
| `RopeCoil` | 1232 | 0,53 × 0,53 × 0,45 | Halat yumağı (iskele dekoru) |
| `Rowboat` | 190 | 3,03 × 2,50 × 0,97 | Filika (liman dekoru, kurtarma teması) |
| `FlagPole` | 180 | 1,05 × 0,28 × 2,85 | Bayrak direği (kozmetik/liman) |
| `Gangway` | 276 | 1,32 × 3,00 × 0,79 | İskele geçiş tahtası (`Gangway_Visuals`) |
| `CrateStack` | 692 | 2,08 × 1,57 × 1,27 | Sandık + varil yığını (iskele/depo) |
| `HarborCrane` | 232 | 1,40 × 4,45 × 4,61 | Liman vinci |
| `HarborHut` | 268 | 3,60 × 2,70 × 3,35 | Liman terminal binası (`Port_*/Visuals`) |
| `FishCrate` | 360 | 0,70 × 0,45 × 0,29 | Balık sandığı (liman tezgâhı dekoru) |
| `Character_VIP` | 676 | 0,77 × 0,48 × 1,75 | VIP yolcu (silindir şapka, baston) |
| `Character_Crew1..4` | ~524 | 0,75 × 0,35 × 1,49 | Mürettebat; gömlek renkleri `Crew_01..04` materyalleriyle aynı hizada |
| `Character_Passenger1..3` | ~540 | 0,72 × 0,52 × 1,63 | Yolcu varyantları |
| `Character_Pirate` | 576 | 0,88 × 0,37 × 1,64 | Korsan karakteri |

## Durum ve sınırlar

- İlk prop dilimi `ShipPrototype` sahnesine bağlandı: dümen, manifest/bilet tezgâhı, servis tabak/şişesi, tamir çantası, elektrik kutusu, sekiz bank, korsan gemisi, iki liman kulübesi ve iskele baba/halat detayları. Toplam 21 `Model_*` yerleşimi vardır. Diğer modeller, karakterler ve elde taşınan ürünler henüz bağlanmadı. Oynanış kökleri, `InteractionAnchor`, `CarrySocket` ve mevcut Renderer referansları korunur.
- Karakterlerde iskelet/animasyon yoktur (statik poz). Hareket için ayrı rig gerekir; mevcut `StylizedCharacterPack` animasyonları bu modellere uymaz.
- Bağlanan prop dilimi Unity Camera.Render ile incelendi (`Logs/GeneratedProps-Layout.png`); dümenin dik/metre ölçeği ve tüm model materyallerinin URP/Lit kullanımı otomatik sahne testindedir. Bu ilk genel kadraj; her varlığın yakın planı, hareketli korsan kadrajı ve insanlarla birinci şahıs okunurluk testi değildir.
- `CannonballPile` 3612 üçgenle diğerlerinden ağırdır (10 ayrı küre).
- Üretim yöntemi: modeller bu depodaki Blender betikleriyle elle yazıldı. Yerel LLM (llama.cpp + Blender MCP) denemeleri tutarlı sonuç vermedi (sandık ve kupa testlerinde eksik/yığılmış parçalar); bu nedenle çıktılar LLM'den değil betiklerden gelir.

## İlk prop sahne entegrasyonu — 22 Ekim 2026

**Ship Mayham → Upgrade Prototype (Generated Props)** mevcut sahneyi eklemeli günceller. `Visuals/Model_<FBX adı>` wrapper'ı yerleşim/ölçeği, içindeki FBX instance kendi eksen/birim dönüşümünü korur. Oynanış Collider'ları değişmez; eklenen model Collider'ları kapalıdır. Eski primitive nesneler silinmez, yalnız ilgili Renderer gizlenir. Önceki sahne `Logs/ShipPrototype-PreGeneratedProps.unity` altında tutulur. Geri dönüş için model wrapper'ını kaldırıp eski Renderer'ı etkinleştirmek de mümkündür.

FBX düz renkleri, `Art/Materials/Generated_*` altındaki 32 paylaşılan URP/Lit materyaline eşlenir. Model dosyaları ve gömülü materyalleri değiştirilmez; hazır URP materyalleri sonraki çalıştırmalarda yeniden yazılmaz. Renk düzenlemesi bu materyallerden yapılabilir. Gemi gövdesi boyaları ve elektrik durum göstergesi kendi mevcut Renderer'larını kullanmayı sürdürür.

Doğrulama: eski 775 serialized nesne/component ID'si, adları, parent/local transformları ve istasyon anchor/visual/label bağları korunmuştur. Güncelleyicinin tekrar çalışması sahne SHA-256 özetini değiştirmedi (`BAF85CB983400DF1801F939B3FD8254049AAE5B2F0F8609501C6786F66622C0E`). `Logs/GeneratedProps-Isolated.xml` içinde 119/119 test geçti; ilk test denemelerinde yeni testin meta biçimi ve sahne izolasyonu sorunları ayrıca giderildi, başarısız raporlar korunur. `CaptureGeneratedPropsPreview` mevcut sahne preview yolunu kullanır; örnek karakter etkinleştirmeleri sahneye kaydedilmez.

Render/etiket hazırlığından sonra repoya alınan son sahneyle **119/119 tekrar geçti**, Unity exit 0 (`Logs/GeneratedProps-Authored.xml`). Bu sahne henüz `1f0705e` WebGL paketinde yoktur; bir sonraki yayınla dağıtılmalıdır. Unity'nin ürettiği prefab YAML'ındaki boş `value:` satırlarının trailing space biçimi korunur; kaynak kodda whitespace kontrolü temizdir.
