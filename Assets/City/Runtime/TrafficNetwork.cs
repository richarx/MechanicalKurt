using System.Collections.Generic;
using System.Text;
using PurrNet.Packing;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

public struct TrafficState : IPredictedData<TrafficState>, IDuplicate<TrafficState>
{
    /// <summary>Heure du trafic (secondes), qui règle les feux et les délais entre arrêts.</summary>
    public double Clock;
    public DisposableList<CarSimState> Cars;
    /// <summary>Fin du délai de chaque point d'arrêt, dans l'ordre des circuits.</summary>
    public DisposableList<double> Cooldowns;

    public void Dispose()
    {
        Cars.Dispose();
        Cooldowns.Dispose();
    }

    public TrafficState Duplicate() => new TrafficState
    {
        Clock = Clock,
        Cars = Cars.Duplicate(),
        Cooldowns = Cooldowns.Duplicate(),
    };

    public override string ToString() => $"heure {Clock:0.00}, {(Cars.isDisposed ? 0 : Cars.Count)} voitures";
}

/// <summary>
/// Trafic partagé : chaque machine avance les voitures au même tick depuis le même état, et l'hôte n'envoie l'état
/// complet qu'à l'arrivée d'un joueur ou quand une machine diverge. Les voitures restent donc solides en multijoueur.
/// Les voitures bougent en fin de tick, après tous les pigeons : chacun voit les mêmes voitures au tick suivant.
/// </summary>
public class TrafficNetwork : DeterministicIdentity<TrafficState>
{
    readonly List<TrafficPath> _paths = new List<TrafficPath>();
    readonly List<AmbientCar> _cars = new List<AmbientCar>();
    readonly List<CityPathPoint> _stops = new List<CityPathPoint>();
    DisposableList<CarSimState> _viewCars;
    bool _collected;

    /// <summary>Vrai quand un trafic synchronisé tourne : les voitures peuvent rester solides à plusieurs.</summary>
    public static bool IsRunning { get; private set; }

    public IReadOnlyList<AmbientCar> Cars => _cars;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsRunning = false;

    void Awake()
    {
        // Une machine qui diverge (arrondis différents entre plateformes) reçoit l'état de l'hôte.
        if (desyncPolicy == DesyncPolicyOverride.Inherit)
            desyncPolicy = DesyncPolicyOverride.Correct;

        // Avant le premier Update des circuits : sans ça, chaque machine avancerait ses voitures de son côté.
        foreach (TrafficPath path in FindObjectsByType<TrafficPath>(FindObjectsSortMode.None))
        {
            if (path.gameObject.scene == gameObject.scene)
                path.DrivenByNetwork = true;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (TrafficPath path in FindObjectsByType<TrafficPath>(FindObjectsSortMode.None))
        {
            if (path.gameObject.scene == gameObject.scene)
                path.DrivenByNetwork = false;
        }
        if (!_viewCars.isDisposed)
            _viewCars.Dispose();
        IsRunning = false;
        CityClock.Release();
    }

    void Collect()
    {
        if (_collected)
            return;
        _collected = true;

        var spawners = new List<TrafficSpawner>();
        foreach (TrafficSpawner spawner in FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))
        {
            if (spawner.gameObject.scene == gameObject.scene)
                spawners.Add(spawner);
        }
        SortByHierarchy(spawners);
        foreach (TrafficSpawner spawner in spawners)
            spawner.EnsureSpawned();

        foreach (AmbientCar car in FindObjectsByType<AmbientCar>(FindObjectsSortMode.None))
        {
            if (car.gameObject.scene != gameObject.scene || car.Path == null)
                continue;
            _cars.Add(car);
            if (!_paths.Contains(car.Path))
                _paths.Add(car.Path);
        }
        SortByHierarchy(_cars);
        SortByHierarchy(_paths);

        foreach (TrafficPath path in _paths)
        {
            path.DrivenByNetwork = true;
            for (int i = 0; i < path.PointCount; i++)
            {
                if (path.GetPoint(i).HasStop)
                    _stops.Add(path.GetPoint(i));
            }
        }
        IsRunning = true;
    }

