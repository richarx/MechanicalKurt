using UnityEngine;

public class MoveZeppelin : MonoBehaviour
{
    [SerializeField] private float distanceFromCenter;
    [SerializeField] private float speed;

    private void Update()
    {
        float xPosition = Mathf.Sin(Time.time * speed) * distanceFromCenter;
        float zPosition = Mathf.Cos(Time.time * speed) * distanceFromCenter;

        Vector3 position = new Vector3(xPosition, 0.0f, zPosition);
        transform.localPosition = position;

        Vector2 direction = new Vector2(xPosition, zPosition);
        direction = direction.AddAngleToDirection(-90.0f);

        Vector3 normalizedDirection = new Vector3(direction.x, 0.0f, direction.y).normalized;
        transform.localRotation = Quaternion.LookRotation(normalizedDirection);
    }
}
