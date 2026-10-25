using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

// Fly through to get a one-shot speed kick along the pigeon's direction of travel (works from both sides).
[RequireComponent(typeof(Collider))]
public class BoostRing : MonoBehaviour
{
    [Header("Boost")]
    [SerializeField] float _boostAmount = 15f;
    [SerializeField] float _minExitSpeed = 35f;
    [SerializeField] float _maxSpeed = 55f;
    [SerializeField] float _cooldown = 0.5f;

    [Header("Feedback")]
    [SerializeField] Transform _visual;
    [SerializeField] float _punchScale = 0.3f;
    [SerializeField] float _punchDuration = 0.4f;
    [SerializeField] List<AudioClip> _mainSfx;
    [SerializeField] float _mainSfxVolume = 0.3f;
    [SerializeField] List<AudioClip> _secondarySfx;
    [SerializeField] float _secondarySfxVolume = 0.3f;

    float _lastTriggerTime = float.NegativeInfinity;
    Tween _punchTween;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - _lastTriggerTime < _cooldown)
            return;

        PigeonController pigeon = other.GetComponentInParent<PigeonController>();
        if (pigeon == null)
            return;
        CharacterController characterController = pigeon.GetComponent<CharacterController>();
        _lastTriggerTime = Time.time;

        Vector3 velocity = characterController.velocity; //pigeon.LastVelocity;
        float speed = velocity.magnitude;

        Debug.Log(characterController.velocity);
        Debug.Log(pigeon.LastVelocity);
        Vector3 direction = speed > 0.1f ? velocity / speed : pigeon.transform.forward;

        float targetSpeed = Mathf.Min(Mathf.Max(speed + _boostAmount, _minExitSpeed), _maxSpeed);
        if (targetSpeed > speed)
            pigeon.AddImpulse(direction * (targetSpeed - speed));
        Debug.Log(direction * (targetSpeed - speed));
        PigeonBoostFeedback feedback = pigeon.GetComponent<PigeonBoostFeedback>();
        if (feedback != null)
            feedback.Play();

        PlayRingFeedback();
    }

    void PlayRingFeedback()
    {
        if (_visual != null)
        {
            _punchTween?.Complete();
            _punchTween = _visual.DOPunchScale(Vector3.one * _punchScale, _punchDuration, 6, 0.5f);
        }

        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayRandomSFX(_mainSfx, _mainSfxVolume);
            SFXManager.Instance.PlayRandomSFX(_secondarySfx, _secondarySfxVolume);
        }
    }

    void OnDestroy()
    {
        _punchTween?.Kill();
    }
}
