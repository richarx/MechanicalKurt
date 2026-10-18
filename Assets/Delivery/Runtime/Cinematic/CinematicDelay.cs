using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class CinematicDelay : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _seconds = 1f;
    [SerializeField] private UnityEvent<int> _onCompleted = new UnityEvent<int>();
    
    private Coroutine _routine;

    public void Wait(int token)
    {
        CancelWait();
        if (isActiveAndEnabled) _routine = StartCoroutine(WaitAndContinue(token));
    }

    private IEnumerator WaitAndContinue(int token)
    {
        yield return new WaitForSecondsRealtime(_seconds);
        _routine = null;
        // Forward the original token, never the manager's latest token.
        _onCompleted.Invoke(token);
    }

    public void CancelWait()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
    }

    private void OnDisable() => CancelWait();
}
