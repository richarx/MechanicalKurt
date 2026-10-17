using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeliverySessionHUD : MonoBehaviour
{
    [SerializeField, Required] private DeliverySession _session;
    [SerializeField, Required] private TMP_Text _timerText;
    [SerializeField, Required] private TMP_Text _stateText;

    #region Display

    private void OnEnable()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_session == null)
        {
            if (_timerText != null)
                _timerText.text = "--:--";
            if (_stateText != null)
                _stateText.text = "Session missing";
            return;
        }

        if (_timerText != null)
        {
            double wholeSeconds = Math.Ceiling(_session.RemainingSeconds);
            _timerText.text = $"{Math.Floor(wholeSeconds / 60d):00}:{wholeSeconds % 60d:00}";
        }

        if (_stateText == null)
            return;

        if (!_session.isActiveAndEnabled)
            _stateText.text = "Session disabled";
        else if (_session.State == DeliverySessionState.Finished)
            _stateText.text = "Time's up";
        else if (_session.State == DeliverySessionState.Ready)
            _stateText.text = _session.IsValid ? "Ready" : "Invalid duration";
        else
            _stateText.text = _session.IsPaused ? "Paused" : "Running";
    }

    #endregion
}
