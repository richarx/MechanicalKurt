# Delivery prototype — steps 1 and 2

Pour le prototype complet jusqu'à l'étape 6, voir [le guide de playtest](PLAYTEST.md).
Les sections ci-dessous documentent les étapes initiales.

Unity **6000.6.6f1**, using the existing Odin Inspector and TextMesh Pro packages. No packages
are added. UniTask and DOTween are not needed for this countdown.

## Step 1: points, content and offers

- **DeliveryPoint**: scene component with a display name, position and world-space radius.
  Move its Transform to position it. Radius is independent of Transform scale.
- **DeliveryData**: reusable text asset, created with **Assets > Create > Delivery > Delivery Data**.
  Fill in label and content. It does not reference scene objects.
- **DeliveryOffer**: scene component linking a data asset, a departure point and a different
  destination point. For future circle selection, use one offer per active departure; this
  convention is not enforced yet.

Configuration fields are private and serialized; public properties are read-only. Odin shows
the first configuration error and `IsValid` outside Play mode. Fixing it reveals the next error.
`DeliveryOffer.IsAvailable` requires the offer and both points to be valid, active and enabled.
Missing references return false safely. Availability is calculated when read and does not
represent delivery progress. It is independent of session state: future interactions must also
check **session.CanInteract**. No offer is accepted, consumed or renewed yet.

### Reproducible points and offers setup

1. Open a normal scene outside Play mode and Prefab mode.
2. Choose **Tools > Delivery > Create Points and Offers Test**.
3. Expand **Delivery - Points and Offers Test**, **Points** and **Simultaneous Offers**.
4. Enable Scene view Gizmos. Points are **Post Office** `(0, 0, 0)`, **Market** `(12, 0, 0)`
   and **Workshop** `(6, 0, 10)`, each with radius `2`.
5. Inspect three offers: Letter Post Office to Market; Small Parcel Workshop to Post Office;
   Telegram Market to Workshop. All three can be available simultaneously.

Original example filenames (`Lettre.asset`, `PetitColis.asset`, `Telegramme.asset`) and GUIDs
are preserved. Labels and contents are English. The setup reads these assets without modifying them.
Previously instantiated step 1 scene objects retain their serialized names and point labels.
Recreate the example setup to obtain English scene labels; no existing scene is migrated.
Discs and names are Scene view gizmos on each point's local XZ plane. They create no Game view
visuals, collisions or selection rules.

### Step 1 manual checks

| Action | Expected result |
| --- | --- |
| Select each example offer before and during Play | All three valid and available; none starts itself. |
| Clear an offer's Data or Destination | English Inspector error; invalid and unavailable; no exception. Other offers remain valid. |
| Use the same point as departure and destination | Invalid and unavailable; different-points error. |
| Clear a data label/content or a point name | Error on that asset/point and all referencing offers. Restore the text afterward. |
| Move a point or change radius | Position and disc follow configuration without moving other points. |
| Set zero/negative radius using Debug Inspector | Invalid point; no invalid disc. Normal Odin Inspector limits entry to 0.01. |
| Disable an offer | Configuration remains valid; availability becomes false. |
| Disable Post Office or its DeliveryPoint | Letter and Small Parcel unavailable; Telegram remains available. Re-enable to restore all three. |

## Step 2: session and global timer

**DeliverySession** owns one explicit scene session, without a singleton or automatic start.
Set **Duration Seconds** in its Inspector: finite and greater than zero. Duration is captured
on start; editing configuration while Running does not alter the active countdown. Reset/restart
applies the configured duration. **DeliverySessionClock** is the small pure C# state/time helper.

State is **Ready**, **Running**, or **Finished**. Remaining time never goes below zero.
`IsRunning` additionally requires the component and GameObject to be enabled.
`CanInteract` additionally requires the session to be unpaused: future pickup/delivery code must
use **CanInteract**, because IsRunning alone does not block paused interactions.
The session runs at execution order `-100`, before ordinary Update interaction components.
No navigation, active deliveries, selection circles, score or rewards are added here.

### Controls and events

Public controls can be connected to UnityEvents; Odin displays their buttons during Play:

| Method / button | Result |
| --- | --- |
| `StartSession` / **Start Session** | Full countdown from Ready or Finished; emits On Started. Repeated start while Running is ignored. |
| `RestartSession` / **Restart Session** | Clears elapsed time and explicit pause; emits On Reset in Ready, then starts a full countdown and emits On Started. |
| `ResetSession` / **Reset Session** | Ready with configured duration; clears explicit pause and emits On Reset, without On Ended. |
| `SetPaused(bool)` / **Toggle Pause** | Pauses/resumes a running session without changing global time scale. |

