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
    private readonly PuzzleSessionMetrics metrics = new PuzzleSessionMetrics();
    private readonly PuzzleHintController hints = new PuzzleHintController();
    private PuzzleHud hud;
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
        if (!started) return;
        if (dragLayer.IsDragging) dragLayer.Follow(input.PointerPosition);
        if (transitioning || menu.IsOpen) return;

        metrics.Tick(Time.unscaledDeltaTime);
        hints.Tick(Time.unscaledDeltaTime);
        hud.Refresh(metrics, currentSession.Difficulty, hints);
    }

    private void OnDestroy()
    {
        menu.Opened -= CancelDrag;
        menu.RetryRequested -= RetryCurrentSession;
        menu.OptionsRequested -= ReturnToOptions;
        if (input != null)
        {
            input.PauseToggled -= TogglePause;
            input.Dispose();
        }
        if (hud != null) hud.Close();
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
        int builtPieces = builder.Build(
            session,
            dragLayer,
            OnSlotFilled,
            OnMoveStarted,
            OnIncorrectAttempt);
        if (builtPieces <= 0)
        {
            CloseSessionUi();
            return false;
        }

        try
        {
            metrics.Begin(builtPieces);
            hints.Configure(
                session.Difficulty,
                builder.Pieces,
                builder.Slots,
                builder.TrayController,
                metrics);
            if (hud == null) hud = PuzzleHud.Show(selectionRoot, UseHint);
            hud.Refresh(metrics, session.Difficulty, hints);
            started = true;
            return true;
        }
        catch (System.Exception exception)
        {
            builder.ClearBoard();
            CloseSessionUi();
            Debug.LogError(
                $"PuzzleController: session interface could not be created: {exception.Message}",
                this);
            return false;
        }
    }

    private void TogglePause()
    {
        if (started && !transitioning) menu.TogglePause();
    }

    private void RetryCurrentSession()
    {
        if (!started || currentSession == null)
            throw new System.InvalidOperationException("There is no active puzzle session to retry.");
        BeginTransition(RestartWithDifferentImage());
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
        hints.Reset();
        metrics.Reset();
        builder.ClearBoard();
        StartCoroutine(routine);
    }

    private IEnumerator RestartWithDifferentImage()
    {
        yield return null;

        Texture2D nextTexture;
        PuzzleSessionDefinition nextSession;
        try
        {
            nextTexture = config.PickDifferentImage(currentSession.Texture);
            nextSession = new PuzzleSessionDefinition(
                currentSession.Difficulty,
                currentSession.Layout,
                currentSession.CutStyle,
                nextTexture,
                config.CreateCutSeed(),
                config.CreateTraySeed());
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"PuzzleController: retry could not create a new session: {exception.Message}", this);
            CloseSessionUi();
            transitioning = false;
            yield break;
        }

        if (!BuildSession(nextSession))
        {
            transitioning = false;
            yield break;
        }

        currentSession = nextSession;
        puzzleTexture = nextTexture;
        transitioning = false;
    }

    private IEnumerator ClearThenShowOptions()
    {
        yield return null;

        PuzzleDifficultyProfile difficulty = currentSession.Difficulty;
        PuzzleCutStyle cutStyle = currentSession.CutStyle;
        if (hud != null)
        {
            hud.Close();
            hud = null;
        }
        transitioning = false;
        ShowOptions(difficulty, cutStyle);
    }

    private void OnSlotFilled(Slot slot)
    {
        if (slot == null) throw new System.ArgumentNullException(nameof(slot));
        metrics.RecordCorrectPlacement();
        hud.Refresh(metrics, currentSession.Difficulty, hints);
        if (metrics.IsComplete) StartCoroutine(CelebrateThenFinish());
    }

    private void OnMoveStarted() => metrics.RecordMoveStarted();

    private void OnIncorrectAttempt() => metrics.RecordIncorrectAttempt();

    private void UseHint()
    {
        if (!started || transitioning || menu.IsOpen)
            throw new System.InvalidOperationException("Hints require an active unpaused session.");
        if (dragLayer.IsDragging)
        {
            hud.ShowStatus("Solte a peça antes de pedir uma dica.");
            return;
        }

        hints.TryUseHint(out string message);
        hud.ShowStatus(message);
        hud.Refresh(metrics, currentSession.Difficulty, hints);
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

    private void CloseSessionUi()
    {
        started = false;
        hints.Reset();
        metrics.Reset();
        if (hud == null) return;
        hud.Close();
        hud = null;
    }
}
