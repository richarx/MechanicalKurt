# Cinématique spéciale : dialogue, descente, danses

Ajouter `SpecialDanceCinematic` à un GameObject qui reste actif pendant toute la
séquence. Aucun branchement de scène ou contrôleur Animator n'est modifié automatiquement.

## Branchements dans l'Inspector

| Champ | Référence / réglage |
| --- | --- |
| Cinematic Manager | Le `ManagerCinematic` existant : Brain, Gameplay Camera et Player Controls renseignés. |
| Dialog Manager | Le `DialogManager` de la scène, ou vide pour son singleton. |
| Player | Le `PigeonController`, ou vide pour une recherche automatique. Ses mouvements physiques sont gelés, en plus du blocage des commandes. |
| Dialogue Camera | Caméra Cinemachine du premier plan, dialogue. |
| Descent Camera | Caméra Cinemachine du deuxième plan, descente. |
| Dance Camera | Caméra Cinemachine du troisième plan, danses. |
| Dialogue | Asset créé via **Create > Dialog > Dialog Sequence**, contenant au moins une réplique. Deux répliques sont prises en charge automatiquement. |
| Dialogue Seconds | **X**, durée totale du dialogue, effet machine à écrire compris, partagée entre ses répliques. Exemple : 6 s avec deux répliques = 3 s par réplique. Prévoir assez de temps pour révéler et lire chaque texte. |
| Music Clip | La musique spéciale, jouée en boucle par les sources existantes du MusicPlayer. |
| Background Music | Le `MusicPlayer` existant. Si vide, utilise son singleton ou le recherche dans la scène. Ses deux sources audio doivent être actives. Aucun AudioSource dédié à la cinématique n'est nécessaire. |
| Descending Object | Le Transform du GameObject qui descend, placé initialement en hauteur. |
| Descent Target | Un GameObject vide placé au point d'arrivée, indépendant de l'objet déplacé. Seule sa position est utilisée. |
| Descent Seconds | **Y**, durée du déplacement linéaire vers ce point. |
| Descent Light | Le composant `Light` à allumer pendant la descente. Facultatif ; son GameObject doit être actif. Le composant peut initialement être désactivé : le script l'active au début de la descente. |
| Light Ramp Seconds | **T**, durée de montée linéaire de l'intensité de **0 à 900**, à partir du début du déplacement de l'objet. |
| Use Transition Zoom | Active une impulsion de zoom/dézoom sur Descent Camera après la fin de la descente. Pour cet effet, Descent Camera doit être une `CinemachineCamera`. |
| Zoom Amount Percent | Intensité du zoom ; commencer à **12 %**. 0 désactive l'impulsion. |
| Zoom Pulse Seconds | Durée totale de l'impulsion ; commencer à **0,35 s**. |
| Zoom Pulse Curve | Courbe sur un temps de 0 à 1 : valeurs positives = zoom, négatives = dézoom. Le réglage initial fait un zoom bref, un léger rebond en dézoom, puis revient au cadrage initial. |
| Dance Camera Transition Seconds | Durée de la transition Descent Camera -> Dance Camera, après l'impulsion. **0 = cut**, **0,2 s** = transition rapide, **0,6 s** = plus douce. |
| Dance Target | Un GameObject vide commun à tous les personnages. Sa position définit l'arrivée ; chaque personnage s'oriente vers elle au départ. La rotation de ce point n'est pas utilisée. |
| Actors | Les personnages dans leur ordre de passage. Pour chacun : Animator, Movement Root et durées Walk Seconds / Back Walk Seconds. |
| Start Dance Delay Seconds | **V**, délai après la fin de la transition vers Dance Camera avant de déclencher StartDance simultanément sur tous les Animators de Actors. 0 = dès que la caméra est prête. |
| Dance Seconds | **Z**, durée de danse pour chaque personnage, transition Animator comprise. |
| Between Actors Seconds | **W**, attente après le retour d'un personnage, avant le prochain. Aucune attente supplémentaire après le dernier. |
| Scripts To Suspend | Les scripts qui déplacent déjà ces personnages ou le décor, par exemple `AmbientWalker`. Leur état activé/désactivé est restauré après le retour au jeu. |

Sur chaque acteur, **Movement Root** doit être la racine du personnage contenant
l'Animator : elle est déplacée en coordonnées monde. Si l'Animator est déjà sur
cette racine, le champ peut rester vide. Utiliser des personnages distincts, sans
racines imbriquées. Placer les points de destination hors des hiérarchies déplacées.
Garder le composant de cinématique et ses managers hors de l'objet qui descend.

Les déplacements sont dirigés par Transform : pour ce montage de jam, utiliser des
personnages sans Rigidbody dynamique ni NavMeshAgent actif qui écriraient aussi
leur position. Le root motion est désactivé temporairement par le script.

## Caméras

Garder une seule caméra Unity de rendu avec le `CinemachineBrain`, la caméra
Cinemachine de gameplay séparée et les trois caméras de cinématique actives.
Exemple de priorités initiales : gameplay = 10 ; cinématiques = 0. Utiliser le même
canal de sortie accepté par le Brain. Ne pas activer/désactiver les GameObjects
de caméra via les événements de cette séquence.

