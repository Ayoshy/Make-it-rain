# Bluetooth : connecter et déconnecter les appareils appairés

## Contrat courant

Les quatre favoris sont déjà appairés. Le dock présente deux états stables :
**Connecté** et **Déconnecté**. Une commande en cours a une indication temporaire ;
un état indisponible ne devient pas artificiellement « déconnecté ».

Le néon Vice City représente une connexion confirmée ; la déconnexion ramène
au gris nacré avec le même fondu que les applications ouvertes. Un repère ambre
identifie l'état indisponible. L'état du pilote Windows
n'est pas un indicateur de connexion. Les commandes ne changent ni l'appariement,
ni l'activation du pilote, ni l'adaptateur, ni le volume ou la sortie audio par défaut.

### Appareils audio : Buds, PARTYBTMS3 et soundbar

La commande utilise les propriétés Windows `KSPROPERTY_ONESHOT_RECONNECT` et
`KSPROPERTY_ONESHOT_DISCONNECT`, via `IKsControl`. Les interfaces audio (sortie et
microphone) sont regroupées par le `ContainerId` du périphérique Bluetooth exact.
Un simple nom ressemblant ne permet pas de cibler un appareil.

Le succès de la requête signifie seulement que Windows a tenté l'action.
Il faut ensuite confirmer la liaison Bluetooth et les interfaces audio.

### DualSense

Lecture SDP réelle du 15 septembre :

- `HIDReconnectInitiate` (0x0205) = vrai.
- `HIDNormallyConnectable` (0x020D) = faux.

Cette manette initie donc sa reconnexion ; le PC ne doit pas prétendre pouvoir
la réveiller/reconnecter. Déconnectée, le dock indique **Appuyez sur PS pour
connecter la manette** et attend que Windows confirme la connexion.
La déconnexion ciblée utilise `IOCTL_BTH_DISCONNECT_DEVICE`, sans désactivation
du pilote ni modification de la radio globale.

## Diagnostic matériel avant implémentation

Buds, initialement connectés :

1. Commandes de déconnexion sur les deux interfaces audio : retours `S_OK`.
2. Sortie et microphone : `DEVICE_STATE_ACTIVE` (1) → `DEVICE_STATE_UNPLUGGED` (8).
3. Liaison Bluetooth `fConnected` : vrai → faux.
4. Reconnexion : retours `S_OK`, deux interfaces 8 → 1, `fConnected` faux → vrai.
5. Appariement conservé, pilote toujours activé. Soundbar et DualSense restent
   connectées ; PARTYBTMS3 reste déconnecté.

L'essai a été effectué sans élévation. Une seconde preuve a vérifié la commande
de déconnexion radio ciblée sur les Buds, puis les a reconnectés via les commandes
audio. Le test spécifique de la DualSense exige une présence humaine pour la
restauration avec PS ; il reste distinct de cette preuve du mécanisme commun.

Les sources et constats de diagnostic sont dans
`artifacts/validation/bluetooth-connection/` : `AudioProbe.cpp`,
`audio.disconnected.txt`, `audio.restored.txt`, `radio.disconnected.txt`,
`radio.restored.txt`, `DisconnectProbe.cpp`, `radio-disconnect-proof.txt`,
`radio-disconnect-restored.txt`, `HidSdpProbe.cpp`, `hid-sdp-proof.txt`.
Ce sont des preuves datées, pas des sources d'état live.

## Pourquoi le précédent correctif ne répondait pas à cet usage

Le précédent bouton appelait ConfigMgr pour désactiver/réactiver un nœud Windows.
Le refus 0x33 avait réellement été corrigé, mais cette commande ne correspondait
pas à la connexion Bluetooth attendue. Le premier test sur une manette déjà
radio-déconnectée ne pouvait pas démontrer une coupure/reprise audio.
Ce chemin et son élévation ne doivent plus servir au dock.
Le compte rendu précédent est conservé dans les preuves locales sous
`BLUETOOTH.previous.md`.

## Références

