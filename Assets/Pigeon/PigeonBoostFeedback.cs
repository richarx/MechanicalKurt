using DG.Tweening;
using UnityEngine;

// Hand-made boost animation: barrel roll + stretch on a pivot between the visual root and the mesh.
// The pivot must not be the PigeonController's _visualRoot (its rotation is overwritten every frame).
public class PigeonBoostFeedback : MonoBehaviour
{
    [SerializeField] Transform _rollPivot;

    [Header("Roll")]
    [SerializeField] float _rollDuration = 0.45f;
    [SerializeField] int _rollCount = 1;
    [SerializeField] Ease _rollEase = Ease.OutCubic;

    [Header("Stretch")]
    [SerializeField] Vector3 _stretchScale = new Vector3(0.8f, 0.8f, 1.3f);
    [SerializeField] float _stretchDuration = 0.35f;

    Sequence _sequence;
    Quaternion _baseRotation;
    Vector3 _baseScale;

    void Awake()
    {
        _baseRotation = _rollPivot.localRotation;
        _baseScale = _rollPivot.localScale;
    }

    public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

    // restart: false ignores the call while a roll is already playing.
    public void Play(bool restart = true, float duration = -1f, bool isLeft = true)
    {
        if (!restart && IsPlaying)
            return;

        if (duration == -1f)
            duration = _rollDuration;

        _sequence?.Kill();
        ResetPivot();

        float stretchIn = _stretchDuration * 0.3f;
        float stretchOut = _stretchDuration - stretchIn;

        _sequence = DOTween.Sequence()
            .Insert(0f, _rollPivot.DOLocalRotate(new Vector3(0f, 0f, 360f * _rollCount * (isLeft ? 1.0f : -1.0f)), duration, RotateMode.LocalAxisAdd).SetEase(_rollEase))
            .Insert(0f, _rollPivot.DOScale(Vector3.Scale(_baseScale, _stretchScale), stretchIn).SetEase(Ease.OutQuad))
            .Insert(stretchIn, _rollPivot.DOScale(_baseScale, stretchOut).SetEase(Ease.OutBack))
            .OnComplete(ResetPivot);
    }

    void ResetPivot()
    {
        _rollPivot.localRotation = _baseRotation;
        _rollPivot.localScale = _baseScale;
    }

    void OnDestroy()
    {
        _sequence?.Kill();
    }
}
