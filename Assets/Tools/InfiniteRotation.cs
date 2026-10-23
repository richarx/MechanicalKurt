using Sirenix.OdinInspector;
using UnityEngine;

public class InfiniteRotation : MonoBehaviour
{
    [field: SerializeField, SuffixLabel("°/s", true)]
    public float RotationSpeed { get; private set; } = 90f;

    private void Update()
    {
        transform.Rotate(Vector3.up, RotationSpeed * Time.deltaTime, Space.Self);
    }
}
