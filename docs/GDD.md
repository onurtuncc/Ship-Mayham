# Ship Mayham — Game Design Document

Dilim 19: Kara için yük tedarikçisi + vinç operatörü rollü yükleme mini oyunu geliştirildi. [HARBOR_CRANE.md](HARBOR_CRANE.md).

Dilim 18: Kara alanı küçük iskeleden gezilebilir liman kasabasına genişletildi; fiziksel dükkânlar, sabit NPC'ler, kara haritası ve Town Salvage işi eklendi. [HARBOR_TOWN.md](HARBOR_TOWN.md).

22 Ekim uygulama eki: on altıncı dilimde liman bagaj ayırma, on yedinci dilimde fiziksel hızlı yama kiti akışı geliştirilmiştir; bu, aşağıdaki tam vizyonun tamamlandığı anlamına gelmez. Kurallar ve doğrulama: [DOCK_SORTING.md](DOCK_SORTING.md), [REPAIR_SUPPLIES.md](REPAIR_SUPPLIES.md).

**Doküman durumu:** v0.1 — İlk konsept ve MVP kapsamı  
**Çalışma adı:** Ship Mayham  
**Tür:** 1–4 oyunculu, kooperatif parti oyunu / kaotik gemi yönetimi  
**Platform hedefi:** PC, gamepad öncelikli  
**Kamera:** Birinci şahıs; fare/sağ analog ile bakış, bakış yönüne göre güvertede hareket  
**Oturum hedefi:** 15–30 dakika  
**Ana referans:** Dear Passengers benzeri ortak görev, fiziksel komedi ve koordinasyon deneyimi

**Uygulama notu — 22 Ekim 2026:** Prototip on beş küçük geliştirme dilimi içerir: ana sefer/servis/arıza/korsan döngüsü, bilet ve yolcu komedisi, liman işleri, sekiz olaylı deste, itibarla rota/kozmetik açılımı, kariyer kaydı, ilk model entegrasyonu, taşınan gülleyle top tedariki, uçuşlu/çarpışmalı top atışları ve yolcu gözlem ipuçları. WebGL ve Relay/WSS yayını vardır; kaynak ve canlı build sürümü ayrıca izlenir. Bu, aşağıdaki tam vizyonun veya insanlarla kabul testinin tamamlandığı anlamına gelmez. Uygulanmış kapsam, kontroller ve test kanıtları [PROTOTYPE.md](PROTOTYPE.md), sonraki kararlar [PLAN.md](PLAN.md) içinde ayrılır.

## 1. Yüksek konsept

Dört arkadaş, yolcu taşıyan küçük bir geminin mürettebatını oluşturur. Limanlar arasında yolcu taşırken gemiyi yürütmek, yolcuları memnun etmek, arızaları gidermek, korsanları savuşturmak ve beklenmedik krizleri yönetmek zorundadırlar. Her başarılı sefer para kazandırır; bu para gemiyi büyütmek, kapasiteyi artırmak, hizmetleri geliştirmek ve kozmetik değişiklikler yapmak için harcanır.

Oyunun eğlencesi kusursuz bir hizmet sunmaktan değil, dört kişinin aynı anda farklı işleri yapmaya çalışırken oluşan iletişim kazalarından, fiziksel komediden ve oyuncuların olayları kendi yöntemleriyle çözmesinden gelir.

### Oyuncu vaadi

> “Dört kişi, tek bir gemi, çok fazla yolcu ve hiçbir şeyin planlandığı gibi gitmediği bir sefer.”

## 2. Tasarım hedefleri

### Hedeflenen deneyim

- Sürekli iletişim ve rol değişimi gerektiren kooperatif oyun.
- Kolay öğrenilen, ustalaşması oyuncu koordinasyonuna bağlı görevler.
- Başarısızlığın cezalandırıcı değil, komik ve tekrar anlatılabilir olması.
- Her seferde farklı olay kombinasyonlarıyla tekrar oynanabilirlik.
- Oyuncuların görevleri özgürce yorumlayabilmesi: ideal çözüm, hızlı çözüm ve tamamen saçma çözüm.
- Kısa oturumlarda anlamlı ilerleme; uzun vadede gemi ve mürettebat gelişimi.

