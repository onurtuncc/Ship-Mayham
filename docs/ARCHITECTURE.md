# Mimari

Bu doküman kodun genel şeklini anlatır. Kavramların tanımı için [GLOSSARY.md](../GLOSSARY.md), kararların nedeni için [docs/adr](adr/), ağ ayrıntıları için [NETCODE.md](NETCODE.md) okunur.

## Assembly'ler

| Assembly | Yol | İçerik |
| --- | --- | --- |
| `ShipMayham.Runtime` | `Assets/ShipMayham/Runtime` | Oyun kuralları, ağ, sunum ve HUD. Oyuna giren her şey. |
| `ShipMayham.Editor` | `Assets/ShipMayham/Editor` | Sahne builder'ları (`Ship Mayham` menüsü), WebGL/Windows build giriş noktaları. |
| `ShipMayham.Tests` | `Assets/ShipMayham/Tests/Editor` | EditMode ve Editor-içi PlayMode testleri. |

## Katmanlar

```
Girdi (PrototypeView) ──niyet──▶ PrototypeSession (NetworkBehaviour, RPC)
                                      │ çağırır
                                      ▼
                          VoyageSimulation (saf C#, sadece host)
                                      │ üretir
                    NetworkVariable / snapshot RPC'leri
                                      ▼
   PrototypeView + *View sınıfları + Feedback + HUD (sadece gösterir)
```

- **`VoyageSimulation`** (`VoyageSimulation*.cs`, `Voyage*.cs`): oyunun tüm kuralları. Sahne, RPC veya görsel bağımlılığı yoktur; `partial` sınıf olarak alanlara bölünmüştür (Combat, Events, Harbor, Passengers, DockJobs, RepairSupplies...). Sadece host'ta çalışır ve doğrudan birim testlenebilir.
- **`PrototypeSession`**: simülasyonu ağa bağlayan tek `NetworkBehaviour`. İstemciden gelen RPC'leri doğrular ve simülasyona iletir, durumu `NetworkVariable`/`NetworkList` ve hareket snapshot'larıyla yayınlar.
- **Durum yapıları** (`PrototypeState.cs`, `EncounterState.cs`, `VoyageEventState.cs`...): `INetworkSerializable` struct'lar; ağ şemasının kendisidir.
- **`PrototypeView`**: yerel Player'ın girdisini okur, bakış/kamerayı yönetir, etkileşim hedefini seçer (`FindTarget`) ve Crew member/yolcu görsellerini durumdan çizer. Oyun kuralı içermez.
- **Sunum bileşenleri** (`Prototype*View`, `Ship/`, `Feedback/`, `Motion/`): yalnızca durumu okuyup görsel/ses/efekt üretir. Görsel değiştirmek için `Visuals` çocukları değiştirilir; gameplay anchor'ları ve referanslar korunur.
- **HUD** (`UI/`): bkz. [HUD.md](HUD.md). `PrototypeView.Prompt`, `Session` ve `Voyage` durumunu okur; hiçbir kural değiştirmez.
- **`PrototypeHud`**: HUD'ın adı kalsa da artık yalnızca bağlantı akışını yönetir: solo, Relay oturumu kurma/katılma, host migration olayları, sahneyi yeniden yükleyerek menüye dönüş.
- **Lobi** (`VoyageLobby.cs`, `LobbyRules.cs`, `UI/LobbyScreen.cs`): online voyage `Session phase = Lobby` ile başlar ([ADR-0005](adr/0005-lobby-is-a-session-phase.md)). `PrototypeHud` host oluştururken `Session.LobbyBeforeVoyage` ayarlar; simülasyon `OpenLobby()` ile bekler, `Tick` ve komutlar durur. Slotların adı/hazır bilgisi `Session.Profiles` ile yayınlanır; `SetNicknameRpc`, `SetReadyRpc`, `StartVoyageRpc` host'ta `VoyageSimulation` kurallarını çalıştırır (host dışında kimse başlatamaz, host Ready basmaz). `LobbyRules` kuralı host ve arayüz için ortak tutar.
- **`CrewRoster`**: slotlara kimin girebileceği: bağlantı onayı, bağlantının arkasındaki kalıcı oyuncu kimliği ve oturum listesiyle uzlaştırılmayı bekleyen ayrılmalar. Unity veya Relay nesnesi tutmaz; `PrototypeHud` oluşturur, `PrototypeSession.Roster` üzerinden paylaşılır (statik erişim yoktur) ve doğrudan test edilir.
- **`InteractionTargeting`** (`Runtime/Interaction`): crosshair'in neyin üzerinde olduğunu ve ne yapılabileceğini belirler; tipli `InteractionTarget` döndürür. Kamera gerektirmez; göz konumu ve bakış yönü girdidir.

