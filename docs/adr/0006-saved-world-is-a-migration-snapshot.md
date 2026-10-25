# ADR-0006: Kayıtlı dünya, host migration anlık görüntüsüdür

## Durum

Kabul edildi (25 Ekim 2026).

## Bağlam

Host'un son dünyası cihazda saklanıp menüden devam edilebilmeli ve arkadaşlar yeniden katılabilmeli. Oyunun zaten host düşünce tüm durumu taşıyan bir host migration anlık görüntüsü var (`ExportMigrationData`).

## Karar

- Kayıt, migration anlık görüntüsünün gzip + base64 hâlidir (PlayerPrefs; WebGL'de IndexedDB), küçük bir özet zarfı (host adı, sefer, coin, itibar, zaman) ile.
- Yalnız limanda (Boarding, Arrived, Failed) ve lobi dışında yazılır; denizdeki sefer kaydedilmez: devam, son limandan başlar. Böylece sefer ortasındaki olay, korsan, yüzen veya mini oyundaki oyuncular gibi geçici durumların hiçbiri kayda girmez.
- Devam ederken anlık görüntü, bekleyen migration verisi gibi yüklenir. `ReadyForNewParty` bağlı oyuncusu olmayan slotları boşaltır: solo devamda hepsi, online devamda kimliği (`PlayerId`) olmayanlar. Kimlikli oyuncular yeniden bağlanınca kendi slotlarını, konumlarını ve sayaçlarını geri alır; lobi bağlı olmayan slotları beklemez.
- Aynı anda tek kayıt vardır; yeni dünya onu değiştirir. Batch çalıştırmalar gerçek kayda dokunmaz.

## Sonuçlar

Yeni oturum durumu eklemek, anlık görüntüye eklemek kadar kayda da eklemek demektir (ek iş yoktur). Anlık görüntü sürümü değişirse eski kayıtlar sessizce yok sayılır (zarf `FormatVersion` ve anlık görüntü sürümü): oyuncu yeni dünya başlatır. Kayıtlı hâlin bozuk veya fazla büyük olması (`MaxPayloadBytes`) reddedilir.
