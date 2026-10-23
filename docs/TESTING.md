# Test rehberi

## Neler var

| Katman | Nerede | Ne kanıtlar |
| --- | --- | --- |
| EditMode birim testleri | `Assets/ShipMayham/Tests/Editor` | `VoyageSimulation` kuralları, ağ simülasyonu (`NetworkSimulationTests`), kariyer kaydı, HUD prompt mantığı (`HudPromptTests`). |
| Sahne testleri | aynı klasör, `PrototypeSceneFixture` türevleri | Yazılı sahnenin bağlantıları, serileştirilmiş referanslar, builder sonuçları. |
| Editor-içi PlayMode testleri (`[UnityTest]`) | aynı klasör | Solo host'la gerçek sahnede akış (`HudSceneTests`, `GameFeelTests`...). |
| Dört süreçli tam sefer | `Scripts/Test-Voyage.ps1` | Gerçek Windows development build'iyle dört süreçte baştan sona sefer. |
| Relay host devri | `Scripts/Test-RelayMigration.ps1` | Gerçek internet oturumunda host kaybı ve migration ([RELAY_MIGRATION_TEST.md](RELAY_MIGRATION_TEST.md)). |
| Web imleç koruması | `node Scripts/Test-WebCursor.cjs` | `WebCursor.jslib` pointer-lock davranışı. |

Dört insanla oynanış dengesi, farklı tarayıcılar/cihazlar ve gerçek pointer-lock kabulü otomatik testlerin kapsamı dışındadır; elle yapılmalıdır.

## EditMode testlerini çalıştırma

Editor, aynı proje açıkken batch testini çalıştıramaz. Seçenekler:

1. **Editor'den**: Window → General → Test Runner → EditMode.
2. **Kapalı projeyle batch**:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults Logs\results.xml -logFile Logs\unity.log
```

3. **Editor açıkken**: projeyi (Library hariç) ayrı bir klasöre kopyalayıp orada batch çalıştır; ilk içe aktarma uzun sürer, sonraki koşular `Assets` farkını eşitleyerek hızlanır.

## Yeni test yazarken

- Sahneyi okuyan testler `PrototypeSceneFixture`'dan türer; sahneyi eklemeli açıp yalnızca kendi açtığını kapatır. Test sonunda açık sahne bırakılmamalıdır.
- PlayMode testleri `yield return new EnterPlayMode()` ve `EditorSceneManager.LoadSceneAsyncInPlayMode` çağrılarını **test metodunun kendisinde** yapmalıdır. Yardımcı bir `IEnumerator` içinden yapılırsa sahne yüklenmez ve test `NullReferenceException` ile düşer.
- Solo akış için kalıp: sahneyi yükle, `hud.ConnectSolo()`, `Session.IsSpawned` olana kadar bekle. Etkileşimler arasında 0,35 sn bekleme vardır; bekleme süreleri bundan rahat büyük olmalıdır (0,6 sn).
- Kurallar `VoyageSimulation` seviyesinde test edilir; sahne testi yalnızca bağlantı ve ağ durumunu doğrular.
- Ağ davranışı gerçek ağa çıkmadan `NetworkSimulationTests` içindeki sanal-zamanlı simülatörle (gecikme, jitter, kayıp, sıra bozulması, TCP duraklaması) denenir.

## Görsel kontrol (ekran görüntüsü)

Bazı testler isteğe bağlı olarak ekran görüntüsü üretir:

```powershell
$env:SHIPMAYHAM_CAPTURE = "1"
```

- `GameFeelTests` → `Logs/GameFeel.png`
- `HudSceneTests.CaptureHudScreens` → `Logs/HudMenu.png`, `HudJoin.png`, `HudSettings.png`, `HudLobby.png`, `HudGame.png`, `HudPrompt.png`, `HudContext.png`, `HudHelp.png`

Değişken yokken bu testler atlanır. Batch'te grafik cihazı gerektiğinden `-nographics` verilmez.

## Bilinen tuzaklar

- Test koşusu `Library/PackageCache` içindeki `shadergraph` için `GUID` derleme hataları yazdırabilir; bu proje kodunun hatası değildir ve sonucu etkilemez.
- Yeni `.cs` dosyalarının `.meta` dosyalarını Unity üretir; commit'e eklemeyi unutma.
- Otomatik Relay doğrulama build'leri (`--ship-online-host`) lobiyi atlar (`PrototypeHud.LobbyBeforeVoyage = false`); lobi akışı yerelde `HudSceneTests` ve `LobbyTests` ile sınanır, gerçek Relay üzerinden elle denenmelidir.
- Ağ şemasını değiştiren her değişiklik protokol sürümünü ve migration snapshot'ını da güncellemelidir (bunu yakalayan otomatik bir kontrol yoktur).
