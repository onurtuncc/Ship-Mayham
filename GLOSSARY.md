# Ship Mayham

1–4 kişilik kooperatif parti oyunu: ekip, iki liman arasında yolcu taşıyan küçük bir geminin mürettebatı olur. Bu sözlük oyunun ortak dilidir; kod ve dokümanlar bu terimleri kullanır. Düz yazı Türkçedir, terimler kodla uyumlu İngilizcedir.

## Language

### Ekip ve oturum

**Player**:
Oyunu oynayan insan ve onun ağ bağlantısı.
_Avoid_: Client (bağlantıyı değil kişiyi kastediyorsan), kullanıcı

**Crew member** (mürettebat):
Gemideki dört sabit slottan biri ve o slottaki karakter. Bir Player ayrılsa bile slotu kimliğiyle korunur ve geri dönebilir.
_Avoid_: Oyuncu karakteri, avatar

**Host**:
Oyun kurallarının tek sahibi olan Player. Diğer Player'lar yalnızca durumu gösterir ve niyet gönderir.
_Avoid_: Server (oyuncu olarak anlatırken)

**Online voyage**:
Join code ile paylaşılan, en çok dört Player'lık internet oturumu. Solo oyun aynı kuralları ağsız yerel host ile çalıştırır.
_Avoid_: Lobi, oda, session (genel anlamda)

**Crew roster**:
Kayıt defteri: hangi Player'ın hangi Crew member slotuna girebileceği ve ayrılan bir Player'ın slotunun ne zaman boşaltılacağı.
_Avoid_: Oyuncu listesi, lobi listesi

**Session phase**:
Online voyage'ın iki aşaması: Lobby ve Voyage. Voyage phase'ten (Boarding, Sailing...) farklıdır; o, tek bir Voyage'ın içindeki durumdur.
_Avoid_: Oyun durumu, faz (Roadmap phase ile karışır)

**Lobby**:
Online voyage'ın, ilk Voyage başlamadan önceki bekleme aşaması. Player'lar toplanır, Ready basar, Host başlatır. Yalnızca oturumun başında bir kez vardır; Unity'nin Lobby servisi değildir.
_Avoid_: Oda, bekleme odası

**Ready**:
Host dışındaki bir Player'ın Lobby'de "başlamaya hazırım" işareti; istediği an geri alabilir. Host ayrıca Ready basmaz.
_Avoid_: Onay, hazır olma

**Ping**:
Bir Crew member'ın dünyaya bıraktığı kısa süreli işaret; baktığı nesnenin adını taşır. Sohbetsiz iletişim aracıdır.
_Avoid_: Marker, çağrı

**Emote**:
Ping çarkından seçilen kısa jest ("Help!", "Thanks!", "Over here!", "Watch out!"): karakter animasyonu ve başının üstünde konuşma balonu.
_Avoid_: Sticker, tepki

**Tip**:
Bir durum ilk kez oluştuğunda çıkan tek seferlik onboarding kartı; cihazda "görüldü" olarak saklanır.
_Avoid_: Tutorial adımı, coach mark

**Award**:
Voyage report'ta öne çıkan Crew member'a verilen esprili tanım ("Yolcu Atıcı"). Rekabet değil, anlatılabilir an üretir.
_Avoid_: Başarım, rozet (başarım kalıcı koleksiyondur)

**Shoal**:
Rotadaki sabit sığ ya da kayalık engel; Helm'in hızına oranla Hull hasarı verir.
_Avoid_: Kaya, engel (genel)

