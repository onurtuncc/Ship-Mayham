# GitHub Pages üzerinde WebGL yayını

Bu repo, Unity lisans bilgilerini GitHub'a göndermeden yerel bilgisayardan WebGL build alıp GitHub Pages'a yayınlanır.

Beklenen adres:

`https://onurtuncc.github.io/Ship-Mayham/`

## Tek komutla build ve yayın

Unity Hub'da **Installs → 6000.4.6f1 → Manage → Add modules → Web Build Support** modülünün kurulu ve Unity lisansının bu bilgisayarda etkin olduğundan emin ol. Ardından proje kökünde çalıştır:

```powershell
.\Scripts\Publish-WebGL.ps1
```

Script şu işlemleri yapar:

1. Projenin kayıtlı Unity sürümünü bulur.
2. `ShipPrototype` sahnesini `Builds/WebGL/` klasörüne derler.
3. Çıktıyı geçici bir Git worktree ile `gh-pages` dalına gönderir.
4. Geçici worktree'yi temizler.

Ana proje Unity Editor'de açıksa script, proje kilidine çarpmamak için `Builds/ShipPrototypeValidation/` altındaki kalıcı ve cache'li build workspace'ini günceller. İlk senkronizasyon sonraki yayınlardan daha uzun sürebilir.

Unity farklı bir konumdaysa yolu açıkça verebilirsin:

```powershell
.\Scripts\Publish-WebGL.ps1 -UnityPath "D:\Unity\Editor\Unity.exe"
```

Build zaten hazırsa yalnızca yayınlamak için:

```powershell
.\Scripts\Publish-WebGL.ps1 -SkipBuild
```

GitHub Actions içinde Unity build çalıştırılmadığı için `UNITY_LICENSE`, `UNITY_EMAIL` veya `UNITY_PASSWORD` secret'larına ihtiyaç yoktur. GitHub Pages kaynağı `gh-pages` dalının kök dizinidir.

## Yerel WebGL build

Unity Hub'da **Installs → 6000.4.6f1 → Manage → Add modules → Web Build Support** modülünü kur. Ardından projeyi açıp:

**Ship Mayham → Build Prototype (WebGL)**

menüsünü çalıştır. Çıktı `Builds/WebGL/` klasörüne yazılır. Build'i dosya sisteminden `index.html` dosyasına çift tıklayarak değil, yerel bir HTTP sunucusuyla aç:

```powershell
python -m http.server 8000 --directory Builds/WebGL
```

Sonra `http://localhost:8000` adresine git.

## Online multiplayer kurulumu

Prototip artık Unity Multiplayer Services oturumlarını, Relay'i ve güvenli WebSocket (`WSS`) aktarımını kullanır. Bu nedenle hem GitHub Pages'taki WebGL sürümü hem de Windows sürümü internet üzerinden aynı oturuma bağlanabilir:

1. İlk oyuncu **CREATE ONLINE VOYAGE** ile özel bir oturum açar.
2. Ekrandaki kodu diğer oyuncularla paylaşır.
3. Diğer oyuncular kodu yazıp **JOIN ONLINE VOYAGE** seçer.

Unity projesi Cloud Project ID'ye bağlıdır. Unity Dashboard'da bu proje için **Authentication**, **Lobby** ve **Relay** hizmetlerinin etkin olduğundan emin ol. Oyuncular şimdilik anonim kimlikle giriş yapar; ayrıca hesap ekranı gerekmez. Ağ trafiği Relay üzerinden geçtiği için modem portu açmaya veya oyuncuların IP adresini paylaşmaya gerek yoktur.

**LOCAL SOLO (OFFLINE TEST)** yalnızca aynı makinede hızlı geliştirme testi içindir; Pages oyuncularının birbirine bağlanması için online düğmeler kullanılmalıdır.

## Host ayrılırsa

Oturum, Unity'nin yerleşik host migration akışını kullanır. Mevcut host her 2 saniyede bir sefer durumunu Lobby migration verisine yükler. Host tarayıcıyı kapatır veya bağlantısı düşerse Lobby kalan oyunculardan yeni host'u seçer; Relay bağlantısı yeniden kurulur ve yeni host son görüntüyü uygular. Anonim Unity oyuncu kimliği mürettebat slotuyla birlikte saklandığı için yeniden bağlanan oyuncular eski slotlarını devralır.

Bu, dedicated server değildir: geçiş birkaç saniye sürebilir ve son görüntüden sonraki en fazla yaklaşık 2 saniyelik hareket/girdi kaybolabilir. Tüm oyuncular ayrılırsa oturum doğal olarak sona erer. Daha sonra kesintisiz 7/24 sunucu istenirse Unity Multiplay/Dedicated Server ayrı bir mimari adımıdır.
