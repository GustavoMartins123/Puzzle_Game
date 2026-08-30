using System.Collections;
using UnityEngine;

public class PuzzleController : MonoBehaviour
{
    [SerializeField] private PuzzleConfig config;
    [SerializeField] private BoardBuilder builder;
    [SerializeField] private DragLayer dragLayer;
    [SerializeField] private GameMenu menu;
    [SerializeField] private RectTransform selectionRoot;
    [SerializeField] private float winDelay = 0.35f;

    private GameInput input;
    private PuzzleCutSelectionMenu selectionMenu;
    private int totalPieces;
    private int placedPieces;
    private bool started;

    private void Awake()
    {
        input = new GameInput();
        input.PauseToggled += TogglePause;
        menu.Opened += CancelDrag;
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("PuzzleController: assign a PuzzleConfig.", this);
            return;
        }

        if (!config.TryValidate(out string error))
        {
            Debug.LogError($"PuzzleController: invalid configuration: {error}.", config);
            return;
        }

        if (selectionRoot == null)
        {
            Debug.LogError("PuzzleController: assign the selection UI root.", this);
            return;
        }

        selectionMenu = PuzzleCutSelectionMenu.Show(
            selectionRoot,
            config.CutStyle,
            config.CutDepth,
            StartPuzzle);
    }

    private void Update()
    {
        if (started && dragLayer.IsDragging) dragLayer.Follow(input.PointerPosition);
    }

    private void OnDestroy()
    {
        menu.Opened -= CancelDrag;
        if (input == null) return;
        input.PauseToggled -= TogglePause;
        input.Dispose();
    }

    private bool StartPuzzle(PuzzleCutStyle cutStyle)
    {
        if (started)
            throw new System.InvalidOperationException("The puzzle has already started.");

        int builtPieces = builder.Build(config, cutStyle, dragLayer, OnSlotFilled);
        if (builtPieces <= 0) return false;

        totalPieces = builtPieces;
        placedPieces = 0;
        started = true;
        selectionMenu = null;
        return true;
    }

    private void TogglePause()
    {
        if (started) menu.TogglePause();
    }

    private void OnSlotFilled(Slot slot)
    {
        placedPieces++;
        if (placedPieces >= totalPieces) StartCoroutine(CelebrateThenFinish());
    }

    private IEnumerator CelebrateThenFinish()
    {
        float duration = builder.PlayCompletionWave();
        yield return new WaitForSecondsRealtime(duration + winDelay);
        menu.ShowWin();
    }

    private void CancelDrag()
    {
        if (!dragLayer.IsDragging) return;
        dragLayer.Release().ReturnToTray();
    }
}
