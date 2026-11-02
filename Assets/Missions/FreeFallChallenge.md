# Chute libre multijoueur

Dans `MainCityScene`, l'objet **Free Fall Challenge** contient le départ. Celui-ci reprend le cercle, l'invite F/Y et le décompte de la course. Son enfant `Start/Drop Pose` se trouve 60 m plus haut ; déplacer cet enfant pour régler la hauteur de lâcher. Placer le cercle sur une surface accessible dans la vue Scene.

Les joueurs prêts sont téléportés ensemble au point de lâcher, en conservant leur décalage horizontal. Le pigeon part à vitesse nulle, comme au sommet d'une montée, puis décroche et accélère verticalement sous la seule gravité. Il n'a ni plané, ni portance, ni vitesse horizontale ; les impulsions et courants ascendants sont ignorés pendant la chute. L'animation Fall existante remplace le plané. Les joueurs non inscrits restent libres. Chaque participant termine sa tentative séparément, même s'il est le dernier à tomber. La manche passe aux résultats une fois toutes les tentatives terminées ; le dernier appui valide obtient la première place. Les éliminations durant le même tick sont ex æquo. À un seul joueur, l'activité permet de s'entraîner jusqu'au premier appui ou contact.

## Règles et record

- La chute verticale est autorisée. Battre des ailes, diriger le pigeon, freiner, piquer ou faire un tonneau élimine le participant et rétablit immédiatement son vol habituel. La caméra et le roucoulement restent utilisables.
- La zone morte des commandes se règle sur `Movement Dead Zone` (0,15 par défaut).
- Le contact avec le sol, un toit, un support ou l'eau élimine aussi. Une réapparition vaut abandon.
- La hauteur est la distance verticale entre le pivot du pigeon et la première surface sous lui. Les triggers et le layer Player sont exclus. `Ground Layers` contient Default, Water et PigeonOnly ; ajouter les layers des nouveaux décors si nécessaire.
- La plus petite hauteur positive atteinte avant la commande d'élimination est retenue. Les commandes sont relevées par image : leur cause et leur délai à l'intérieur du pas réseau sont transmis, puis la simulation avance jusqu'à cet appui avant de relever la hauteur. Cela réduit les paliers dus aux 20 ticks/s sans modifier la fréquence réseau globale. L'affichage des hauteurs en cours de chute est interpolé ; le record reste calculé dans la simulation, sans accepter de hauteur fournie par le client.
- Toucher le sol ou l'eau avant l'appui, demander une réapparition ou atteindre le délai de sécurité invalide la tentative entière pour le record. Un contact après l'appui ne remplace pas la tentative déjà terminée. Une tentative invalide n'affiche pas de hauteur finale valide.
- Le record est partagé par la session et enregistré dans les PlayerPrefs de l'hôte après confirmation de l'état réseau. Une valeur plus petite bat le record. La clé par défaut dépend du nom de la scène et de l'activité ; renseigner `Record Key` pour conserver le record après un renommage.
- Les résultats se quittent individuellement avec F/Y. La zone rouvre une fois tous les participants sortis ou après 90 s. Une chute est limitée à 120 s pour libérer une manche bloquée ; ces durées sont réglables.

## Vérification dans Unity

1. Ouvrir `MainCityScene`, vérifier le cercle de départ et placer `Drop Pose` au-dessus d'un espace dégagé. Vérifier les layers des surfaces sous le lâcher.
2. Lancer un hôte et un client. Se poser dans le nouveau cercle et appuyer sur F/Y avec les deux joueurs avant la fin du décompte. Vérifier le départ en hauteur, les décalages conservés et les HUD sur les deux écrans.
3. Ne rien toucher : le pigeon doit partir immobile puis tomber de plus en plus vite, à X/Z constants en l'absence de collision, avec l'animation de chute et sans plané. Vérifier aussi un lâcher après une arrivée rapide : aucun élan initial ne doit subsister. Bouger légèrement la caméra doit rester autorisé. Avec un stick, un bruit inférieur à la zone morte doit rester sans effet.
4. Battre des ailes sur le client : sa hauteur est figée et il attend ; l'hôte continue de tomber et sa hauteur continue de descendre. Aucun stationnaire automatique ni écran de résultats tant que l'hôte n'a pas terminé. Faire ensuite un appui avec l'hôte : les deux voient les résultats, avec deux hauteurs propres à leurs appuis. Refaire en dirigeant, freinant, piquant et faisant un tonneau.
5. Refaire en touchant le sol ou l'eau : élimination, aucune amélioration du record. Une réapparition doit aussi être exclue du record.
6. En solo, varier progressivement le moment de l'appui à proximité du sol : les hauteurs ne doivent plus être limitées aux paliers d'un tick de chute entière. Le contact avant l'appui doit toujours invalider l'essai. Attendre plus bas avant de battre des ailes : le record doit diminuer. Relancer la session avec le même hôte pour vérifier sa conservation. Les records déjà enregistrés ne sont pas effacés par cette correction.
7. Avec trois participants, vérifier que le premier éliminé peut voler librement en attendant les autres. Tester des éliminations simultanées, un spectateur qui arrive en cours de manche et une déconnexion.
8. Quitter les résultats avec un seul joueur : l'autre garde son écran. Quitter avec le dernier : le départ rouvre, et les autres missions redeviennent disponibles.

La compilation C# et les contrôles de règles hors éditeur ne valident pas la physique, la présentation ou la synchronisation en partie réelle. Ces vérifications Play Mode restent nécessaires.