### Kaçınılacaklar

- Tek oyuncunun tüm önemli sistemleri yönetebilmesi.
- Uzun menüler ve görev dışı bekleme.
- Hataların oyuncuyu uzun süre oyundan düşürmesi.
- Rastgeleliğin oyuncu kararlarını anlamsızlaştırması.
- Şiddetin gerçekçi veya rahatsız edici bir tona kayması; korsan çatışmaları karikatürize kalmalı.

## 3. Oyun döngüsü

### Bir seferin akışı

1. **Karada hazırlık:** Oyuncular limanda hedef rotayı seçer, yolcu listesini inceler, erzak alır ve gemi yükseltmelerini seçer.
2. **Yolcu kabulü:** Yolcular gemiye alınır; bilet, kapasite ve özel ihtiyaçlar kontrol edilir.
3. **Seyir:** Gemi hedef limana ilerler. Oyuncular rollerini üstlenir ve geminin hizmetlerini sürdürür.
4. **Olaylar:** Arızalar, talepler, kaçak yolcular, korsanlar ve diğer olaylar belirli aralıklarla veya koşullara bağlı olarak ortaya çıkar.
5. **Varış ve değerlendirme:** Yolcuların memnuniyeti, gecikme, hasar, kayıp eşya ve hizmet kalitesi hesaplanır.
6. **Ödeme:** Temel ücret, bonuslar ve cezalar net geliri oluşturur.
7. **Gelişim:** Kazançla gemi modülleri, kapasite, hizmet kalitesi, silahlar ve kozmetikler açılır.
8. **Yeni rota:** Daha uzak, daha kazançlı ve daha riskli limanlar açılır.

### Ana kaynaklar

- **Para:** Gemi ve hizmet geliştirmelerinin ana kaynağı.
- **Gemi sağlığı:** Gövde, motor, elektrik ve su sistemlerinin toplam dayanıklılığı.
- **Yolcu memnuniyeti:** Yolcu davranışlarını, bahşişleri ve sefer sonu geliri etkiler.
- **Erzak:** Yiyecek, içecek, tamir malzemesi ve mühimmat gibi tüketilebilirler.
- **İtibar:** Daha iyi yolcuları, rotaları ve kara etkinliklerini açan uzun vadeli değer.

### Kazanç formülü — ilk prototip için

`Net gelir = bilet geliri + hizmet geliri + memnuniyet bonusu + olay bonusu - hasar/ceza - erzak maliyeti`

İlk sürümde formül oyuncuya okunabilir olmalı; gizli katsayılar minimumda tutulmalıdır.

## 4. Oyuncular ve roller

Roller sınıf kilidi değildir. Oyuncular istedikleri zaman görev değiştirebilir; ancak görev değişimi zaman ve koordinasyon maliyeti yaratır. Gemide her rol için birincil istasyon bulunur ve görevler istasyon dışındaki küçük etkileşimlerle desteklenir.

### 4.1 Kaptan / Dümenci

**Amaç:** Gemiyi rotada ve güvenli hızda tutmak.

**Temel işler:**

- Dümen ve hız kontrolü.
- Haritada hedef limanı takip etmek.
- Sığ alan, kayalık, fırtına ve yüzen engellerden kaçınmak.
- Gerekirse demirlemek veya yanaşma manevrası yapmak.
- Diğer oyuncuların verdiği rota ve öncelik çağrılarını uygulamak.

**Mini mekanikler:** Görüş kontrolü, kısa rota seçimi, hız–hasar dengesi, yanaşma hizalaması.

**İyileştirmeler:** Daha güçlü motor, daha hızlı dümen, daha iyi harita, sis görüşü, otomatik dengeleyici.