**Rogue wave**:
Önceden yönüyle uyarılan kaba dalga; Helm'i dalgaya çevirmek gemiyi ve Passenger'ları korur.
_Avoid_: Fırtına (fırtına Storm Passage Route'udur)

**Workload scale**:
Crew sayısına göre Ship event sıklığı ve Pirates baskısını ölçekleyen katsayı; Passenger sayısı ve ekonomi sabit kalır.
_Avoid_: Zorluk seviyesi

**Nickname**:
Player'ın Lobby'de ve gemide görünen, cihazda saklanan adı. Kimlik değildir; kimlik Relay oyuncu kimliğidir.
_Avoid_: Kullanıcı adı, hesap adı

**Join code**:
Bir Online voyage'a katılmak için paylaşılan kısa kod.
_Avoid_: Oda kodu, davet kodu

**Host migration**:
Host ayrılınca oturumun başka bir Player'a devredilip sefer durumuyla devam etmesi.
_Avoid_: Host devri (kod ve testler İngilizce terimi kullanır)

### Sefer

**Voyage** (sefer):
İki Port arasındaki tek yolculuk; Boarding ile başlar, Arrived veya Failed ile biter.
_Avoid_: Trip, yolculuk, tur

**Voyage phase**:
Seferin dört durumu: Boarding, Sailing, Arrived, Failed.
_Avoid_: Faz (Faz yol haritası aşamasıdır)

**Route**:
Seferin zorluk ve ödül profili: Coastal Ferry, Market Run, Storm Passage. Fiziksel rota değil, sözleşme profilidir.
_Avoid_: Contract, sözleşme, rota profili

**Reputation**:
Başarılı seferlerle kazanılan ortak itibar; Route'ların ve Livery'lerin kilidini açar. Harcanmaz.
_Avoid_: Level, XP, ün

**Wallet** (ortak kasa):
Ekibin paylaştığı Coins havuzu. Gelir Voyage sonunda yazılır, harcamalar anında düşer.
_Avoid_: Bütçe, para birimi adı olarak "gold"

**Voyage report**:
Seferin sonunda bilet, servis, bahşiş, bonus ve masrafların ayrı gösterildiği net gelir özeti.
_Avoid_: Skor ekranı

### Gemi

**Station**:
Gemide bir işin yapıldığı sabit nokta: Manifest, Helm, Food, Drink, Repair, Cannon, Power, Ammo, Repair supply ve üç Upgrade istasyonu.
_Avoid_: Panel, terminal, makine

**Manifest**:
Purser'ın masası: yolcuların gemiye alındığı, yeni Voyage'ın hazırlandığı ve gemi Upgrade'lerinin satın alındığı Station.
_Avoid_: Check-in, kabul masası

**Purser**:
Manifest masasının arkasında duran NPC. Onunla konuşmak masa panelini açar: yolcu alma, yeni Voyage ve Upgrade satın alma. Yalnızca limandayken (Sailing dışında) çalışır.
_Avoid_: Gişe görevlisi, memur

**Helm**:
Gemiyi süren Station; aynı anda tek Crew member kullanır. Bırakılınca gemi hız ve yönünü korur.
_Avoid_: Dümen istasyonu, direksiyon

**Hull**:
Geminin sağlık değeri. Hull shield, limanda satın alınan ve hasarın bir kısmını emen ek katmandır.
_Avoid_: Can, HP

**Leak**:
Seyir sırasında çıkan, Hull'u eritmeye devam eden su kaçağı. Elle yamayla veya Quick patch kit ile kapatılır.
_Avoid_: Delik, arıza (arıza genel terimdir)

**Quick patch kit**:
Repair supply Station'ından taşınıp Repair tezgâhına teslim edilen, Leak'i hızlı kapatan fiziksel kit.
_Avoid_: Yama paketi

**Power outage**:
Elektriğin kesilmesi; Power Station'ında sigortalar sıfırlanana kadar yeni yiyecek/içecek alınamaz.
_Avoid_: Blackout

**Upgrade**:
Gemiyi kalıcı güçlendiren üç yükseltmeden biri: Engine, Capacity (seats), Comfort. Purser'dan, yalnızca limandayken satın alınır.
_Avoid_: Modül, geliştirme

**Livery**:
Gemi gövde boyası: Classic, Seafoam, Sunset. Yalnızca görünümdür, güç etkisi yoktur.
_Avoid_: Skin, kozmetik

### Yolcular

**Passenger** (yolcu):
Gemiye alınan, sipariş veren, bilet taşıyan ve Voyage'ın gelirini belirleyen NPC.
_Avoid_: Müşteri, NPC (genel anlamda)

**Order**:
Passenger'ın istediği ürün (yiyecek veya içecek). Süre dolarsa memnuniyet düşer.
_Avoid_: Talep, request

**Service**:
Bir Order'ın doğru ürünle karşılanması. Voyage gelirine servis ücreti olarak yazılır.
_Avoid_: Teslimat (Dock job teslimatıyla karışır)

**Ticket**:
Passenger'ın bileti. Kontrol edilir (check), ödenmemişse fare tahsil edilir (collect); bu iki ayrı eylemdir.
_Avoid_: Bilet kontrolü ve tahsilat tek işlem gibi anlatılmasın

**Fare**:
Ödenmemiş biletten tahsil edilen ücret.
_Avoid_: Ticket price

**Rescue**:
Denizden yolcuyu geri almak; süreli ve masraflıdır.
_Avoid_: Kurtarma görevi

### Tehditler ve olaylar

**Pirates**:
Seyir sırasında gelen ve Cannon'la püskürtülen düşman gemi. Karikatürize bir tehdittir.
_Avoid_: Düşman, enemy

**Cannon**:
Pirates'e karşı kullanılan Station; aynı anda tek Crew member kullanır. Gülle fiziksel olarak taşınır.
_Avoid_: Top istasyonu (Station zaten istasyon demektir)

**Ammo**:
Cannon'un gülleleri; magazine (ambar), top yanı stok (rack) ve doldurulmuş atış hazırlığı olarak üç yerde bulunur.
_Avoid_: Mühimmat, mermi (uçuştaki gülle için "cannonball")

**Ship event**:
Seyir sırasında gelen kısa kriz: Contraband, Galley Rush, Loose Cargo, VIP Service, Ticket Audit, Fuse Overload, Engine Overheat, Seat Shuffle.
_Avoid_: Görev, quest, olay (genel anlamda)

**Event deck**:
Ship event'lerin Route ağırlıklarıyla karılan, tur bitmeden tekrar etmeyen destesi.
_Avoid_: Rastgele olay havuzu

### Liman

**Harbor**:
İki Port'taki kıyı bölgesi ve onu çalıştıran sistem; gemiden inilen ve hazırlık yapılan yer.
_Avoid_: Kara hub, iskele (sadece fiziksel yürüme şeridi için)

**Port**:
Harbor'ın iki yerinden biri: Brass Quay (kalkış) veya North Haven (varış).
_Avoid_: Liman (Harbor ile karışır), terminal

**Ashore**:
Crew member'ın gemiden inip Harbor'da olması. Karadayken kalkış ve yeni Voyage engellenir.
_Avoid_: On shore, karada (kod `OnShore` kullanır)

**Gangway**:
Gemi ile Harbor arasındaki geçiş noktası.
_Avoid_: İskele, plank

**Dock job**:
Harbor'da yapılan, ortak ödül veren iş: Cargo relay, Bag sort, Town salvage, Crane loading.
_Avoid_: Town job, liman görevi, side quest

**Crane loading**:
İki rollü (yük tedarikçisi ve vinç operatörü) Dock job.
_Avoid_: Vinç görevi

### Kariyer

**Career progress**:
Host cihazında saklanan kalıcı ilerleme: Coins, Reputation, Upgrade seviyeleri ve tamamlanan Dock job sayısı. Anlık sefer durumunu içermez.
_Avoid_: Save, kayıt (anlık durum için)

### Proje dili

**Slice** (dilim):
Geliştirmenin tek, test edilebilir artımı; `PLAN.md` dilimleri numaralandırır.
_Avoid_: Sprint, milestone

**Roadmap phase** (faz):
GDD ve planın yol haritası aşaması (Faz 0–8). Bir Voyage phase değildir ve bir Slice ile bire bir eşleşmez.
_Avoid_: Phase (tek başına)

**Interaction target**:
Crosshair'in üzerinde olduğu ve Crew member'ın etkileşime girebileceği şey: bir Station, Passenger, Harbor noktası veya hurda paketi.
_Avoid_: Focus, hedef nesne

**Prompt**:
Crosshair'in baktığı nesne için HUD'da gösterilen eylem özeti (başlık, tuş, eylem, detay, maliyet).
_Avoid_: Tooltip, hint (bağlam ipuçları için ayrı: bottom hint strip)
