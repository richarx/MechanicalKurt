using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dirigeable sur un cercle, réglé sur l'horloge partagée : même endroit pour tous les joueurs. Ses colliders suivent
/// les ticks réseau, pour que les pigeons posés dessus restent cohérents ; son affichage est lissé entre deux ticks.
/// </summary>
public class MoveZeppelin : MonoBehaviour
{
    [SerializeField] private float distanceFromCenter;
    [SerializeField] private float speed;

    private Transform _view;

    public Transform View => _view;

    private void Awake() => BuildView();

    private void OnEnable()
    {
        CityClock.Driven += PlaceBody;
        CityClock.ViewChanged += PlaceView;
    }

    private void OnDisable()
    {
        CityClock.Driven -= PlaceBody;
        CityClock.ViewChanged -= PlaceView;
    }

    private void Update()
    {
        if (CityClock.IsDriven)
            return;
        PlaceBody();
        _view.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private void PlaceBody()
    {
        GetLocalPose(CityClock.Now, out Vector3 position, out Quaternion rotation);
        transform.SetLocalPositionAndRotation(position, rotation);
    }

    private void PlaceView()
    {
        GetLocalPose(CityClock.ViewNow, out Vector3 position, out Quaternion rotation);
        Transform parent = transform.parent;
        if (parent != null)
            _view.SetPositionAndRotation(parent.TransformPoint(position), parent.rotation * rotation);
        else
            _view.SetPositionAndRotation(position, rotation);
    }

    private void GetLocalPose(double time, out Vector3 position, out Quaternion rotation)
    {
        float angle = (float)(time * speed);
        float xPosition = Mathf.Sin(angle) * distanceFromCenter;
        float zPosition = Mathf.Cos(angle) * distanceFromCenter;
        position = new Vector3(xPosition, 0.0f, zPosition);

        Vector2 direction = new Vector2(xPosition, zPosition).AddAngleToDirection(-90.0f);
        rotation = Quaternion.LookRotation(new Vector3(direction.x, 0.0f, direction.y).normalized);
    }

    /// <summary>
    /// Le maillage et son collider sont sur le même objet : l'affichage passe sur une copie du rendu, placée sous un
    /// pivot libre de bouger sans déplacer les colliders.
    /// </summary>
    private void BuildView()
    {
        _view = new GameObject("View").transform;
        _view.SetParent(transform, false);

        MovingSupport support = GetComponentInChildren<MovingSupport>();
        foreach (MeshRenderer source in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!source.TryGetComponent(out MeshFilter filter))
                continue;

            Transform copy = CopyPose(source.transform);
            copy.gameObject.layer = source.gameObject.layer;
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = copy.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage;
            renderer.reflectionProbeUsage = source.reflectionProbeUsage;
            renderer.renderingLayerMask = source.renderingLayerMask;
            source.enabled = false;

            if (support != null && support.transform == source.transform)
                support.ViewTransform = copy;
        }

        if (support != null && support.ViewTransform == support.transform)
            support.ViewTransform = CopyPose(support.transform);
    }

    private Transform CopyPose(Transform source)
    {
        Transform copy = new GameObject(source.name).transform;
        copy.SetParent(_view, false);
        copy.SetPositionAndRotation(source.position, source.rotation);
        Vector3 parent = transform.lossyScale;
        Vector3 scale = source.lossyScale;
        copy.localScale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z);
        return copy;
    }
}