Expiration sets Finished and zero remaining **before** **On Ended**, once per expired session.
Reset/restart are not expiration. Controls called from inside the session's own event listeners
are ignored, including pause controls: no recursive events or accidental immediate restarts.
Restart's On Reset listener may disable/destroy the session; the subsequent start is cancelled.
To restart after On Ended, use later external input or a later frame.

Controls are runtime-only. Start/restart require an active enabled component and valid duration.
Invalid starts do nothing; Odin displays the configuration error. Reset works while disabled
and safely displays zero if configured duration is invalid.

Time uses `Time.unscaledDeltaTime`: slow motion consumes real seconds. `Time.timeScale == 0`
freezes time and interaction, as does explicit pause. Restoring time scale does not clear an
explicit pause; clearing explicit pause does not override zero time scale. This subsystem never
writes `Time.timeScale`.

Disabling the session component/GameObject suspends time and immediately makes IsRunning and
CanInteract false. Stored Running state, remaining time and explicit pause are retained.
Re-enabling resumes from that time, respecting both pause sources. No disabled time is deducted;
enable/disable emits no session event. Disabling only the HUD hides the display without pausing.

**DeliverySessionHUD** references a session and two TMP texts. It displays time rounded up to a
whole second, plus **Ready**, **Running**, **Paused** or **Time's up**. Missing/disabled/invalid
configuration has explicit English status messages. Refresh runs on enable and in LateUpdate.

### Reproducible 15-second session setup

1. Open a normal scene outside Play mode and Prefab mode.
2. Choose **Tools > Delivery > Create Session Test**. Step 1 setup is not required.
3. Select **Delivery - Session Test (15 seconds)**: DeliverySession has duration 15 and child
   **Session HUD** is a connected overlay Canvas with TMP timer/state texts.
4. Enter Play; expect **00:15 / Ready** in Game view.
5. Press **Start Session** in the root's Odin Inspector.
6. Expect Running, a visible countdown, then **00:00 / Time's up** after about 15 real seconds.

Setup explicitly uses the existing font at
`Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`.
Missing font aborts setup with a single clear error. No EventSystem is needed for this display-only
Canvas. Controls are Inspector buttons; no menu screen is created.

Both setup menus create a new independent root, leave existing objects alone, mark the scene dirty
and never save it to disk. One **Undo** removes the entire setup; **Redo** restores references.
Repeated menu use creates another independent root. Menus are disabled in/entering Play mode and
in Prefab mode.

### Deterministic clock checks

Choose **Tools > Delivery > Run Session Clock Checks**. Success logs
**Delivery session clock checks passed.** Failure throws a descriptive exception. Checks use
controlled elapsed time without waiting, scene changes, global time changes or extra frameworks:
Ready/start, partial countdown, repeated start, explicit/external pause, exact expiration,
overshoot clamped to zero, unique expiration, start after Finished, reset/restart, invalid durations
and invalid elapsed values.

For an external harness, compile `Runtime/DeliverySessionClock.cs` and
`Editor/DeliverySessionClockChecks.cs` without `UNITY_EDITOR`, then call
`DeliverySessionClockChecks.RunAll()`. Only the menu wrapper depends on Unity.

### Step 2 manual checks

| Action | Expected result |
| --- | --- |
| Run the 15-second setup | Ready, then Running after Start, then Finished at zero; CanInteract false. |
| Wire On Ended to a visible existing scene response and wait past 15 seconds | Response fires once; no repeated expiration. |
| Press Start during Running | Countdown continues; no extra Started event. |
| Restart halfway through or after expiration | 00:15, explicit pause cleared, Running; one Reset then one Started event. |
| Reset halfway through | Ready, 00:15 stays still until Start; no Ended event. |
| Toggle Pause, wait, toggle again | Paused, CanInteract false, time frozen; resumes afterward. |
| Use project time controls to set timeScale to 0.2 | Approximately 15 real seconds until expiration. Restore the previous scale afterward. |
| Set timeScale to zero, then restore | Time/interactions frozen at zero scale; explicit pause remains if set. |
| Disable DeliverySession, wait, re-enable | Time frozen; IsRunning/CanInteract false while disabled; resumes retained time. |
| Wire each session event back to Start/Restart/Reset/Toggle Pause on the same session | Nested controls ignored; no recursion or duplicate events. |
| Wire On Reset to disable the session, then Restart | Ready; no subsequent Started event. |
| Enter invalid duration in Debug Inspector | Inspector error; Start/Restart ignored; Reset displays Ready and zero safely. |
| Undo/Redo either setup, or create it twice | Whole setup restored with references; repeated roots independent. |

