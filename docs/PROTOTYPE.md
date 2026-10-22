# İlk oynanabilir prototip

**Güncel yayın — dilim 18:** Kaynak `1afa86d`, WebGL `fa132b5`, Pages işi `39729740601` başarılı. Liman kasabası ve Town Salvage işi, protokol/snapshot **8**. Yeni kara alanı için 7/7 odaklı kontrol, Windows/WebGL build ve canlı açılış geçti. Önceki 202 test/v7 Relay kanıtları önceki kaynak içindir; uzun akışlar bu dilimde tekrar çalıştırılmadı. [HARBOR_TOWN.md](HARBOR_TOWN.md). Aşağıdaki eski kaynak/yayın kayıtları tarihseldir.

**Yeni kaynak — dilim 17:** Fiziksel hızlı yama kitleri eklendi; son sahneyle **202/202 test**, Windows build ve dört süreçli tam sefer geçti. v7 gerçek Relay host kaybı geçti. Kaynak ağ/snapshot protokolü **7**. Canlı Pages paketi aşağıdaki v6 yayınıdır; yeni kaynak için ayrı WebGL build/yayın yapılmadan v7 Windows ile aynı oturuma bağlanamaz. Kurallar ve doğrulama: [REPAIR_SUPPLIES.md](REPAIR_SUPPLIES.md).

**Güncel yayın — 22 Ekim, dilim 16:** Kaynak `1e9d53d`, WebGL `e3e2bdd`, Pages işi `39725524917` **başarılı**. Canlı HTML yeni build ile eşleşti; oyun açılışı, BAG SORT arayüzü ve Relay host başlangıcı doğrulandı, tarayıcı error kaydı boştu (`Logs/WebGL-DockSorting-Relay.png`). **182/182 EditMode**, dört süreçli tam sefer/bagaj teslimatı ve v6 gerçek Relay host kaybı geçti. Protokol **6**: eski sekmeleri yenileyin. Aşağıdaki 167 test/v5/web bilgileri önceki dilimin tarihsel kaydıdır. [DOCK_SORTING.md](DOCK_SORTING.md).

**22 Ekim 2026 doğrulaması:** Yolcu ipuçları, uçuşlu gülle ve migration temizliğini içeren EditMode paketi **167/167 geçti** (`Logs/PassengerCues-Final.xml`). Son dört Windows sürecinde tam sefer, hareketli mermi, taşıma ve liman akışı geçti; v5 Relay/WSS host kaybı balistik diliminde ayrıca doğrulandı. Yeni ipuçları [PASSENGER_CUES.md](PASSENGER_CUES.md), balistik ağ kanıtı [BALLISTICS.md](BALLISTICS.md), önceki aralıklı hata ve üyelik düzeltmesi [AMMO_FLOW.md](AMMO_FLOW.md) içinde ayrılır. Normal tarayıcıda gerçek bakış ve dört insanla oyun dengesi kabulü hâlâ gereklidir.

Bu sürüm GDD'nin ilk on beş geliştirme dilimini içerir: karada rota sözleşmesini seç → yolcu al → davranışları gözleyip biletleri kontrol et → gemiyi sür → siparişleri karşıla / yolcu kaosunu ve olayları yönet → arızaları onar / topçuya gülle taşı / uçuşlu atışlarla korsanları püskürt → limana yanaş → ödeme ve itibar al → karada erzak/bakım, boya veya gemide yükseltme seç → ekibi topla → tekrar sefere çık. İlk prop modeli grubu ve yolcu valizleri bağlanmıştır; karakterler hâlâ blok prototiptir.

