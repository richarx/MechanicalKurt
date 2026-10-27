using UnityEditor;
using UnityEngine;

// « Error Pause » (console) met le Play en pause à chaque erreur : un ✘ de scénario ou une exception du jeu
// figerait toute la série. Pendant les scénarios, on reprend aussitôt ; l'erreur reste dans la console et le rapport.
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
