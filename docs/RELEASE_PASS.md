# Yayın öncesi geçiş (25 Ekim 2026 gecesi)

Bu belge, prototipi oynanabilir ve yayın içeriği üretebilir hâle getiren geçişi özetler: ne eklendi, hangi sayılar ilk deneme değeri, neyin elle kabul edilmesi gerekir. Kod ayrıntısı [ARCHITECTURE.md](ARCHITECTURE.md) içindedir.

## Düzeltilen hatalar

- **Işınlanmalar kalktı.** Yolcular iskeleden sıraya girip kapıdan geçerek koltuklarına yürür (`Boarding`) ve sefer bitince aynı yoldan iner (`Leaving`); kalkış herkes oturana, yeni sefer herkes inene kadar bekler. Atılan, bırakılan ve kurtarılan yolcular yatar, sersemler ve koltuklarına yürür. Gemi–kara geçişi düğme değil, kapıdan geçip iskele tahtası boyunca yürümektir (bkz. aşağıda "Hareket ve dümen geçişi").
- **Kapalı koltuklar.** Kapasite yükseltmesiyle açılmamış koltuklar ne çarpışır, ne sahiplenilir (bırakılan yolcu yalnız açık koltuklardan en yakın boşa oturur).
- **Su içme.** Eski 15 sn'lik klip yolcuyu kaldırıp 2,8 m yürütüyordu; yerini yerinde yudumlama klibi aldı.

## Yeni mekanikler

| Mekanik | Özet | Ayar sayıları |
| --- | --- | --- |
| Gangway kapısı | Eller boşken kapıdan geç, tahta boyunca yürü; E ile geçiş yok | şerit ±0,6 m, devir çizgileri x=-6,9 / -6,5 |
| Yüzme | Halat merdivenle suya atla (limanda), yüz, batık para topla, merdivenle çık | hız %60, 30 sn limit (denizde), 5 coin ceza |
| Denize savrulma | Rogue wave, dümen/topta olmayan ve uzak korkuluğa 2,2 m içindeki mürettebatı denize atar | 2,2 m |
| Yüzen/yüzemeyen yolcu | %70 yüzer, kendi merdivenine yüzer (3–8 sn); diğerleri simitle çırpınır, 8 sn sonra çekilir | |
| Kilit açma | Gömülü sandık (2/liman) | 25–60 coin, temiz açılış +1 itibar, 3 kurtarma |
| Kazı | 4/liman | birkaç coin, %12 harita (+1 itibar) |
| Olta | İskele kenarı, 3 nokta, sınırsız | küçük balık +2, büyük +5 yiyecek, çizme +1 coin |
| Çekiç | Sızıntıya tahta çak (kit/vana yoksa) | 3 çivi, 3 ıska; başarısızlık 1 adım verir |
| Batık paralar | 8/liman, yüzerek | 5 coin; sekizinci +25 ve itibar |
| Top yükseltmeleri | Purser'dan Gunnery (2 seviye) ve Extra barrels (2 seviye) | dolum 2,5→1,5 sn, raf 3→5, vuruş 34→50; ek namlu yarı hasar, ±3,5°; fiyat 120/240 ve 180/360 |
| Gerçek adalar | Kasaba bir adanın doğusundadır; çevresinde plaj, tepe, palmiye/kaya/mercan/kemik süsleri; kıyıdan yürüyerek suya girilir, sığa gelince çıkılır | ada yarıçapı 56–70 m, wading ≤ 0,75 m, yüzme alanı 160 m |
| Ada etkinlikleri | Her adanın kendi kazı/sandık/olta noktaları | Reef 5 olta, Cove 4 sandık, Skull 4 kazı |
| Kayıt / devam | Menüden solo veya online devam | yalnız limanda yazılır |

Yaptığını gösterme: oynayan oyuncu olta/kazma/kilit/çekiç pozunda, elinde aracıyla görünür.

## Hareket ve dümen geçişi (protokol 27)

