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
    private Texture2D puzzleTexture;
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

        try
        {
            puzzleTexture = config.PickImage();
            selectionMenu = PuzzleCutSelectionMenu.Show(
                selectionRoot,
                config.CutStyle,
                config.CutDepth,
                puzzleTexture,
                StartPuzzle);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"PuzzleController: cut selection could not be created: {exception.Message}", this);
        }
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

        if (puzzleTexture == null)
            throw new System.InvalidOperationException("The selected puzzle texture is missing.");

        int builtPieces = builder.Build(config, cutStyle, puzzleTexture, dragLayer, OnSlotFilled);
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
