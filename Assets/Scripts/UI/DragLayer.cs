using UnityEngine;

public class DragLayer : MonoBehaviour
{
    [SerializeField] private float followSpeed = 30f;

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
        held = piece;
        gameObject.SetActive(true);
        rectTransform.position = pointerPosition;
        piece.AttachTo(rectTransform);
    }

    public PuzzlePiece Release()
    {
        PuzzlePiece piece = held;
        held = null;
        gameObject.SetActive(false);
        return piece;
    }

    public void Follow(Vector2 pointerPosition)
    {
        float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        rectTransform.position = Vector2.Lerp(rectTransform.position, pointerPosition, t);
    }
}
