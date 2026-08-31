using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleImageGallery : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0.005f, 0.01f, 0.02f, 0.88f);
    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.085f, 1f);
    private static readonly Color CardColor = new Color(0.065f, 0.085f, 0.115f, 1f);
    private static readonly Color CompletedCardColor = new Color(0.06f, 0.2f, 0.18f, 1f);
    private static readonly Color AccentColor = new Color(0.12f, 0.72f, 0.64f, 1f);
    private static readonly Color ProgressColor = new Color(0.88f, 0.67f, 0.2f, 1f);
    private static readonly Color LockedColor = new Color(0.34f, 0.4f, 0.48f, 1f);
    private static readonly Color MutedColor = new Color(0.7f, 0.78f, 0.86f, 1f);

    private PuzzleConfig config;
    private PuzzleProgressData progress;
    private Action opened;
    private GameObject launcher;
    private RectTransform overlay;
    private RectTransform content;
    private Text summary;

    public bool IsOpen => overlay != null && overlay.gameObject.activeSelf;

    public static PuzzleImageGallery Show(
        RectTransform parent,
        PuzzleConfig config,
        PuzzleProgressData progress,
        Action opened)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        if (opened == null) throw new ArgumentNullException(nameof(opened));
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException(
                "Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        var host = new GameObject("ImageGallery", typeof(RectTransform));
        host.layer = parent.gameObject.layer;
        RectTransform hostRect = (RectTransform)host.transform;
        hostRect.SetParent(parent, false);
        Stretch(hostRect);

        PuzzleImageGallery gallery = host.AddComponent<PuzzleImageGallery>();
        gallery.config = config;
        gallery.progress = progress;
        gallery.opened = opened;
        gallery.Build(font, hostRect);
        gallery.overlay.gameObject.SetActive(false);
        return gallery;
    }

    public void Open()
    {
        if (IsOpen)
            throw new InvalidOperationException("Image gallery is already open.");
        opened.Invoke();
        launcher.SetActive(false);
        overlay.gameObject.SetActive(true);
        ((RectTransform)transform).SetAsLastSibling();
        RebuildRows();
    }

    public void ClosePanel()
    {
        if (!IsOpen)
            throw new InvalidOperationException("Image gallery is not open.");
        ClearRows();
        overlay.gameObject.SetActive(false);
        launcher.SetActive(true);
    }

    public void SetLauncherVisible(bool visible)
    {
        if (launcher == null)
            throw new InvalidOperationException("Image gallery launcher is missing.");
        launcher.SetActive(visible);
    }

    public void RaiseToTop()
    {
        ((RectTransform)transform).SetAsLastSibling();
    }

    public void Close()
    {
        config = null;
        progress = null;
        opened = null;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(gameObject);
            return;
        }
