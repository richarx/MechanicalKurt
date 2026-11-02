using System.Linq;
using Sirenix.Utilities;
using Unity.Collections;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
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
    private Light[] _lampadaires;

    private float clockTime = 0.0f;
    private float currentBlend = 0.0f;
    private const float blendLength = 6.0f;

    public float normalizedClock => Tools.NormalizeValue(clockTime, 0.0f, dayDurationInSeconds);
    private Vector3 currentSunRotation => sunDirectionalLight.transform.rotation.eulerAngles;

    public bool IsNightTime => (clockTime >= beginNightTime && clockTime <= endNightTime);

    void Awake()
    {
        if (DayNightCycle.instance != null)
        {
            Debug.LogWarning("Several instances of DayNightCycle in scene");
            return;
        }
        instance = this;

        if (_streetLightsParent == null)
        {
            Debug.LogError("DayNightCycle : assigner le parent des lampadaires dans l'Inspector.", this);
            enabled = false;
            return;
        }

        _lampadaires = _streetLightsParent.GetComponentsInChildren<Light>(true);
    }

    private void Start()
    {
        skyboxMaterial.SetFloat("_Blend", currentBlend);
    }

    private void Update()
    {
        float oldTime = clockTime;
        clockTime += Time.deltaTime;
        if (clockTime >= dayDurationInSeconds)
            clockTime = dayDurationInSeconds - clockTime;

        currentBlend = Tools.NormalizeValueInRange(clockTime, 0.0f, dayDurationInSeconds, 0.0f, blendLength);
        skyboxMaterial.SetFloat("_Blend", currentBlend);

        if (oldTime < beginNightTime && clockTime >= beginNightTime)
        {
            SetHeadlights(true);
            foreach (var lamp in _lampadaires)
            {
                lamp.gameObject.SetActive(true);
            }
        }
        else if (oldTime < endNightTime && clockTime >= endNightTime)
        {
            SetHeadlights(false);
            foreach (var lamp in _lampadaires)
                lamp.gameObject.SetActive(false);
        }

        sunDirectionalLight.color = sunLightGradient.Evaluate(normalizedClock);

        sunDirectionalLight.transform.rotation = Quaternion.Euler(new Vector3(sunRotationCurve.Evaluate(normalizedClock) * 100.0f, currentSunRotation.y, currentSunRotation.z));
    }



    public void SetHeadlights(bool isOn)
    {
        Color emission = isOn
            ? Color.white * headlightsIntensity
            : Color.black;

        foreach (Material material in carMaterials)
            material.SetColor("_EmissionColor", emission);
    }

    private void OnApplicationQuit()
    {
        skyboxMaterial.SetFloat("_Blend", 0.0f);
        foreach (Material material in carMaterials)
            material.SetColor("_EmissionColor", Color.black);
    }
}
