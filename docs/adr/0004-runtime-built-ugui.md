# Arayüz sahneye serileştirilmez; kodla, çalışma anında uGUI olarak kurulur

Eski IMGUI (`OnGUI`) HUD'ı, üretim kalitesinde ölçeklenmeyen tek kutuya yığılmış bir debug katmanıydı. Yerine `PrototypeHud.Awake` içinde `HudController` ile kurulan bir uGUI canvas'ı geldi: ekran düzeni, tema ve widget'lar `Runtime/UI` altındaki kodda tanımlıdır, sahne ve prefab'lara hiçbir UI nesnesi kaydedilmez.

Sahne zaten editor builder scriptleriyle üretildiği ve sürekli yeniden üretildiği için UI'ı sahneye gömmek her builder çalıştırmasında çakışma ve elle bakım riski doğururdu; kodla kurulan canvas sahne uyumluluğunu bozmaz ve testlerde tek noktadan üretilebilir.

## Considered Options

- **Sahneye serileştirilmiş Canvas/prefab**: reddedildi; builder'larla çakışma ve görsel düzeni code review'da okunaksız kılması.
- **UI Toolkit**: denenmedi; WebGL ve ugui paketi zaten projede, ek bir UI sistemi öğrenme/test maliyeti getirirdi.

## Consequences

- Görsel iterasyon Editor'de sürükleyip bırakarak değil kodla yapılır; düzeni doğrulamak için `SHIPMAYHAM_CAPTURE=1` ile ekran görüntüsü testi kullanılır ([HUD.md](../HUD.md)).
- Yazılar legacy `UnityEngine.UI.Text` ile çizilir (TextMeshPro içe aktarımı gerekmez); yerelleştirme veya zengin metin ihtiyacı doğarsa bu karar yeniden değerlendirilmelidir.
- `ShipMayham.Runtime.asmdef` `UnityEngine.UI` paketine bağlıdır.
