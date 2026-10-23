# Cinématiques et récupération de livraison

## ManagerCinematic : gestion indépendante du cadrage

Le manager ne connaît ni DeliveryManager, ni les sessions, ni les dialogues, ni les
animations des PNJ. L'appelant lui fournit une caméra Cinemachine et décide quand finir.

```csharp
bool started = cinematicManager.BeginCinematic(cameraDuPnj,
    onReady: () => dialogManager.StartDialog(sequence),
    onEnded: () => Debug.Log("Cinématique terminée, y compris après une interruption"));

// Plus tard, par exemple lorsque le dialogue se termine :
cinematicManager.CancelCinematic(
    onFinished: () => Debug.Log("Retour caméra et restauration terminés"));
```

- `BeginCinematic(camera, onReady, onEnded)` retourne `false` si le manager est occupé,
  désactivé ou si les caméras sont mal configurées. Aucun callback de cette demande
  refusée n'est appelé. L'appelant gère ce cas.
- `onReady` est appelé une fois, après l'apparition des bandes et la fin du blend vers
  la caméra demandée. Le manager conserve ensuite le plan sans limite de durée.
- `CancelCinematic(onFinished)` demande le retour vers la caméra précédente, puis le
  retrait des bandes et la restauration du curseur. Le callback est appelé après ces
  étapes. Plusieurs demandes d'annulation reçoivent chacune leur callback de fin.
- Une annulation pendant l'entrée empêche `onReady` et revient à la vue précédente.
- Si le manager est déjà au repos, le callback d'annulation est appelé immédiatement.
- `onEnded` est facultatif et informe aussi l'appelant initial d'une interruption forcée,
  même si `onReady` n'a pas eu lieu. Une désactivation restaure immédiatement les réglages
  et appelle les callbacks de fin ; le manager ne peut plus animer le retour.
- Un timeout d'entrée provoque un retour sans annoncer la caméra comme prête.
- Une caméra `null` conserve la vue actuelle, avec les bandes et les mêmes callbacks.
- `IsPlaying` reste vrai pendant l'entrée, l'attente et le retour. `IsReady` est vrai
  seulement pendant l'attente, avant la demande d'annulation.

Le manager bloque les commandes du pigeon dès qu'une cinématique est acceptée,
jusqu'à la fin du retour caméra et du fondu des bandes. Affecter `Player Controls`
pour choisir le pigeon ; si vide, le manager recherche un `PlayerControls` dans la scène.
Le blocage couvre les déplacements, les battements d'ailes, le piqué, le freinage et
les actions ponctuelles. Ces dernières sont vidées pour éviter une action différée
après la cinématique. Le pigeon ne peut pas recapturer le curseur pendant le blocage.
La validation du dialogue avec Espace reste disponible. La physique continue et
`Time.timeScale` reste inchangé ; les transitions et les bandes utilisent le temps réel.
Le manager restaure le blocage précédent avant les callbacks de fin, y compris après
une annulation, un timeout ou sa désactivation. Un pigeon déjà bloqué le reste.
L'appelant gère toujours la pause éventuelle de la session de livraison.
Vérifications du blocage : scripts de jeu compilés et dix scénarios validés en
simulation (entrées neutralisées, actions en attente vidées, retour normal,
annulation à l'entrée, désactivation, refus, timeout, blocage initial conservé,
composant de commandes désactivé et cinématiques consécutives). À vérifier en Play :
maintenir les touches pendant le dialogue, le geste final et le retour caméra ;
Espace doit faire avancer le dialogue sans faire décoller le pigeon.
Le premier callback d'entrée est toujours différé, même sans caméra et sans fondu.
Ne pas laisser un ancien callback annuler une nouvelle cinématique : l'appelant doit
nettoyer ses abonnements lorsqu'il reçoit `onEnded`.

## Configuration des caméras

1. Garder une seule caméra Unity de rendu avec un CinemachineBrain. Créer une caméra
   Cinemachine de gameplay séparée, priorité 10, et des caméras de cinématique, priorité 0.
   Les garder actives sur un canal accepté par le Brain. Le blend est réglé sur le Brain.
2. Sur ManagerCinematic, affecter Brain et Gameplay Camera. Letterbox peut rester vide
   pour créer automatiquement les bandes. Bar Height = 0,1 correspond à 10 % par bande.
3. Prototype View Source est réservé au contrôleur noclip qui écrit directement sur
   Main Camera. Le laisser vide pour un rig Cinemachine normal.

Pour réparer un ancien montage, sélectionner ManagerCinematic puis utiliser
**Tools > Delivery > Repair Cinematic Camera Setup** hors Play. La commande sépare
la caméra de gameplay et place le Brain sur la caméra de rendu, avec Undo.

## DeliveryPickupCinematic : orchestration propre aux livraisons

Ce composant conserve le fonctionnement des récupérations existantes. Il choisit la
caméra du départ, met la session en pause, demande le cadrage à ManagerCinematic,
attend le dialogue ou le délai, déclenche SendOff, puis demande le retour caméra.
La récupération est validée et la pause rétablie après le retour.

Les scènes existantes ont été migrées en conservant les réglages et UnityEvents :

1. DeliveryManager : Pickup Mode = External ; On Pickup Started ->
   **DeliveryPickupCinematic.BeginPickUp (Dynamic int)**.
2. DeliveryPickupCinematic : affecter Cinematic Manager et Delivery Manager.
3. On Cinematic Ready -> CinematicDelay.Wait (Dynamic int).
4. CinematicDelay : Seconds = 1 ; On Completed ->
   **DeliveryPickupCinematic.ContinuePickup (Dynamic int)**.
5. On Cinematic Ended -> CinematicDelay.CancelWait.

