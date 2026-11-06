using System;
using UnityEngine;

/// <summary>
/// Heure des feux et des arrêts : celle du trafic synchronisé quand il tourne (identique pour tous les joueurs),
/// sinon le temps local.
/// </summary>
public static class CityClock
{
    static bool _driven;
    static double _time;
    static double _viewTime;

    public static bool IsDriven => _driven;

    /// <summary>Heure simulée : n'avance qu'aux ticks réseau, et recule pendant une correction.</summary>
    public static double Now => _driven ? _time : Time.timeAsDouble;

    /// <summary>Heure à afficher, lissée entre deux ticks réseau.</summary>
    public static double ViewNow => _driven ? _viewTime : Time.timeAsDouble;

    /// <summary>L'heure simulée vient de changer (tick ou correction).</summary>
    public static event Action Driven;

    /// <summary>L'heure à afficher vient de changer.</summary>
    public static event Action ViewChanged;

    public static void Drive(double time)
    {
        if (!_driven)
            _viewTime = time;
        _driven = true;
        _time = time;
        Driven?.Invoke();
    }

    public static void ShowView(double time)
    {
        _viewTime = time;
        ViewChanged?.Invoke();
    }

    public static void Release() => _driven = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _driven = false;
        Driven = null;
        ViewChanged = null;
    }
}
