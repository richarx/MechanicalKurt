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
   Pour téléporter le pigeon joueur avant la course, créer un objet vide indépendant,
   régler sa position et sa rotation puis l'assigner dans **Player Departure Pose**.
   La téléportation se fait après la fin normale de la cinématique de départ,
   avant le déplacement et le lancement du délai. Si le champ est vide, le
   joueur conserve sa position actuelle. Sa vitesse est remise à zéro et son
   forward est celui du repère (axe bleu local), y compris son inclinaison.
   Orienter cet axe vers l'eau pour préparer le plongeon. En vol, cette
   inclinaison est conservée jusqu'à une commande de tangage ou de plongeon.
   La caméra orbitale est replacée immédiatement derrière le pigeon, selon le
   forward du repère et dans les limites de son orbite actuelle. L'ancienne référence
   **Character Departure Pose** est conservée dans ce nouveau champ.
3. Dans la section **Course**, cliquer sur **Créer le trajet (reprendre les anciens
   points)**. Un objet avec `CharacterRacePath` est créé, assigné dans **Path** et
   sélectionné. Comme sur les autres paths, cliquer sur **Ajouter des points dans
   la Scene**, survoler un collider et appuyer sur **Ctrl gauche** pour chaque
   point. **Échap** termine l'ajout. Cliquer sur un point pour le déplacer avec
   les poignées ou le supprimer dans l'Inspector ; Undo est disponible.
   Il est aussi possible de créer un `CharacterRacePath` manuellement et de
   l'assigner à **Path**. Le garder indépendant du personnage.
   La position du personnage après le dialogue constitue le départ ; le dernier
   point constitue l'arrivée. Ce trajet ne boucle pas et ignore les réglages
   d'arrêt et de feu éventuels des points : seule leur position est utilisée.
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
8. Dans **Race Objects**, ajouter tous les GameObjects à afficher pendant la
   course (obstacles, indications, effets, etc.). Ils sont masqués avant le départ,
   activés à la fin normale de la cinématique de départ, puis désactivés dès
   l'arrivée du joueur, avant la cinématique finale, ou en cas d'échec/annulation.
   Ils sont réactivés lors d'une nouvelle tentative. Les entrées vides sont ignorées.
   Garder leurs parents actifs et ne pas partager ces objets entre deux courses
   simultanées.
9. Dans **Arrivée → Completion Achievement**, assigner l'`AchievementData` de
   victoire et l'ajouter aussi à la liste **Achievements** de l'`AchievementManager`
   de la scène. Il est débloqué après la fin normale de la cinématique finale,
   avant **On Completed**. Laisser ce champ vide pour une course sans récompense.

Les deux cinématiques et leurs caméras, le joueur, le personnage, le message et
le contrôleur doivent rester hors de **Destination Object** et **Departure Marker**.
Le parent de l'arrivée doit rester actif pour que l'objet puisse être affiché.
**Race Objects** ne doit contenir ni le contrôleur, ni le PigeonStop de départ,
ni les cinématiques, ni le pigeon, ni le personnage, ni le message, ni leurs parents.
Ces entrées sont ignorées lors du masquage et empêchent le départ de la course,
avec un avertissement dans la Console.
Le déplacement du personnage est piloté par cette course : désactiver les autres
scripts qui le déplacent (`AmbientWalker`, NavMeshAgent, etc.) et utiliser un
Rigidbody cinématique s'il en possède un. Le root motion de son Animator est
suspendu pendant le trajet puis restauré pour la cinématique et les nouvelles tentatives.
Dans l'Animator Controller du personnage, prévoir un paramètre **Bool** nommé
exactement **Glide**. La course le passe à `true` au début du trajet, puis à `false`
à l'arrivée avant le dialogue final, à l'échec ou à l'annulation (y compris si le
contrôleur de course est désactivé). L'Animator est recherché sur **Character**
et ses enfants, comme pour le root motion.
**Face Movement** oriente le personnage horizontalement dans le sens du trajet.

Les anciennes références **Waypoints** restent conservées et utilisables tant
que **Path** n'est pas assigné. Le bouton de création copie leurs positions dans
le nouveau trajet. Il ne supprime pas les anciens objets vides. **Path** devient
ensuite la source du déplacement ; les anciens objets vides peuvent être supprimés
après vérification du nouveau trajet.

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
  finale jouée. Le pigeon joueur est immobilisé à sa position d'arrivée pendant
  toute cette cinématique et le retour de caméra : sa vitesse est annulée, et
  la gravité, les déplacements et les forces de vol sont suspendus.
  Son mouvement est rétabli sans reprendre son ancienne vitesse à la fin,
  ou si la cinématique est refusée/interrompue ou la course désactivée.
  Un blocage de mouvement déjà actif avant l'arrivée est conservé.
  Après sa fin normale, **Completion Achievement**, si renseigné,
  est débloqué et **On Completed** est invoqué une fois.
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
Arriver en vol à grande vitesse : vérifier que le pigeon reste sur place pendant
le dialogue et le retour de caméra, puis peut repartir à la fin. Tester aussi
l'interruption de la cinématique et la désactivation de la course pour vérifier
que le joueur n'est pas laissé immobilisé.
Avec un achievement enregistré et encore verrouillé, vérifier son déblocage à la
fin du dialogue final. Vérifier qu'un échec ou une cinématique finale interrompue
ne le débloque pas.
Avec **Player Departure Pose** placé ailleurs que le pigeon, vérifier la
téléportation du joueur, son orientation et sa vitesse nulle après le dialogue.
Incliner le repère vers l'eau et tourner la caméra avant le dialogue : vérifier
que le pigeon conserve ce forward au départ sans redressement automatique en vol,
et que la caméra se replace derrière lui. Vérifier ensuite les commandes de
rotation, de tangage et de plongeon.
Le personnage adverse doit commencer son trajet depuis sa propre position.
Sans repère assigné, vérifier que la course démarre comme avant.
Puis tester séparément le dépassement de hauteur et le délai écoulé : vérifier le
texte exact, le retour du personnage et la possibilité de recommencer entièrement.
Vérifier la pause, une arrivée à la limite du délai, une cinématique annulée et
la désactivation/réactivation du contrôleur en pleine course.
Observer **Glide** dans l'Animator : `true` uniquement pendant le trajet, puis
`false` lors d'une victoire, d'un échec et d'une désactivation de la course.
Vérifier aussi l'ajout, le déplacement, la suppression et Undo d'un point dans la
Scene. Les autres paths doivent conserver leur affichage en boucle. Ajouter deux
objets dans **Race Objects** : vérifier qu'ils sont masqués avant le départ,
visibles seulement pendant le trajet, puis masqués à la victoire, au dépassement
de hauteur, à l'expiration du délai et à la désactivation du contrôleur.
