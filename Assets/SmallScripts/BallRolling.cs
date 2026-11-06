using System.Collections.Generic;
using PurrNet.Prediction;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ballon de plage partagé (PredictedRigidbody, l'hôte fait référence), roulé par les pigeons posés dessus d'après
/// leur état prédit. Son affichage est lissé entre deux ticks sur le pivot <see cref="_view"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(MovingSupport))]
public class BallRolling : MonoBehaviour
{
    [SerializeField] float _rotationMax = 1f;
    [SerializeField] float _powerTorque = 1f;
    [SerializeField, Tooltip("Pivot vide, « graphics » du PredictedTransform : le maillage du ballon y est rangé au lancement.")]
    Transform _view;

    [ReadOnly] public Vector3 rotationAxis = new Vector3();
    [ReadOnly] public Vector3 angularVelocity = new Vector3();

    MovingSupport _movingSupport;
    Rigidbody _rgbd;
    SphereCollider _collider;
    PredictionManager _prediction;
    readonly List<PigeonController> _riders = new List<PigeonController>();

    /// <summary>Pigeons posés sur le ballon au dernier pas de physique (lu par CheckForGoal et les scénarios).</summary>
    public IReadOnlyList<PigeonController> Riders => _riders;

    void Awake()
    {
        _movingSupport = GetComponent<MovingSupport>();
        _rgbd = GetComponent<Rigidbody>();
        _collider = GetComponent<SphereCollider>();

        if (_view == null)
            return;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != _view)
                child.SetParent(_view, true);
        }
    }

    void OnEnable()
    {
        PredictionManager.OnInstanceAdded += HandleInstanceAdded;
        if (PredictionManager.TryGetInstance(gameObject.scene.handle, out PredictionManager manager))
            Bind(manager);
    }

    void OnDisable()
    {
        PredictionManager.OnInstanceAdded -= HandleInstanceAdded;
        Bind(null);
    }

    void HandleInstanceAdded(SceneHandle sceneHandle, PredictionManager manager)
    {
        if (sceneHandle == gameObject.scene.handle)
            Bind(manager);
    }

    void Bind(PredictionManager manager)
    {
        if (_prediction == manager)
            return;
        if (_prediction != null)
            _prediction.onBeforePhysicsPass -= BeforePhysicsPass;
        _prediction = manager;
        if (_prediction != null)
            _prediction.onBeforePhysicsPass += BeforePhysicsPass;
    }

    void BeforePhysicsPass()
    {
        if (_prediction == null || _prediction.isReplaying || _rgbd.isKinematic)
            return;

        _riders.Clear();
        Vector3 walkVelocity = Vector3.zero;
        IReadOnlyList<PigeonController> pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState state = ref pigeons[i].SimState;
            if (state.SupportId != 0 && state.SupportId == _movingSupport.Id)
            {
                _riders.Add(pigeons[i]);
                walkVelocity += Vector3.ProjectOnPlane(state.LastVelocity, Vector3.up);
            }
        }
        if (_riders.Count == 0)
            return;
        walkVelocity /= _riders.Count;

        float dt = _prediction.tickDelta;
        float radius = _collider.radius;
        if (dt <= 0f || radius <= 0.0001f)
            return;

        float maxAngularSpeed = Mathf.Max(0f, _rotationMax);
        _rgbd.maxAngularVelocity = maxAngularSpeed;

        angularVelocity = _rgbd.angularVelocity;
        Vector3 rollingVelocity = Vector3.ProjectOnPlane(angularVelocity, Vector3.up);
        _rgbd.angularVelocity = rollingVelocity;

        rotationAxis = Vector3.Cross(walkVelocity, Vector3.up);
        Vector3 targetAngularVelocity = Vector3.ClampMagnitude(rotationAxis / radius, maxAngularSpeed);

        Vector3 angularAcceleration = (targetAngularVelocity - rollingVelocity) / dt;
        angularAcceleration = Vector3.ClampMagnitude(angularAcceleration, _powerTorque);

        _rgbd.AddTorque(angularAcceleration, ForceMode.Acceleration);
    }
}