### 4.2 Purser / Servis sorumlusu

**Amaç:** Yolcuların temel ihtiyaçlarını karşılamak ve geliri yükseltmek.

**Temel işler:**

- Yiyecek ve içecek hazırlamak/servis etmek.
- Bilet ve ödeme kontrolü yapmak.
- Yolcu taleplerini, koltuk/alan şikâyetlerini ve özel ihtiyaçları yönetmek.
- Kirlenen veya dağılan alanları temizlemek.
- Bahşiş ve hizmet bonuslarını toplamak.

**Mini mekanikler:** Sipariş önceliği, tepsi taşıma, sıcak/soğuk ürün dengesi, sabırsız yolcuları sakinleştirme.

**İyileştirmeler:** Daha büyük mutfak, hızlı servis tezgâhı, premium menü, otomatik ödeme cihazı, daha fazla yolcu alanı.

### 4.3 Mühendis / Teknisyen

**Amaç:** Geminin çalışır halde kalmasını sağlamak.

**Temel işler:**

- Elektrik kesintisi, su sızıntısı, motor harareti ve gövde deliğini onarmak.
- Sigorta, vana, pompa ve kablo panellerini yönetmek.
- Yangın, kaygan zemin ve devrilmiş eşyaları temizlemek.
- Gerekirse yedek parça üretmek veya depodan almak.

**Mini mekanikler:** Doğru vanayı bulma, kablo eşleştirme, pompa ritmi, yangın söndürme ve hızlı yama.

**İyileştirmeler:** Daha büyük parça deposu, yedek sistemler, dayanıklı borular, hızlı tamir kiti, alarm paneli.

### 4.4 Topçu / Güvenlik sorumlusu

**Amaç:** Gemiyi korsanlardan ve dış tehditlerden korumak.

**Temel işler:**

- Korsan gemilerini tespit etmek ve top kullanmak.
- Düşmanların yaklaşma yönünü mürettebata bildirmek.
- Mühimmat yüklemek ve hedef önceliği belirlemek.
- Yolcuları güvenli bölgeye yönlendirmek.
- Saldırı sırasında gemide çıkan ikincil sorunlara yardım etmek.

**Mini mekanikler:** Nişan alma, zamanlama, mühimmat seçimi, karşı ateşten kaçınma, uyarı atışı.

**İyileştirmeler:** Daha hızlı doldurma, farklı gülleler, ikinci top, dürbün, savunma bariyeri.

## 5. Yolcular

Yolcular, gelirin kaynağı olmanın yanında geminin hareketli hedefleri, olay tetikleyicileri ve komedi üreticileridir.

### Yolcu özellikleri

- Hedef liman.
- Bilet sınıfı.
- Memnuniyet seviyesi.
- Sabır seviyesi.
- İhtiyaçlar ve alerjiler.
- Kişilik etiketi: zengin, huysuz, meraklı, korkak, sarhoş, korsan hayranı, pazarlıkçı vb.
- Gizli durum: kaçak yolcu, kötü niyetli sızmacı, ünlü kişi, denetçi, sahte biletli yolcu vb.

### Yolcu davranışları

Yolcular gemide dolaşır, hizmet ister, manzarayı izler, kavga çıkarabilir, yanlış kapıyı açabilir, panikleyebilir veya başka yolcuları etkileyebilir. Davranışlar “simülasyon” için değil, oyunculara net ve komik karar anları üretmek için tasarlanmalıdır.

### Yolcu memnuniyeti

Memnuniyet; bekleme süresi, hizmet kalitesi, güvenlik, gemi temizliği, rota gecikmesi ve olayların yönetimine göre değişir. Memnuniyetin sıfırlanması oyunu anında bitirmez; bahşiş kaybı, şikâyet, kargaşa veya bazı yolcuların gemiyi terk etmesi gibi sonuçlar doğurur.

