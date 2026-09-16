# Audio et fond musical — notes historiques d'intégration

Cette tranche possède AudioMixer.cs, AudioSurface.cs, AudioPolicy.cs,
Spectrum.cs, AudioEnvelope.cs et le rendu musical NativeBackground.
Raccordements ponctuels dans DesktopWorkspace, DesktopLayout, DeskSurface,
DesktopSettings, SettingsWindow et Native. La coordination Vidéo de ce chantier
est terminée ; ces notes ne donnent pas de consigne active de rechargement.
Build intermédiaire : build/battlestation-audio. La livraison complète est dans
build/battlestation-cockpit, avec -NoActivate. Voir [COCKPIT.md](COCKPIT.md)
pour le périmètre et les preuves historiques ; README.md décrit l'usage actuel.

Le mixer utilise NAudio 2.2.1 / Core Audio. Les échantillons de sortie restent
en mémoire pour le calcul du spectre ; aucun son ni contenu terminal enregistré.
Le microphone est seulement commandé par son état muet, jamais capturé.

Le changement de sortie utilise IPolicyConfig, interface Windows non documentée
également décrite par [EarTrumpet](https://github.com/File-New-Project/EarTrumpet/blob/master/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs).
Un échec doit rester visible. Les applications qui fixent leur propre sortie
peuvent conserver cette sortie.
