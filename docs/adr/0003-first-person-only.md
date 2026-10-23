# Oyun birinci şahıs oynanır; tek istisna dümendeki chase kamera

GDD kamerayı birinci şahıs olarak tanımlar: bakış yönü hareket yönünü belirler, etkileşim crosshair'in baktığı nesneye yapılır ve oyuncular birbirini güvertede görür. Üçüncü şahıs veya yukarıdan kamera yerine bu seçilmiştir; etkileşim seçimi, yolcu taşıma/fırlatma, top nişanı ve HUD tasarımı bu varsayıma dayanır.

Tek istisna Helm'dedir: gemiyi süren Crew member, tercihine bağlı olarak yükseltilmiş takip (chase) kamerası kullanabilir (`C` / `Y`). Tercih tarayıcı/cihazda saklanır.

## Consequences

- Etkileşim hedefi, kameranın baktığı açıya göre seçilir (`PrototypeView.FindTarget`); yeni bir interactable eklerken hem menzil hem bakış açısı koşulu düşünülmelidir.
- Crosshair ve Prompt kartı yalnızca fare kilitliyken ve chase kamera kapalıyken anlamlıdır; HUD bunu kendisi gizler.
- WebGL'de pointer lock kullanıcı hareketi gerektirir; bağlantı kurulunca otomatik kilit istenmez, oyuncu merkeze tıklar.
