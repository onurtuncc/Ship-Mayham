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

WebGL buildi `WebBuildSettings` altında ayrı bir Forward render profili üretir. Storage-buffer shader varyantları tarayıcıda derlenemediği için bu profil klasik light probe kullanır; APV, HDR, native render pass ve FSR kullanmaz. Build sırasında tüm quality seviyeleri bu profile bağlanır; işlem sonunda editörün özgün pipeline atamaları geri yüklenir. Unity'nin [resmî sürüm notları](https://release-notes.ds.unity3d.com/search?from_release=6000.1.17f1&to_release=6000.3.2f1) APV'nin WebGL'de desteklenmediğini belirtir.

**PLAY SOLO (OFFLINE)** soketsiz `SoloTransport` ile çalışır; Relay veya internet oturumu oluşturmaz. Doğrudan UDP host başlatmak WebGL'de desteklenmez. Windows LAN smoke testleri mevcut `Connect` akışını, çevrimiçi oyun ise WSS/Relay akışını kullanır.

Build sonrasında loader, data, framework ve wasm URL'lerine dosyanın SHA-256 özetinden bir sürüm parametresi eklenir. Böylece aynı dosya adlarıyla yapılan yeni yayınlarda tarayıcı önceki shader/veri paketini kullanmaz. Yayını doğrularken yalnız HTTP 200'e bakma: GitHub Pages deploy işinin ilgili commit için tamamlandığını, oyunun açıldığını ve gemi geometrisinin göründüğünü de kontrol et.

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
