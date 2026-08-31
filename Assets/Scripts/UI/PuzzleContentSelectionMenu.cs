using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleContentSelectionMenu : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 1f);
    private static readonly Color CardColor = new Color(0.09f, 0.12f, 0.17f, 1f);
    private static readonly Color SelectedColor = new Color(0.12f, 0.38f, 0.38f, 1f);
    private static readonly Color AccentColor = new Color(0.25f, 0.92f, 0.78f, 1f);
    private static readonly Color MutedColor = new Color(0.66f, 0.72f, 0.79f, 1f);

    private readonly Dictionary<PuzzleCollectionDefinition, Image> collectionCards =
        new Dictionary<PuzzleCollectionDefinition, Image>();
    private readonly Dictionary<PuzzleImageDefinition, Image> imageCards =
        new Dictionary<PuzzleImageDefinition, Image>();

    private PuzzleConfig config;
    private PuzzleProgressData progress;
    private Func<PuzzleImageDefinition, bool> confirmed;
    private PuzzleCollectionDefinition selectedCollection;
    private PuzzleImageDefinition selectedImage;
    private Font font;
    private RectTransform imageListContent;
    private RawImage preview;
    private AspectRatioFitter previewAspect;
    private Text previewTitle;
    private Text resultSummary;
    private Text progressStatus;
    private Button confirmButton;

    public static PuzzleContentSelectionMenu Show(
        RectTransform parent,
        PuzzleConfig config,
        PuzzleProgressData progress,
        string initialImageId,
        Func<PuzzleImageDefinition, bool> onConfirmed)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (!config.TryValidate(out string configError))
            throw new ArgumentException($"Invalid puzzle configuration: {configError}.", nameof(config));
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        progress.Validate(config);
        PuzzleImageDefinition initialImage = config.GetImage(initialImageId);
        if (!progress.IsImageUnlocked(initialImage.Id))
            throw new ArgumentException(
                $"Initial image '{initialImage.Id}' is locked.", nameof(initialImageId));
        if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

        GameObject root = CreateUiObject("ContentSelection", parent);
        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);
        Image overlay = root.AddComponent<Image>();
        overlay.color = OverlayColor;
        overlay.raycastTarget = true;

        PuzzleContentSelectionMenu menu = root.AddComponent<PuzzleContentSelectionMenu>();
        menu.Initialize(config, progress, initialImage, onConfirmed);
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
            throw new ArgumentException("Content selection error is required.", nameof(message));
        if (progressStatus == null)
            throw new InvalidOperationException("Content selection status is missing.");
        progressStatus.color = new Color(1f, 0.42f, 0.42f, 1f);
        progressStatus.text = "ERRO: " + message;
    }

    private void Initialize(
        PuzzleConfig config,
        PuzzleProgressData progress,
        PuzzleImageDefinition initialImage,
        Func<PuzzleImageDefinition, bool> onConfirmed)
    {
        this.config = config;
        this.progress = progress;
        confirmed = onConfirmed;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException("Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        RectTransform panel = CreateRect("Panel", transform);
        SetRect(panel, Vector2.zero, new Vector2(1120f, 840f), Vector2.one * 0.5f, Vector2.one * 0.5f);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        CreateText(
            "Title", panel, "ESCOLHA SUA IMAGEM", 32, FontStyle.Bold, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0f, -30f), new Vector2(1040f, 46f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        RectTransform progressRect = CreateText(
            "Progress", panel,
            BuildProgressStatus(),
            17, FontStyle.Normal, MutedColor, TextAnchor.MiddleCenter,
            new Vector2(0f, -75f), new Vector2(1040f, 34f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        progressStatus = progressRect.GetComponent<Text>();

        BuildCollections(panel);
        BuildImageList(panel);
        BuildPreview(panel);
        confirmButton = CreateButton(
            panel,
            "Continue",
            "CONTINUAR PARA AS REGRAS",
            new Vector2(0f, 28f),
            new Vector2(310f, 56f));
        SetRect(
            (RectTransform)confirmButton.transform,
            new Vector2(0f, 55f),
            new Vector2(310f, 56f),
            new Vector2(0.5f, 0f),
            Vector2.one * 0.5f);
        confirmButton.onClick.AddListener(ConfirmSelection);

        PuzzleCollectionDefinition initialCollection = config.GetCollectionForImage(initialImage.Id);
        SelectCollection(initialCollection);
        SelectImage(initialImage);
    }

    private void BuildCollections(RectTransform panel)
    {
        RectTransform root = CreateRect("Collections", panel);
        SetRect(
            root,
            new Vector2(30f, -120f),
            new Vector2(200f, 620f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < config.Collections.Count; i++)
        {
            PuzzleCollectionDefinition collection = config.Collections[i];
            bool unlocked = config.IsCollectionUnlocked(collection, progress.UniqueCompletedImages);
            string label = unlocked
                ? collection.DisplayName
                : $"{collection.DisplayName}\nBLOQUEADO • {collection.RequiredUniqueCompletions}";
            Button button = CreateButton(
                root,
                collection.Id,
                label,
                Vector2.zero,
                new Vector2(200f, 64f));
            button.interactable = unlocked;
            PuzzleCollectionDefinition captured = collection;
            button.onClick.AddListener(() => SelectCollection(captured));
            collectionCards.Add(collection, button.targetGraphic as Image);
        }
    }

    private void BuildImageList(RectTransform panel)
    {
        RectTransform listPanel = CreateRect("Images", panel);
        SetRect(
            listPanel,
            new Vector2(245f, -120f),
            new Vector2(350f, 620f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        Image background = listPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.047f, 0.068f, 1f);

        ScrollRect scroll = listPanel.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        RectTransform viewport = CreateRect("Viewport", listPanel);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(10f, 10f);
        viewport.offsetMax = new Vector2(-10f, -10f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        imageListContent = CreateRect("Content", viewport);
        imageListContent.anchorMin = new Vector2(0f, 1f);
        imageListContent.anchorMax = new Vector2(1f, 1f);
        imageListContent.pivot = new Vector2(0.5f, 1f);
        imageListContent.anchoredPosition = Vector2.zero;
        imageListContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = imageListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = imageListContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = imageListContent;
    }

    private void BuildPreview(RectTransform panel)
    {
        RectTransform previewPanel = CreateRect("PreviewPanel", panel);
        SetRect(
            previewPanel,
            new Vector2(-30f, -120f),
            new Vector2(475f, 620f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));
        Image background = previewPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.047f, 0.068f, 1f);

        RectTransform titleRect = CreateText(
            "PreviewTitle", previewPanel, string.Empty, 24, FontStyle.Bold, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(435f, 40f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        previewTitle = titleRect.GetComponent<Text>();

        RectTransform frame = CreateRect("PreviewFrame", previewPanel);
        SetRect(
            frame,
            new Vector2(0f, -78f),
            new Vector2(425f, 330f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f));
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.012f, 0.017f, 0.025f, 1f);

        RectTransform previewRect = CreateRect("Image", frame);
        SetRect(
            previewRect,
            Vector2.zero,
            new Vector2(395f, 300f),
            Vector2.one * 0.5f,
            Vector2.one * 0.5f);
        preview = previewRect.gameObject.AddComponent<RawImage>();
        preview.color = Color.white;
        preview.raycastTarget = false;
        previewAspect = previewRect.gameObject.AddComponent<AspectRatioFitter>();
        previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        RectTransform summaryRect = CreateText(
            "BestResults", previewPanel, string.Empty, 17, FontStyle.Normal, MutedColor,
            TextAnchor.UpperLeft, new Vector2(25f, 24f), new Vector2(425f, 150f),
            new Vector2(0f, 0f), new Vector2(0f, 0f));
        resultSummary = summaryRect.GetComponent<Text>();
    }

    private void SelectCollection(PuzzleCollectionDefinition collection)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (!config.IsCollectionUnlocked(collection, progress.UniqueCompletedImages))
            throw new InvalidOperationException($"Collection '{collection.Id}' is locked.");

        selectedCollection = collection;
        foreach (KeyValuePair<PuzzleCollectionDefinition, Image> pair in collectionCards)
            pair.Value.color = pair.Key == collection ? SelectedColor : CardColor;

        for (int i = imageListContent.childCount - 1; i >= 0; i--)
            Destroy(imageListContent.GetChild(i).gameObject);
        imageCards.Clear();

        for (int i = 0; i < collection.Images.Count; i++)
        {
            PuzzleImageDefinition image = collection.Images[i];
            bool unlocked = progress.IsImageUnlocked(image.Id);
            string label = unlocked
                ? image.DisplayName
                : $"{image.DisplayName}\nBLOQUEADO • {image.RequiredUniqueCompletions}";
            Button button = CreateButton(
                imageListContent,
                image.Id,
                label,
                Vector2.zero,
                new Vector2(330f, 52f));
            button.interactable = unlocked;
            PuzzleImageDefinition captured = image;
            button.onClick.AddListener(() => SelectImage(captured));
            imageCards.Add(image, button.targetGraphic as Image);
        }

        if (selectedImage == null || config.GetCollectionForImage(selectedImage.Id) != collection)
        {
            PuzzleImageDefinition firstUnlocked = null;
            for (int i = 0; i < collection.Images.Count; i++)
                if (progress.IsImageUnlocked(collection.Images[i].Id))
                {
                    firstUnlocked = collection.Images[i];
                    break;
                }
            if (firstUnlocked == null)
                throw new InvalidOperationException(
                    $"Unlocked collection '{collection.Id}' has no unlocked images.");
            SelectImage(firstUnlocked);
        }
    }

    private void SelectImage(PuzzleImageDefinition image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (config.GetCollectionForImage(image.Id) != selectedCollection)
            throw new InvalidOperationException(
                $"Image '{image.Id}' does not belong to the selected collection.");
        if (!progress.IsImageUnlocked(image.Id))
            throw new InvalidOperationException($"Image '{image.Id}' is locked.");

        selectedImage = image;
        foreach (KeyValuePair<PuzzleImageDefinition, Image> pair in imageCards)
            pair.Value.color = pair.Key == image ? SelectedColor : CardColor;
        progressStatus.color = MutedColor;
        progressStatus.text = BuildProgressStatus();
        Texture2D texture = config.LoadImage(image);
        preview.texture = texture;
        previewAspect.aspectRatio = texture.width / (float)texture.height;
        previewTitle.text = image.DisplayName;
        resultSummary.text = BuildResultSummary(image);
        confirmButton.interactable = true;
    }

    private string BuildResultSummary(PuzzleImageDefinition image)
    {
        string summary = "MELHORES RESULTADOS";
        bool hasResult = false;
        for (int i = 0; i < config.DifficultyProfiles.Count; i++)
        {
            PuzzleDifficultyProfile profile = config.DifficultyProfiles[i];
            if (!progress.TryGetBestResult(image.Id, profile.Difficulty, out PuzzleBestResultRecord result))
                continue;
            hasResult = true;
            summary += $"\n{profile.DisplayName}: {result.BestScore:N0} • " +
                       $"{result.BestSeconds:F1}s • {PuzzleMedalCalculator.Label(result.BestMedal)}";
        }

        if (!hasResult) summary += "\nNenhuma conclusão nesta imagem.";
        return summary;
    }

    private string BuildProgressStatus() =>
        $"{progress.UniqueCompletedImages} imagens concluídas • " +
        "conteúdo bloqueado mostra o requisito";

    private void ConfirmSelection()
    {
        if (!UiInputGuard.Allows) return;
        if (confirmed == null)
            throw new InvalidOperationException("Content confirmation callback is missing.");
        if (selectedImage == null)
            throw new InvalidOperationException("Image selection is missing.");

        UiInputGuard.Block();
        confirmButton.interactable = false;
        if (confirmed(selectedImage)) Close();
        else confirmButton.interactable = true;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.sizeDelta = size;
        if (position != Vector2.zero) rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = CardColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f),
            pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.42f, 0.46f, 0.5f, 0.65f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        RectTransform labelRect = CreateText(
            "Label", rect, label, 15, FontStyle.Bold, Color.white,
            TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero);
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    private RectTransform CreateText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        FontStyle style,
        Color color,
        TextAnchor alignment,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, position, size, anchor, pivot);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.alignByGeometry = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return rect;
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
