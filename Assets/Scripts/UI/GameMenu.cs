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

    public event Action Opened;

    public event Action RetryRequested;

    public event Action OptionsRequested;

    public bool IsOpen => panel.activeSelf;

    private void Awake() => panel.SetActive(false);

    public void TogglePause()
    {
        if (finished) return;
        if (IsOpen) Close();
        else Open(pauseLabel);
    }

    public void ShowWin()
    {
        finished = true;
        Open(winLabel);
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

    private void Open(string label)
    {
        title.text = label;
        panel.SetActive(true);
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
        Close();
    }
}
