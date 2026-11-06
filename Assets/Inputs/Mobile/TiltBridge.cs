using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Inclinaison du téléphone (Plugins/WebGL/Tilt.jslib), convertie en valeur de joystick : pencher à droite ou à gauche
/// pour tourner, vers l'avant ou l'arrière pour piquer ou monter, par rapport à la position tenue au départ.
/// </summary>
public static class TiltBridge
{
    const float DeadZoneDegrees = 4f;
    const float FullTiltDegrees = 25f;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void TiltStart();
    [DllImport("__Internal")] static extern int TiltHasData();
    [DllImport("__Internal")] static extern float TiltRoll();
    [DllImport("__Internal")] static extern float TiltPitch();

    static bool ReadSensor(out float roll, out float pitch)
    {
        roll = TiltRoll();
        pitch = TiltPitch();
        return TiltHasData() != 0;
    }
#else
    static void TiltStart() { }

    static bool ReadSensor(out float roll, out float pitch)
    {
        roll = _testRoll;
        pitch = _testPitch;
        return _testHasData;
    }
#endif

    static bool _hasNeutral;
    static float _neutralPitch;

    public static void Begin() => TiltStart();

    /// <summary>Le navigateur envoie des données d'inclinaison (faux si refusé, sans capteur, ou bloqué par la page qui héberge le jeu).</summary>
    public static bool IsAvailable => ReadSensor(out _, out _);

    /// <summary>La position actuelle du téléphone devient la position neutre.</summary>
    public static void Recenter() => _hasNeutral = false;

    public static Vector2 Stick
    {
        get
        {
            if (!ReadSensor(out float roll, out float pitch))
                return Vector2.zero;
            if (!_hasNeutral)
            {
                _hasNeutral = true;
                _neutralPitch = pitch;
            }
            return new Vector2(Shape(roll), Shape(Mathf.DeltaAngle(pitch, _neutralPitch)));
        }
    }

    static float Shape(float degrees)
    {
        float amount = Mathf.InverseLerp(DeadZoneDegrees, FullTiltDegrees, Mathf.Abs(degrees));
        return degrees < 0f ? -amount : amount;
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    static bool _testHasData;
    static float _testRoll;
    static float _testPitch;

    /// <summary>Scénarios : simule le capteur du téléphone.</summary>
    public static void SetForTests(bool hasData, float roll, float pitch)
    {
        _testHasData = hasData;
        _testRoll = roll;
        _testPitch = pitch;
    }
#endif
}
