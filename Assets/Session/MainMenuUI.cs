using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Écrans du menu principal : choix, création (code à partager), saisie du code pour rejoindre.</summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] SessionLauncher _launcher;

    [Header("Écrans")]
    [SerializeField] GameObject _choicePanel;
    [SerializeField] GameObject _createPanel;
    [SerializeField] GameObject _joinPanel;
    [SerializeField] GameObject _busyPanel;

    [Header("Choix")]
    [SerializeField] Button _createButton;
    [SerializeField] Button _joinButton;
    [Tooltip("Message sous les choix : retour d'une partie terminée, erreur de création…")]
    [SerializeField] TMP_Text _choiceMessage;

    [Header("Créer")]
    [SerializeField] TMP_Text _codeText;
    [SerializeField] Button _copyCodeButton;
    [Tooltip("Visible seulement dans l'export web, où la page a une adresse à partager.")]
    [SerializeField] Button _copyLinkButton;
    [SerializeField] Button _createContinueButton;
    [SerializeField] Button _createBackButton;
    [SerializeField] TMP_Text _createFeedback;

    [Header("Rejoindre")]
    [SerializeField] TMP_InputField _codeInput;
    [SerializeField] Button _pasteButton;
    [SerializeField] Button _joinContinueButton;
    [SerializeField] Button _joinBackButton;
    [SerializeField] TMP_Text _joinError;

    [Header("Attente")]
    [SerializeField] TMP_Text _busyText;

    const string PasteMethod = nameof(OnClipboardText);

    public string ShownCode => _codeText != null ? _codeText.text : null;
    public string JoinError => _joinError != null ? _joinError.text : null;
    public string ChoiceMessage => _choiceMessage != null ? _choiceMessage.text : null;
    public bool IsChoiceShown => _choicePanel != null && _choicePanel.activeSelf;
    public bool IsCreateShown => _createPanel != null && _createPanel.activeSelf;
    public bool IsJoinShown => _joinPanel != null && _joinPanel.activeSelf;

    IEnumerator Start()
    {
        _createButton.onClick.AddListener(OnCreate);
        _joinButton.onClick.AddListener(() => ShowJoin(null));
        _copyCodeButton.onClick.AddListener(() => Copy(GameSession.Code, "Code copied!"));
        _copyLinkButton.onClick.AddListener(() => Copy(GameSession.InviteLink, "Link copied!"));
        _createContinueButton.onClick.AddListener(_launcher.EnterGame);
        _createBackButton.onClick.AddListener(OnCreateBack);
        _pasteButton.onClick.AddListener(() => ClipboardBridge.RequestPaste(gameObject, PasteMethod));
        _joinContinueButton.onClick.AddListener(OnJoin);
        _joinBackButton.onClick.AddListener(() => ShowChoice(null));
        _codeInput.onValueChanged.AddListener(OnCodeEdited);
        _codeInput.onSubmit.AddListener(_ => OnJoin());
        ClipboardBridge.ListenBrowserPaste(gameObject, PasteMethod);

        ShowBusy("Loading the city…");
        while (!_launcher.GameSceneLoaded && _launcher.LoadError == null)
            yield return null;
        if (_launcher.LoadError != null)
        {
            ShowBusy(_launcher.LoadError);
            yield break;
        }

        string message = GameSession.PendingMessage;
        GameSession.PendingMessage = null;
        string invited = SessionCode.FromInviteLink(GameSession.PageUrl);
        if (invited != null)
            ShowJoin(invited);
        else
            ShowChoice(message);
    }

    public void OnCreate() => StartCoroutine(CreateRoutine());

    IEnumerator CreateRoutine()
    {
        ShowBusy("Creating the game…");
        string error = null;
        yield return _launcher.Create(result => error = result);
        if (error != null)
        {
            ShowChoice(error);
            yield break;
        }
        ShowOnly(_createPanel);
        _codeText.text = GameSession.Code;
        _createFeedback.text = "";
        _copyLinkButton.gameObject.SetActive(GameSession.InviteLink != null);
    }

    void OnCreateBack()
    {
        _launcher.Leave();
        ShowChoice(null);
    }

    public void OnJoin()
    {
        if (!_joinPanel.activeSelf)
            return;
        StartCoroutine(JoinRoutine(SessionCode.Normalize(_codeInput.text)));
    }

    /// <summary>Tests : saisit un code et rejoint, comme le joueur.</summary>
    public void JoinWithCode(string code)
    {
        ShowJoin(code);
        OnJoin();
    }

    IEnumerator JoinRoutine(string code)
    {
        if (!SessionCode.IsValid(code))
        {
            _joinError.text = $"The code is {SessionCode.Length} characters long (letters and digits).";
            yield break;
        }
        ShowBusy("Joining the game…");
        string error = null;
        yield return _launcher.Join(code, result => error = result);
        if (error != null)
        {
            ShowJoin(code);
            _joinError.text = error;
            yield break;
        }
        _launcher.EnterGame();
    }

    /// <summary>Texte du presse-papiers (bouton « Coller » ou Ctrl+V dans le navigateur).</summary>
    public void OnClipboardText(string text)
    {
        if (_joinPanel.activeSelf)
            _codeInput.text = SessionCode.Normalize(text);
    }

    void OnCodeEdited(string text)
    {
        string code = SessionCode.Normalize(text);
        if (code != text)
            _codeInput.SetTextWithoutNotify(code);
        _joinError.text = "";
    }

    void Copy(string text, string feedback)
    {
        ClipboardBridge.Copy(text);
        _createFeedback.text = feedback;
    }

    void ShowChoice(string message)
    {
        ShowOnly(_choicePanel);
        _choiceMessage.text = message ?? "";
    }

    void ShowJoin(string code)
    {
        ShowOnly(_joinPanel);
        _codeInput.SetTextWithoutNotify(code ?? "");
        _joinError.text = "";
        _codeInput.ActivateInputField();
    }

    void ShowBusy(string text)
    {
        ShowOnly(_busyPanel);
        _busyText.text = text;
    }

    void ShowOnly(GameObject panel)
    {
        _choicePanel.SetActive(panel == _choicePanel);
        _createPanel.SetActive(panel == _createPanel);
        _joinPanel.SetActive(panel == _joinPanel);
        _busyPanel.SetActive(panel == _busyPanel);
    }
}
