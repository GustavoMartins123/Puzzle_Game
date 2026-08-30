using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(ProceduralPuzzleImage))]
public class PuzzlePiece : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler
{
    [SerializeField] private PieceFeedback feedback;

    private RectTransform rectTransform;
    private DragLayer dragLayer;
    private PieceTrayController tray;
    private int id;
    private bool placed;
    private bool rejected;
    private ProceduralPuzzleImage image;
    private Action moveStarted;
    private PuzzlePieceCategory category;
    private bool attempted;
    private bool rotationEnabled;
    private float rotationStepDegrees;
    private float rotationToleranceDegrees;
    private float rotationDegrees;
    private float rotationAtDragStart;

    public int Id => id;

    public bool Placed => placed;

    public RectTransform RectTransform => rectTransform;

    public Image Image => image;

    public PuzzlePieceCategory Category => category;

    public bool Attempted => attempted;

    public bool RotationEnabled => rotationEnabled;

    public float RotationDegrees => rotationDegrees;

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
        PieceTrayController tray,
        PuzzlePieceCategory category,
        bool rotationEnabled,
        float rotationStepDegrees,
        float rotationToleranceDegrees,
        float initialRotationDegrees,
        Action moveStarted)
    {
        if (dragLayer == null) throw new System.ArgumentNullException(nameof(dragLayer));
        if (tray == null) throw new System.ArgumentNullException(nameof(tray));
        if (moveStarted == null) throw new ArgumentNullException(nameof(moveStarted));
        if (!Enum.IsDefined(typeof(PuzzlePieceCategory), category))
            throw new ArgumentOutOfRangeException(nameof(category));
        if (!float.IsFinite(rotationStepDegrees) || !float.IsFinite(rotationToleranceDegrees) ||
            !float.IsFinite(initialRotationDegrees))
            throw new ArgumentException("Rotation values must be finite.");
        if (!rotationEnabled &&
            (rotationStepDegrees != 0f || rotationToleranceDegrees != 0f ||
             initialRotationDegrees != 0f))
            throw new ArgumentException("Disabled rotation requires zero rotation values.");
        if (rotationEnabled &&
            (rotationStepDegrees <= 0f || rotationToleranceDegrees <= 0f ||
             rotationToleranceDegrees >= rotationStepDegrees * 0.5f))
            throw new ArgumentException("Enabled rotation values are invalid.");

        this.id = id;
        this.dragLayer = dragLayer;
        this.tray = tray;
        this.category = category;
        this.rotationEnabled = rotationEnabled;
        this.rotationStepDegrees = rotationStepDegrees;
        this.rotationToleranceDegrees = rotationToleranceDegrees;
        rotationDegrees = NormalizeRotation(initialRotationDegrees);
        this.moveStarted = moveStarted;
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
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
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
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees + tiltDegrees);
        rectTransform.localScale = Vector3.one * scale;
    }

    public void MarkRejected()
    {
        rejected = true;
        rotationDegrees = rotationAtDragStart;
    }

    public void PlayHint() => feedback.PlayHint();

    public bool IsRotationValid() =>
        !rotationEnabled ||
        Mathf.Abs(Mathf.DeltaAngle(rotationDegrees, 0f)) <= rotationToleranceDegrees;

    public void Rotate(int direction)
    {
        if (!rotationEnabled)
            throw new InvalidOperationException("Rotation is disabled for this piece.");
        if (placed) throw new InvalidOperationException("Placed pieces cannot be rotated.");
        if (direction != -1 && direction != 1)
            throw new ArgumentOutOfRangeException(nameof(direction));

        rotationDegrees = NormalizeRotation(rotationDegrees + rotationStepDegrees * direction);
        tray.RefreshPieceRotation(this);
        feedback.PlayRotation();
    }

    public void ReturnToTray()
    {
        rejected = false;
        image.raycastTarget = true;
        tray.ReturnToCell(this);
    }

    public void PlaceInto(Slot slot)
    {
        placed = true;
        rotationDegrees = 0f;
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
            attempted = true;
            rotationAtDragStart = rotationDegrees;
            tray.NotifyAttempted(this);
            dragLayer.Take(this, eventData.position);
            feedback.PlaySelected();
            moveStarted.Invoke();
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
        if (dragLayer.Held == this) dragLayer.Move(eventData.position);
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!rotationEnabled || placed) return;
        if (eventData is ExtendedPointerEventData pointerData &&
            pointerData.pointerType == UIPointerType.Touch)
            Rotate(1);
    }

    private static float NormalizeRotation(float value)
    {
        float normalized = Mathf.Repeat(value, 360f);
        return Mathf.Approximately(normalized, 360f) ? 0f : normalized;
    }
}
