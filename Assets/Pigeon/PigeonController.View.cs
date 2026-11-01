using Unity.Cinemachine;
using UnityEngine;

public partial class PigeonController
{
    protected override void UpdateView(PigeonState view, PigeonState? verified)
    {
        _viewState = view;

        Vector3 position = view.Position;
        if (view.SupportId != 0 && MovingSupport.TryGet(view.SupportId, out MovingSupport support))
            position = support.ViewTransform.TransformPoint(view.SupportLocalAnchor);

        transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(view.Stunned ? 0f : -view.Pitch, view.Yaw, 0f));

        UpdateRagdollView(view);
        UpdateVisualRotation(view, Time.deltaTime);
        if (_equipment != null)
            _equipment.ApplyWorn(view.Equipment);
        UpdateCooView(view.CooCount);
    }

    byte _viewCooCount;
    bool _viewCooKnown;

    /// <summary>
    /// OnCoo quand le compteur avance. Rien à la première vue (arrivée en cours de partie), et un retour en arrière
    /// (correction de prédiction) ne rejoue rien.
    /// </summary>
    void UpdateCooView(byte count)
    {
        if (!_viewCooKnown)
        {
            _viewCooKnown = true;
            _viewCooCount = count;
            return;
        }
        byte ahead = (byte)(count - _viewCooCount);
        if (ahead == 0 || ahead >= 128)
            return;
        _viewCooCount = count;
        OnCoo.Invoke();
    }

    void UpdateRagdollView(in PigeonState view)
    {
        if (view.Stunned == _viewStunned)
            return;
        _viewStunned = view.Stunned;

        if (_ragdoll == null)
            return;
        if (_viewStunned)
            _ragdoll.SetRagdollState(true);
        else
            _ragdoll.EndRagdoll();
    }

    void UpdateVisualRotation(in PigeonState view, float dt)
    {
        if (_visualRoot == null)
            return;

        if (view.Stunned)
        {
            _visualRoot.localRotation = view.StunRotation;
            return;
        }

        bool groundMode = view.Grounded && (view.LastVelocity.y <= 0.001f || view.SupportId != 0);
        Quaternion target = groundMode || currentInput.Brake
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, -currentInput.RelativeMovement.x * _visualRollAmount);
        float speed = groundMode ? 10f : _rollSpeed;
        _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, target, speed * dt);
    }

    void PlayRoll(bool isLeft)
    {
        if (_boostFeedback != null)
            _boostFeedback.Play(restart: true, _rollDuration, isLeft, false);
        OnRoll.Invoke();
    }

    void OnTeleported(Vector3 delta)
    {
        ResetInterpolation();
        if (_visualRoot != null)
            _visualRoot.localRotation = Quaternion.identity;

        if (!isOwner)
            return;
        CinemachineCore.OnTargetObjectWarped(transform, delta);
        if (TryGetComponent(out CameraSwapper cameras))
            cameras.SnapNow();
        if (_controls is PlayerControls playerControls)
            playerControls.SnapCameraBehind();
    }

    void OnGUI()
    {
        if (_activeDebug && isOwner)
        {
            GUI.Label(new Rect(10, 50, 300, 20), $"Pitch: {_viewState.Pitch:F1}");
            GUI.Label(new Rect(10, 80, 300, 20), $"Speed: {Speed:F1}");
            GUI.Label(new Rect(10, 110, 300, 20), $"HorizontalSpeed: {HorizontalSpeed:F1}");
            GUI.Label(new Rect(10, 140, 300, 20), $"VerticalSpeed: {_viewState.LastVelocity.y:F1}");
        }
    }
}
