using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>A standalone flight loop. The delivery prefab is only used as a visual.</summary>
[DisallowMultipleComponent]
public class RandomFlightTargetTest : MonoBehaviour
{
    [field: SerializeField, BoxGroup("References"), Required]
    public Transform Player { get; private set; }

    [field: SerializeField, BoxGroup("References"), Required, AssetsOnly]
    public GameObject RingPrefab { get; private set; }

    [field: SerializeField, BoxGroup("Placement")]
    [field: Tooltip("Volume en coordonnees monde. Le rayon part du haut de cette boite.")]
    public Bounds SpawnBounds { get; private set; } = new Bounds(new Vector3(0f, 50f, 0f), new Vector3(300f, 120f, 300f));

    [field: SerializeField, BoxGroup("Placement")]
    public LayerMask SurfaceLayers { get; private set; } = Physics.DefaultRaycastLayers;

    [field: SerializeField, BoxGroup("Placement")]
    public LayerMask ObstacleLayers { get; private set; } = Physics.DefaultRaycastLayers;

    [field: SerializeField, BoxGroup("Placement"), MinValue(0.1f)]
    public float HeightAboveSurface { get; private set; } = 4f;

    [field: SerializeField, BoxGroup("Placement"), MinValue(0.1f)]
    [field: Tooltip("Volume libre autour du centre. Garder cette valeur inferieure a la hauteur.")]
    public float ClearanceRadius { get; private set; } = 3f;

    [field: SerializeField, BoxGroup("Placement"), MinValue(0f)]
    public float MinimumDistance { get; private set; } = 40f;

    [field: SerializeField, BoxGroup("Placement"), MinValue(0.1f)]
    public float MaximumDistance { get; private set; } = 250f;

    [field: SerializeField, BoxGroup("Placement"), Range(0f, 60f)]
    public float MaximumSurfaceSlope { get; private set; } = 25f;

    [field: SerializeField, BoxGroup("Placement"), MinValue(1)]
    public int PlacementAttempts { get; private set; } = 160;

    [field: SerializeField, BoxGroup("Validation"), MinValue(0.1f)]
    public float VisitRadius { get; private set; } = 3f;

    [field: SerializeField, BoxGroup("Affichage")]
    public bool ShowHUD { get; private set; } = true;

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public Transform CurrentTarget => _ring != null && _ring.activeSelf ? _ring.transform : null;

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public int VisitedCount { get; private set; }

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public int TargetVersion { get; private set; }

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public string Status { get; private set; } = "Pret";

    public float DistanceToTarget => Player != null && CurrentTarget != null
        ? Vector3.Distance(Player.position, CurrentTarget.position) : 0f;

    private readonly Collider[] _overlaps = new Collider[64];
    private GameObject _ring;
    private Vector3 _previousPlayerPosition;
    private bool _hasPreviousPosition;
    private float _retryAt;
    private GUIStyle _hudStyle;

    private void OnEnable()
    {
        _hasPreviousPosition = false;
        _retryAt = 0f;
    }

    private void LateUpdate()
    {
        if (Player == null || Time.timeScale <= 0f)
            return;

        Vector3 position = Player.position;
        if (CurrentTarget == null)
        {
            if (Time.unscaledTime >= _retryAt)
                NextTarget();
        }
        else if (SegmentVisitsTarget(_hasPreviousPosition ? _previousPlayerPosition : position,
                     position, CurrentTarget.position, VisitRadius))
        {
            VisitedCount++;
            NextTarget();
        }

        // The old movement segment is never tested against the newly spawned target.
        _previousPlayerPosition = position;
        _hasPreviousPosition = true;
    }

    [Button("Nouvel objectif"), DisableInEditorMode]
    public void NextTarget()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (_ring != null)
            _ring.SetActive(false);

        _retryAt = Time.unscaledTime + 1f;
        if (!HasValidSettings())
        {
            SetFailure("Verifier le joueur, le prefab, la zone, les distances et les layers.");
            return;
        }

        if (!TryFindTargetPosition(out Vector3 position))
        {
            SetFailure("Aucun emplacement libre : ajuster la zone, les layers ou les distances. Nouvel essai dans 1 s.");
            return;
        }

        if (_ring == null)
        {
            _ring = Instantiate(RingPrefab, position, RingPrefab.transform.rotation);
            _ring.name = "Flight Test - Current Target";
            // Independent from delivery interactions, including with a custom visual prefab.
            foreach (Collider collider in _ring.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
        else
            _ring.transform.position = position;

        _ring.SetActive(true);
        TargetVersion++;
        Status = "Rejoindre l'anneau";
        _previousPlayerPosition = Player.position;
        _hasPreviousPosition = true;
    }

    [Button("Recommencer"), DisableInEditorMode]
    public void RestartTest()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;
        VisitedCount = 0;
        NextTarget();
    }