Güncel web paketi `0115215` (kaynak `d091e03`), Pages deployment `39719323498` başarılıdır: v5/uçuşlu gülle kaynaklı WebGL paketi açıldı, Relay host başlangıcı ve Leave ile menüye dönüş doğrulandı (`Logs/WebGL-Ballistics-Relay.png`), browser error kaydı boştu. Tarayıcıda elle uçuşlu atış/normal pointer-lock kabulü ayrı kalır; hareketli mermi replikasyonu dört Windows sürecinde kanıtlandı. Site: [Ship Mayham](https://onurtuncc.github.io/Ship-Mayham/). Model kapsamı [ART_PIPELINE.md](ART_PIPELINE.md) içindedir.

## Çalıştırma

1. Unity **6000.4.6f1** ile projeyi aç ve derlemenin bitmesini bekle.
2. `Assets/ShipMayham/Scenes/ShipPrototype.unity` sahnesini aç. Alternatif: **Ship Mayham → Open Prototype** menüsü. Sahne henüz yoksa menü ilk kopyayı üretir; mevcut sahneyi yeniden üretmez.
3. Play'e bas; tek oyuncu için **PLAY SOLO (OFFLINE)**, internet üzerinden arkadaşlarla oynamak için **CREATE ONLINE VOYAGE** seç. Solo, WebGL'de de çalışan soketsiz yerel host kullanır.
4. Arkadaşların aynı güncel build'de paylaşılan oturum kodunu girip **JOIN ONLINE VOYAGE** seçer. İnternet bağlantısı Unity Relay üzerinden WSS kullanır; port yönlendirme gerekmez ve en fazla dört oyuncu bağlanabilir. Geliştirme/test amaçlı doğrudan `Connect` API'si Windows/Editor'de UDP `7777` kullanır; WebGL doğrudan UDP istemcisi değildir.
5. Ayrı Windows build için **Ship Mayham → Build Prototype (Windows)**. Sadece prototip sahnesini `Builds/ShipPrototype/ShipPrototype.exe` içine derler; mevcut Build Settings listesini değiştirmez.

Netcode'un editörde sahne senkronizasyonu yapabilmesi için prototip sahnesi Build Settings'e eklenmiştir. Önceden var olan sahne girişleri korunur. Genel Build düğmesi yerine yukarıdaki prototip build menüsünü kullan; mevcut listede eski template sahneleri de bulunuyor.

## İlk sefer

- **Fare / sağ analog:** Birinci şahıs bakış. Kamera oyuncunun göz hizasındadır; kendi gövden kamerayı kapatmaz, diğer oyuncuları görürsün.
- **WASD / sol analog:** Güvertede baktığın yöne göre hareket. Dümendeyken bakıştan bağımsız W/S hız artırma–fren, A/D dönüş.
- **E / gamepad A:** Nişangâhla baktığın yakındaki istasyonla/yolcuyla etkileşim; dümeni al/bırak; siparişi teslim et. Hedef ekranın altında gösterilir.
- **Space / gamepad X:** Güvertede zıpla. Dümendeyken seferi başlat/yanaş; top kullanırken gamepad X doldurma işlevini korur (klavyede R). Dümen/top kullanırken zıplanmaz.
- **F / gamepad Y:** Yakındaki hedef yolcunun biletini kontrol et; ödememişse tekrar basarak 40 ücret tahsil et. Ücret başarılı varışta kasaya geçer. Ödemiş yolcudan tekrar ücret alınmaz.
- **G / gamepad B:** Boş ellerle hedef yolcuyu kaldır; tekrar basarak önüne bırak. Yolcu taşırken **sol tık / RT** fırlatır. Yürüme hızı %70'e iner; servis veya istasyon kullanmak için önce bırak.
- **Esc / gamepad Start:** Fareyi menü için serbest bırak veya yeniden bakışa kilitle. Serbestken hareket ve etkileşim durur; ekranın ortasına tıklayarak da devam edebilirsin.
- **F1:** Ayrıntılı kontrol yardımını aç/kapat. Hedef bulunduğunda nişangâh sarı olur; alt ipucu biletin kontrol/tahsilat aşamasına göre değişir. Bildirimler 6 saniyede söner.
- Sol arka taraftaki **Manifest** istasyonuna yaklaş ve E ile dört yolcuyu al.
- Turuncu **Food**, mavi **Drink** istasyonundan bir ürün al. **Eldeki yiyecek turuncu, içecek mavidir**; diğer oyuncuların taşıdığı ürünler de aynı renkleri kullanır. İstenen ürün yolcunun üzerinde yazılıdır; yakınına git, ona bak ve E ile teslim et. Elinde yanlış ürün varsa servis tezgâhında E ile stoğa geri bırak.
- Ön taraftaki **Helm** istasyonunda E, ardından Space ile hareket izni al. W ile hızlan. İlk hedef kuzeyde, 220 metre uzakta.
- E ile dümeni bıraktığında tekne hızını ve yönünü korur; tek oyuncu da servis yapabilir. Dümenden ayrılmadan önce gerektiğinde S ile yavaşla.
- Seyir başladıktan yaklaşık 25 saniye sonra su kaçağı çıkar. Sağ arka taraftaki yeşil **Repair** istasyonunda dört kez E ile yama yap. Etkileşimler arasında en az 0,35 saniye vardır.
- Sağ öndeki **Cannon** istasyonunda E ile topu al. Fare/sağ analog nişan alır, **sol tık / RT** ateş eder, **R / gamepad X** teslim edilmiş gülleyi 2,5 saniyede doldurur, **E / A** bırakır. Topta yürüyemezsin. Her sefer biri yüklenmiş, 11'i sağ öndeki **AMMO** ambarında toplam 12 gülleyle başlar; limanda ateş edilemez. Boş ellerle AMMO'da E/A bir gülle alır, CANNON'da E/A topçuya teslim eder; topçuyu görevinden çıkarmaz. AMMO'da tekrar E/A geri verir. Top yanı stok üç yuvalıdır; doldurmadaki gülle bir iade yuvası ayırır. Solo oyuncu iskelede stok hazırlayabilir veya topu bırakıp kendisi taşıyabilir. Ayrıntılar [AMMO_FLOW.md](AMMO_FLOW.md).
- 12. saniyede korsan uyarısı, 6 saniye sonra saldırı başlar. Korsan gemiyi takip edip menzilde her 8 saniyede 8 gövde hasarı verir; ilk isabet elektriği keser. Üç isabet korsanı püskürtür; **+50 ödül başarılı varışta** ödenir. Yenilmezse 40 saniye saldırıdan sonra çekilir; ödül verilmez. Yanaşmak da tehdidi bitirir.
- 22. saniyede elektrik arızası çıkar. Sağ ortadaki **Power** paneline bakıp üç kez E ile sigortaları sıfırla. Elektrik yokken yeni yiyecek/içecek alınamaz; eldeki ürün teslim edilebilir veya iade edilebilir. Panelin göstergesi turuncu/yeşil, HUD da metinle durumu gösterir. Korsan ilk atışında daha önce onardığın elektriği tekrar kesebilir; devam eden onarımın ilerlemesini sıfırlamaz.
- Sağ üstteki rota haritası ve HUD mesafesi hedefi gösterir. Hedefin 12 metre yakınına gel, S ile 1 m/s veya altına in, dümendeyken Space ile yanaş.
- Sefer sonunda bilet, servis, bahşiş ve masraflar ayrı gösterilir. Para ortak kasaya bir kez eklenir.
- Mor istasyonlarda motor, yolcu kapasitesi veya konfor yükseltmesi al. Manifest ile yeni sefere hazırlan; tekrar E ile yeni yolcuları al.
- Limandayken geminin sol arka köşesindeki yeşil **ASHORE** işaretine yaklaş, ona bakıp **E / A** ile iskeleye geç. Dümeni/topu bırakmış, yerde ve elleri boş olmalısın. Karada WASD/analog ve zıplama çalışır; terminalin önündeki işaretli iskele şeridinde yürüyebilirsin.
- Yolcuları Manifest'ten almadan önce karadayken sağdaki sözleşme panosundan **COAST / MARKET / STORM** seç. Market Run üç servis karşılığında 35 para; Storm Passage daha erken olay ve sızıntı karşılığında 70+ gövdeyle varışta 50 para verir. Başarılı sözleşmeler kalıcı ortak itibarı yükseltir.
- Aynı panelden **START CARGO RELAY** seçildiğinde turuncu FOOD noktası kasa kaynağına, yeşil HULL noktası teslim alanına dönüşür. Üç kasa teslimatı 24 ortak para ve 1 itibar verir; oyuncular paralel taşıyabilir. Kasayı FOOD'a iade edebilir, elde kasa yokken işi iptal edebilirsin.
- Karadayken **SHIP PAINT** panelinden gövde boyası seçebilirsin. Classic başlangıç görünümünü geri getirir; Seafoam 2, Sunset 5 itibar gerektirir. Boyalar ücretsizdir, gemi gücünü değiştirmez ve host kariyer kaydında tutulur. Fareyle panel kullanımı için Esc ile imleci aç.
- İskelede turuncu **FOOD +8** ve mavi **DRINK +8** tezgâhları 12'şer para, yeşil **HULL SHIELD +20** tezgâhı 30 para ister. Yaklaş, bak ve E/A kullan. Satın alma ortak kasadan anında düşer. Erzak geminin stoğuna eklenir; elde taşınanlar dahil tür başına en fazla 32 ürün depolanır. Gövde koruması sızıntı ve korsanlardan gelecek toplam 20 hasarı emer; sıfırlanmadan tekrar satın alınamaz.
- **BOARD SHIP** işaretinde E/A ile güverteye dön. Karada aktif oyuncu varken kaptan ayrılamaz, Manifest de yeni sefere geçiremez. Ayrılan oyuncu bağlantısını kapatırsa ekip onu beklemek zorunda kalmaz.

Bu ilk döngüde yeni sefer başlangıç limanına geri yerleştirir. Yolcular binme/yürüme animasyonu olmadan koltuklarda belirir. İki limanda da dar bir yürünebilir iskele/hazırlık alanı vardır; terminal binalarının içi, açık kara dünyası ve görevler yoktur. Başarılı yanaşmada gemi sabit rıhtım konumuna/yönüne hizalanır. Geçişler E/A ile anlıktır; yürüyerek kesintisiz plank geçişi değildir. Başarısız seferde limana geçiş kapalıdır; önce Manifest ile ücretsiz yeni sefere hazırlan.

Ortak para, itibar, motor/kapasite/konfor seviyeleri ve tamamlanan kargo işi sayısı host cihazında sürümlemeli yerel kariyer kaydı olarak tutulur. WebGL'de bu kayıt tarayıcının kalıcı depolamasını kullanır. Sağlık, aktif rota, yolcular ve devam eden olaylar kariyer kaydına girmez; çevrimiçi host migration bunları ayrı snapshot ile taşır.

Rota panosunda **Coastal Ferry** başlangıçta açıktır; **Market Run 2**, **Storm Passage 5** ortak itibar gerektirir. İtibar seçimde harcanmaz. Kilitli rota istemci düğmesi dışında host kurallarında da reddedilir. Başarılı standart sefer ve liman kargo işi birer itibar verir; böylece ilk zor sözleşmeye kısa bir sefer ve isteğe bağlı liman işiyle ulaşılabilir. Market'te üç servisle 2, Storm'da en az 70 gövdeyle 3 itibar kazanılır. Kayıt ve migration zaten itibarı taşıdığından ayrı bir açılım kaydı veya ücret yoktur.

Bu dilimle **118/118 EditMode testi geçti**, Unity exit 0 (`Logs/RouteUnlocks-Final.xml`, `Logs/RouteUnlocks-Final.log`). Kurallar düşük/eşik itibarı, seçimde kaynak harcanmamasını ve kargo işiyle açılmayı; sahne testi gerçek `SelectRouteRpc` üzerinden kilitli/açık seçim ve ağ durumunu doğrular. İlk koşuda eski top sahne testi 118'de bir kez başarısızdı (`Logs/RouteUnlocks.xml`); bekleme yardımcısı duvar saati yerine oyun saatine bağlandı, beş saniyelik ek üst sınır ve kalkış assertion'ı eklendi. Son koşu tüm pakettir; başarısız ilk kanıt silinmedi.

Host migration düzeltmesini içeren WebGL `188ffa1` yayınının Pages işi `39703647843` başarılıdır. Canlı tarayıcıda geometri ve Relay host oluşturma tekrar doğrulandı; bu kontrolde konsol hata listesi boştu (`Logs/WebGL-Migration-Fix-Verified.png`). Bu kısa kontrol önceki Chromium `UnknownError` kaydının kök nedeninin çözüldüğünü veya dört tarayıcılı migration kabulünün tamamlandığını kanıtlamaz. Rota açılımı bu eski WebGL paketinde henüz yoktur; sonraki yayına dahil edilmelidir.

Bu oturumun sonraki çıkış kontrolünde, Esc/imleç geçişi sırasında aynı Chromium hatası **01:10:05 UTC'de yeniden görüldü**. Mesaj Chromium'un pointer-lock hata koduyla eşleşir; üretilmiş Unity/Emscripten `requestPointerLock` çağrısı dönen Promise'in reddini yakalamaz. Bu kanıt Relay migration veya sefer simülasyonunun çöktüğünü göstermemektedir. Yeni `WebCursor.jslib`, yalnız oyunun canvas'ındaki kilit isteğinin senkron/Promise hatalarını uyarı olarak ele alır; genel hata dinleyicisini, tarayıcı prototipini veya başka hataları değiştirmez. WebGL artık asenkron bağlantı tamamlanınca otomatik kilit istemez; oyuncu merkezden tıklar. Klasik/Promise başarı, ret, senkron hata ve yeniden kurulum `node Scripts/Test-WebCursor.cjs` ile sınanır. Tarayıcıdaki gerçek kontrol ayrıca gereklidir. Üst sağdaki bağımsız **Leave session** düğmesi, yoğun HUD metni nedeniyle görünmez hale gelmez.

Pointer-lock korumasının Node testi başarılıdır; Unity Editor regresyon paketi de **118/118**, exit 0 (`Logs/WebCursor-EditMode.xml`). WebGL'ye özel JS bağlantısı, Esc ile bırakma/merkezden yeniden yakalama ve görünür Leave düğmesi sonraki WebGL build/tarayıcı kabulünde ayrıca doğrulanmalıdır.

Sonraki WebGL `1f0705e` build'i Unity exit 0 ile tamamlandı; Pages işi `39705329746` başarılıdır. Canlı solo kontrolde yeni rota eşikleri ve üst sağdaki Leave düğmesi göründü. İki merkez tıklaması/Esc geçişinde tarayıcı kilidi `UnknownError` ve `WrongDocumentError` ile reddetti; bunlar yalnız `[ShipMayham] Pointer lock was not granted...` **uyarısı** olarak yakalandı, hata diyaloğu açılmadı ve error seviyesinde konsol kaydı oluşmadı. Leave menüye döndü (`Logs/WebGL-Cursor-Guard-Verified.png`, `Logs/WebGL-Leave-Verified.png`). Bu, kilit reddinde güvenli davranışı doğrular; bu gömülü tarayıcı ortamında başarılı pointer-lock/bakış kontrolü kanıtlanmadı ve normal masaüstü tarayıcıda manuel kontrol gerekir. Unity'nin kalıcı depolama API'si deprecation ve kullanılmayan FSR shader'ı için ayrı uyarıları vardır; günlük tamamen uyarısız değildir.

## Olay destesi

Host her oturumda bir seed üretir. Deste mevcut istasyon ve stoklara göre uygun kartlardan rota ağırlıklarıyla seçim yapar; aynı kart, o turdaki uygun kartlar bitmeden tekrar gelmez. Market Run servis/VIP kartlarına, Storm Passage teknik kartlara daha fazla ağırlık verir. Yeni tur sınırında da mümkünse art arda aynı kart gelmez. Deste, hedef yolcu, denetlenen yolcu maskesi ve hararetin soğuma süresi host migration snapshot v3 ile devam eder; tüm oyuncular aynı sürümü kullanmalıdır.

| Olay | Çözüm | Süre dolarsa |
|---|---|---|
| Contraband | F/Y ile gizli şüpheli yolcuyu kontrol et; önceden bakılan bilet tekrar kontrol edilebilir | 5 gövde hasarı ve elektrik arızası |
| Galley Rush | İki sipariş servis et | Aktif yolculara 8 memnuniyet kaybı |
| Loose Cargo | Sızıntıyı giderdikten sonra REPAIR noktasında üç sabitleme | 12 gövde hasarı |
| VIP Service | HUD'da belirtilen yolcunun siparişini servis et | VIP'de 20 memnuniyet kaybı |
| Ticket Audit | F/Y ile iki farklı yolcuyu kontrol et | Aktif yolculara 8 memnuniyet kaybı |
| Fuse Overload | POWER noktasındaki gerçek sigortaları sıfırla | 8 gövde hasarı; kesinti onarılana kadar sürer |
| Engine Overheat | Hızı 1 m/s veya altına indirip 8 saniye bekle ya da bu hızda REPAIR noktasında iki vana aç | 15 gövde hasarı ve motor durması |
| Seat Shuffle | HUD'daki yolcuyu G/B ile taşı, özgün koltuğundan en az 1,5 m uzağa güvenle bırak | Yolcuda 20 memnuniyet kaybı |

Her başarı başarılı varışta ödenen 15 para olay bonusu kazandırır. Fırlatmak, Seat Shuffle görevini tamamlamaz. Bilet denetiminde aynı yolcuyu tekrar kontrol etmek ilerleme kazandırmaz.

Sekiz kartlı deste Unity 6000.4.6f1 ile ayrı doğrulama projesinde `-runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests` kullanılarak test edildi: **113/113 geçti, process exit 0**. Rapor `Logs/EventDeckEight-Final.xml`, günlük `Logs/EventDeckEight-Final.log`. Kapsam sekiz kartı gerçek etkileşimlerle çözme, denetim/VIP hedefleri, hararetin iki çözümü ve hız kesintisi, yetersiz istasyonların filtrelenmesi, karışık sınırlı erzak, 256 seed üzerinde rota ağırlıkları ve migration sonrasında sekiz gelecek çekimin eşitliğidir.

Ağ protokolü 3'e sabitlendikten sonra tam paket yine **113/113** geçti (`Logs/EventDeckEight-Protocol.xml`). Aktif olay mesajlarının ücret tahsilatını engellememesi düzeltmesinden sonra olay test sınıfı `-testFilter ShipMayham.Tests.VoyageEventTests` ile tekrar koşuldu: **12/12 geçti**, exit 0 (`Logs/EventFareRegression.xml`). Bu son koşu, denetim bitmeden kontrol edilmiş bir yolcudan ücret alınabildiğini de kapsar.

Son WebGL yayını `93ea92f` için Pages deployment başarılı oldu. Canlı tarayıcıda görünür geometriyle Relay host oturumu oluşturuldu; imleç Esc ile açıldıktan sonra Leave session menüye döndü. Kanıt ekranı `Logs/WebGL-EightEvents-Verified.png`. Bu kontrolde oturum açıldıktan sonra Chromium'un genel `UnknownError: If you see this error we have a bug. Please report this bug to chromium.` bildirimi de görüldü (22 Ekim 2026, 00:31:49 UTC). Kayıt C# stack trace içermiyor; neden henüz belirlenmedi. Bu nedenle temiz tarayıcıda yeniden üretim ve çok istemcili internet/migration kontrolü hâlâ açık doğrulama işleridir; hatasız tarayıcı oturumu veya tam internet kabul testi tamamlandı diye raporlanmaz.

## Sahne ve model değiştirme sözleşmesi

| Kök / alt nesne | Sorumluluk |
|---|---|
| `Ship_Player` | Geminin senkronize konumu/yönü; ölçeğini `(1,1,1)` tut |
| `Ship_Player/Visuals` | Gövde, güverte, korkuluk modelleri; değiştirilebilir |
| `Ship_Player/Collision` | Görselden bağımsız fizik geometrisi |
| `Ship_Player/Stations/Station_*` | İstasyon tipi ve oyun referansları; kökü koru |
| `Station_*/Visuals` | Tezgâh/dümen/tamir/yükseltme modeli; değiştirilebilir |
| `Station_*/InteractionAnchor` | Etkileşim merkezi; istasyon tanımı oturum başında buradan okunur |
| `Ship_Player/Passengers/Passenger_01…08` | Koltuk konumu ve yolcu kimliği |
| `Passenger_*/Visuals`, `SeatVisuals` | NPC ve koltuk modelleri; ayrı ayrı değiştirilebilir |
| `Passenger_*/Visuals/RescueFloat_Visual` | Kurtarma sırasında görünen can simidi temsili; adını/çocuk bağlantısını koru |
| `Ship_Player/Crew/CrewSlot_01…04/Visuals` | Mürettebat modeli; kökteki hareket ve etiket referanslarını koru |
| `CrewSlot_*/CarrySocket` | Taşınan eşyanın görsel bağlama noktası |
| `Station_Cannon/AimPivot/Visuals` | Top namlusu modeli; pivot +Z yönünde ateş eder |
| `Station_Cannon/AimPivot/MuzzleAnchor` | Atış görselinin referansı; kök/pivot konumlarını koru |
| `Station_Power/Visuals/PowerStatus_Visual` | Arıza durumuna göre renklenen gösterge; Renderer referansını koru |
| `Ship_Pirate_01/Visuals`, `ThreatLabel` | Korsan gemisi modeli ve tehdit etiketi |
| `Systems_Presentation/*Shot_Visual` | Kısa süreli atış çizgileri; hasar hesaplamaz |
| `Ship_Player/Harbor_Gangway` | Gemide karaya geçiş işareti; kök ve anchor'ı koru |
| `Environment/Port_*/HarborStations/Harbor_*` | Dönüş, erzak ve gövde koruması noktaları; `Visuals` çocukları değiştirilebilir |
| `Environment/Port_*/Gangway_Visuals` | Yalnızca bağlı olunan limanda görünen geçiş tahtası; oynanış E/A geçişidir |
| `Environment/Port_*/Visuals` | İskele/terminal modelleri; mantıksal yürüme sınırları ayrı tutulur |
| `Systems_Network`, `Systems_Voyage`, `Systems_Presentation` | Bağlantı, oyun durumu ve sunum |

Primitive objeler editörde kaydedilmiş sahne içindedir; Play sırasında tekrar üretilmez. `Visuals` altındaki primitive meshleri gerçek modellerle değiştirebilirsin. Gameplay köklerini, scriptleri, `InteractionAnchor`, etiket ve `CarrySocket` referanslarını koru. Kamera ve etiketler sunum katmanındadır; sonradan ayrı değiştirilebilir.

Birinci şahıs ayarları `Systems_Presentation → PrototypeView` üzerindedir: `EyeHeight`, `MouseSensitivity`, `StickLookSpeed`, `FirstPersonFieldOfView`, `HeldItemOffset`. Yerel oyuncunun `Visuals` alt ağacı gizlenir; `CarrySocket` kameranın sağ altına yerleştirilir, diğer oyuncuların soketleri karaktere bağlı kalır. Eşya rengi ağdaki `Carrying` türünden `MaterialPropertyBlock` ile atanır; ortak yiyecek materyali değiştirilmez. Yeni eşya materyallerinin `_BaseColor` veya `_Color` özelliğini desteklemesi gerekir (mevcut URP materyalleri destekler).

Koordinat düzeni: gemi yerel **+Z baş**, **+X sancak**, güverte üstü **Y=0,5**. Görsel boyutunu alt modelin ölçeğiyle ayarla. İstasyonların etkileşim merkezi ve koltuk köklerinin konumu oturum başında okunur; yerleşimi Play dışında düzenle. Koltuk/istasyon dizilerinin sırası ağ hedef kimliğidir, istemci ve host aynı sahneyi kullanmalıdır.

İlk hareket sistemi gemi yerel düzleminde çalışır: güverte sınırı `VoyageSimulation.DeckBounds`, tezgâh ve koltuk çevresinde küçük dairesel engeller vardır. Collider boyutlarını değiştirmek bu mantıksal sınırları otomatik değiştirmez. Yeni kat, merdiven, farklı gemi biçimi veya fiziksel itiş kakış eklerken hareket sistemi genişletilmelidir.

Zıplama host üzerinde `JumpSpeed = 6 m/s`, `JumpGravity = 18 m/s²` ile hesaplanır: yaklaşık 1 m yükseklik ve 0,67 saniye havada kalma. `CrewState.JumpHeight` ve `VerticalSpeed` ağda paylaşılır; kamera, karakter ve taşınan eşya aynı hareketi izler. Havada yön değiştirilebilir fakat ikinci zıplama ve istasyon etkileşimi yapılamaz. Yere indikten sonra yeniden tuşa basmak gerekir; basılı tutmak otomatik zıplatmaz. Güverte sınırları ve mevcut yatay engeller korunur: bu sürüm tezgâha çıkma, farklı yüzeylere basma veya gemiden atlama sistemi değildir. Yeni sefer ve yeniden bağlanma dikey hareketi sıfırlar.

Yolcu hareketinde `Passenger_*` kökü ilk koltuğun sabit referansıdır; sadece `Visuals` ve etiket taşınır, `SeatVisuals` yerinde kalır. Güncel yolcu konumu host durumundan gelir; bırakılan yolcu yeni konumunda servis alır. Yerel taşıma görünümü kameranın sağındadır; diğer oyuncular ağdaki taşıma konumunu görür. Yolcu taşırken zıplanabilir ve havada fırlatılabilir; normal bırakma, yeni yolcu kaldırma ve bilet kontrolü yerde yapılır.

Liman hareketi ikinci bir yerel düzlemdir: `CrewState.OnShore` açıkken `Position` limana göredir. Başlangıç limanı dünya `(-14,0,0)`, varış limanı `(-14,0,220)`; ayak yüksekliği 0,3, yürünebilir şerit X=0,2…5,2 / Z=-10…10'dur. Kamera/gövde bu düzlemden dünya konumuna çevrilir; mürettebat kökleri aynı isimle gemi hiyerarşisinde kalır. `VoyageHarbor.HarborPoint` ve sınırlar oynanışın kaynağıdır; iskeleyi/tezgâhları taşırken yalnızca mesh değil bu sabitleri ve sahne anchor'larını birlikte güncelle. Mevcut 10 gemi istasyonunun sırası korunur; liman etkileşimi ayrı RPC kullanır.

## Kod yapısı

- `VoyageSimulation`: Sefer kuralları, hareket, siparişler, tamir, ekonomi. MonoBehaviour değildir; bağımsız test edilir.
- `PrototypeState`: Ağda paylaşılan sefer, mürettebat ve yolcu veri tipleri.
- `PrototypeSession`: Netcode host otoritesi. İstemci niyetini RPC ile alır; mesafe, durum, stok, para ve istasyon sahipliğini host doğrular. Gemi ve oyuncuların yerel konumları aynı durum akışından gelir.
- `PrototypeStation`: Sahnede istasyon tipi ve görsel/etkileşim referansları.
- `PrototypeView`: Input System, kamera, görsel interpolasyon, hedef seçimi ve etiketler.
- `PrototypeHud`: Host/join, bağlantı sınırı, sefer göstergeleri ve sonuç ekranı.
- `PrototypeSceneBuilder`: İlk sahneyi, materyalleri ve bağımsız Windows build'ini üretir.
- `PrototypeSmokeCheck`: Yalnızca editor/development build'de, özel komut satırı bayrağıyla çalışan ağ doğrulaması; normal oyunda etkinleşmez.
- `VoyageCombat`: `VoyageSimulation` sınıfının top, korsan ve elektrik kuralları; tüm sonuçlar host otoritesindedir.
- `EncounterState`: Ağda paylaşılan tehdit, top, mühimmat, arıza ve son atış verileri. Geç katılan istemci mevcut durumu alır.
- `PrototypeCombatView`: İkinci dilimin sahne referansları, namlu dönüşü, korsan modeli, panel rengi ve atış çizgileri.
- `PrototypePhaseTwoBuilder`: Mevcut sahneye eklemeli/idempotent istasyon güncellemesi; gemiyi veya önceki istasyonları yeniden üretmez.
- `VoyagePassengers`: Host üzerinde bilet, tek taşıyıcı sahipliği, bırakma/fırlatma ve kurtarma kuralları.
- `PassengerPresentation`: Bilet durumu ve bekleme/teşekkür/şikâyet/kurtarma etiketleri.
- `PrototypePhaseThreeBuilder`: Mevcut yolcu görsellerine eklemeli/idempotent kurtarma görseli güncellemesi.
- `VoyageHarbor`: Karada hareket, geçiş, rota sözleşmesi, kalkış engeli, erzak satın alma ve hasar emen bakım kuralları.
- `PrototypeHarbor`: İki limanın sahne referansları, işaretler, bağlama göre görünen geçiş tahtaları ve etiketler.
- `PrototypePhaseFourBuilder`: Eski sahne kimliklerini koruyarak liman hazırlık noktaları ekler.
- `WorldLabelPresentation`: Birinci şahıs için dünya yazılarını mesafeye göre ölçekler; yakın etiketlerin ekranı kaplamasını önler.
- `VoyageEvents`: Şüpheli yolcu, servis yoğunluğu ve savrulan yük görevlerini zamanlayan deterministik olay destesi; sonuçlar host otoritesindedir ve host migration ile taşınır.
- `CareerProgressStore`: Host cihazında yalnız uzun vadeli ortak ilerlemeyi sürümlemeli ve bozuk veri toleranslı olarak saklar.
- `SoloTransport`: NGO'nun yerel host RPC akışını soket açmadan çalıştırır; tarayıcıda gerçek çevrimdışı solo oyun sağlar.

Mevcut Blocks karakter prefabları üçüncü şahıs animasyon/kamera/olay bağlantıları taşıdığı için bu küçük bloklama sahnesinin hareketi ayrı tutuldu. Mevcut projeyle aynı Netcode ve Input System paketlerini kullanır. Bu kontrolcü nihai karakter veya fizik sistemi değildir.

## İlk denge değerleri

| Sistem | Başlangıç değeri |
|---|---|
| Yolcu sayısı | 4; kapasite yükseltmesi başına +2, en fazla 8 |
| Bilet | Ödemiş ve başarılı varışta gemide bulunan yolcu başına 40 |
| Ödemeyen yolcu | Her sefer 1; kimliği sefer numarasıyla değişir, ilk sefer yolcu 1 |
| Fırlatma / denize düşme | -5 / ilave -20 memnuniyet; otomatik kurtarma 8 saniye |
| Kurtarma masrafı | Her denize düşüş 15; sefer sonu masraflara eklenir |
| Servis | Doğru sipariş +8 gelir, 2 maliyet; varışta hesaplanır |
| Bahşiş | Toplam yolcu memnuniyetinin %10'u |
| Hasar maliyeti | Kaybedilen gövde sağlığının yarısı, yukarı yuvarlanır |
| Stok | İlk sefer 16+16; yeni seferde her tür en az 16'ya ücretsiz tamamlanır, fazlası korunur |
| Liman erzağı | 12 para karşılığı +8, tür başına depoda+elde en fazla 32 |
| Gövde koruması | 30 para karşılığı toplam 20 hasar emme; kullanılmayan miktar sonraki sefere kalır |
| Market Run | En az 3 servisle başarılı varış: +35 para, +2 itibar; koşul kaçarsa +5 para, +1 itibar |
| Storm Passage | İlk olay 12 sn, ilk sızıntı 16 sn; 70+ gövdeyle başarılı varış: +50 para, +3 itibar |
| Liman kargo relay | Liman başına 3 teslimat; +24 ortak para ve +1 itibar |
| Motor | 4 m/s; yükseltme başına +1,5 m/s; 100, sonra 200 |
| Kapasite | Yükseltme ücretleri 150, sonra 300; sonraki yolcu alımında uygulanır |
| Konfor | 120; bekleyen siparişin memnuniyet kaybını azaltır |
| Sızıntı | 25. saniyede başlar; saniyede 0,65 hasar; tamirden 45 saniye sonra tekrarlar |

Kasa ve yükseltmeler aynı host oturumu boyunca korunur. Disk kaydı henüz yoktur. Batma sonrası kasa sıfırın altına inmez, yeni sefere ücretsiz hazırlanılır; prototip borç yüzünden kilitlenmez.

## Kontroller ve sonraki adım

EditMode testleri: Unity Test Runner → `ShipMayham.Tests`. Testler yanlış/çift servis, stok, mesafe, dümen kilidi, bağlantı ayrılması, sızıntı, yanaşma, tek ödeme, yükseltmeler ve yeni seferi kapsar.

19 Ekim 2026 doğrulaması: ayrı Unity 6000.4.6f1 projesinde **17/17 EditMode testi geçti** ve Windows development build üretildi. Aynı bilgisayarda bir host + üç istemciyle otomatik ağ testi; uzaktaki oyuncuların hareketini, dört mürettebatın ortak durumunu, yolcu kabulünü, bir sipariş teslimini, gerçek gaz/fren hareketiyle varışı ve tüm istemcilerde **190** ortak kasa sonucunu doğruladı. Bu bir LAN/uzak bağlantı gecikme testi veya dört insanla eğlence testi değildir.

İlk dilimin primitive yerleşimi, renkleri ve etiketleri ayrı sahne render'ıyla kontrol edildi. Gerçek oyuncularla kamera, HUD, gamepad ve etkileşim hissi için manuel Play testi hâlâ gereklidir. Ana Editor açıkken ayrı doğrulama projesi kullanıldığından son doğrulanmış Windows development çıktısı `Builds/ShipPrototypeValidation/Builds/ShipPrototype/ShipPrototype.exe` altındadır. Ana projeden menü build'i `Builds/ShipPrototype/ShipPrototype.exe` üretir; bu eski çıktı kendiliğinden güncellenmez. Çıktılar Git'e dahil edilmez; sürüme özgü doğrulamalar aşağıdadır.

Tekrarlamak için development build'in bir kopyasını `-batchmode -nographics --ship-smoke-host`, diğer üç kopyayı `-batchmode -nographics --ship-smoke-client` ile çalıştır. Her sürece ayrı `-logFile` ver. Başarıda `SHIP_SMOKE_PASS` yazar ve süreç kapanır; 130 saniye zaman aşımında hata verir. Yerel raporlar `Logs/ShipPrototype-*.log` ve `Logs/ShipPrototype-EditMode.xml` altındadır (Git'e dahil edilmez).

