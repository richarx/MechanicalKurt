using UnityEngine;

public class WindVelocityControl : MonoBehaviour
{
    [SerializeField] private PigeonController pigeonController;

    [SerializeField] private float minimumSpeedThreshold = 2f;
    [SerializeField] private float speedMultiplier = 0.5f;
    [SerializeField] private float emissionMultiplier = 2f;

    [SerializeField] private float turbulenceStrength = 0.05f;

    private ParticleSystem windParticles;
    private ParticleSystem.MainModule windMain;
    private ParticleSystem.EmissionModule windEmission;
    private ParticleSystem.VelocityOverLifetimeModule windVelocity;

    private void Start()
    {
        windParticles = GetComponent<ParticleSystem>();
        windMain = windParticles.main;
        windEmission = windParticles.emission;
        windVelocity = windParticles.velocityOverLifetime;

        windMain.simulationSpace = ParticleSystemSimulationSpace.World;
        windVelocity.enabled = true;
        windVelocity.space = ParticleSystemSimulationSpace.World;
    }

    private void Update()
    {
        if (pigeonController == null)
            return;

        Vector3 velocity = pigeonController.Velocity;
        float currentSpeed = velocity.magnitude;

        if (currentSpeed > 0.1f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);

        if (currentSpeed < minimumSpeedThreshold)
        {
            windMain.startSpeed = 0f;
            windEmission.rateOverTime = 0f;

            windVelocity.x = new ParticleSystem.MinMaxCurve(0f);
            windVelocity.y = new ParticleSystem.MinMaxCurve(0f);
            windVelocity.z = new ParticleSystem.MinMaxCurve(0f);
        }
        else
        {
            float backwardSpeed = -currentSpeed * speedMultiplier;
            windMain.startSpeed = backwardSpeed;

            windEmission.rateOverTime = currentSpeed * emissionMultiplier;

            float currentTurbulence = currentSpeed * turbulenceStrength;

            windVelocity.x = new ParticleSystem.MinMaxCurve(-currentTurbulence, currentTurbulence);
            windVelocity.y = new ParticleSystem.MinMaxCurve(-currentTurbulence, currentTurbulence);

            windVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }
    }
}
