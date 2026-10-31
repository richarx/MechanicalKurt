using UnityEngine;
using UnityEngine.Events;

public class PigeonStamina : MonoBehaviour
{
    [HideInInspector] public UnityEvent OnBoostStamina = new UnityEvent();
    [HideInInspector] public UnityEvent OnCannotUseStamina = new UnityEvent();
    [HideInInspector] public UnityEvent OnUseStamina = new UnityEvent();

    [SerializeField] private float maxStamina;

    [Space]
    [SerializeField] private float staminaNormalizedThresholdToFlap;

    [Space]
    [SerializeField] private float staminaDepletionRate_Flap;

    [Space]
    [SerializeField] private float staminaRecoveryRate_Regular;
    [SerializeField] private float staminaRecoveryRate_Brake;
    [SerializeField] private float staminaRecoveryRate_UnderWater;
    [SerializeField] private float staminaRecoveryRate_Dive;

    [Space]
    [SerializeField] private float staminaInstantRecovery_Roll;
    [SerializeField] private float staminaInstantRecovery_Boost;

    private float currentStamina;
    public float CurrentStamina => currentStamina;
    public float CurrentStaminaNormalized => Tools.NormalizeValue(currentStamina, 0.0f, maxStamina);

    public float MaxStamina => maxStamina;
    public bool HasStamina => currentStamina > 0.0f;
    public bool HasEnoughStaminaToFlap => CurrentStaminaNormalized >= staminaNormalizedThresholdToFlap;
    public bool HasMaxStamina => currentStamina >= maxStamina;

    private PigeonController pigeonController;

    private void Start()
    {
        pigeonController = GetComponent<PigeonController>();

        pigeonController.OnRoll.AddListener(() =>
        {
            if (!HasMaxStamina)
                UpdateStamina(staminaInstantRecovery_Roll);
        });
        pigeonController.OnBoost.AddListener(() =>
        {
            if (!HasMaxStamina)
                UpdateStamina(staminaInstantRecovery_Boost);
        });
        pigeonController.OnLand.AddListener(() =>
        {
            if (!HasMaxStamina)
                UpdateStamina(maxStamina);
        });

        SetStamina(maxStamina);
    }

    private void LateUpdate()
    {
        if (pigeonController.CurrentPigeonState == PigeonController.PigeonState.Diving)
            UpdateStaminaAtRate(staminaRecoveryRate_Dive);
        else if (pigeonController.CurrentPigeonState == PigeonController.PigeonState.Flapping)
        {
            UpdateStaminaAtRate(-staminaDepletionRate_Flap);
        }
        else if (pigeonController.CurrentPigeonState == PigeonController.PigeonState.Braking)
            UpdateStaminaAtRate(staminaRecoveryRate_Brake);
        else if (pigeonController.IsUndeWater)
            UpdateStaminaAtRate(staminaRecoveryRate_UnderWater);
        else if (!HasMaxStamina)
            UpdateStaminaAtRate(staminaRecoveryRate_Regular);

        if (pigeonController.Controls.Flap && !HasEnoughStaminaToFlap)
            OnCannotUseStamina?.Invoke();
    }

    private void UpdateStaminaAtRate(float toAdd)
    {
        SetStamina(currentStamina + toAdd * Time.deltaTime);
        if (toAdd < 0.0f)
            OnUseStamina?.Invoke();
    }

    private void UpdateStamina(float toAdd)
    {
        SetStamina(currentStamina + toAdd);
        OnBoostStamina?.Invoke();
    }

    private void SetStamina(float newValue)
    {
        currentStamina = Mathf.Clamp(newValue, 0.0f, maxStamina);
    }
}