## 6. Olay sistemi

Olaylar, oyunculara “doğru cevabı bul” sorusu değil, sınırlı zaman ve kaynakla öncelik belirleme problemi sunar.

### Olay kategorileri

- **Teknik:** Su kaçağı, elektrik arızası, motor harareti, yangın, dümen kilitlenmesi.
- **Sosyal:** Kavga, kayıp çocuk, sahte bilet, aşırı sarhoş yolcu, yolcu şikâyeti.
- **Güvenlik:** Korsan saldırısı, kaçak yolcu, sabotajcı, hırsızlık, karantina riski.
- **Çevresel:** Fırtına, yoğun sis, dalga, yüzen konteyner, deniz canlısı sürüsü.
- **Fırsat:** Acil yardım çağrısı, denizde sürüklenen sandık, VIP yolcu, yasadışı ama kazançlı teslimat.

### Olay tasarım ilkeleri

- Olayın ne olduğu ve aciliyeti ilk bakışta anlaşılmalı.
- En az iki makul çözüm bulunmalı.
- Çözüm, tek bir rolün görevini diğerlerinden daha değerli yapmamalı.
- Başarısızlık kademeli olmalı: para kaybı, gecikme, hasar, kötü itibar.
- Oyuncular fiziksel etkileşimlerle doğaçlama çözümler deneyebilmeli.

### Örnek olaylar

| Olay | Temel çözüm | Alternatif/komik çözüm | Başarısızlık |
|---|---|---|---|
| Gövdede delik | Mühendis yama yapar | Yolcu valiziyle deliği kapatma | Su seviyesi ve hasar artar |
| Sahte biletli yolcu | Servis sorumlusu bileti kontrol eder | Yolcuyu güverteye “misafir” olarak çalıştırma | Gelir ve itibar kaybı |
| Korsan baskını | Topçu ateş açar | Yolcuları bağırarak korkutup korsanları vazgeçirme | Kargo/para kaybı |
| Elektrik kesintisi | Sigortaları düzeltme | Mum satıp ek gelir elde etme | Hizmetler geçici durur |
| Parasız oturan yolcu | Ücret talep etme | Yolcuya güverte temizletme | Memnuniyet veya itibar kaybı |
| Kötü niyetli NPC | Davranış ipuçlarını takip etme | Onu yanlışlıkla VIP sanma | Sabotaj ve ek olaylar |

## 7. Komedi ve oyuncu özgürlüğü

Oyun, oyuncuların “sistemin izin verdiği” eylemleri birleştirerek beklenmedik çözümler üretmesini teşvik eder. Komedi, yazılı şakalardan çok fizik, zamanlama, NPC tepkileri ve oyuncu iletişiminden doğmalıdır.

### Uygun komedi örnekleri

- Yolcuyu güvenli olmayan ama çizgi film tonunda bir şekilde güverteye çıkarmak/atmak.
- Yolcuyu kovmak yerine mürettebata geçici iş vermek.
- Ödemeyen yolcuyu yakalamak için küçük bir bilet kontrol kovalamacası.
- Korsanlara yiyecek vererek saldırıyı geciktirmek.
- Arızayı tamir etmek için yanlış parçayı takıp yeni bir arıza başlatmak.
- VIP yolcu ile sahte VIP yolcuyu karıştırmak.

Güvenlik sınırları: fiziksel komedi karikatürize olmalı; kan, gerçekçi işkence, ağır yaralanma veya ayrımcı mizah kullanılmamalıdır.

## 8. Karada yaşam ve meta oyun

Kara bölümü, seferler arasında seçim ve hazırlık hissini güçlendirir. İlk MVP’de küçük bir liman merkezi yeterlidir; tam açık dünya hedeflenmez.

### Liman merkezindeki aktiviteler

