# GDD genişletmesi — öncelik listesinin 2–6. maddeleri

Dal: codex/gdd-phases-2-6. Başlangıç commit'i: 441fef9.
Bu numaralar eski GDD fazları veya PLAN.md içindeki tarihsel dilim numaraları değildir.
1. madde (onboarding) kapsam dışında tutuldu. Main'e birleştirme hazırlığı sırasında güncel main commitleriyle uzlaştırıldı.

## 2 — Kaptan tehlikeleri ve ekip temposu

Main'deki HazardLogic/VoyageHazards tek kayalık/dalga otoritesidir; ikinci hasar ve çizim sistemi kaldırıldı.
Coastal/Market/Storm güzergâhlarında 3/4/6 Shoal bulunur. Temas gemiyi dışarı iter, hızını düşürür
ve cooldown'lı Hull hasarı verir; yavaşlamak hasarı azaltır fakat tamamen kaldırmaz.
Main'deki minimap, yönlü uyarılar, ses ve görsel geri bildirim korunur.
Market Run ve Storm Passage Rogue wave üretir; beş saniyelik uyarıda gemiyi dalga yönüne
50 derece içinde çevirmek hasarı önler. İlk planlanan dalga aralığı rotaya göre 50/30 saniyedir.

Bağlı Crew sayısı 4/3/2/1 olduğunda Workload scale sırasıyla 1/.8/.6/.45'tir.
Event araları, dalga sıklığı ve Pirates atış aralığı ölçeklenir.
Aktif Event süreleri, Passenger sayısı ve ekonomi değiştirilmez.

## 3 — Yaşayan Passenger'lar ve temizlik

Patient, Impatient ve Nervous profilleri bekleme/korkuya farklı tepki verir.
En fazla iki Passenger kısa koridor yürüyüşleri yapar ve koltuğuna döner.
Engellenen yürüyüşler süre aşımıyla güvenle sonlanır.
Servis sırasında yürüyen Passenger ayakta duraklar; koridorda oturur poza geçmez.
Taşıma, servis ve Rescue mevcut akışlarla birlikte çalışır.

Passenger'a bakarken R / D-pad aşağı, 12 saniye cooldown'lı sakinleştirmedir.
Güvertedeki döküntü yakındaki memnuniyeti azaltır; boş elle üç E/A etkileşimiyle temizlenir.
Walking/Standing animasyonları mevcut Mixamo kliplerini kullanır; yeni indirme gerekmez.

## 4 — Mühendislik kararları

Elektrik yangını altı saniye önceden uyarır. Yanarken Hull hasarı verir.
POWER'da R / D-pad aşağı devreyi yalıtır: yangın hasarı durur, Food/Drink tezgâhları kapanır.
KITS'te R kırmızı söndürücüyü alır/iade eder; yangına bakarken E/A püskürtür.
Ortak dört şarj vardır; iki püskürtme yangını söndürür. Ardından POWER sigortaları sıfırlanabilir.
Yalıtım, bağımsız bir Leak'in hasarını durdurmaz.

REPAIR'da R, A/B/C vanalarını döndürür; sıcak boru ipucu doğru vanayı gösterir.
E seçimi uygular: doğru vana kit harcamadan Leak'i kapatır, yanlış vana 2 hasar verir.
Mevcut dört adımlı elle yama ve Quick patch kit seçeneği korunur.

## 5 — Alternatif çözümler ve deniz fırsatı

İncelenmiş fakat ödenmemiş Ticket için, döküntü varken Passenger'a R ile temizlik işi önerilebilir.
Passenger döküntüye yürür ve altı saniye çalışır; Fare kapanır fakat 40 Coins üretilmez.
Elle tahsilat veya Passenger'ı kaldırmak bitmemiş işi iptal eder.
Engellenen işlerde süre aşımı vardır; tamamlanan iş tekrar gelir oluşturmaz.

Güverte dolabından iki Temporary plug alınabilir; REPAIR'da kullanmak Leak'i 20 saniye tutar.
Kalıcı yama/vana/kit hâlâ gerekir. İade ve bağlantı kaybı envanteri korur.
Elde taşınan tıkaç kahverengi ve söndürücüden/siparişlerden ayrı biçimdedir.

Doğrudan güzergâhın batısında Sea chest belirir.
Gemiyle 15 m içine girip 1 m/s veya altına yavaşladıktan sonra sol Recovery rope noktasında E/A ile alınır.
30 Coins yalnız başarılı varışta, bir kez ödenir; toplama anında Wallet artmaz.