### Editör host başlangıcı düzeltmesi — 19 Ekim 2026

NGO 2.11.1, `CreatePlayerObject = false` olsa bile bağlantı onayında `PlayerPrefab?.GetComponent<NetworkObject>()` çalıştırır. Unity Editör'deki boş/ömrü bitmiş nesne referansı C# açısından gerçek `null` olmayabilir. Bu nedenle sahnede `PlayerPrefab: {fileID: 0}` bırakmak tek başına yeterli değildi. `PrototypeHud.Connect`, host veya istemci başlamadan hemen önce referansı gerçek `null` değerine atar ve otomatik oyuncu prefabı üretimini kapatır. Mürettebat sahnedeki slotları kullanmaya devam eder; Inspector'dan oyuncu prefabı atamak gerekmez.

`HostStartupTests` EditMode testinden Play Mode'a girip gerçek sahnenin `Connect` yolunu sınar: hem kayıtlı boş referans hem de yok edilmiş prefab referansı. Düzeltme öncesi **2/2 test aynı `HandleConnectionApproval:975` hatasıyla başarısız oldu**; düzeltme sonrası tüm paket **19/19 geçti**, test süreci `0 (Ok)` ile kapandı. Bildirilen `Trying to destroy object` ve `Directly calling UnityTransport.Shutdown` uyarıları düzeltilmiş test günlüğünde oluşmadı.