## Verification status

## Step 3: select an offer at a departure

`DeliveryOfferSelector` displays one thin LineRenderer ring at each configured available
departure whenever the session is Ready, Running or Paused. Rings use a persistent URP Unlit material;
the test setup creates `Examples/SelectionCircle.mat` once, so the player build references a real
material asset rather than relying on a runtime shader lookup. The selector has no dependency on
the camera, movement controller, UI or tween packages.

With no accepted offer, stay inside a departure's horizontal radius and within the configured
vertical tolerance at or below the maximum speed for the hold duration (defaults: 1 second,
1.5 units/second and 2 world units vertically). Leaving the circle or exceeding the speed clears
progress. Overlapping candidates keep the current eligible offer; otherwise the nearest eligible
departure wins, with Inspector list order breaking exact distance ties. One acceptance hides all
rings until session reset/restart. The selector validates session interactivity and offer validity
again immediately before invoking **On Offer Accepted (DeliveryOffer)**. It does not implement
delivery completion or offer renewal.

Speed input order is explicit: `SetExternalSpeed(float)` from a movement controller, then an
assigned Rigidbody's velocity magnitude, then Transform displacement divided by unscaled frame
time. Call `ClearExternalSpeed()` to return to Rigidbody/Transform sampling. Transform fallback
is suitable for the test placeholder and simple controllers; a controller with its own speed value
should call the public method every frame.

Choose **Tools > Delivery > Create Selection Test** in a normal scene, outside Play and Prefab
mode. It creates a 60-second session, three existing example offers and a movable **Player
Placeholder** at the Post Office. Start the session with the Odin **Start Session** button, then
move the placeholder Transform in Scene view during Play. Frame the positions with the Game camera
yourself; the setup intentionally leaves the scene camera unchanged. It marks the scene dirty and
supports Undo/Redo without saving it.

### Step 3 manual checks

| Action | Expected result |
| --- | --- |
| Start, place the player at a departure and keep still | Its ring progresses and one offer event fires after the hold duration; every ring hides. |
| Move through a ring quickly or use Rigidbody/external speed above the limit | No acceptance; any partial progress clears. |
| Begin holding, then leave the ring | Progress clears; re-entering starts over. |
| Hover above a departure beyond vertical tolerance | No candidate or progress. |
| Pause while selecting | Progress holds and circles remain visible; eligibility and speed are checked again on resume. |
| Let the timer expire | No acceptance after expiration; rings hide. |
| Reset or restart | Selection clears, rings return when the session is interactive, and one new offer may be accepted. |
| Disable an offer or a departure | It cannot be selected. |

Les scripts ont été relus et compilés hors éditeur avec les références Unity locales.
Le rendu et les interactions en Play restent à vérifier dans Unity.

Performed outside Unity: deterministic `DeliverySessionClockChecks.RunAll()` compiled and executed
with .NET — **PASS**. Runtime and Editor scripts also compiled against local Unity references.
Static review covers English text, preserved asset paths/GUIDs, metadata and setup references.
No Unity editor was launched and no scene was saved by this implementation.

Still required in Unity: full project import/compilation, running the menu wrapper, Odin
controls/events and reentrancy behavior, TMP display, scene Undo/Redo, and both manual check tables.
Pure clock checks do not exercise the MonoBehaviour, actual UnityEvent dispatch or rendered UI.

## Temporary noclip flight controls

The selection test setup also adds `DeliveryNoclipFlightController` and a real Unity `CharacterController` to **Player Placeholder**, and connects the selector, session and current main camera. Default speed is 8 units/second; holding Shift doubles it. Movement is relative to the camera's horizontal forward/right axes, with vertical movement along world up. If no camera is assigned or tagged MainCamera, forward/right world axes are used.

Controls use Unity's Input System keyboard: **WASD** moves horizontally (**W** forward, **S** backward, **A** left, **D** right), **Space** rises, **Ctrl** descends and **Shift** boosts. Input System maps these physical key positions for the active keyboard layout, including ZQSD positions on AZERTY. Diagonal movement is normalized. Movement works before session start and after finish; an explicit session pause, time scale zero, or free cursor suspends movement. The controller sends its current speed to the offer selector, so stopping permits offer selection and moving too quickly cancels it.

