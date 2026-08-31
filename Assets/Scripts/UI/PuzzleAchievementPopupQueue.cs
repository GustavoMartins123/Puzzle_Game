using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleAchievementPopupQueue : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0.005f, 0.01f, 0.02f, 0.78f);
    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.085f, 1f);
    private static readonly Color AccentColor = new Color(0.12f, 0.72f, 0.64f, 1f);

    private readonly Queue<PuzzleAchievementNotification> notifications =
        new Queue<PuzzleAchievementNotification>();
    private readonly HashSet<long> queuedIds = new HashSet<long>();

    private RectTransform popupRoot;
    private Text title;
    private Text description;
    private Text progress;
    private Text error;
    private Button acknowledgeButton;
    private Action<long> acknowledge;

    public bool IsOpen => popupRoot != null && popupRoot.gameObject.activeSelf;

    public int PendingCount => notifications.Count;

    public static PuzzleAchievementPopupQueue Show(
        RectTransform parent,
        Action<long> acknowledge)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (acknowledge == null) throw new ArgumentNullException(nameof(acknowledge));
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException(
                "Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        var host = new GameObject("AchievementPopupQueue", typeof(RectTransform));
        host.layer = parent.gameObject.layer;
        RectTransform hostRect = (RectTransform)host.transform;
        hostRect.SetParent(parent, false);
        Stretch(hostRect);

        PuzzleAchievementPopupQueue queue = host.AddComponent<PuzzleAchievementPopupQueue>();
        queue.acknowledge = acknowledge;
        queue.Build(font, hostRect);
        queue.popupRoot.gameObject.SetActive(false);
        return queue;
    }

    public void Enqueue(IReadOnlyList<PuzzleAchievementNotification> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        for (int i = 0; i < values.Count; i++)
        {
            PuzzleAchievementNotification notification = values[i] ??
                throw new ArgumentException($"Achievement notification {i} is missing.", nameof(values));
            if (!queuedIds.Add(notification.NotificationId)) continue;
            notifications.Enqueue(notification);
        }
        if (!IsOpen && notifications.Count > 0) PresentNext();
    }

    public void Close()
    {
        if (acknowledgeButton != null)
            acknowledgeButton.onClick.RemoveListener(AcknowledgeCurrent);
        acknowledge = null;
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
        GameObject backdropObject = new GameObject(
            "Backdrop",
            typeof(RectTransform),
            typeof(Image));
        backdropObject.layer = parent.gameObject.layer;
        popupRoot = (RectTransform)backdropObject.transform;
        popupRoot.SetParent(parent, false);
        Stretch(popupRoot);
        backdropObject.GetComponent<Image>().color = BackdropColor;

        GameObject panelObject = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(Image));
        panelObject.layer = parent.gameObject.layer;
        RectTransform panel = (RectTransform)panelObject.transform;
        panel.SetParent(popupRoot, false);
        panel.anchorMin = panel.anchorMax = Vector2.one * 0.5f;
        panel.pivot = Vector2.one * 0.5f;
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(680f, 370f);
        panelObject.GetComponent<Image>().color = PanelColor;

        Text eyebrow = CreateText(
            panel,
            "Eyebrow",
            font,
            "CONQUISTA DESBLOQUEADA",
            18,
            FontStyle.Bold,
            AccentColor,
            TextAnchor.MiddleCenter,
            new Vector2(40f, -78f),
            new Vector2(-40f, -34f));
        eyebrow.raycastTarget = false;

        title = CreateText(
            panel,
            "Title",
            font,
            string.Empty,
            32,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter,
            new Vector2(42f, -152f),
            new Vector2(-42f, -90f));
        description = CreateText(
            panel,
            "Description",
            font,
            string.Empty,
            20,
            FontStyle.Normal,
            new Color(0.8f, 0.86f, 0.92f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(62f, -230f),
            new Vector2(-62f, -158f));
        progress = CreateText(
            panel,
            "Progress",
            font,
            string.Empty,
            17,
            FontStyle.Bold,
            AccentColor,
            TextAnchor.MiddleCenter,
            new Vector2(62f, -266f),
            new Vector2(-62f, -230f));
        error = CreateText(
            panel,
            "Error",
            font,
            string.Empty,
            14,
            FontStyle.Normal,
            new Color(1f, 0.45f, 0.42f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(44f, -302f),
            new Vector2(-44f, -270f));

        acknowledgeButton = CreateButton(panel, font);
        acknowledgeButton.onClick.AddListener(AcknowledgeCurrent);
    }

    private void PresentNext()
    {
        if (notifications.Count == 0)
        {
            popupRoot.gameObject.SetActive(false);
            return;
        }

        PuzzleAchievementNotification notification = notifications.Peek();
        title.text = notification.Definition.Title;
        description.text = notification.Definition.Description;
        progress.text = $"META CONCLUÍDA  {notification.Definition.Target:N0}/{notification.Definition.Target:N0}";
        error.text = string.Empty;
        acknowledgeButton.interactable = true;
        transform.SetAsLastSibling();
        popupRoot.gameObject.SetActive(true);
    }

    private void AcknowledgeCurrent()
    {
        if (!UiInputGuard.Allows) return;
        if (notifications.Count == 0)
            throw new InvalidOperationException("There is no achievement notification to acknowledge.");
        if (acknowledge == null)
            throw new InvalidOperationException("Achievement acknowledgement callback is missing.");

        PuzzleAchievementNotification notification = notifications.Peek();
        acknowledgeButton.interactable = false;
        try
        {
            acknowledge.Invoke(notification.NotificationId);
            notifications.Dequeue();
            queuedIds.Remove(notification.NotificationId);
            UiInputGuard.Block();
            PresentNext();
        }
        catch (Exception exception)
        {
            error.text = "NÃO FOI POSSÍVEL CONFIRMAR. TENTE NOVAMENTE.";
            acknowledgeButton.interactable = true;
            Debug.LogError(
                $"Achievement notification {notification.NotificationId} was not acknowledged: " +
                exception.Message,
                this);
        }
    }

    private static Button CreateButton(RectTransform parent, Font font)
    {
        var buttonObject = new GameObject(
            "Acknowledge",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 28f);
        rect.sizeDelta = new Vector2(250f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = AccentColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        Text label = CreateText(
            rect,
            "Label",
            font,
            "CONTINUAR",
            18,
            FontStyle.Bold,
            new Color(0.015f, 0.04f, 0.05f, 1f),
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero);
        Stretch(label.rectTransform);
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
