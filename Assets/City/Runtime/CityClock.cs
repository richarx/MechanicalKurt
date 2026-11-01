using UnityEngine;

/// <summary>
/// Heure des feux et des arrêts : celle du trafic synchronisé quand il tourne (identique pour tous les joueurs),
/// sinon le temps local.
/// </summary>
public static class CityClock
{
    static bool _driven;
    static double _time;

    public static double Now => _driven ? _time : Time.timeAsDouble;

    public static void Drive(double time)
    {
        _driven = true;
        _time = time;
    }

    public static void Release() => _driven = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _driven = false;
}
