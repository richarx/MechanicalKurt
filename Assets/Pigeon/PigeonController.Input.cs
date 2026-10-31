using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public partial class PigeonController
{
    bool _respawnRequested;
    byte _equipmentWearRequested;
    byte _equipmentToggleRequested;

    /// <summary>Source d'input actuelle (joueur ou bot). Les scripts qui lisent des appuis passent par ici plutôt que par PlayerControls.</summary>
    public IPigeonControls Controls => _controls;

    /// <summary>Remplace la source d'input (bots, scénarios de test). null rétablit le composant IPigeonControls.</summary>
    public void SetControlsOverride(IPigeonControls controls)
    {
        _controls = controls ?? GetComponent<IPigeonControls>();
    }

    /// <summary>Demande le retour au point d'apparition. Ignorée si ce pigeon n'est pas celui du joueur local.</summary>
    public void RequestRespawn()
    {
        if (!isController)
            return;
        _respawnRequested = true;
    }

    /// <summary>
    /// Demande à porter (ou basculer) un équipement. Passe par l'input : la simulation l'applique, puis l'état le
    /// montre à tous les clients. Ignorée si ce pigeon n'est pas contrôlé ici.
    /// </summary>
    public void RequestEquipment(PigeonEquipment.Equipment equipment, bool toggle = false)
    {
        if (!isController)
            return;
        byte bit = PigeonEquipment.Bit(equipment);
        if (toggle)
            _equipmentToggleRequested ^= bit;
        else
            _equipmentWearRequested |= bit;
    }

    protected override void UpdateInput(ref PigeonInput input)
    {
        if (_controls == null)
            return;

        input.BarrelLeft |= _controls.ConsumeLeftFlex;
        input.BarrelRight |= _controls.ConsumeRightFlex;
        input.Interact |= _controls.ConsumePredictedInteract;
    }

    protected override void GetFinalInput(ref PigeonInput input)
    {
        input.Respawn = _respawnRequested;
        _respawnRequested = false;
        input.EquipmentWear = _equipmentWearRequested;
        input.EquipmentToggle = _equipmentToggleRequested;
        _equipmentWearRequested = _equipmentToggleRequested = 0;
        WriteMissionInput(ref input);

        if (_controls == null)
        {
            input.Movement = Vector2.zero;
            input.RelativeMovement = Vector2.zero;
            input.Flap = input.Dive = input.Brake = false;
            return;
        }

        input.Movement = _controls.Movement;
        input.RelativeMovement = _controls.RelativeMovement;
        input.Flap = _controls.Flap;
        input.Dive = _controls.Dive;
        input.Brake = _controls.Brake;
    }

    protected override void SanitizeInput(ref PigeonInput input)
    {
        input.Movement = Vector2.ClampMagnitude(input.Movement, 1f);
        input.RelativeMovement = Vector2.ClampMagnitude(input.RelativeMovement, 1f);
        input.EquipmentWear &= PigeonEquipment.AllBits;
        input.EquipmentToggle &= PigeonEquipment.AllBits;
    }

    protected override void ModifyExtrapolatedInput(ref PigeonInput input)
    {
        input.BarrelLeft = false;
        input.BarrelRight = false;
        input.Interact = false;
        input.Respawn = false;
        input.EquipmentWear = 0;
        input.EquipmentToggle = 0;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _cursorInitialized)
            SetCursorCaptured(false);
    }

    void UpdateCursorCapture()
    {
        if (_controls is PlayerControls playerControls && playerControls.AreControlsBlocked)
            return;

        if (!Application.isFocused)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorCaptured(false);
        else if (Cursor.lockState != CursorLockMode.Locked && Time.timeScale > 0f
            && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            SetCursorCaptured(true);
    }

    void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }
}