## Kural: host otoritesi

İstemci hiçbir oyun durumunu kendisi değiştirmez. Etkileşimler `InteractRpc`, `HarborCommandRpc`, `PassengerCommandRpc` gibi RPC'lerle istenir, host `VoyageSimulation` içinde doğrular (menzil, bekleme süresi, kaynak, sahiplik) ve sonucu yayınlar. Yeni bir eylem eklerken doğrulama simülasyona, RPC `PrototypeSession`'a, girdi `PrototypeView`'a, gösterim HUD'a/sunuma konur ([ADR-0001](adr/0001-host-authoritative-simulation-over-relay.md)).

Hareket istisnadır: yerel Player yürürken tahmin yürütür ve host snapshot'ına göre uzlaştırır ([NETCODE.md](NETCODE.md)).

## Crew member kimliği

Dört Crew member sahnede hazır slottur ve Relay oyuncu kimliğine bağlanır; Player kopsa bile slot korunur ([ADR-0002](adr/0002-crew-members-are-scene-slots.md)). NGO player prefab'ı kullanılmaz.

## Ağ sürümü ve migration

- `PrototypeHud.NetworkProtocolVersion` (şu an 10): durum yapıları veya mesaj şeması değişince elle artırılır. Farklı sürümlü build'ler bağlanamaz; eski sekmeler yenilenmelidir.
- `VoyageMigrationDataHandler` (`VoyageMigration.cs`): host ayrılırken anlık sefer durumu snapshot'a yazılır, yeni host'ta yüklenir. Yeni kalıcı oturum alanı eklenirse burada da taşınmalıdır.
- Kariyer ilerlemesi (`CareerProgressStore`) yalnızca host cihazında yerel olarak tutulur (WebGL'de tarayıcı depolaması); anlık sefer durumu buna girmez.

## Sahne ve builder'lar

`Assets/ShipMayham/Scenes/ShipPrototype.unity` sahnesi `Editor/Prototype*Builder.cs` scriptleriyle üretilir ve artımlı güncellenir (**Ship Mayham → Upgrade Prototype (...)** menüleri). Güncelleyiciler idempotent olmalı, mevcut serileştirilmiş referansları korumalıdır. Sahneyi elle düzenlediysen builder'ı yeniden çalıştırmadan önce farkı kontrol et. UI sahnede yer almaz ([ADR-0004](adr/0004-runtime-built-ugui.md)).

## Platformlar

Hedefler Windows ve WebGL'dir. WebGL'de doğrudan UDP ve gerçek soket yoktur: solo oyun `SoloTransport` (soketsiz yerel host) ile, çevrimiçi oyun Relay/WSS ile çalışır. Yayın süreci [WEBGL_DEPLOYMENT.md](WEBGL_DEPLOYMENT.md).

## Sanat

Modeller Blender'da betikle üretilir (`Tools/BlenderAssets`), FBX olarak içe aktarılır ve builder'larla sahneye bağlanır. Süreç ve kurallar [ART_PIPELINE.md](ART_PIPELINE.md).
