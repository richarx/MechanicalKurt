using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class PigeonFan : MonoBehaviour
{
    [field: SerializeField, BoxGroup("Souffle"), MinValue(0.01f), SuffixLabel("unités", true)]
    [field: Tooltip("Rayon horizontal en unités monde, indépendant de l'échelle de l'objet.")]
    public float Radius { get; private set; } = 3f;

    [field: SerializeField, BoxGroup("Souffle"), MinValue(0.01f), SuffixLabel("unités", true)]
    [field: Tooltip("Hauteur maximale au-dessus de la position du ventilateur. Le souffle reste vertical.")]
    public float Height { get; private set; } = 12f;

    [field: SerializeField, BoxGroup("Souffle"), MinValue(0.01f), SuffixLabel("unités/s", true)]
    public float LiftSpeed { get; private set; } = 8f;

    [field: SerializeField, BoxGroup("Souffle"), MinValue(0.01f), SuffixLabel("unités/s²", true)]
    [field: Tooltip("Vitesse de convergence vers la montée souhaitée, après les forces de vol.")]
    public float Acceleration { get; private set; } = 30f;

    [field: SerializeField, BoxGroup("Souffle"), MinValue(0.01f), SuffixLabel("unités", true)]
    [field: Tooltip("Distance sous le plafond sur laquelle la montée ralentit progressivement.")]
    public float SlowdownDistance { get; private set; } = 2f;

    [field: SerializeField, BoxGroup("Visuel")]
    [field: Tooltip("Hélice facultative, sans collider. Sa rotation n'affecte pas le souffle.")]
    public Transform Rotor { get; private set; }

    [field: SerializeField, BoxGroup("Visuel"), SuffixLabel("°/s", true)]
    public float RotorSpeed { get; private set; } = 720f;

    private static readonly List<PigeonFan> ActiveFans = new List<PigeonFan>();

    private void OnEnable()
    {
        if (!ActiveFans.Contains(this))
            ActiveFans.Add(this);
    }

    private void OnDisable()
    {
        ActiveFans.Remove(this);
    }

    private void Update()
    {
        if (Rotor != null && Rotor != transform)
            Rotor.Rotate(Vector3.up, RotorSpeed * Time.deltaTime, Space.Self);
    }

    // Called after flight forces, before the single CharacterController.Move.
    // Evaluate each fan from the same incoming velocity so overlapping fans do not stack.
    public static Vector3 ApplyUpdrafts(Vector3 position, Vector3 velocity, float deltaTime, bool grounded)
    {
        if (deltaTime <= 0f)
            return velocity;

        bool affected = false;
        float verticalSpeed = float.NegativeInfinity;
        foreach (PigeonFan fan in ActiveFans)
        {
            if (fan == null || !fan.isActiveAndEnabled)
                continue;

            Vector3 offset = position - fan.transform.position;
            if (offset.y < 0f || offset.y > fan.Height ||
                offset.x * offset.x + offset.z * offset.z > fan.Radius * fan.Radius)
                continue;

            float remainingHeight = Mathf.Max(0f, fan.Height - offset.y);
            float targetSpeed = fan.LiftSpeed * Mathf.Clamp01(
                remainingHeight / Mathf.Max(0.01f, fan.SlowdownDistance));
            // Ground movement reapplies a downward stick-to-ground speed every frame.
            // Ignore it here so even a gentle fan can start lifting the pigeon.
            float incomingSpeed = grounded ? Mathf.Max(0f, velocity.y) : velocity.y;
            float candidate = Mathf.MoveTowards(incomingSpeed, targetSpeed, fan.Acceleration * deltaTime);
            // Stop at this fan's ceiling even during a long frame or a fast ascent.
            candidate = Mathf.Min(candidate, remainingHeight / deltaTime);
            verticalSpeed = Mathf.Max(verticalSpeed, candidate);
            affected = true;
        }

        if (affected)
            velocity.y = verticalSpeed;
        return velocity;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Vector3 bottom = transform.position;
        Vector3 top = bottom + Vector3.up * Height;
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float nextAngle = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Radius;
            Vector3 nextRadial = new Vector3(Mathf.Cos(nextAngle), 0f, Mathf.Sin(nextAngle)) * Radius;
            Gizmos.DrawLine(bottom + radial, bottom + nextRadial);
            Gizmos.DrawLine(top + radial, top + nextRadial);
            if (i % (segments / 4) == 0)
                Gizmos.DrawLine(bottom + radial, top + radial);
        }

        Gizmos.DrawLine(bottom, top);
        Gizmos.color = previousColor;
    }
}
