using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ProceduralPuzzleImage))]
public class Slot : MonoBehaviour, IDropHandler
{
    [SerializeField] private SlotFeedback feedback;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private Action<Slot> filled;
    private int id;
    private ProceduralPuzzleImage image;

    public int Id => id;

    public RectTransform RectTransform => rectTransform;

    public SlotFeedback Feedback => feedback;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        image = GetComponent<ProceduralPuzzleImage>();
        if (image == null)
            throw new InvalidOperationException("Slot requires a ProceduralPuzzleImage.");
    }

    public void Setup(
        int id,
        ProceduralPuzzleGeometry geometry,
        float cutPadding,
        DragLayer dragLayer,
        Action<Slot> filled)
    {
        if (image.sprite == null) throw new InvalidOperationException("Slot requires a source sprite.");
        if (dragLayer == null) throw new ArgumentNullException(nameof(dragLayer));

        this.id = id;
        this.dragLayer = dragLayer;
        this.filled = filled;
        image.Configure(
            image.sprite,
            geometry,
            false,
            Vector2.one * (1f + cutPadding * 2f));
    }

    public void OnDrop(PointerEventData eventData)
    {
        PuzzlePiece piece = dragLayer.Held;
        if (piece == null) return;

        if (piece.Id != id)
        {
            piece.MarkRejected();
            feedback.PlayRejected(image);
            return;
        }

        Vector3 origin = piece.RectTransform.position;

        dragLayer.Release();
        piece.PlaceInto(this);
        image.raycastTarget = false;
        feedback.PlayFilled(piece, origin);
        filled?.Invoke(this);
    }
}
