using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ProceduralPuzzleImage))]
public class PuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private ProceduralPuzzleImage image;
    [SerializeField] private PieceFeedback feedback;
    [SerializeField] private float dropMargin = 0.05f;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private RectTransform tray;
    private Vector2 trayAnchor;
    private Quaternion trayRotation;
    private int id;
    private bool placed;
    private bool rejected;

    public int Id => id;

    public bool Placed => placed;

    public RectTransform RectTransform => rectTransform;

    public Image Image => image;

    private void Awake() => rectTransform = (RectTransform)transform;

    public void Setup(
        int id,
        Sprite sprite,
        ProceduralPuzzleGeometry geometry,
        Vector2 cellSize,
        float cutPadding,
        DragLayer dragLayer,
        RectTransform tray)
    {
        if (image == null) throw new System.InvalidOperationException("PuzzlePiece requires a ProceduralPuzzleImage.");
        if (dragLayer == null) throw new System.ArgumentNullException(nameof(dragLayer));
        if (tray == null) throw new System.ArgumentNullException(nameof(tray));

        this.id = id;
        this.dragLayer = dragLayer;
        this.tray = tray;
        image.Configure(sprite, geometry, true, Vector2.one);
        rectTransform.sizeDelta = Vector2.Scale(cellSize, Vector2.one * (1f + cutPadding * 2f));
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

    public void MarkRejected() => rejected = true;

    public void ReturnToTray()
    {
        rejected = false;
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
        feedback.Stop();
        image.raycastTarget = false;
        dragLayer.Take(this, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragLayer.Held != this) return;

        bool wasRejected = rejected;
        Vector3 origin = rectTransform.position;

        dragLayer.Release();
        if (!wasRejected) MoveHomeTo(origin, eventData.pressEventCamera);
        ReturnToTray();

        if (wasRejected) feedback.PlayReturn(origin);
        else feedback.PlayDrop();
    }

    private void MoveHomeTo(Vector3 worldPoint, Camera eventCamera)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPoint);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tray, screenPoint, eventCamera, out Vector2 local)) return;

        Rect area = tray.rect;
        float limit = 1f - dropMargin;

        trayAnchor = new Vector2(
            Mathf.Clamp(Mathf.InverseLerp(area.xMin, area.xMax, local.x), dropMargin, limit),
            Mathf.Clamp(Mathf.InverseLerp(area.yMin, area.yMax, local.y), dropMargin, limit));
        trayRotation = Quaternion.identity;
    }
}
