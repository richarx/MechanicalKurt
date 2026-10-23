using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class SpecialDanceActor
{
    [field: SerializeField, Required]
    public Animator Animator { get; private set; }

    [field: SerializeField, Tooltip("Racine à déplacer. Si vide, déplace le Transform de l'Animator.")]
    public Transform MovementRoot { get; private set; }

    [field: SerializeField, MinValue(0.01f), SuffixLabel("s", true)]
    public float WalkSeconds { get; private set; } = 2f;

    [field: SerializeField, MinValue(0.01f), SuffixLabel("s", true)]
    public float BackWalkSeconds { get; private set; } = 2f;

    public Transform Root => MovementRoot != null ? MovementRoot : Animator != null ? Animator.transform : null;
}

[DisallowMultipleComponent]
public class SpecialDanceCinematic : MonoBehaviour
{
    private static readonly int StartDanceHash = Animator.StringToHash("StartDance");
    private static readonly int WalkHash = Animator.StringToHash("Walk");
    private static readonly int DanceHash = Animator.StringToHash("SpecialDanse");
    private static readonly int BackWalkHash = Animator.StringToHash("BackWalk");
    private static readonly int BackHash = Animator.StringToHash("Back");

    [field: SerializeField, Required, Title("Managers")]
    public ManagerCinematic CinematicManager { get; private set; }

    [field: SerializeField, Tooltip("Utilise le singleton de la scène si vide.")]
    public DialogManager DialogManager { get; private set; }

    [field: SerializeField, Tooltip("Pigeon à immobiliser, même en vol. Recherche automatique si vide.")]
    public PigeonController Player { get; private set; }

    [field: SerializeField, Required, Title("Caméras Cinemachine")]
    public CinemachineVirtualCameraBase DialogueCamera { get; private set; }

    [field: SerializeField, Required]
    public CinemachineVirtualCameraBase DescentCamera { get; private set; }

    [field: SerializeField, Required]
    public CinemachineVirtualCameraBase DanceCamera { get; private set; }

    [field: SerializeField, Required, Title("Dialogue et musique")]
    [field: Tooltip("Dialogue non vide avec effet machine à écrire. X est partagé entre ses répliques, révélation du texte comprise : avec deux répliques, chacune dure X/2.")]
    public DialogSequence Dialogue { get; private set; }

    [field: SerializeField, MinValue(0.01f), SuffixLabel("s (X)", true)]
    public float DialogueSeconds { get; private set; } = 9f;

    [field: SerializeField, Required]
    public AudioClip MusicClip { get; private set; }

    [field: SerializeField, Tooltip("MusicPlayer utilisé pour jouer Music Clip puis rétablir la musique normale. Récupéré automatiquement si vide.")]
    public MusicPlayer BackgroundMusic { get; private set; }

    [field: SerializeField, Title("Descente"), Required]
    public Transform DescendingObject { get; private set; }

    [field: SerializeField, Required, Tooltip("Point indépendant de l'objet déplacé, en coordonnées monde.")]
    public Transform DescentTarget { get; private set; }

    [field: SerializeField, MinValue(0.01f), SuffixLabel("s (Y)", true)]
    public float DescentSeconds { get; private set; } = 4f;

    [field: SerializeField, Tooltip("Lumière facultative : activée au début de la descente, son intensité monte de 0 à 900 en parallèle du déplacement.")]
    public Light DescentLight { get; private set; }

    [field: SerializeField, MinValue(0.01f), SuffixLabel("s (T)", true)]
    public float LightRampSeconds { get; private set; } = 2f;

    [field: SerializeField, Title("Zoom avant la caméra de danse")]
    public bool UseTransitionZoom { get; private set; } = true;

    [field: SerializeField, ShowIf(nameof(UseTransitionZoom)), MinValue(0f), MaxValue(80f), SuffixLabel("%", true)]
    [field: Tooltip("Intensité relative du zoom sur Descent Camera. 12 = environ 12 % de réduction du champ de vision au pic.")]
    public float ZoomAmountPercent { get; private set; } = 12f;

    [field: SerializeField, ShowIf(nameof(UseTransitionZoom)), MinValue(0.01f), SuffixLabel("s", true)]
    public float ZoomPulseSeconds { get; private set; } = 0.35f;

