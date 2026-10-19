using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PigeonSFX : MonoBehaviour
{
    [SerializeField] private List<AudioClip> coohClips;
    [SerializeField] private AudioClip takeOffClip;
    [SerializeField] private AudioClip flyLoopClip;
    [SerializeField] private AudioClip poopClip;
    [SerializeField] private List<AudioClip> rollClips;

    [Space]
    [SerializeField] private Transform waterPlane;
    [SerializeField] private AudioClip enterWater;
    [SerializeField] private AudioClip exitWater;

    private IPigeonControls controls;
    private PigeonController pigeonController;
    private ButtHoleManager buttHoleManager;

    private bool isCurrentlyUnderWater;

    private AudioSource flySource;
    private Sequence flySequence;

    private void Start()
    {
        controls = GetComponent<IPigeonControls>();
        pigeonController = GetComponent<PigeonController>();
        buttHoleManager = GetComponent<ButtHoleManager>();

        pigeonController.OnTakeoff.AddListener(() => SFXManager.Instance.PlaySFX(takeOffClip));
        pigeonController.OnRoll.AddListener(() => SFXManager.Instance.PlayRandomSFX(rollClips));
        buttHoleManager.OnShit.AddListener(() => SFXManager.Instance.PlaySFX(poopClip));
    }

    private void Update()
    {
        if (controls.ConsumeCooing)
            SFXManager.Instance.PlayRandomSFX(coohClips, 1.0f);

        bool isUnderWater = transform.position.y < waterPlane.position.y;
        if (isUnderWater != isCurrentlyUnderWater)
        {
            isCurrentlyUnderWater = isUnderWater;

            if (isCurrentlyUnderWater)
                SFXManager.Instance.PlaySFX(enterWater);
            else
                SFXManager.Instance.PlaySFX(exitWater);
        }

        if (!pigeonController.IsGrounded && !controls.Flap)
        {
            if (flySource == null)
                flySource = SFXManager.Instance.PlaySFX(flyLoopClip, 0.5f, loop: true);

            if (flySequence != null && flySequence.IsPlaying())
                flySequence.Kill();

            if (flySource.volume < 0.5f)
            {
                flySequence = DOTween.Sequence();

                if (flySource.isPlaying)
                    flySequence.Append(flySource.DOFade(0.5f, 0.5f));
            }

            flySource.pitch = Tools.NormalizeValue(pigeonController.Speed, 5.0f, 25.0f);
        }
        else
        {
            if (flySource)
            {
                if (flySequence != null && flySequence.IsPlaying())
                    flySequence.Kill();

                flySequence = DOTween.Sequence();

                if (flySource.isPlaying)
                    flySequence.Append(flySource.DOFade(0.0f, 0.5f));
            }
        }
    }
}
