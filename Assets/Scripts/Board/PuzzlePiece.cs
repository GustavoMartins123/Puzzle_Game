using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ProceduralPuzzleImage))]
public class PuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private PieceFeedback feedback;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private PieceTrayController tray;
    private int id;
    private bool placed;
    private bool rejected;
    private ProceduralPuzzleImage image;

    public int Id => id;

    public bool Placed => placed;

    public RectTransform RectTransform => rectTransform;

    public Image Image => image;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        image = GetComponent<ProceduralPuzzleImage>();
        if (image == null)
            throw new System.InvalidOperationException("PuzzlePiece requires a ProceduralPuzzleImage.");
    }

    public void Setup(
        int id,
        Sprite sprite,
        ProceduralPuzzleGeometry geometry,
        Vector2 cellSize,
        float cutPadding,
        DragLayer dragLayer,
        PieceTrayController tray)
    {
        if (dragLayer == null) throw new System.ArgumentNullException(nameof(dragLayer));
        if (tray == null) throw new System.ArgumentNullException(nameof(tray));

        this.id = id;
        this.dragLayer = dragLayer;
        this.tray = tray;
        image.Configure(sprite, geometry, true, Vector2.one);
        rectTransform.sizeDelta = Vector2.Scale(cellSize, Vector2.one * (1f + cutPadding * 2f));
    }

    public void AttachTo(RectTransform parent)
    {
        if (parent == null) throw new System.ArgumentNullException(nameof(parent));
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.one * 0.5f;
        rectTransform.anchorMax = Vector2.one * 0.5f;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    internal void AssignTray(PieceTrayController controller)
    {
        if (controller == null) throw new System.ArgumentNullException(nameof(controller));
        if (controller != tray)
            throw new System.InvalidOperationException("PuzzlePiece received a different tray controller.");
    }

    internal void AttachToTrayCell(RectTransform cell, float scale, float tiltDegrees)
    {
        if (cell == null) throw new System.ArgumentNullException(nameof(cell));
        if (!float.IsFinite(scale) || scale <= 0f || scale > 1f)
            throw new System.ArgumentOutOfRangeException(nameof(scale));
        if (!float.IsFinite(tiltDegrees))
            throw new System.ArgumentOutOfRangeException(nameof(tiltDegrees));

        rectTransform.SetParent(cell, false);
        rectTransform.anchorMin = Vector2.one * 0.5f;
        rectTransform.anchorMax = Vector2.one * 0.5f;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        rectTransform.localScale = Vector3.one * scale;
    }

    public void MarkRejected() => rejected = true;

    public void ReturnToTray()
    {
        rejected = false;
        image.raycastTarget = true;
        tray.ReturnToCell(this);
    }

    public void PlaceInto(Slot slot)
    {
        placed = true;
        image.raycastTarget = false;
        AttachTo(slot.RectTransform);
        tray.CommitPlacement(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placed || dragLayer.IsDragging) return;
        feedback.Stop();
        image.raycastTarget = false;
        tray.BeginDrag(this);
        try
        {
            dragLayer.Take(this, eventData.position);
        }
        catch
        {
            image.raycastTarget = true;
            tray.ReturnToCell(this);
            throw;
        }
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
        ReturnToTray();

        if (wasRejected) feedback.PlayReturn(origin);
        else feedback.PlayDrop();
    }
}
