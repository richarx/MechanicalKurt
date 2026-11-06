#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>Mesure, après la jauge d'endurance et la caméra, l'écart entre la jauge et la position du pigeon à l'écran.</summary>
[DefaultExecutionOrder(20000)]
public class StaminaFollowProbe : MonoBehaviour
{
    public StaminaDisplay Display;
    public PigeonController Pigeon;
    public Vector3 Offset;
    public float Worst;
    public int Frames;

    void LateUpdate()
    {
        if (Display == null || Pigeon == null || Camera.main == null)
            return;
        Vector3 expected = Camera.main.WorldToScreenPoint(Pigeon.transform.position) + Offset;
        Worst = Mathf.Max(Worst, Vector2.Distance(Display.transform.position, expected));
        Frames++;
    }
}

public partial class ScenarioRunner
{
    /// <summary>Lâchée en plein vol, la fiente part à la vitesse du pigeon, puis seule la gravité agit.</summary>
    IEnumerator PoopDropsLikeABomb()
    {
        CameraFrame(out Vector3 forward, out Vector3 _);
        yield return Teleport(Pigeon, Pigeon.transform.position + Vector3.up * 120f, Quaternion.LookRotation(forward), "Haut dans le ciel");
        Bot.Dive = true;
        yield return Expect("Il prend de la vitesse", () => Pigeon.Speed > 20f, 4f, () => $"vitesse {Pigeon.Speed:0.0}");
        Bot.Dive = false;
        yield return new WaitForSecondsRealtime(0.2f);

        Bot.PressShit();
        Rigidbody poop = null;
        Vector3 pigeonVelocity = Vector3.zero;
        yield return Expect("Une fiente part", () =>
        {
            pigeonVelocity = Pigeon.Velocity;
            Poop found = FindAnyObjectByType<Poop>();
            poop = found != null ? found.GetComponent<Rigidbody>() : null;
            return poop != null;
        }, 1f);
        Vector3 start = poop.linearVelocity;
        yield return Check("Elle part à la vitesse du pigeon", Vector3.Distance(start, pigeonVelocity) < pigeonVelocity.magnitude * 0.15f,
            $"fiente {start}, pigeon {pigeonVelocity}");

        float startTime = Time.time;
        yield return new WaitForSeconds(0.5f);
        yield return Check("Elle est encore en l'air", poop != null);
        float elapsed = Time.time - startTime;
        Vector3 now = poop.linearVelocity;
        yield return Check("Sa vitesse horizontale ne change pas", Vector2.Distance(new Vector2(now.x, now.z), new Vector2(start.x, start.z)) < 0.2f,
            $"au départ {start}, puis {now}");
        yield return Check("Seule la gravité la fait tomber", Mathf.Abs(start.y - now.y + Physics.gravity.y * elapsed) < 1f,
            $"vitesse verticale {start.y:0.0} puis {now.y:0.0} en {elapsed:0.00} s");
    }

    /// <summary>Invite et touches qui font avancer un dialogue.</summary>
    IEnumerator DialogPrompt()
    {
        DialogManager dialogs = DialogManager.instance;
        yield return Check("Le gestionnaire de dialogues existe", dialogs != null);
        var sequence = ScriptableObject.CreateInstance<DialogSequence>();
        Defer(() => { dialogs.CancelDialog(sequence); Destroy(sequence); });
        sequence.speaker = "Test";
        sequence.lines.Add(new DialogLine { text = "One" });
        sequence.lines.Add(new DialogLine { text = "Two" });

        dialogs.StartDialog(sequence);
        yield return Expect("Entre deux lignes : l'invite dit quoi faire", () => dialogs.ShownPromptText == "Press any key to continue", 4f,
            () => $"invite « {dialogs.ShownPromptText} »");
        dialogs.RequestConfirm();
        yield return Expect("Dernière ligne : l'invite aussi", () => dialogs.ShownPromptText == "Press any key", 4f,
            () => $"invite « {dialogs.ShownPromptText} »");
        yield return Check("Une touche quelconque fait avancer", DialogManager.CanConfirmDialog(UnityEngine.InputSystem.Key.F)
            && DialogManager.CanConfirmDialog(UnityEngine.InputSystem.Key.Space) && DialogManager.CanConfirmDialog(UnityEngine.InputSystem.Key.Enter));
        yield return Check("Mais pas le déplacement ni la pause", !DialogManager.CanConfirmDialog(UnityEngine.InputSystem.Key.W)
            && !DialogManager.CanConfirmDialog(UnityEngine.InputSystem.Key.DownArrow) && !DialogManager.CanConfirmDialog(UnityEngine.InputSystem.Key.Escape));
        dialogs.RequestConfirm();
        yield return Expect("Le dialogue se ferme", () => !dialogs.IsPlaying, 2f);
    }

