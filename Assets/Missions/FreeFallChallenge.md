# Chute libre multijoueur

Dans `MainCityScene`, l'objet **Free Fall Challenge** se trouve 7 m à droite du départ de **Timed Race**. Le départ reprend le cercle, l'invite F/Y et le décompte de la course. Son enfant `Start/Drop Pose` se trouve 60 m plus haut ; déplacer cet enfant pour régler la hauteur de lâcher. Vérifier le placement du nouveau cercle sur le toit dans la vue Scene.

Les joueurs prêts sont téléportés ensemble au point de lâcher, en conservant leur décalage horizontal. Le pigeon part à vitesse nulle, comme au sommet d'une montée, puis décroche et accélère verticalement sous la seule gravité. Il n'a ni plané, ni portance, ni vitesse horizontale ; les impulsions et courants ascendants sont ignorés pendant la chute. L'animation Fall existante remplace le plané. Les joueurs non inscrits restent libres. Le dernier participant encore sans commande gagne ; les éliminations durant le même tick sont ex æquo. À un seul joueur, l'activité permet de s'entraîner jusqu'au premier appui ou contact.

## Règles et record

- La chute verticale est autorisée. Battre des ailes, diriger le pigeon, freiner, piquer ou faire un tonneau élimine le participant et rétablit immédiatement son vol habituel. La caméra et le roucoulement restent utilisables.
- La zone morte des commandes se règle sur `Movement Dead Zone` (0,15 par défaut).
- Le contact avec le sol, un toit, un support ou l'eau élimine aussi. Une réapparition vaut abandon.
- La hauteur est la distance verticale entre le pivot du pigeon et la première surface sous lui. Les triggers et le layer Player sont exclus. `Ground Layers` contient Default, Water et PigeonOnly ; ajouter les layers des nouveaux décors si nécessaire.
- La plus petite hauteur positive atteinte pendant une tentative sans contact est retenue, avant l'effet de la commande d'élimination. Le dernier survivant valide aussi sa tentative. Un contact avec le sol ou l'eau, une réapparition ou le délai de sécurité invalide la tentative entière pour le record.
- Le record est partagé par la session et enregistré dans les PlayerPrefs de l'hôte après confirmation de l'état réseau. Une valeur plus petite bat le record. La clé par défaut dépend du nom de la scène et de l'activité ; renseigner `Record Key` pour conserver le record après un renommage.
- Les résultats se quittent individuellement avec F/Y. La zone rouvre une fois tous les participants sortis ou après 90 s. Une chute est limitée à 120 s pour libérer une manche bloquée ; ces durées sont réglables.

## Vérification dans Unity

1. Ouvrir `MainCityScene`, vérifier le cercle de départ et placer `Drop Pose` au-dessus d'un espace dégagé. Vérifier les layers des surfaces sous le lâcher.
2. Lancer un hôte et un client. Se poser dans le nouveau cercle et appuyer sur F/Y avec les deux joueurs avant la fin du décompte. Vérifier le départ en hauteur, les décalages conservés et les HUD sur les deux écrans.
3. Ne rien toucher : le pigeon doit partir immobile puis tomber de plus en plus vite, à X/Z constants en l'absence de collision, avec l'animation de chute et sans plané. Vérifier aussi un lâcher après une arrivée rapide : aucun élan initial ne doit subsister. Bouger légèrement la caméra doit rester autorisé. Avec un stick, un bruit inférieur à la zone morte doit rester sans effet.
4. Battre des ailes sur le client : il est éliminé, l'autre gagne et les deux voient les mêmes résultats. Refaire en dirigeant, freinant, piquant et faisant un tonneau.
5. Refaire en touchant le sol ou l'eau : élimination, aucune amélioration du record. Une réapparition doit aussi être exclue du record.
6. En solo, attendre plus bas avant de battre des ailes : le record doit diminuer. Relancer la session avec le même hôte pour vérifier sa conservation.
7. Avec trois participants, vérifier que le premier éliminé peut voler librement en attendant les autres. Tester des éliminations simultanées, un spectateur qui arrive en cours de manche et une déconnexion.
8. Quitter les résultats avec un seul joueur : l'autre garde son écran. Quitter avec le dernier : le départ rouvre, et les autres missions redeviennent disponibles.

La compilation C# et les contrôles de règles hors éditeur ne valident pas la physique, la présentation ou la synchronisation en partie réelle. Ces vérifications Play Mode restent nécessaires.
