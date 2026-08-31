using System;
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
    private PuzzleContentSelectionMenu contentMenu;
    private PuzzleCutSelectionMenu selectionMenu;
    private PuzzleImageDefinition selectedImage;
    private Texture2D puzzleTexture;
    private PuzzleSessionDefinition currentSession;
    private PuzzleProgressStore progressStore;
    private PuzzleProgressData progress;
    private PuzzleDifficultyProfile pendingDifficulty;
    private PuzzleCutStyle pendingStyle;
    private readonly PuzzleSessionMetrics metrics = new PuzzleSessionMetrics();
    private readonly PuzzleHintController hints = new PuzzleHintController();
    private PuzzleHud hud;
    private PuzzleAchievementCenter achievementCenter;
    private PuzzleAchievementPopupQueue achievementPopups;
    [NonSerialized] private bool started;
    [NonSerialized] private bool transitioning;

    private void Awake()
    {
        if (builder == null || dragLayer == null || menu == null)
            throw new InvalidOperationException("PuzzleController scene references are incomplete.");

        input = new GameInput();
        input.PauseToggled += TogglePause;
        input.RotationRequested += RotateHeldPiece;
        menu.Opened += CancelDrag;
        menu.RetryRequested += RetryCurrentSession;
        menu.OptionsRequested += ReturnToOptions;
        menu.RepeatSeedRequested += RepeatCurrentSession;
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
            progressStore = new PuzzleProgressStore();
            progress = progressStore.LoadOrCreate(config);
            PuzzleDifficultyProfile difficulty =
                config.GetDifficulty(progress.LastSelectedDifficulty);
            ShowContentSelection(difficulty, progress.LastSelectedCutStyle);
            achievementCenter = PuzzleAchievementCenter.Show(
                selectionRoot,
                LoadAchievementCatalog,
                CancelDrag);
            achievementPopups = PuzzleAchievementPopupQueue.Show(
                selectionRoot,
                AcknowledgeAchievementNotification);
            ShowPendingAchievementNotifications();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"PuzzleController: progress initialization failed: {exception.Message}",
                this);
            menu.ShowFailure(
                "ERRO AO CARREGAR",
                "O progresso salvo é inválido e o jogo não criou dados substitutos.\n\n" +
                exception.Message);
        }
    }

    private void Update()
    {
        if (!started) return;
        if (!metrics.IsActive || currentSession == null)
        {
            FailClosedSession(
                "A sessao ativa perdeu o estado interno de metricas. " +
                "Reinicie a partida para continuar.");
            return;
        }
        if (transitioning || menu.IsOpen || IsAchievementUiOpen) return;

        metrics.Tick(Time.unscaledDeltaTime);
        hints.Tick(Time.unscaledDeltaTime);
        hud.Refresh(metrics, currentSession.Difficulty, hints);
    }

    private void OnDestroy()
    {
        menu.Opened -= CancelDrag;
        menu.RetryRequested -= RetryCurrentSession;
        menu.OptionsRequested -= ReturnToOptions;
        menu.RepeatSeedRequested -= RepeatCurrentSession;
        if (input != null)
        {
            input.PauseToggled -= TogglePause;
            input.RotationRequested -= RotateHeldPiece;
            input.Dispose();
        }
        if (progressStore != null)
        {
            progressStore.Dispose();
            progressStore = null;
        }
        if (hud != null) hud.Close();
        if (achievementCenter != null)
        {
            achievementCenter.Close();
            achievementCenter = null;
        }
        if (achievementPopups != null)
        {
            achievementPopups.Close();
            achievementPopups = null;
        }
    }

    private void ShowContentSelection(
        PuzzleDifficultyProfile preferredDifficulty,
        PuzzleCutStyle preferredStyle)
    {
        if (contentMenu != null || selectionMenu != null)
            throw new InvalidOperationException("A puzzle selection menu is already open.");
        if (progress == null)
            throw new InvalidOperationException("Puzzle progress is not loaded.");
        if (!progress.IsDifficultyUnlocked(config, preferredDifficulty))
            throw new InvalidOperationException(
                $"Preferred difficulty {preferredDifficulty.DisplayName} is locked.");
        if (!preferredDifficulty.AllowsStyle(preferredStyle) ||
            !progress.IsStyleUnlocked(config, preferredStyle))
            throw new InvalidOperationException(
                $"Preferred cut style {preferredStyle} is unavailable.");

        pendingDifficulty = preferredDifficulty;
        pendingStyle = preferredStyle;
        contentMenu = PuzzleContentSelectionMenu.Show(
            selectionRoot,
            config,
            progress,
            progress.LastSelectedImageId,
            SelectContent);
    }

    private bool SelectContent(PuzzleImageDefinition image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (contentMenu == null)
            throw new InvalidOperationException("Content selection menu is not open.");

        try
        {
            Texture2D texture = config.LoadImage(image);
            progress.SelectImage(config, image.Id);
            progressStore.Save(progress, config);
            selectedImage = image;
            puzzleTexture = texture;
            ShowOptions(pendingDifficulty, pendingStyle);
            contentMenu = null;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"PuzzleController: image selection failed: {exception.Message}",
                this);
            contentMenu.ShowError(exception.Message);
            return false;
        }
    }

    private void ShowOptions(
        PuzzleDifficultyProfile initialDifficulty,
        PuzzleCutStyle initialStyle)
    {
        if (selectionMenu != null)
            throw new InvalidOperationException("The puzzle options menu is already open.");
        if (selectedImage == null || puzzleTexture == null)
            throw new InvalidOperationException("The selected puzzle image is missing.");

        selectionMenu = PuzzleCutSelectionMenu.Show(
            selectionRoot,
            config.DifficultyProfiles,
            initialDifficulty,
            initialStyle,
            puzzleTexture,
            config,
            progress,
            StartPuzzle);
    }

    private bool StartPuzzle(
        PuzzleDifficultyProfile difficulty,
        PuzzleCutStyle cutStyle)
    {
        if (started)
            throw new InvalidOperationException("The puzzle has already started.");
        if (transitioning)
            throw new InvalidOperationException("A puzzle transition is already running.");
        if (selectedImage == null || puzzleTexture == null)
            throw new InvalidOperationException("The selected puzzle image is missing.");
        if (!progress.IsDifficultyUnlocked(config, difficulty))
            throw new InvalidOperationException(
                $"Difficulty {difficulty.DisplayName} is locked.");
        if (!progress.IsStyleUnlocked(config, cutStyle))
            throw new InvalidOperationException($"Cut style {cutStyle} is locked.");

        string progressSnapshot = PuzzleProgressSerializer.Serialize(progress, config);
        try
        {
            var session = new PuzzleSessionDefinition(
                difficulty,
                difficulty.PickLayout(),
                cutStyle,
                selectedImage.Id,
                puzzleTexture,
                config.CreateCutSeed(),
                config.CreateTraySeed());
            if (!BuildSession(session))
                throw new InvalidOperationException("The puzzle board could not be built.");

            progress.SelectRules(config, difficulty, cutStyle);
            progressStore.Save(progress, config);
            currentSession = session;
            selectionMenu = null;
            return true;
        }
        catch (Exception exception)
        {
            progress = PuzzleProgressSerializer.Deserialize(progressSnapshot, config);
            builder.ClearBoard();
            CloseSessionUi();
            Debug.LogError(
                $"PuzzleController: session could not start: {exception.Message}",
                this);
            selectionMenu.ShowError(exception.Message);
            return false;
        }
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
            if (session.Difficulty.RotationEnabled)
                hud.ShowStatus(
                    "ROTAÇÃO: Q/E, botão direito ou ombros; no toque, toque breve na peça.");
            started = true;
            return true;
        }
        catch (Exception exception)
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
        if (achievementCenter != null && achievementCenter.IsOpen)
        {
            achievementCenter.ClosePanel();
            return;
        }
        if (started && !transitioning && !IsAchievementUiOpen) menu.TogglePause();
    }

    private void RetryCurrentSession()
    {
        RequireActiveSession("retry");
        BeginTransition(RestartWithDifferentImage());
    }

    private void RepeatCurrentSession()
    {
        RequireActiveSession("repeat");
        BeginTransition(RestartExactSession());
    }

    private void ReturnToOptions()
    {
        RequireActiveSession("leave");
        BeginTransition(ClearThenShowContent());
    }

    private void RequireActiveSession(string action)
    {
        if (!started || currentSession == null)
            throw new InvalidOperationException(
                $"There is no active puzzle session to {action}.");
    }

    private void BeginTransition(IEnumerator routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));
        if (transitioning)
            throw new InvalidOperationException("A puzzle transition is already running.");

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

        string progressSnapshot = PuzzleProgressSerializer.Serialize(progress, config);
        try
        {
            PuzzleImageDefinition nextImage =
                config.PickDifferentUnlockedImage(currentSession.ImageId, progress);
            Texture2D nextTexture = config.LoadImage(nextImage);
            var nextSession = new PuzzleSessionDefinition(
                currentSession.Difficulty,
                currentSession.Layout,
                currentSession.CutStyle,
                nextImage.Id,
                nextTexture,
                config.CreateCutSeed(),
                config.CreateTraySeed());

            progress.SelectImage(config, nextImage.Id);
            progressStore.Save(progress, config);
            if (!BuildSession(nextSession))
                throw new InvalidOperationException("The retry board could not be built.");

            selectedImage = nextImage;
            currentSession = nextSession;
            puzzleTexture = nextTexture;
            transitioning = false;
        }
        catch (Exception exception)
        {
            Exception reportedException = exception;
            try
            {
                progress = PuzzleProgressSerializer.Deserialize(progressSnapshot, config);
                progressStore.Save(progress, config);
            }
            catch (Exception rollbackException)
            {
                reportedException = new AggregateException(
                    "Retry failed and its progress transaction could not be rolled back.",
                    exception,
                    rollbackException);
            }
            Debug.LogError(
                "PuzzleController: retry could not create a new session: " +
                reportedException.Message,
                this);
            builder.ClearBoard();
            CloseSessionUi();
            transitioning = false;
            menu.ShowFailure(
                "ERRO NO RETRY",
                "A nova sessão não foi criada.\n\n" + reportedException.Message);
        }
    }

    private IEnumerator RestartExactSession()
    {
        yield return null;

        try
        {
            if (!BuildSession(currentSession))
                throw new InvalidOperationException("The repeated board could not be built.");

            selectedImage = config.GetImage(currentSession.ImageId);
            puzzleTexture = currentSession.Texture;
            transitioning = false;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"PuzzleController: exact replay failed: {exception.Message}",
                this);
            CloseSessionUi();
            transitioning = false;
            menu.ShowFailure(
                "ERRO NA REPETIÇÃO",
                "A sessão com a mesma seed não foi criada.\n\n" + exception.Message);
        }
    }

    private IEnumerator ClearThenShowContent()
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
        ShowContentSelection(difficulty, cutStyle);
    }

    private void OnSlotFilled(Slot slot)
    {
        if (slot == null) throw new ArgumentNullException(nameof(slot));
        metrics.RecordCorrectPlacement();
        hud.Refresh(metrics, currentSession.Difficulty, hints);
        if (metrics.IsComplete) StartCoroutine(CelebrateThenFinish());
    }

    private void OnMoveStarted() => metrics.RecordMoveStarted();

    private void OnIncorrectAttempt(PuzzleDropFailure failure)
    {
        metrics.RecordIncorrectAttempt();
        hud.ShowStatus(failure switch
        {
            PuzzleDropFailure.WrongSlot => "Esta peça pertence a outra região.",
            PuzzleDropFailure.WrongRotation => "Posição correta: ajuste a rotação.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        });
    }

    private void RotateHeldPiece(int direction)
    {
        if (!started || transitioning || menu.IsOpen || IsAchievementUiOpen) return;
        PuzzlePiece piece = dragLayer.Held;
        if (piece == null)
        {
            if (currentSession.Difficulty.RotationEnabled)
                hud.ShowStatus("Segure uma peça para girar com Q/E, botão direito ou controle.");
            return;
        }

        if (!piece.RotationEnabled)
        {
            hud.ShowStatus("Rotação desativada neste perfil.");
            return;
        }

        piece.Rotate(direction);
        hud.ShowStatus($"Rotação: {Mathf.RoundToInt(piece.RotationDegrees)}°.");
    }

    private void UseHint()
    {
        if (!started || transitioning || menu.IsOpen || IsAchievementUiOpen)
            throw new InvalidOperationException("Hints require an active unpaused session.");
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
        PuzzleScoreBreakdown score = PuzzleScoreCalculator.Calculate(currentSession, metrics);
        string progressSnapshot = PuzzleProgressSerializer.Serialize(progress, config);
        PuzzleProgressUpdate progressUpdate;
        try
        {
            progressUpdate = progress.RecordCompletion(config, currentSession, metrics, score);
            progressStore.CompleteSession(
                progress,
                config,
                currentSession,
                metrics,
                score,
                progressUpdate);
        }
        catch (Exception exception)
        {
            progress = PuzzleProgressSerializer.Deserialize(progressSnapshot, config);
            Debug.LogError(
                $"PuzzleController: completed result was not saved: {exception.Message}",
                this);
            menu.ShowFailure(
                "ERRO AO SALVAR",
                "O progresso não foi alterado.\n\n" + exception.Message);
            yield break;
        }

        float duration = builder.PlayCompletionWave();
        yield return new WaitForSecondsRealtime(duration + winDelay);
        menu.ShowWin(score, progressUpdate);
        ShowPendingAchievementNotifications();
    }

    private void ShowPendingAchievementNotifications()
    {
        if (achievementPopups == null)
            throw new InvalidOperationException("Achievement popup queue is not initialized.");
        achievementPopups.Enqueue(
            progressStore.GetPendingAchievementNotifications(config));
    }

    private System.Collections.Generic.IReadOnlyList<PuzzleAchievementCatalogEntry>
        LoadAchievementCatalog()
    {
        if (progressStore == null)
            throw new InvalidOperationException("Progress store is not initialized.");
        return progressStore.GetAchievementCatalog(config);
    }

    private void AcknowledgeAchievementNotification(long notificationId)
    {
        if (progressStore == null)
            throw new InvalidOperationException("Progress store is not initialized.");
        progressStore.AcknowledgeAchievementNotification(notificationId);
    }

    private bool IsAchievementUiOpen =>
        (achievementCenter != null && achievementCenter.IsOpen) ||
        (achievementPopups != null && achievementPopups.IsOpen);

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

    private void FailClosedSession(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A session failure message is required.", nameof(message));

        Debug.LogError($"PuzzleController: {message}", this);
        StopAllCoroutines();
        transitioning = false;
        CancelDrag();
        builder.ClearBoard();
        CloseSessionUi();
        currentSession = null;
        menu.ShowFailure("ERRO DE SESSAO", message);
    }
}