- Gemi yükseltme ve bakım.
- Erzak ve mühimmat alışverişi.
- Yeni rota ve yolcu sözleşmesi seçimi.
- Kozmetik dükkânı: gövde rengi, yelken, bayrak, top görünümü, mürettebat kıyafeti.
- Mini oyunlar: balıkçılık, halat bağlama, yükleme, pazarlık.
- NPC konuşmaları ve kısa görevler.
- Liman itibarına bağlı özel hizmetler.

### Meta ilerleme

Oyuncu tek bir ortak gemiyi geliştirir. İlerleme, oturum içi kaynakların yanında kalıcı açılımlarla desteklenir. Kozmetiklerin güce etkisi olmamalıdır; mekanik yükseltmeler ise seçenek yaratmalı, tek bir zorunlu “en iyi” yapı oluşturmamalıdır.

## 9. Geminin gelişimi

### Geliştirilebilir sistemler

- Gövde: dayanıklılık ve su alma toleransı.
- Motor: hız ve yakıt verimliliği.
- Dümen: manevra ve yanaşma kolaylığı.
- Mutfak: servis kapasitesi ve ürün çeşitliliği.
- Yolcu alanı: kapasite ve konfor.
- Teknik oda: tamir hızı ve yedek sistemler.
- Silah sistemi: menzil, doldurma hızı ve mühimmat seçenekleri.
- Depo: erzak ve yedek parça kapasitesi.

Her yükseltme yeni bir oyun seçeneği açmalı veya bir darboğazı azaltmalı. Yalnızca sayı yükselten ve oyuncunun kararını değiştirmeyen yükseltmeler sonraki fazlara bırakılmalıdır.

## 10. Rota ve sefer yapısı

İlk sürümde 3–5 liman ve dallanan bir rota haritası hedeflenir.

- Kısa rota: düşük gelir, düşük risk, öğretici.
- Ticaret rotası: daha fazla yük ve servis fırsatı.
- Fırtına rotası: yüksek risk, yüksek ödeme.
- Korsan suları: güvenlik olayları yoğun.
- Turistik rota: memnuniyet ve kozmetik bonusları.

Seferlerin süresi, olay yoğunluğu ve oyuncu becerisine göre ayarlanabilir olmalı. Oyuncu isterse hedefe hızlı ulaşmayı, isterse yolda fırsat kovalamayı seçer.

## 11. Başarı, başarısızlık ve tekrar oynanabilirlik

### Sefer başarısı

Sefer; hedef yolcuların çoğu güvenle ulaştığında, gemi tamamen kaybedilmediğinde ve işletme iflas etmediğinde tamamlanmış sayılır. Yüksek skor için tüm yolcuların memnuniyeti, hızlı varış, hasarsız gemi ve bonus olaylar değerlendirilir.

### Sefer başarısızlığı

- Gemi kritik hasar alır veya batar.
- Mürettebat temel sistemleri uzun süre çalıştıramaz.
- Oyuncuların ortak bütçesi bakım ve erzağı karşılayamaz.

Başarısızlıkta tüm ilerleme silinmemeli. Oyuncu bir miktar para, itibar veya ekipman kaybedebilir; ancak yeni bir deneme için yeterli başlangıç kapasitesi korunmalıdır.

### Tekrar oynanabilirlik araçları

- Rastgele olay desteleri.
- Farklı yolcu kişilik kombinasyonları.
- Değişen hava ve rota koşulları.
- Farklı gemi kurulumları.
- İsteğe bağlı zorluk değiştiricileri.
- Kozmetik koleksiyonları ve komik başarımlar.

## 12. Kullanıcı deneyimi ve erişilebilirlik

- Her rolün mevcut işi, aciliyeti ve hedefi ekranda açıkça görünmeli.
- Renk tek başına bilgi taşımamalı; ikon ve metinle desteklenmeli.
- Gamepad ile tüm kritik işlemler yapılabilmeli.
- Yazı boyutu, altyazı ve kamera sarsıntısı ayarları bulunmalı.
- Oyuncu düşme/yeniden doğma, erişilemeyen alana sıkışma ve soft-lock durumlarından kurtarılabilmeli.
- Rol önerisi sunulabilir; rol zorunlu olarak kilitlenmemeli.

