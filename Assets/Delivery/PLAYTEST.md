# Playtest du prototype Delivery

## Créer une scène de test complète

Dans Unity, choisissez **Tools > Delivery > Create Full Delivery Test**. Unity demande d'abord quoi faire des scènes modifiées. Si vous annulez, rien n'est créé. La commande ouvre ensuite une scène temporaire et y monte le sélecteur de livraisons existant, une caméra, un sol simple et l'interface. Elle ne sauvegarde jamais la scène automatiquement : utilisez **File > Save As** seulement si vous souhaitez la conserver.

La scène de test utilise trois livraisons d'exemple et une session de trois minutes. Le joueur commence à `(0, 0, -6)`, à l'extérieur des cercles de départ; la caméra est attachée à sa hauteur de vue. Les contrôles de vol du placeholder sont ceux du script existant : souris pour regarder, `WASD` pour se déplacer, `Space` pour monter, `Ctrl` pour descendre et `Shift` pour accélérer.

## Mettre à niveau une scène existante

Sélectionnez la racine du test de livraison, par exemple `Delivery - Selection Test` dans `TataScene`, ou l'un de ses enfants, puis choisissez **Tools > Delivery > Upgrade Selected Delivery Test**. La commande retrouve la session, le sélecteur et le placeholder du joueur, puis ajoute les composants et éléments UI manquants. Les offres, leur ordre, les réglages de mouvement et les références déjà renseignées dans les composants UI sont conservés. Faites **Undo** si vous souhaitez annuler le montage; sauvegardez la scène manuellement pour garder le résultat.

## Jouer

Cliquez sur **Start** pour lancer le chronomètre, puis volez dans un cercle de départ et ralentissez pour accepter l'offre. Suivez la flèche vers le cercle de destination; arrêtez-vous dedans pour déposer le colis. Les boutons **Restart** et **Pause / Resume** contrôlent la session.

Dans la scène créée, la souris est libre au démarrage pour cliquer sur **Start**. Cliquez dans la fenêtre Game pour capturer la souris et regarder autour de vous; appuyez sur `Escape` pour la libérer et cliquer sur les boutons. Quand la session est en pause, le contrôleur ne reprend pas la capture automatiquement, ce qui permet de cliquer sur **Resume**. Une mise à niveau conserve le réglage de capture d'un contrôleur déjà présent.

## Vérification dans l'éditeur

- Confirmez que le HUD affiche le chronomètre, l'objectif, le cargo, la progression et le nombre de livraisons.
- Confirmez que la flèche de guidage et la distance apparaissent après une prise en charge et pointent vers la destination.
- Vérifiez que la livraison se termine après l'arrêt dans le cercle de destination, puis que le sélecteur propose la suite.
- Testez la pause, la reprise, le redémarrage et la fin du chronomètre.
- Dans une scène configurée, vérifiez que la mise à niveau ne remplace pas vos offres ni les références UI déjà renseignées.

Ces étapes sont des vérifications à effectuer dans l'éditeur Unity; elles n'ont pas été jouées automatiquement.

## Étape 4 — récupération et dépôt

`DeliveryManager` gère les états `Idle`, `PickingUp`, `Carrying`, `DroppingOff` et `Renewing`. Il utilise le joueur, les offres et la mesure de vitesse du sélecteur. Par défaut, rester à l'arrêt une seconde dans un départ accepte une offre; rester 0,75 seconde à l'arrêt dans sa destination déclenche le dépôt. Les durées et le seuil de vitesse sont ajustables dans l'Inspector. La détection utilise le rayon horizontal du point et la tolérance verticale du sélecteur : voler au-dessus du cercle ne suffit pas.

Les modes de récupération et de dépôt sont indépendants :

- `Instant` termine l'interaction automatiquement après le déclenchement.
- `DebugDelay` simule une animation ou un dialogue pendant la durée configurée, une seconde par défaut.
- `External` attend un appel à `CompleteInteraction(int token)`.

