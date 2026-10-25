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
- **Purser** (`UI/PurserPanel.cs`, `VoyageSimulation.BuyUpgrade`, `PrototypeNpcAnimator`): Manifest masasının arkasındaki NPC. Crosshair onun üzerindeyken `E` `PrototypeView.OpenPurser()` ile paneli açar (imleç serbest, ekip yerinde durur); düğmeler `InteractRpc` (yolcu alma / yeni sefer) ve `BuyUpgradeRpc` çağırır, doğrulama host'ta yapılır (gemide, masaya `InteractionRange` içinde, limanda). Upgrade fitting'leri simülasyonda durur ama sahnede gizlenir ve crosshair onları seçmez. NPC'lerin (purser ve liman sakinleri) canlı gözleri `GazeDirector` ile gelir; `PrototypeNpcAnimator` onları idle/konuşma/el sallama arasında gezdirir.
- **Ödüller** (`Runtime/Awards`, `VoyageTally.cs`): host oyuncu başına `CrewTally` sayar (fırlatılan yolcu, servis, tamir adımı, topçu isabeti, tahsil edilen fare, dock job teslimi, dümen süresi), `PrototypeSession.Tallies` ile yayınlar ve migration snapshot'ı taşır; `Awards.Compute` saf kurallarla sefer raporunun öne çıkanlarını seçer. Sayaç hiçbir kuralı etkilemez. Bkz. [HUD.md](HUD.md).
- **İş yükü ölçeği** (`Runtime/Workload`, `VoyageWorkload.cs`): aktif Crew member sayısına göre baskı çarpanı (4: 1,0; 3: 0,8; 2: 0,6; tek: 0,45). Host ilk kalkışta (`BeginWorkload`) korsanın gelişini (12 sn), elektrik arızasını (22 sn) ve ilk olay gecikmesini 1 / çarpan ile uzatır; olaylar arası bekleme ve korsan atış aralığı (8 sn) de her kurulduklarında uzatılır. Sızıntı, yolcu sayısı, ekonomi ve sığlık/dalga etkilenmez. Zaman çizelgesi kalkışta sabitlenir ve migration snapshot'ında taşınır (sürüm 14); sefer sırasında oyuncu girip çıkması çalışan bir geri sayımı yeniden başlatmaz. Çarpanlar ilk deneme değerleridir. Ağda değişiklik yok.
- **Yüzme** (`VoyageSwimming.cs`, `PrototypeView`, `PrototypeCrewAnimator`): gemi çevresindeki su gemi-yerel koordinatlarda simüle edilir (yüzen gemiyle birlikte hareket eder; limanda iskele 14 m solda). `CrewState.Swimming` ile ağda taşınır, protokol 23 / snapshot 20. Limandayken (`HarborOpen`) güverte/iskele halat merdiveninin (`Ladders`, dört adet) üstünde E ile suya dalınır, herhangi bir merdivenin dibinde E ile çıkılır; yüzücü gövdeye (±5,8 × ±8,8) giremez, yürümekten %40 yavaştır, zıplayamaz, istasyon/yolcu/liman komutlarını kullanamaz ve kapı onu geçirmez. Yüzücü varken gemi kalkamaz. Rogue wave isabet edince (`WashCrewOverboard`) dalganın gittiği yöndeki korkuluğa 2,2 m içinde duran, dümen/top/vinçte olmayan mürettebat denize savrulur; 30 sn içinde merdivene yetişemezse halatla çekilir ve 5 coin öder. Görünümde gövde su çizgisinden `SwimBodyOffset` aşağıda, kamera su üstünde 0,4 m'dedir; alan değişimleri kayarak yapılır.
- **Mini oyunlar ve aktiviteler** (`Runtime/MiniGames`, `VoyageActivities.cs`, `UI/MiniGameOverlay.cs`, `PrototypeActivityView`): kilit açma (gömülü sandık), çekiçle tahta çakma (sızıntı), olta (iskele kenarı) ve kazı (kum) ile yüzerken toplanan batık paralar. Oyun kuralları (`LockpickGame`, `HammerGame`, `FishingGame`, `DigGame`) saf ve tohumludur; host `ActivityCommand` ile oyunu açar (menzil, boş eller, güvertede/karada olma), tohumu ve zorluğu `MiniGameStartRpc` ile o oyuncuya yollar, istemci `MiniGameOverlay` ile oynar ve `MiniGameResultRpc` ile sonucu bildirir. Host sonucu yalnız açtığı oyunla (tür, hedef, oyuncu) eşleşiyorsa ve süre makulse ödüler (`FinishMiniGame`); balık türünü istemci değil tohum belirler. Her limanda 2 sandık, 4 kazı yeri, 3 olta noktası ve 8 batık para vardır (`DigMask`, `ChestMask`, `CoinMask`, `ActivityPortKey`; protokol 25 / snapshot 22) ve yeni limanda yenilenir. Ödüller: sandık 25-60 coin (+temiz açılışta itibar), kazı birkaç coin/itibar, balık galeye 2-5 yiyecek, tahta çakma sızıntıyı kapatır (başarısızlık 1 adım verir), her para 5 coin, sekizinci para +25 ve itibar.
- **Yaptığını göster** (`CrewState.Doing`, `PrototypeCrewTools`): mini oyun oynayan Crew member herkes için olta/kazma/kilit açma/çekiçleme pozuna geçer (`CrewPose.FishingIdle`, `Digging`, `LockPicking`, `Hammering`), yerinde durur (`StepWalker` girdiyi yok sayar) ve sağ elinde aracı (`FishingRod`, `Shovel`, `RepairHammer`) tutar; araç animasyonlu gövdenin `RightHand` kemiğini izler, yerel oyuncuda birinci şahıs görünümünde gösterilmez. Protokol 26 / snapshot 23. Suya atlama ve çıkış (`FeedbackKind.DivedIn/ClimbedOut`) sıçrama efekti ve sesi üretir.
- **Yolcu yüzmesi** (`PassengerState.CanSwim`, `VoyageSwimming.PassengerSwims`): yolcuların yaklaşık %70'i yüzebilir (sefer numarasına göre değişir, protokol 24 / snapshot 21). Denize düşen yüzücü en yakın gemi merdivenine 1,4 m/sn ile yüzer (3-8 sn), kendi başına çıkıp sersemler ve koltuğuna yürür; yüzemeyen, kırmızı-beyaz simit (`Floaties`) takar, yerinde çırpınır (`SwimPanic`) ve 8 sn sonra korkuluktan çekilir. Etiket "can't swim" der. Yürüyüş, tırmanış ve poz zinciri diğer yolcu geçişleriyle aynıdır.
- **Dünya kaydı** (`Runtime/Save/WorldSave.cs`, `VoyageWorldSave.cs`): host'un son dünyası cihazda saklanır ve menüden "CONTINUE SOLO / CONTINUE ONLINE" ile sürdürülür. Kayıt, host migration anlık görüntüsünün (`ExportMigrationData`) gzip + base64 hâlidir (PlayerPrefs, WebGL'de IndexedDB); yalnız limanda (Boarding/Arrived/Failed) ve lobi dışında, her 15 sn ve limana varınca ve host ayrılırken yazılır, denizde asla. Devam ederken anlık görüntü bekleyen migration verisi gibi yüklenir (`VoyageMigrationDataHandler.Stage`); `ReadyForNewParty` bağlı oyuncusu olmayan slotları boşaltır: solo devamda hepsi, online devamda kimliği (`PlayerId`) olmayanlar; kimlikli oyuncular yeniden bağlanınca kendi slotlarını ve konumlarını geri alır. Bekleyen (bağlı olmayan) slotlar lobide Ready beklenmez. Yeni dünya başlatmak kaydı değiştirir; Settings sayfasından silinebilir. Batch çalıştırmalar gerçek kayda dokunmaz (`WorldSave.Enabled`). Ağ protokolü değişmedi.
- **Gangway kapısı** (`VoyageGangway.cs`, `PrototypeGangwayView`, `PrototypeReleaseBuilder`): gemiden karaya geçiş için düğme yoktur. Eller boşken kapıya (güvertede `ShipGangway`, karada `HarborPoint(BoardShip)`) 1 m yaklaşıp kapıya doğru yürümek (`TryGangway`, yön eşiği 0,25) geçişi host'ta yapar; 0,8 sn bekleme gidiş-gelişi önler. `E` ile eski `HarborCommandRpc` hâlâ çalışır (betikli oyuncular için) ama HUD ipucu düğme göstermez. Görünümde alan değişimi 4,5/sn ile sönen bir kayma ile yumuşatılır (`PrototypeView.crewGlide`); `GangwayGate` modeli yaklaşan kişi veya yolcu için kanatlarını açar. Ağ protokolü değişmedi.
- **Yolcu giriş/çıkış akışı** (`VoyagePassengerFlow.cs`): limanlı gemide yolcular Manifest'ten sonra iskele hattında sıraya girer (`Boarding`), kapıdan geçip merkez koridordan (`AisleGoalIn`) koltuğuna yürür; varışta (`Leaving`) aynı yolun tersiyle gemiden iner ve pier'in ucunda ortadan kalkar. Kalkış herkes oturana kadar, yeni sefer herkes inene kadar bekler. Limansız test gemileri ve `VoyageSimulation.WalkingPassengers = false` eski anlık oturmayı korur. Yalnız `OpenSeats` (kapasite yükseltmesiyle açılan koltuklar) engel sayılır ve sahiplenilebilir; kapalı koltuklar katlıdır, çarpışmaz ve oturulamaz.
- **Yolcu geçişleri** (`VoyagePassengers.cs`, `PassengerPresentation`, `PrototypePassengerAnimator`): tutulan, fırlatılan veya kurtarılan yolcu bir anda koltukta belirmez. Host zinciri yürütür: fırlatma `Airborne` → `Landing` (yerde yatar, 1,2 sn) → `Dizzy` (1 sn); bırakma `Standing` (0,5 sn); kurtarma `Rescuing` → güvertenin kenarında `Dizzy`. Sonra `Returning`: yolcu 1,6 m/sn ile koltuğuna yürür, istasyonların etrafından dolanır, 8 sn içinde varamazsa koltuğa oturtulur. Bırakılan yolcu en yakın boş koltuğu seçer (`PassengerState.SeatClaim`, 0 = kendi koltuğu); iki yolcu aynı koltuğu tutamaz. Yürüyen veya sersem yolcu yeniden kaldırılabilir, yatan yolcu kaldırılamaz; bilet ve servis yalnızca oturan (`OnDeck`) yolcuyla yapılır. İstemci her hareket durumu için ayrı poz oynatır (`Lying`, `Dizzy`, `Standing`, `Walking`), yürürken gidiş yönüne döner. Bkz. [HUD.md](HUD.md).
- **Kaptan engelleri** (`Runtime/Hazards`, `VoyageHazards.cs`): sığlıklar ve rogue wave. `HazardLogic` saf kurallardır (yerleşim, hasar, dalga zamanlaması). Sığlık konumları ağda gitmez: host `HazardState.Seed` ve rotadan üretir, istemciler aynı tohumdan `ShoalSet` ile türetir. Host `TickHazards` ile gemiyi sığlıktan iter, hıza oranlı hasar verir (1,5 sn bekleme) ve hızı keser; rogue wave 5 sn önceden `HazardState.Wave` ile bildirilir ve geminin pruvası dalgaya 50° içinde değilse hasar verir. `HazardView` kayaları çizer, minimap 60 m içindekileri gösterir, HUD uyarı kartı ve `PrototypeFeedback` korna/afiş üretir. Liman girişi ve kalkış noktası çevresi güvenli koridordur. Bkz. [HUD.md](HUD.md).
- **Sinyaller** (`Runtime/Signals`, `UI/SignalLayer.cs`): ping ve emote. `PrototypeView` `Q` girdisini okur (kısa basış ping, basılı tutma çark), `PrototypeSession.SignalRpc` host'a gönderir; host doğrular, oyuncu başına 1 sn ile sınırlar ve `SignalBroadcastRpc` ile herkese yayar. Simülasyon durumuna dokunmaz; yalnızca sunumdur. Bkz. [HUD.md](HUD.md).
- **Ayarlar** (`Runtime/Settings`): `GameSettings` kullanıcı tercihlerini (ses seviyeleri, arayüz boyutu, kamera sarsıntısı, bakış hızı, titreşim, ipuçları) cihazda saklar ve `Changed` olayı verir. `FeedbackSettings` ses kazancını buradan türetir (`SfxGain`, `MusicGain`), `PrototypeFeedback` sarsıntı ölçeğini, `PrototypeView` bakış hızını, `HudController` arayüz ölçeğini okur. `Haptics` kontrolcü titreşimini verir. Oyun içi menü `PauseMenu` ve `UnstuckRpc` için bkz. [HUD.md](HUD.md).
- **`InteractionTargeting`** (`Runtime/Interaction`): crosshair'in neyin üzerinde olduğunu ve ne yapılabileceğini belirler; tipli `InteractionTarget` döndürür. Kamera gerektirmez; göz konumu ve bakış yönü girdidir.

