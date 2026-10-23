# Oyun kuralları tek host'ta çalışır; bağlantı Unity Relay (WSS) ve host migration ile kurulur

Tüm oyun kuralları host'ta `VoyageSimulation` içinde çalışır; istemciler yalnızca durumu gösterir ve niyet (hareket komutu, etkileşim) gönderir. Oyun WebGL'de de çalıştığı ve oyuncuların port yönlendirmesi yapmaması gerektiği için bağlantı Unity Relay üzerinden WebSocket Secure (WSS) ile kurulur; ayrılan host'un yerine geçecek Player için Unity Multiplayer host migration kullanılır.

## Considered Options

- **Ayrı dedicated server**: reddedildi, çünkü 1–4 kişilik bir parti oyunu için işletme maliyeti ve altyapı yükü orantısız.
- **Doğrudan UDP (IP + port)**: yalnızca Windows/Editor'de geliştirme ve test amaçlı bırakıldı; WebGL doğrudan UDP istemcisi olamaz ve oyuncular port yönlendirmek zorunda kalırdı.

## Consequences

- WebSocket TCP olduğundan head-of-line blocking vardır; hareket için tahmin, uzlaştırma ve snapshot interpolasyonu gerekti ([NETCODE.md](../NETCODE.md)).
- Host ayrılınca oturum sürsün diye anlık sefer durumu migration snapshot'ında taşınır (`VoyageMigrationDataHandler`). Her yeni kalıcı alan hem ağ şemasına hem bu snapshot'a eklenmelidir.
- Şema değişince `PrototypeHud.NetworkProtocolVersion` elle artırılır; farklı sürümler birbirine bağlanamaz.
- Host kuralların sahibidir: hile koruması sınırlıdır (örn. yürüme süresi tavanı) ve host'un cihazı güvenilir kabul edilir.
