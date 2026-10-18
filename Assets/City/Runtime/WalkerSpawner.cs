using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class WalkerSpawner : MonoBehaviour
{
    [field: SerializeField, BoxGroup("Pietons"), Required, AssetsOnly]
    [field: Tooltip("Prefabs avec AmbientWalker sur leur racine. Leur Path peut rester vide.")]
    public AmbientWalker[] WalkerPrefabs { get; private set; } = Array.Empty<AmbientWalker>();

    [field: SerializeField, BoxGroup("Parcours")]
    [field: Tooltip("Laisser vide pour utiliser tous les WalkerPath actifs de la scene.")]
    public WalkerPath[] Paths { get; private set; } = Array.Empty<WalkerPath>();

    [field: SerializeField, BoxGroup("Repartition"), MinValue(0)]
    [field: Tooltip("Nombre de pietons repartis regulierement sur chaque boucle.")]
    public int WalkersPerPath { get; private set; } = 8;

    [field: SerializeField, BoxGroup("Generation")]
    public bool SpawnOnStart { get; private set; } = true;

    [ShowInInspector, ReadOnly, BoxGroup("Generation")]
    public int SpawnedCount { get; private set; }

    private GameObject _spawnRoot;

    private void Start()
    {
        if (SpawnOnStart)
            SpawnWalkers();
    }

    [Button("Generer les pietons"), DisableInEditorMode]
    public void SpawnWalkers()
    {
        if (!Application.isPlaying)
            return;

        AmbientWalker[] prefabs = Array.FindAll(WalkerPrefabs ?? Array.Empty<AmbientWalker>(),
            prefab => prefab != null && prefab.transform.parent == null);
        if (prefabs.Length == 0)
        {
            Debug.LogWarning("WalkerSpawner : assigner au moins un prefab avec AmbientWalker sur sa racine.", this);
            return;
        }

        WalkerPath[] paths = Paths == null || Paths.Length == 0
            ? FindObjectsByType<WalkerPath>() : Paths;
        ClearSpawnedWalkers();
        if (WalkersPerPath <= 0)
            return;

        // Assign the path and next waypoint before any walker receives OnEnable.
        _spawnRoot = new GameObject("Spawned Walkers");
        _spawnRoot.SetActive(false);
        _spawnRoot.transform.SetParent(transform, false);

        var visited = new HashSet<WalkerPath>();
        foreach (WalkerPath path in paths)
        {
            if (path == null || !visited.Add(path))
                continue;
            if (path.PointCount < 2 || !path.isActiveAndEnabled)
            {
                Debug.LogWarning("WalkerSpawner : parcours ignore (inactif ou moins de deux points).", path);
                continue;
            }

            Vector3[] points = new Vector3[path.PointCount];
            bool valid = true;
            for (int i = 0; i < points.Length; i++)
            {
                if (path.GetPoint(i) == null)
                {
                    valid = false;
                    break;
                }
                points[i] = path.GetPosition(i);
            }
            if (!valid)
            {
                Debug.LogWarning("WalkerSpawner : parcours ignore car il contient un point manquant.", path);
                continue;
            }

            var route = new TrafficRoute(points);
            if (!route.IsValid)
            {
                Debug.LogWarning("WalkerSpawner : parcours ignore car sa longueur est nulle.", path);
                continue;
            }

            float spacing = route.Length / WalkersPerPath;
            for (int i = 0; i < WalkersPerPath; i++)
            {
                float distance = i * spacing;
                Vector3 position = route.GetPosition(distance);
                int targetIndex = GetNextPointIndex(points, distance);
                Vector3 heading = points[targetIndex] - position;
                heading.y = 0f;
                Quaternion rotation = heading.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(heading, Vector3.up) : Quaternion.identity;
                AmbientWalker prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
                AmbientWalker walker = Instantiate(prefab, position, rotation, _spawnRoot.transform);
                walker.Initialize(path, targetIndex);
                walker.enabled = true;
                walker.gameObject.SetActive(true);
                SpawnedCount++;
            }
        }

        _spawnRoot.SetActive(true);
        if (SpawnedCount == 0)
            Debug.LogWarning("WalkerSpawner : aucun WalkerPath valide trouve. Assigner les parcours ou activer ceux de la scene.", this);
    }

    [Button("Supprimer les pietons generes"), DisableInEditorMode]
    public void ClearSpawnedWalkers()
    {
        if (!Application.isPlaying)
            return;
        if (_spawnRoot != null)
        {
            _spawnRoot.SetActive(false);
            Destroy(_spawnRoot);
            _spawnRoot = null;
        }
        SpawnedCount = 0;
    }

    private static int GetNextPointIndex(Vector3[] points, float distance)
    {
        float segmentEnd = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            int next = (i + 1) % points.Length;
            segmentEnd += Vector3.Distance(points[i], points[next]);
            if (distance < segmentEnd)
                return next;
        }
        return 0;
    }

    private void OnValidate()
    {
        WalkersPerPath = Mathf.Max(0, WalkersPerPath);
    }
}