## 6 — Ekipman, para harcama ve fiziksel güzergâhlar

Purser panelindeki GEAR / PASSAGE sekmesi:

- Service tray: 90 Coins; aynı üründen iki Order taşır. Her servis yalnız birini tüketir.
- Workshop: 90 Coins; sefer başına bir kez +2 kit, üç kitlik Repair tezgâhı.
- Pennant: 60 Coins; sonrasında ücretsiz göster/gizle. Mekanik güç etkisi yoktur.

Her ekipman bir kez satın alınır; sahip olunan ekipmanlar limanda ücretsiz değiştirilebilir.
Workshop bonusu tekrar seçerek çoğaltılamaz. Elde kalan Order'lar iade/kopma sırasında korunur.
Seçimler limanda, boş elle ve Passenger'lar alınmadan yapılır; mevcut Upgrade'ler UPGRADES sekmesindedir.

Direct practice passage korunur.
Safe western passage ve Risky eastern shortcut, yanaşmadan önce sıralı iki fiziksel Gate ziyareti gerektirir.
Başarılı varış bonusları +12/+25'tir. Minimap ve dünya işaretleri sonraki Gate'i gösterir.
Safe güzergâh ilk Shoal'dan önce batıya sapar; 128 farklı tohumla düz Gate bağlantıları da sınanır.
Bunlar aynı iki Port arasındaki farklı güzergâhlardır; yeni Port veya harita değildir.

Career progress v2, ekipman/Pennant sahipliğini saklar ve v1 kayıtların Wallet'ını koruyarak okur.
Migration snapshot 19 ve ağ protokolü 22 tüm yeni durumları taşır.
Diğer geliştirme dallarıyla birleştirilirken şema sürümleri birlikte uzlaştırılmalı/artırılmalıdır.

## Mimari, model değişimi ve kabul sınırları

Kurallar host-otoriter VoyageSimulation partial'larında; sunum eylemleri yetkilendirmez.
RPC'ler gönderen slotunu, mesafeyi, Voyage phase'i, eldeki nesneyi, zıplamayı ve Station kullanımını doğrular.
Timer'lar, NPC işleri, envanter ve Gate ilerlemesi replikasyona ve Host migration'a dahildir.
Mevcut Station sırası, sahne kimlikleri ve koltuk anchor'ları değiştirilmedi.

PrototypeNavigationView ve PrototypeDeckLifeView isimli primitive sunum kökleri üretir.
Gerçek modeller bu presenter'larda, yetkili FirePoint/PlugLocker/RescueRope/CourseGate/ShoalPosition
noktalarını değiştirmeden yerleştirilebilir. Bunlar final sanat varlıkları değildir.
Workshop kitleri mevcut kit modelinden çoğaltılır; ikinci Drink de mavi görünür.

NavigationPhaseTests, PassengerLifePhaseTests, EmergencyPhaseTests, OpportunityPhaseTests,
PreparationPhaseTests ve AdventureSceneTests yeni mekanikleri kapsar.
SmokeInputOwnershipTests, otomatik komut sürücüsü ile scripted RPC hareketinin çakışmasını gerçek sahnede sınar.
Bu çakışma önce iki gerçek süreçte ve başarısız sahne testinde üretildi; düzeltme yalnız development smoke
sürücüsünün girdi sahipliğindedir, normal oyun hareket kuralları değiştirilmedi.

Dört insanla tempo/denge, rota okunabilirliği, komedi ve gamepad hissi ayrıca oynanarak kabul edilmelidir.
Bu dal için WebGL yayını veya canlı Relay Host migration kabulü otomatik olarak yapılmış sayılmaz.

## Son doğrulama — 24 Ekim 2026

- Tam EditMode/sahne paketi: 473 test, 472 başarılı, 0 başarısız, 1 isteğe bağlı ekran görüntüsü testi atlandı.
  Kanıt: Logs/Final-Acceptance.xml ve Logs/Final-Acceptance.log.
- Windows x64 development build: başarılı, çıkış kodu 0.
  Mevcut PC render ayarları kullanıldı; geçici validation pipeline kurulmadı.
  Kanıt: Logs/Final-Acceptance-Build.log. Çıktı: Builds/ShipPrototype/ShipPrototype.exe.
