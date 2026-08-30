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
    private PuzzleSessionDefinition currentSession;
    private int totalPieces;
    private int placedPieces;
    private bool started;
    private bool transitioning;

    private void Awake()
    {
        input = new GameInput();
        input.PauseToggled += TogglePause;
        menu.Opened += CancelDrag;
        menu.RetryRequested += RetryCurrentSession;
        menu.OptionsRequested += ReturnToOptions;
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
            PuzzleDifficultyProfile difficulty = config.DefaultDifficulty;
            ShowOptions(difficulty, difficulty.DefaultCutStyle);
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
        menu.RetryRequested -= RetryCurrentSession;
        menu.OptionsRequested -= ReturnToOptions;
        if (input == null) return;
        input.PauseToggled -= TogglePause;
        input.Dispose();
    }

    private void ShowOptions(
        PuzzleDifficultyProfile initialDifficulty,
        PuzzleCutStyle initialStyle)
    {
        if (selectionMenu != null)
            throw new System.InvalidOperationException("The puzzle options menu is already open.");
        if (puzzleTexture == null)
            throw new System.InvalidOperationException("The selected puzzle texture is missing.");

        selectionMenu = PuzzleCutSelectionMenu.Show(
            selectionRoot,
            config.DifficultyProfiles,
            initialDifficulty,
            initialStyle,
            puzzleTexture,
            StartPuzzle);
    }

    private bool StartPuzzle(
        PuzzleDifficultyProfile difficulty,
        PuzzleCutStyle cutStyle)
    {
        if (started)
            throw new System.InvalidOperationException("The puzzle has already started.");
        if (transitioning)
            throw new System.InvalidOperationException("A puzzle transition is already running.");

        if (puzzleTexture == null)
            throw new System.InvalidOperationException("The selected puzzle texture is missing.");

        var session = new PuzzleSessionDefinition(
            difficulty,
            difficulty.PickLayout(),
            cutStyle,
            puzzleTexture,
            config.CreateCutSeed(),
            config.CreateTraySeed());
        if (!BuildSession(session)) return false;

        currentSession = session;
        selectionMenu = null;
        return true;
    }

    private bool BuildSession(PuzzleSessionDefinition session)
    {
        int builtPieces = builder.Build(session, dragLayer, OnSlotFilled);
        if (builtPieces <= 0) return false;

        totalPieces = builtPieces;
        placedPieces = 0;
        started = true;
        return true;
    }

    private void TogglePause()
    {
        if (started && !transitioning) menu.TogglePause();
    }

    private void RetryCurrentSession()
    {
        if (!started || currentSession == null)
            throw new System.InvalidOperationException("There is no active puzzle session to retry.");
        BeginTransition(RestartCurrentSession());
    }

    private void ReturnToOptions()
    {
        if (!started || currentSession == null)
            throw new System.InvalidOperationException("There is no active puzzle session to leave.");
        BeginTransition(ClearThenShowOptions());
    }

    private void BeginTransition(IEnumerator routine)
    {
        if (transitioning)
            throw new System.InvalidOperationException("A puzzle transition is already running.");

        StopAllCoroutines();
        transitioning = true;
        CancelDrag();
        started = false;
        totalPieces = 0;
        placedPieces = 0;
        builder.ClearBoard();
        StartCoroutine(routine);
    }

    private IEnumerator RestartCurrentSession()
    {
        yield return null;

        if (!BuildSession(currentSession))
        {
            transitioning = false;
            yield break;
        }

        transitioning = false;
    }

    private IEnumerator ClearThenShowOptions()
    {
        yield return null;

        PuzzleDifficultyProfile difficulty = currentSession.Difficulty;
        PuzzleCutStyle cutStyle = currentSession.CutStyle;
        transitioning = false;
        ShowOptions(difficulty, cutStyle);
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