    [field: SerializeField, ShowIf(nameof(UseTransitionZoom))]
    [field: Tooltip("Temps normalisé de 0 à 1. Valeur positive = zoom ; négative = dézoom. Le cadrage initial est restauré après l'impulsion.")]
    public AnimationCurve ZoomPulseCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.6f, -0.35f), new Keyframe(1f, 0f));

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Durée de la transition Descent Camera -> Dance Camera, après le zoom. 0 = cut. Les autres transitions conservent les réglages du Brain.")]
    public float DanceCameraTransitionSeconds { get; private set; } = 0.2f;

    [field: SerializeField, Title("Danse"), Required]
    [field: Tooltip("Point d'arrivée partagé par tous. Chaque personnage s'oriente vers sa position au départ du trajet, sur l'axe horizontal.")]
    public Transform DanceTarget { get; private set; }

    [field: SerializeField, ListDrawerSettings(ShowIndexLabels = true)]
    public List<SpecialDanceActor> Actors { get; private set; } = new List<SpecialDanceActor>();

    [field: SerializeField, MinValue(0f), SuffixLabel("s (V)", true)]
    [field: Tooltip("Délai après la fin de la transition vers Dance Camera avant de déclencher StartDance sur tous les personnages.")]
    public float StartDanceDelaySeconds { get; private set; } = 1f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s (V)", true)]
    [field: Tooltip("Délai après la fin de la transition vers Dance Camera avant de déclencher StartDance sur tous les personnages.")]
    public float StartSpecialDanceDelaySeconds { get; private set; } = 1f;


    [field: SerializeField, MinValue(0.01f), SuffixLabel("s (Z)", true)]
    public float DanceSeconds { get; private set; } = 5f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s (W)", true)]
    public float BetweenActorsSeconds { get; private set; } = 1f;

    [field: SerializeField, Title("Suspension du gameplay")]
    [field: Tooltip("Scripts de déplacement concurrents à désactiver pendant toute la séquence (ex. AmbientWalker). Ne pas mettre les managers de cette cinématique.")]
    public MonoBehaviour[] ScriptsToSuspend { get; private set; } = Array.Empty<MonoBehaviour>();

    [field: SerializeField, Title("Événements")]
    public UnityEvent OnCinematicStarted { get; private set; } = new UnityEvent();

    [field: SerializeField]
    public UnityEvent OnCinematicCompleted { get; private set; } = new UnityEvent();

    [field: SerializeField]
    public UnityEvent OnCinematicEnded { get; private set; } = new UnityEvent();

    [ShowInInspector, ReadOnly] public bool IsPlaying { get; private set; }
    [ShowInInspector, ReadOnly] public bool CompletedNormally { get; private set; }

    private class ActorSnapshot
    {
        public SpecialDanceActor Actor;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool RootMotion;
        public AnimatorCullingMode CullingMode;
        public AnimatorStateInfo[] States;
        public bool HasReturned;
    }

    private readonly List<ActorSnapshot> _actors = new List<ActorSnapshot>();
    private readonly Dictionary<MonoBehaviour, bool> _scripts = new Dictionary<MonoBehaviour, bool>();
    private DialogManager _dialogs;
    private PigeonController _player;
    private MusicPlayer _backgroundMusic;
    private Coroutine _routine;
    private bool _ownsDialogue;
    private bool _returning;
    private bool _sequenceFinished;
    private bool _wasFrozen;
    private bool _musicStarted;
    private Vector3 _descentOrigin;
    private CinemachineCamera _zoomCamera;
    private float _originalFieldOfView;
    private float _originalOrthographicSize;
    private Light _animatedLight;
    private float _previousLightIntensity;
    private bool _previousLightEnabled;
    private float _lightElapsed;
    private bool _lightRamping;
    private bool _ownsLight;

    [Button("Jouer la cinématique"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled || IsPlaying)
            return;
        _dialogs = DialogManager != null ? DialogManager : global::DialogManager.instance;
        _backgroundMusic = BackgroundMusic != null ? BackgroundMusic
            : MusicPlayer.instance != null ? MusicPlayer.instance : FindAnyObjectByType<MusicPlayer>();
        if (!ValidateSetup())
            return;

        IsPlaying = true;
        CompletedNormally = _sequenceFinished = _returning = false;
        if (!CinematicManager.BeginCinematic(DialogueCamera, BeginSequence, HandleCinematicEnded))
        {
            IsPlaying = false;
            return;
        }

        _descentOrigin = DescendingObject.position;
        _player = Player != null ? Player : CinematicManager.PlayerControls != null
            ? CinematicManager.PlayerControls.GetComponent<PigeonController>() : FindAnyObjectByType<PigeonController>();
        if (_player != null)
        {
            _wasFrozen = _player.IsMovementFrozen;
            _player.SetMovementFrozen(true);
        }
        foreach (MonoBehaviour script in ScriptsToSuspend)
        {
            if (script == null || _scripts.ContainsKey(script)) continue;
            _scripts.Add(script, script.enabled);
            script.enabled = false;
        }
        foreach (SpecialDanceActor actor in Actors)
        {
            Animator animator = actor.Animator;
            var snapshot = new ActorSnapshot
            {
                Actor = actor,
                Position = actor.Root.position,
                Rotation = actor.Root.rotation,
                RootMotion = animator.applyRootMotion,
                CullingMode = animator.cullingMode,
                States = new AnimatorStateInfo[animator.layerCount]
            };
            for (int layer = 0; layer < snapshot.States.Length; layer++)
                snapshot.States[layer] = animator.GetCurrentAnimatorStateInfo(layer);
            _actors.Add(snapshot);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        OnCinematicStarted.Invoke();
    }

    private void BeginSequence()
    {
        if (!IsPlaying || _returning) return;
        if (!_dialogs.StartScriptedDialog(Dialogue))
        {
            CancelCinematic();
            return;
        }
        // A dialogue event can cancel the cinematic synchronously.
        if (!IsPlaying || _returning)
        {
            _dialogs.CancelDialog(Dialogue);
            return;
        }
        _ownsDialogue = true;
        if (_backgroundMusic == null || !_backgroundMusic.PlayCinematicMusic(MusicClip))
        {
            CancelCinematic();
            return;
        }
        _musicStarted = true;
        _routine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // One cumulative clock prevents frame-rounding drift between dialogue lines.
        int lineCount = Dialogue.lines.Count;
        float elapsed = 0f;
        int line = 0;
        while (elapsed < DialogueSeconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
            int nextLine = Mathf.Min(lineCount - 1, Mathf.FloorToInt(elapsed * lineCount / DialogueSeconds));
            if (nextLine != line)
            {
                line = nextLine;
                if (!_dialogs.ShowScriptedLine(Dialogue, line))
                {
                    CancelCinematic();
                    yield break;
                }
            }
        }
        CloseDialogue();
        if (!CinematicManager.SwitchCinematicCamera(DescentCamera))
        {
            CancelCinematic();
            yield break;
        }
        while (!CinematicManager.IsReady) yield return null;
        BeginLightRamp();
        yield return Move(DescendingObject, DescentTarget.position, DescentSeconds);
        if (UseTransitionZoom && ZoomAmountPercent > 0f)
            yield return PlayZoomPulse();
        if (!CinematicManager.SwitchCinematicCamera(DanceCamera, DanceCameraTransitionSeconds))
        {
            CancelCinematic();
            yield break;
        }
        while (!CinematicManager.IsReady) yield return null;

        yield return Wait(StartDanceDelaySeconds);
        foreach (ActorSnapshot snapshot in _actors)
            Trigger(snapshot.Actor.Animator, StartDanceHash);
        // Let the Animators consume StartDance before the first character receives Walk.
        yield return null;
        yield return Wait(StartSpecialDanceDelaySeconds);
        for (int i = 0; i < _actors.Count; i++)
        {
            ActorSnapshot snapshot = _actors[i];
            SpecialDanceActor actor = snapshot.Actor;
            Vector3 heading = DanceTarget.position - actor.Root.position;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0001f)
                actor.Root.rotation = Quaternion.LookRotation(heading, Vector3.up);
            Trigger(actor.Animator, WalkHash);
            yield return Move(actor.Root, DanceTarget.position, actor.WalkSeconds);
            Trigger(actor.Animator, DanceHash);
            yield return Wait(DanceSeconds);
            Trigger(actor.Animator, BackWalkHash);
            // Keep facing the dance point while walking backwards.
            yield return Move(actor.Root, snapshot.Position, actor.BackWalkSeconds);
            snapshot.HasReturned = true;
            RestoreActor(snapshot);
            Trigger(actor.Animator, BackHash);
            if (i + 1 < _actors.Count)
                yield return Wait(BetweenActorsSeconds);
        }
        _routine = null;
        _sequenceFinished = true;
        CancelCinematic();
    }

    private void BeginLightRamp()
    {
        if (DescentLight == null) return;
        _animatedLight = DescentLight;
        _previousLightIntensity = _animatedLight.intensity;
        _previousLightEnabled = _animatedLight.enabled;
        _lightElapsed = 0f;
        _lightRamping = _ownsLight = true;
        _animatedLight.intensity = 0f;
        _animatedLight.enabled = true;
    }

    [SerializeField] private float _lightDelay = 2f;

    private void UpdateLightRamp()
    {
        if (!_lightRamping) return;
        if (_animatedLight == null)
        {
            _lightRamping = false;
            return;
        }
        _lightElapsed += Time.deltaTime;
        if (_lightElapsed < _lightDelay)
            return;
        _animatedLight.intensity = Mathf.Lerp(0f, 100f, Mathf.Clamp01((_lightElapsed - _lightDelay) / LightRampSeconds));
        if (_lightElapsed >= (LightRampSeconds + _lightDelay)) _lightRamping = false;
    }

    private void RestoreLight()
    {
        if (!_ownsLight) return;
        if (_animatedLight != null)
        {
            _animatedLight.intensity = _previousLightIntensity;
            _animatedLight.enabled = _previousLightEnabled;
        }
        _lightRamping = _ownsLight = false;
        _animatedLight = null;
    }

    private IEnumerator PlayZoomPulse()
    {
        _zoomCamera = DescentCamera as CinemachineCamera;
        if (_zoomCamera == null) yield break;
        _originalFieldOfView = _zoomCamera.Lens.FieldOfView;
        _originalOrthographicSize = _zoomCamera.Lens.OrthographicSize;
        float elapsed = 0f;
        while (elapsed < ZoomPulseSeconds)
        {
            yield return null;
            if (_zoomCamera == null) yield break;
            elapsed += Time.deltaTime;
            float pulse = ZoomPulseCurve.Evaluate(Mathf.Clamp01(elapsed / ZoomPulseSeconds));
            float scale = 1f - ZoomAmountPercent * 0.01f * pulse;
            // Handle perspective and orthographic lenses without changing the camera rig.
            _zoomCamera.Lens.FieldOfView = Mathf.Clamp(_originalFieldOfView * scale, 1f, 179f);
            _zoomCamera.Lens.OrthographicSize = Mathf.Max(0.01f, _originalOrthographicSize * scale);
        }
        RestoreZoom();
        // Let the Brain evaluate the restored lens before starting the next blend.
        yield return null;
    }

    private void RestoreZoom()
    {
        if (_zoomCamera != null)
        {
            _zoomCamera.Lens.FieldOfView = _originalFieldOfView;
            _zoomCamera.Lens.OrthographicSize = _originalOrthographicSize;
        }
        _zoomCamera = null;
    }

    private static IEnumerator Move(Transform target, Vector3 destination, float seconds)
    {
        Vector3 origin = target.position;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            target.position = Vector3.Lerp(origin, destination, Mathf.Clamp01(elapsed / seconds));
        }
        if (target != null) target.position = destination;
    }

    private static IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    [Button("Annuler"), EnableIf(nameof(IsPlaying))]
    public void CancelCinematic()
    {
        if (!IsPlaying || _returning) return;
        _returning = true;
        StopSequence();
        CloseDialogue();
        RestoreActors();
        if (!_sequenceFinished && DescendingObject != null)
            DescendingObject.position = _descentOrigin;
        if (CinematicManager != null)
            CinematicManager.CancelCinematic();
        else
            HandleCinematicEnded();
    }

    private void HandleCinematicEnded()
    {
        if (!IsPlaying) return;
        StopSequence();
        CloseDialogue();
        RestoreActors();
        if (!_sequenceFinished && DescendingObject != null)
            DescendingObject.position = _descentOrigin;
        if (_player != null) _player.SetMovementFrozen(_wasFrozen);
        if (_musicStarted && _backgroundMusic != null) _backgroundMusic.RestoreMusicAfterCinematic();
        foreach (var pair in _scripts)
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        _scripts.Clear();
        _actors.Clear();
        _player = null;
        _backgroundMusic = null;
        _musicStarted = false;
        IsPlaying = _returning = false;
        CompletedNormally = _sequenceFinished;
        if (CompletedNormally) OnCinematicCompleted.Invoke();
        OnCinematicEnded.Invoke();
    }

    private void CloseDialogue()
    {
        if (!_ownsDialogue) return;
        _ownsDialogue = false;
        if (_dialogs != null) _dialogs.CancelDialog(Dialogue);
    }

    private void StopSequence()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        RestoreZoom();
        if (!_sequenceFinished) RestoreLight();
        else _ownsLight = false;
    }

    private static void Trigger(Animator animator, int hash)
    {
        animator.ResetTrigger(StartDanceHash);
        animator.ResetTrigger(WalkHash);
        animator.ResetTrigger(DanceHash);
        animator.ResetTrigger(BackWalkHash);
        animator.ResetTrigger(BackHash);
        animator.SetTrigger(hash);
    }

    private static void RestoreActor(ActorSnapshot snapshot)
    {
        if (snapshot.Actor.Root != null)
            snapshot.Actor.Root.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
        Animator animator = snapshot.Actor.Animator;
        if (animator == null) return;
        // Once home, let Back drive the final animation, including during cleanup.
        // Interrupted actors that never arrived still recover their original state.
        if (!snapshot.HasReturned)
        {
            animator.ResetTrigger(StartDanceHash);
            animator.ResetTrigger(WalkHash);
            animator.ResetTrigger(DanceHash);
            animator.ResetTrigger(BackWalkHash);
            animator.ResetTrigger(BackHash);
            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                for (int layer = 0; layer < snapshot.States.Length; layer++)
                    if (animator.HasState(layer, snapshot.States[layer].fullPathHash))
                        animator.Play(snapshot.States[layer].fullPathHash, layer, snapshot.States[layer].normalizedTime);
        }
        animator.applyRootMotion = snapshot.RootMotion;
        animator.cullingMode = snapshot.CullingMode;
    }

    private void RestoreActors()
    {
        foreach (ActorSnapshot snapshot in _actors) RestoreActor(snapshot);
    }

    private void Update()
    {
        // Independent of Y and camera shots; a long T can finish after gameplay resumes.
        UpdateLightRamp();
        if (!IsPlaying || _returning) return;
        if (CinematicManager == null || !CinematicManager.IsPlaying || _dialogs == null
            || !_dialogs.isActiveAndEnabled || DescendingObject == null || DanceTarget == null
            || _backgroundMusic == null || !_backgroundMusic.isActiveAndEnabled
            || (_musicStarted && !_backgroundMusic.IsPlayingCinematicMusic)
            || (_ownsDialogue && (!_dialogs.IsPlaying || _dialogs.CurrentSequence != Dialogue)))
        {
            CancelCinematic();
            return;
        }
        foreach (ActorSnapshot actor in _actors)
            if (actor.Actor.Root == null || actor.Actor.Animator == null || !actor.Actor.Animator.isActiveAndEnabled)
            {
                CancelCinematic();
                return;
            }
    }

    private void OnDisable() => CancelCinematic();

    private bool ValidateSetup()
    {
        if (CinematicManager == null || _dialogs == null || !_dialogs.isActiveAndEnabled
            || Dialogue == null || Dialogue.lines == null || Dialogue.lines.Count == 0
            || Dialogue.lines.Exists(line => line == null)
            || DescendingObject == null || DescentTarget == null || DescentTarget.IsChildOf(DescendingObject)
            || DescendingObject == transform || transform.IsChildOf(DescendingObject)
            || DanceTarget == null || _backgroundMusic == null || !_backgroundMusic.CanPlayCinematicMusic
            || MusicClip == null || Actors == null || Actors.Count == 0
            || !ValidSeconds(DialogueSeconds) || !ValidSeconds(DescentSeconds) || !ValidSeconds(DanceSeconds)
            || !ValidDelay(StartDanceDelaySeconds) || !ValidDelay(BetweenActorsSeconds)
            || !ValidDelay(DanceCameraTransitionSeconds)
            || !ValidCamera(DialogueCamera) || !ValidCamera(DescentCamera) || !ValidCamera(DanceCamera))
            return ConfigurationError("assigner les managers, trois caméras Cinemachine actives, un dialogue non vide, un MusicPlayer disponible avec ses deux sources actives, Music Clip, les points et les personnages. X/Y/Z et les trajets doivent être > 0, V/W et la transition vers Dance Camera >= 0.");
        if (CinematicManager.IsPlaying || _dialogs.IsPlaying) return false;
        if (DescentLight != null && (!DescentLight.gameObject.activeInHierarchy || !ValidSeconds(LightRampSeconds)))
            return ConfigurationError("la lumière de descente doit être sur un GameObject actif et Light Ramp Seconds (T) doit être > 0.");
        if (UseTransitionZoom && (!ValidSeconds(ZoomPulseSeconds) || !ValidDelay(ZoomAmountPercent)
            || ZoomAmountPercent > 80f || ZoomPulseCurve == null || ZoomPulseCurve.length < 2
            || !(DescentCamera is CinemachineCamera)))
            return ConfigurationError("pour le zoom, utiliser une CinemachineCamera comme Descent Camera, une durée > 0, une intensité de 0 à 80 % et une courbe avec au moins deux clés.");
        var roots = new HashSet<Transform>();
        var animators = new HashSet<Animator>();
        foreach (SpecialDanceActor actor in Actors)
        {
            if (actor == null || actor.Root == null || !actor.Root.gameObject.activeInHierarchy
                || !ValidSeconds(actor.WalkSeconds) || !ValidSeconds(actor.BackWalkSeconds)
                || !HasTrigger(actor.Animator, StartDanceHash) || !HasTrigger(actor.Animator, WalkHash)
                || !HasTrigger(actor.Animator, DanceHash)
                || !HasTrigger(actor.Animator, BackWalkHash) || !HasTrigger(actor.Animator, BackHash)
                || !roots.Add(actor.Root) || !animators.Add(actor.Animator)
                || !actor.Animator.transform.IsChildOf(actor.Root) || DanceTarget.IsChildOf(actor.Root)
                || DescentTarget.IsChildOf(actor.Root)
                || actor.Root.IsChildOf(DescendingObject) || DescendingObject.IsChildOf(actor.Root))
                return ConfigurationError("chaque personnage doit avoir une racine distincte contenant son Animator actif, les triggers StartDance / Walk / SpecialDanse / BackWalk / Back et des points indépendants des objets déplacés.");
            foreach (Transform root in roots)
                if (root != actor.Root && (root.IsChildOf(actor.Root) || actor.Root.IsChildOf(root)))
                    return ConfigurationError("les racines des personnages ne doivent pas être imbriquées.");
        }
        foreach (MonoBehaviour script in ScriptsToSuspend)
            if (script != null && (script == this || script == CinematicManager || script == _dialogs
                || script is MusicPlayer || script is PigeonController || script is PlayerControls))
                return ConfigurationError("Scripts To Suspend doit contenir seulement les scripts concurrents des personnages/décors, pas les managers, la musique ou le pigeon.");
        return true;
    }

    private bool ValidCamera(CinemachineVirtualCameraBase camera) => camera != null && camera.isActiveAndEnabled
        && CinematicManager.Brain != null && CinematicManager.Brain.IsValidChannel(camera)
        && camera != CinematicManager.GameplayCamera;

    private static bool ValidSeconds(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool ValidDelay(float value) => value == 0f || ValidSeconds(value);

    private static bool HasTrigger(Animator animator, int hash)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    private bool ConfigurationError(string message)
    {
        Debug.LogWarning("SpecialDanceCinematic : " + message, this);
        return false;
    }
}