Le dialogue commence après le blend d'entrée et l'apparition des bandes. À cet
instant démarrent ensemble la première réplique, la musique et le chronométrage X.
Les textes apparaissent progressivement avec l'effet machine à écrire et le son de
frappe du DialogManager. Régler **Characters Per Second** sur ce manager pour ajuster
la vitesse. Chaque réplique dispose de X divisé par le nombre de répliques (X/2
avec deux répliques) : augmenter X pour
les textes longs afin qu'ils puissent être révélés et lus entièrement. Espace,
manette et le raccourci de test P ne peuvent ni avancer ni remplacer le dialogue.

Après X : fermeture du dialogue, transition normale vers Descent Camera, puis
descente pendant Y une fois cette caméra prête. À cet instant démarre aussi la
montée de Descent Light de 0 à 900 en T secondes. Son chronométrage est indépendant :
si T dépasse Y, la lumière continue de monter pendant les plans suivants, sans
retarder le zoom ni le changement de caméra, et même après le retour au jeu si T
est très long. Elle reste à 900 après la fin de la montée. Le menu Pause suspend
cette montée avec le temps du jeu.

Après Y : impulsion de zoom/dézoom
si elle est activée, puis transition vers Dance Camera pendant **Dance Camera
Transition Seconds**. Une durée nulle force un cut ; une durée positive utilise
un blend Ease In Out. Ce réglage est propre à ce changement de plan : les réglages
Default Blend et Custom Blends du Brain sont ensuite rétablis, y compris après
une annulation. Une transition longue n'est pas interrompue par le timeout ordinaire
du manager : le délai de sécurité est étendu à sa durée plus une seconde.

Le zoom agit sur le champ de vision en perspective, ou la taille en orthographique,
sans déplacer le rig de caméra. Le cadrage d'origine est restauré avant la transition,
ainsi qu'en cas d'annulation ou de désactivation pendant l'effet. La durée de
l'impulsion s'ajoute à Y ; elle n'est pas comprise dans le temps de descente.

Une fois Dance Camera prête et sa transition terminée, le script attend **V** secondes, puis déclenche
**StartDance** sur tous les Animators de la liste Actors. Il laisse une frame pour
la prise en compte du trigger, puis commence les passages individuels avec Walk.
Les durées X/Y/Z/W/V n'incluent pas les transitions de caméra.

## Animator de chaque personnage

Mettre **Idle** comme état initial de chaque Animator, puis créer cinq paramètres
de type **Trigger**, avec exactement ces noms :

- `StartDance`
- `Walk`
- `SpecialDanse` (orthographe avec « s », comme demandé)
- `BackWalk`
- `Back`

Créer une transition **Idle -> danse d'attente** conditionnée par **StartDance**,
sans sortie automatique d'Idle avant ce trigger. La danse d'attente peut boucler
pendant les passages des autres personnages.

