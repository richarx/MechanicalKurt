#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

public partial class ScenarioRunner
{
    IEnumerator PauseKeepsGameRunning()
    {
        PauseMenu menu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        yield return Check("Un PauseMenu existe", menu != null);
        PlayerControls player = Pigeon.GetComponent<PlayerControls>();
        yield return Check("Le pigeon du joueur a des PlayerControls", player != null);
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        Defer(() => menu.SetPaused(false));

        Pigeon.SetControlsOverride(null);
        Vector3 spot = Pigeon.transform.position + Vector3.up * 15f;
        yield return Teleport(Pigeon, spot, Quaternion.Euler(0f, Pigeon.transform.eulerAngles.y, 0f), "Le pigeon est en l'air");

        menu.SetPaused(true);
        yield return Expect("Menu pause ouvert : commandes en pause", () => player.IsPaused, 1f);
        yield return Check("Le temps n'est pas arrêté", Time.timeScale > 0.99f, $"timeScale {Time.timeScale}");
        float clockBefore = cycle != null ? cycle.Clock : 0f;

        yield return ExpectHolds("Le pigeon se met en vol stationnaire", () => Pigeon.IsHovering && !Pigeon.IsGrounded && Pigeon.Speed < 2f,
            1f, 5f, () => $"stationnaire {Pigeon.IsHovering}, au sol {Pigeon.IsGrounded}, vitesse {Pigeon.Speed:0.00}");
        float altitude = Pigeon.transform.position.y;
        yield return ExpectStays("Il reste à la même hauteur pendant la pause", () => Mathf.Abs(Pigeon.transform.position.y - altitude) < 1.5f
            && !Pigeon.IsGrounded, 2f, () => $"hauteur {Pigeon.transform.position.y:0.00} (début {altitude:0.00})");
        if (cycle != null)
            yield return Check("Le cycle jour/nuit avance pendant la pause", Mathf.Repeat(cycle.Clock - clockBefore, cycle.Duration) > 1f,
                $"avant {clockBefore:0.0} s, maintenant {cycle.Clock:0.0} s");

        menu.SetPaused(false);
        yield return Expect("Menu fermé : commandes rendues", () => !player.IsPaused, 2f);
        yield return Expect("Le pigeon quitte le vol stationnaire", () => !Pigeon.IsHovering, 1f);
    }

    IEnumerator TrafficIsRunning()
    {
        TrafficSpawner[] spawners = FindObjectsByType<TrafficSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        yield return Check("Des TrafficSpawner existent", spawners.Length > 0, $"{spawners.Length}");
        var report = new System.Text.StringBuilder();
        foreach (TrafficSpawner spawner in spawners)
            report.Append($" | {spawner.name} actif {spawner.isActiveAndEnabled}, voitures {spawner.SpawnedCount}/{spawner.CarCount}");
        AmbientCar[] cars = FindObjectsByType<AmbientCar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int visible = 0;
        var before = new Vector3[cars.Length];
        for (int i = 0; i < cars.Length; i++)
        {
            before[i] = cars[i].transform.position;
            // Un .blend mal importé (Blender absent au moment de l'import) donne un MeshFilter sans maillage, sans erreur.
            foreach (MeshFilter mesh in cars[i].GetComponentsInChildren<MeshFilter>())
                if (mesh.sharedMesh != null && mesh.sharedMesh.vertexCount > 0 && mesh.TryGetComponent(out Renderer r) && r.enabled && r.gameObject.activeInHierarchy)
                {
                    visible++;
                    break;
                }
        }
        report.Append($" | voitures {cars.Length}, avec rendu actif {visible}");
        if (cars.Length > 0)
            report.Append($", exemple « {cars[0].name} » en {cars[0].transform.position}, actif {cars[0].gameObject.activeInHierarchy}, layer {LayerMask.LayerToName(cars[0].gameObject.layer)}");
        yield return Check("Toutes les voitures sont créées et visibles", cars.Length > 0 && visible == cars.Length, report.ToString());
        yield return Wait("Les voitures roulent", 2f);
        int moved = 0;
        for (int i = 0; i < cars.Length; i++)
            if (cars[i] != null && Vector3.Distance(before[i], cars[i].transform.position) > 0.5f)
                moved++;
        yield return Check("Les voitures avancent", moved > cars.Length / 2, $"{moved}/{cars.Length} ont bougé");
    }

