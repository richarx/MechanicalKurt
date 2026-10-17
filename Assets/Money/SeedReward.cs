using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeedReward : MonoBehaviour
{
    public static SeedReward instance { get; private set; }
    [SerializeField] private RectTransform seedPrefab;
    [SerializeField] private RectTransform seedLayer;
    [SerializeField] private Camera worldCamera;

    [SerializeField] private float burstRadius = 120f;
    [SerializeField] private float burstDuration = 0.35f;

    [SerializeField] private float flyDurationMin = 0.5f;
    [SerializeField] private float flyDurationMax = 0.8f;
    [SerializeField] private float delayBetweenSeeds = 0.05f;
    [SerializeField] private float curveStrength = 200f;
    [SerializeField] private float endScale = 0.6f;

    [SerializeField] private int maxVisualSeeds = 15;

    private readonly Stack<RectTransform> pool = new Stack<RectTransform>();
    private Canvas rootCanvas;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (worldCamera == null)
            worldCamera = Camera.main;

        rootCanvas = seedLayer.GetComponentInParent<Canvas>().rootCanvas;
    }

    private void OnDisable()
    {
        if (ScoreManager.instance != null)
            ScoreManager.instance.SyncDisplay();
    }

    public void GetReward(Vector3 worldPosition, int amount)
    {
        SpawnFromWorld(worldPosition, amount);
    }


    private void SpawnFromWorld(Vector3 worldPosition, int amount)
    {
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPosition);

        if (screenPos.z < 0f)
        {
            ScoreManager.instance.AddScore(amount);
            return;
        }

        SpawnFromScreen(screenPos, amount);
    }

    private void SpawnFromScreen(Vector2 screenPosition, int amount)
    {
        if (amount <= 0) return;

        ScoreManager.instance.AddScoreDeferred(amount);

        int seedCount = Mathf.Clamp(amount, 1, maxVisualSeeds);

        // Split the amount so the seeds add up exactly (e.g. 50 over 15 seeds = 3 or 4 each).
        int baseValue = amount / seedCount;
        int remainder = amount % seedCount;

        for (int i = 0; i < seedCount; i++)
        {
            int value = baseValue + (i < remainder ? 1 : 0);
            StartCoroutine(AnimateSeed(screenPosition, value, i * delayBetweenSeeds));
        }
    }

    private IEnumerator AnimateSeed(Vector2 start, int value, float flightDelay)
    {
        float scale = rootCanvas.scaleFactor;

        RectTransform seed = GetSeed();
        seed.position = start;
        seed.localScale = Vector3.one;

        // ---- Phase 1: burst outward (fast, then slows down) ----
        Vector2 burstTarget = start + Random.insideUnitCircle * burstRadius * scale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / burstDuration;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);   // ease-out cubic
            seed.position = Vector2.LerpUnclamped(start, burstTarget, e);
            yield return null;
        }

        // Staggered departure so seeds arrive one after another.
        if (flightDelay > 0f)
            yield return new WaitForSeconds(flightDelay);

        // ---- Phase 2: curved flight into the score icon (slow, then fast) ----
        RectTransform target = ScoreManager.instance.ScoreIcon;
        Vector2 from = seed.position;
        Vector2 initialTo = target.position;

        Vector2 dir = (initialTo - from).normalized;
        Vector2 side = new Vector2(-dir.y, dir.x) * (Random.value < 0.5f ? -1f : 1f);
        Vector2 control = (from + initialTo) * 0.5f
                          + side * curveStrength * scale * Random.Range(0.5f, 1f);

        float duration = Random.Range(flyDurationMin, flyDurationMax);
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            float e = k * k * k;

            Vector2 to = target.position;
            seed.position = QuadraticBezier(from, control, to, e);
            seed.localScale = Vector3.one * Mathf.Lerp(1f, endScale, e);
            yield return null;
        }

        ReleaseSeed(seed);
        ScoreManager.instance.OnSeedArrived(value);
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // ---- Simple object pool: reuse seeds instead of Instantiate/Destroy every time ----

    private RectTransform GetSeed()
    {
        RectTransform seed = pool.Count > 0 ? pool.Pop() : Instantiate(seedPrefab, seedLayer);
        seed.gameObject.SetActive(true);
        return seed;
    }

    private void ReleaseSeed(RectTransform seed)
    {
        seed.gameObject.SetActive(false);
        pool.Push(seed);
    }
}
