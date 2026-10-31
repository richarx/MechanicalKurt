#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

public partial class ScenarioRunner
{
    IEnumerator RunBeachBall()
    {
        BallRolling ball = FindAnyObjectByType<BallRolling>();
        yield return Check("Un ballon de plage existe", ball != null);
        MovingSupport support = ball.GetComponent<MovingSupport>();
        Rigidbody body = ball.GetComponent<Rigidbody>();
        Collider collider = ball.GetComponent<Collider>();
        yield return Check("Le ballon a un MovingSupport enregistré", support != null && support.Id != 0
            && MovingSupport.TryGet(support.Id, out _), $"Id {support?.Id}");

        Vector3 top = collider.bounds.center + Vector3.up * (collider.bounds.extents.y + 0.15f);
        yield return Teleport(Pigeon, top, Quaternion.Euler(0f, 0f, 0f), "Placement au-dessus du ballon");
        var trace = new System.Text.StringBuilder();
        float traceStart = Time.realtimeSinceStartup, nextTrace = 0f;
        bool Landed()
        {
            float t = Time.realtimeSinceStartup - traceStart;
            if (t >= nextTrace && trace.Length < 1400)
            {
                nextTrace = t + 0.15f;
                Vector3 rel = Pigeon.transform.position - collider.bounds.center;
                trace.Append($" [{t:0.00}s rel({rel.x:0.0},{rel.y:0.00},{rel.z:0.0}) sup {(Pigeon.SupportId == support.Id ? "ballon" : Pigeon.SupportId.ToString())} {(Pigeon.IsGrounded ? "sol" : "air")} v{Pigeon.Speed:0.0} ballon v{body.linearVelocity.magnitude:0.0} kin {body.isKinematic}]");
            }
            return Pigeon.SupportId == support.Id && Pigeon.IsGrounded;
        }
        yield return Expect("Posé sur le ballon (support)", Landed, 3f,
            () => $"rayon {collider.bounds.extents.y:0.00} m — trace :{trace}");
        yield return ExpectStays("Reste sur le ballon sans bouger", () => Pigeon.SupportId == support.Id, 1.5f,
            () => $"support {Pigeon.SupportId}");

        Vector3 ballStart = body.position;
        float angularMax = 0f;
        int samples = 0, onBall = 0;
        Bot.Movement = new Vector2(0f, 0.4f);
        float end = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < end)
        {
            samples++;
            if (Pigeon.SupportId == support.Id) onBall++;
            angularMax = Mathf.Max(angularMax, body.angularVelocity.magnitude);
            yield return null;
        }
        Bot.Movement = Vector2.zero;
        yield return Check("En marchant, le ballon roule", angularMax > 0.3f || Vector3.Distance(ballStart, body.position) > 0.3f,
            $"rotation max {angularMax:0.00} rad/s, déplacement {Vector3.Distance(ballStart, body.position):0.00} m");
        yield return Check("Le pigeon reste dessus la plupart du temps", onBall >= samples * 0.5f,
            $"{100f * onBall / Mathf.Max(1, samples):0}% du temps sur le ballon");
        yield return Check("Pas de stun en marchant sur le ballon", !Pigeon.IsStun);

