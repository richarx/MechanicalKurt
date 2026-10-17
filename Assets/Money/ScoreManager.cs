using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance { get; private set; }
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private RectTransform scoreIcon;
    [SerializeField] private float countDuration = 2f;
    [SerializeField] private Squeeze iconSqueeze;
    public int Score { get; private set; }
    public RectTransform ScoreIcon => scoreIcon;

    private int displayTarget;
    private float displayedScore; 
    private int lastShownScore = -1;
    private float countFrom;
    private float countTimer;

    //Sound
    [SerializeField] private AudioClip seedArriveClip;

    private void Awake()
    {
        if (instance != null && instance != this)
            Destroy(gameObject);
        else
            instance = this;

        countTimer = countDuration;
    }

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        if (Keyboard.current.kKey.wasPressedThisFrame)
            SeedFlyEffect.instance.SpawnFromScreenCenter(200);

        if (countTimer < countDuration)
        {
            countTimer += Time.deltaTime;
            float t = Mathf.Clamp01(countTimer / countDuration);
            displayedScore = Mathf.Lerp(countFrom, displayTarget, t);
            UpdateUI();
        }
    }

    public void AddScore(int amount)
    {
        Score += amount;
        displayTarget = Score;

        countFrom = displayedScore;
        countTimer = 0f;

        if (iconSqueeze != null)
            iconSqueeze.SqueezeForDuration(countDuration);
    }

    public void AddScoreDeferred(int amount)
    {
        Score += amount;
    }

    public void OnSeedArrived(int amount)
    {
        displayTarget = Mathf.Min(displayTarget + amount, Score);
        SnapDisplay();

        if (iconSqueeze != null)
            iconSqueeze.Trigger();

        SFXManager.Instance.PlaySFX(seedArriveClip);
    }

    public void SyncDisplay()
    {
        displayTarget = Score;
        SnapDisplay();
    }

    private void SnapDisplay()
    {
        displayedScore = displayTarget;
        countTimer = countDuration;
        UpdateUI();
    }

    private void UpdateUI()
    {
        int shown = Mathf.RoundToInt(displayedScore);

        if (shown == lastShownScore)
            return;

        lastShownScore = shown;
        scoreText.text = $"x{shown}";
    }
}
