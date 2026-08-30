using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Slot : MonoBehaviour, IDropHandler
{
    [SerializeField] private Image image;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private Action<Slot> filled;
    private int id;

    public int Id => id;

    public RectTransform RectTransform => rectTransform;

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

        dragLayer.Release();
        piece.PlaceInto(this);
        image.raycastTarget = false;
        filled?.Invoke(this);
    }
}