    protected override TrafficState GetInitialState()
    {
        Collect();
        var state = new TrafficState
        {
            Cars = DisposableList<CarSimState>.Create(_cars.Count),
            Cooldowns = DisposableList<double>.Create(_stops.Count),
        };
        foreach (AmbientCar car in _cars)
            state.Cars.Add(car.CaptureSimState());
        foreach (CityPathPoint stop in _stops)
            state.Cooldowns.Add(double.NegativeInfinity);
        return state;
    }

    protected override void SetUnityState(TrafficState state)
    {
        Apply(state);
        Physics.SyncTransforms();
    }

    protected override void LateSimulate(ref TrafficState state, sfloat delta)
    {
        float dt = delta.ToFloat();
        state.Clock += dt;
        Apply(state);
        foreach (TrafficPath path in _paths)
            path.Step(dt);
        Capture(ref state);
        Physics.SyncTransforms();
    }

    void Apply(in TrafficState state)
    {
        CityClock.Drive(state.Clock);
        if (!Matches(state))
            return;
        for (int i = 0; i < _cars.Count; i++)
        {
            if (_cars[i] != null)
                _cars[i].ApplySimState(state.Cars[i]);
        }
        for (int i = 0; i < _stops.Count; i++)
            _stops[i].CooldownEnd = state.Cooldowns[i];
    }

    void Capture(ref TrafficState state)
    {
        if (!Matches(state))
            return;
        for (int i = 0; i < _cars.Count; i++)
        {
            if (_cars[i] != null)
                state.Cars[i] = _cars[i].CaptureSimState();
        }
        for (int i = 0; i < _stops.Count; i++)
            state.Cooldowns[i] = _stops[i].CooldownEnd;
    }

    bool Matches(in TrafficState state)
    {
        return !state.Cars.isDisposed && !state.Cooldowns.isDisposed
            && state.Cars.Count == _cars.Count && state.Cooldowns.Count == _stops.Count;
    }

    /// <summary>
    /// Voitures entre deux ticks, dans une liste réutilisée (l'état de vue ne doit pas allouer) : la distance suit
    /// le circuit, même au passage de la boucle.
    /// </summary>
    protected override TrafficState Interpolate(TrafficState from, TrafficState to, float t)
    {
        if (from.Cars.isDisposed || to.Cars.isDisposed || from.Cars.Count != to.Cars.Count
            || to.Cars.Count != _cars.Count)
            return to;

        if (_viewCars.isDisposed)
            _viewCars = DisposableList<CarSimState>.Create(_cars.Count);
        _viewCars.Clear();
        for (int i = 0; i < to.Cars.Count; i++)
        {
            CarSimState a = from.Cars[i];
            CarSimState b = to.Cars[i];
            AmbientCar car = _cars[i];
            float step = b.Distance - a.Distance;
            if (car != null && car.Path != null && car.Path.IsValid)
            {
                float length = car.Path.Length;
                if (step > length * 0.5f) step -= length;
                else if (step < -length * 0.5f) step += length;
            }
            b.Distance = a.Distance + step * t;
            b.Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
            _viewCars.Add(b);
        }
        double clock = from.Clock + (to.Clock - from.Clock) * t;
        return new TrafficState { Clock = clock, Cars = _viewCars, Cooldowns = to.Cooldowns };
    }

    protected override void UpdateView(TrafficState view, TrafficState? verified)
    {
        CityClock.ShowView(view.Clock);
        if (view.Cars.isDisposed || view.Cars.Count != _cars.Count)
            return;
        for (int i = 0; i < _cars.Count; i++)
        {
            if (_cars[i] != null)
                _cars[i].ShowView(view.Cars[i].Distance, view.Cars[i].Rotation);
        }
    }

    static void SortByHierarchy<T>(List<T> items) where T : Component
    {
        var keys = new Dictionary<T, string>();
        foreach (T item in items)
            keys[item] = HierarchyKey(item.transform);
        items.Sort((a, b) => string.CompareOrdinal(keys[a], keys[b]));
    }

    static string HierarchyKey(Transform transform)
    {
        var chain = new List<Transform>();
        for (Transform t = transform; t != null; t = t.parent)
            chain.Add(t);
        var key = new StringBuilder();
        for (int i = chain.Count - 1; i >= 0; i--)
            key.Append('/').Append(chain[i].GetSiblingIndex().ToString("D4")).Append(chain[i].name);
        return key.ToString();
    }
}
