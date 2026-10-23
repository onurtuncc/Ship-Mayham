# Party hissi ve rol derinliği planı

GDD'ye göre eksikler ve party oyunu iyileştirmeleri, kararlaştırılan sıra ve kurallarla. Bu doküman niyeti kaydeder; her dilim bitince [PLAN.md](PLAN.md) ve ilgili dokümanlar güncellenir. Terimler [GLOSSARY.md](../GLOSSARY.md)'dedir.

Öncelik hattı: **B** party hissi, **A** onboarding ve erişilebilirlik, **C** rol derinliği, **D** içerik çeşitliliği. D bu planın kapsamı dışındadır (hava durumu, sosyal olaylar, yolcu kişilikleri, yeni liman).

## Kararlar

| Konu | Karar |
| --- | --- |
| Ping | `Q` kısa basış: crosshair'in baktığı yere işaret, nesneye bakılıyorsa adı yazılır. Gamepad: D-pad yukarı. Host yayınlar. |
| Emote | `Q` basılı tutma: dört seçenekli çark (Help! / Thanks! / Over here! / Watch out!). Gamepad: D-pad basılı tutma. Jest animasyonu ve konuşma balonu. Oyuncu başına 1 sn bekleme. Metin sohbeti yok. |
| Müzik | Procedural ve uyarlanabilir: Harbor (sakin), Sailing (sabit), Tension (korsan, sızıntı, elektrik, aktif olay); zafer ve yenilgi vuruşları. Yumuşak geçiş. Lisanslı müzik veya ses dosyası yok. |
| Gevezelik | Yolcu ve NPC'ler için kısa hece sesleri (procedural). |
| Ses ayarı | Settings: Master / Music / SFX kaydırıcıları. `M` hepsini kısar. |
| Awards | Host oyuncu başına sayar: fırlatılan yolcu, servis, tamir adımı, topçu isabeti, tahsil edilen fare, kurtarma, dock job teslimi, dümen süresi. Rapor 3–4 ödül gösterir; eşik en az 2 örnek, en yüksek kazanır, beraberlikte ikisi de alır. Solo'da kendi öne çıkanları. Sıralama tablosu yok. |
| Tips | Sırasız, olay bazlı, her biri bir kez; cihazda saklı. Settings'te Tips anahtarı ve "Reset tips". Eğitim seferi yok. |
| Kaptan engelleri | Shoal: tohuma göre sabit yerler, hıza oranlı hasar ve yavaşlama, yaklaşırken korna, minimapte 60 m içinde görünür. Rogue wave: 5 sn önceden yönüyle uyarı, dalgaya çevirmek hasarı önler. |
| Workload scale | Ship event sıklığı ve Pirates baskısı: 4 oyuncu 1,0 / 3 oyuncu 0,8 / 2 oyuncu 0,6 / solo 0,45. Passenger sayısı ve ekonomi sabit. Değerler tahmindir, oyuncu testiyle ayarlanmalıdır. |
| Settings | UI ölçeği (küçük/normal/büyük), kamera sarsıntısı (kapalı/az/tam), fare hassasiyeti, titreşim, Tips. Yiyecek/içecek/onarım için renkten bağımsız ikon ve şekil. Hepsi cihazda saklı. |
| Oyun içi menü | Esc: Resume, Settings, "I'm stuck" (güverteye ışınla, 30 sn bekleme), Leave. Solo'da menü açıkken oyun duraklar; online'da duraklamaz. |
| Kontrolcü titreşimi | Hasar, top atışı ve çarpma anlarında; Settings'ten kapatılır. |
| Kapsam dışı | Gizli kişisel hedefler (GDD açık sorusu), ses sohbeti, hava durumu, yeni liman. |

## Dilimler

Her dilim: kod, birim ve sahne testleri, doküman, commit. Ağı değiştirenler protokol sürümünü artırır (dilim 1 `UnstuckRpc` ile 13'e çıkardı; 2, 4, 6 ve 7 de artıracak).

1. **Ayarlar ve oyun içi menü** (Settings store, kaydırıcılar, UI ölçeği, kamera sarsıntısı, fare hassasiyeti, titreşim, Esc menüsü, "I'm stuck"). Diğer dilimlerin bağımlılığıdır.
2. **Ping ve emote** (`Q`, çark, dünya işaretçisi, konuşma balonu, host yayını).
3. **Uyarlanabilir müzik ve gevezelik** (procedural katmanlar, ses veriyolu ayrımı).
4. **Sefer sonu ödülleri** (host sayaçları, rapor bölümü).
5. **Onboarding tips** (olay bazlı kartlar, kayıt, Settings anahtarı).
6. **Kaptan engelleri** (Shoal, Rogue wave, minimap).
7. **Workload scale** (oyuncu sayısına göre ölçekleme) ve renkten bağımsız ikonlar.

Sıra, bağımlılığa göre: 1 önce gelir (müziğin kaydırıcıları ve diğer dilimlerin ayar anahtarları buradadır).