Unity CLI doğrulama komutu (Unity 6000.4.6f1, açık projeden ayrı doğrulama kopyasında):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-HostRegression-After.xml' -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-HostRegression-After.log'
```

Test runner'ın Play Mode geçişlerini tamamlaması için `-quit` verilmez. Önce/sonra raporları `Logs/ShipPrototype-HostRegression-Before.xml` ve `Logs/ShipPrototype-HostRegression-After.xml` dosyalarıdır. Bu düzeltme için sahnenin yeniden üretilmesi gerekmez; Play modundan çıkıp script derlemesinden sonra yeniden başlatmak yeterlidir.

### Birinci şahıs ve servis rengi — 19 Ekim 2026

Unity **6000.4.6f1** ile ayrı doğrulama kopyasında **26/26 test geçti**, test süreci `0 (Ok)` ile kapandı. `FirstPersonViewTests` dört bakış yönünde hareket dönüşümünü, analog hız/çapraz hareket sınırını, gerçek sahnede host başlatarak göz hizası ve dönen gemiye kamera takibini, kendi gövdesinin gizlenmesini, eşya soketinin kamera takibini ve öndeki hedef seçimini kapsar. Drink alma/iade/Food alma RPC akışıyla ürün rengi kontrol edilir; başka bir mürettebat slotunun görünümü ve ortak materyalin değişmemesi de sınanır. Bu testte ikinci slot simülasyon verisidir, ayrı ağ istemcisi değildir.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-FirstPerson-Final.xml' -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-FirstPerson-Final.log'
```

