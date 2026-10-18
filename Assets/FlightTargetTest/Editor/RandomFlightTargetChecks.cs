using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RandomFlightTargetChecks
{
    [MenuItem("Tools/Flight Target Test/Check Current Test")]
    public static void RunAll()
    {
        Require(RandomFlightTargetTest.SegmentVisitsTarget(new Vector3(-20f, 0f, 0f),
            new Vector3(20f, 0f, 0f), Vector3.zero, 3f), "Un passage rapide doit valider la cible.");
        Require(!RandomFlightTargetTest.SegmentVisitsTarget(new Vector3(-20f, 4f, 0f),
            new Vector3(20f, 4f, 0f), Vector3.zero, 3f), "Un passage hors du rayon ne doit pas valider.");
        Require(RandomFlightTargetTest.SegmentVisitsTarget(Vector3.zero, Vector3.zero, Vector3.one, 3f),
            "Un joueur immobile dans la cible doit pouvoir valider.");
        Require(!RandomFlightTargetTest.SegmentVisitsTarget(Vector3.zero, Vector3.right,
            Vector3.right * 20f, 3f), "Ne pas prolonger le segment au-dela du mouvement reel.");

        Scene scene = SceneManager.GetActiveScene();
        var loops = RandomFlightTestSetup.FindInScene<RandomFlightTargetTest>(scene);
        var arrows = RandomFlightTestSetup.FindInScene<RandomFlightCompassArrow>(scene);
        Require(loops.Count == 1 && arrows.Count == 1, "La scene doit contenir un test et une boussole.");
        RandomFlightTargetTest loop = loops[0];
        RandomFlightCompassArrow arrow = arrows[0];
        Require(loop.HasValidSettings(), "Les parametres de placement doivent etre valides.");
        Require(arrow.Objectives == loop && arrow.ViewCamera != null && arrow.Visual != null && arrow.ArrowPivot != null,
            "La boussole doit etre reliee au test, a la camera et a son visuel.");
        foreach (DeliveryManager manager in RandomFlightTestSetup.FindInScene<DeliveryManager>(scene))
            Require(!manager.isActiveAndEnabled, "Les livraisons doivent etre desactivees dans la copie.");
        foreach (DeliveryCompassArrow old in RandomFlightTestSetup.FindInScene<DeliveryCompassArrow>(scene))
            Require(!old.isActiveAndEnabled, "Une ancienne boussole ne doit pas piloter le visuel du test.");

        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(5901);
            Physics.SyncTransforms();
            for (int i = 0; i < 20; i++)
            {
                Require(loop.TryFindTargetPosition(out Vector3 target), "Un objectif doit pouvoir etre place dans la ville.");
                Require(loop.SpawnBounds.Contains(target), "L'objectif doit rester dans la zone.");
                float distance = Vector3.Distance(loop.Player.position, target);
                Require(distance >= loop.MinimumDistance && distance <= loop.MaximumDistance,
                    "La distance de l'objectif doit respecter les limites.");
                Require(!Physics.CheckSphere(target, loop.ClearanceRadius, loop.ObstacleLayers, QueryTriggerInteraction.Ignore),
                    "L'objectif doit disposer d'un volume libre.");
            }
        }
        finally
        {
            UnityEngine.Random.state = randomState;
        }
        Debug.Log("Flight Target Test : controles reussis (passages rapides, references, isolation et 20 placements physiques).");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Flight Target Test : " + message);
    }
}
