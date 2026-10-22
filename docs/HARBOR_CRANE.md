# Liman vinci — dilim 19

PORT OFFICE'te **CRANE LOADING** seçilir. Ekip FISH MARKET'ten üç yük paketini alıp **LOAD PAD**'e taşır; pad iki paket alır. Bir oyuncu **CRANE CONTROLS** başında E / A ile operatör olur. Diğerleri taşımaya devam edebilir. İş sefer öncesi veya varış limanında yapılabilir.

- **WASD / sol çubuk:** kancayı kasabanın kuzey-yukarı eksenlerinde hareket ettirir.
- **Space / X:** boş kanca pad üstündeyken yük alır; yüklüyken indirir.
- **E / A:** kontrolleri bırakır. Yük kancada kalır; başka operatör devralabilir.

Operatör, pad ve açık denizdeki mavnayı gören yüksek iş kamerasına geçer; serbest bakış korunur. Yüklü kanca hafif salınır. Mavnanın yeşil hedefi içinde indirmek teslimattır. Yanlış indirmede güvenlik ekibi paketi pad'e, pad doluysa pazara geri koyar; para veya kaynak kaybolmaz. Bu sunucu kurallı bir mini oyundur, gerçek Rigidbody/halat fiziği simülasyonu değildir.

Üç teslimat **45 ortak para +1 itibar** verir, operatör serbest kalır. Liman ziyaretindeki tek tamamlanmış iş sınırı diğer liman işleriyle ortaktır. İade noktaları FISH MARKET ve FOOD'dur. Elde veya kancada yük varken iptal edilemez; iptal hazır pad yüklerini temizler, kısmi ödül vermez.

Aktif/tamamlanmış işte korunum: **pazar + pad + eller + kanca + teslim edilen = 3**. Geçici/kalıcı ayrılan taşıyıcının paketi pazara döner. Operatör ayrılınca kancadaki yük korunur. Kanca, salınım saati, stoklar ve `AtCrane` rolü snapshot/ağ durumundadır; protokol ve snapshot **9**. Kalıcı kariyer kaydına geçici vinç durumu yazılmaz.

## Doğrulama

`Logs/HarborCrane-Focused.xml`: **7/7**, Unity çıkışı 0. Paralel tedarik/pad kapasitesi, operatör kilidi ve kanca hareketi, tek ödül, yanlış indirme/iptal, snapshot + ayrılan operatör sonrası devir, uzak/yanlış/kapalı eylemler ve gerçek sahne/RPC taşıma/kamera/indirme kapsandı. Bu testteki RPC sahnesi solo host'tur; internet üzerinden iki insanla vinç testi değildir. Eski sefer/Relay paketleri tekrar çalıştırılmadı.

İlk Windows build çıkışı 0 (`Logs/HarborCrane-WindowsBuild.log`). Kamera açısı/dünya kasa ölçeği düzenlemesi çıkışı 0 (`Logs/HarborCrane-Final-WindowsBuild.log`); en son, mavnayı vinç/ağaç arkasında bırakmayan yüksek iş kamerasıyla build çıkışı **0** (`Logs/HarborCrane-WorkCamera-WindowsBuild.log`). `Logs/HarborCrane-Layout.png` editör Camera.Render önizlemesidir; canlı oynanış görüntüsü değildir. Son görsel düzenlemeden sonra yedi test tekrar çalıştırılmadı.

Canlı web yayını şimdilik dilim 18/v8 `fa132b5`'tir; yeni vinç Windows v9 ile aynı oturuma giremez. Web paketi sonraki toplu kara yayınıyla güncellenecektir. Ağ gecikmesinde hedef hissi, farklı cihazlar ve ekip dengesi için insanlı kabul açıktır.