Doğrulama headless çalıştı: gerçek fare/gamepad hissi, imleç kilidi ve görsel kadraj manuel Play kontrolü gerektirir. Mevcut prototip sahnesini yeniden üretmek gerekmez; Play'den çıkıp scriptlerin derlenmesini bekle ve tekrar Host başlat.

### İkinci dilimin sınırları

Korsan ve elektrik olayları şimdilik her seferde aynı zamanlamayı kullanır; rastgele olay destesi değildir. Altı saniyelik yaklaşma uyarısı hazırlık süresidir; korsan ancak saldırı başladığında hasar alır. Top atışı fiziksel uçan mermi yerine host üzerinde menzil/açı ile anlık isabet kontrolüdür; kısa atış çizgisi görsel geri bildirim sağlar. Yeniden doldurma elde taşınan gülle değil tuşla başlatılan süredir; topu bırakmak veya bağlantının kopması devam eden doldurmayı iptal eder. Hasar küresi 3,5 m, top menzili 90 m, korsan ateş menzili 70 m'dir. Topun kurala bağlı namlu merkezi güverte yerel Y=1,8 ve uç ofseti 1,5 m'dir; model değişiminde bu referansları koru.

Sahnede ilk sekiz istasyonun sırası korunur; Cannon ve Power sona eklenir. Yolcu hedef kimlikleri artık `Stations.Length + passengerIndex` üzerinden hesaplanır. Bütün oyuncular aynı güncel sahneyi ve build'i kullanmalıdır. Güncellenmiş sahne repodadır; normal kullanımda yeniden üretme gerekmez. Eski bir prototip kopyasını taşırken **Ship Mayham → Upgrade Prototype (Phase Two)** eklemeli güncellemeyi çalıştırır; tekrar çalıştırmak yeni kopyalar üretmez.

