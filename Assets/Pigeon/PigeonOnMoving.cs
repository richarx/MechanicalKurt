using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PigeonOnMoving : MonoBehaviour
{
    private CharacterController _controller;
    private PigeonController _pigeon;
    private Collider _supportCollider;
    private MovingSupport _support;
    private Vector3 _localAnchor;
    private Vector3 _worldAnchor;
    private Vector3 _lastSupportVelocity;
    private float _supportNormalY;
    private bool _collectSupportContacts;
    private bool _hasSupportAnchor;

    public bool HasSupport => isActiveAndEnabled && _controller.enabled &&
                              _support != null && _support.isActiveAndEnabled &&
                              _supportCollider != null && _supportCollider.enabled &&
                              _supportCollider.gameObject.activeInHierarchy;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (!TryGetComponent(out _pigeon))
        {
            Debug.LogError("PigeonOnMoving nécessite un PigeonController sur le même objet.", this);
            enabled = false;
        }
    }

    // Appelé par PigeonController après le déplacement des objets de la scène.
    public void Move(Vector3 displacement)
    {
        if (!_controller.enabled)
        {
            ClearSupport();
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        bool hadSupport = _hasSupportAnchor;
        MovingSupport oldSupport = _support;
        Vector3 departureVelocity = _lastSupportVelocity;

        Physics.SyncTransforms();

        if (HasSupport)
        {
            Vector3 supportDisplacement = _supportCollider.transform.TransformPoint(_localAnchor) - _worldAnchor;
            departureVelocity = supportDisplacement / deltaTime;
            if (displacement.y <= 0f && supportDisplacement.sqrMagnitude > 0f)
                _controller.Move(supportDisplacement);

            if (!_controller.enabled)
            {
                ClearSupport();
                return;
            }
        }


        ClearSupport();
        _collectSupportContacts = displacement.y <= 0f;
        _controller.Move(displacement);
        _collectSupportContacts = false;

        if (HasSupport)
        {
            _worldAnchor = transform.position;
            _localAnchor = _supportCollider.transform.InverseTransformPoint(_worldAnchor);
            _hasSupportAnchor = true;
            _lastSupportVelocity = oldSupport == _support ? departureVelocity : Vector3.zero;
        }
        else if (hadSupport && _controller.enabled)
        {
            _pigeon.AddImpulse(departureVelocity);
        }
    }

    private bool TryGetSupport(ControllerColliderHit hit, out MovingSupport support)
    {
        support = null;
        float minimumNormalY = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
        if (hit.normal.y <= 0.01f || hit.normal.y < minimumNormalY ||
            hit.point.y > transform.TransformPoint(_controller.center).y)
            return false;

        support = hit.collider.GetComponentInParent<MovingSupport>();
        return support != null && support.isActiveAndEnabled;
    }

    public bool TryRegisterSupportContact(ControllerColliderHit hit)
    {
        if (!TryGetSupport(hit, out MovingSupport support))
            return false;

        if (_collectSupportContacts && hit.normal.y >= _supportNormalY)
        {
            _support = support;
            _supportCollider = hit.collider;
            _supportNormalY = hit.normal.y;
        }

        return true;
    }

    public void ClearSupport()
    {
        _support = null;
        _supportCollider = null;
        _supportNormalY = 0f;
        _collectSupportContacts = false;
        _hasSupportAnchor = false;
        _lastSupportVelocity = Vector3.zero;
    }

    private void OnDisable()
    {
        ClearSupport();
    }
}