Pour brancher une animation ou un dialogue, écoutez `OnPickupStarted` ou `OnDropOffStarted`. Conservez le token reçu par cet événement et transmettez **ce même token** à `CompleteInteraction` à la fin de la séquence. Ne relisez pas `CurrentInteractionToken` depuis un ancien callback : il pourrait appartenir à une nouvelle livraison. Les événements `OnPickedUp`, `OnDelivered` et `OnCancelled` transmettent l'offre concernée pour vos réactions de gameplay.

En mode `External`, le bouton Odin **Complete Interaction** permet de tester sans écrire de script : recopiez le `Current Interaction Token` affiché dans son argument. Un ancien token est ignoré après annulation, redémarrage ou fin de session. Une validation reçue pendant la pause attend la reprise. Pendant un dépôt différé, quitter la zone ou repartir trop vite annule cette interaction et conserve le colis pour une nouvelle tentative.

Tests manuels :

1. Traverser un départ trop vite ne doit pas accepter l'offre. S'arrêter dedans doit remplir la progression, puis afficher le colis.
2. S'arrêter dans un autre point ne doit pas déposer le colis. La bonne destination doit terminer la livraison une seule fois.
3. Refaire le parcours en `DebugDelay`, puis en `External`; la livraison doit attendre la fin de la séquence.
4. Pendant un dépôt différé, quitter la zone : revenir doit permettre de retenter le dépôt avec un nouveau token.

## Étape 5 — guidage et retours

`DeliveryGuidance` affiche un cercle jaune à destination, un repère avec sa distance et une flèche en bord d'écran lorsque la destination est hors champ. `DeliveryHUD` affiche l'objectif, le colis, la progression et le nombre de livraisons. Les notifications de récupération, dépôt, annulation et fin de temps utilisent une courte animation DOTween.

Les sons sont facultatifs : renseignez l'`Audio Source` et les clips `Pickup`, `Delivery` et `Cancel` sur `DeliveryHUD` pour les activer. Aucun son ni dialogue de contenu n'est fourni.

Tests manuels :

1. Accepter une offre, puis regarder à l'opposé : le repère doit rester visible au bord de l'écran.
2. Se rapprocher de la destination : la distance doit diminuer et le cercle jaune doit désigner le bon point.
3. Vérifier les textes et notifications à la récupération, au dépôt et à la fin du temps, y compris en redimensionnant la fenêtre Game.
4. Appuyer sur `Escape`, cliquer **Pause / Resume**, puis reprendre : le timer et les progressions doivent rester figés pendant la pause. Cliquer un bouton ne doit pas capturer la souris; cliquer ailleurs dans Game la capture.

## Étape 6 — renouvellement et session complète

Une seconde après un dépôt, les offres configurées redeviennent disponibles. La dernière livraison terminée est écartée tant qu'une autre offre valide est disponible. Avec une seule offre, elle peut être rejouée. Le système réutilise les trajets renseignés; il ne génère pas de nouveaux points dans la ville.

Tests manuels :

1. Enchaîner cinq livraisons : le compteur doit augmenter exactement une fois par dépôt, les départs doivent revenir après le délai et la dernière offre ne doit pas être proposée immédiatement si une autre existe.
2. Réduire la durée de session à dix secondes avant Play et laisser le temps expirer pendant une récupération ou un dépôt différé : aucun dépôt tardif ne doit être compté et les marqueurs doivent disparaître.
3. En mode `External`, noter un token, redémarrer, accepter une nouvelle offre et soumettre l'ancien token : il ne doit pas terminer la nouvelle interaction.
4. Redémarrer après plusieurs livraisons : timer, compteur, colis et progression doivent repartir à zéro ou à leur valeur initiale.
5. Tester avec une seule offre puis aucune : une seule doit pouvoir se répéter; aucune doit afficher `No deliveries available` sans erreur.
6. Relancer la commande de mise à niveau : elle doit réutiliser les composants et ne pas ajouter un deuxième timer ni plusieurs gestionnaires.

Le montage complet conserve le contrôleur de vol de test. Le branchement sur le déplacement définitif du pigeon reste une étape distincte.