### İkinci dilim doğrulaması — 19 Ekim 2026

Unity **6000.4.6f1** ile **42/42 test geçti** ve runner `0 (Ok)` ile kapandı. Önceki 26 test korundu. Yeni testler top sahipliği/mesafe kontrolü, bağlantı kopması, hareket kilidi, mühimmat, doldurma süresi ve tekrar komutlar, geçersiz nişan, menzil/ıskalama, üç isabet, tek ödül/başarısızlık, arıza etkisi, üç sigorta ve yeni sefer sıfırlamasını kapsar. Gerçek kayıtlı sahnede Play Mode testi RPC akışını, yeni referansları, namlu yönünü, atış çizgisi başlangıcını, panel rengini ve korsan görünümünü doğrular. Sahne güncellemesinde önceki nesne kimliklerinin tamamı korundu; yerel önceki kopya `Logs/ShipPrototype-PrePhaseTwo.unity` altındadır.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseTwo.xml' -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseTwo-Tests.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -quit -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -executeMethod ShipMayham.Editor.PrototypeSceneBuilder.Build --prototype-validation -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseTwo-Build.log'
```

Windows development build **Success / exit 0**. Ardından bir host + üç ayrı istemci ile otomatik sefer testi geçti: uzaktaki topçu nişan/ateş/doldurma RPC'leriyle korsanı püskürttü, başka istemci elektrik panelini onardı, host yolcu/servis/gaz/fren/yanaşma akışını tamamladı. **Dördünde de kasa 240 ve korsan ödülü 50**, `SHIP_SMOKE_PASS` ve süreç çıkış kodu **0** görüldü. Test günlüğünde exception veya hata/uyarı bulunmadı. Raporlar `Logs/ShipPrototype-PhaseTwo-{host,client1,client2,client3}.log`.

Bu test localhost ve headless çalıştı; internet gecikmesi, dört insanla rol dengesi, gerçek fare/gamepad hissi ve görsel kadraj için manuel oyuncu testi hâlâ gereklidir. Başarılı build normal `Builds/ShipPrototype/` klasörüne kopyalandı; kaynak DLL hash'i doğrulandı. Play'den çık, değişen sahneyi yeniden aç ve Host başlat; yeni nesneler runtime'da üretilmez.

İlk oyuncu testinde dört kişinin aynı anda gemide dolaşmasını, dümen/top devrini, servis kesintisinin anlaşılmasını ve korsan sırasında iş paylaşımını değerlendirelim. Bu sürüm, GDD'deki bütün MVP özelliklerinin tamamlandığı anlamına gelmez.

### Üçüncü dilim: bilet ve yolcu komedisi

Yolcuların bilet durumu kontrol edilene kadar gizlidir. İlk F/Y yalnızca kontrol eder; ödeme yapmayan yolcuda ikinci F/Y tahsil eder. Diğer yolcular önceden ödemiştir; kontrol etmemek onların bilet gelirini engellemez. Ödemeyen yolcu göz ardı edilirse 40 bilet geliri kaybolur. Gelir anında cüzdana değil, başarılı varış hesabına yazılır. HUD kontrol/tahsilat/kurtarma sayılarını ve sonuçta ödenmemiş, kurtarmada kalan yolcu ile kurtarma masrafını gösterir.

Aynı yolcuyu yalnızca bir oyuncu taşıyabilir; taşıyanın bağlantısı koparsa yolcu ücretsiz olarak koltuğuna döner. Ürün ve yolcu aynı anda taşınamaz, dümen/top başında yolcu etkileşimi yapılamaz. Güverteye bırakma uygun boş nokta arar; bulunamazsa ilk koltuğa döner. Fırlatma güvertede sonuçlanırsa kurtarma ücreti yoktur. Denize düşen yolcu can simidiyle 8 saniye bekler, sonra koltuğuna döner; her düşüş bir kez 15 masraf ve ilave 20 memnuniyet kaybıdır. Fırlatmanın kendisi ayrıca 5 memnuniyet kaybettirir. Kurtarmada sipariş bekleme sayacı durur. Varış anında hâlâ kurtarmada olan yolcu bilet/bahşiş üretmez; sonradan dönmesi tamamlanmış hesaba para eklemez.

Bu kontrollü komedi prototipidir: gerçek ragdoll/çarpışmalı itiş, yüzen dünya fiziği, yolcu yürüme AI'ı, sahte bilet/sızmacı ve rastgele olay destesi yoktur. Atış yayı ve denizde bekleme gemi yerel koordinatındadır; gemi ilerlerken yolcu onunla birlikte hareket eder. Oyuncular hâlâ güverte sınırları içinde kalır. Kara hub'ı bu dilime dahil değildir.

Güncellenmiş sahne repodadır; Play'den çıkıp `ShipPrototype` sahnesini yeniden aç. Eski bir prototip kopyasına **Ship Mayham → Upgrade Prototype (Phase Three)** ile kurtarma görselleri eklenebilir; mevcut kökler yeniden üretilmez. Önceki sahne yerel `Logs/ShipPrototype-PrePhaseThree.unity` dosyasında saklanır. Host ve istemciler genişleyen yolcu/sefer ağ şeması nedeniyle aynı sürümü kullanmalıdır.

### Zıplama doğrulaması — 20 Ekim 2026

Unity **6000.4.6f1** ile **50/50 test geçti**, test süreci `0 (Ok)` ile kapandı. Yedi yeni kural testi zıplama yayı/iniş/tekrar komut, yatay hareket ve güverte sınırı, dümen kısıtı, havada etkileşim, eldeki ürün, yeniden bağlanma/yeni sefer ve ağ durumu karşılaştırmasını kapsar. Top istasyonu testine zıplama kilidi eklendi. Yeni sahne testi gerçek `JumpRpc` üzerinden birinci şahıs kameranın, taşınan ürünün ve başka bir slotun yükselip dönen/hareket eden geminin güvertesine inmesini doğrular; bu sahne testinin ikinci slotu simülasyondur.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-Jump.xml' -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-Jump-Tests.log'
```

