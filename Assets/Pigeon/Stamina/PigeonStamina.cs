using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Réglages de l'endurance et sa lecture pour l'affichage. L'endurance elle-même fait partie de l'état prédit du pigeon
/// (PigeonController.Stamina.cs) : battre des ailes en dépend, toutes les machines doivent donc la calculer pareil.
/// </summary>
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

    public float MaxStamina => maxStamina;
    public float FlapThreshold => staminaNormalizedThresholdToFlap * maxStamina;
    public float DepletionRateFlap => staminaDepletionRate_Flap;
    public float RecoveryRateRegular => staminaRecoveryRate_Regular;
    public float RecoveryRateBrake => staminaRecoveryRate_Brake;
    public float RecoveryRateUnderWater => staminaRecoveryRate_UnderWater;
    public float RecoveryRateDive => staminaRecoveryRate_Dive;
    public float InstantRecoveryRoll => staminaInstantRecovery_Roll;
    public float InstantRecoveryBoost => staminaInstantRecovery_Boost;

    public float CurrentStamina => pigeonController != null ? pigeonController.Stamina : maxStamina;
    public float CurrentStaminaNormalized => Tools.NormalizeValue(CurrentStamina, 0.0f, maxStamina);
    public bool HasStamina => CurrentStamina > 0.0f;
    public bool HasEnoughStaminaToFlap => CurrentStamina >= FlapThreshold;
    public bool HasMaxStamina => CurrentStamina >= maxStamina;

    private PigeonController pigeonController;
    private float lastStamina;
    private byte lastBoosts;
    private bool started;

    private void Start()
    {
        pigeonController = GetComponent<PigeonController>();
    }

    private void LateUpdate()
    {
        if (pigeonController == null)
            return;
        float stamina = pigeonController.Stamina;
        byte boosts = pigeonController.StaminaBoosts;
        if (started)
        {
            if (boosts != lastBoosts)
                OnBoostStamina?.Invoke();
            else if (stamina < lastStamina)
                OnUseStamina?.Invoke();
        }
        started = true;
        lastStamina = stamina;
        lastBoosts = boosts;

        IPigeonControls controls = pigeonController.Controls;
        if (pigeonController.isOwner && pigeonController.UsesStamina && controls != null && controls.Flap && !HasEnoughStaminaToFlap)
            OnCannotUseStamina?.Invoke();
    }
}