- [Reconnexion audio Windows](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/ksproperty-oneshot-reconnect)
- [Déconnexion audio Windows](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/ksproperty-oneshot-disconnect)
- [Déconnexion Bluetooth ciblée](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/bthioctl/ni-bthioctl-ioctl_bth_disconnect_device)
- [États connexion et appariement](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/ns-bluetoothapis-bluetooth_device_info_struct)
- [Spécification de test HID, reconnexion initiée par hôte ou appareil](https://files.bluetooth.com/wp-content/uploads/2024/10/HID11.TS_.p14.pdf)
- [Identifiants des attributs SDP](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Assigned_Numbers/out/en/index-en.html)
- [Explication de la navigation Core Audio vers le filtre Bluetooth](https://github.com/m2jean/ToothTray)

## Audition et DualSense : confirmations réelles

L'utilisateur confirme que le son du PC était diffusé dans les Buds. Après un
aller-retour de déconnexion/reconnexion, il confirme : « Oui, coupure puis retour
du son ». Les états Windows de cet essai sont dans `audible-roundtrip.txt`.

Avec l'utilisateur disponible pour restaurer la connexion, la commande radio
ciblée a ensuite déconnecté la DualSense : retour 0, `fConnected=false`, pilote
et appariement conservés. L'utilisateur a appuyé sur PS ; une nouvelle lecture
Windows a confirmé `fConnected=true`. Les Buds et la soundbar sont restés
connectés, PARTYBTMS3 est resté déconnecté. Les preuves sont
`dualsense-disconnect-proof.txt`, `dualsense-disconnected.txt` et
`dualsense-restored.txt`.

Ces essais prouvent les mécanismes natifs. L'acceptation du build intégré et
le déclenchement par le vrai clic de dock restent des contrôles séparés.

## Skins et disposition

Les silhouettes sont maintenant des SVG/PNG originaux de boombox horizontale,
Buds3 Pro, DualSense et soundbar. `scripts/Build-BluetoothIcons.py` reprend
exactement les dégradés des icônes d'applications : nacré à l'arrêt et palette
Vice City/halo en activité. Aucun fichier d'icône d'application n'est modifié.
La transition de matériau dure 260 ms, avec la même interpolation que le dock
applications. Le néon apparaît après connexion confirmée et disparaît après
une déconnexion confirmée. L'état inconnu conserve un repère ambre distinct.

En mode Réorganiser, le redimensionnement choisit parmi 4 × 1, 2 × 2 et 1 × 4
pour garder les quatre appareils visibles avec le plus de place possible.
La taille minimale est 128 × 128. Des rendus de contrôle existent pour
512 × 128, 128 × 512, 440 × 288 et 128 × 128 dans
`artifacts/validation/dock-review/`. Ce sont des rendus WPF isolés, pas des
captures du bureau.

## Intégration et vérifications

- Commandes natives intégrées dans **Battlestation.Desk.dll**, sans nouvel
  exécutable helper, service ou demande d'élévation.
- Ancien `SetEnabled`, commandes CM_Enable/Disable et mode élevé one-shot retirés.
- Nouveau service `SetConnected` testé avec le code source réel et la DLL
  intégrée : déconnexion/reconnexion des Buds, deux interfaces audio et liaison
  radio confirmées. Voir `integrated-roundtrip.txt`.
- Tests Bluetooth : validation de cible et d'identité physique, résultats honnêtes.
- Tests WPF : matériau néon/gris et son fondu, tooltip sans mouvement, erreurs
  conservées après lecture Windows, quatre hits dans chaque disposition, aucune
  intersection entre les zones, dimensions du verre et icônes d'applications.
- Compilation native et publication .NET réussies. Le build activé est
  `build/battlestation-bluetooth-connect-neon`, PID 21252 au contrôle.
- Jeton du bureau non élevé ; disposition identique à celle capturée immédiatement
  avant rechargement ; les deux sessions terminal présentes à cet instant sont
  conservées dans l'hôte 13472. Les autres sessions de captures plus anciennes
  avaient évolué avant ce rechargement ; aucune ancienne capture n'a été réappliquée.
- Des changements vidéo parallèles ont été constatés et laissés intacts. Ils ne
  constituent pas une modification de cette tranche Bluetooth.

Commandes :

```powershell
dotnet run --project tests/Battlestation.Bluetooth.Tests.csproj -c Release
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -c Release -- . --bluetooth-read-only
.\scripts\Build-Battlestation.ps1 -OutputDirectory build/battlestation-bluetooth-connect-neon -NoActivate
```

Computer Use reste indisponible (`native pipe`, erreur Windows 2). L'essai de
clic dans le dock est donc réalisé par l'utilisateur avec observation des états
Windows. Le démarrage Windows n'a pas été basculé dans cette tranche.

## Bilan Eclipse

- Principal : diagnostic Windows, preuves matérielles et audit de la restauration,
  revue du code natif/managé, correction du contrat, création des silhouettes,
  matériaux existants et disposition adaptative, finitions UI, intégration et
  coordination de la validation avec l'utilisateur.
- Worker `gpt-5.6-luna`, effort `xhigh` demandés : service de connexion, exports
  natifs, états fonctionnels de la surface et adaptation des tests. Paramètres
  effectifs non confirmés par les métadonnées du lanceur.
- Un retour précis a corrigé le tampon de support KS, les compatibilités avec
  l'ancien modèle, les erreurs de scan et les confirmations après échec. Le
  principal a repris l'intégration finale et la nouvelle tranche visuelle.
- Tokens/coûts par modèle et total indisponibles. Économie : **non mesurable**.

### Validation utilisateur du nouveau dock

Après activation, l'utilisateur a effectué l'aller-retour depuis l'icône des
Buds et a confirmé : « Oui : coupure/gris puis son/néon ».
Le déclenchement par clic réel, la coupure/reprise audible et le suivi du matériau
visuel sont donc validés ensemble. Une lecture Windows finale confirme les Buds
connectés, toujours appairés et leur pilote activé.

La précédente surveillance passive s'était terminée avant cette confirmation ;
elle n'est pas présentée comme ayant capturé l'aller-retour. La preuve du clic
et du changement visuel est le compte rendu direct de l'utilisateur, complété
par la lecture finale `user-confirmed-final-buds.txt`.
