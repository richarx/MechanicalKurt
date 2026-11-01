#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

public partial class ScenarioRunner
{
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
        yield return Expect("En vol stationnaire, battre des ailes fait monter", () => Pigeon.transform.position.y > y + 1f, 3f,
            () => $"hauteur {Pigeon.transform.position.y:0.0} (départ {y:0.0})");
        Bot.Flap = false;
        y = Pigeon.transform.position.y;
        Bot.RelativeMovement = new Vector2(0f, -1f);
        yield return Expect("… et tirer vers le bas fait descendre", () => Pigeon.transform.position.y < y - 1f, 3f,
            () => $"hauteur {Pigeon.transform.position.y:0.0} (départ {y:0.0})");
        Bot.RelativeMovement = Vector2.zero;
        yield return Expect("L'endurance remonte en vol stationnaire", () => Pigeon.Stamina > stamina.FlapThreshold, 5f,
            () => $"endurance {Pigeon.Stamina:0.0}");
        Bot.Brake = false;

        yield return ExpectLanded(Pigeon, 15f);
        yield return Expect("Posé : endurance pleine", () => stamina.HasMaxStamina, 1f, () => $"endurance {Pigeon.Stamina:0.0}");
    }
}
#endif
