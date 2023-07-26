using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InputManager: MonoBehaviour
{
    public EventHandler<bool> OnPieceChanged;
    public EventHandler OnPauseCalled;
    PlayerInputActions2 inputActions;
    PlayerInputActions2.MoveActions moveActions;
    [SerializeField] UiDragPiece dragPiece;

    [SerializeField] GameObject panelWin;
    bool pause = false;
    private void Awake()
    {
        inputActions = new PlayerInputActions2();
        moveActions = inputActions.Move;
        moveActions.MousePos.performed += ctx => GetMousePosition();
        moveActions.Pause.performed += ctx => Pause();
    }
    private void OnEnable()
    {
        moveActions.Enable();
    }

    private void OnDisable()
    {
        moveActions.Disable();
    }

    void GetMousePosition()
    {
        Vector2 mousePosition = moveActions.MousePos.ReadValue<Vector2>();

        float screenWidth = Screen.currentResolution.width;
        float screenHeight = Screen.currentResolution.height;
        Vector2 currentPosition = dragPiece.mouseImg.rectTransform.position;

        Vector2 clampedPosition = new Vector2(Mathf.Clamp(currentPosition.x, -screenWidth, screenWidth),
            Mathf.Clamp(currentPosition.y, -screenHeight, screenHeight));

        Vector2 smoothedPosition = Vector2.Lerp(clampedPosition, mousePosition, 0.5f);
        dragPiece.mouseImg.rectTransform.position = smoothedPosition;
    }

    void Pause()
    {
        pause = !pause;
        panelWin.SetActive(pause);
        Time.timeScale = pause ? 0f : 1f;
        OnPauseCalled?.Invoke(this, EventArgs.Empty);
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
