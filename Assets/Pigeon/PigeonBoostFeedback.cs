using System;
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

    [Header("Particles")]
    [SerializeField] private ParticleSystem smokeParticles;
    [SerializeField] private ParticleSystem speedParticles;
    [SerializeField] private Material speedMaterial;

    private Sequence _sequence;
    private Sequence materialSequence;
    private Quaternion _baseRotation;
    private Vector3 _baseScale;

    private PigeonController pigeonController;

    private void Awake()
    {
        _baseRotation = _rollPivot.localRotation;
        _baseScale = _rollPivot.localScale;
    }

    private void Start()
    {
        pigeonController = GetComponent<PigeonController>();
        pigeonController.OnStunned.AddListener(CancelParticles);
    }

    public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

    // restart: false ignores the call while a roll is already playing.
    public void Play(bool restart = true, float duration = -1f, bool isLeft = true, bool playParticleEffect = true)
    {
        if (!restart && IsPlaying)
            return;

        if (playParticleEffect)
        {
            smokeParticles.Play();
            speedParticles.Play();

            materialSequence?.Kill();
            materialSequence = DOTween.Sequence()
                .Append(speedMaterial.DOFloat(0.7f, "_Alpha", 0.1f))
                .AppendInterval(1.3f)
                .Append(speedMaterial.DOFloat(0.0f, "_Alpha", 0.3f));
        }

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

    private void CancelParticles()
    {
        smokeParticles.Stop();
        speedParticles.Stop();

        materialSequence?.Complete();
    }

    void ResetPivot()
    {
        _rollPivot.localRotation = _baseRotation;
        _rollPivot.localScale = _baseScale;
    }

    void OnDestroy()
    {
        _sequence?.Kill();
        materialSequence?.Kill();
        speedMaterial.SetFloat("_Alpha", 0.0f);
    }
}
