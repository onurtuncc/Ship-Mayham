# Adalar, özgür keşif ve baştan sona döngü — 26 Ekim 2026

Kullanıcı geri bildirimi: oyunda düzgün bir döngü yok (hep başa dönüyoruz), kara az çeşitli, yüzmede dalma yok ve yüzülebilecek yerlerde görünmez sınırlar var; oyun daha özgür/keşfedilebilir olmalı ve başlangıçtan sona giden bir döngüye girmeli.

Kararlar (kullanıcı onayladı):

- **Ana döngü**: açık denizde 5–6 ada/liman. Kaptan haritadan sonraki adayı seçer; her adanın kendi işleri ve yolcu hedefi vardır. Küçük gemiden büyük gemiye, son adada final.
- **Kara**: her ada kendi biçimine ve iş setine sahip olur; kara serbest yürünür, görünmez duvar yok.
- **Su**: dalma + geniş yüzme alanı; sınırlar doğal olur (derin su/akıntı) ve oyuncuya gösterilir.
- **Hedef**: hikaye + gemi yükseltmesi ile final; sonra yeni oyun+.

## Bugünkü durum (neden böyle hissettiriyor)

Üç gözlem, kodla doğrulandı (`HarborTownLayout`, `VoyageSwimming`, `PrototypeHarbor`):

1. **Kara bir maket**: yaklaşık 40×46 m'lik düz bir platform ve üstünde bina kutuları. Etrafı sonsuz düz deniz, deniz tabanı yok. Yürünebilirlik "dikdörtgen + bina kutuları" ile belirlenir.
2. **Su bir kutu**: yüzme alanı geminin yanında 20×28 m'lik dikdörtgendir (`WaterMin/WaterMax`); dışı görünmez duvardır.
3. **Döngü iki limanlı**: iki sabit sahne nesnesi (`PortRoots[0/1]`, rota hedefi `(0,220)`), her sefer aynı iki kasaba arasında gidip gelir.

Ayrıca kara koordinatları gemiye göre sabit bir ofsetle (`QuayOffsetX = -14`) tanımlıdır; gemi limana "yapışır".

## Mimari karar: ada bir yükseklik haritasıdır

Her adanın zemini saf C# bir yükseklik fonksiyonudur (`IslandTerrain`): aynı fonksiyon (1) sunucuda yürünebilirlik/yüzme/derinlik kararı, (2) istemcide zemin mesh'i ve deniz tabanı üretimi için kullanılır. Böylece kıyı çizgisi bir kural değil bir sonuçtur: sığ suda yürünür, belden yukarı suda yüzülür, daha derinde dalınır; kıyıya çıkınca ayağa kalkılır. Merdiven seçeneği kalır ama zorunlu olmaz. Mevcut iki liman, platformun etrafına plaj ve deniz tabanı eklenerek gerçek adaya çevrilir.

Koordinat çerçevesi aynı kalır (gemi-yerel çerçeve + `QuayOffsetX`), böylece gangway, yolcu akışı ve mevcut testler bozulmaz.

## Dilimler

| # | Dilim | İçerik | Bitti sayılır |
| --- | --- | --- | --- |
| A | Dalma ve açık su | `CrewState.Depth` ve nefes; Space basılı = dal, bırak = yüzeye çık; deniz tabanı; su altı kamera/sis; geniş yüzme alanı, yumuşak sınır ("akıntı geri taşır" + yön göstergesi); batık paralar ve hazineler derinde | testler yeşil, dalış capture'ı, protokol bump |
| B | Ada zemini | `IslandTerrain` + mesh view; platform çevresinde plaj, kayalık, deniz tabanı; yürüyerek suya girme/çıkma; kara alanı platformun ötesine genişler | kıyıdan serbest geçiş, görünmez duvarsız |
| C | Kampanya | `Campaign` (adalar, sıra, açılma koşulu, hikaye parçası), hedef adayı seçme (harita arayüzü), ada başına liman anahtarı, final, yeni oyun+, kayıt | baştan sona oynanabilir döngü testi |
| D | Ada içeriği | Ada başına veri: zemin parametreleri, prop yerleşimi, aktivite seti (sandık/kazı/olta/NPC görevi/yağma), dükkân alt kümesi, korsan/tehlike yoğunluğu | her adada ayrı iş seti |
| E | Cila | Ada müziği/gökyüzü, ipuçları, HUD haritası, dokümantasyon, yayın | web yayını doğrulanmış |

## Riskler

- Mevcut 745 test iki liman ve dikdörtgen su varsayıyor; her dilim önce eski davranışı korur (varsayılan ada = mevcut limanlar), yeni davranış veriyle açılır.
- WebGL belleği: ada mesh'leri düşük çözünürlüklü (≤ 128×128 vertex) ve ada başına yalnız etkin ada kurulur.
- İki oyunculu koordinat çerçevesi (kara/yüzme) dönüşümü; her geçişte dönüşüm testle kilitlenir.