        if (Pigeon.SupportId == support.Id)
        {
            Bot.Flap = true;
            yield return Expect("En s'envolant, le pigeon quitte le ballon", () => Pigeon.SupportId == 0, 2f,
                () => $"support {Pigeon.SupportId}");
            Bot.Flap = false;
        }
    }

    IEnumerator RunMaxDepth()
    {
        Water water = FindAnyObjectByType<Water>();
        yield return Check("De l'eau dans la scène", water != null);
        float surface = water.transform.position.y;
        float modifier = PrivateFloat(Pigeon, "_waterLevelModifier");
        float maxDepth = PrivateFloat(Pigeon, "_maxUnderwaterDepth");
        float floor = surface - modifier - maxDepth;

        bool found = TryFindWaterLane(water, surface, out Vector3 lane, out Vector3 direction, out float length);
        yield return Check("Un coin d'eau profond existe", found && length > 20f, found ? $"{lane}" : "aucun");

        Vector3 above = new Vector3(lane.x, surface + 15f, lane.z);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        yield return Teleport(Pigeon, above, Quaternion.Euler(60f, yaw, 0f), "15 m au-dessus de l'eau, nez en bas");
        Bot.Dive = true;
        float minY = float.MaxValue;
        float end = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < end)
        {
            minY = Mathf.Min(minY, Pigeon.transform.position.y);
            yield return null;
        }
        Bot.Dive = false;
        yield return Check("Le piqué est bien entré dans l'eau", minY < surface - 1f, $"point le plus bas {surface - minY:0.00} m sous la surface");
        yield return Check($"Jamais sous le plancher ({modifier + maxDepth:0.0} m sous la surface)", minY >= floor - 0.15f,
            $"point le plus bas {surface - minY:0.00} m sous la surface");
        yield return Check("Pas de stun sous l'eau", !Pigeon.IsStun);
    }

    IEnumerator RunCameras()
    {
        CameraSwapper swapper = Pigeon.GetComponent<CameraSwapper>();
        yield return Check("Le pigeon a un CameraSwapper", swapper != null);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Check("Brain Cinemachine sur la caméra principale", brain != null);
        CinemachineCamera underwater = PrivateCamera(swapper, "underWaterCamera");
        CinemachineCamera precision = PrivateCamera(swapper, "precisionCamera");
        yield return Check("Caméras orbitale, sous l'eau et précision reliées", swapper.OrbitalCamera != null && underwater != null && precision != null);

        Water water = FindAnyObjectByType<Water>();
        bool found = TryFindWaterLane(water, water.transform.position.y, out Vector3 lane, out Vector3 direction, out _);
        yield return Check("Un coin d'eau existe", found);

        yield return Teleport(Pigeon, lane, Quaternion.LookRotation(direction), "Sous l'eau");
        yield return Expect("Sous l'eau : caméra sous-marine active", () => swapper.Current == CameraSwapper.CameraType.UnderWater
            && (Object)brain.ActiveVirtualCamera == underwater, 2f,
            () => $"type {swapper.Current}, active {brain.ActiveVirtualCamera?.Name}");

        var bots = new List<SpawnedBot>();
        yield return SpawnBot(lane + direction * 3f, Quaternion.LookRotation(direction), bots);
        Pigeon.RequestRespawn();
        yield return Expect("Revenu à terre : caméra orbitale", () => swapper.Current == CameraSwapper.CameraType.Orbital
            && (Object)brain.ActiveVirtualCamera == swapper.OrbitalCamera, 3f,
            () => $"type {swapper.Current}, active {brain.ActiveVirtualCamera?.Name}");
        CameraSwapper botSwapper = bots[0].Pigeon.GetComponent<CameraSwapper>();
        yield return ExpectStays("Le bot sous l'eau ne vole pas la vue, et son CameraSwapper reste inerte",
            () => (Object)brain.ActiveVirtualCamera == swapper.OrbitalCamera
                && (botSwapper == null || botSwapper.Current != CameraSwapper.CameraType.UnderWater), 1.5f,
            () => $"active {brain.ActiveVirtualCamera?.Name}, bot sous l'eau {bots[0].Pigeon.IsUndeWater}, choix du bot {botSwapper?.Current}");
        BotSpawner.Delete(bots[0].Pigeon);

        Vector3 sky = Pigeon.transform.position + Vector3.up * 25f;
        yield return Teleport(Pigeon, sky, Pigeon.transform.rotation, "En l'air");
        Bot.PrecisionCamera = true;
        Bot.Flap = true;
        yield return Expect("En vol + bouton : caméra précision active", () => swapper.Current == CameraSwapper.CameraType.Precision
            && (Object)brain.ActiveVirtualCamera == precision, 2f,
            () => $"type {swapper.Current}, au sol {Pigeon.IsGrounded}, active {brain.ActiveVirtualCamera?.Name}");
        Bot.PrecisionCamera = false;
        Bot.Flap = false;
        yield return Expect("Bouton relâché : retour à l'orbitale", () => swapper.Current == CameraSwapper.CameraType.Orbital, 1f,
            () => $"type {swapper.Current}");
    }

    IEnumerator RunSunglasses()
    {
        LootableEquipment loot = null;
        foreach (LootableEquipment candidate in FindObjectsByType<LootableEquipment>(FindObjectsInactive.Exclude))
            if (candidate.Equipment == PigeonEquipment.Equipment.Sunglasses)
                loot = candidate;
        yield return Check("Un loot de lunettes existe (réglé sur Sunglasses)", loot != null);

        PigeonEquipment equipment = Pigeon.GetComponent<PigeonEquipment>();
        byte bit = PigeonEquipment.Bit(PigeonEquipment.Equipment.Sunglasses);
        yield return Check("Les 4 équipements tiennent dans le masque", PigeonEquipment.AllBits == 0b1111, $"AllBits {PigeonEquipment.AllBits}");
        yield return Check("Lunettes pas encore portées", (Pigeon.WornEquipment & bit) == 0 && !equipment.IsWorn(PigeonEquipment.Equipment.Sunglasses));
        byte before = Pigeon.WornEquipment;

        yield return HoldStillAt(loot.transform.position, loot.Radius, loot.StopSpeedThreshold, "loot de lunettes");
        yield return Expect("Le loot de lunettes est ramassé", () => loot == null, 2f);
        yield return Expect("Lunettes dans l'état du pigeon (bit 3)", () => (Pigeon.WornEquipment & bit) != 0, 3f,
            () => $"état {System.Convert.ToString(Pigeon.WornEquipment, 2)}");
        yield return Expect("Lunettes visibles", () => equipment.IsWorn(PigeonEquipment.Equipment.Sunglasses), 1f);
        yield return Check("Les autres équipements n'ont pas changé", (Pigeon.WornEquipment & ~bit & PigeonEquipment.AllBits) == (before & ~bit),
            $"avant {System.Convert.ToString(before, 2)}, après {System.Convert.ToString(Pigeon.WornEquipment, 2)}");
        PlayerControls controls = Pigeon.GetComponent<PlayerControls>();
        yield return Expect("Fin de la cinématique : commandes rendues", () => controls == null || !controls.AreControlsBlocked, 6f);
    }

    IEnumerator RunSewerRings()
    {
        var rings = new List<BoostRing>();
        foreach (BoostRing ring in FindObjectsByType<BoostRing>(FindObjectsInactive.Exclude))
            if (ring.name.StartsWith("BoostRing_Small"))
                rings.Add(ring);
        rings.Sort((a, b) => string.CompareOrdinal(a.name, b.name) != 0
            ? string.CompareOrdinal(a.name, b.name) : a.transform.position.x.CompareTo(b.transform.position.x));
        yield return Check("Au moins deux petits anneaux dans les égouts", rings.Count >= 2, $"{rings.Count} anneaux");

        int boosts = 0;
        void CountBoost() => boosts++;
        Pigeon.OnBoosted.AddListener(CountBoost);
        Defer(() => Pigeon.OnBoosted.RemoveListener(CountBoost));

        yield return Wait("Fin d'un combo précédent", BoostRing.ComboWindow + 0.5f);

        for (int i = 0; i < 2; i++)
        {
            BoostRing ring = rings[i];
            int expected = i + 1;
            Vector3 center = ring.GetComponent<Collider>().bounds.center;
            Vector3 axis = ring.transform.forward;
            if (i == 0)
                yield return Teleport(Pigeon, center, Quaternion.LookRotation(axis), $"Dans l'anneau {expected} ({ring.name})");
            else
            {
                yield return Teleport(Pigeon, center - axis * 3f, Quaternion.LookRotation(axis), $"3 m avant l'anneau {expected} ({ring.name})");
                PigeonController pigeon = Pigeon;
                PigeonController.RunInSimulation(_ => pigeon.SimState.PendingImpulse += axis * 35f);
            }
            yield return Expect($"Anneau {expected} : boost", () => boosts >= expected, 1.5f,
                () => $"boosts {boosts}, vitesse {Pigeon.Speed:0.0}, distance au centre {Vector3.Distance(Pigeon.transform.position, center):0.0} m");
            yield return Check($"Anneau {expected} : rang {expected} dans le combo", Pigeon.RingCombo == expected, $"combo {Pigeon.RingCombo}");
            yield return Expect($"Anneau {expected} : vitesse de sortie ≥ 80 % de {ring.MinExitSpeed}", () => Pigeon.Speed >= ring.MinExitSpeed * 0.8f, 1f,
                () => $"vitesse {Pigeon.Speed:0.0}");
        }
    }

    IEnumerator RunCarRide()
    {
        int carLayer = LayerMask.NameToLayer("PigeonOnly"), pigeonLayer = LayerMask.NameToLayer("Player");
        yield return Check("En solo, les voitures sont solides", !TrafficCollisionGate.CarsAreGhosts
            && !Physics.GetIgnoreLayerCollision(carLayer, pigeonLayer));

        AmbientCar car = null;
        MovingSupport support = null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (car == null && Time.realtimeSinceStartup < deadline)
        {
            foreach (AmbientCar candidate in FindObjectsByType<AmbientCar>(FindObjectsInactive.Exclude))
                if (candidate.CurrentSpeed > 3f && candidate.TryGetComponent(out MovingSupport s))
                {
                    car = candidate;
                    support = s;
                    break;
                }
            if (car == null)
                yield return null;
        }
        yield return Check("Une voiture roule (avec MovingSupport)", car != null);

        Bounds roof = default;
        bool any = false;
        foreach (Collider c in car.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!any) { roof = c.bounds; any = true; } else roof.Encapsulate(c.bounds);
        }
        yield return Check("La voiture a des colliders", any);

        Vector3 onRoof = new Vector3(roof.center.x, roof.max.y + 0.15f, roof.center.z);
        yield return Teleport(Pigeon, onRoof, Quaternion.LookRotation(car.transform.forward), $"Sur le toit de « {car.name} »");
        yield return Expect("Posé sur la voiture (support)", () => Pigeon.SupportId == support.Id, 2f,
            () => $"support {Pigeon.SupportId} (voiture {support.Id}), au sol {Pigeon.IsGrounded}, vitesse voiture {car.CurrentSpeed:0.0}");

        Vector3 startCar = car.transform.position;
        int samples = 0, onCar = 0;
        float maxOffset = 0f;
        float end = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < end)
        {
            samples++;
            if (Pigeon.SupportId == support.Id) onCar++;
            Vector3 offset = Pigeon.transform.position - car.transform.position;
            offset.y = 0f;
            maxOffset = Mathf.Max(maxOffset, offset.magnitude);
            yield return null;
        }
        float carMoved = Vector3.Distance(startCar, car.transform.position);
        yield return Check("Transporté par la voiture", onCar >= samples * 0.8f && maxOffset < car.VehicleLength,
            $"{100f * onCar / Mathf.Max(1, samples):0}% du temps dessus, écart max {maxOffset:0.0} m, voiture déplacée de {carMoved:0.0} m");
        yield return Check("La voiture a bien avancé pendant ce temps", carMoved > 1f, $"{carMoved:0.0} m");
    }

    static float PrivateFloat(object target, string field)
        => (float)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static CinemachineCamera PrivateCamera(CameraSwapper swapper, string field)
        => typeof(CameraSwapper).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(swapper) as CinemachineCamera;
}
#endif
