# Ship Mayham — Uygulama Planı

Bu plan, GDD’deki vizyonu küçük ve test edilebilir artımlara böler. Her fazın sonunda oynanabilir bir build alınmalı ve oyuncu testi yapılmalıdır.

## Uygulama durumu — 20 Ekim 2026

İlk primitive prototip `Assets/ShipMayham/Scenes/ShipPrototype.unity` altına eklendi. Çalıştırma, kontroller, model değiştirme kuralları ve doğrulama sonuçları için [PROTOTYPE.md](PROTOTYPE.md).

İlk dilim Faz 1–4 ve Faz 6'nın dar bir kesitini kapsar: gemi yerel düzleminde dört kişilik hareket, serbest dümen devri, yolcu kabulü, yiyecek/içecek servisi, su kaçağı, yanaşma, ortak gelir ve üç yükseltme türü. Kamera birinci şahıstır.

**İkinci geliştirme dilimi:** Top istasyonu, korsan karşılaşması ve elektrik arızası eklendi. Topta tek kullanıcı kilidi, serbest devir, nişan/ateş/elle doldurma ve sınırlı mühimmat vardır. Korsan yaklaşma uyarısı verir, gemiyi takip eder ve ateş eder; ilk isabet elektrik kesintisi doğurur. Elektrik paneli üç etkileşimle onarılır; kesinti yeni ürün alımını engeller. Korsanı püskürtme ödülü yalnızca başarılı varışta ödenir. Bu dilim aşağıdaki numaralı Faz 4–5'in temel mekaniklerini kapsar; “Faz 2: Ana sefer döngüsü” ile aynı isimlendirme değildir.

**Üçüncü geliştirme dilimi:** Bilet kontrolü, ödeme yapmadan oturan yolcudan ücret tahsilatı, yolcuyu kaldırma/taşıma/bırakma/fırlatma ve süreli denizden kurtarma eklendi. Kontrol ve tahsilat ayrı eylemlerdir; çift ücret alınamaz. Gelir başarılı varışa bağlıdır; kurtarma masrafı ve memnuniyet kaybı komediyi ekonomiye bağlar. Bu dilim Faz 3 ve Faz 7'nin dar bir kesitidir; rastgele olay destesi veya kara hub'ı değildir. Birinci şahıs ve zıplama korunur.

**Dördüncü geliştirme dilimi ve polish:** İki limanda sınırlı iskele yürüyüşü, E/A ile gemiden inme/binme, yiyecek/içecek satın alma ve gövde koruması eklendi. Satın alınan stok ve kullanılmayan koruma yeni sefere aktarılır. Karada mürettebat varken kalkış ve yeni sefere sıfırlama engellenir. HUD bağlama göre liman/yolcu bilgisi gösterir; ayrıntılı kontroller F1 ile açılır, bildirimler söner ve uzak yolcu etiketleri gizlenir. Bu, Faz 8'in küçük hazırlık hub'ı kesitidir; çoklu rota, kara görevleri veya açık dünya değildir.

**Beşinci geliştirme dilimi:** Host-otoriter, deterministik bir olay destesi eklendi. Kaçak kargo taşıyan şüpheli yolcuyu bilet kontrolüyle bulma, iki hızlı servis tamamlama ve tamir panelinden savrulan yükü sabitleme olayları sırayla fakat sefer numarasına göre dönen başlangıçla gelir. Başarı varışta olay bonusu, zaman aşımı ise arızaya uygun hasar/memnuniyet/elektrik sonucu üretir. Aktif olay ve deste konumu host migration anlık görüntüsüne dahildir.

**Altıncı geliştirme dilimi:** Limandaki sözleşme panosu üzerinden üç rota profili seçilebilir. Coastal Ferry standart akıştır; Market Run üç servis karşılığında ek ödeme verir; Storm Passage krizleri ve ilk sızıntıyı erkene çeker fakat sağlam varışa yüksek bonus ve itibar kazandırır. Seçim host otoritesindedir, yalnız karadaki mürettebat tarafından yolcular alınmadan önce değiştirilebilir ve sefer sonucunda ortak ekonomi/itibara yansır.

Fazlar henüz bütünüyle tamamlanmış değildir: fiziksel mermi/taşınan gülle, rastgele olay destesi, gelişmiş arıza mini oyunları, kapsamlı kara görevleri, kayıt ve gelişmiş yolcu davranışları sonraki dilimlerde yapılacaktır. Doğrulama kapsamı ve mevcut sınırlamalar [PROTOTYPE.md](PROTOTYPE.md) içinde tutulur.

