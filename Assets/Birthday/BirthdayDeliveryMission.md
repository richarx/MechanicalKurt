# Livraison d'anniversaire

Dans `MainCityScene`, le composant `BirthdayDeliveryMission` est sur `Birthday`.

- `Birthday/StartMissionBirthday` : nouveau départ près du personnage du tutoriel de la poste. Une copie du personnage et de sa caméra joue le briefing du message spécial. Déplacer cette racine pour déplacer ensemble le cercle, le personnage et le cadrage.
- `Birthday/EndMissionBirthday` : destination existante. Sa `MissionStartZone` dépend du départ ; l'ancien déclenchement local `PigeonStopZone` est désactivé.
- Aux deux étapes, chaque joueur doit être immobile dans le rayon de 3 mètres et valider avec F / Y. La zone attend tout le monde, puis lance un décompte de 3 secondes, annulable avec F / Y.
- Chacun voit le briefing sur son écran. Quand tout le monde l'a terminé, les pigeons sont libérés et la flèche indique l'arrivée. La seconde synchronisation lance la cinématique d'anniversaire existante chez tous les joueurs.
- Les pigeons restent gelés jusqu'à la fin de la cinématique chez tous les participants. Les délais de sécurité sont de 90 secondes au départ et 300 secondes à l'arrivée ; garder une marge si la durée des cinématiques change.
- La quête est jouable une fois par session de scène. Les autres missions de la poste sont conservées.

## Vérification en Play

1. Ouvrir la scène avec un hôte et un client. Aller d'abord à l'arrivée : aucune cinématique ni invite ne doit se déclencher.
2. Au nouveau départ, valider avec un seul joueur : attendre le second. Avec les deux, annuler pendant le décompte puis relancer.
3. Terminer le briefing sur un seul écran : les deux pigeons restent gelés. Terminer sur le second : la flèche pointe vers `EndMissionBirthday` et le vol reprend.
4. À l'arrivée, attendre que les deux joueurs soient prêts. Vérifier l'anniversaire sur les deux écrans, le retour des commandes à la fin et l'absence de relance en restant dans la zone.
5. Vérifier le placement du nouveau personnage, les cadrages et le tutoriel de la poste d'origine.

Vérifications effectuées : compilation des scripts avec les références du projet ; références, identifiants et liens de hiérarchie de la scène ; 13 vérifications en simulation isolée sur les vrais scripts de zone et de quête, avec deux pigeons simulés (verrouillage, attente des joueurs, managers occupés, cinématiques, annulation, guidage et fin unique). Cette simulation ne couvre pas le moteur Unity, les animations ni le transport réseau : le test en Play ci-dessus reste nécessaire.