Windows development build de `Success / exit 0` ile tamamlandı (`Logs/ShipPrototype-Jump-Build.log`). Build komutu ikinci dilimdekiyle aynıdır; `-logFile` bu dosyaya yönlendirilmiştir. Bir host + üç gerçek istemcili smoke testte her istemci kendi zıplama RPC'sinin yükselme/iniş verisini aldı, host dört slotun da yükseldiğini gördü. Ardından korsan, elektrik tamiri ve yanaşma akışı tamamlandı: dört süreçte `SHIP_SMOKE_PASS`, kasa **240**, ödül **50** ve çıkış kodu **0**. Raporlar `Logs/ShipPrototype-Jump-{host,client1,client2,client3}.log`; güncel build `Builds/ShipPrototype/ShipPrototype.exe` konumuna kopyalandı ve runtime DLL hash'i doğrulandı. Testler headless/localhost çalıştı; gerçek fare/gamepad hissi ve internet gecikmesi manuel test gerektirir.

Sahne değişikliği veya yeniden üretim gerekmez. Scriptlerin derlenmesinden sonra Play'i yeniden başlat. `CrewState` ağ şeması genişlediğinden host ve istemciler aynı güncel sürümü kullanmalıdır.

### Üçüncü dilim doğrulaması — 20 Ekim 2026

