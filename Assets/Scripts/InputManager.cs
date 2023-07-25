using UnityEngine;
public class InputManager: MonoBehaviour
{
    PlayerInputActions2 inputActions;
    PlayerInputActions2.MoveActions moveActions;
    [SerializeField] UiDragPiece dragPiece;

    private void Awake()
    {
        inputActions = new PlayerInputActions2();
        moveActions = inputActions.Move;
        moveActions.MousePos.performed += ctx => GetMousePosition();
        //moveActions.MousePos.canceled += ctx => GetMousePosition(); Work on this
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

        Vector2 targetPosition = Vector2.Lerp(dragPiece.mouseImg.rectTransform.position, mousePosition, 0.4f);

        dragPiece.mouseImg.rectTransform.position = targetPosition;
    }
}
