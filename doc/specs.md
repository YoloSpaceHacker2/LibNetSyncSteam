# YSHNetSyncLib


## Multi platform

Windows/linux
Csharp
Based on Steam library
Thread safe
Can be used in Unity or Winforms projects


La cible est unity+librairie csharp pour le networking+steam dll. 

Sous windows, et unix. Je commence par développer la lib réseau seule. 

Lors de l'init de la librairie, un paramètre Init(Steam/Stub) permet de tester les fonctionnalisés avec un stub de la librairie Steam pour tester en local

V0: Stub only 
V1: Steam Integration via Steamworks.NET
V2: Integration Unity


### Steam
- init Steam 
- SteamId 
- Friend list 
- FriendStatus(online, in game which game ?)

### Lobby
- CreateLobby 
- Accepter ou refuser une demande de join 
- JoinLobby d'un amis, être notifié de la réponse
 
## Messages
- transmettre des messages vers le lobby 
- recevoir des messages du lobby
- les messages peuvent être reliable ou non
- info du ping moyen 

## Synchro 
- Instancier une classe ObjNetSync 
- Liste des objets de type ObjNetSync
- Cette classe est automatiquement synchronisée avec les autres participants 
- Chaque instance a sa propre fréquence de synchro

