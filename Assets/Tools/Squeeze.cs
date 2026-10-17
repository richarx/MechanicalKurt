using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Squeeze : MonoBehaviour
{
    public Transform targetTransform;
    private Coroutine squeezeRoutine = null;

    public float xSqueeze;
    public float ySqueeze;
    public float duration;
    public float delay;
    private Vector3 OGSize;

    void Awake()
    {
        OGSize = targetTransform.localScale;
    }

    public void Trigger()
    {
        if (squeezeRoutine != null)
            StopCoroutine(squeezeRoutine);
            
        squeezeRoutine = StartCoroutine(SqueezeAndStretch());
    }

    public void SqueezeForDuration(float totalDuration)
    {
        if (squeezeRoutine != null)
            StopCoroutine(squeezeRoutine);

        squeezeRoutine = StartCoroutine(SqueezeLoop(totalDuration));
    }

    private IEnumerator SqueezeLoop(float totalDuration)
    {
        float endTime = Time.time + totalDuration;

        while (Time.time < endTime)
            yield return SqueezeAndStretch();
    }

    private IEnumerator SqueezeAndStretch()
    {
        Vector3 newSize = new Vector3(OGSize.x * xSqueeze, OGSize.y * ySqueeze, OGSize.z);

        float timer = 0f;
        float t = 0f;

        if (delay != 0)
            yield return new WaitForSeconds(delay);

        while (timer < duration)
        {
            t = Tools.NormalizeValue(timer, 0, duration);
            targetTransform.localScale = Vector3.Lerp(newSize, OGSize, t);
            yield return null;
            timer += Time.deltaTime;
        }

        targetTransform.localScale = OGSize;
    }
}