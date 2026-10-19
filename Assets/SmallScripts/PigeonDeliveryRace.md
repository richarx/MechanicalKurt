# Course de livraison avec PigeonStop

Ajouter `PigeonDeliveryRace` sur un GameObject `CourseLivraison` qui reste actif.
Le composant pilote le chrono, les deux zones, leurs repères, la flèche et les dialogues.
Il ne nécessite ni `DeliveryManager`, ni `DeliverySession`, ni l'ancien composant `DeliveryPoint`.

## Objets dans la scène

```text
CourseLivraison                   PigeonDeliveryRace
Depart                            PigeonStopZone + DialogCinematic
  RepereDepart                    Visuel facultatif, par exemple un cylindre
Arrivee                           PigeonStopZone + DialogCinematic
  RepereArrivee                   Visuel facultatif, par exemple un cylindre
Canvas
  ChronoLivraison                 TextMeshPro - Text (UI)
Main Camera
  CompassArrow_Prefab             DeliveryCompassArrow déjà configuré par le prefab
```

Garder `Depart`, `Arrivee`, les PNJ, les caméras et `CourseLivraison` actifs.
Le script désactive les **composants** PigeonStopZone ; il ne désactive pas leurs GameObjects.
Les champs Marker servent uniquement aux visuels à masquer. Ne pas y mettre un parent
du contrôleur de course, d'une cinématique, d'un PNJ animé ou d'une caméra.
Ne pas placer le contrôleur de course sur le texte du chrono, qui sera masqué.

Les PigeonStopZone détectent un pigeon arrêté dans leur rayon, sans collider nécessaire.
Régler Radius, Stop Speed Threshold et Stop Duration pour les deux zones. Affecter le
même PigeonController à leur champ Pigeon, ou laisser leur recherche automatique le trouver.

## Références du composant PigeonDeliveryRace

| Champ | Objet à glisser |
| --- | --- |
| Departure Stop | PigeonStopZone de Depart |
| Departure Cinematic | DialogCinematic de Depart |
| Departure Marker | RepereDepart, facultatif |
| Destination Stop | PigeonStopZone de Arrivee |
| Arrival Cinematic | DialogCinematic de Arrivee |
| Destination Marker | RepereArrivee, facultatif |
| Player | Transform du pigeon joueur |
| Duration Seconds | Temps autorisé, par exemple 60 |
| Timer Text | Texte TMP ChronoLivraison, dédié à cette course |
| Arrow | DeliveryCompassArrow du prefab CompassArrow_Prefab |
| Completion Achievement | Asset AchievementData à débloquer |

Sur chaque DialogCinematic : affecter Cinematic Manager, Character Animator et un
Dialogue non vide. L'Animator doit posséder les triggers **Talk** et **SendOff**.
Affecter Cinematic Camera si un plan spécifique est souhaité ; vide conserve la vue courante.
Garder un ManagerCinematic et un DialogManager configurés dans la scène.

Créer l'achievement via **Create > Achievements > Achievement** si nécessaire,
avec un identifiant unique, puis l'ajouter à la liste Achievements du **AchievementManager**
présent dans la scène. Sans cet enregistrement, le manager refuse de le débloquer.

## Deux branchements UnityEvent

1. `Depart / PigeonStopZone / On Player Stopped` : glisser CourseLivraison et choisir
   **PigeonDeliveryRace.StartDelivery()**.
2. `Arrivee / PigeonStopZone / On Player Stopped` : glisser CourseLivraison et choisir
   **PigeonDeliveryRace.CompleteDelivery()**.

Sur ces deux zones, retirer les anciens liens directs vers DialogCinematic.PlayCinematic
ou PigeonStopDialogCinematic.PlayCinematic, pour éviter un double lancement.
Ne pas brancher les fins de cinématique manuellement : la course s'y abonne elle-même.
Ne pas laisser DeliverySessionHUD ou un autre script écrire dans ce même texte de chrono.

La flèche peut garder son champ Manager vide : elle reçoit directement le joueur et
la destination. Garder son GameObject et son composant actifs ; le script gère sa visibilité.
Pour ce montage, ne pas lancer en parallèle l'ancienne boucle DeliveryManager/DeliverySession.

## Déroulement

- Au chargement : départ disponible ; arrivée, repère d'arrivée et chrono désactivés.
- Arrêt au départ : dialogue de départ et désactivation du départ.
- Après le dialogue, SendOff et le retour caméra : activation de l'arrivée, du chrono
  et de la flèche vers la zone d'arrivée.
- Arrêt à destination avant zéro : arrêt et masquage du chrono, de la flèche et de
  l'arrivée ; dialogue d'arrivée ; achievement après SendOff et le retour caméra.
- Temps écoulé : masquage du chrono, de la flèche et de l'arrivée, puis réactivation
  du départ. Une nouvelle tentative repart avec la durée complète.
- Cinématique refusée ou interrompue : retour au départ, sans achievement.
- Après réussite : les deux zones restent désactivées pour cette course dans la scène.

Le chrono utilise le temps de jeu : `Time.timeScale = 0` le suspend.
Une seule course peut être active à la fois ; plusieurs courses peuvent ainsi partager
la même flèche et le même texte de chrono. Chaque course possède ses deux zones et ses
deux DialogCinematic distincts. L'état terminé n'est pas sauvegardé entre chargements ;
l'achievement utilise la sauvegarde existante du projet.

## Vérifications dans Unity

1. En Play, vérifier que seul le départ est disponible et que son dialogue ne consomme
   aucune seconde du chrono, y compris pendant le retour caméra.
2. Livrer dans le temps imparti : vérifier que le chrono et la flèche disparaissent
   dès l'arrivée et que l'achievement apparaît seulement après la cinématique finale.
3. Avec Duration Seconds = 3, laisser expirer : le départ redevient disponible,
   l'arrivée et l'interface se masquent, et une nouvelle tentative repart à 3 secondes.
4. Arriver après expiration : aucun dialogue final ni achievement.
5. Interrompre une cinématique : aucun achievement ; départ à nouveau disponible.
6. Mettre Time.timeScale à 0 pendant le vol : le temps restant doit rester stable.

Si l'achievement a déjà été obtenu, le manager ne le redonne pas. Son menu
**Reset All Achievements** permet de refaire la vérification en effaçant tous les succès.
