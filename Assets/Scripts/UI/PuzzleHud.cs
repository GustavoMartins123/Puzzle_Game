using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleHud : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.085f, 0.97f);
    private static readonly Color AccentColor = new Color(0.12f, 0.72f, 0.64f, 1f);

    private Text metricsLabel;
    private Text statusLabel;
    private Text hintLabel;
    private Button hintButton;
    private Action hintRequested;

    public static PuzzleHud Show(RectTransform parent, Action hintRequested)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (hintRequested == null) throw new ArgumentNullException(nameof(hintRequested));

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException("Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        var rootObject = new GameObject("PuzzleHud", typeof(RectTransform), typeof(Image));
        rootObject.layer = parent.gameObject.layer;
        RectTransform root = (RectTransform)rootObject.transform;
        root.SetParent(parent, false);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -12f);
        root.sizeDelta = new Vector2(920f, 64f);
        rootObject.GetComponent<Image>().color = PanelColor;

        PuzzleHud hud = rootObject.AddComponent<PuzzleHud>();
        hud.hintRequested = hintRequested;
        hud.metricsLabel = CreateText(
            root,
            "Metrics",
            font,
            new Vector2(16f, -6f),
            new Vector2(660f, 30f),
            18,
            FontStyle.Bold,
            TextAnchor.MiddleLeft);
        hud.statusLabel = CreateText(
            root,
            "Status",
            font,
            new Vector2(16f, -34f),
            new Vector2(660f, 24f),
            14,
            FontStyle.Normal,
            TextAnchor.MiddleLeft);
        hud.statusLabel.color = new Color(0.72f, 0.8f, 0.88f, 1f);
        hud.hintButton = CreateButton(root, font, out hud.hintLabel);
        hud.hintButton.onClick.AddListener(hud.OnHintClicked);
        root.SetAsLastSibling();
        return hud;
    }

    public void Refresh(
        PuzzleSessionMetrics metrics,
        PuzzleDifficultyProfile difficulty,
        PuzzleHintController hints)
    {
        if (metrics == null || !metrics.IsActive)
            throw new ArgumentException("Active metrics are required.", nameof(metrics));
        if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
        if (hints == null) throw new ArgumentNullException(nameof(hints));

        int totalSeconds = Mathf.FloorToInt(metrics.ElapsedSeconds);
        metricsLabel.text =
            $"{totalSeconds / 60:00}:{totalSeconds % 60:00}   •   " +
            $"PEÇAS {metrics.PlacedPieces}/{metrics.TotalPieces}   •   " +
            $"MOVIMENTOS {metrics.MovesStarted}   •   " +
            $"ERROS {metrics.IncorrectAttempts}";

        if (difficulty.HintAllowance == PuzzleHintAllowance.Disabled)
        {
            hintLabel.text = "DICAS DESATIVADAS";
            hintButton.interactable = false;
            return;
        }

        if (hints.HasFiniteLimit && hints.RemainingHints <= 0)
        {
            hintLabel.text = "SEM DICAS";
            hintButton.interactable = false;
            return;
        }

        hintLabel.text = hints.CooldownRemaining > 0f
            ? $"DICA  {Mathf.CeilToInt(hints.CooldownRemaining)}s"
            : hints.HasFiniteLimit
                ? $"DICA  ({hints.RemainingHints})"
                : "DICA  (∞)";
        hintButton.interactable = hints.HasAvailableTarget;
    }

    public void ShowStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Status message is empty.", nameof(message));
        statusLabel.text = message;
    }

    public void Close()
    {
        hintButton.onClick.RemoveListener(OnHintClicked);
        hintRequested = null;
        Destroy(gameObject);
    }

    private void OnHintClicked()
    {
        if (hintRequested == null)
            throw new InvalidOperationException("Hint callback is missing.");
        hintRequested.Invoke();
    }

    private static Button CreateButton(RectTransform parent, Font font, out Text label)
    {
        var buttonObject = new GameObject("HintButton", typeof(RectTransform), typeof(Image));
        buttonObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-8f, 0f);
        rect.sizeDelta = new Vector2(230f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = AccentColor;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        label = CreateText(
            rect,
            "Label",
            font,
            Vector2.zero,
            Vector2.zero,
            17,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        label.color = new Color(0.02f, 0.05f, 0.06f, 1f);
        return button;
    }

    private static Text CreateText(
        RectTransform parent,
        string name,
        Font font,
        Vector2 position,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = alignment;
        text.alignByGeometry = true;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
