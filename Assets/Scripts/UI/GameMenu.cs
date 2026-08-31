using System;
using UnityEngine;
using UnityEngine.UI;

public class GameMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text title;
    [SerializeField] private string pauseLabel = "PAUSED";
    [SerializeField] private string winLabel = "YOU WIN!";

    private bool finished;
    private GameObject resultRoot;
    private Text resultDetails;
    private GameObject repeatSeedRoot;
    private GameObject retryAction;
    private GameObject optionsAction;

    public event Action Opened;

    public event Action RetryRequested;

    public event Action OptionsRequested;

    public event Action RepeatSeedRequested;

    public bool IsOpen => panel.activeSelf;

    private void Awake()
    {
        if (panel == null || title == null)
            throw new InvalidOperationException("Game menu references are incomplete.");
        retryAction = RequireAction("RetryButton");
        optionsAction = RequireAction("OptionsButton");
        panel.SetActive(false);
    }

    public void TogglePause()
    {
        if (finished) return;
        if (IsOpen) Close();
        else Open(pauseLabel, false, false);
    }

    public void ShowWin(PuzzleScoreBreakdown score, PuzzleProgressUpdate progressUpdate)
    {
        if (score == null) throw new ArgumentNullException(nameof(score));
        if (progressUpdate == null) throw new ArgumentNullException(nameof(progressUpdate));
        finished = true;
        EnsureResultView();
        resultDetails.text = score.BuildDetails() + "\n\n" + progressUpdate.BuildSummary();
        Open(winLabel, true, true);
    }

    public void ShowFailure(string label, string details)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Failure label is required.", nameof(label));
        if (string.IsNullOrWhiteSpace(details))
            throw new ArgumentException("Failure details are required.", nameof(details));
        finished = true;
        EnsureResultView();
        resultDetails.text = details;
        Open(label, true, false, false);
    }

    public void Retry()
    {
        ResetAndClose();
        RetryRequested?.Invoke();
    }

    public void BackToOptions()
    {
        ResetAndClose();
        OptionsRequested?.Invoke();
    }

    public void RepeatSamePuzzle()
    {
        if (!finished)
            throw new InvalidOperationException("Exact replay is available only after completion.");
        ResetAndClose();
        RepeatSeedRequested?.Invoke();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Open(
        string label,
        bool showResult,
        bool showRepeatSeed,
        bool showStandardActions = true)
    {
        title.text = label;
        if (showResult && resultRoot == null)
            throw new InvalidOperationException("Result view is missing.");
        if (resultRoot != null) resultRoot.SetActive(showResult);
        if (repeatSeedRoot != null) repeatSeedRoot.SetActive(showRepeatSeed);
        retryAction.SetActive(showStandardActions);
        optionsAction.SetActive(showStandardActions);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Time.timeScale = 0f;
        Opened?.Invoke();
    }

    private void Close()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void ResetAndClose()
    {
        UiInputGuard.Block();
        finished = false;
        if (resultRoot != null) resultRoot.SetActive(false);
        if (repeatSeedRoot != null) repeatSeedRoot.SetActive(false);
        retryAction.SetActive(true);
        optionsAction.SetActive(true);
        Close();
    }

    private GameObject RequireAction(string objectName)
    {
        Transform action = panel.transform.Find(objectName);
        if (action == null)
            throw new InvalidOperationException($"Game menu action '{objectName}' is missing.");
        return action.gameObject;
    }

    private void EnsureResultView()
    {
        if (resultRoot != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            throw new InvalidOperationException("Unity built-in font 'LegacyRuntime.ttf' is unavailable.");

        resultRoot = new GameObject("ScoreBreakdown", typeof(RectTransform), typeof(Image));
        resultRoot.layer = panel.layer;
        RectTransform rootRect = (RectTransform)resultRoot.transform;
        rootRect.SetParent(panel.transform, false);
        rootRect.anchorMin = new Vector2(0.04f, 0.18f);
        rootRect.anchorMax = new Vector2(0.34f, 0.78f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image background = resultRoot.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.055f, 0.94f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Details", typeof(RectTransform), typeof(Text));
        textObject.layer = panel.layer;
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(22f, 20f);
        textRect.offsetMax = new Vector2(-22f, -20f);
        resultDetails = textObject.GetComponent<Text>();
        resultDetails.font = font;
        resultDetails.fontSize = 20;
        resultDetails.fontStyle = FontStyle.Bold;
        resultDetails.color = Color.white;
        resultDetails.alignment = TextAnchor.MiddleLeft;
        resultDetails.alignByGeometry = true;
        resultDetails.raycastTarget = false;
        resultRoot.SetActive(false);

        repeatSeedRoot = new GameObject("RepeatSeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
        repeatSeedRoot.layer = panel.layer;
        RectTransform repeatRect = (RectTransform)repeatSeedRoot.transform;
        repeatRect.SetParent(panel.transform, false);
        repeatRect.anchorMin = new Vector2(0.396f, 0.10f);
        repeatRect.anchorMax = new Vector2(0.604f, 0.22f);
        repeatRect.offsetMin = Vector2.zero;
        repeatRect.offsetMax = Vector2.zero;
        Image repeatImage = repeatSeedRoot.GetComponent<Image>();
        repeatImage.color = Color.white;
        Button repeatButton = repeatSeedRoot.GetComponent<Button>();
        repeatButton.targetGraphic = repeatImage;
        repeatButton.navigation = new Navigation { mode = Navigation.Mode.None };
        repeatButton.colors = new ColorBlock
        {
            normalColor = new Color(0.12f, 0.65f, 0.56f, 1f),
            highlightedColor = new Color(0.18f, 0.82f, 0.7f, 1f),
            pressedColor = new Color(0.08f, 0.48f, 0.42f, 1f),
            selectedColor = new Color(0.18f, 0.82f, 0.7f, 1f),
            disabledColor = new Color(0.2f, 0.28f, 0.3f, 0.65f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };
        repeatButton.onClick.AddListener(RepeatSamePuzzle);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.layer = panel.layer;
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(repeatRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.font = font;
        label.text = "REPETIR MESMA IMAGEM E SEED";
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.alignByGeometry = true;
        label.raycastTarget = false;
        repeatSeedRoot.SetActive(false);
    }
}