## Faz 0 — Proje ve test altyapısı

**Amaç:** Mevcut Unity projesini gemi prototipine hazır hale getirmek.

- Dokümantasyon klasörünü ve temel oyun veri yapılarını oluştur.
- Mevcut multiplayer template akışını doğrula.
- Oyuncu spawn, bağlantı kopması ve yeniden bağlanma davranışlarını test et.
- Basit debug HUD: oyuncu numarası, bağlantı durumu, sefer durumu.
- Geliştirme sahnesini tek gemi prototipi için düzenle.

**Çıkış kriteri:** 1–4 oyuncu aynı sahnede hareket edebiliyor ve bağlantı durumları güvenilir şekilde görülüyor.

## Faz 1 — Gemide hareket ve istasyonlar

**Amaç:** Oyuncuların gemide fiziksel olarak rol paylaşabilmesi.

- Gemi güvertesini oynanabilir alan olarak ayarla.
- Dümen, servis tezgâhı, tamir paneli ve top için etkileşim noktaları oluştur.
- İstasyon kullanımı, bırakma ve başka oyuncunun istasyonu devralması.
- Oyuncu animasyon/etkileşim geri bildirimi.
- Basit gemi içi kamera sınırları ve düşme/kurtarma davranışı.

**Çıkış kriteri:** Dört oyuncu aynı anda dört farklı istasyonda anlamlı bir eylem yapabiliyor.

## Faz 2 — Ana sefer döngüsü

**Amaç:** Limandan çıkıp hedefe ulaşma ve ödeme alma.

- Başlangıç limanı ve hedef liman.
- Basit rota/harita ekranı.
- Gemi hareketi ve yanaşma.
- Sefer başlangıç/seyir/varış durum makinesi.
- Sefer sonu ödeme ekranı.
- Yolcu kapasitesi ve temel bilet geliri.

**Çıkış kriteri:** Oyuncular bir seferi başlatıp, gemiyi hedefe götürüp, anlaşılır bir ödeme alabiliyor.

## Faz 3 — Yolcu hizmetleri

**Amaç:** Servis rolünü ekonomik döngüye bağlamak.

- Yolcu spawn ve hedef liman etiketi.
- Yolcu kabulü ve gemiye binme.
- Basit yiyecek/içecek siparişi.
- Bilet kontrolü.
- Memnuniyet, bekleme ve bahşiş hesabı.
- Basit yolcu tepkileri: bekleme, teşekkür, şikâyet, ayrılma.

**Çıkış kriteri:** Servis oyuncusu yolcu ihtiyaçlarını yöneterek sefer gelirini ölçülebilir biçimde değiştirebiliyor.

## Faz 4 — Arıza ve acil durumlar

**Amaç:** Dört oyuncunun görev paylaşımını zorunlu hale getirmek.

- Elektrik kesintisi.
- Su sızıntısı/gövde deliği.
- Ortak gemi sağlık modeli.
- Arıza alarmı, hedef göstergesi ve süre baskısı.
- Tamir malzemesi ve basit kaynak yönetimi.
- Arızanın yolcu hizmetlerine etkisi.

**Çıkış kriteri:** Bir olay çıktığında ekip görevleri bölüşüyor; arızayı görmezden gelmek gelir veya gemi sağlığı açısından hissedilir sonuç doğuruyor.

## Faz 5 — Korsan karşılaşması

**Amaç:** Topçu rolünü ana döngüye bağlamak.

- Basit korsan gemisi AI’ı.
- Tespit, hedefleme, ateş etme ve mermi dolumu.
- Korsan hasarı ve gemi savunması.
- Saldırı sırasında gemide doğan ikincil arıza.
- Basit ödül/ceza.

**Çıkış kriteri:** Korsan saldırısı yalnızca bir nişan alma mini oyunu değil; diğer oyuncuların da katıldığı koordinasyon olayıdır.

## Faz 6 — İlk ekonomi ve yükseltmeler

**Amaç:** Oyuncuların para kazanıp anlamlı bir seçim yapması.

- Sefer net gelir hesabı.
- Ortak para cüzdanı.
- 3–5 mekanik gemi yükseltmesi.
- Birkaç görsel kozmetik.
- Liman içi yükseltme menüsü.
- Başarısız seferden sonra kısmi toparlanma.

**Çıkış kriteri:** Oyuncular bir seferden sonra en az bir yükseltmeyi tartışıp seçiyor; yükseltme bir sonraki seferin oyun tarzını etkiliyor.

## Faz 7 — Olay destesi ve komedi prototipleri

