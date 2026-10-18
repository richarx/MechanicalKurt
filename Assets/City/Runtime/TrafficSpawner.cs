using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class TrafficSpawner : MonoBehaviour
{
    [field: SerializeField, BoxGroup("Circuit"), Required]
    public TrafficPath Path { get; private set; }

    [field: SerializeField, BoxGroup("Voitures"), Required, AssetsOnly]
    [field: Tooltip("Prefabs avec AmbientCar sur leur objet racine. Le chemin est assigne par le spawner.")]
    public AmbientCar[] CarPrefabs { get; private set; } = Array.Empty<AmbientCar>();

    [SerializeField, BoxGroup("Voitures")]
    [Tooltip("Vitesse Min/Max"), MinMaxSlider(0.01f, 10f)]
    private Vector2 _minMaxVelocity = new Vector2(6f,6f);

    [field: SerializeField, BoxGroup("Voitures"), MinValue(0)]
    public int CarCount { get; private set; } = 8;

    [field: SerializeField, BoxGroup("Repartition"), MinValue(0f)]
    [field: Tooltip("Variation des espacements en unites monde. Zero donne une repartition reguliere de l'espace libre.")]
    public float SpacingStandardDeviation { get; private set; } = 3f;

    [field: SerializeField, BoxGroup("Repartition"), MinValue(0.1f)]
    [field: Tooltip("Marge ajoutee aux longueurs et espaces de securite des voitures lors du placement.")]
    public float ExtraSpawnGap { get; private set; } = 1f;

    [field: SerializeField, BoxGroup("Generation")]
    public bool SpawnOnStart { get; private set; } = true;

    [ShowInInspector, ReadOnly, BoxGroup("Generation")]
    public int SpawnedCount { get; private set; }

    private GameObject _spawnRoot;

    private void Start()
    {
        if (SpawnOnStart)
            SpawnCars();
    }

    [Button("Generer les voitures"), DisableInEditorMode]
    public void SpawnCars()
    {
        if (!Application.isPlaying)
            return;

        if (Path == null || !Path.IsValid || transform.IsChildOf(Path.transform))
        {
            Debug.LogWarning("TrafficSpawner : assigner un circuit valide et placer le spawner hors de sa hierarchie.", this);
            return;
        }

        AmbientCar[] validPrefabs = Array.FindAll(CarPrefabs ?? Array.Empty<AmbientCar>(),
            prefab => prefab != null && prefab.transform.parent == null);
        if (validPrefabs.Length == 0)
        {
            Debug.LogWarning("TrafficSpawner : assigner au moins un prefab avec AmbientCar sur sa racine.", this);
            return;
        }

        ClearSpawnedCars();
        if (CarCount == 0)
            return;

        var random = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
        AmbientCar[] selected = new AmbientCar[CarCount];
        float[] lengths = new float[CarCount];
        float[] minimumGaps = new float[CarCount];
        for (int i = 0; i < selected.Length; i++)
        {
            selected[i] = validPrefabs[random.Next(validPrefabs.Length)];
            lengths[i] = selected[i].VehicleLength;
            minimumGaps[i] = selected[i].MinimumGap;
        }

        float[] distances;
        while (!TrafficSpawnLayout.TryCreateDistances(Path.Length, lengths, minimumGaps,
            SpacingStandardDeviation, ExtraSpawnGap, random, out distances))
        {
            int count = selected.Length - 1;
            if (count == 0)
            {
                Debug.LogWarning("TrafficSpawner : circuit trop court pour les voitures choisies et leurs marges.", this);
                return;
            }
            Array.Resize(ref selected, count);
            Array.Resize(ref lengths, count);
            Array.Resize(ref minimumGaps, count);
        }

        // An inactive parent prevents OnEnable before the path and distance are assigned.
        _spawnRoot = new GameObject("Spawned Cars");
        _spawnRoot.SetActive(false);
        _spawnRoot.transform.SetParent(transform, false);
        for (int i = 0; i < selected.Length; i++)
        {
            AmbientCar prefab = selected[i];
            if (!Path.CanPlaceCar(distances[i], prefab.VehicleLength, prefab.MinimumGap, ExtraSpawnGap))
                continue;

            AmbientCar car = Instantiate(prefab, Path.GetPosition(distances[i]), Quaternion.identity, _spawnRoot.transform);
            car.MaxSpeed = UnityEngine.Random.Range(_minMaxVelocity.x, _minMaxVelocity.y);
            car.Initialize(Path, distances[i]);
            car.enabled = true;
            car.gameObject.SetActive(true);
            SpawnedCount++;
        }
        _spawnRoot.SetActive(true);

        if (SpawnedCount < CarCount)
            Debug.LogWarning($"TrafficSpawner : {SpawnedCount}/{CarCount} voitures creees, selon la place disponible sur le circuit.", this);
    }

    [Button("Supprimer les voitures generees"), DisableInEditorMode]
    public void ClearSpawnedCars()
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

    private void OnValidate()
    {
        CarCount = Mathf.Max(0, CarCount);
        SpacingStandardDeviation = Mathf.Max(0f, SpacingStandardDeviation);
        ExtraSpawnGap = Mathf.Max(0.1f, ExtraSpawnGap);
    }
}
