#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ScenarioRunner
{
    public const string EquipmentScenario = "Équipement : ramassage";

    IEnumerator RunEquipmentPickup()
    {
        LootableEquipment loot = null;
        foreach (LootableEquipment candidate in FindObjectsByType<LootableEquipment>(FindObjectsInactive.Exclude))
            if (loot == null || string.CompareOrdinal(candidate.name, loot.name) < 0
                || (candidate.name == loot.name && candidate.transform.position.x < loot.transform.position.x))
                loot = candidate;
        yield return Check("Un objet à ramasser existe dans la scène", loot != null,
            loot != null ? $"« {loot.name} » ({loot.Equipment}) en {loot.transform.position}" : null);

        PigeonEquipment equipment = Pigeon.GetComponent<PigeonEquipment>();
        PlayerControls playerControls = Pigeon.GetComponent<PlayerControls>();
        PigeonEquipment.Equipment kind = loot.Equipment;
        byte bit = PigeonEquipment.Bit(kind);
        yield return Check("Le pigeon a un PigeonEquipment", equipment != null);
        yield return Check($"{kind} pas encore porté", (Pigeon.WornEquipment & bit) == 0 && !equipment.IsWorn(kind));

        yield return HoldStillAt(loot.transform.position, loot.Radius, loot.StopSpeedThreshold, $"objet {loot.name}");
        yield return Expect("L'objet est ramassé (il disparaît)", () => loot == null, 2f);
        yield return Expect("Cinématique d'équipement chez le joueur (commandes bloquées)",
            () => playerControls == null || playerControls.AreControlsBlocked, 1f);
        yield return Expect($"{kind} porté dans l'état du pigeon (répliqué)", () => (Pigeon.WornEquipment & bit) != 0, 3f,
            () => $"état {System.Convert.ToString(Pigeon.WornEquipment, 2)}");
        yield return Expect($"{kind} visible sur le pigeon", () => equipment.IsWorn(kind), 1f);
        yield return Expect("Fin de la cinématique : commandes rendues",
            () => playerControls == null || !playerControls.AreControlsBlocked, 6f);

        PigeonEquipment.Equipment otherKind = kind == PigeonEquipment.Equipment.Shoes
            ? PigeonEquipment.Equipment.Hat : PigeonEquipment.Equipment.Shoes;
        byte otherBit = PigeonEquipment.Bit(otherKind);
        var bots = new List<SpawnedBot>();
        Vector3 beside = Pigeon.transform.position + Pigeon.transform.right * 2f + Vector3.up * 0.5f;
        yield return SpawnBot(beside, Pigeon.transform.rotation, bots);
        PigeonController other = bots[0].Pigeon;
        PigeonEquipment otherEquipment = other.GetComponent<PigeonEquipment>();
        yield return Check("Le bot n'a encore rien", other.WornEquipment == 0 && otherEquipment != null && !otherEquipment.IsWorn(otherKind),
            $"état {System.Convert.ToString(other.WornEquipment, 2)}");

        otherEquipment.UnlockItem(otherKind);
        yield return Check("Pas de cinématique chez le joueur local pour le pigeon d'un autre",
            playerControls == null || !playerControls.AreControlsBlocked);
        yield return Expect($"Le bot porte {otherKind} (état)", () => (other.WornEquipment & otherBit) != 0, 2f,
            () => $"état {System.Convert.ToString(other.WornEquipment, 2)}");
        yield return Expect($"{otherKind} visible sur le bot", () => otherEquipment.IsWorn(otherKind), 1f);
        yield return Check("L'équipement du joueur n'a pas changé", (Pigeon.WornEquipment & otherBit) == 0 && (Pigeon.WornEquipment & bit) != 0,
            $"joueur {System.Convert.ToString(Pigeon.WornEquipment, 2)}");
    }
}
#endif