Créer les transitions **Any State -> Walk**, **Any State -> SpecialDanse** et
**Any State -> BackWalk**, chacune avec son trigger comme condition. Ajouter une
transition **BackWalk -> état de repos** (ou vers l'animation finale souhaitée),
avec le trigger **Back** comme condition.
Décocher **Has Exit Time** et **Can Transition To Self**, et garder une transition
courte, par exemple 0 à 0,1 s. Faire boucler les clips pour couvrir les durées
configurées ; éviter une sortie automatique de la danse avant le trigger BackWalk.

Le personnage se tourne vers la position de Dance Target au départ, uniquement
autour de l'axe vertical pour rester droit, puis avance vers elle. Il conserve cette
orientation pendant la danse de Z secondes et le recul vers sa position d'origine.
Si le point est déjà à sa position horizontale, il garde son orientation actuelle.
Le modèle doit regarder dans la direction de la flèche bleue (+Z) de Movement Root ;
si nécessaire, corriger la rotation locale du modèle enfant plutôt que celle du point.
À son retour exact au point de départ, le script restaure sa rotation et déclenche
**Back** une seule fois, avant l'attente W. Il laisse alors le contrôleur Animator
gérer l'animation finale, y compris pour le dernier personnage et au retour au jeu.
L'ancien état Animator n'est restauré que pour les personnages interrompus avant
leur retour : une annulation en cours de trajet ne déclenche pas Back.

## Déclenchement et événements

Brancher le UnityEvent souhaité, par exemple `PigeonStopZone.OnPlayerStopped`, sur
**SpecialDanceCinematic.PlayCinematic()**. Depuis un script :

```csharp
specialDanceCinematic.PlayCinematic();
// Pour interrompre depuis un autre script :
specialDanceCinematic.CancelCinematic();
```

Les appels répétés pendant la lecture sont ignorés. Une autre cinématique ou un
dialogue déjà actif empêche le démarrage. Pour tester, le bouton **Jouer la
cinématique** est disponible dans l'Inspector en Play.

- **On Cinematic Started** : lancement accepté, commandes bloquées ; la caméra entre.
- **On Cinematic Completed** : séquence réussie, après le retour effectif au jeu.
- **On Cinematic Ended** : après le retour au jeu, succès ou interruption.
- **Completed Normally** : résultat consultable depuis un script ou dans l'Inspector.

Le MusicPlayer remplace temporairement ses deux morceaux (normal et low-pass) par
Music Clip, dès le début du dialogue. Il conserve ses réglages de volume et de mixer.
L'alternance automatique et la transition musicale éventuellement en cours sont
suspendues pendant ce remplacement. Au retour caméra, les morceaux précédents sont
rétablis à leur position de lecture mémorisée, puis le fonctionnement normal reprend.
Une musique qui était arrêtée avant la cinématique le reste ; le démarrage différé
normal du MusicPlayer reste disponible. L'annulation rétablit aussi la musique.

Les commandes,
l'immobilisation préalable du pigeon, la musique de fond, les priorités de caméra,
le root motion et les scripts suspendus retrouvent leur état précédent.
Sur succès, l'objet descendu reste à son point d'arrivée. Sur annulation, il est
replacé au point de départ ; les personnages reviennent immédiatement à leurs
poses mémorisées. La désactivation du composant demande aussi l'annulation.
Si la montée de lumière avait commencé, une annulation restaure aussi son intensité
et son état activé/désactivé précédents. Une fin normale conserve l'éclairage obtenu.

La séquence ne modifie pas `Time.timeScale` ni le chrono de livraison. Ses délais,
ses déplacements et les animations suivent le temps du jeu : le menu Pause les
interrompt. Le retour caméra reste géré par ManagerCinematic en temps réel.
Si un chrono de livraison doit être suspendu pour cette scène, le gérer depuis
ses événements de début/fin avec l'état de pause précédent.

## Vérification dans Unity

1. Régler X = 6, Y = 2, Z = 3, W = 1, V = 2, avec au moins deux personnages en Idle.
2. Lancer avec deux textes courts : deux répliques de 3 s, révélées progressivement ;
   appuyer sur Espace, P et le bouton de validation manette pour vérifier qu'ils ne
   peuvent ni révéler instantanément le texte ni avancer les répliques. Vérifier que
   les invites de validation restent masquées. Essayer aussi de voler.
3. Vérifier la descente de 2 s après le deuxième cadrage, puis le zoom/dézoom de
   0,35 s et la transition vers les danses. Tester Dance Camera Transition Seconds
   à 0 (cut), 0,2 et 0,6 s ; les autres transitions doivent rester inchangées.
   Vérifier que les personnages restent en Idle pendant les 2 s suivant le cadrage,
   puis reçoivent tous StartDance ensemble avant le premier Walk. Tester aussi V = 0
   et une annulation pendant V : aucun StartDance retardé ne doit alors partir.
4. Placer les personnages de plusieurs côtés du point : vérifier qu'ils se tournent
   chacun vers leur destination à l'aller et qu'ils reculent sans demi-tour au retour.
   Vérifier l'ordre des personnages, leur arrivée au même point, leur danse de 3 s,
   le recul jusqu'à leur position d'origine, le déclenchement de Back à l'arrivée
   (y compris pour le dernier personnage) et la pause de 1 s entre deux passages.
5. Vérifier que Music Clip remplace la musique dans le MusicPlayer sans autre source,
   puis le retour caméra et commandes, la reprise de la musique de fond au même endroit,
   `Completed Normally = true` et l'objet descendu laissé en bas.
6. Relancer puis annuler pendant l'entrée, le dialogue, la descente, la danse et
   le recul. Tester aussi la désactivation du composant. Vérifier la restauration,
   un seul événement de fin et l'absence d'événement Completed.
7. Vérifier qu'un dialogue classique reste interactif après cette cinématique.
8. Tester avec la musique normale déjà en lecture, en cours de fondu, puis avant
   son démarrage différé. Vérifier aussi la restauration après annulation.
9. Annuler pendant le zoom et pendant le blend vers Dance Camera : vérifier le
   retour au champ de vision d'origine et la restauration des réglages du Brain.
   Relancer ensuite la cinématique pour vérifier que le zoom ne s'accumule pas.
10. Assigner une Light active à Descent Light. Avec Y = 2 et T = 1, vérifier une
    montée de 0 à 900 en une seconde au début de la descente. Avec T = 5, vérifier
    que la montée continue après la descente sans retarder les caméras. Annuler à
    mi-montée : l'intensité et l'état activé/désactivé d'origine doivent être restaurés.

Vérification effectuée hors éditeur : compilation C# des scripts de jeu et d'éditeur avec les
assemblies Unity du projet et 15 contrôles du vrai code MusicPlayer avec audio/tweens
simulés (remplacement des deux sources, restauration des morceaux et positions,
transition musicale en cours, alternance suspendue, relecture, arrêt préalable,
démarrage différé, désactivation et refus sans effet). Les cadrages, le son et les contrôleurs Animator réels
restent à valider en Play avec les références de la scène.
