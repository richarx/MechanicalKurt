using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private float dayDurationInSeconds;
    [SerializeField] private Gradient sunLightGradient;
    [SerializeField] private Light sunDirectionalLight;
    [SerializeField] private AnimationCurve sunRotationCurve;

    private float clockTime = 0.0f;
    private float currentBlend = 0.0f;
    private const float blendLength = 6.0f;

    public float normalizedClock => Tools.NormalizeValue(clockTime, 0.0f, dayDurationInSeconds);
    private Vector3 currentSunRotation => sunDirectionalLight.transform.rotation.eulerAngles;

    private void Start()
    {
        skyboxMaterial.SetFloat("_Blend", currentBlend);
    }

    private void Update()
    {
        clockTime += Time.deltaTime;
        if (clockTime >= dayDurationInSeconds)
            clockTime = dayDurationInSeconds - clockTime;

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