**Amaç:** Tekrarlanabilirlik ve oyuncu kaynaklı hikâyeler.

- Olay üretici ve ağırlıklandırma sistemi.
- 8–12 basit olay.
- Sahte biletli yolcu.
- Ödeme yapmadan oturan yolcu.
- Kötü niyetli NPC için temel ipuçları.
- Olay sonuçlarının gelir, itibar ve memnuniyete etkisi.
- Güvenli fiziksel komedi etkileşimleri.

**Çıkış kriteri:** Aynı rota iki kez oynandığında olay sırası ve çözüm şekilleri belirgin biçimde farklılaşıyor.

## Faz 8 — Kara hub ve rota çeşitliliği

**Amaç:** Seferler arasındaki tempo ve uzun vadeli hedefi güçlendirmek.

- Küçük liman hub’ı.
- Erzak, bakım, yükseltme ve kozmetik dükkânları.
- En az 3 farklı rota profili.
- İtibar ve yeni rota açılımı.
- Kısa NPC görevleri veya liman mini oyunları.

**Çıkış kriteri:** Oyuncular sefer dışında da karar verebiliyor ve karadaki zaman oyunun ritmini yavaşlatmadan anlamlı seçim sunuyor.

## Önceliklendirme ilkesi

Bir özellik aşağıdaki sırayla değerlendirilmelidir:

1. Dört oyuncunun aynı gemide koordinasyonunu artırıyor mu?
2. Ana sefer döngüsünü daha okunur ve eğlenceli yapıyor mu?
3. Tekrar oynanabilirlik veya komedi üretiyor mu?
4. Gelişim için anlamlı bir harcama alanı oluşturuyor mu?

Bu sorulara cevap vermeyen özellikler MVP sonrasına bırakılmalıdır.

## İlk dikey dilim

İlk hedef, tüm oyunu küçük ölçekte temsil eden şu dikey dilimdir:

`Liman → 4 oyuncu gemiye biner → 4 istasyon → 4 yolcu → 1 su kaçağı → 1 korsan saldırısı → hedef liman → ödeme → 1 yükseltme`

Bu dilim tamamlanmadan kapsam; kara, gelişmiş NPC, çoklu rota ve çok sayıda kozmetik yönünde büyütülmemelidir.

## Test soruları

Her prototip build’inde aşağıdaki ölçümler toplanmalıdır:

- Oyuncular hangi rolü seçti ve ne kadar süre kullandı?
- İlk arızaya tepki verme süresi neydi?
- Oyuncular hangi bilgiyi kaçırdı?
- Sefer sırasında kaç kez istasyon değiştirildi?
- Oyuncular başarısız olduğunda nedenini anlayabildi mi?
- Sefer sonunda hangi yükseltme istendi ve neden?
- Aynı ekip ikinci seferi oynamak istedi mi?

## Riskler ve erken çözümler

| Risk | Erken belirti | Önlem |
|---|---|---|
| Bir rol diğerlerinden sıkıcı kalır | Oyuncular sürekli rol değiştiriyor | Her role kısa vadeli karar, olay ve görünür etki ekle |
| Gemi çok karmaşık olur | Oyuncular hedef göstergelerini takip edemiyor | Önce 4 istasyon, sonra yeni sistem ekle |
| Rastgelelik adaletsiz görünür | Olaylar çözülemiyormuş gibi hissediliyor | Olay ön uyarısı ve en az iki çözüm sun |
| Kara bölümü tempoyu düşürür | Oyuncular limanı atlamak istiyor | Hub’ı kısa tut, kritik kararları hızlı sun |
| Ağ senkronizasyonu güvenilmez olur | Yolcu/para durumları farklı görünüyor | Kritik ekonomi ve olayları server otoritesine taşı |
| İçerik üretim maliyeti büyür | Her olay özel kod ve sahne istiyor | Veri tabanlı olay tanımları ve yeniden kullanılabilir etkileşimler kullan |

## Sonraki karar noktası

Dördüncü dilimin oyuncu testinde limana geçişin bulunabilirliğini, ekip geri dönmeden kalkış engelini, stok/bakım harcamalarının değerini ve HUD okunurluğunu değerlendirelim. Sonraki dilim adayı veri tabanlı olay destesi ve sızmacı ipuçlarıdır; tekrar oynanabilirliği artırmalı, aynı anda dört kişiyi boğan olay yığılmalarını sınırlamalıdır. Alternatif rota çeşitliliğidir. Bunlar henüz uygulanmadı; yeni özellikten önce mevcut akışın dört insanla eğlenceli olduğu doğrulanmalıdır.
