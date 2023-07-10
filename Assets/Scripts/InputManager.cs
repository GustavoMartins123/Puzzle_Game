using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager
{
    PlayerActionInput inputActions;
    public PlayerActionInput.PlayerActionsActions moveActions;
    internal void InitializeInput()
    {
        inputActions = new PlayerActionInput();
        moveActions = inputActions.PlayerActions;
    }
}