    public bool HasValidSettings()
    {
        return Player != null && RingPrefab != null && SurfaceLayers.value != 0 && ObstacleLayers.value != 0
            && IsFinite(SpawnBounds.center) && IsFinite(SpawnBounds.size)
            && SpawnBounds.size.x > 0f && SpawnBounds.size.y > 0f && SpawnBounds.size.z > 0f
            && IsPositive(HeightAboveSurface) && IsPositive(ClearanceRadius) && IsPositive(VisitRadius)
            && !float.IsNaN(MinimumDistance) && MinimumDistance >= 0f
            && IsPositive(MaximumDistance) && MaximumDistance > Mathf.Max(MinimumDistance, VisitRadius)
            && !float.IsNaN(MaximumSurfaceSlope) && MaximumSurfaceSlope >= 0f && MaximumSurfaceSlope <= 60f
            && PlacementAttempts > 0;
    }

    /// <summary>Reject occupied points; never fall back to an unchecked destination.</summary>
    public bool TryFindTargetPosition(out Vector3 position)
    {
        position = default;
        if (!HasValidSettings())
            return false;

        Vector3 min = SpawnBounds.min;
        Vector3 max = SpawnBounds.max;
        float minimum = Mathf.Max(MinimumDistance, VisitRadius + 0.1f);
        float minSquared = minimum * minimum;
        float maxSquared = MaximumDistance * MaximumDistance;
        float minimumNormalY = Mathf.Cos(MaximumSurfaceSlope * Mathf.Deg2Rad);

        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            Vector3 origin = new Vector3(Random.Range(min.x, max.x), max.y, Random.Range(min.z, max.z));
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, SpawnBounds.size.y,
                SurfaceLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit surface = default;
            foreach (RaycastHit hit in hits)
            {
                if (IsIgnored(hit.collider) || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                surface = hit;
            }

            if (float.IsPositiveInfinity(nearest) || surface.normal.y < minimumNormalY)
                continue;

            Vector3 candidate = surface.point + Vector3.up * HeightAboveSurface;
            float distanceSquared = (candidate - Player.position).sqrMagnitude;
            if (!SpawnBounds.Contains(candidate) || distanceSquared < minSquared || distanceSquared > maxSquared)
                continue;

            int count = Physics.OverlapSphereNonAlloc(candidate, ClearanceRadius, _overlaps,
                ObstacleLayers, QueryTriggerInteraction.Ignore);
            bool blocked = count == _overlaps.Length;
            for (int i = 0; i < count && !blocked; i++)
                blocked = !IsIgnored(_overlaps[i]);
            if (blocked)
                continue;

            position = candidate;
            return true;
        }
        return false;
    }

    private bool IsIgnored(Collider collider)
    {
        return collider.transform.IsChildOf(Player)
            || (_ring != null && collider.transform.IsChildOf(_ring.transform));
    }

    // Swept sphere test: a fast pigeon can cross the whole objective between two frames.
    public static bool SegmentVisitsTarget(Vector3 start, Vector3 end, Vector3 target, float radius)
    {
        Vector3 segment = end - start;
        float t = segment.sqrMagnitude > 0.000001f
            ? Mathf.Clamp01(Vector3.Dot(target - start, segment) / segment.sqrMagnitude) : 0f;
        return (start + segment * t - target).sqrMagnitude <= radius * radius;
    }

    private void SetFailure(string message)
    {
        if (Status != message)
            Debug.LogWarning("Flight Target Test : " + message, this);
        Status = message;
    }

    private void OnDisable()
    {
        if (_ring != null)
            _ring.SetActive(false);
        _hasPreviousPosition = false;
    }

    private void OnDestroy()
    {
        if (_ring != null)
            Destroy(_ring);
    }

    private void OnGUI()
    {
        if (!ShowHUD)
            return;
        if (_hudStyle == null)
            _hudStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        string objective = CurrentTarget != null ? $"Objectif : {DistanceToTarget:0} m" : Status;
        GUI.Box(new Rect(20f, Mathf.Max(10f, Screen.height - 95f), Mathf.Min(650f, Screen.width - 40f), 75f),
            $"  TEST DE VOL - Points visites : {VisitedCount}\n  {objective}", _hudStyle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 1f);
        Gizmos.DrawWireCube(SpawnBounds.center, SpawnBounds.size);
        if (CurrentTarget == null)
            return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(CurrentTarget.position, VisitRadius);
        if (Player != null)
            Gizmos.DrawLine(Player.position, CurrentTarget.position);
    }

    private static bool IsPositive(float value) => value > 0f && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}
