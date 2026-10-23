using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private float dayDurationInSeconds;

    private float clockTime = 0.0f;
    private float currentBlend = 0.0f;
    private const float blendLength = 6.0f;

    public float normalizedClock => Tools.NormalizeValue(clockTime, 0.0f, blendLength);

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
    }
}
