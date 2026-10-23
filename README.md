# Ship Mayham

1–4 kişilik kooperatif parti oyunu. Dört arkadaş, yolcu taşıyan küçük bir geminin mürettebatı olur: gemiyi sürer, yolcuların siparişlerini yetiştirir, sızıntıları yamar, korsanları püskürtür ve hiçbir şeyin planlandığı gibi gitmediği seferleri birlikte atlatır. Birinci şahıs, Windows ve WebGL.

> Durum: oynanabilir prototip. Karakterler ve bazı sanatlar hâlâ geçicidir; dört insanla denge/tempo kabulü yapılmamıştır. Ayrıntı: [docs/PROTOTYPE.md](docs/PROTOTYPE.md).

Canlı WebGL sürümü: <https://onurtuncc.github.io/Ship-Mayham/>

## Çalıştırma

Gereksinim: Unity **6000.4.6f1**.

1. Projeyi Unity'de aç ve derlemenin bitmesini bekle.
2. `Assets/ShipMayham/Scenes/ShipPrototype.unity` sahnesini aç (veya **Ship Mayham → Open Prototype**).
3. Play'e bas. Ana menüde bir takma ad seç. **Play solo** doğrudan oyuna girer. **Host online** bir **lobi** açar (Unity Relay, port yönlendirme gerekmez): kodu paylaş, arkadaşlar **Join online** ile katılıp **Ready** basar, hepsi hazır olunca sen **Start voyage** dersin. Sefer başladıktan sonra da katılınabilir.

Windows build: **Ship Mayham → Build Prototype (Windows)**. WebGL yayını: [docs/WEBGL_DEPLOYMENT.md](docs/WEBGL_DEPLOYMENT.md).

## Kontroller

| Eylem | Klavye / fare | Gamepad |
| --- | --- | --- |
| Hareket / bakış | WASD / fare | Sol / sağ analog |
| Etkileşim | E | A |
| Zıpla (dümende: kalkış / yanaşma) | Space | X |
| Bilet kontrolü / ücret tahsili | F | Y |
| Yolcuyu kaldır / bırak | G | B |
| Fırlat / ateş (top) | Sol tık | RT |
| Top doldur | R | X |
| Menü (devam, ayarlar, sıkıştım, çık) | Esc | Start |
| Kontrol rehberi | F1 | — |

Oyundaki ipuçları kullandığın cihaza göre tuşları gösterir.

## Dokümanlar

| Konu | Dosya |
| --- | --- |
| Ortak dil | [GLOSSARY.md](GLOSSARY.md) |
| Kod mimarisi | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| Alınan kararlar ve nedenleri | [docs/adr](docs/adr/) |
| Oyun tasarımı (vizyon) | [docs/GDD.md](docs/GDD.md) |
| Uygulama planı ve dilimler | [docs/PLAN.md](docs/PLAN.md) |
| Prototipin güncel durumu | [docs/PROTOTYPE.md](docs/PROTOTYPE.md) |
| Ağ ve hareket | [docs/NETCODE.md](docs/NETCODE.md), [docs/RELAY_MIGRATION_TEST.md](docs/RELAY_MIGRATION_TEST.md) |
| HUD ve etkileşim arayüzü | [docs/HUD.md](docs/HUD.md) |
| Test | [docs/TESTING.md](docs/TESTING.md) |
| Sanat üretimi | [docs/ART_PIPELINE.md](docs/ART_PIPELINE.md) |
| Oyun hissi (ses, efekt) | [docs/GAME_FEEL.md](docs/GAME_FEEL.md) |
| Yayın | [docs/WEBGL_DEPLOYMENT.md](docs/WEBGL_DEPLOYMENT.md) |

Özellik dilimlerinin ayrıntıları: [AMMO_FLOW](docs/AMMO_FLOW.md), [BALLISTICS](docs/BALLISTICS.md), [DOCK_SORTING](docs/DOCK_SORTING.md), [HARBOR_TOWN](docs/HARBOR_TOWN.md), [HARBOR_CRANE](docs/HARBOR_CRANE.md), [PASSENGER_CUES](docs/PASSENGER_CUES.md), [REPAIR_SUPPLIES](docs/REPAIR_SUPPLIES.md). Tasarım araştırması: [PARTY_DESIGN_RESEARCH](docs/PARTY_DESIGN_RESEARCH.md).

## Katkı

Kod standartları [AGENTS.md](AGENTS.md) içindedir: mevcut mimariye uy, odaklı değişiklik yap, doğrulamayı çalıştır ve çalıştıramadıklarını açıkça bildir. Ağ şeması değişirse protokol sürümünü artır. Yeni terimleri önce [GLOSSARY.md](GLOSSARY.md)'ye yaz.

## Lisans

MIT, bkz. [LICENSE](LICENSE).