- Dört gerçek süreçli tam Voyage: host ve üç istemci çıkış kodu 0 / SHIP_SMOKE_PASS.
  Uzak hareket/zıplama, iki gülle tedariki, uçuşlu çatışma, Quick patch, Ticket/taşıma/atma/Rescue,
  varış hesabı, Bag sort, liman alışverişi ve yeni Voyage hazırlığı doğrulandı.
  Kanıt: Logs/VoyageSmoke-20261024-142851-13ad63.
  Varış Wallet=243, sonraki hazırlık Wallet=219 / Food=23 / Drink=24 / Hull shield=20.

Yeni kararların deterministik kuralları ve Host migration durumu birim testlerindedir;
yeni menü/temizlik/yangın/sandık komutları AdventureSceneTests içinde gerçek host RPC'siyle çalıştırıldı.
Dört süreçli kontrol, her yeni kararın uzak Player tarafından denenmesi veya canlı internet kabulü yerine geçmez.

Unity'nin build sırasında kendiliğinden yazdığı genel render/servis ayarları ve materyal metadata'sı bu dala dahil edilmedi.
Üretilen DefaultVolumeProfile kopyaları silinmedi; Logs/GeneratedBuildAssets altında geri alınabilir biçimde saklandı.
Önceki doğrulamalar, main ile uzlaştırmadan önceki dalı kapsar. Birleşim sonuçları aşağıda ayrıca kaydedilir; uzak depoya push yapılmaz.

## Main ile uzlaştırma — 24 Ekim 2026

Main'in 3b69c49 commit'ine kadar olan çalışmaları bu dala birleştirildi.
Main'in HazardLogic/VoyageHazards/HazardView uygulaması tek otorite olarak korundu;
tekrar eden NavigationState, hasar döngüsü ve kayalık çizimi kaldırıldı.
Güvenli fiziksel güzergâh yeni kayalık zarfının dışına taşındı ve 128 tohumla sınandı.
Yeni sanat dosyaları, SittingSip klibi ve dokulu prop koruma kodu kayıpsız dahil edildi.

- Logs/Merge-Isolated.xml: 501 test; 500 başarılı, 0 başarısız, 1 isteğe bağlı capture atlandı.
- Logs/Merge-Build.log: Windows development build başarılı.
- Logs/Merge-Latest-Art-Compile.log: son sanat/Editor değişikliklerinin derlemesi başarılı.
- Logs/VoyageSmoke-20261024-150337-3d86e4: dört süreç exit 0 / SHIP_SMOKE_PASS.
  Varış Wallet=227, hazırlık Wallet=203 / Food=23 / Drink=24 / Hull shield=20.

Normal LAN varsayılanı 7777 olarak korundu. Tek-host testleri OS-assigned port (0),
development smoke 19777 kullanır; paralel ajanların oturumları durdurulmaz.
Düz-seyir uyumluluk seferi açık merkez hattı olan sabit Coastal seed 1 kullanır;
tehlikeler kapatılmaz, çarpışma/dalga kuralları HazardTests'te farklı tohumlarla sınanır.

Main'e aktarma öncesi main çalışma kopyasında başka modelin çakışan, commitlenmemiş
oynanış/animasyon değişiklikleri görüldü. Bunlar stash'lenmedi, değiştirilmedi ve commitlenmedi.
Bu önceki kontrolün ardından yolcu düzeltmeleri main'de 505f9b4 ile commitlendi.
Son entegrasyon f73ab7f'yi de içerir: yeni yolcu toparlanma pozları, koltuk sahipliği
ve düzeltilmiş prop eksenleri korunur. Dolaşma rutini artık sahiplenilen koltuğa döner.
Kullanıcı, main'de devam eden diğer değişikliklerin commit dışında korunmasını onayladı.

f73ab7f entegrasyonu: Unity 6000.4.6f1 EditMode tam paket `Logs/Merge-Final.xml`:
522 test, 521 başarılı, 0 başarısız, 1 isteğe bağlı capture atlandı; süreç exit 0.

Sonrasında main'in `4103f9e` iş yükü/ikon commit'i de dahil edildi. Korsan ve olay
gecikmelerini artık bu ortak `Workload` sistemi hesaplar; önceki sürekli sayaç ölçeği
kaldırılarak çifte yavaşlatma önlendi. `CrewPressure` aynı ölçeği kullanır; bağlantısı
askıdaki üyeler aktif iş yüküne sayılmaz. Göç şeması bu ek zamanlamalarla 19 oldu.
Koltuk sahipliğinin dolaşma sonrasında da korunması için odaklı regresyon testi eklendi.

4103f9e dahil son entegrasyon: `Logs/Merge-Workload.xml`, 548 test, 547 başarılı,
0 başarısız, 1 isteğe bağlı capture atlandı; Unity süreç exit 0.
