using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SplatterAnimation : MonoBehaviour
{
    [SerializeField] private bool destroyAfterDuration;
    [SerializeField] private float destroyDuration;

    private DecalProjector decalProjector;

    private IEnumerator Start()
    {
        decalProjector = GetComponent<DecalProjector>();

        float duration = 0.15f;
        float timer = 0.0f;

        while (timer <= duration)
        {
            float size = Tools.NormalizeValueInRange(timer, 0.0f, duration, 0.3f, 3.0f);
            decalProjector.size = new Vector3(size, size, 3.0f);
            yield return null;
            timer += Time.deltaTime;
        }

        if (!destroyAfterDuration)
            yield break;

        yield return new WaitForSeconds(destroyDuration);

        duration = 3.0f;
        timer = duration;
        while (timer >= 0.0f)
        {
            float size = Tools.NormalizeValueInRange(timer, 0.0f, duration, 0.3f, 3.0f);
            decalProjector.size = new Vector3(size, size, 3.0f);
            yield return null;
            timer -= Time.deltaTime;
        }

        Destroy(gameObject);
    }
}