`unity-cli` doğrulaması, ana çalışma sahnesine dokunmadan ayrı doğrulama kopyasında Unity **6000.4.6f1** ile çalıştırıldı. **66/66 test geçti**, runner çıkışı **0 (Ok)**. Önceki 50 teste 15 yolcu kural testi ve gerçek sahne/RPC/sunum testi eklendi: kontrol ve tek tahsilat, ödenmeyen biletin gelirden çıkarılması, geçersiz/uzak/havadaki komutlar, tek taşıyıcı sahipliği, bırakılan konumda servis, yavaş yürüme/zıplama, güverteye iniş, süreli kurtarma/tek masraf, kurtarma sırasında varış, bağlantı kopması ve yeni sefer sıfırlaması. Sahne testinde koltuk köklerinin korunması, yerel taşıma kadrajının transformu, etiketler ve can simidi görünürlüğü de sınanır.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseThree.xml' -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseThree-Tests.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -quit -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -executeMethod ShipMayham.Editor.PrototypeSceneBuilder.Build --prototype-validation -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseThree-Build.log'
```

Windows development build **Success / exit 0**. Bir host + üç ayrı istemciyle ağ testi de geçti: uzaktaki yolcu sorumlusu kontrol/tahsilat/taşıma/bırakma/fırlatma/kurtarma akışını tamamlarken diğer istemciler top ve elektrik görevlerini yaptı. Dört süreçte `SHIP_SMOKE_PASS` ve çıkış kodu **0**, ortak kasa **223**, korsan ödülü **50** görüldü. Test ayrıca her tarafta taşıma/kurtarma durumunu, 1 kontrol/1 tahsilat, **160 bilet geliri** ve **15 kurtarma masrafını** doğrular. Önceki zıplama, servis, gerçek gaz/fren/yanaşma kontrolleri korunur. Ağ test günlüklerinde exception/hata/uyarı görülmedi. Raporlar `Logs/ShipPrototype-PhaseThree-{host,client1,client2,client3}.log`.

Sahne güncellemesi hiçbir eski nesne kimliğini silmedi; sekiz yolcuya toplam 32 GameObject/component kaydı eklendi. Doğrulanmış build `Builds/ShipPrototype/` klasörüne kopyalandı; runtime DLL hash'i kaynakla eşleşti. Gizli pencereyle alınan iki önizleme siyah görüntü üretti; bu nedenle yeni HUD/kadraj için görsel onay verilmedi (loglar: `ShipPrototype-PhaseThree-Preview*.log`). Manuel oyuncu testi hâlâ gereklidir: HUD yerleşimi, taşıma sırasında görüş, yeni tuşların anlaşılması, gamepad, gecikmeli bağlantılar ve dört kişiyle eğlence/dengenin doğruluğunu otomatik testler kanıtlamaz.

### Dördüncü dilim ve polish — 20 Ekim 2026

Bu dilim küçük liman hazırlığıdır; yeni rota, kayıt, sızmacı veya olay destesi eklemez. Karadaki alan terminalin önündeki dar şerittir. Dükkânda stok satın alma anlıktır; fiziksel kasa taşıma mini oyunu değildir. Gövde bakımı tamir satın alımı değil, gelecek hasara karşı 20 puanlık korumadır; hasarı emer fakat korsanların elektrik kesintisini engellemez. Ücretsiz temel stok ve yeni seferde 100 gövde, parasız ekibin kilitlenmesini önler. Bu kolay başlangıç ekonomisi oyuncu testinden sonra yeniden dengelenebilir.

Polish: Yakındaki yolcular iki satırlık özet gösterir, ayrıntılı tepki odaklanınca açılır; kurtarma uyarısı hariç 7 metreden uzaktaki etiketler gizlenir. Dünya yazılarında `characterSize=0,02` ve mesafeye bağlı sınırlı ölçek kullanılır; gemi istasyonu yazıları 10 metreden sonra ve oyuncu karadayken gizlenir. Alt ipucu `check ticket` / `collect 40 coins` / `ticket valid` durumlarını ayırır; sipariş yokken `serve None` yazmaz. Fırlatma ve denize düşme cezaları ayrı açıklanır. Bildirimler son iki saniyede sönerek 6 saniyede kaybolur. F1 yardım katmanını açar, limanda sağ panel hazırlık/ekip bilgisine dönüşür. `PrototypeView.HeldPassengerOffset` model değişiminden sonra taşıma kadrajını ayarlamak için Inspector'da açıktır.

Sahne eklemeli güncellendi: eski nesne kimliklerinden hiçbiri silinmedi, 151 yeni GameObject/component kaydı eklendi. Yedek `Logs/ShipPrototype-PrePhaseFour.unity`. Güncel sahneyi yeniden açmak yeterlidir; eski bir kopya için **Ship Mayham → Upgrade Prototype (Phase Four)** idempotent güncellemesini kullan. Host ve istemciler `OnShore` / `HullShield` ağ alanları nedeniyle aynı sürümde olmalıdır.

`unity-cli` doğrulamasında Unity **6000.4.6f1**, ayrı `Builds/ShipPrototypeValidation` kopyası kullanıldı. **84/84 test geçti, runner exit 0 (Ok)**. Önceki 66 teste 17 liman kural testi ve gerçek sahnede Play/RPC/kamera testi eklendi. Kurallar geçişte mesafe/eller/zıplama/sahiplik, karadan gemi komutu engeli, iskele sınırı, kalkış/yeni sefer kilidi ve bağlantı ayrılması, yetersiz para, depo kapasitesinde eldeki ürün, tekrarlı/eşzamanlı satın alma, sızıntı/korsan hasarına koruma ve seferler arası aktarımı kapsar. Sahne testi limanda dünya konumu, birinci şahıs kamera, hedef seçimi, tezgâh alışverişi, bildirim ve geri binişi doğrular.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -runTests -testPlatform EditMode -assemblyNames ShipMayham.Tests -testResults 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseFour.xml' -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseFour-Tests.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe' -batchmode -quit -projectPath 'D:/UnityRepos/Ship Mayham/Builds/ShipPrototypeValidation' -executeMethod ShipMayham.Editor.PrototypeSceneBuilder.Build --prototype-validation -logFile 'D:/UnityRepos/Ship Mayham/Logs/ShipPrototype-PhaseFour-Build.log'
```

Windows development build `Success / exit 0`. Opt-in smoke senaryosu artık varıştan sonra üçüncü istemciyi karaya gönderir, iki erzak paketi ve gövde koruması aldırır, gemiye döndürür; host yeni sefere hazırlanır. Tüm istemciler 23 yiyecek / 24 içecek / 20 koruma ve alışverişler için kasadan toplam 54 düşüldüğünü doğrulamak zorundadır. Normal oyun başlangıcında otomasyon çalışmaz.

İlk dört süreçli koşu bu akışı başarıyla tamamladı: hepsinde kasa **169**, yiyecek **23**, içecek **24**, koruma **20**, `SHIP_SMOKE_PASS` ve exit **0**; hata/exception/uyarı görülmedi (`Logs/ShipPrototype-PhaseFour-{host,client1,client2,client3}.log`). Off-screen kamera incelemesi önceki büyük/üst üste binen dünya yazılarını görünür kıldı; boyut ve görünürlük düzeltmesinden sonra **84/84 test tekrar geçti**. Son test raporu `Logs/ShipPrototype-PhaseFour-Final.xml`, runner logu `Logs/ShipPrototype-PhaseFour-Final-Tests.log`; komut aynı, yalnızca rapor/log adları bu son dosyalara yönlendirilir. Son build günlüğü `Logs/ShipPrototype-PhaseFour-Final-Build.log`.

Son build `Success / exit 0`. Güverte ve liman birinci şahıs kamera render'ları görsel olarak incelendi; büyük yazı taşmaları giderildi. `--ship-preview` ve ek `--ship-preview-harbor` bayrakları geliştirme build'inde `ShipPrototype-Deck-Camera.png` / `ShipPrototype-Harbor-Camera.png` üretir ve kapanır; bu çıktılar HUD'ı içermez. Gizli pencerenin normal `ScreenCapture` çıktısı hâlâ siyahtır. Bu nedenle HUD'ın tam ekran okunurluğu, gerçek fare/gamepad hissi ve dört insanla tempo/dengenin doğruluğu manuel test gerektirir. Kamera önizleme logları `Logs/ShipPrototype-PhaseFour-Final-Preview-{Deck,Harbor}.log`.

Son görsel düzeltmeleri içeren build'de dört süreçli smoke tekrarlandı ve yine **4/4 exit 0**, `SHIP_SMOKE_PASS`, **169 para / 23 yiyecek / 24 içecek / 20 koruma** doğrulandı. Hata/exception/uyarı görülmedi. Son günlükler `Logs/ShipPrototype-PhaseFour-Final-{host,client1,client2,client3}.log`. Doğrulanmış çıktı `Builds/ShipPrototype/ShipPrototype.exe` konumuna aktarıldı; runtime DLL hash'i kaynakla eşleşti. Play'den çıkıp güncel `ShipPrototype` sahnesini yeniden açarak dene.
