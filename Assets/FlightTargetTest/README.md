# Test de vol entre objectifs aleatoires

Tout le prototype est contenu dans ce dossier. Les scripts, prefabs et scenes d'origine ne sont pas modifies. Les visuels `Delivery/Prefabs/End` et `Compass/CompassArrow_Prefab` sont references depuis leur emplacement existant.

## Demarrer

Ouvrir `Scenes/RandomFlightCityTest.unity` puis lancer Play. Cette copie de `TataCityScene` est deja configuree, avec son prefab de boussole dans `Prefabs/RandomFlightCompass.prefab`.

Pour creer une nouvelle copie a partir de la ville et de ses derniers reglages :

1. Ouvrir la scene de ville contenant le pigeon et sa camera.
2. Choisir **Tools > Flight Target Test > Create From Current City**. Unity propose d'abord de sauvegarder les scenes modifiees.
3. Le menu cree et ouvre une nouvelle scene dans `Assets/FlightTargetTest/Scenes`. Il ne remplace aucune scene de test existante.
4. Lancer Play. Rejoindre l'anneau : le point est valide au passage et un nouvel objectif apparait immediatement. La boussole suit le nouvel objectif, avec les reglages visuels de la boussole de la scene source.

La copie conserve le controleur du pigeon et sa camera. Le gestionnaire de livraisons, la selection, les cinematiques et les affichages de livraison sont desactives uniquement dans cette copie. Le test n'a pas besoin de demarrer une session de livraison. La scene livree correspond a la version enregistree sur disque de la ville ; utiliser le menu apres sauvegarde pour reprendre d'autres changements de l'equipe.

## Reglages

Selectionner **Flight Target Test** dans la Hierarchy :

- `Spawn Bounds` : volume en coordonnees monde, affiche lorsque l'objet est selectionne. Par defaut, zone de 300 x 300 autour du depart du pigeon.
- `Surface Layers` : surfaces sur lesquelles chercher le sol. Par defaut, tous les layers physiques habituels ; les toits plats peuvent donc recevoir un objectif.
- `Obstacle Layers` : geometrie contre laquelle verifier l'espace libre. Garder les batiments dans ce masque.
- `Height Above Surface` : hauteur du centre de l'anneau au-dessus de la surface, 4 unites par defaut.
- `Clearance Radius` : espace libre exige autour du centre, 3 unites par defaut. Garder une valeur inferieure a la hauteur, sinon le sol bloque le placement.
- `Minimum / Maximum Distance` : distance 3D depuis le joueur au moment du tirage, 40 / 250 par defaut.
- `Visit Radius` : distance 3D qui valide le point, 3 unites par defaut. Le mouvement entre deux images est pris en compte, meme a pleine vitesse.
- `Placement Attempts` : nombre maximal d'essais. En cas d'echec, aucune cible inaccessible n'est imposee et un nouvel essai a lieu une seconde plus tard.
- Les boutons Odin **Nouvel objectif** et **Recommencer** permettent respectivement de passer une cible et de remettre le compteur a zero pendant Play.

La zone s'evalue avec les colliders de la ville. Les triggers et le joueur sont ignores. Le test verifie le volume libre de destination, pas l'existence d'un trajet : le pigeon reste libre de contourner les obstacles. Si la ville comporte de l'eau avec un collider, l'exclure via `Surface Layers` pour eviter des objectifs au-dessus de l'eau.

**Flight Test Compass** contient `RandomFlightCompassArrow` : reference `Objectives` vers le test, camera, enfant `Graphics`, puis `ArrowPivot`. Le nouveau composant remplace `DeliveryCompassArrow` dans le prefab dedie au test. Le menu peut aussi adapter une instance dans la nouvelle scene. Le prefab source conserve son composant et ses reglages. Aucun acces par reflection ni faux objet de livraison n'est necessaire.

## Verification

**Tools > Flight Target Test > Check Current Test** verifie les passages rapides, les references, la desactivation des anciens gestionnaires et 20 placements dans les colliders de la ville.

Dans Play, verifier aussi :

1. L'anneau et sa distance sont visibles, la boussole indique la bonne direction.
2. Un passage rapide incremente le compteur une seule fois ; la boussole change immediatement de direction.
3. Plusieurs trajets consecutifs restent praticables, y compris vers les toits.
4. Changer le FOV ou tourner rapidement la camera conserve une fleche stable.
5. Desactiver puis reactiver le test masque puis relance les objectifs ; quitter Play ne laisse aucun anneau dans la scene.
6. Avec un masque de surfaces vide, aucun objectif n'apparait et le HUD indique le probleme. Retablir le masque relance le placement automatiquement.

Les controles automatiques ne remplacent pas ces essais de vol et de lisibilite dans l'editeur.

Verification effectuee lors de la creation : compilation des nouveaux scripts et six cas de detection de passage executes sur le code compile (passage rapide, proximite sans entree, joueur immobile dedans/dehors, limite du segment et contact au bord du rayon). Les references locales, GUID et la conservation des fichiers source ont egalement ete controles. Les placements physiques et le rendu en Play restent a verifier dans Unity : le projet etait deja ouvert dans une autre instance, ce qui a empeche la verification en batch.
