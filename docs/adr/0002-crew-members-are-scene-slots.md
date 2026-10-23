# Crew member'lar NGO player prefab'ı değil, sahnedeki sabit dört slottur

Netcode, bağlanan her istemci için player nesnesi üretmez (`PlayerPrefab = null`, `CreatePlayerObject = false`). Gemideki dört Crew member, sahnede hazır duran slotlardır; slot, Relay oyuncu kimliğine (Authentication `PlayerId`) bağlanır ve `VoyageSimulation` içinde `CrewState` olarak yaşar.

Bunun nedeni kimliğin bağlantıdan bağımsız kalmasıdır: kopan veya host migration sırasında yeniden bağlanan bir Player aynı slotuna, elindeki eşya ve konumuyla geri döner; ağ nesnesi yok olup yeniden doğmaz. Slot sayısı sabit (`MaxCrew = 4`) olduğundan sahne ve simülasyon aynı diziyi paylaşır.

## Consequences

- Yeni bir Crew member görseli veya davranışı eklemek için prefab spawn akışı değil, sahnedeki slot referansları ve `CrewState` kullanılır.
- Slotu boşaltma kararı Relay oturum üyeliğine göre verilir (`RemoveOnlinePlayer`); gecikmeli Lobby olayları güncel üyelik listesiyle uzlaştırılmadan slot silinmez.
- Aynı anda dörtten fazla Player desteklenmez; beşincisi bağlantı onayında reddedilir.
