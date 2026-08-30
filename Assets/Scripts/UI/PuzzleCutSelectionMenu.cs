using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleCutSelectionMenu : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 1f);
    private static readonly Color CardColor = new Color(0.09f, 0.12f, 0.17f, 1f);
    private static readonly Color CardSelectedColor = new Color(0.12f, 0.28f, 0.31f, 1f);
    private static readonly Color AccentColor = new Color(0.25f, 0.92f, 0.78f, 1f);
    private static readonly Color MutedTextColor = new Color(0.66f, 0.72f, 0.79f, 1f);

    private readonly Dictionary<PuzzleCutStyle, Toggle> styleOptions =
        new Dictionary<PuzzleCutStyle, Toggle>();

    private Func<PuzzleDifficultyProfile, PuzzleCutStyle, bool> confirmed;
    private PuzzleConfig config;
    private PuzzleProgressData progress;
    private PuzzleDifficultyProfile selectedDifficulty;
    private PuzzleCutStyle selectedStyle;
    private Button confirmButton;
    private Font font;
    private Texture2D previewTexture;
    private ProceduralPuzzlePreviewGraphic previewGraphic;
    private Text previewTitle;
    private GameObject noPreviewMessage;
    private Text difficultySummary;

    public static PuzzleCutSelectionMenu Show(
        RectTransform parent,
        IReadOnlyList<PuzzleDifficultyProfile> difficulties,
        PuzzleDifficultyProfile initialDifficulty,
        PuzzleCutStyle initialStyle,
        Texture2D previewTexture,
        PuzzleConfig config,
        PuzzleProgressData progress,
        Func<PuzzleDifficultyProfile, PuzzleCutStyle, bool> onConfirmed)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (difficulties == null || difficulties.Count == 0)
            throw new ArgumentException("At least one difficulty profile is required.", nameof(difficulties));
        if (initialDifficulty == null) throw new ArgumentNullException(nameof(initialDifficulty));
        bool containsInitialDifficulty = false;
        var profileList = new PuzzleDifficultyProfile[difficulties.Count];
        var difficultyValues = new HashSet<PuzzleDifficulty>();
        for (int i = 0; i < difficulties.Count; i++)
        {
            PuzzleDifficultyProfile profile = difficulties[i];
            if (profile == null)
                throw new ArgumentException($"Difficulty profile {i} is missing.", nameof(difficulties));
            if (!profile.TryValidate(out string profileError))
                throw new ArgumentException(
                    $"Difficulty profile '{profile.name}' is invalid: {profileError}.",
                    nameof(difficulties));
            if (!difficultyValues.Add(profile.Difficulty))
                throw new ArgumentException(
                    $"Difficulty {profile.Difficulty} is duplicated.",
                    nameof(difficulties));

            profileList[i] = profile;
            if (profile == initialDifficulty) containsInitialDifficulty = true;
        }

        if (!containsInitialDifficulty)
            throw new ArgumentException("Initial difficulty is not part of the configured profiles.");
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), initialStyle))
            throw new ArgumentOutOfRangeException(nameof(initialStyle));
        if (!initialDifficulty.AllowsStyle(initialStyle))
            throw new ArgumentException(
                $"Initial style {initialStyle} is not allowed by {initialDifficulty.DisplayName}.");
        if (previewTexture == null) throw new ArgumentNullException(nameof(previewTexture));
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        progress.Validate(config);
        if (!progress.IsDifficultyUnlocked(config, initialDifficulty))
            throw new ArgumentException(
                $"Initial difficulty {initialDifficulty.DisplayName} is locked.");
        if (!progress.IsStyleUnlocked(config, initialStyle))
            throw new ArgumentException($"Initial style {initialStyle} is locked.");
        if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

        GameObject root = CreateUiObject("CutStyleSelection", parent);
        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);
        Image overlay = root.AddComponent<Image>();
        overlay.color = OverlayColor;
        overlay.raycastTarget = true;

        PuzzleCutSelectionMenu menu = root.AddComponent<PuzzleCutSelectionMenu>();
        menu.Initialize(
            profileList,
            initialDifficulty,
            initialStyle,
            previewTexture,
            config,
            progress,
            onConfirmed);
        rootRect.SetAsLastSibling();
        return menu;
    }

    public void Close()
    {
        confirmed = null;
        Destroy(gameObject);
    }

    public void ShowError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Cut selection error is required.", nameof(message));
        if (difficultySummary == null)
            throw new InvalidOperationException("Difficulty summary is missing.");
        difficultySummary.color = new Color(1f, 0.42f, 0.42f, 1f);
        difficultySummary.text = "ERRO: " + message;
    }

    private void Initialize(
        PuzzleDifficultyProfile[] difficulties,
        PuzzleDifficultyProfile initialDifficulty,
        PuzzleCutStyle initialStyle,
        Texture2D previewTexture,
        PuzzleConfig config,
        PuzzleProgressData progress,
        Func<PuzzleDifficultyProfile, PuzzleCutStyle, bool> onConfirmed)
    {
        selectedStyle = initialStyle;
        confirmed = onConfirmed;
        this.previewTexture = previewTexture;
        this.config = config;
        this.progress = progress;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException("Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        RectTransform panel = CreatePanel();
        CreateText(
            "Title",
            panel,
            "CONFIGURE SUA PARTIDA",
            32,
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
            "Escolha a dificuldade e depois selecione um formato.",
            18,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -81f),
            new Vector2(1000f, 32f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));

        Toggle initialDifficultyToggle = CreateDifficultySelector(
            panel,
            difficulties,
            initialDifficulty);

        RectTransform gridRect = CreateRect("Options", panel);
        SetRect(
            gridRect,
            new Vector2(40f, -210f),
            new Vector2(460f, 460f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(218f, 56f);
        grid.spacing = new Vector2(14f, 10f);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        CreatePreviewPanel(panel);

        ToggleGroup group = gridRect.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = true;
        Toggle initialToggle = null;
        foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
        {
            Toggle toggle = CreateOption(gridRect, group, style);
            if (style == initialStyle) initialToggle = toggle;
        }

        if (initialToggle == null)
            throw new InvalidOperationException($"No UI option exists for cut style {initialStyle}.");

        initialDifficultyToggle.SetIsOnWithoutNotify(true);
        initialDifficultyToggle.graphic.canvasRenderer.SetAlpha(1f);
        initialDifficultyToggle.GetComponent<Image>().color = CardSelectedColor;
        ApplyDifficulty(initialDifficulty, initialStyle);
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
            new Vector2(1120f, 840f),
            Vector2.one * 0.5f,
            Vector2.one * 0.5f);
        Image image = panelObject.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;
        return panel;
    }

    private Toggle CreateDifficultySelector(
        RectTransform parent,
        PuzzleDifficultyProfile[] difficulties,
        PuzzleDifficultyProfile initialDifficulty)
    {
        RectTransform selectorRect = CreateRect("Difficulties", parent);
        SetRect(
            selectorRect,
            new Vector2(0f, -105f),
            new Vector2(1000f, 56f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        GridLayoutGroup grid = selectorRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(324f, 54f);
        grid.spacing = new Vector2(14f, 0f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;

        RectTransform summaryRect = CreateText(
            "DifficultySummary",
            parent,
            string.Empty,
            16,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -165f),
            new Vector2(1000f, 40f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        difficultySummary = summaryRect.GetComponent<Text>();

        ToggleGroup group = selectorRect.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = false;
        Toggle initialToggle = null;
        for (int i = 0; i < difficulties.Length; i++)
        {
            PuzzleDifficultyProfile profile = difficulties[i];
            Toggle toggle = CreateDifficultyOption(selectorRect, group, profile);
            if (profile == initialDifficulty) initialToggle = toggle;
        }

        if (initialToggle == null)
            throw new InvalidOperationException("Initial difficulty toggle was not created.");
        return initialToggle;
    }

    private Toggle CreateDifficultyOption(
        RectTransform parent,
        ToggleGroup group,
        PuzzleDifficultyProfile profile)
    {
        GameObject cardObject = CreateUiObject(profile.Difficulty.ToString(), parent);
        Image background = cardObject.AddComponent<Image>();
        background.color = CardColor;

        Toggle toggle = cardObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.group = group;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.colors = OptionColors();

        RectTransform box = CreateRect("Checkbox", cardObject.transform);
        SetRect(
            box,
            new Vector2(16f, 0f),
            new Vector2(22f, 22f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f));
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(0.025f, 0.035f, 0.05f, 1f);
        boxImage.raycastTarget = false;

        RectTransform check = CreateRect("Checkmark", box);
        SetRect(check, Vector2.zero, new Vector2(13f, 13f), Vector2.one * 0.5f, Vector2.one * 0.5f);
        Image checkImage = check.gameObject.AddComponent<Image>();
        checkImage.color = AccentColor;
        checkImage.raycastTarget = false;
        checkImage.canvasRenderer.SetAlpha(0f);
        toggle.graphic = checkImage;

        bool unlocked = progress.IsDifficultyUnlocked(config, profile);
        string label = unlocked
            ? profile.DisplayName
            : $"{profile.DisplayName} • BLOQUEADO {profile.RequiredUniqueCompletions}";
        CreateText(
            "Label",
            cardObject.transform,
            label,
            unlocked ? 18 : 13,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft,
            new Vector2(50f, 0f),
            new Vector2(255f, 40f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f));

        toggle.onValueChanged.AddListener(isOn =>
        {
            background.color = isOn ? CardSelectedColor : CardColor;
            if (isOn) ApplyDifficulty(profile, profile.DefaultCutStyle);
        });
        toggle.interactable = unlocked;
        toggle.SetIsOnWithoutNotify(false);
        return toggle;
    }

    private Toggle CreateOption(
        RectTransform parent,
        ToggleGroup group,
        PuzzleCutStyle style)
    {
        GameObject cardObject = CreateUiObject(style.ToString(), parent);
        Image background = cardObject.AddComponent<Image>();
        background.color = CardColor;

        Toggle toggle = cardObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.group = group;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.colors = OptionColors();

        RectTransform box = CreateRect("Checkbox", cardObject.transform);
        SetRect(
            box,
            new Vector2(14f, 0f),
            new Vector2(24f, 24f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f));
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

        bool unlocked = progress.IsStyleUnlocked(config, style);
        string label = unlocked
            ? LabelFor(style)
            : $"{LabelFor(style)} • BLOQUEADO {config.GetStyleRequirement(style)}";
        CreateText(
            "Label",
            cardObject.transform,
            label,
            style == PuzzleCutStyle.FullyRandom ? 12 : unlocked ? 16 : 12,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft,
            new Vector2(46f, 0f),
            new Vector2(168f, 40f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f));

        PuzzleCutStyle optionStyle = style;
        toggle.onValueChanged.AddListener(isOn =>
        {
            background.color = isOn ? CardSelectedColor : CardColor;
            if (!isOn) return;

            selectedStyle = optionStyle;
            UpdatePreview(optionStyle);
        });
        toggle.interactable = unlocked;
        toggle.SetIsOnWithoutNotify(false);
        styleOptions.Add(style, toggle);
        return toggle;
    }

    private void ApplyDifficulty(
        PuzzleDifficultyProfile profile,
        PuzzleCutStyle preferredStyle)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (!profile.TryValidate(out string error))
            throw new InvalidOperationException(
                $"Difficulty profile '{profile.name}' is invalid: {error}.");
        if (difficultySummary == null)
            throw new InvalidOperationException("Difficulty summary is missing.");

        selectedDifficulty = profile;
        difficultySummary.color = MutedTextColor;
        difficultySummary.text = $"{profile.Description}  {profile.BuildRuleSummary()}";

        foreach (KeyValuePair<PuzzleCutStyle, Toggle> option in styleOptions)
        {
            bool allowed = profile.AllowsStyle(option.Key);
            bool unlocked = progress.IsStyleUnlocked(config, option.Key);
            option.Value.SetIsOnWithoutNotify(false);
            option.Value.GetComponent<Image>().color = CardColor;
            option.Value.gameObject.SetActive(allowed);
            option.Value.interactable = unlocked;
        }

        if (!profile.AllowsStyle(preferredStyle))
            throw new InvalidOperationException(
                $"Cut style {preferredStyle} is not allowed by {profile.DisplayName}.");
        if (!progress.IsStyleUnlocked(config, preferredStyle))
            throw new InvalidOperationException($"Cut style {preferredStyle} is locked.");
        if (!styleOptions.TryGetValue(preferredStyle, out Toggle targetToggle))
            throw new InvalidOperationException(
                $"No UI option exists for cut style {preferredStyle}.");

        targetToggle.SetIsOnWithoutNotify(true);
        targetToggle.graphic.canvasRenderer.SetAlpha(1f);
        targetToggle.onValueChanged.Invoke(true);
    }

    private void CreatePreviewPanel(RectTransform parent)
    {
        GameObject panelObject = CreateUiObject("PreviewPanel", parent);
        RectTransform panel = (RectTransform)panelObject.transform;
        SetRect(
            panel,
            new Vector2(-40f, -210f),
            new Vector2(500f, 460f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.047f, 0.068f, 1f);
        panelImage.raycastTarget = false;

        RectTransform titleRect = CreateText(
            "PreviewTitle",
            panel,
            string.Empty,
            24,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -26f),
            new Vector2(450f, 38f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        previewTitle = titleRect.GetComponent<Text>();

        GameObject frameObject = CreateUiObject("PreviewFrame", panel);
        RectTransform frame = (RectTransform)frameObject.transform;
        SetRect(
            frame,
            new Vector2(0f, -75f),
            new Vector2(440f, 310f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        Image frameImage = frameObject.AddComponent<Image>();
        frameImage.color = new Color(0.012f, 0.017f, 0.025f, 1f);
        frameImage.raycastTarget = false;

        RectTransform previewRect = CreateRect("Preview", frame);
        Stretch(previewRect);
        previewRect.offsetMin = new Vector2(18f, 18f);
        previewRect.offsetMax = new Vector2(-18f, -18f);
        previewGraphic = previewRect.gameObject.AddComponent<ProceduralPuzzlePreviewGraphic>();
        previewGraphic.gameObject.SetActive(false);

        RectTransform noPreviewRect = CreateText(
            "NoPreview",
            frame,
            "SEM PREVIEW\n\nEste modo combina formatos diferentes\nem cada aresta a cada partida.",
            18,
            FontStyle.Italic,
            MutedTextColor,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero);
        Stretch(noPreviewRect);
        noPreviewRect.offsetMin = new Vector2(30f, 30f);
        noPreviewRect.offsetMax = new Vector2(-30f, -30f);
        noPreviewMessage = noPreviewRect.gameObject;

        CreateText(
            "PreviewCaption",
            panel,
            "A prévia usa a imagem e a geometria da partida.",
            16,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 24f),
            new Vector2(450f, 34f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f));
    }

    private void UpdatePreview(PuzzleCutStyle style)
    {
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (selectedDifficulty == null || previewGraphic == null ||
            previewTitle == null || noPreviewMessage == null)
            throw new InvalidOperationException("Cut selection preview panel is incomplete.");

        previewTitle.text = LabelFor(style);
        bool hasPreview = style != PuzzleCutStyle.FullyRandom;
        previewGraphic.gameObject.SetActive(hasPreview);
        noPreviewMessage.SetActive(!hasPreview);
        if (!hasPreview) return;

        ProceduralPuzzleGeometry[,] board = ProceduralPuzzleGenerator.Create(
            3,
            style,
            selectedDifficulty.CutDepth,
            3109 + (int)style * 97);
        previewGraphic.Configure(board[1, 1], previewTexture);
    }

    private Button CreateButton(RectTransform parent)
    {
        GameObject buttonObject = CreateUiObject("Play", parent);
        RectTransform rect = (RectTransform)buttonObject.transform;
        SetRect(
            rect,
            new Vector2(0f, 40f),
            new Vector2(300f, 58f),
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
        if (selectedDifficulty == null)
            throw new InvalidOperationException("Difficulty selection is missing.");
        if (!progress.IsDifficultyUnlocked(config, selectedDifficulty))
            throw new InvalidOperationException(
                $"Difficulty {selectedDifficulty.DisplayName} is locked.");
        if (!progress.IsStyleUnlocked(config, selectedStyle))
            throw new InvalidOperationException($"Cut style {selectedStyle} is locked.");

        if (confirmed(selectedDifficulty, selectedStyle)) Close();
        else confirmButton.interactable = true;
    }

    private static ColorBlock OptionColors() => new ColorBlock
    {
        normalColor = Color.white,
        highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f),
        pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f),
        selectedColor = Color.white,
        disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.65f),
        colorMultiplier = 1f,
        fadeDuration = 0.1f,
    };

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
        text.alignByGeometry = true;
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
