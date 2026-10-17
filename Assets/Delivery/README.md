# Livraison — étape 1 : lieux, contenus et offres

Cette étape prépare les données de la boucle de livraison. Elle ne prend aucune livraison,
ne déplace pas le pigeon et ne calcule ni score ni chronomètre.

## Configuration

- **DeliveryPoint** : composant d'un lieu de scène. Configurer son nom et son rayon,
  déplacer son Transform pour définir sa position. Le rayon est exprimé en unités monde,
  indépendamment de l'échelle du Transform. Chaque lieu expose `DisplayName`, `Position`
  et `Radius` en lecture seule.
- **DeliveryData** : asset créé par **Assets > Create > Delivery > Delivery Data**.
  Renseigner le libellé et le contenu. Cet asset contient seulement du texte et ne
  référence aucun lieu de scène ; il peut être réutilisé par plusieurs offres.
- **DeliveryOffer** : composant de scène liant un DeliveryData, un départ et une destination.
  Créer autant d'objets avec ce composant que d'offres souhaitées. Pour préparer la future
  sélection par arrêt dans un cercle, configurer une offre par départ actif. Cette convention
  n'est pas imposée par le code à cette étape ; aucun arbitrage entre doublons n'est ajouté.

Les champs de configuration sont privés et sérialisés. Les propriétés publiques sont en
lecture seule. Odin affiche une erreur de configuration et `IsValid` dans l'Inspector.
Le premier problème rencontré est affiché ; le suivant apparaît après correction.
Les points, les données et les offres se valident sans entrer en Play.

`DeliveryOffer.IsAvailable` est vrai si l'offre et ses deux lieux sont actifs et activés,
et si toutes leurs données sont valides. Une référence absente renvoie faux sans exception.
Cette propriété est recalculée à la consultation. Elle n'est pas un état de progression :
aucune offre n'est consommée ou renouvelée dans cette étape.
Un futur système pourra consulter les composants de scène et leurs propriétés ;
il n'y a pas de singleton ni de gestionnaire global imposé ici.

## Créer le setup reproductible

1. Ouvrir une scène normale dans Unity 6000.6.6f1, hors Play et hors mode Prefab.
2. Choisir **Tools > Delivery > Créer les 3 lieux et 3 offres de test**.
3. La racine **Delivery - Test étape 1** est ajoutée à la scène courante, sélectionnée
   et cadrée dans la vue Scene. Déplier **Lieux** et **Offres simultanées**.
4. Activer **Gizmos** dans la vue Scene. Les trois lieux sont Poste `(0, 0, 0)`,
   Marché `(12, 0, 0)` et Atelier `(6, 0, 10)`, avec un rayon de `2`.
5. Les trois offres utilisent les assets fournis dans `Assets/Delivery/Examples` :
   Lettre : Poste → Marché ; Petit colis : Atelier → Poste ; Télégramme : Marché → Atelier.

Le menu ajoute uniquement des objets sous une nouvelle racine. Il ne remplace aucun objet
existant et ne sauvegarde pas la scène. Un seul **Undo** retire toute la racine créée ;
**Redo** la restaure avec ses références. Relancer le menu ajoute une autre racine indépendante.
Les trois assets exemple sont fournis dans le projet : le menu les lit sans les modifier.
Il est désactivé pendant le Play ou en mode Prefab.

Les disques et noms sont des **gizmos de la vue Scene**, dans le plan local XZ de chaque lieu.
Orienter le Transform oriente le disque ; il s'agit uniquement d'un repère de configuration.
Ces repères ne créent ni collision ni règle de distance, et ne constituent pas les cercles
lumineux de gameplay prévus à l'étape 3. Aucun rendu Game n'est ajouté ici.

## Tests manuels et résultats attendus

| Manipulation | Résultat attendu |
| --- | --- |
| Créer le setup puis sélectionner successivement les trois offres | `Is Valid` et `Is Available` vrais pour chacune ; trois offres disponibles en même temps, chacune depuis un lieu différent. |
| Passer en Play sans ajouter d'autre système | Les trois offres restent consultables et disponibles ; aucune livraison ne démarre seule. |
| Vider Destination sur Lettre | Erreur explicite dans l'Inspector ; `Is Valid` et `Is Available` faux. Les deux autres offres restent disponibles. |
| Affecter Poste comme départ **et** destination de Lettre | Erreur « deux lieux différents » ; offre invalide et indisponible. |
| Vider Data sur une offre | Erreur de données manquantes ; aucune exception lors de la consultation des indicateurs. |
| Effacer le libellé ou le contenu d'un asset DeliveryData | Erreur sur l'asset et sur l'offre qui le référence ; offre invalide. Restaurer ensuite le texte. |
| Effacer le nom d'un lieu | Erreur sur le lieu et sur les offres qui le référencent. Restaurer ensuite le nom. |
| Déplacer un lieu puis changer son rayon | `Position` et le disque suivent la configuration, sans déplacer les autres lieux. |
| Mettre un rayon nul ou négatif via l'Inspector en mode Debug | `Is Valid` faux et erreur de rayon ; aucun disque invalide dessiné. L'Inspector Odin normal limite la saisie à 0,01. |
| Désactiver un objet offre | Il reste configuré correctement mais `Is Available` devient faux. |
| Désactiver Poste ou son composant DeliveryPoint | Lettre et Petit colis deviennent indisponibles, leurs configurations restent valides. Télégramme reste disponible. Réactiver rétablit les trois offres. |
| Après création, faire Undo puis Redo avant toute autre modification | La racine entière disparaît puis revient avec trois lieux et trois offres correctement référencées. |
| Relancer le menu de setup | Une seconde racine apparaît ; chaque groupe d'offres référence les lieux de sa propre racine. |

Les erreurs de configuration sont affichées dans l'Inspector ; elles ne remplissent pas
la Console à chaque frame. Les composants ne possèdent pas de méthode Update.

## Vérifications de cette livraison de code

Effectuées : relecture statique du code, inspection des changements, contrôle des fichiers
`.meta` et des GUID des trois assets exemple. Compilation hors éditeur des scripts runtime
et Editor contre les références Unity locales, puis compilation runtime sans `UNITY_EDITOR` :
aucune erreur (avertissements CS0649 attendus sur les champs renseignés dans l'Inspector).
Aucune scène existante n'est modifiée.

À effectuer dans Unity : import et compilation du projet complet, affichage Odin et gizmos, création du setup,
Undo/Redo et tableau de tests ci-dessus. L'éditeur n'a pas été lancé par cette implémentation.
