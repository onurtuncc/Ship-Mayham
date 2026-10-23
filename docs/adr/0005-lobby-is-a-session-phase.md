# Lobi ayrı bir sahne değil, Online voyage'ın bir oturum aşamasıdır

Online voyage kurulunca NGO host zaten başlar ve slotlar Relay oyuncu kimliğine bağlanır (ADR-0001, ADR-0002). Lobi bu yüzden aynı sahnede ve aynı oturumda bir **Session phase** olarak (Lobby, sonra Voyage) yaşar: oyuncular bağlanır, adlarını ve Ready durumlarını gösterir, host Start deyince simülasyon başlar. Lobide karakterler dolaşmaz, ekran bir arayüzdür.

## Considered Options

- **Ayrı lobi sahnesi, Start'ta oyun sahnesini yükle**: reddedildi. Relay oturumunu ve NGO bağlantısını sahne değişiminde taşımak veya yeniden kurmak ek hata yüzeyi ve host migration'ı iki sahneye bölmek demektir.

## Consequences

- Session phase, her slotun Nickname ve Ready bilgisi ağ durumuna girer: protokol sürümü artar ve host migration snapshot'ı bunları taşır. Lobide host düşerse mevcut migration çalışır.
- Lobi yalnızca oturumun başında bir kez çıkar. Start'tan sonra katılım sürer (drop-in) ve yeni oyuncu lobiyi atlayıp doğrudan oyuna girer; sefer bitince Lobby'ye dönülmez.
- Solo oyun lobiyi atlar.
- Lobide ayrılan oyuncunun slotu saklanmaz (saklanacak sefer durumu yoktur); sefer sırasındaki slot koruma kuralı (ADR-0002) Voyage aşamasında geçerlidir.