## Kural: host otoritesi

İstemci hiçbir oyun durumunu kendisi değiştirmez. Etkileşimler `InteractRpc`, `HarborCommandRpc`, `PassengerCommandRpc` gibi RPC'lerle istenir, host `VoyageSimulation` içinde doğrular (menzil, bekleme süresi, kaynak, sahiplik) ve sonucu yayınlar. Yeni bir eylem eklerken doğrulama simülasyona, RPC `PrototypeSession`'a, girdi `PrototypeView`'a, gösterim HUD'a/sunuma konur ([ADR-0001](adr/0001-host-authoritative-simulation-over-relay.md)).

Hareket istisnadır: yerel Player yürürken tahmin yürütür ve host snapshot'ına göre uzlaştırır ([NETCODE.md](NETCODE.md)).

## Crew member kimliği

Dört Crew member sahnede hazır slottur ve Relay oyuncu kimliğine bağlanır; Player kopsa bile slot korunur ([ADR-0002](adr/0002-crew-members-are-scene-slots.md)). NGO player prefab'ı kullanılmaz.

## Ağ sürümü ve migration

- `PrototypeHud.NetworkProtocolVersion` (şu an 17; 24 Ekim kaynak incelemesinde 15): durum yapıları veya mesaj şeması değişince elle artırılır. Farklı sürümlü build'ler bağlanamaz; eski sekmeler yenilenmelidir. Güncel değer için kod esas alınır.
- `VoyageMigrationDataHandler` (`VoyageMigration.cs`): host ayrılırken anlık sefer durumu snapshot'a yazılır, yeni host'ta yüklenir. Yeni kalıcı oturum alanı eklenirse burada da taşınmalıdır.
- Kariyer ilerlemesi (`CareerProgressStore`) yalnızca host cihazında yerel olarak tutulur (WebGL'de tarayıcı depolaması); anlık sefer durumu buna girmez.

## Sahne ve builder'lar

`Assets/ShipMayham/Scenes/ShipPrototype.unity` sahnesi `Editor/Prototype*Builder.cs` scriptleriyle üretilir ve artımlı güncellenir (**Ship Mayham → Upgrade Prototype (...)** menüleri). Güncelleyiciler idempotent olmalı, mevcut serileştirilmiş referansları korumalıdır. Sahneyi elle düzenlediysen builder'ı yeniden çalıştırmadan önce farkı kontrol et. UI sahnede yer almaz ([ADR-0004](adr/0004-runtime-built-ugui.md)).

## Platformlar

Hedefler Windows ve WebGL'dir. WebGL'de doğrudan UDP ve gerçek soket yoktur: solo oyun `SoloTransport` (soketsiz yerel host) ile, çevrimiçi oyun Relay/WSS ile çalışır. Yayın süreci [WEBGL_DEPLOYMENT.md](WEBGL_DEPLOYMENT.md).

## Sanat

Modeller Blender'da betikle üretilir (`Tools/BlenderAssets`), FBX olarak içe aktarılır ve builder'larla sahneye bağlanır. Süreç ve kurallar [ART_PIPELINE.md](ART_PIPELINE.md).