- **Yolcular takılmadan yürür.** İstemci yolcuları 25 Hz ağ güncellemelerini doğrudan kovalayan lerp yerine mürettebatla aynı `MotionSmoother` ile, gemi düzleminde çizer; yüz yönü yumuşatılmış hıza bakar. Simülasyonda yürüyüş hızlanarak başlar (14 m/s²) ve engelden kaçarken zaten geçtiği taraftan devam eder (zikzak yok). Zaman aşımı artık koltuğa ışınlamaz: takılan yolcu engelleri yok sayarak yürür; ancak 6 sn (`StuckGraceSeconds`) daha sürerse son çare olarak yerine konur. Gezinti yolu kapanırsa yolcu bekler, sonra yürüyerek döner.
- **Gangway yürünür.** Kapı açıkken (liman açık, eller boş, istasyonda değil) gemi korkuluğundan rıhtıma uzanan 1,2 m genişliğinde bir şerit yürünebilir. Tahtanın ortasında konum diğer alanın koordinatlarına birebir çevrilir (`GangwayPassage`); arada 0,4 m boşluk tahtada duranın alan değiştirip durmasını engeller. Sunucu, istemci tahmini ve uzak oyuncu interpolasyonu aynı kuralı kullanır; tampon temizlenmez, kaydırılır. Tahtadayken kalkış yapılamaz. `HarborCommand(GoAshore/BoardShip)` artık kimseyi geçirmez.
- **Gerçekçi dümen.** `ShipSteering` Nomoto birinci derece modeliyle çalışır (T·r' + r = K·δ, T=1,8 sn, K=16°/s): pruva dönmeye geç başlar, dümen ortalanınca bir süre dönmeye devam eder. W/S motor telgrafı gibi kolu hareket ettirir ve kol bırakılınca yerinde kalır (tam tornistan -0,6). Doğrusal + karesel su direnci: gemi yavaş hızlanır, uzun süre süzülür, durmak için tornistan gerekir. Dümen ancak su akışıyla etkilidir (kendi hız + ileri yönde pervane akıntısı); geri giderken dümen ters çalışır. Dönüşte gövde dışa kayar ve hız kaybeder. HUD motor emrini ve dümen açısını gösterir. Ayar sabitleri `ShipSteering` içindedir.
- **Yön düzeltmeleri.** Güverte bankı modeli -Z'ye bakıyordu, yolcular arkalığa dönük oturuyordu; bank sarmalayıcısı 180° döndürüldü. Kasaba sakinlerinin kökleri eski statik modele göre yazılmıştı; rigged gövdeleri 180° çevrildi, artık tezgâhlarına bakıyorlar. Uygulama: `Ship Mayham/Upgrade Prototype (Release Props)` (`PrototypeFacingBuilder`).
- **Merdivenden çıkış.** Suda merdiven artık bakış açısına göre değil, uzanma mesafesindeki en yakın merdiven olarak seçilir; merdivenin dibindeyken E/A çalışır. Gemi merdivenlerinin üst noktaları DRINK, AMMO ve koltuk yükseltmesi tezgâhlarının çarpışma alanı içindeydi; çıkan oyuncu kıpırdayamıyordu. Artık çıkışta tezgâhlardan en az 0,3 m uzak en yakın boş noktaya konur (`FreeStandingSpot`), ayrıca bir engelin içinde kalan biri her yöne yürüyerek çıkabilir (`CrewLocomotion`).

Kaynaklar: Nomoto modeli için [NTNU IFAC 2005](https://skoge.folk.ntnu.no/prost/proceedings/ifac2005/Fullpapers/02331.pdf); oyun tarafında hıza bağlı dümen etkisi ve yanal sürtünme için [Uppsala oyun tasarımı yazısı](https://babel.speldesign.uu.se/?p=1979238).

## Döngü: ada zinciri (26 Ekim)

Sefer artık başa dönmez: gemi vardığı adada kalır ve sonraki ada liman memurundan seçilir. Altı ada, her birinin görevi ve kilidi, bayrak gemisi finali ve yeni tur için [ISLANDS_PLAN.md](ISLANDS_PLAN.md) dilim C'ye bakın. İlk oyun: Brass Quay → North Haven (iş bitir) → Coral Cay (6 inci, dalarak) veya Smugglers' Cove (2 sandık) → Storm Reef (fırtınalı sefer, ≥%50 gövde) → Skull Atoll (bayrak gemisi).

Dalma, geniş yüzme alanı (90 m), derin coin'ler ve top yükseltmeleri de bu geçişin parçasıdır (bkz. yukarıdaki tablo).

## Elle kabul edilmesi gerekenler

Otomatik testler (694 test) kuralları, ağ durumunu, kaydı ve sahne bağlantılarını kapsar; şunlar insanlı denemeyle onaylanmalıdır:

0. Baştan sona bir oyun: altı adayı gezip bayrak gemisini batırmak kaç dakika sürüyor, hangi ada sıkıcı, kilit şartları (token/itibar) dengeli mi, bayrak gemisi çok kolay/zor mu.
1. Dört kişiyle yüzme, merdivenden çıkış ve rogue wave ile denize savrulma hissi; 30 sn limitin ve 5 coin cezasının doğru dengede olup olmadığı.
2. Her mini oyunun zorluğu ve süresi (özellikle olta ve kilit); gamepad ile oynanış.
3. Yolcu yürüyüşlerinin kalabalık koridorda takılmaması; kapının açılış zamanlaması; online istemcide gangway yürüyüşünün akıcılığı.
7. Yeni dümen modelinin dengesi: hızlanma, tornistanla durma mesafesi (~15 m) ve 12 m'lik yanaşma alanı; gerekirse `ShipSteering` sabitleri.
4. Kilit açma ve çekiç sonuç sesleri henüz yok (yalnız efektler ve bildirim metinleri var).
5. Online devamda gerçek Relay ile yeniden katılma; kayıtlı dünyanın tarayıcıda (WebGL) yeniden açılması.
6. Yüzme, ladders ve kazı için sanat: modeller primitif/boyalı ilk sürümdür; animasyonlar Blender'da yazıldı, Mixamo ile değiştirilebilir.