## 13. Teknik ve çok oyunculu çerçeve

Mevcut Unity projesi çok oyunculu prototipleme temeli üzerine kuruludur. İlk teknik hedef, 1–4 oyuncu arasında ortak dünya durumunun güvenilir şekilde paylaşılmasıdır.

### Ağda otoriter olması gereken sistemler

- Para, bilet ve yolcu durumları.
- Gemi sağlığı ve arızalar.
- Olayların üretimi ve sonuçları.
- Korsanlar, mermiler ve hasar.
- Sefer sonucu ve kalıcı yükseltmeler.

### İlk prototipte ağ kapsamı

- Oyuncu hareketi ve etkileşim.
- Dümen, servis tezgâhı, tamir paneli ve top istasyonu.
- Ortak gemi durumu.
- Basit yolcu NPC’leri.
- Tek rota, tek rastgele olay ve sefer sonu ödeme.

## 14. MVP kapsamı

MVP’nin amacı “dört oyuncu bir gemide rol paylaşarak bir limana gider, yolculara hizmet verir ve para kazanır” deneyimini kanıtlamaktır.

### MVP’de olacaklar

- 1 oynanabilir gemi.
- 1 başlangıç limanı ve 1 hedef liman.
- 1–4 oyuncu bağlantısı.
- Dört temel rol istasyonu.
- 6–8 yolcu ve basit bilet sistemi.
- Yiyecek/içecek servisi.
- Basit yolcu memnuniyeti.
- Elektrik veya su sızıntısı gibi en az 2 arıza tipi.
- Basit korsan saldırısı ve top istasyonu.
- Sefer sonu gelir ekranı.
- En az 3 gemi yükseltmesi.
- Bir temel kozmetik seçimi.

### MVP dışında bırakılacaklar

- Büyük açık dünya kara bölgesi.
- Çok sayıda liman ve hikâye görevi.
- Derin NPC kişilik simülasyonu.
- Karmaşık ekonomi ve ticaret.
- İleri düzey envanter, crafting ve görev zincirleri.
- Çok sayıda gemi sınıfı.

## 15. Başarı ölçütleri

İlk oynanabilir testte şu sorulara olumlu cevap vermeliyiz:

1. Dört oyuncu, en az üç rolü aynı anda anlamlı biçimde kullanıyor mu?
2. Oyuncular görevleri birbirine söylemeden de sistemin aciliyetini anlayabiliyor mu?
3. Bir arıza çıktığında iletişim ve önceliklendirme gerekiyor mu?
4. Sefer sonunda kazanılan para, bir sonraki yükseltmeyi isteme motivasyonu yaratıyor mu?
5. Başarısızlık oyuncuları güldürüyor ve yeniden denemeye teşvik ediyor mu?
6. 15 dakikalık bir sefer, tekrar oynandığında farklı hissettiriyor mu?

## 16. Açık tasarım soruları

- Oyuncular aynı karakterle mi oynayacak, yoksa kozmetik mürettebat karakterleri mi olacak?
- Gemi yok olduğunda sefer anında mı bitmeli, yoksa kurtarma mini oyunu mu olmalı?
- Geminin maksimum boyutu fiziksel olarak büyüyecek mi, yoksa modüler kabinler mi eklenecek?
- Kara bölümü her sefer arasında kısa bir hub mı, yoksa ayrı oynanabilir bölüm mü olacak?
- Dost ateşi, yolcuları denize düşürme ve benzeri özgürlükler hangi sınırlar içinde kalacak?
- Tek seferde tüm oyuncuların aynı hedefe bağlı kalması mı, yoksa kişisel gizli hedeflerin eklenmesi mi daha iyi olacak?

Bu sorular MVP testlerinden sonra cevaplanmalıdır.
