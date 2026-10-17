using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance { get; private set; }
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private float countDuration = 2f;
    [SerializeField] private Squeeze iconSqueeze;
    private int score;
    private float displayedScore;
    private int lastShownScore = -1;
    private float countFrom;
    private float countTimer;

    //Sound
    [SerializeField] private AudioClip scoreGainLoopSound;
    private Coroutine coinSoundRoutine;
    private AudioSource coinSource;

    private void Awake()
    {
        if (instance != null && instance != this) 
            Destroy(gameObject);
        else
            instance = this;
    }

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        if (Keyboard.current.kKey.wasPressedThisFrame)
            AddScore(200);

        if (countTimer < countDuration)
        {
            countTimer += Time.deltaTime;
            float t = Mathf.Clamp01(countTimer / countDuration);
            displayedScore = Mathf.Lerp(countFrom, score, t);
            UpdateUI();
        }
    }

    public void AddScore(int amount)
    {
        score += amount;
        
        countFrom = displayedScore;
        countTimer = 0f;

        if (iconSqueeze != null)
            iconSqueeze.SqueezeForDuration(countDuration);

        if (coinSoundRoutine != null)
            StopCoroutine(coinSoundRoutine);

        coinSoundRoutine = StartCoroutine(CoinSoundLoop());
    }

    private IEnumerator CoinSoundLoop()
    {
        if (coinSource == null)
            coinSource = SFXManager.Instance.PlaySFX(scoreGainLoopSound, loop: true);

        yield return new WaitForSeconds(countDuration);

        Destroy(coinSource.gameObject);
        coinSource = null;
        coinSoundRoutine = null;
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