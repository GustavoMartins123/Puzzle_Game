using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Slot : MonoBehaviour, IDropHandler
{
    [SerializeField] private Image image;
    [SerializeField] private SlotFeedback feedback;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private Action<Slot> filled;
    private int id;

    public int Id => id;

    public RectTransform RectTransform => rectTransform;

    public SlotFeedback Feedback => feedback;

    private void Awake() => rectTransform = (RectTransform)transform;

    public void Setup(int id, DragLayer dragLayer, Action<Slot> filled)
    {
        this.id = id;
        this.dragLayer = dragLayer;
        this.filled = filled;
    }

    public void OnDrop(PointerEventData eventData)
    {
        PuzzlePiece piece = dragLayer.Held;
        if (piece == null || piece.Id != id) return;

        Vector3 origin = piece.RectTransform.position;

        dragLayer.Release();
        piece.PlaceInto(this);
        image.raycastTarget = false;
        feedback.PlayFilled(piece, origin);
        filled?.Invoke(this);
    }
}
