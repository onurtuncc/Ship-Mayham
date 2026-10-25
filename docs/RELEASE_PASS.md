# Yayın öncesi geçiş (25 Ekim 2026 gecesi)

Bu belge, prototipi oynanabilir ve yayın içeriği üretebilir hâle getiren geçişi özetler: ne eklendi, hangi sayılar ilk deneme değeri, neyin elle kabul edilmesi gerekir. Kod ayrıntısı [ARCHITECTURE.md](ARCHITECTURE.md) içindedir.

## Düzeltilen hatalar

- **Işınlanmalar kalktı.** Yolcular iskeleden sıraya girip kapıdan geçerek koltuklarına yürür (`Boarding`) ve sefer bitince aynı yoldan iner (`Leaving`); kalkış herkes oturana, yeni sefer herkes inene kadar bekler. Atılan, bırakılan ve kurtarılan yolcular yatar, sersemler ve koltuklarına yürür. Gemi–kara geçişi düğme değil, kapıya yürümektir ve kayarak yapılır.
- **Kapalı koltuklar.** Kapasite yükseltmesiyle açılmamış koltuklar ne çarpışır, ne sahiplenilir (bırakılan yolcu yalnız açık koltuklardan en yakın boşa oturur).
- **Su içme.** Eski 15 sn'lik klip yolcuyu kaldırıp 2,8 m yürütüyordu; yerini yerinde yudumlama klibi aldı.

## Yeni mekanikler

| Mekanik | Özet | Ayar sayıları |
| --- | --- | --- |
| Gangway kapısı | Eller boşken kapıya yürü | tetik 1 m, bekleme 0,8 sn |
| Yüzme | Halat merdivenle suya atla (limanda), yüz, batık para topla, merdivenle çık | hız %60, 30 sn limit (denizde), 5 coin ceza |
| Denize savrulma | Rogue wave, dümen/topta olmayan ve uzak korkuluğa 2,2 m içindeki mürettebatı denize atar | 2,2 m |
| Yüzen/yüzemeyen yolcu | %70 yüzer, kendi merdivenine yüzer (3–8 sn); diğerleri simitle çırpınır, 8 sn sonra çekilir | |
| Kilit açma | Gömülü sandık (2/liman) | 25–60 coin, temiz açılış +1 itibar, 3 kurtarma |
| Kazı | 4/liman | birkaç coin, %12 harita (+1 itibar) |
| Olta | İskele kenarı, 3 nokta, sınırsız | küçük balık +2, büyük +5 yiyecek, çizme +1 coin |
| Çekiç | Sızıntıya tahta çak (kit/vana yoksa) | 3 çivi, 3 ıska; başarısızlık 1 adım verir |
| Batık paralar | 8/liman, yüzerek | 5 coin; sekizinci +25 ve itibar |
| Kayıt / devam | Menüden solo veya online devam | yalnız limanda yazılır |

Yaptığını gösterme: oynayan oyuncu olta/kazma/kilit/çekiç pozunda, elinde aracıyla görünür.

## Elle kabul edilmesi gerekenler

Otomatik testler (694 test) kuralları, ağ durumunu, kaydı ve sahne bağlantılarını kapsar; şunlar insanlı denemeyle onaylanmalıdır:

1. Dört kişiyle yüzme, merdivenden çıkış ve rogue wave ile denize savrulma hissi; 30 sn limitin ve 5 coin cezasının doğru dengede olup olmadığı.
2. Her mini oyunun zorluğu ve süresi (özellikle olta ve kilit); gamepad ile oynanış.
3. Yolcu yürüyüşlerinin kalabalık koridorda takılmaması; kapının açılış zamanlaması.
4. Kilit açma ve çekiç sonuç sesleri henüz yok (yalnız efektler ve bildirim metinleri var).
5. Online devamda gerçek Relay ile yeniden katılma; kayıtlı dünyanın tarayıcıda (WebGL) yeniden açılması.
6. Yüzme, ladders ve kazı için sanat: modeller primitif/boyalı ilk sürümdür; animasyonlar Blender'da yazıldı, Mixamo ile değiştirilebilir.
