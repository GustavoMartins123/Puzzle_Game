using UnityEngine;

public class DragLayer : MonoBehaviour
{
    private RectTransform rectTransform;
    private PuzzlePiece held;

    public PuzzlePiece Held => held;

    public bool IsDragging => held != null;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        gameObject.SetActive(false);
    }

    public void Take(PuzzlePiece piece, Vector2 pointerPosition)
    {
        if (piece == null) throw new System.ArgumentNullException(nameof(piece));
        if (held != null)
            throw new System.InvalidOperationException("DragLayer already holds a puzzle piece.");

        held = piece;
        gameObject.SetActive(true);
        rectTransform.SetAsLastSibling();
        rectTransform.position = pointerPosition;
        piece.AttachTo(rectTransform);
    }

    public PuzzlePiece Release()
    {
        if (held == null)
            throw new System.InvalidOperationException("DragLayer has no puzzle piece to release.");

        PuzzlePiece piece = held;
        held = null;
        gameObject.SetActive(false);
        return piece;
    }

    public void Move(Vector2 pointerPosition)
    {
        if (held == null)
            throw new System.InvalidOperationException("DragLayer has no puzzle piece to move.");
        rectTransform.position = pointerPosition;
    }
}
