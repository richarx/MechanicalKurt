using UnityEngine;

public class Poop : MonoBehaviour
{
    Rigidbody _rigidbody;


    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        transform.position = collision.contacts[0].point;
        transform.up = collision.contacts[0].normal;
        transform.parent = collision.transform;
        _rigidbody.isKinematic = true;
    }
}