    /// <summary>Marcher dans une pente, à la montée comme à la descente, sans décoller.</summary>
    IEnumerator WalkOnSlopes()
    {
        Vector3 center = Pigeon.transform.position + Vector3.up * 60f;
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Defer(() => Destroy(ramp));
        ramp.name = "[Scenario] Ramp";
        ramp.transform.SetPositionAndRotation(center, Quaternion.Euler(30f, 0f, 0f));
        ramp.transform.localScale = new Vector3(8f, 0.5f, 30f);
        Vector3 Surface(float along) => center + ramp.transform.up * 0.3f + ramp.transform.forward * along;

        yield return Teleport(Pigeon, Surface(-8f), Quaternion.identity, "En haut de la rampe");
        yield return ExpectLanded(Pigeon, 4f);

        Bot.Movement = Bot.RelativeMovement = Vector2.up;
        yield return new WaitForSecondsRealtime(0.2f);
        float startY = Pigeon.transform.position.y;
        yield return ExpectStays("Descente : il reste au sol", () => Pigeon.IsGrounded && Pigeon.CurrentPigeonState == PigeonController.MovementState.Grounded, 2f,
            () => $"état {Pigeon.CurrentPigeonState}, au sol {Pigeon.IsGrounded}, vitesse {Pigeon.Velocity}");
        yield return Check("… et il a bien descendu", Pigeon.transform.position.y < startY - 0.3f, $"hauteur {Pigeon.transform.position.y:0.00} (départ {startY:0.00})");

        Bot.Movement = Bot.RelativeMovement = Vector2.down;
        yield return new WaitForSecondsRealtime(0.5f);
        startY = Pigeon.transform.position.y;
        yield return ExpectStays("Montée : il reste au sol", () => Pigeon.IsGrounded && Pigeon.CurrentPigeonState == PigeonController.MovementState.Grounded, 2f,
            () => $"état {Pigeon.CurrentPigeonState}, au sol {Pigeon.IsGrounded}, vitesse {Pigeon.Velocity}");
        yield return Check("… et il a bien monté", Pigeon.transform.position.y > startY + 0.3f, $"hauteur {Pigeon.transform.position.y:0.00} (départ {startY:0.00})");

        Bot.Flap = true;
        yield return Expect("Battre des ailes dans la pente : il décolle", () => !Pigeon.IsGrounded, 1f, () => $"état {Pigeon.CurrentPigeonState}");
        Bot.Flap = false;
        Bot.Movement = Bot.RelativeMovement = Vector2.zero;
        yield return Teleport(Pigeon, Surface(14f), Quaternion.identity, "En bas de la rampe, près du bord");
        yield return ExpectLanded(Pigeon, 4f);
        Bot.Movement = Bot.RelativeMovement = Vector2.up;
        yield return Expect("Au bout de la rampe, il tombe dans le vide au lieu de rester collé", () => !Pigeon.IsGrounded && Pigeon.transform.position.y < Surface(15f).y - 1f, 6f,
            () => $"état {Pigeon.CurrentPigeonState}, hauteur {Pigeon.transform.position.y:0.00}");
        Bot.Movement = Bot.RelativeMovement = Vector2.zero;
    }

