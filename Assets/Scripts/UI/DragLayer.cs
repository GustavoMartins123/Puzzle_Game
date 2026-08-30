using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class DragLayer : MonoBehaviour
{
    private RectTransform rectTransform;
    private PuzzlePiece held;
    private InputAction pointerPosition;
    private InputAction primaryPress;
    private readonly List<RaycastResult> raycastResults = new();

    public PuzzlePiece Held => held;

    public bool IsDragging => held != null;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        pointerPosition = new InputAction("Puzzle drag position", InputActionType.PassThrough);
        pointerPosition.AddBinding("<Pointer>/position");
        primaryPress = new InputAction("Puzzle drag press", InputActionType.Button);
        primaryPress.AddBinding("<Pointer>/press");
        pointerPosition.Enable();
        primaryPress.Enable();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (held == null)
            throw new InvalidOperationException("An active DragLayer must hold a puzzle piece.");

        Vector2 currentPosition = pointerPosition.ReadValue<Vector2>();
        Move(currentPosition);
        if (primaryPress.WasReleasedThisFrame()) CompleteDrag(currentPosition);
    }

    private void OnDestroy()
    {
        pointerPosition?.Dispose();
        primaryPress?.Dispose();
    }

    public void Take(PuzzlePiece piece, Vector2 pointerPosition)
    {
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        if (held != null)
            throw new InvalidOperationException("DragLayer already holds a puzzle piece.");
        if (!primaryPress.IsPressed())
            throw new InvalidOperationException(
                "A puzzle drag can only begin while the primary pointer is physically pressed.");

        held = piece;
        gameObject.SetActive(true);
        rectTransform.SetAsLastSibling();
        rectTransform.position = pointerPosition;
        piece.AttachTo(rectTransform);
    }

    public PuzzlePiece Release()
    {
        if (held == null)
            throw new InvalidOperationException("DragLayer has no puzzle piece to release.");

        PuzzlePiece piece = held;
        held = null;
        gameObject.SetActive(false);
        return piece;
    }

    public void Move(Vector2 pointerPosition)
    {
        if (held == null)
            throw new InvalidOperationException("DragLayer has no puzzle piece to move.");
        rectTransform.position = pointerPosition;
    }

    private void CompleteDrag(Vector2 pointerPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            throw new InvalidOperationException("Puzzle drag completion requires an active EventSystem.");

        raycastResults.Clear();
        var eventData = new PointerEventData(eventSystem) { position = pointerPosition };
        eventSystem.RaycastAll(eventData, raycastResults);

        Slot slot = raycastResults.Count == 0
            ? null
            : raycastResults[0].gameObject.GetComponentInParent<Slot>();
        if (slot != null) slot.TryDrop(held);

        if (held != null) held.FinishUnplacedDrag();
        raycastResults.Clear();
    }
}
