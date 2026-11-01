using UnityEngine;

public class RandomScratchRecord : MonoBehaviour
{
    [SerializeField] private ScratchRecord _scratchRecord;
    [SerializeField, Range(0f, 1f)] private float _chancePerSecond = 0.1f;
    [SerializeField] private int _minScratches = 2;
    [SerializeField] private int _maxScratches = 4;

    private float _elapsedTime;

    private void Awake()
    {
        if (_scratchRecord == null)
            _scratchRecord = GetComponent<ScratchRecord>();

        if (_scratchRecord == null)
        {
            Debug.LogError("RandomScratchRecord : assigner un ScratchRecord ou placer les deux composants sur le même objet.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _elapsedTime = 0f;
    }

    private void Update()
    {
        _elapsedTime += Time.deltaTime;

        while (_elapsedTime >= 1f)
        {
            _elapsedTime -= 1f;

            if (_chancePerSecond < 1f && Random.value >= _chancePerSecond)
                continue;

            int min = Mathf.Max(1, Mathf.Min(_minScratches, _maxScratches));
            int max = Mathf.Max(min, Mathf.Max(_minScratches, _maxScratches));
            // La borne haute entière de Random.Range est exclusive : les deux bornes restent incluses ici.
            int repetitions = Random.Range(min - 1, max) + 1;
            _scratchRecord.LaunchScratch(repetitions);
        }
    }
}