    IEnumerator StaminaAndHover()
    {
        PigeonStamina stamina = Pigeon.GetComponent<PigeonStamina>();
        yield return Check("Le pigeon a une endurance", stamina != null && Pigeon.UsesStamina && stamina.MaxStamina > 0f);
        Vector3 sky = Pigeon.transform.position + Vector3.up * 60f;
        CameraFrame(out Vector3 forward, out Vector3 _);
        yield return Teleport(Pigeon, sky, Quaternion.LookRotation(forward), "Le pigeon est haut dans le ciel");
        yield return Expect("Endurance pleine au départ", () => stamina.HasMaxStamina, 3f, () => $"endurance {Pigeon.Stamina:0.0}");

        Bot.Flap = true;
        yield return Expect("Il bat des ailes", () => Pigeon.CurrentPigeonState == PigeonController.MovementState.Flapping, 1f,
            () => $"état {Pigeon.CurrentPigeonState}");
        // La jauge suit le pigeon sans retard : en fin d'image, elle est sur sa position à l'écran.
        StaminaDisplay display = FindAnyObjectByType<StaminaDisplay>();
        if (display != null)
        {
            var probe = new GameObject("[Scenario] Stamina probe").AddComponent<StaminaFollowProbe>();
            probe.Display = display;
            probe.Pigeon = Pigeon;
            probe.Offset = (Vector3)typeof(StaminaDisplay).GetField("positionOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(display);
            Defer(() => Destroy(probe.gameObject));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Check("La jauge d'endurance colle au pigeon (même image)", probe.Frames > 5 && probe.Worst < 1f,
                $"écart max {probe.Worst:0.0} px sur {probe.Frames} images");
        }
        float max = stamina.MaxStamina;
        yield return Expect("Battre des ailes vide l'endurance, puis il ne peut plus", () => Pigeon.CurrentPigeonState != PigeonController.MovementState.Flapping,
            max / 10f + 3f, () => $"état {Pigeon.CurrentPigeonState}, endurance {Pigeon.Stamina:0.0}/{max}");
        yield return Check("À bout d'endurance", Pigeon.Stamina < stamina.FlapThreshold, $"endurance {Pigeon.Stamina:0.0}, seuil {stamina.FlapThreshold:0.0}");
        Bot.Flap = false;

        yield return Teleport(Pigeon, sky, Quaternion.LookRotation(forward), "Retour dans le ciel");
        Bot.Brake = true;
        yield return Expect("Frein : vol stationnaire", () => Pigeon.CurrentPigeonState == PigeonController.MovementState.Braking, 1f,
            () => $"état {Pigeon.CurrentPigeonState}");
        yield return new WaitForSecondsRealtime(1f);
        float y = Pigeon.transform.position.y;
        Bot.Flap = true;
        yield return Expect("En vol stationnaire, battre des ailes fait monter", () => Pigeon.transform.position.y > y + 1f, 7f,
            () => $"hauteur {Pigeon.transform.position.y:0.0} (départ {y:0.0})");
        yield return Check("… dans l'état vol stationnaire en battant", Pigeon.CurrentPigeonState == PigeonController.MovementState.HoverFlapping,
            $"état {Pigeon.CurrentPigeonState}");
        Bot.Flap = false;
        y = Pigeon.transform.position.y;
        Bot.RelativeMovement = new Vector2(0f, -1f);
        yield return Expect("… et tirer vers le bas fait descendre", () => Pigeon.transform.position.y < y - 1f, 3f,
            () => $"hauteur {Pigeon.transform.position.y:0.0} (départ {y:0.0})");
        Bot.RelativeMovement = Vector2.zero;
        yield return Expect("L'endurance remonte en vol stationnaire", () => Pigeon.Stamina > stamina.FlapThreshold, 8f,
            () => $"endurance {Pigeon.Stamina:0.0}");
        Bot.Brake = false;

        yield return ExpectLanded(Pigeon, 15f);
        yield return Expect("Posé : endurance pleine", () => stamina.HasMaxStamina, 1f, () => $"endurance {Pigeon.Stamina:0.0}");

        // Danse : le pigeon reste sur place même si on pousse le joystick.
        Bot.Flex = true;
        Bot.Movement = Bot.RelativeMovement = Vector2.up;
        yield return Expect("La danse passe en état Dancing", () => Pigeon.CurrentPigeonState == PigeonController.MovementState.Dancing, 1f,
            () => $"état {Pigeon.CurrentPigeonState}");
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Check("Il ne marche pas en dansant", Pigeon.Speed < 1f, $"vitesse {Pigeon.Speed:0.00}");
        Bot.Flex = false;
        yield return Expect("La danse finie, il remarche", () => Pigeon.CurrentPigeonState == PigeonController.MovementState.Grounded && Pigeon.Speed > 1f, 1f,
            () => $"état {Pigeon.CurrentPigeonState}, vitesse {Pigeon.Speed:0.00}");
        Bot.Movement = Bot.RelativeMovement = Vector2.zero;

        // Descente en vol stationnaire : posé, le joystick encore tiré ne le fait pas marcher en arrière.
        yield return Teleport(Pigeon, Pigeon.transform.position + Vector3.up * 2f, Quaternion.LookRotation(forward), "Juste au-dessus du sol");
        Bot.Brake = true;
        Bot.Movement = Bot.RelativeMovement = Vector2.down;
        yield return ExpectLanded(Pigeon, 8f);
        Bot.Brake = false;
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Check("Joystick encore tiré après la descente : il ne marche pas", Pigeon.IsGrounded && Pigeon.Speed < 1f,
            $"au sol {Pigeon.IsGrounded}, vitesse {Pigeon.Speed:0.00}");
        Bot.Movement = Bot.RelativeMovement = Vector2.zero;
        yield return new WaitForSecondsRealtime(0.2f);
        Bot.Movement = Bot.RelativeMovement = Vector2.down;
        yield return Expect("Joystick revenu : il remarche", () => Pigeon.Speed > 1f, 1f, () => $"vitesse {Pigeon.Speed:0.00}");
        Bot.Movement = Bot.RelativeMovement = Vector2.zero;

        // Effets du piqué : déplacés de la scène de main vers le prefab réseau.
        WindVelocityControl wind = Pigeon.GetComponentInChildren<WindVelocityControl>();
        yield return Check("L'effet de vent est sur le pigeon du joueur", wind != null && wind.isActiveAndEnabled);
        PidgeonVfx vfx = Pigeon.GetComponentInChildren<PidgeonVfx>();
        yield return Check("Le pigeon a ses effets de piqué", vfx != null);
        if (vfx != null)
        {
            yield return Teleport(Pigeon, sky + Vector3.up * 100f, Quaternion.LookRotation(forward), "Haut dans le ciel pour piquer");
            Bot.Dive = true;
            float topSpeed = 0f;
            float diveStartY = Pigeon.transform.position.y;
            yield return Expect("Le piqué déforme l'écran du joueur", () =>
                {
                    topSpeed = Mathf.Max(topSpeed, Pigeon.Speed);
                    return vfx.DiveLensDistortion < -0.01f;
                }, 6f,
                () => $"état {Pigeon.CurrentPigeonState}, vitesse {Pigeon.Speed:0.0} (max {topSpeed:0.0}), hauteur {Pigeon.transform.position.y:0.0} (départ {diveStartY:0.0}), "
                    + $"local {Pigeon == PigeonController.Local}, gelé {Pigeon.IsMovementFrozen}, déformation {vfx.DiveLensDistortion:0.000}");
            Bot.Dive = false;
            yield return Expect("… et l'écran revient à la normale après", () => vfx.DiveLensDistortion > -0.01f, 6f,
                () => $"état {Pigeon.CurrentPigeonState}, déformation {vfx.DiveLensDistortion:0.000}");
            yield return ExpectLanded(Pigeon, 30f);
        }
    }
}
#endif
