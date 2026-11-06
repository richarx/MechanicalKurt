#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using Unity.Cinemachine;

public partial class ScenarioRunner
{
    IEnumerator PrecisionZone()
    {
        PrecisionCameraZone zone = FindAnyObjectByType<PrecisionCameraZone>();
        yield return Check("Une zone de caméra de précision existe, en plusieurs colliders", zone != null && zone.ColliderCount >= 2,
            zone != null ? $"{zone.name}, {zone.ColliderCount} colliders" : "absente");
        CameraSwapper swapper = Pigeon.GetComponent<CameraSwapper>();
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();

        Collider[] colliders = zone.GetComponentsInChildren<Collider>();
        foreach (Collider part in colliders)
        {
            yield return Check($"{part.name} : trigger, ne gêne pas le vol", part.isTrigger);
            Bot.Brake = true;
            yield return Teleport(Pigeon, part.bounds.center, Quaternion.identity, $"Dans {part.name}");
            yield return Expect($"{part.name} : caméra de précision sans appuyer sur rien",
                () => swapper.Current == CameraSwapper.CameraType.Precision && (Object)brain.ActiveVirtualCamera == swapper.CurrentCamera, 2f,
                () => $"caméra {swapper.Current}, active {brain.ActiveVirtualCamera?.Name}, état {Pigeon.CurrentPigeonState}, position {Pigeon.transform.position}");
        }

        Vector3 inZone = colliders[0].bounds.center;
        yield return Check("Dans la zone : forcée en vol, pas au sol", PrecisionCameraZone.Forces(inZone, false) && !PrecisionCameraZone.Forces(inZone, true));
        Bot.Brake = false;
        bool hasFloor = Physics.Raycast(inZone, Vector3.down, out RaycastHit floor, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        yield return Check("Un sol sous la zone", hasFloor, $"depuis {inZone}");
        yield return Teleport(Pigeon, floor.point + Vector3.up * 0.4f, Quaternion.identity, "Posé dans la zone");
        yield return ExpectLanded(Pigeon, 4f);
        yield return Expect("Au sol : retour à la caméra normale", () => Pigeon.IsGrounded && swapper.Current != CameraSwapper.CameraType.Precision, 2f,
            () => $"caméra {swapper.Current}, état {Pigeon.CurrentPigeonState}, position {Pigeon.transform.position}, dans la zone {zone.Contains(Pigeon.transform.position)}");
        Capture(Camera.main, "precision_zone_ground");
        Bot.Brake = true;

        yield return Teleport(Pigeon, colliders[0].bounds.center + Vector3.up * 300f, Quaternion.identity, "Loin au-dessus de la zone");
        yield return Expect("Hors de la zone : retour à l'orbitale", () => swapper.Current == CameraSwapper.CameraType.Orbital, 2f, () => $"caméra {swapper.Current}");
        Bot.Brake = false;
    }

    IEnumerator BatchIsSilent()
    {
        yield return Check("Lancé en batch : son coupé", !Application.isBatchMode || AudioListener.volume == 0f, $"volume {AudioListener.volume}");
        yield return Check("Mais les sons continuent de jouer, sans sortir", !AudioListener.pause);
    }

    IEnumerator MobileTouchControls()
    {
        MobileControls controls = MobileControls.Create();
        Defer(() => { TiltBridge.SetForTests(false, 0f, 0f); Destroy(controls.gameObject); });
        TiltBridge.SetForTests(false, 0f, 0f);
        yield return Expect("Boutons affichés en jeu", () => controls.IsGameplayShown, 1f);
        yield return Check("Sans inclinaison : joystick à l'écran", controls.IsJoystickShown);
        yield return new WaitForSecondsRealtime(0.2f);
        CaptureCanvas(controls.GetComponent<Canvas>(), "mobile_joystick");

        // Du bouton à l'interface du pigeon : la manette virtuelle remplace le joueur.
        Pigeon.SetControlsOverride(null);
        Defer(() => Pigeon.SetControlsOverride(Bot));
        IPigeonControls input = Pigeon.GetComponent<PlayerControls>();
        var pointer = new PointerEventData(EventSystem.current) { position = Vector2.zero };

        yield return PressAndCheck(controls.FlapButton, pointer, "Flap", () => input.Flap);
        yield return PressAndCheck(controls.DiveButton, pointer, "Dive", () => input.Dive);
        yield return PressAndCheck(controls.BrakeButton, pointer, "Brake", () => input.Brake);

        TiltBridge.SetForTests(true, 20f, 30f);
        TiltBridge.Recenter();
        yield return Expect("Avec inclinaison : joystick caché", () => !controls.IsJoystickShown, 1f);
        CaptureCanvas(controls.GetComponent<Canvas>(), "mobile_tilt");
        yield return Expect("Pencher à droite : le pigeon tourne à droite", () => input.RelativeMovement.x > 0.5f && Mathf.Abs(input.RelativeMovement.y) < 0.2f, 1f,
            () => $"commande {input.RelativeMovement}, stick {TiltBridge.Stick}");
        TiltBridge.SetForTests(true, 0f, 10f);
        yield return Expect("Pencher vers l'avant : il pique", () => input.RelativeMovement.y > 0.5f, 1f,
            () => $"commande {input.RelativeMovement}, stick {TiltBridge.Stick}");
        TiltBridge.SetForTests(true, 2f, 28f);
        yield return Expect("Petit mouvement : rien ne bouge (zone morte)", () => input.RelativeMovement.magnitude < 0.05f, 1f,
            () => $"commande {input.RelativeMovement}, stick {TiltBridge.Stick}");
    }

    IEnumerator PressAndCheck(OnScreenButton button, PointerEventData pointer, string label, System.Func<bool> pressed)
    {
        button.OnPointerDown(pointer);
        yield return Expect($"Bouton {label} appuyé", pressed, 1f);
        button.OnPointerUp(pointer);
        yield return Expect($"Bouton {label} relâché", () => !pressed(), 1f);
    }

    /// <summary>Photo de la vue du joueur avec ce canvas par-dessus.</summary>
    static void CaptureCanvas(Canvas canvas, string label)
    {
        Camera camera = Camera.main;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = camera.nearClipPlane + 0.05f;
        Canvas.ForceUpdateCanvases();
        Capture(camera, label);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
#endif
