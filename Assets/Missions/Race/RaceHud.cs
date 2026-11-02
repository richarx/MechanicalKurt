using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Affichage local d'une course chronométrée : chrono et place en haut de l'écran, classement final au centre.</summary>
public class RaceHud : MonoBehaviour
{
    TMP_Text _race;
    GameObject _resultsPanel;
    TMP_Text _results;

    public string RaceText => _race != null && _race.gameObject.activeSelf ? _race.text : null;
    public string ResultsText => _resultsPanel != null && _resultsPanel.activeSelf ? _results.text : null;

    public static RaceHud Create(TMP_FontAsset font)
    {
        var root = new GameObject("[Race] HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var hud = root.AddComponent<RaceHud>();
        hud._race = CreateText(root.transform, "Timer", font, 54f, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 320f));
        hud._race.alignment = TextAlignmentOptions.Top;

        hud._resultsPanel = new GameObject("Results", typeof(RectTransform), typeof(Image));
        var panel = (RectTransform)hud._resultsPanel.transform;
        panel.SetParent(root.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(900f, 700f);
        hud._resultsPanel.GetComponent<Image>().color = new Color(0.08f, 0.02f, 0.02f, 0.78f);
        hud._results = CreateText(panel, "Text", font, 48f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 640f));
        hud._results.alignment = TextAlignmentOptions.Center;

        hud.HideAll();
        return hud;
    }

    static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, float size, Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(60, 0, 0, 255);
        text.raycastTarget = false;
        return text;
    }

    public void ShowRace(string content)
    {
        _race.text = content;
        _race.gameObject.SetActive(true);
    }

    public void HideRace() => _race.gameObject.SetActive(false);

    public void ShowResults(string content)
    {
        _results.text = content;
        _resultsPanel.SetActive(true);
    }

    public void HideResults() => _resultsPanel.SetActive(false);

    public void HideAll()
    {
        HideRace();
        HideResults();
    }

    void LateUpdate()
    {
        // Pas par-dessus le menu principal.
        bool show = !GameSession.InMenu;
        if (TryGetComponent(out Canvas canvas) && canvas.enabled != show)
            canvas.enabled = show;
    }
}
