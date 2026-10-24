using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>Unlocks tutorial achievements from actual flight and mission activation.</summary>
[DisallowMultipleComponent]
public class PigeonTutorialAchievements : MonoBehaviour
{
    [field: SerializeField, Required]
    public AchievementManager Manager { get; private set; }

    [field: SerializeField, Required]
    public PigeonController Player { get; private set; }

    [field: SerializeField, Required]
    public DeliveryOfferSelector MissionZones { get; private set; }

    [field: SerializeField, Required]
    public AchievementData Takeoff { get; private set; }

    [field: SerializeField, Required]
    public AchievementData Steering { get; private set; }

    [field: SerializeField, Required]
    public AchievementData Interaction { get; private set; }

    [field: SerializeField, MinValue(1f), MaxValue(180f), SuffixLabel("degrees", true)]
    public float SteeringAngle { get; private set; } = 45f;

    private CharacterController _body;
    private IPigeonControls _controls;
    private bool _takeoffRequested;
    private float _previousYaw;
    private float _steeredAngle;

    private void OnEnable()
    {
        if (Player != null)
        {
            _body = Player.GetComponent<CharacterController>();
            _controls = Player.GetComponent<IPigeonControls>();
            ResetFlightProgress();
        }
        if (MissionZones != null) MissionZones.OnOfferAccepted.AddListener(HandleMissionActivated);
        if (Manager != null) Manager.OnReset += ResetFlightProgress;
    }

    private void OnDisable()
    {
        if (MissionZones != null) MissionZones.OnOfferAccepted.RemoveListener(HandleMissionActivated);
        if (Manager != null) Manager.OnReset -= ResetFlightProgress;
    }

    private void LateUpdate()
    {
        if (Manager == null || Player == null || !Player.isActiveAndEnabled
            || _body == null || _controls == null || Time.timeScale <= 0f) return;

        float yaw = Player.transform.eulerAngles.y;
        float turn = Mathf.Abs(Mathf.DeltaAngle(_previousYaw, yaw));
        _previousYaw = yaw;
        if (_body.isGrounded)
        {
            _takeoffRequested = _controls.Flap;
            _steeredAngle = 0f;
            return;
        }

        if (!Manager.IsUnlocked(Takeoff))
        {
            // Remember flapping until actual ascent, including a short tap followed by release.
            _takeoffRequested |= _controls.Flap;
            if (_takeoffRequested && Player.LastVelocity.y > 0.01f)
                Manager.Unlock(Takeoff);
            return;
        }

        // Count only turns made with steering input while flying, after takeoff.
        if (!Manager.IsUnlocked(Steering) && Mathf.Abs(_controls.RelativeMovement.x) > 0.2f)
        {
            _steeredAngle += turn;
            if (_steeredAngle >= SteeringAngle) Manager.Unlock(Steering);
        }
    }

    private void HandleMissionActivated(DeliveryOffer offer)
    {
        if (offer == null || Manager == null || Player == null || !Player.isActiveAndEnabled
            || MissionZones == null || MissionZones.Player != Player.transform
            || _body == null || !_body.isGrounded || !Manager.IsUnlocked(Steering)) return;
        Manager.Unlock(Interaction);
    }

    private void ResetFlightProgress()
    {
        _takeoffRequested = false;
        _steeredAngle = 0f;
        if (Player == null) return;
        _previousYaw = Player.transform.eulerAngles.y;
    }
}