#endif
        Destroy(gameObject);
    }

    private void Build(Font font, RectTransform parent)
    {
        launcher = CreateButton(
            parent,
            "OpenGallery",
            font,
            "IMAGENS",
            new Color(0.18f, 0.23f, 0.3f, 1f),
            Open).gameObject;
        RectTransform launcherRect = (RectTransform)launcher.transform;
        launcherRect.anchorMin = launcherRect.anchorMax = Vector2.one;
        launcherRect.pivot = Vector2.one;
        launcherRect.anchoredPosition = new Vector2(-240f, -18f);
        launcherRect.sizeDelta = new Vector2(210f, 52f);

        var overlayObject = new GameObject(
            "Overlay",
            typeof(RectTransform),
            typeof(Image));
        overlayObject.layer = parent.gameObject.layer;
        overlay = (RectTransform)overlayObject.transform;
        overlay.SetParent(parent, false);
        Stretch(overlay);
        overlayObject.GetComponent<Image>().color = BackdropColor;

        var panelObject = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(Image));
        panelObject.layer = parent.gameObject.layer;
        RectTransform panel = (RectTransform)panelObject.transform;
        panel.SetParent(overlay, false);
        panel.anchorMin = Vector2.one * 0.5f;
        panel.anchorMax = Vector2.one * 0.5f;
        panel.pivot = Vector2.one * 0.5f;
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(1160f, 790f);
        panelObject.GetComponent<Image>().color = PanelColor;

        CreateText(
            panel,
            "Title",
            font,
            "IMAGENS",
            34,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft,
            new Vector2(46f, -86f),
            new Vector2(-160f, -30f));
        summary = CreateText(
            panel,
            "Summary",
            font,
            string.Empty,
            16,
            FontStyle.Normal,
            MutedColor,
            TextAnchor.MiddleLeft,
            new Vector2(48f, -116f),
            new Vector2(-48f, -84f));

        Button close = CreateButton(
            panel,
            "Close",
            font,
            "FECHAR",
            new Color(0.18f, 0.23f, 0.3f, 1f),
            ClosePanel);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-34f, -30f);
        closeRect.sizeDelta = new Vector2(120f, 48f);

        CreateGrid(panel);
    }

    private void CreateGrid(RectTransform panel)
    {
        var viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        viewportObject.layer = panel.gameObject.layer;
        RectTransform viewport = (RectTransform)viewportObject.transform;
        viewport.SetParent(panel, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(46f, 40f);
        viewport.offsetMax = new Vector2(-46f, -198f);
        viewportObject.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.9f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = true;

        content = CreateRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(526f, 122f);
        grid.spacing = new Vector2(12f, 12f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38f;
    }

    private void RebuildRows()
    {
        ClearRows();

        int unlockedCount = 0;
        int completedCount = 0;
        int total = 0;
        for (int collectionIndex = 0;
             collectionIndex < config.Collections.Count;
             collectionIndex++)
        {
            PuzzleCollectionDefinition collection = config.Collections[collectionIndex];
            for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
            {
                PuzzleImageDefinition image = collection.Images[imageIndex];
                total++;
                bool unlocked = progress.IsImageUnlocked(image.Id);
                if (unlocked) unlockedCount++;
                bool completed = unlocked && HasCompletions(image);
                if (completed) completedCount++;
                CreateEntry(image, unlocked, completed);
            }
        }

        summary.text =
            $"{unlockedCount:N0} / {total:N0} DESBLOQUEADAS   â€¢   " +
            $"{completedCount:N0} / {total:N0} CONCLUÃDAS";
    }

    private bool HasCompletions(PuzzleImageDefinition image)
    {
        for (int i = 0; i < config.DifficultyProfiles.Count; i++)
        {
            if (progress.TryGetBestResult(
                    image.Id,
                    config.DifficultyProfiles[i].Difficulty,
                    out PuzzleBestResultRecord result) &&
                result.Completions > 0)
                return true;
        }
        return false;
    }

    private void CreateEntry(PuzzleImageDefinition image, bool unlocked, bool completed)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cardObject = new GameObject(
            image.Id,
            typeof(RectTransform),
            typeof(Image));
        cardObject.layer = content.gameObject.layer;
        RectTransform card = (RectTransform)cardObject.transform;
        card.SetParent(content, false);
        cardObject.GetComponent<Image>().color = completed
            ? CompletedCardColor
            : unlocked ? CardColor : new Color(0.045f, 0.058f, 0.08f, 1f);

        RectTransform frame = CreateRect(card, "ThumbFrame");
        frame.anchorMin = new Vector2(0f, 0.5f);
        frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.anchoredPosition = new Vector2(12f, 0f);
        frame.sizeDelta = new Vector2(98f, 98f);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.012f, 0.017f, 0.025f, 1f);
        frameImage.raycastTarget = false;

        if (unlocked)
        {
            Texture2D texture = config.LoadImage(image);
            RectTransform thumbRect = CreateRect(frame, "Thumb");
            thumbRect.anchorMin = Vector2.zero;
            thumbRect.anchorMax = Vector2.one;
            thumbRect.pivot = Vector2.one * 0.5f;
            thumbRect.offsetMin = new Vector2(3f, 3f);
            thumbRect.offsetMax = new Vector2(-3f, -3f);
            RawImage thumbnail = thumbRect.gameObject.AddComponent<RawImage>();
            thumbnail.texture = texture;
            thumbnail.raycastTarget = false;
        }
        else
        {
            CreateText(
                frame,
                "LockLabel",
                font,
                "BLOQUEADA",
                13,
                FontStyle.Bold,
                LockedColor,
                TextAnchor.MiddleCenter,
                new Vector2(6f, -66f),
                new Vector2(-6f, -32f));
        }

        Color stateColor = completed
            ? AccentColor
            : unlocked ? ProgressColor : LockedColor;
        string state = completed
            ? "CONCLUÃDA"
            : unlocked ? "DISPONÃVEL" : $"BLOQUEADA â€¢ REQUER {image.RequiredUniqueCompletions}";

        CreateText(
            card,
            "Title",
            font,
            unlocked ? image.DisplayName : "IMAGEM SECRETA",
            20,
            FontStyle.Bold,
            unlocked ? Color.white : LockedColor,
            TextAnchor.MiddleLeft,
            new Vector2(124f, -44f),
            new Vector2(-20f, -12f));
        CreateText(
            card,
            "State",
            font,
            state,
            14,
            FontStyle.Bold,
            stateColor,
            TextAnchor.MiddleLeft,
            new Vector2(124f, -70f),
            new Vector2(-20f, -46f));
        CreateText(
            card,
            "BestResult",
            font,
            BuildBestResultLine(image, unlocked),
            14,
            FontStyle.Normal,
            MutedColor,
            TextAnchor.MiddleLeft,
            new Vector2(124f, -104f),
            new Vector2(-20f, -72f));
    }

    private string BuildBestResultLine(PuzzleImageDefinition image, bool unlocked)
    {
        if (!unlocked) return "Continue concluindo quebra-cabeÃ§as para desbloquear.";

        int bestScore = 0;
        float bestSeconds = float.MaxValue;
        PuzzleMedal bestMedal = PuzzleMedal.Bronze;
        int completions = 0;
        bool hasResult = false;
        for (int i = 0; i < config.DifficultyProfiles.Count; i++)
        {
            if (!progress.TryGetBestResult(
                    image.Id,
                    config.DifficultyProfiles[i].Difficulty,
                    out PuzzleBestResultRecord result))
                continue;
            hasResult = true;
            completions += result.Completions;
            if (result.BestScore > bestScore) bestScore = result.BestScore;
            if (result.BestSeconds < bestSeconds)
            {
                bestSeconds = result.BestSeconds;
                bestMedal = result.BestMedal;
            }
        }

        if (!hasResult) return "DisponÃ­vel para a primeira montagem.";
        return $"CONCLUÃDA {completions:N0}Ã—   â€¢   MELHOR: {bestScore:N0} pts   â€¢   " +
               $"{bestSeconds:F1}s   â€¢   {PuzzleMedalCalculator.Label(bestMedal)}";
    }

    private void ClearRows()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child);
            else Destroy(child);
#else
            Destroy(child);
#endif
        }
    }

    private static Button CreateButton(
        RectTransform parent,
        string name,
        Font font,
        string label,
        Color color,
        UnityEngine.Events.UnityAction clicked)
    {
        var buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(clicked);

        Text text = CreateText(
            rect,
            "Label",
            font,
            label,
            16,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            new Vector2(8f, -38f),
            new Vector2(-8f, -14f));
        return button;
    }

    private static Text CreateText(
        RectTransform parent,
        string name,
        Font font,
        string value,
        int fontSize,
        FontStyle style,
        Color color,
        TextAnchor alignment,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (font == null) throw new ArgumentNullException(nameof(font));
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.alignByGeometry = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(RectTransform parent, string name)
    {
        var value = new GameObject(name, typeof(RectTransform));
        value.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)value.transform;
        rect.SetParent(parent, false);
        return rect;
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
