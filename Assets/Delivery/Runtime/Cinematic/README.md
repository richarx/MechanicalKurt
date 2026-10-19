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

Le manager ne met pas le gameplay en pause et ne change pas `Time.timeScale`.
Les transitions et les bandes utilisent le temps réel. L'appelant gère la pause.
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