Les événements et méthodes de livraison sont désormais sur DeliveryPickupCinematic.
Le token appartient à la récupération ; ManagerCinematic ne reçoit aucun token.

Sur chaque DeliveryPoint, Camera Cinematic reste la caméra à utiliser. Le composant
DeliveryPointCinematic, sur ce même objet, conserve l'Animator, le trigger SendOff et
sa durée. Garder le PNJ et la caméra en dehors du visuel de sélection qui se désactive.
Sans DeliveryPointCinematic, Default Send Off Seconds est utilisé sans animation.
Pour une fin d'animation explicite, cocher Wait For Send Off Signal sur
DeliveryPickupCinematic et appeler FinishSendOff(token) avec le token reçu via
On Send Off Started. Sans signal, le composant attend.

Pour remplacer le délai par un dialogue, remplacer le lien vers CinematicDelay.Wait
par un composant qui conserve le token, lance le dialogue et appelle
DeliveryPickupCinematic.ContinuePickup(token) à sa fermeture. Nettoyer ce composant
via On Cinematic Ended. Ne pas relire CurrentToken depuis un ancien callback.
On Cinematic Ended est aussi appelé après une interruption : ce n'est pas un événement
de réussite de livraison.

## DialogCinematic : dialogue de personnage depuis un arrêt

1. Ajouter `DialogCinematic` à un objet qui reste actif pendant la séquence.
2. Affecter `Cinematic Manager`, `Character Animator`, l'asset `Dialogue` et la
   caméra Cinemachine dans `Cinematic Camera` (facultative : vide, la vue reste la même).
   `Dialog Manager` peut rester vide pour utiliser le singleton de la scène.
3. Dans l'Animator du personnage, créer deux paramètres de type Trigger nommés
   exactement `Talk` et `SendOff`, avec leurs transitions vers les animations.
   Autoriser la réentrée de l'état Talk si chaque réplique doit relancer l'animation.
4. Brancher `PigeonStopZone.OnPlayerStopped` sur `DialogCinematic.PlayCinematic()`.
5. Régler `Send Off Seconds` pour laisser jouer le geste final avant le retour caméra.

Le dialogue démarre après le blend d'entrée. `Talk` est déclenché au début de chaque
réplique, première comprise ; révéler instantanément le texte ne le redéclenche pas.
Après la validation de la dernière réplique et le fondu de fermeture, `SendOff` est
déclenché, puis le composant attend le délai avant de rendre la caméra au gameplay.
Les appels répétés pendant la séquence ou quand un autre dialogue est actif sont ignorés.
`CancelCinematic()` ou la désactivation ferme le dialogue et rend la caméra sans SendOff.
`OnCinematicEnded` arrive après le retour, y compris en cas d'annulation.
Les commandes du pigeon sont bloquées et rétablies par `ManagerCinematic`.
Le composant ne modifie pas `Time.timeScale`.

À vérifier en Play : dialogue de plusieurs répliques (`Talk` une fois par réplique),
dernière validation (`SendOff`, délai, retour), double appel pendant la lecture,
annulation pendant l'entrée ou le dialogue, désactivation puis nouvelle lecture.

Vérifications réalisées pour ce composant : compilation des scripts de jeu et sept
scénarios en simulation (enchaînement, appels répétés et relecture, annulation à
l'entrée, désactivation, interruption du manager, managers occupés, remplacement
du dialogue). Les animations et les blends réels restent à vérifier dans Unity.

## SpecialDanceCinematic : dialogue chronométré, descente et danses

Le composant `SpecialDanceCinematic` enchaîne les répliques d'un dialogue contrôlées par script,
une descente d'objet, un cut caméra et les passages successifs des personnages.
Les branchements, les durées X/Y/Z/W, les transitions Animator et les vérifications
sont décrits dans [SpecialDanceCinematic.md](SpecialDanceCinematic.md).

`ManagerCinematic.SwitchCinematicCamera(camera, cut)` change de plan sans libérer
les commandes ni perdre la caméra de retour. Attendre `IsReady` avant de commencer
l'action du nouveau plan ; `cut: true` force une coupe pour cette transition.
La surcharge `SwitchCinematicCamera(camera, transitionSeconds)` définit une durée
pour ce seul changement de plan : 0 = cut, valeur positive = Ease In Out.
Les réglages de blend du Brain sont restaurés après la transition ou son annulation.

## Vérification dans Unity

- Tester un appel direct à BeginCinematic avec une caméra : `onReady` arrive après
  le cadrage ; attendre plusieurs secondes ne termine pas la cinématique.
- Appeler CancelCinematic avec un callback : celui-ci arrive après le retour caméra,
  le retrait des bandes et la restauration du curseur.
- Annuler pendant l'entrée : aucun `onReady`, puis une seule fin par callback.
- Désactiver le manager : restauration immédiate et notification de fin.
- Tester une caméra null, un appel refusé pendant une cinématique et deux demandes
  d'annulation. Vérifier les notifications et l'absence d'effets sur une demande refusée.
- Dans MainCityScene, démarrer une session et valider un départ : pause, cadrage,
  attente d'une seconde, geste SendOff, retour, puis état Carrying et reprise du timer.
- Tester un autre départ, puis un reset pendant l'attente et pendant le retour.
  Aucun ancien token ne doit terminer une nouvelle récupération ou la bloquer.
- Tester deux caméras de même priorité : le retour doit rétablir la caméra précédente.

Vérifications automatiques réalisées : compilation des scripts de jeu et d'éditeur,
12 vérifications du cycle de vie et de l'adaptateur dans une simulation des coroutines,
contrôle des références et de la migration des huit scènes. La simulation ne remplace
pas Unity : le rendu, les blends réels et les animations restent à vérifier en Play.
