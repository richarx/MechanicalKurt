using System.Collections;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DialogCinematic : MonoBehaviour
{
    private static readonly int TalkHash = Animator.StringToHash("Talk");
    private static readonly int SendOffHash = Animator.StringToHash("SendOff");

    [field: SerializeField, Required]
    public ManagerCinematic CinematicManager { get; private set; }

    [field: SerializeField, Tooltip("Facultative : conserver la vue actuelle si vide.")]
    public CinemachineVirtualCameraBase CinematicCamera { get; private set; }

    [field: SerializeField, Tooltip("Facultatif : utilise DialogManager.instance si vide.")]
    public DialogManager DialogManager { get; private set; }

    [field: SerializeField, Required]
    public Animator CharacterAnimator { get; private set; }

    [field: SerializeField, Required]
    public DialogSequence Dialogue { get; private set; }

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Durée du plan après SendOff, transition de l'animation comprise.")]
    public float SendOffSeconds { get; private set; } = 1f;

    [field: SerializeField]
    public UnityEvent OnCinematicStarted { get; private set; } = new UnityEvent();

    [field: SerializeField]
    public UnityEvent OnCinematicEnded { get; private set; } = new UnityEvent();

    private enum Phase { Idle, Entering, Dialog, SendOff, Returning }
    private Phase _phase;
    private DialogManager _dialogs;
    private Coroutine _routine;
    private bool _ownsDialog;

    [ShowInInspector, ReadOnly]
    public bool IsPlaying => _phase != Phase.Idle;

    // Brancher cette méthode sur PigeonStopZone.OnPlayerStopped.
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled || IsPlaying)
            return;

        _dialogs = DialogManager != null ? DialogManager : global::DialogManager.instance;
        if (CinematicManager == null || _dialogs == null || !_dialogs.isActiveAndEnabled
            || Dialogue == null || Dialogue.lines == null || Dialogue.lines.Count == 0
            || !HasTrigger(TalkHash) || !HasTrigger(SendOffHash))
        {
            Debug.LogWarning("DialogCinematic : assigner les managers, un dialogue non vide et un Animator actif avec les triggers Talk et SendOff.", this);
            return;
        }

        if (_dialogs.IsPlaying)
            return;

        _phase = Phase.Entering;
        if (!CinematicManager.BeginCinematic(CinematicCamera, BeginDialog, HandleCinematicEnded))
        {
            _phase = Phase.Idle;
            _dialogs = null;
            return;
        }
        OnCinematicStarted.Invoke();
    }

    private void BeginDialog()
    {
        if (_phase != Phase.Entering)
            return;
        if (_dialogs == null || !_dialogs.isActiveAndEnabled || _dialogs.IsPlaying)
        {
            CancelCinematic();
            return;
        }

        _phase = Phase.Dialog;
        _ownsDialog = true;
        _dialogs.OnLineStarted.AddListener(HandleLineStarted);
        _dialogs.OnDialogEnded.AddListener(HandleDialogEnded);
        _dialogs.StartDialog(Dialogue);
    }

    private void HandleLineStarted(int lineIndex)
    {
        if (_phase != Phase.Dialog)
            return;
        if (_dialogs.CurrentSequence != Dialogue)
        {
            // Un autre dialogue a remplacé le nôtre : le laisser ouvert.
            CancelCinematic();
            return;
        }
        CharacterAnimator.ResetTrigger(SendOffHash);
        CharacterAnimator.ResetTrigger(TalkHash);
        CharacterAnimator.SetTrigger(TalkHash);
    }

    private void HandleDialogEnded()
    {
        if (_phase != Phase.Dialog)
            return;

        ReleaseDialog(false);
        _phase = Phase.SendOff;
        CharacterAnimator.ResetTrigger(TalkHash);
        CharacterAnimator.ResetTrigger(SendOffHash);
        CharacterAnimator.SetTrigger(SendOffHash);
        _routine = StartCoroutine(FinishSendOff());
    }

    private IEnumerator FinishSendOff()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, SendOffSeconds));
        _routine = null;
        CancelCinematic();
    }

    public void CancelCinematic()
    {
        if (!IsPlaying || _phase == Phase.Returning)
            return;

        _phase = Phase.Returning;
        StopSendOff();
        ReleaseDialog(true);
        if (CinematicManager != null)
            CinematicManager.CancelCinematic();
        else
            HandleCinematicEnded();
    }

    private void HandleCinematicEnded()
    {
        if (!IsPlaying)
            return;

        StopSendOff();
        ReleaseDialog(true);
        _dialogs = null;
        _phase = Phase.Idle;
        OnCinematicEnded.Invoke();
    }

    private void ReleaseDialog(bool close)
    {
        if (!_ownsDialog)
            return;
        _ownsDialog = false;
        if (_dialogs == null)
            return;
        _dialogs.OnLineStarted.RemoveListener(HandleLineStarted);
        _dialogs.OnDialogEnded.RemoveListener(HandleDialogEnded);
        if (close)
            _dialogs.CancelDialog(Dialogue);
    }

    private void StopSendOff()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;
    }

    private void Update()
    {
        if (IsPlaying && _phase != Phase.Returning
            && (CinematicManager == null || _dialogs == null || !_dialogs.isActiveAndEnabled
                || CharacterAnimator == null || !CharacterAnimator.isActiveAndEnabled))
            CancelCinematic();
    }

    private void OnDisable() => CancelCinematic();

    private bool HasTrigger(int hash)
    {
        if (CharacterAnimator == null || !CharacterAnimator.isActiveAndEnabled
            || CharacterAnimator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in CharacterAnimator.parameters)
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        return false;
    }
}
