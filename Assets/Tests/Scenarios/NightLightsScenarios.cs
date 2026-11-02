#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;

public partial class ScenarioRunner
{
    IEnumerator NightLights()
    {
        DayNightCycle cycle = DayNightCycle.instance;
        yield return Check("Cycle jour/nuit avec lampadaires et phares", cycle != null && cycle.StreetLights.Length > 0 && cycle.CarMaterials.Length > 0,
            cycle == null ? "pas de cycle" : $"{cycle.StreetLights.Length} lampadaires, {cycle.CarMaterials.Length} matériaux de voiture");
        float before = cycle.Clock;
        Defer(() => PigeonController.RunInSimulation(_ => cycle.SetClockForTests(before)));
        Material car = cycle.CarMaterials[0];

        PigeonController.RunInSimulation(_ => cycle.SetClockForTests(cycle.BeginNightTime + 5f));
        yield return Expect("La nuit : lampadaires allumés", () => cycle.LightsOn == true && cycle.StreetLights.All(l => l.gameObject.activeSelf), 2f,
            () => $"heure {cycle.Clock:0}, allumés {cycle.LightsOn}");
        yield return Check("… et phares des voitures", car.GetColor("_EmissionColor").maxColorComponent > 1f, $"émission {car.GetColor("_EmissionColor")}");
        yield return new WaitForSecondsRealtime(0.5f);
        Capture(Camera.main, "night_lights");

        PigeonController.RunInSimulation(_ => cycle.SetClockForTests(cycle.EndNightTime + 5f));
        yield return Expect("Le jour : tout s'éteint", () => cycle.LightsOn == false && cycle.StreetLights.All(l => !l.gameObject.activeSelf)
            && car.GetColor("_EmissionColor").maxColorComponent < 0.01f, 2f, () => $"heure {cycle.Clock:0}, allumés {cycle.LightsOn}");
    }
}
#endif
