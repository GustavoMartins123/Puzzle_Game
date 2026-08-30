using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameInput : IDisposable
{
    private readonly PlayerInputActions actions;

    public event Action PauseToggled;

    public GameInput()
    {
        actions = new PlayerInputActions();
        actions.Move.Pause.performed += OnPausePerformed;
        actions.Enable();
    }

    public Vector2 PointerPosition => actions.Move.MousePos.ReadValue<Vector2>();

    private void OnPausePerformed(InputAction.CallbackContext context) => PauseToggled?.Invoke();

    public void Dispose()
    {
        actions.Move.Pause.performed -= OnPausePerformed;
        actions.Disable();
        actions.Dispose();
    }
}
