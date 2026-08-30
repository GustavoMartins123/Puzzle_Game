using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image image;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private RectTransform tray;
    private Vector2 trayAnchor;
    private Quaternion trayRotation;
    private int id;
    private bool placed;

    public int Id => id;

    public bool Placed => placed;

    private void Awake() => rectTransform = (RectTransform)transform;

    public void Setup(int id, Sprite sprite, float cellSize, DragLayer dragLayer, RectTransform tray)
    {
        this.id = id;
        this.dragLayer = dragLayer;
        this.tray = tray;
        image.sprite = sprite;
        rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
    }

    public void ScatterTo(Vector2 anchor, float tiltDegrees)
    {
        trayAnchor = anchor;
        trayRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        ReturnToTray();
    }

    public void AttachTo(RectTransform parent)
    {
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.one * 0.5f;
        rectTransform.anchorMax = Vector2.one * 0.5f;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
    }

    public void ReturnToTray()
    {
        image.raycastTarget = true;
        rectTransform.SetParent(tray, false);
        rectTransform.anchorMin = trayAnchor;
        rectTransform.anchorMax = trayAnchor;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = trayRotation;
    }

    public void PlaceInto(Slot slot)
    {
        placed = true;
        image.raycastTarget = false;
        AttachTo(slot.RectTransform);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placed || dragLayer.IsDragging) return;
        image.raycastTarget = false;
        dragLayer.Take(this, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragLayer.Held != this) return;
        dragLayer.Release();
        ReturnToTray();
    }
}
