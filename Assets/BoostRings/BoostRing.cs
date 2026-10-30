using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Fly through to get a one-shot speed kick along the pigeon's direction of travel (works from both sides).
/// <summary>
/// ParamÃ¨tres et feedback d'un anneau ; le boost lui-mÃªme est dÃ©tectÃ© et appliquÃ© par PigeonController.Simulate.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BoostRing : MonoBehaviour
{
    [Header("Boost")]
    [SerializeField] float _boostAmount = 15f;
    [SerializeField] float _minExitSpeed = 35f;
    [SerializeField] float _maxSpeed = 55f;

    [Header("Feedback")]
    [SerializeField] Transform _visual;
    [SerializeField] float _punchScale = 0.3f;
    [SerializeField] float _punchDuration = 0.4f;
    [SerializeField] List<AudioClip> _mainSfx;
    [SerializeField] float _mainSfxVolume = 0.3f;
    [SerializeField] List<AudioClip> _secondarySfx;
    [SerializeField] float _secondarySfxVolume = 0.3f;

    public const float ComboWindow = 5f;
    const float ComboPitchStep = 0.1f;
    const float ComboMaxPitch = 3f;

    Tween _punchTween;

    public float BoostAmount => _boostAmount;
    public float MinExitSpeed => _minExitSpeed;
    public float MaxSpeed => _maxSpeed;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public void PlayRingFeedback(int comboCount = 1)
    {
        if (_visual != null)
        {
            _punchTween?.Complete();
            _punchTween = _visual.DOPunchScale(Vector3.one * _punchScale, _punchDuration, 6, 0.5f);
        }

        if (SFXManager.Instance != null)
        {
            float pitch = Mathf.Min(1.0f + comboCount * ComboPitchStep, ComboMaxPitch);
            SFXManager.Instance.PlayRandomSFX(_mainSfx, _mainSfxVolume, pitch: pitch);
            SFXManager.Instance.PlayRandomSFX(_secondarySfx, _secondarySfxVolume);
        }
    }

    void OnDestroy()
    {
        _punchTween?.Kill();
    }
}
