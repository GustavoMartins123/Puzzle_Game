using System;
using UnityEngine.InputSystem;

public sealed class GameInput : IDisposable
{
    private readonly PlayerInputActions actions;
    private readonly InputAction rotateClockwise;
    private readonly InputAction rotateCounterClockwise;

    public event Action PauseToggled;

    public event Action<int> RotationRequested;

    public GameInput()
    {
        actions = new PlayerInputActions();
        rotateClockwise = new InputAction("RotateClockwise", InputActionType.Button);
        rotateClockwise.AddBinding("<Keyboard>/e");
        rotateClockwise.AddBinding("<Mouse>/rightButton");
        rotateClockwise.AddBinding("<Gamepad>/rightShoulder");
        rotateCounterClockwise = new InputAction("RotateCounterClockwise", InputActionType.Button);
        rotateCounterClockwise.AddBinding("<Keyboard>/q");
        rotateCounterClockwise.AddBinding("<Gamepad>/leftShoulder");
        actions.Move.Pause.performed += OnPausePerformed;
        rotateClockwise.performed += OnRotateClockwise;
        rotateCounterClockwise.performed += OnRotateCounterClockwise;
        actions.Enable();
        rotateClockwise.Enable();
        rotateCounterClockwise.Enable();
    }

    private void OnPausePerformed(InputAction.CallbackContext context) => PauseToggled?.Invoke();

    private void OnRotateClockwise(InputAction.CallbackContext context) =>
        RotationRequested?.Invoke(1);

    private void OnRotateCounterClockwise(InputAction.CallbackContext context) =>
        RotationRequested?.Invoke(-1);

    public void Dispose()
    {
        actions.Move.Pause.performed -= OnPausePerformed;
        rotateClockwise.performed -= OnRotateClockwise;
        rotateCounterClockwise.performed -= OnRotateCounterClockwise;
        rotateClockwise.Disable();
        rotateCounterClockwise.Disable();
        rotateClockwise.Dispose();
        rotateCounterClockwise.Dispose();
        actions.Disable();
        actions.Dispose();
    }
}
