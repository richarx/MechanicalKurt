using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class Popup : MonoBehaviour
{
    [SerializeField, TextArea] string _keyboardText;

    [SerializeField, TextArea] string _joypadText;

    [SerializeField] TMP_Text _textBox;

    // Partagé et toujours à l'écoute : un popup masqué affiche le bon texte dès sa réapparition.
    private static bool _usingGamepad;
    private static event Action _inputMethodChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        _usingGamepad = false;
        _inputMethodChanged = null;
        InputSystem.onEvent -= HandleInputEvent;
        InputSystem.onEvent += HandleInputEvent;
    }

    private static void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!(device is Gamepad) && !(device is Keyboard)) return;

        bool usingGamepad = device is Gamepad;
        float threshold = 0.1f;
        // Read changed controls from the event: a held key or an idle gamepad must not steal focus.
        // Include analog controls so moving a stick also switches the instructions.
        if (usingGamepad != _usingGamepad && eventPtr.HasButtonPress(magnitude: threshold, buttonControlsOnly: false))
        {
            _usingGamepad = usingGamepad;
            _inputMethodChanged?.Invoke();
        }
    }

    private void OnEnable()
    {
        _inputMethodChanged += RefreshCommands;
        RefreshCommands();
    }

    private void OnDisable()
    {
        _inputMethodChanged -= RefreshCommands;
    }

    private string _keyboardOverride;
    private string _joypadOverride;

    /// <summary>Remplace le texte le temps d'un affichage (ex. une zone de course) ; ClearTexts revient aux textes réglés.</summary>
    public void SetTexts(string keyboard, string joypad)
    {
        _keyboardOverride = keyboard;
        _joypadOverride = joypad;
        RefreshCommands();
    }

    public void ClearTexts() => SetTexts(null, null);

    /// <summary>Texte affiché pour le clavier et pour la manette (avec un éventuel remplacement).</summary>
    public string KeyboardText => string.IsNullOrEmpty(_keyboardOverride) ? _keyboardText : _keyboardOverride;
    public string JoypadText => string.IsNullOrEmpty(_joypadOverride) ? _joypadText : _joypadOverride;

    private void RefreshCommands()
    {
        if (_textBox != null)
            _textBox.text = _usingGamepad ? JoypadText : KeyboardText;
    }
}