    /// <summary>Photos d'une voiture, puis du pigeon posé sur son toit (Logs/Scenario_car_*.png).</summary>
    IEnumerator PigeonOnCar()
    {
        AmbientCar car = null;
        foreach (AmbientCar candidate in FindObjectsByType<AmbientCar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (car == null || candidate.CurrentSpeed < car.CurrentSpeed)
                car = candidate;
        yield return Check("Une voiture est en jeu", car != null);
        Bounds bounds = CarBounds(car);
        string colliders = string.Join(", ", System.Array.ConvertAll(car.GetComponentsInChildren<Collider>(),
            c => $"{c.GetType().Name}{(c.isTrigger ? " (trigger)" : "")} layer {LayerMask.LayerToName(c.gameObject.layer)}"));
        CaptureNear(car, "car_side");

        Pigeon.SetControlsOverride(null);
        Vector3 roof = new Vector3(bounds.center.x, bounds.max.y + 0.6f, bounds.center.z);
        yield return Teleport(Pigeon, roof, Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f), "Le pigeon est au-dessus de la voiture");
        yield return Wait("Il se pose", 1.2f);
        bounds = CarBounds(car);
        Vector3 p = Pigeon.transform.position;
        bool onRoof = Mathf.Abs(p.y - bounds.max.y) < 0.8f && new Vector2(p.x - bounds.center.x, p.z - bounds.center.z).magnitude < bounds.extents.magnitude;
        CaptureNear(car, "car_pigeon_side");
        Capture(Camera.main, "car_pigeon_game");
        yield return Check("Le pigeon est posé sur la voiture", onRoof,
            $"« {car.name} » vitesse {car.CurrentSpeed:0.0}, toit à {bounds.max.y:0.00}, pigeon en {p}, au sol {Pigeon.IsGrounded}, colliders : {colliders}");
    }

    static Bounds CarBounds(AmbientCar car)
    {
        Bounds bounds = new Bounds(car.transform.position, Vector3.zero);
        foreach (Renderer r in car.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static void CaptureNear(AmbientCar car, string label)
    {
        Bounds bounds = CarBounds(car);
        var go = new GameObject("[Capture]");
        Camera camera = go.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        float distance = Mathf.Max(5f, bounds.extents.magnitude * 2.2f);
        go.transform.position = bounds.center + (car.transform.right * 0.8f - car.transform.forward * 0.6f).normalized * distance + Vector3.up * distance * 0.35f;
        go.transform.LookAt(bounds.center);
        Capture(camera, label);
        DestroyImmediate(go);
    }

    static void Capture(Camera camera, string label)
    {
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        System.IO.File.WriteAllBytes($"Logs/Scenario_{label}.png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = previous;
        DestroyImmediate(target);
        DestroyImmediate(image);
    }

    IEnumerator MultiPauseAndDayNight()
    {
        PigeonController remote = RemotePigeon;
        PauseMenu menu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        yield return Check("Un PauseMenu existe", menu != null);
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        yield return Check("Un DayNightCycle existe", cycle != null);

        float Gap()
        {
            if (!ScenarioLinkProtocol.TryParse(Remote(RemoteKeys.DayNightClock), out float seen))
                return float.PositiveInfinity;
            float gap = Mathf.Abs(seen - cycle.Clock);
            return Mathf.Min(gap, cycle.Duration - gap);
        }
        yield return Expect("Même heure du jour chez les deux joueurs", () => Gap() < 0.5f, 3f,
            () => $"hôte {cycle.Clock:0.00} s, second joueur {Remote(RemoteKeys.DayNightClock)} s");

        Defer(() => menu.SetPaused(false));
        menu.SetPaused(true);
        yield return Check("Pause de l'hôte : le temps n'est pas arrêté", Time.timeScale > 0.99f, $"timeScale {Time.timeScale}");
        Vector3 air = remote.transform.position + Vector3.up * 10f;
        yield return Teleport(remote, air, Quaternion.Euler(0f, remote.transform.eulerAngles.y, 0f), "Le second joueur est en l'air");
        Vector3 from = remote.transform.position;
        yield return Expect("Pendant la pause de l'hôte, le second joueur continue de bouger", () => Vector3.Distance(remote.transform.position, from) > 2f,
            3f, () => $"déplacement {Vector3.Distance(remote.transform.position, from):0.00} m");
        yield return Expect("L'heure avance toujours, au même rythme chez les deux", () => Gap() < 0.5f, 2f,
            () => $"hôte {cycle.Clock:0.00} s, second joueur {Remote(RemoteKeys.DayNightClock)} s");
        menu.SetPaused(false);

        Defer(() => _link?.Send("pause", "off"));
        yield return Teleport(remote, air, Quaternion.Euler(0f, remote.transform.eulerAngles.y, 0f), "Le second joueur repart en l'air");
        yield return RemoteDo("override", "off");
        yield return RemoteDo("pause", "on");
        yield return Expect("Le second joueur est en pause chez lui", () => RemoteFlag(RemoteKeys.Paused), 2f, RemoteState);
        yield return ExpectHolds("L'hôte le voit en vol stationnaire", () => remote.IsHovering && !remote.IsGrounded && remote.Speed < 2f, 1f, 5f,
            () => $"stationnaire {remote.IsHovering}, au sol {remote.IsGrounded}, vitesse {remote.Speed:0.00}");
        yield return RemoteDo("pause", "off");
        yield return RemoteDo("override", "on");
        yield return Expect("Fin de la pause chez le second joueur", () => RemoteIs(RemoteKeys.Paused, "0"), 2f, RemoteState);
    }
}
#endif
