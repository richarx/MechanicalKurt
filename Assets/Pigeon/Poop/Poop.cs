using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Poop : MonoBehaviour
{
    [SerializeField] private Material splatterMaterial;
    [SerializeField] private DecalProjector decalProjectorPrefab;
    [SerializeField] private List<Texture> splatterTextures;

    private Rigidbody _rigidbody;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        SpawnSplatter(collision.contacts[0].point);
        Destroy(gameObject);
    }

    private void SpawnSplatter(Vector3 position)
    {
        DecalProjector decal = Instantiate(decalProjectorPrefab, position, Quaternion.identity);
        Material material = new Material(splatterMaterial);
        material.SetTexture("_DecalColorMap", splatterTextures[Random.Range(0, splatterTextures.Count)]);
        decal.material = material;
        decal.transform.rotation = Quaternion.Euler(new Vector3(90.0f, 0.0f, 0.0f));
    }
}
