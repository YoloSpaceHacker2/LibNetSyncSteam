# Archi de dev/test
- appli csharp en mode texte
- script sans interaction utisateur 
- host sous windows 11: compte steam 'testwin01'
- VM sous ubuntu avec un compte steam 'testlinux01'.
- VM sous ubuntu avec un compte steam 'testlinux02'.


Les tests peuvent se faire sur l'archi cible avec steam Init(steam), ou sur le host en lançant 3 process en mode Init(Stub).


## Test 01
- 'testwin01' lance le jeu. 
    - 1 ObjSyncPlayerPos est instancié
    - 10 ObjSyncMobs sont instanciés 
- 'testlinux01', lance le jeu. 
    - Obtient la liste de ses amis.
    - Sait s'ils ont le jeu.
    - Voit que 'testwin01' est en jeu.
    - Demander à rejoindre la partie en cours
- 'testwin01' est notifié, et peut accepter ou refuser. 
    - Il accepte automatiquement. 
- 'testlinux01' est notifié que la demande est acceptée. 
    - A la connection une Synchro est faite avec création des objects avec leurs valeurs
    - 1 ObjSyncPlayerPos est instancié par 'testlinux01'
    - Ensuite les modifications de part et d'autre sont envoyées.
- Au bout de 30s, le client se déconnecte. Le serveur est notifié. 
    - Les instance client sont taguées 'déconnecté. 
    - Le serveur termine le programme



## Test 02
- Idem test01 avec 'testlinux02' en iso de 'testlinux01'