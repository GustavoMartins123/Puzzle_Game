using UnityEngine;

public class PuzzleController : MonoBehaviour
{
    [SerializeField] private PuzzleConfig config;
    [SerializeField] private BoardBuilder builder;
    [SerializeField] private DragLayer dragLayer;
    [SerializeField] private GameMenu menu;

    private GameInput input;
    private int totalPieces;
    private int placedPieces;

    private void Awake()
    {
        input = new GameInput();
        input.PauseToggled += menu.TogglePause;
        menu.Opened += CancelDrag;
    }

    private void Start() => totalPieces = builder.Build(config, dragLayer, OnSlotFilled);

    private void Update()
    {
        if (dragLayer.IsDragging) dragLayer.Follow(input.PointerPosition);
    }

    private void OnDestroy()
    {
        menu.Opened -= CancelDrag;
        if (input == null) return;
        input.PauseToggled -= menu.TogglePause;
        input.Dispose();
    }

    private void OnSlotFilled(Slot slot)
    {
        placedPieces++;
        if (placedPieces >= totalPieces) menu.ShowWin();
    }

    private void CancelDrag()
    {
        if (!dragLayer.IsDragging) return;
        dragLayer.Release().ReturnToTray();
    }
}
