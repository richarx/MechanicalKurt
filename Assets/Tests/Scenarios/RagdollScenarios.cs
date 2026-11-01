#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

public partial class ScenarioRunner
{
    /// <summary>Pose locale de chaque os du modèle (position, rotation), dans l'ordre de la hiérarchie.</summary>
    static (Transform[] bones, Vector3[] positions, Quaternion[] rotations) BonePose(PigeonController pigeon)
    {
        Animator animator = pigeon.GetComponentInChildren<Animator>();
        Transform[] bones = (animator != null ? animator.transform : pigeon.transform).GetComponentsInChildren<Transform>();
        var positions = new Vector3[bones.Length];
        var rotations = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            bones[i].GetLocalPositionAndRotation(out positions[i], out rotations[i]);
        return (bones, positions, rotations);
    }

    /// <summary>Plus grands écarts entre deux poses : position (m) et angle (°), avec l'os concerné.</summary>
    static string PoseGap((Transform[] bones, Vector3[] positions, Quaternion[] rotations) a,
        (Transform[] bones, Vector3[] positions, Quaternion[] rotations) b, out float maxPosition, out float maxAngle)
    {
        maxPosition = 0f;
        maxAngle = 0f;
        string positionBone = "-", angleBone = "-";
        for (int i = 1; i < a.bones.Length && i < b.bones.Length; i++)
        {
            float position = Vector3.Distance(a.positions[i], b.positions[i]);
            float angle = Quaternion.Angle(a.rotations[i], b.rotations[i]);
            if (position > maxPosition) { maxPosition = position; positionBone = a.bones[i].name; }
            if (angle > maxAngle) { maxAngle = angle; angleBone = a.bones[i].name; }
        }
        return $"position {maxPosition * 100f:0.0} cm ({positionBone}), angle {maxAngle:0.0}° ({angleBone})";
    }

    IEnumerator RagdollRecovers()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course existe (sol dégagé de sa zone de départ)", race != null);
        Vector3 ground = race.DepartureZone.transform.position + Vector3.up * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, race.DepartureZone.transform.eulerAngles.y, 0f);

        yield return Teleport(Pigeon, ground, rotation, "Le pigeon se pose");
        yield return ExpectLanded(Pigeon, 4f);
        yield return new WaitForSecondsRealtime(0.5f);
        var before = BonePose(Pigeon);
        Capture(Camera.main, "ragdoll_before");

        for (int fall = 1; fall <= 2; fall++)
        {
            yield return TeleportStunned(Pigeon, ground + Vector3.up * 6f, rotation, $"Chute {fall} : le pigeon tombe en ragdoll de haut");
            foreach (Rigidbody body in Pigeon.GetComponentsInChildren<Rigidbody>())
                if (!body.isKinematic)
                    body.AddForce(Random.onUnitSphere * 4f, ForceMode.VelocityChange);
            yield return new WaitForSecondsRealtime(1.5f);
            string during = PoseGap(before, BonePose(Pigeon), out _, out _);

            Bot.Flap = true;
            yield return Expect($"Chute {fall} : il se relève en battant des ailes", () => !Pigeon.IsStun, 3f, () => $"ragdoll {Pigeon.IsStun}");
            Bot.Flap = false;
            yield return Teleport(Pigeon, ground, rotation, $"Chute {fall} : retour au sol");
            yield return ExpectLanded(Pigeon, 4f);
            yield return new WaitForSecondsRealtime(0.5f);

            string after = PoseGap(before, BonePose(Pigeon), out float position, out float angle);
            Capture(Camera.main, $"ragdoll_after_{fall}");
            yield return Check($"Chute {fall} : debout, il a retrouvé sa forme", position < 0.1f && angle < 20f,
                $"après : {after} | pendant le ragdoll : {during}");
        }
    }
}
#endif
