using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PuzzleAchievementFilter
{
    All,
    Unlocked,
    InProgress,
    Locked,
}

public sealed class PuzzleAchievementCenter : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0.005f, 0.01f, 0.02f, 0.88f);
    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.085f, 1f);
    private static readonly Color CardColor = new Color(0.065f, 0.085f, 0.115f, 1f);
    private static readonly Color AccentColor = new Color(0.12f, 0.72f, 0.64f, 1f);
    private static readonly Color ProgressColor = new Color(0.88f, 0.67f, 0.2f, 1f);
    private static readonly Color LockedColor = new Color(0.34f, 0.4f, 0.48f, 1f);

    private readonly Dictionary<PuzzleAchievementFilter, Image> filterBackgrounds =
        new Dictionary<PuzzleAchievementFilter, Image>();

    private Func<IReadOnlyList<PuzzleAchievementCatalogEntry>> loadCatalog;
    private Action opened;
    private Font font;
    private PuzzleAchievementVisuals visuals;
    private RectTransform overlay;
    private GameObject launcher;
    private RectTransform content;
    private Text summary;
    private IReadOnlyList<PuzzleAchievementCatalogEntry> catalog;
    private PuzzleAchievementFilter currentFilter;

    public bool IsOpen => overlay != null && overlay.gameObject.activeSelf;

    public PuzzleAchievementFilter CurrentFilter => currentFilter;

    public int VisibleEntryCount
    {
        get
        {
            if (content == null)
                throw new InvalidOperationException("Achievement center content is not initialized.");
            return content.childCount;
        }
    }

    public static PuzzleAchievementCenter Show(
        RectTransform parent,
        PuzzleAchievementVisuals visuals,
        Func<IReadOnlyList<PuzzleAchievementCatalogEntry>> loadCatalog,
        Action opened)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (visuals == null) throw new ArgumentNullException(nameof(visuals));
        if (!visuals.TryValidate(out string visualsError))
            throw new InvalidOperationException($"Invalid achievement visuals: {visualsError}.");
        if (loadCatalog == null) throw new ArgumentNullException(nameof(loadCatalog));
        if (opened == null) throw new ArgumentNullException(nameof(opened));
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException(
                "Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        var host = new GameObject("AchievementCenter", typeof(RectTransform));
        host.layer = parent.gameObject.layer;
        RectTransform hostRect = (RectTransform)host.transform;
        hostRect.SetParent(parent, false);
        Stretch(hostRect);

        PuzzleAchievementCenter center = host.AddComponent<PuzzleAchievementCenter>();
        center.loadCatalog = loadCatalog;
        center.opened = opened;
        center.font = font;
        center.visuals = visuals;
        center.Build(font, hostRect);
        center.overlay.gameObject.SetActive(false);
        return center;
    }

    public void Open()
    {
        if (IsOpen)
            throw new InvalidOperationException("Achievement center is already open.");
        catalog = loadCatalog.Invoke() ??
            throw new InvalidOperationException("Achievement catalog query returned null.");
        if (catalog.Count == 0)
            throw new InvalidOperationException("Achievement catalog is empty.");

        opened.Invoke();
        launcher.SetActive(false);
        overlay.gameObject.SetActive(true);
        ((RectTransform)transform).SetAsLastSibling();
        ApplyFilter(PuzzleAchievementFilter.All);
    }

    public void ClosePanel()
    {
        if (!IsOpen)
            throw new InvalidOperationException("Achievement center is not open.");
        ClearRows();
        catalog = null;
        overlay.gameObject.SetActive(false);
        launcher.SetActive(true);
    }

    public void SetLauncherVisible(bool visible)
    {
        if (launcher == null)
            throw new InvalidOperationException("Achievement launcher is missing.");
        launcher.SetActive(visible);
    }

    public void RaiseToTop()
    {
        ((RectTransform)transform).SetAsLastSibling();
    }

    public void ApplyFilter(PuzzleAchievementFilter filter)
    {
        if (!IsOpen)
            throw new InvalidOperationException("Achievement center must be open before filtering.");
        if (!Enum.IsDefined(typeof(PuzzleAchievementFilter), filter))
            throw new ArgumentOutOfRangeException(nameof(filter));

        currentFilter = filter;
        ClearRows();
        int unlockedCount = 0;
        for (int i = 0; i < catalog.Count; i++)
        {
            PuzzleAchievementCatalogEntry entry = catalog[i] ??
                throw new InvalidOperationException($"Achievement catalog entry {i} is missing.");
            if (entry.IsUnlocked) unlockedCount++;
            if (Matches(entry, filter)) CreateEntry(entry, content);
        }

        summary.text =
            $"{unlockedCount:N0} / {catalog.Count:N0} DESBLOQUEADAS   •   " +
            $"{content.childCount:N0} EXIBIDAS";
        foreach (KeyValuePair<PuzzleAchievementFilter, Image> pair in filterBackgrounds)
            pair.Value.color = pair.Key == filter
                ? AccentColor
                : new Color(0.09f, 0.12f, 0.17f, 1f);
        Canvas.ForceUpdateCanvases();
    }

    public void Close()
    {
        loadCatalog = null;
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
        Button launcherButton = CreateButton(
            parent,
            "OpenAchievements",
            font,
            "CONQUISTAS",
            AccentColor,
            Open);
        launcher = launcherButton.gameObject;
        RectTransform launcherRect = (RectTransform)launcher.transform;
        launcherRect.anchorMin = launcherRect.anchorMax = Vector2.one;
        launcherRect.pivot = Vector2.one;
        launcherRect.anchoredPosition = new Vector2(-18f, -18f);
        launcherRect.sizeDelta = new Vector2(210f, 52f);
        RawImage launcherIcon = CreateRawImage(
            launcherRect,
            "Icon",
            visuals.Unlocked);
        RectTransform launcherIconRect = launcherIcon.rectTransform;
        launcherIconRect.anchorMin = launcherIconRect.anchorMax = new Vector2(0f, 0.5f);
        launcherIconRect.pivot = new Vector2(0f, 0.5f);
        launcherIconRect.anchoredPosition = new Vector2(8f, 0f);
        launcherIconRect.sizeDelta = new Vector2(38f, 38f);
        Text launcherLabel = launcherRect.Find("Label")?.GetComponent<Text>() ??
            throw new InvalidOperationException("Achievement launcher label is missing.");
        launcherLabel.rectTransform.offsetMin = new Vector2(48f, 0f);

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
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(1160f, 790f);
        panelObject.GetComponent<Image>().color = PanelColor;

        CreateText(
            panel,
            "Title",
            font,
            "CONQUISTAS",
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
            new Color(0.7f, 0.78f, 0.86f, 1f),
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

        CreateFilters(panel, font);
        CreateList(panel);
    }

    private void CreateFilters(RectTransform panel, Font font)
    {
        RectTransform filters = CreateRect(panel, "Filters");
        filters.anchorMin = new Vector2(0f, 1f);
        filters.anchorMax = new Vector2(1f, 1f);
        filters.pivot = new Vector2(0.5f, 1f);
        filters.offsetMin = new Vector2(46f, -178f);
        filters.offsetMax = new Vector2(-46f, -124f);

        PuzzleAchievementFilter[] values =
        {
            PuzzleAchievementFilter.All,
            PuzzleAchievementFilter.Unlocked,
            PuzzleAchievementFilter.InProgress,
            PuzzleAchievementFilter.Locked,
        };
        string[] labels = { "TODAS", "DESBLOQUEADAS", "EM PROGRESSO", "BLOQUEADAS" };
        float width = 250f;
        float spacing = 14f;
        for (int i = 0; i < values.Length; i++)
        {
            PuzzleAchievementFilter captured = values[i];
            Button button = CreateButton(
                filters,
                captured.ToString(),
                font,
                labels[i],
                new Color(0.09f, 0.12f, 0.17f, 1f),
                () => ApplyFilter(captured));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(i * (width + spacing), 0f);
            rect.sizeDelta = new Vector2(width, 50f);
            filterBackgrounds.Add(captured, button.GetComponent<Image>());
        }
    }

    private void CreateList(RectTransform panel)
    {
        var viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        viewportObject.layer = panel.gameObject.layer;
        RectTransform viewport = (RectTransform)viewportObject.transform;
        viewport.SetParent(panel, false);
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(46f, 40f);
        viewport.offsetMax = new Vector2(-46f, -198f);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.02f, 0.03f, 0.05f, 0.9f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = true;

        content = CreateRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewportObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38f;
    }

    private void CreateEntry(PuzzleAchievementCatalogEntry entry, RectTransform parent)
    {
        var cardObject = new GameObject(
            entry.Definition.Id,
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        cardObject.layer = parent.gameObject.layer;
        RectTransform card = (RectTransform)cardObject.transform;
        card.SetParent(parent, false);
        cardObject.GetComponent<Image>().color = entry.IsUnlocked
            ? new Color(0.06f, 0.2f, 0.18f, 1f)
            : CardColor;
        LayoutElement layout = cardObject.GetComponent<LayoutElement>();
        layout.minHeight = 122f;
        layout.preferredHeight = 122f;

        bool conceal = entry.Definition.Hidden && !entry.IsUnlocked;
        string titleValue = conceal ? "CONQUISTA SECRETA" : entry.Definition.Title;
        string descriptionValue = conceal
            ? "Continue jogando para revelar esta conquista."
            : entry.Definition.Description;
        Color stateColor = entry.IsUnlocked
            ? AccentColor
            : conceal ? LockedColor : entry.Progress > 0 ? ProgressColor : LockedColor;
        string state = entry.IsUnlocked
            ? $"{TierLabel(entry.Definition.Tier)}  •  DESBLOQUEADA"
            : conceal ? "SECRETA" : entry.Progress > 0 ? "EM PROGRESSO" : "BLOQUEADA";
        string progressValue = conceal
            ? "? / ?"
            : $"{entry.Progress:N0} / {entry.Definition.Target:N0}";

        RawImage icon = CreateRawImage(
            card,
            "Icon",
            visuals.GetCatalogIcon(entry.Definition, entry.IsUnlocked));
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(18f, 0f);
        iconRect.sizeDelta = new Vector2(92f, 92f);

        CreateText(
            card,
            "Title",
            font,
            titleValue,
            20,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft,
            new Vector2(130f, -46f),
            new Vector2(-230f, -12f));
        CreateText(
            card,
            "Description",
            font,
            descriptionValue,
            15,
            FontStyle.Normal,
            new Color(0.74f, 0.81f, 0.88f, 1f),
            TextAnchor.MiddleLeft,
            new Vector2(130f, -78f),
            new Vector2(-230f, -48f));
        CreateText(
            card,
            "State",
            font,
            state,
            14,
            FontStyle.Bold,
            stateColor,
            TextAnchor.MiddleRight,
            new Vector2(0f, -48f),
            new Vector2(-22f, -18f));
        CreateText(
            card,
            "ProgressValue",
            font,
            progressValue,
            15,
            FontStyle.Bold,
            stateColor,
            TextAnchor.MiddleRight,
            new Vector2(0f, -80f),
            new Vector2(-22f, -52f));

        RectTransform bar = CreateRect(card, "ProgressBar");
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.offsetMin = new Vector2(130f, 16f);
        bar.offsetMax = new Vector2(-22f, 24f);
        Image barBackground = bar.gameObject.AddComponent<Image>();
        barBackground.color = new Color(0.015f, 0.025f, 0.04f, 1f);

        float fillFraction = conceal ? 0f : Mathf.Clamp01((float)entry.Progress / entry.Definition.Target);
        RectTransform fill = CreateRect(bar, "Fill");
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(fillFraction, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = stateColor;
    }

    private void ClearRows()
    {
        if (content == null)
            throw new InvalidOperationException("Achievement center content is not initialized.");
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child);
            else
            {
                child.transform.SetParent(null, false);
                child.SetActive(false);
                Destroy(child);
            }
#else
            child.transform.SetParent(null, false);
            child.SetActive(false);
            Destroy(child);
#endif
        }
    }

    private static bool Matches(
        PuzzleAchievementCatalogEntry entry,
        PuzzleAchievementFilter filter) => filter switch
    {
        PuzzleAchievementFilter.All => true,
        PuzzleAchievementFilter.Unlocked => entry.IsUnlocked,
        PuzzleAchievementFilter.InProgress => !entry.IsUnlocked && entry.Progress > 0,
        PuzzleAchievementFilter.Locked => !entry.IsUnlocked && entry.Progress == 0,
        _ => throw new ArgumentOutOfRangeException(nameof(filter)),
    };

    private static string TierLabel(PuzzleAchievementTier tier) => tier switch
    {
        PuzzleAchievementTier.Bronze => "BRONZE",
        PuzzleAchievementTier.Silver => "PRATA",
        PuzzleAchievementTier.Gold => "OURO",
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    private static RawImage CreateRawImage(
        RectTransform parent,
        string name,
        Texture2D texture)
    {
        if (texture == null) throw new ArgumentNullException(nameof(texture));
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        imageObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.SetParent(parent, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
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
            Vector2.zero,
            Vector2.zero);
        Stretch(text.rectTransform);
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
