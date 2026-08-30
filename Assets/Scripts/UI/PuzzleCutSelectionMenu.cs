using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleCutSelectionMenu : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 1f);
    private static readonly Color CardColor = new Color(0.09f, 0.12f, 0.17f, 1f);
    private static readonly Color CardHoverColor = new Color(0.13f, 0.18f, 0.25f, 1f);
    private static readonly Color CardSelectedColor = new Color(0.12f, 0.28f, 0.31f, 1f);
    private static readonly Color AccentColor = new Color(0.25f, 0.92f, 0.78f, 1f);
    private static readonly Color MutedTextColor = new Color(0.66f, 0.72f, 0.79f, 1f);

    private Func<PuzzleCutStyle, bool> confirmed;
    private PuzzleCutStyle selectedStyle;
    private Button confirmButton;
    private Font font;

    public static PuzzleCutSelectionMenu Show(
        RectTransform parent,
        PuzzleCutStyle initialStyle,
        float cutDepth,
        Func<PuzzleCutStyle, bool> onConfirmed)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), initialStyle))
            throw new ArgumentOutOfRangeException(nameof(initialStyle));
        if (!float.IsFinite(cutDepth) || cutDepth < 0.08f || cutDepth > 0.3f)
            throw new ArgumentOutOfRangeException(nameof(cutDepth));
        if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

        GameObject root = CreateUiObject("CutStyleSelection", parent);
        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);
        Image overlay = root.AddComponent<Image>();
        overlay.color = OverlayColor;
        overlay.raycastTarget = true;

        PuzzleCutSelectionMenu menu = root.AddComponent<PuzzleCutSelectionMenu>();
        menu.Initialize(initialStyle, cutDepth, onConfirmed);
        rootRect.SetAsLastSibling();
        return menu;
    }

    public void Close()
    {
        confirmed = null;
        Destroy(gameObject);
    }

    private void Initialize(
        PuzzleCutStyle initialStyle,
        float cutDepth,
        Func<PuzzleCutStyle, bool> onConfirmed)
    {
        selectedStyle = initialStyle;
        confirmed = onConfirmed;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException("Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        RectTransform panel = CreatePanel();
        CreateText(
            "Title",
            panel,
            "ESCOLHA O FORMATO DAS PEÇAS",
            30,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -35f),
            new Vector2(1000f, 48f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        CreateText(
            "Subtitle",
            panel,
            "Marque uma opção. As miniaturas usam a mesma geometria procedural do tabuleiro.",
            17,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -81f),
            new Vector2(1000f, 32f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));

        RectTransform gridRect = CreateRect("Options", panel);
        SetRect(
            gridRect,
            new Vector2(0f, -125f),
            new Vector2(1010f, 575f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(240f, 132f);
        grid.spacing = new Vector2(16f, 15f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        ToggleGroup group = gridRect.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = true;
        Toggle initialToggle = null;
        foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
        {
            Toggle toggle = CreateOption(gridRect, group, style, cutDepth);
            if (style == initialStyle) initialToggle = toggle;
        }

        if (initialToggle == null)
            throw new InvalidOperationException($"No UI option exists for cut style {initialStyle}.");
        initialToggle.SetIsOnWithoutNotify(true);
        initialToggle.graphic.canvasRenderer.SetAlpha(1f);
        group.allowSwitchOff = false;

        confirmButton = CreateButton(panel);
        confirmButton.onClick.AddListener(ConfirmSelection);
    }

    private RectTransform CreatePanel()
    {
        GameObject panelObject = CreateUiObject("Panel", transform);
        RectTransform panel = (RectTransform)panelObject.transform;
        SetRect(
            panel,
            Vector2.zero,
            new Vector2(1100f, 860f),
            Vector2.one * 0.5f,
            Vector2.one * 0.5f);
        Image image = panelObject.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;
        return panel;
    }

    private Toggle CreateOption(
        RectTransform parent,
        ToggleGroup group,
        PuzzleCutStyle style,
        float cutDepth)
    {
        GameObject cardObject = CreateUiObject(style.ToString(), parent);
        Image background = cardObject.AddComponent<Image>();
        background.color = CardColor;

        Toggle toggle = cardObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.group = group;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.colors = new ColorBlock
        {
            normalColor = CardColor,
            highlightedColor = CardHoverColor,
            pressedColor = CardSelectedColor,
            selectedColor = CardSelectedColor,
            disabledColor = new Color(0.06f, 0.07f, 0.09f, 0.6f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        RectTransform box = CreateRect("Checkbox", cardObject.transform);
        SetRect(
            box,
            new Vector2(13f, -13f),
            new Vector2(24f, 24f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(0.025f, 0.035f, 0.05f, 1f);
        boxImage.raycastTarget = false;

        RectTransform check = CreateRect("Checkmark", box);
        SetRect(check, Vector2.zero, new Vector2(14f, 14f), Vector2.one * 0.5f, Vector2.one * 0.5f);
        Image checkImage = check.gameObject.AddComponent<Image>();
        checkImage.color = AccentColor;
        checkImage.raycastTarget = false;
        checkImage.canvasRenderer.SetAlpha(0f);
        toggle.graphic = checkImage;

        CreateText(
            "Label",
            cardObject.transform,
            LabelFor(style),
            15,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft,
            new Vector2(47f, -9f),
            new Vector2(180f, 30f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));

        if (style == PuzzleCutStyle.FullyRandom)
        {
            CreateText(
                "NoPreview",
                cardObject.transform,
                "SEM PREVIEW\ncombina arestas a cada partida",
                14,
                FontStyle.Italic,
                MutedTextColor,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -25f),
                new Vector2(210f, 64f),
                Vector2.one * 0.5f,
                Vector2.one * 0.5f);
        }
        else
        {
            RectTransform previewRect = CreateRect("Preview", cardObject.transform);
            SetRect(
                previewRect,
                new Vector2(0f, -22f),
                new Vector2(128f, 76f),
                Vector2.one * 0.5f,
                Vector2.one * 0.5f);
            ProceduralPuzzlePreviewGraphic preview =
                previewRect.gameObject.AddComponent<ProceduralPuzzlePreviewGraphic>();
            ProceduralPuzzleGeometry[,] board = ProceduralPuzzleGenerator.Create(
                3,
                style,
                cutDepth,
                3109 + (int)style * 97);
            Color previewColor = Color.HSVToRGB(((int)style * 0.083f + 0.42f) % 1f, 0.5f, 1f);
            preview.Configure(board[1, 1], previewColor);
        }

        PuzzleCutStyle optionStyle = style;
        toggle.onValueChanged.AddListener(isOn =>
        {
            if (isOn) selectedStyle = optionStyle;
        });
        toggle.SetIsOnWithoutNotify(false);
        return toggle;
    }

    private Button CreateButton(RectTransform parent)
    {
        GameObject buttonObject = CreateUiObject("Play", parent);
        RectTransform rect = (RectTransform)buttonObject.transform;
        SetRect(
            rect,
            new Vector2(0f, 35f),
            new Vector2(280f, 58f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f));
        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.colors = new ColorBlock
        {
            normalColor = new Color(0.12f, 0.65f, 0.56f, 1f),
            highlightedColor = new Color(0.18f, 0.82f, 0.7f, 1f),
            pressedColor = new Color(0.08f, 0.48f, 0.42f, 1f),
            selectedColor = new Color(0.18f, 0.82f, 0.7f, 1f),
            disabledColor = new Color(0.2f, 0.28f, 0.3f, 0.65f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        RectTransform label = CreateText(
            "Label",
            buttonObject.transform,
            "JOGAR COM ESTE FORMATO",
            18,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero);
        Stretch(label);
        return button;
    }

    private void ConfirmSelection()
    {
        if (confirmed == null)
            throw new InvalidOperationException("Cut selection confirmation callback is missing.");

        confirmButton.interactable = false;
        if (confirmed(selectedStyle)) Close();
        else confirmButton.interactable = true;
    }

    private RectTransform CreateText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        GameObject textObject = CreateUiObject(name, parent);
        RectTransform rect = (RectTransform)textObject.transform;
        SetRect(rect, position, size, anchor, pivot);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return rect;
    }

    private static string LabelFor(PuzzleCutStyle style)
    {
        switch (style)
        {
            case PuzzleCutStyle.Square: return "QUADRADO";
            case PuzzleCutStyle.Round: return "REDONDO";
            case PuzzleCutStyle.Ellipse: return "ELIPSE";
            case PuzzleCutStyle.Rectangle: return "RETÂNGULO";
            case PuzzleCutStyle.Hexagon: return "HEXÁGONO";
            case PuzzleCutStyle.Triangle: return "TRIÂNGULO";
            case PuzzleCutStyle.Diamond: return "DIAMANTE";
            case PuzzleCutStyle.Wave: return "ONDULADO";
            case PuzzleCutStyle.Zigzag: return "ZIGUE-ZAGUE";
            case PuzzleCutStyle.DoubleLobe: return "LÓBULOS";
            case PuzzleCutStyle.Organic: return "ORGÂNICO";
            case PuzzleCutStyle.Procedural: return "PROCEDURAL";
            case PuzzleCutStyle.FullyRandom: return "TOTALMENTE ALEATÓRIO";
            default: throw new ArgumentOutOfRangeException(nameof(style));
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static RectTransform CreateRect(string name, Transform parent) =>
        (RectTransform)CreateUiObject(name, parent).transform;

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
