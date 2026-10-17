# Cinématique de récupération

Ces composants se branchent sur le mode External de DeliveryManager. Aucun changement
du core n'est nécessaire. Le déclenchement se fait après l'arrêt validé dans le cercle,
pas au premier franchissement de la zone.

## Montage dans la scène

Si le Brain a été mis sur l'objet du manager ou si Main Camera porte aussi le composant
CinemachineCamera de gameplay : hors Play, sélectionner ManagerCinematic, puis lancer
**Tools > Delivery > Repair Cinematic Camera Setup**. La commande déplace le Brain sur
la caméra de rendu et sépare la caméra Cinemachine de gameplay. Elle préserve le regard
souris du contrôleur noclip via Prototype View Source, avec Undo, sans sauvegarder la scène.
Pour un rig Cinemachine normal, laisser Prototype View Source vide.

1. Garder une seule caméra Unity de rendu avec un CinemachineBrain. Créer une caméra
   Cinemachine de gameplay, priorité 10, et une caméra Cinemachine par point, priorité 0.
   Garder les caméras Cinemachine actives, sur un canal accepté par le Brain. Dans le
   Brain, régler Default Blend sur Ease In Out / 0,5 seconde (ou la durée souhaitée).
2. Sur chaque DeliveryPoint, affecter Camera Cinematic à sa caméra, ou laisser vide
   pour conserver la vue actuelle pendant toute la séquence. Garder le PNJ et
   cette caméra à côté du visuel de sélection, qui se désactive dès l'acceptation.
3. Ajouter DeliveryPointCinematic sur le même objet que DeliveryPoint. Affecter Animator,
   Send Off Trigger (SendOff par défaut), et Send Off Seconds à la durée du geste avec
   sa transition. Dans l'Animator : Trigger SendOff, transition vers le geste sans Exit
   Time, clip sans Loop Time, puis retour vers l'idle avec Exit Time. Retirer l'ancien
   déclenchement SendOff depuis On Picked Up pour éviter un deuxième geste.
4. Sur un objet actif indépendant, ajouter ManagerCinematic et CinematicDelay.
   Affecter Delivery Manager, Brain et Gameplay Camera au manager. Laisser Letterbox
   vide pour créer automatiquement les bandes. Si un CanvasGroup existant est utilisé,
   il doit rester actif avec alpha 0, sans bloquer les clics. Bar Height est la fraction
   de hauteur d'écran par bande (0,1 = 10 %).
5. Sur DeliveryManager : Pickup Mode = External. Dans On Pickup Started, ajouter
   ManagerCinematic.BeginPickUp depuis la section **Dynamic int**.
6. Sur ManagerCinematic : On Cinematic Ready -> CinematicDelay.Wait (**Dynamic int**).
7. Sur CinematicDelay : Seconds = 1 ; On Completed -> ManagerCinematic.ContinuePickup
   (**Dynamic int**). Ne pas utiliser une valeur int statique : il faut transférer le
   token reçu, qui identifie cette récupération.
8. Sur ManagerCinematic : On Cinematic Ended -> CinematicDelay.CancelWait (sans argument).

Le contrôleur de vol de test respecte déjà la pause de session. Unlock Cursor libère la souris pour un dialogue et
restaure son état à la fin. La cinématique ne change pas Time.timeScale.

## Remplacer l'attente par un dialogue

Retirer le lien On Cinematic Ready -> CinematicDelay.Wait et appeler à la place le
composant de dialogue. Celui-ci reçoit et conserve le token, puis appelle
`ContinuePickup(token)` lorsque le dialogue est terminé. Ne pas relire CurrentToken
dans un ancien callback. Nettoyer le dialogue depuis On Cinematic Ended.

Après cette attente, le manager déclenche le SendOff du PNJ concerné et attend la durée
configurée sur DeliveryPointCinematic, puis rend la caméra, retire les bandes, demande
la validation au core et reprend le timer. Sans composant DeliveryPointCinematic, le
manager attend Default Send Off Seconds sans déclencher d'Animator.

Pour piloter la fin exacte du geste depuis un autre composant : cocher Wait For Send Off
Signal ; recevoir le token via On Send Off Started, et appeler `FinishSendOff(token)`
quand l'animation est finie. Le composant doit conserver ce token pendant ce geste.
Sans ce signal, la séquence reste volontairement en attente.

## Vérification dans Unity

- Démarrer une session, s'arrêter dans un départ : le timer se fige, la caméra rejoint
  le PNJ, les bandes apparaissent. Le PNJ doit rester visible après disparition du cercle.
- Après l'arrivée caméra, attendre une seconde : un seul SendOff sur ce PNJ, puis retour
  caméra, disparition des bandes et reprise du timer. L'état devient Carrying.
- Essayer un autre point : cadrage et PNJ propres à ce point.
- Mettre Seconds à 3 pour vérifier que le geste attend bien cette durée.
- Reset/restart pendant l'attente : caméra, bandes et commandes sont restaurées ;
  un ancien token ne doit pas terminer la récupération suivante.
- Désactiver ManagerCinematic pendant une séquence valide : la cinématique est sautée
  et la récupération peut terminer, sans laisser la session en pause.
- Un point sans caméra conserve la vue actuelle : pause, attente, SendOff et bandes
  fonctionnent normalement, même sans Brain assigné au manager.
- Avec une caméra de point assignée, une configuration de Brain invalide produit un
  avertissement et saute la cinématique pour ne pas bloquer le mode External.

Les blends Cinemachine utilisent les réglages du Brain. Camera Timeout Seconds limite
l'attente d'une caméra qui ne prendrait pas la main (canal, priorité ou Brain mal réglé).
La fin du timer, un reset, une offre invalidée ou une désactivation interrompent la
séquence et restaurent les réglages sauvegardés. On Cinematic Ended s'exécute aussi à
l'interruption ; ce n'est pas un événement de réussite de livraison.