This test controller is deliberately noclip. `CharacterController.Move` still resolves contacts even with `detectCollisions` turned off, so the controller component remains attached but disabled while flight moves the Transform directly. This avoids collision response without changing any global layer collision settings. To use it on an already-created test player, add a CharacterController and DeliveryNoclipFlightController, disable the CharacterController, then assign its reference, the root's DeliveryOfferSelector and DeliverySession, and a movement camera. No scene is modified automatically.

### Souris : regard caméra

La caméra déjà assignée au contrôleur tourne avec le déplacement de la souris, à une sensibilité de 0,12 degré par pixel et un angle vertical limité à ±80° (réglables dans l'Inspector). La rotation initiale de la caméra est conservée au clic de reprise. Le vol clavier suit l'orientation horizontale de la caméra.

Le curseur est capturé et masqué au démarrage du contrôleur. **Échap** le libère; un clic gauche le capture de nouveau. Quand le curseur est libre, la souris ne fait pas tourner la caméra et le vol clavier s'arrête. La perte de focus libère aussi les contrôles; il faut cliquer pour les reprendre. La désactivation restaure l'état antérieur du curseur si le contrôleur le détenait encore. Une pause de session suspend le regard et le vol; cliquer pour recapturer ne reprend pas la session.

Le menu du setup assigne la caméra MainCamera sans la déplacer ni la re-parenter. Pour que la caméra suive le joueur dans un setup neuf, faites-la manuellement enfant de **Player Placeholder** et placez-la environ à `(0, 1.6, 0)` en coordonnées locales. Dans `TataScene`, la caméra est déjà enfant du joueur; ne la déplacez pas pour appliquer cette consigne.

## Dépannage de l'étape 3

Les cercles sont visibles dès l'état **Ready** et restent visibles pendant une pause; la sélection ne progresse que lorsque la session est **Running** et non en pause. Ils s'élèvent légèrement au-dessus des départs pour éviter d'être masqués par une surface au même niveau. Le composant DeliveryOfferSelector affiche maintenant son état et sa progression dans l'Inspector; une référence obligatoire manquante produit aussi une erreur explicite.

Pour réparer le setup étape 3 déjà présent sans le recréer : sélectionnez **Delivery - Selection Test** (ou un de ses enfants) hors Play, puis lancez **Tools > Delivery > Repair Selected Selection Test**. La commande répare les références manquantes et composants du setup avec Undo; elle conserve l'ordre et les références existantes dans la liste d'offres, remplit ses cases nulles, et ne sauvegarde pas la scène. Vérifiez dans l'Inspector du root que **Interaction Status** indique l'action attendue. Au lancement, les cercles sont visibles avant le démarrage. Appuyez sur **Start Session**, déplacez le joueur dans un cercle et arrêtez-vous au plus à 1,5 unité/s pendant une seconde. **Current Offer** indique l'offre acceptée; l'événement **On Offer Accepted** s'exécute et les cercles se cachent. Le dépôt à destination et la livraison complète ne font pas partie de cette étape.

### Corrections et test de régression de la sélection

L'offre candidate et l'offre acceptée sont maintenant distinctes : `CurrentOffer` conserve
l'offre acceptée jusqu'au démarrage, à la réinitialisation ou à la fin de session.
La sélection s'évalue en `LateUpdate`, après le déplacement et le timer.
Les cercles suivent la position et le rayon des départs, y compris après leur réactivation.
Leur matériau est une copie temporaire à double face : la couleur est appliquée à `_BaseColor`
pour être visible avec URP Unlit, sans modifier le matériau partagé du projet.

À vérifier en Play :

1. Démarrer la session et s'arrêter dans un cercle : sa couleur change, puis `Current Offer`
   reste renseigné et les cercles se cachent. Rester sur place ne doit pas accepter deux fois.
2. Avant acceptation, mettre en pause : la progression et les cercles restent visibles mais
   ne progressent plus. Reprendre dans le même cercle permet de terminer la sélection.
3. Déplacer un départ ou changer son rayon : son cercle suit. Désactiver puis réactiver une
   offre initialement désactivée doit également afficher son cercle.
4. Désactiver le sélecteur après acceptation puis le réactiver : l'offre reste acceptée.
   Redémarrer la session pendant que le sélecteur est désactivé : à sa réactivation, il est libre.
5. Appuyer sur Échap : le vol s'arrête. Cliquer dans Game reprend le vol et le regard.
6. Retirer la référence Player : l'Inspector affiche l'erreur sans exception répétée.

Ces scénarios et le rendu Game n'ont pas été exécutés ici. Aucune scène existante ni aucun
réglage URP global n'a été modifié pendant cette correction.
