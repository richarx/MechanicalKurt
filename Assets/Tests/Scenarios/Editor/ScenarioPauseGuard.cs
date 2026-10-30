using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ScenarioPauseGuard
{
    static ScenarioPauseGuard()
    {
        EditorApplication.pauseStateChanged += state =>
        {
            if (state != PauseState.Paused || !SessionState.GetBool(ScenarioRunner.ActiveKey, false))
                return;
            Debug.Log("[Scénario] Pause sur erreur ignorée pendant les scénarios.");
            EditorApplication.isPaused = false;
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                SessionState.EraseBool(ScenarioRunner.ActiveKey);
        };
    }
}
