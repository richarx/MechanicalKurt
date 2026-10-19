# Course contre un personnage

`PigeonCharacterRace` reprend le départ par PigeonStop et dialogue. À la fin
normale de la cinématique, l'arrivée apparaît et le personnage suit les points
du trajet en **Duration Seconds**, à vitesse constante sur l'ensemble du trajet.
Aucun chronomètre n'est affiché au joueur. La pause suspend le déplacement et le délai.

## Montage dans Unity

1. Ajouter `PigeonCharacterRace` sur un GameObject actif dédié, indépendant des
   objets qu'il masque. Renseigner **Player** avec le pigeon et **Character**
   avec la racine du personnage à déplacer.
2. Renseigner **Departure Stop**, **Departure Cinematic** et éventuellement
   **Departure Marker** (visuel uniquement). Le GameObject du PigeonStop de
   départ reste actif : seul son composant est désactivé pendant la tentative.
3. Créer des objets vides pour les points du trajet, indépendants du personnage,
   puis les ranger dans **Waypoints**, dans l'ordre. La position du personnage
   après le dialogue constitue le départ ; le dernier point constitue l'arrivée.
   Les lignes cyan apparaissent en sélectionnant le contrôleur de course.
4. Régler **Duration Seconds** et **Maximum Height**. La hauteur est une altitude
   absolue en coordonnées monde (Y), pas une distance au-dessus du sol.
5. Renseigner **Destination Object** avec l'objet à afficher pendant la course
   (zone et visuel), et **Destination Stop** avec son `PigeonStopZone`.
   Sur ce PigeonStop, régler **Stop Speed Threshold** très haut (par exemple
   `10000`) et **Stop Duration** à `0` pour accepter un passage en vol.
   Ajuster **Radius** pour qu'un passage rapide soit détectable.
6. Renseigner **Character Arrival Pose** avec un objet vide indépendant indiquant
   la position ET la rotation du personnage pour le dialogue final. Renseigner
   **Arrival Cinematic** avec un second `DialogCinematic`.
7. Renseigner **Failure Message** avec un `FadingMessage` actif, portant un texte
   TMP sur le même objet et son **Group** assigné au CanvasGroup. Garder cet objet
   actif : son alpha est géré par `FadingMessage`.

Les deux cinématiques et leurs caméras, le joueur, le personnage, le message et
le contrôleur doivent rester hors de **Destination Object** et **Departure Marker**.
Le parent de l'arrivée doit rester actif pour que l'objet puisse être affiché.
Le déplacement du personnage est piloté par cette course : désactiver les autres
scripts qui le déplacent (`AmbientWalker`, NavMeshAgent, etc.) et utiliser un
Rigidbody cinématique s'il en possède un. Le root motion de son Animator est
suspendu pendant le trajet puis restauré pour la cinématique et les nouvelles tentatives.
**Face Movement** oriente le personnage horizontalement dans le sens du trajet.

## Événements à brancher

| Événement | Appel |
| --- | --- |
| Départ : `PigeonStopZone.OnPlayerStopped` | `DialogCinematic.PlayCinematic()` de départ |
| Cinématique de départ : `OnCinematicEnded` | `PigeonCharacterRace.StartRace()` |
| Arrivée : `PigeonStopZone.OnPlayerStopped` | `PigeonCharacterRace.CompleteRace()` |

Le composant écoute automatiquement le début de la cinématique de départ et la
fin de celle d'arrivée. À l'arrivée, il téléporte le personnage puis lance lui-même
la cinématique : ne pas ajouter un second appel à `PlayCinematic` sur l'arrivée.
Utiliser une zone de départ dédiée sans `PigeonStopDialogCinematic`, qui consommerait
le départ et attribuerait son succès dès le dialogue initial.

## Résultat et nouvelles tentatives

- Passage à l'arrivée strictement avant le délai et sans dépasser la hauteur :
  arrivée masquée, personnage téléporté à **Character Arrival Pose**, cinématique
  finale jouée. Après sa fin normale, **On Completed** est invoqué une fois.
- Pigeon au-dessus de **Maximum Height** : `CHEATER`.
- Délai atteint : `YOU LOOSE`.
- Dans ces deux échecs, l'arrivée disparaît, le personnage retrouve sa position
  et rotation d'avant le dialogue et le départ redevient disponible.
- Une cinématique interrompue ou refusée annule la tentative et réactive le départ.
  Désactiver le contrôleur annule aussi la tentative en cours.
- Après réussite, le départ et l'arrivée restent masqués, y compris après une
  désactivation/réactivation du contrôleur.

## Vérification en Play à effectuer

Avec un trajet de trois points et un délai court, vérifier : départ → dialogue →
activation de l'arrivée et déplacement sans chrono visible. Atteindre l'arrivée
avant le personnage : vérifier sa téléportation, le dialogue final et **On Completed**.
Puis tester séparément le dépassement de hauteur et le délai écoulé : vérifier le
texte exact, le retour du personnage et la possibilité de recommencer entièrement.
Vérifier la pause, une arrivée à la limite du délai, une cinématique annulée et
la désactivation/réactivation du contrôleur en pleine course.
