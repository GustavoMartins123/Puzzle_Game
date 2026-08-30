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

    public event Action Opened;

    public event Action RetryRequested;

    public event Action OptionsRequested;

    public bool IsOpen => panel.activeSelf;

    private void Awake() => panel.SetActive(false);

    public void TogglePause()
    {
        if (finished) return;
        if (IsOpen) Close();
        else Open(pauseLabel, false);
    }

    public void ShowWin(PuzzleScoreBreakdown score)
    {
        if (score == null) throw new ArgumentNullException(nameof(score));
        finished = true;
        EnsureResultView();
        resultDetails.text = score.BuildDetails();
        Open(winLabel, true);
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

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Open(string label, bool showResult)
    {
        title.text = label;
        if (showResult && resultRoot == null)
            throw new InvalidOperationException("Result view is missing.");
        if (resultRoot != null) resultRoot.SetActive(showResult);
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
        finished = false;
        if (resultRoot != null) resultRoot.SetActive(false);
        Close();
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
    }
}
