using PurrNet.Prediction;
using UnityEngine;

public struct DayNightState : IPredictedData<DayNightState>
{
    public float Clock;

    public void Dispose() { }
}

/// <summary>
/// Cycle jour/nuit partagé : l'heure fait partie de l'état prédit, identique pour tous les joueurs,
/// y compris ceux qui rejoignent la partie en cours. Lampadaires et phares suivent cette heure.
/// </summary>
public class DayNightCycle : PredictedIdentity<DayNightState>
{
    public static DayNightCycle instance;

    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private float dayDurationInSeconds;
    [SerializeField] private Gradient sunLightGradient;
    [SerializeField] private Light sunDirectionalLight;
    [SerializeField] private AnimationCurve sunRotationCurve;

    [SerializeField] private float beginNightTime = 162;
    [SerializeField] private float endNightTime = 306;

    [SerializeField] private GameObject _streetLightsParent;

    [SerializeField] private Material[] carMaterials;
    [SerializeField] private float headlightsIntensity = 3f;
    private Light[] _lampadaires = new Light[0];
    private bool? _lightsOn;

    private float clockTime = 0.0f;
    private float currentBlend = 0.0f;
    private const float blendLength = 6.0f;

    private float DayDuration => Mathf.Max(1.0f, dayDurationInSeconds);
    /// <summary>Heure affichée, en secondes depuis le début du cycle.</summary>
    public float Clock => clockTime;
    public float Duration => DayDuration;
    public float normalizedClock => Tools.NormalizeValue(clockTime, 0.0f, dayDurationInSeconds);
    private Vector3 currentSunRotation => sunDirectionalLight.transform.rotation.eulerAngles;

    public bool IsNightTime => clockTime >= beginNightTime && clockTime <= endNightTime;
    public float BeginNightTime => beginNightTime;
    public float EndNightTime => endNightTime;
    public Light[] StreetLights => _lampadaires;
    public Material[] CarMaterials => carMaterials;

#if UNITY_EDITOR
    /// <summary>Scénarios (hôte) : avance l'heure partagée.</summary>
    [SimulationOnly]
    public void SetClockForTests(float clock) => currentState.Clock = Mathf.Repeat(clock, DayDuration);
#endif
    /// <summary>Lampadaires et phares allumés (null tant que la vue n'a pas tourné).</summary>
    public bool? LightsOn => _lightsOn;

    void Awake()
    {
        if (instance != null && instance != this)
            Debug.LogWarning("Several instances of DayNightCycle in scene", this);
        else
            instance = this;

        if (_streetLightsParent == null)
            Debug.LogError("DayNightCycle : assigner le parent des lampadaires dans l'Inspector.", this);
        else
            _lampadaires = _streetLightsParent.GetComponentsInChildren<Light>(true);
    }

    protected override void LateAwake()
    {
        skyboxMaterial.SetFloat("_Blend", currentBlend);
    }

    protected override void Destroyed()
    {
        if (instance == this)
            instance = null;
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

        // Selon l'heure plutôt qu'au passage d'un seuil : un joueur qui arrive de nuit trouve tout allumé.
        bool night = IsNightTime;
        if (_lightsOn != night)
        {
            _lightsOn = night;
            SetHeadlights(night);
            foreach (Light lamp in _lampadaires)
                if (lamp != null)
                    lamp.gameObject.SetActive(night);
        }
    }

    public void SetHeadlights(bool isOn)
    {
        Color emission = isOn ? Color.white * headlightsIntensity : Color.black;
        foreach (Material material in carMaterials)
            if (material != null)
                material.SetColor("_EmissionColor", emission);
    }

    private void OnApplicationQuit()
    {
        skyboxMaterial.SetFloat("_Blend", 0.0f);
        SetHeadlights(false);
    }
}
