using PurrNet.Prediction;
using UnityEngine;

public struct DayNightState : IPredictedData<DayNightState>
{
    public float Clock;

    public void Dispose() { }
}

/// <summary>
/// Cycle jour/nuit partagé : l'heure fait partie de l'état prédit, identique pour tous les joueurs,
/// y compris ceux qui rejoignent la partie en cours.
/// </summary>
public class DayNightCycle : PredictedIdentity<DayNightState>
{
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private float dayDurationInSeconds;
    [SerializeField] private Gradient sunLightGradient;
    [SerializeField] private Light sunDirectionalLight;
    [SerializeField] private AnimationCurve sunRotationCurve;

    private float clockTime = 0.0f;
    private float currentBlend = 0.0f;
    private const float blendLength = 6.0f;

    private float DayDuration => Mathf.Max(1.0f, dayDurationInSeconds);
    /// <summary>Heure affichée, en secondes depuis le début du cycle.</summary>
    public float Clock => clockTime;
    public float Duration => DayDuration;
    public float normalizedClock => Tools.NormalizeValue(clockTime, 0.0f, dayDurationInSeconds);
    private Vector3 currentSunRotation => sunDirectionalLight.transform.rotation.eulerAngles;

    protected override void LateAwake()
    {
        skyboxMaterial.SetFloat("_Blend", currentBlend);
    }

    protected override DayNightState GetInitialState() => new DayNightState();

    protected override void GetUnityState(ref DayNightState s) { }
    protected override void SetUnityState(DayNightState s) { }

    protected override void Simulate(ref DayNightState s, float dt)
    {
        s.Clock = Mathf.Repeat(s.Clock + dt, DayDuration);
    }

    protected override DayNightState Interpolate(DayNightState from, DayNightState to, float t)
    {
        if (to.Clock < from.Clock)
            return to;
        return new DayNightState { Clock = Mathf.Lerp(from.Clock, to.Clock, t) };
    }

    protected override void UpdateView(DayNightState view, DayNightState? verified)
    {
        clockTime = view.Clock;

        currentBlend = Tools.NormalizeValueInRange(clockTime, 0.0f, dayDurationInSeconds, 0.0f, blendLength);
        skyboxMaterial.SetFloat("_Blend", currentBlend);

        sunDirectionalLight.color = sunLightGradient.Evaluate(normalizedClock);

        sunDirectionalLight.transform.rotation = Quaternion.Euler(new Vector3(sunRotationCurve.Evaluate(normalizedClock) * 100.0f, currentSunRotation.y, currentSunRotation.z));
    }

    private void OnApplicationQuit()
    {
        skyboxMaterial.SetFloat("_Blend", 0.0f);
    }
}
