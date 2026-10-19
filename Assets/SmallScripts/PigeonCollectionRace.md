# Course de collecte

La course active tous les objets à ramasser en même temps. Le pigeon les récupère
en traversant leur rayon, sans se poser. Chaque objet disparaît et le compteur
passe de `0/Y` à `Y/Y`. Quand tout est récupéré, la zone d'arrivée et son repère
apparaissent, et la flèche pointe vers elle. Cette course n'utilise pas de chrono.

## Montage dans Unity

1. Créer un GameObject actif **CourseCollecte** avec `PigeonCollectionRace`.
2. Pour chaque objet à ramasser, créer un GameObject **ObjetCollecte** avec
   `PigeonCollectionZone`. Placer le mesh de l'objet et le repère visuel en enfants.
   Ajuster **Radius** : la sphère cyan est visible en sélectionnant la zone.
   Aucun collider, Rigidbody ou événement de ramassage n'est nécessaire.
3. Renseigner tous ces composants dans **Collection Zones** de la course,
   chaque zone une seule fois. Le script masque leurs GameObjects avant le départ,
   les active au lancement et les masque au ramassage.
4. Dans le Canvas, créer un panneau **CompteurCollecte** avec un texte TMP et
   une `Image` à côté. Renseigner **Counter Parent**, **Counter Text**,
   **Counter Icon** et **Collection Sprite**. Le script écrit `X/Y`, assigne le
   sprite à l'Image et masque/affiche le panneau.
5. Renseigner **Player** avec le `PigeonController` du joueur.
6. Préparer l'arrivée avec un `PigeonStopZone`, puis le renseigner dans
   **Destination Stop**. **Destination Marker** est son visuel facultatif.
   Garder le GameObject de la zone actif : le script active/désactive son composant.
7. Renseigner **Arrow** avec le `DeliveryCompassArrow` existant sous la caméra.
   Garder son GameObject actif. La flèche apparaît seulement au retour.
8. Renseigner **Departure Stop** et éventuellement **Departure Marker** pour
   masquer le départ pendant la course. **Departure Cinematic** est facultative :
   si elle est renseignée, une cinématique interrompue ne lance pas la collecte.
9. **Stop To Disable During Return** : PigeonStop facultatif, distinct de l'arrivée.
   S'il est actif quand la flèche apparaît, son composant est désactivé jusqu'à la
   réussite (cinématique finale comprise), puis réactivé. S'il était déjà inactif,
   il reste inchangé. L'annulation de la course restaure aussi le composant suspendu.

Le contrôleur **CourseCollecte** doit rester actif. Ne pas le placer sous le panneau,
une zone de collecte ou un repère que la course masque. Les zones de collecte
doivent être indépendantes les unes des autres et garder leurs parents actifs.
Un **Marker** contient uniquement le visuel, jamais un PigeonStop, une cinématique
ou la course. Utiliser un panneau de compteur dédié et éviter deux courses
simultanées utilisant les mêmes zones ou la même flèche.

## Événements à brancher

| Événement | Appel |
| --- | --- |
| Départ : `PigeonStopZone.OnPlayerStopped` | `DialogCinematic.PlayCinematic()` de départ, comme avant |
| Cinématique de départ : `OnCinematicEnded` | `CourseCollecte → PigeonCollectionRace.StartCollection()` |
| Arrivée : `PigeonStopZone.OnPlayerStopped` | `CourseCollecte → PigeonCollectionRace.CompleteCollection()` |

Aucun événement à brancher sur les objets à ramasser. Ils sont détectés
automatiquement à partir de la liste **Collection Zones**, y compris lors d'un
passage rapide entre deux frames. Un ramassage ne compte qu'une fois.

## Fin et récompense facultatives

- **Arrival Cinematic** : renseigner le `DialogCinematic` final pour que
  `CompleteCollection()` le joue automatiquement. Ne pas brancher aussi
  `PlayCinematic()` sur le PigeonStop d'arrivée. Une interruption ou un refus de la
  cinématique réactive l'arrivée pour réessayer, avec les objets déjà récupérés.
- **Completion Achievement** : renseigner le succès et l'ajouter aussi à la liste
  du `AchievementManager`. Il est attribué après la fin normale de la cinématique
  finale, ou directement à l'arrivée si aucune cinématique n'est renseignée.
- **On Completed** : événement facultatif déclenché une fois à la réussite.

Après réussite, le départ, les objets, le compteur et l'arrivée restent masqués.
Désactiver/réactiver le composant en cours de collecte annule cette tentative :
le prochain lancement repart à zéro. Une course réussie reste terminée.

## Vérification en Play

Avec trois objets : vérifier que tout est masqué avant le départ, puis que
`StartCollection()` affiche les trois objets et `0/3` avec le sprite. Traverser
une zone en vol : l'objet disparaît et le compteur affiche `1/3`. Repasser à cet
endroit ne change rien. Après le troisième objet, vérifier `3/3`, le repère
d'arrivée et la flèche. Se poser à l'arrivée : le compteur et la flèche disparaissent,
la cinématique finale éventuelle se joue et le succès est attribué à sa fin.
Vérifier aussi un passage rapide, la pause et une cinématique finale interrompue.
