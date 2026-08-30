using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProceduralPuzzleValidator
{
    private const float CutDepth = 0.22f;

    [MenuItem("Puzzle/Validate Procedural Cuts")]
    public static void Run()
    {
        int boardsValidated = 0;
        int piecesValidated = 0;

        foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
        {
            int seedCount = SeedCount(style);
            float padding = style == PuzzleCutStyle.Square ? 0f : CutDepth;
            for (int seed = 1; seed <= seedCount; seed++)
            {
                for (int divisions = 2; divisions <= 5; divisions++)
                {
                    ProceduralPuzzleGeometry[,] board =
                        ProceduralPuzzleGenerator.Create(divisions, style, CutDepth, seed);
                    ValidateBoard(board, divisions, padding, style, seed);
                    boardsValidated++;
                    piecesValidated += divisions * divisions;
                }
            }
        }

        ValidateSelectionUi();
        ValidateContentSelectionUi();
        ValidateSessionDefinitions();
        ValidateSessionMetrics();
        ValidatePhaseFourRules();
        ValidatePhaseFiveProgression();
        ValidateTrayLayouts();
        ValidateProceduralPrefabs();
        ValidateGameScene();

        Debug.Log(
            $"Procedural puzzle validation passed: {boardsValidated} boards and " +
            $"{piecesValidated} pieces across every cut style; difficulty profiles, sessions, " +
            "retry image rotation, session metrics, deterministic scoring, optional piece " +
            "rotation, geometric tray filters, content selection, versioned progression, " +
            "transactional persistence, responsive trays, prefabs, and scene are valid.");
    }

    private static void ValidateSessionDefinitions()
    {
        PuzzleConfig config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(
            "Assets/Settings/PuzzleConfig.asset");
        if (config == null)
            throw new InvalidOperationException("Puzzle configuration is missing.");
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException(
                $"Puzzle configuration is invalid: {configError}.");

        var texture = new Texture2D(8, 8)
        {
            name = "ProceduralPuzzleSessionValidationTexture",
            hideFlags = HideFlags.HideAndDontSave,
        };

        try
        {
            for (int profileIndex = 0; profileIndex < config.DifficultyProfiles.Count; profileIndex++)
            {
                PuzzleDifficultyProfile profile = config.DifficultyProfiles[profileIndex];
                for (int layoutIndex = 0; layoutIndex < profile.Layouts.Count; layoutIndex++)
                {
                    PuzzleLayout layout = profile.Layouts[layoutIndex];
                    for (int styleIndex = 0; styleIndex < profile.AllowedCutStyles.Count; styleIndex++)
                    {
                        PuzzleCutStyle style = profile.AllowedCutStyles[styleIndex];
                        int cutSeed = 1000 + profileIndex * 100 + layoutIndex * 10 + styleIndex;
                        int traySeed = 2000 + profileIndex * 100 + layoutIndex * 10 + styleIndex;
                        var session = new PuzzleSessionDefinition(
                            profile,
                            layout,
                            style,
                            config.DefaultImageId,
                            texture,
                            cutSeed,
                            traySeed);

                        if (!session.TryValidate(out string sessionError))
                            throw new InvalidOperationException(
                                $"Valid session was rejected: {sessionError}.");
                        if (session.Difficulty != profile || !profile.ContainsLayout(session.Layout) ||
                            session.CutStyle != style || session.ImageId != config.DefaultImageId ||
                            session.Texture != texture ||
                            session.CutSeed != cutSeed || session.TraySeed != traySeed)
                            throw new InvalidOperationException(
                                "Puzzle session did not retain its exact immutable definition.");
                    }
                }
            }

            PuzzleProgressData progress = PuzzleProgressData.CreateNew(config);
            PuzzleImageDefinition initialImage = config.GetImage(config.DefaultImageId);
            PuzzleImageDefinition retryImage =
                config.PickDifferentUnlockedImage(initialImage.Id, progress);
            if (retryImage.Id == initialImage.Id)
                throw new InvalidOperationException(
                    "Retry image selection returned the current image.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidateSessionMetrics()
    {
        var metrics = new PuzzleSessionMetrics();
        metrics.Begin(2);
        metrics.Tick(1.5f);
        metrics.RecordMoveStarted();
        metrics.RecordIncorrectAttempt();
        metrics.RecordHint();
        metrics.RecordCorrectPlacement();

        if (!metrics.IsActive || metrics.IsComplete ||
            !Mathf.Approximately(metrics.ElapsedSeconds, 1.5f) ||
            metrics.MovesStarted != 1 || metrics.IncorrectAttempts != 1 ||
            metrics.HintsUsed != 1 || metrics.CorrectPlacements != 1 ||
            metrics.PlacedPieces != 1 || metrics.TotalPieces != 2)
            throw new InvalidOperationException("Session metrics recorded an invalid state.");

        metrics.RecordCorrectPlacement();
        if (!metrics.IsComplete)
            throw new InvalidOperationException("Session metrics did not mark completion.");

        metrics.Tick(5f);
        if (!Mathf.Approximately(metrics.ElapsedSeconds, 1.5f))
            throw new InvalidOperationException("Completed session time continued advancing.");
    }

    private static void ValidatePhaseFourRules()
    {
        PuzzleConfig config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(
            "Assets/Settings/PuzzleConfig.asset");
        if (config == null)
            throw new InvalidOperationException(
                "Phase four requires the puzzle configuration asset.");
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException(
                $"Phase four requires a valid configuration: {configError}.");

        PuzzleDifficultyProfile rotationProfile = null;
        PuzzleDifficultyProfile fixedProfile = null;
        for (int i = 0; i < config.DifficultyProfiles.Count; i++)
        {
            PuzzleDifficultyProfile profile = config.DifficultyProfiles[i];
            if (profile.RotationEnabled) rotationProfile = profile;
            else fixedProfile = profile;
        }

        if (rotationProfile == null || fixedProfile == null)
            throw new InvalidOperationException(
                "Phase four requires both rotation-enabled and fixed-orientation profiles.");

        foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
            if (PuzzleScoreCalculator.ComplexityPercent(style) < 100)
                throw new InvalidOperationException($"Style {style} has an invalid score complexity.");

        var texture = new Texture2D(8, 8)
        {
            name = "PhaseFourScoreValidationTexture",
            hideFlags = HideFlags.HideAndDontSave,
        };
        try
        {
            var session = new PuzzleSessionDefinition(
                rotationProfile,
                rotationProfile.Layouts[0],
                rotationProfile.DefaultCutStyle,
                config.DefaultImageId,
                texture,
                7001,
                7002);
            int totalPieces = session.Layout.divisions * session.Layout.divisions;
            var metrics = new PuzzleSessionMetrics();
            metrics.Begin(totalPieces);
            metrics.Tick(32.25f);
            metrics.RecordIncorrectAttempt();
            metrics.RecordHint();
            for (int i = 0; i < totalPieces; i++) metrics.RecordCorrectPlacement();

            PuzzleScoreBreakdown first = PuzzleScoreCalculator.Calculate(session, metrics);
            PuzzleScoreBreakdown second = PuzzleScoreCalculator.Calculate(session, metrics);
            if (first.Total != second.Total || first.BaseScore != second.BaseScore ||
                first.ComplexityBonus != second.ComplexityBonus ||
                first.RotationBonus != second.RotationBonus ||
                first.ReferenceBonus != second.ReferenceBonus ||
                first.TimePenalty != second.TimePenalty ||
                first.ErrorPenalty != second.ErrorPenalty ||
                first.HintPenalty != second.HintPenalty)
                throw new InvalidOperationException("Score calculation is not deterministic.");
            if (first.RotationBonus <= 0 || first.TimePenalty <= 0 ||
                first.ErrorPenalty <= 0 || first.HintPenalty <= 0)
                throw new InvalidOperationException("Score breakdown omitted an active component.");

            string details = first.BuildDetails();
            if (!details.Contains("Tempo") ||
                !details.Contains("Tentativas incorretas") ||
                !details.Contains("Dicas utilizadas"))
                throw new InvalidOperationException("Score details omitted a visible penalty.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidatePhaseFiveProgression()
    {
        PuzzleConfig config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(
            "Assets/Settings/PuzzleConfig.asset");
        if (config == null)
            throw new InvalidOperationException(
                "Phase five requires the puzzle configuration asset.");
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException(
                $"Phase five requires a valid configuration: {configError}.");
        if (!PuzzleImageLibrary.TryValidateAssignments(config, out string assignmentError))
            throw new InvalidOperationException(
                $"Phase five image collections are invalid: {assignmentError}.");
        if (PuzzleImageLibrary.SynchronizeConfig(config) != 0)
            throw new InvalidOperationException(
                "Phase five validation found unsynchronized puzzle images.");

        for (int collectionIndex = 0; collectionIndex < config.Collections.Count; collectionIndex++)
            for (int imageIndex = 0; imageIndex < config.Collections[collectionIndex].Images.Count; imageIndex++)
                config.LoadImage(config.Collections[collectionIndex].Images[imageIndex]);

        PuzzleProgressData progress = PuzzleProgressData.CreateNew(config);
        if (progress.Schema != PuzzleProgressData.CurrentSchema ||
            progress.Version != PuzzleProgressData.CurrentVersion ||
            progress.UniqueCompletedImages != 0 ||
            progress.UnlockedImageIds.Count < 2 ||
            progress.LastSelectedImageId != config.DefaultImageId)
            throw new InvalidOperationException("New progress has an invalid canonical state.");

        while (progress.UniqueCompletedImages < 12)
        {
            PuzzleImageDefinition nextImage = null;
            for (int collectionIndex = 0;
                 collectionIndex < config.Collections.Count && nextImage == null;
                 collectionIndex++)
            {
                PuzzleCollectionDefinition collection = config.Collections[collectionIndex];
                for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
                {
                    PuzzleImageDefinition candidate = collection.Images[imageIndex];
                    if (!progress.IsImageUnlocked(candidate.Id) ||
                        Contains(progress.CompletedImageIds, candidate.Id))
                        continue;
                    nextImage = candidate;
                    break;
                }
            }

            if (nextImage == null)
                throw new InvalidOperationException(
                    "Progression cannot reach twelve unique image completions.");
            CompleteImageForProgressValidation(config, progress, nextImage);
        }

        PuzzleDifficultyProfile normal = config.GetDifficulty(PuzzleDifficulty.Normal);
        PuzzleDifficultyProfile expert = config.GetDifficulty(PuzzleDifficulty.Expert);
        if (!progress.IsDifficultyUnlocked(config, normal) ||
            !progress.IsDifficultyUnlocked(config, expert) ||
            !progress.IsStyleUnlocked(config, PuzzleCutStyle.FullyRandom))
            throw new InvalidOperationException(
                "Progression did not unlock its configured difficulties and formats.");
        progress.SelectRules(config, expert, PuzzleCutStyle.FullyRandom);

        string json = PuzzleProgressSerializer.Serialize(progress, config);
        PuzzleProgressData roundTrip = PuzzleProgressSerializer.Deserialize(json, config);
        if (roundTrip.UniqueCompletedImages != progress.UniqueCompletedImages ||
            roundTrip.BestResults.Count != progress.BestResults.Count ||
            roundTrip.UnlockedImageIds.Count != progress.UnlockedImageIds.Count ||
            roundTrip.LastSelectedDifficulty != PuzzleDifficulty.Expert ||
            roundTrip.LastSelectedCutStyle != PuzzleCutStyle.FullyRandom)
            throw new InvalidOperationException("Progress JSON round-trip changed persisted data.");

        string invalidJson = json.Replace("\"version\": 1", "\"version\": 999");
        if (invalidJson == json)
            throw new InvalidOperationException("Progress version could not be changed for validation.");
        bool invalidVersionRejected = false;
        try
        {
            PuzzleProgressSerializer.Deserialize(invalidJson, config);
        }
        catch (InvalidOperationException)
        {
            invalidVersionRejected = true;
        }
        if (!invalidVersionRejected)
            throw new InvalidOperationException("Unsupported progress version was accepted.");

        string invalidSchemaJson = json.Replace(
            PuzzleProgressData.CurrentSchema,
            "unsupported-progress-schema");
        if (invalidSchemaJson == json)
            throw new InvalidOperationException("Progress schema could not be changed for validation.");
        bool invalidSchemaRejected = false;
        try
        {
            PuzzleProgressSerializer.Deserialize(invalidSchemaJson, config);
        }
        catch (InvalidOperationException)
        {
            invalidSchemaRejected = true;
        }
        if (!invalidSchemaRejected)
            throw new InvalidOperationException("Unsupported progress schema was accepted.");

        string validationKey =
            "Puzzle.Validation." + Guid.NewGuid().ToString("N");
        if (PlayerPrefs.HasKey(validationKey))
            throw new InvalidOperationException(
                $"Temporary PlayerPrefs key '{validationKey}' already exists.");
        try
        {
            var store = new PuzzleProgressStore(validationKey);
            store.Save(progress, config);
            store.Save(progress, config);
            PuzzleProgressData loaded = store.LoadOrCreate(config);
            if (loaded.UniqueCompletedImages != progress.UniqueCompletedImages ||
                loaded.BestResults.Count != progress.BestResults.Count)
                throw new InvalidOperationException(
                    "Transactional progress store changed persisted data.");
        }
        finally
        {
            PlayerPrefs.DeleteKey(validationKey);
            PlayerPrefs.Save();
        }
    }

    private static void CompleteImageForProgressValidation(
        PuzzleConfig config,
        PuzzleProgressData progress,
        PuzzleImageDefinition image)
    {
        PuzzleDifficultyProfile profile = config.DefaultDifficulty;
        PuzzleLayout layout = profile.Layouts[0];
        var session = new PuzzleSessionDefinition(
            profile,
            layout,
            profile.DefaultCutStyle,
            image.Id,
            config.LoadImage(image),
            8101 + progress.UniqueCompletedImages,
            9101 + progress.UniqueCompletedImages);
        int totalPieces = layout.divisions * layout.divisions;
        var metrics = new PuzzleSessionMetrics();
        metrics.Begin(totalPieces);
        metrics.Tick(totalPieces * 5f);
        for (int i = 0; i < totalPieces; i++) metrics.RecordCorrectPlacement();
        PuzzleScoreBreakdown score = PuzzleScoreCalculator.Calculate(session, metrics);
        PuzzleProgressUpdate update =
            progress.RecordCompletion(config, session, metrics, score);
        if (update.Medal != PuzzleMedal.Gold)
            throw new InvalidOperationException("Gold medal validation session received another medal.");
    }

    private static void ValidateTrayLayouts()
    {
        PuzzleConfig config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(
            "Assets/Settings/PuzzleConfig.asset");
        if (config == null)
            throw new InvalidOperationException("Puzzle configuration is missing.");
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException(
                $"Puzzle configuration is invalid: {configError}.");

        Vector2[] screenSizes =
        {
            new Vector2(1600f, 900f),
            new Vector2(1440f, 900f),
            new Vector2(1200f, 900f),
        };
        float[] imageAspects = { 0.6f, 1f, 1.8f };

        for (int screenIndex = 0; screenIndex < screenSizes.Length; screenIndex++)
        {
            Vector2 viewport = ReferenceViewportForScreen(screenSizes[screenIndex]);
            for (int profileIndex = 0; profileIndex < config.DifficultyProfiles.Count; profileIndex++)
            {
                PuzzleDifficultyProfile profile = config.DifficultyProfiles[profileIndex];
                for (int layoutIndex = 0; layoutIndex < profile.Layouts.Count; layoutIndex++)
                {
                    PuzzleLayout puzzleLayout = profile.Layouts[layoutIndex];
                    for (int aspectIndex = 0; aspectIndex < imageAspects.Length; aspectIndex++)
                    {
                        float aspect = imageAspects[aspectIndex];
                        Vector2 cellSize = aspect >= 1f
                            ? new Vector2(puzzleLayout.cellSize, puzzleLayout.cellSize / aspect)
                            : new Vector2(puzzleLayout.cellSize * aspect, puzzleLayout.cellSize);
                        Vector2 pieceSize =
                            cellSize * (1f + profile.CutDepth * 2f);

                        if (!PieceTrayController.TryCalculateLayout(
                                viewport,
                                Vector2.one * 600f,
                                puzzleLayout.divisions * puzzleLayout.divisions,
                                pieceSize,
                                profile.InitialTilt,
                                out PieceTrayLayout trayLayout,
                                out string trayError))
                            throw new InvalidOperationException(
                                $"Tray layout failed for {screenSizes[screenIndex].x:F0}x" +
                                $"{screenSizes[screenIndex].y:F0}, {profile.DisplayName}, " +
                                $"{puzzleLayout.divisions}x{puzzleLayout.divisions}, aspect {aspect:F1}: " +
                                $"{trayError}.");

                        if (trayLayout.PageCount <= 0 || trayLayout.CapacityPerPage <= 0 ||
                            trayLayout.CellSize.x <= 0f || trayLayout.CellSize.y <= 0f ||
                            trayLayout.PieceScale <= 0f || trayLayout.PieceScale > 1f)
                            throw new InvalidOperationException("Tray layout returned invalid dimensions.");

                        PieceTrayLayoutMode expectedMode = viewport.x / viewport.y >= 1.5f
                            ? PieceTrayLayoutMode.SidePanels
                            : PieceTrayLayoutMode.BottomPanel;
                        if (trayLayout.Mode != expectedMode)
                            throw new InvalidOperationException("Tray selected the wrong responsive mode.");
                    }
                }
            }
        }
    }

    private static Vector2 ReferenceViewportForScreen(Vector2 screenSize)
    {
        float widthScale = screenSize.x / 1600f;
        float heightScale = screenSize.y / 900f;
        float scaleFactor = Mathf.Sqrt(widthScale * heightScale);
        return screenSize / scaleFactor;
    }

    private static void ValidateSelectionUi()
    {
        var canvasObject = new GameObject(
            "ProceduralPuzzleUiValidation",
            typeof(RectTransform),
            typeof(Canvas));
        var previewTexture = new Texture2D(64, 64)
        {
            name = "ProceduralPuzzleUiValidationTexture",
            hideFlags = HideFlags.HideAndDontSave,
        };

        try
        {
            PuzzleConfig config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(
                "Assets/Settings/PuzzleConfig.asset");
            if (config == null)
                throw new InvalidOperationException("Puzzle configuration is missing.");
            if (!config.TryValidate(out string configError))
                throw new InvalidOperationException(
                    $"Puzzle configuration is invalid: {configError}.");
            PuzzleDifficultyProfile initialDifficulty = config.DefaultDifficulty;
            PuzzleProgressData progress = PuzzleProgressData.CreateNew(config);

            PuzzleCutSelectionMenu menu = PuzzleCutSelectionMenu.Show(
                (RectTransform)canvasObject.transform,
                config.DifficultyProfiles,
                initialDifficulty,
                initialDifficulty.DefaultCutStyle,
                previewTexture,
                config,
                progress,
                (_, _) => true);
            Canvas.ForceUpdateCanvases();

            int styleCount = Enum.GetValues(typeof(PuzzleCutStyle)).Length;
            Toggle[] toggles = menu.GetComponentsInChildren<Toggle>(true);
            ProceduralPuzzlePreviewGraphic[] previews =
                menu.GetComponentsInChildren<ProceduralPuzzlePreviewGraphic>(true);
            Button[] buttons = menu.GetComponentsInChildren<Button>(true);
            Text[] texts = menu.GetComponentsInChildren<Text>(true);

            int difficultyCount = config.DifficultyProfiles.Count;
            if (toggles.Length != styleCount + difficultyCount)
                throw new InvalidOperationException(
                    $"Selection UI has {toggles.Length} toggles; expected " +
                    $"{styleCount + difficultyCount}.");
            if (previews.Length != 1)
                throw new InvalidOperationException(
                    $"Selection UI has {previews.Length} previews; expected one shared lateral preview.");
            if (buttons.Length != 1)
                throw new InvalidOperationException(
                    $"Selection UI has {buttons.Length} confirmation buttons; expected 1.");
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].fontSize < 12 || !texts[i].alignByGeometry)
                    throw new InvalidOperationException(
                        $"Selection UI text '{texts[i].name}' is not configured for crisp rendering.");
            }

            Transform options = menu.transform.Find("Panel/Options");
            RectTransform panelRect = menu.transform.Find("Panel") as RectTransform;
            Transform previewPanel = menu.transform.Find("Panel/PreviewPanel");
            Transform previewTransform = previewPanel?.Find("PreviewFrame/Preview");
            Transform noPreviewTransform = previewPanel?.Find("PreviewFrame/NoPreview");
            Transform previewTitleTransform = previewPanel?.Find("PreviewTitle");
            if (options == null || panelRect == null || previewPanel == null || previewTransform == null ||
                noPreviewTransform == null || previewTitleTransform == null)
                throw new InvalidOperationException("Selection UI master-detail layout is incomplete.");
            if (panelRect.rect.width > 1600f || panelRect.rect.height > 900f)
                throw new InvalidOperationException(
                    "Selection UI panel exceeds the 1600x900 reference viewport.");

            ProceduralPuzzlePreviewGraphic preview = previews[0];
            Text previewTitle = previewTitleTransform.GetComponent<Text>();
            if (preview.transform != previewTransform || previewTitle == null)
                throw new InvalidOperationException("Selection UI lateral preview references are invalid.");

            Transform difficulties = menu.transform.Find("Panel/Difficulties");
            Text difficultySummary = menu.transform.Find("Panel/DifficultySummary")?.GetComponent<Text>();
            if (difficulties == null || difficultySummary == null)
                throw new InvalidOperationException("Selection UI difficulty controls are incomplete.");

            for (int i = 0; i < difficultyCount; i++)
            {
                PuzzleDifficultyProfile profile = config.DifficultyProfiles[i];
                if (!progress.IsDifficultyUnlocked(config, profile))
                {
                    Toggle lockedToggle =
                        difficulties.Find(profile.Difficulty.ToString())?.GetComponent<Toggle>();
                    Text lockedLabel = lockedToggle?.transform.Find("Label")?.GetComponent<Text>();
                    if (lockedToggle == null || lockedToggle.interactable || lockedLabel == null ||
                        !lockedLabel.text.Contains("BLOQUEADO"))
                        throw new InvalidOperationException(
                            $"Locked difficulty {profile.DisplayName} is not visibly locked.");
                    continue;
                }

                ValidateDifficultySelection(
                    difficulties,
                    options,
                    preview,
                    noPreviewTransform.gameObject,
                    previewTitle,
                    difficultySummary,
                    previewTexture,
                    profile,
                    config,
                    progress);
            }

            Transform randomCard = menu.transform.Find("Panel/Options/FullyRandom");
            if (randomCard == null)
                throw new InvalidOperationException("Selection UI is missing the FullyRandom option.");
            if (randomCard.Find("Preview") != null)
                throw new InvalidOperationException("Format options must not contain individual previews.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(previewTexture);
        }
    }

    private static void ValidateContentSelectionUi()
    {
        var canvasObject = new GameObject(
            "PuzzleContentUiValidation",
            typeof(RectTransform),
            typeof(Canvas));

        try
        {
            PuzzleConfig config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(
                "Assets/Settings/PuzzleConfig.asset");
            if (config == null)
                throw new InvalidOperationException("Puzzle configuration is missing.");
            PuzzleProgressData progress = PuzzleProgressData.CreateNew(config);
            PuzzleContentSelectionMenu menu = PuzzleContentSelectionMenu.Show(
                (RectTransform)canvasObject.transform,
                config,
                progress,
                progress.LastSelectedImageId,
                _ => true);
            Canvas.ForceUpdateCanvases();

            RectTransform panel = menu.transform.Find("Panel") as RectTransform;
            Transform collections = menu.transform.Find("Panel/Collections");
            Transform images = menu.transform.Find("Panel/Images/Viewport/Content");
            RawImage preview =
                menu.transform.Find("Panel/PreviewPanel/PreviewFrame/Image")?.GetComponent<RawImage>();
            Text results =
                menu.transform.Find("Panel/PreviewPanel/BestResults")?.GetComponent<Text>();
            RectTransform previewFrame =
                menu.transform.Find("Panel/PreviewPanel/PreviewFrame") as RectTransform;
            RectTransform continueButton =
                menu.transform.Find("Panel/Continue") as RectTransform;
            if (panel == null || collections == null || images == null ||
                preview == null || results == null || previewFrame == null ||
                continueButton == null)
                throw new InvalidOperationException("Content selection layout is incomplete.");
            if (panel.rect.width > 1600f || panel.rect.height > 900f)
                throw new InvalidOperationException(
                    "Content selection panel exceeds the reference viewport.");
            if (collections.childCount != config.Collections.Count)
                throw new InvalidOperationException(
                    "Content selection does not show every configured collection.");
            if (preview.texture != config.LoadImage(config.GetImage(progress.LastSelectedImageId)))
                throw new InvalidOperationException(
                    "Content selection preview does not use the selected image.");
            if (WorldRect(results.rectTransform).Overlaps(WorldRect(previewFrame)))
                throw new InvalidOperationException(
                    "Content selection results overlap the image preview.");
            if (WorldRect(continueButton).Overlaps(WorldRect((RectTransform)collections)) ||
                WorldRect(continueButton).Overlaps(
                    WorldRect((RectTransform)images.parent.parent)) ||
                WorldRect(continueButton).Overlaps(
                    WorldRect((RectTransform)previewFrame.parent)))
                throw new InvalidOperationException(
                    "Content selection confirmation overlaps a content column.");

            for (int i = 0; i < config.Collections.Count; i++)
            {
                PuzzleCollectionDefinition collection = config.Collections[i];
                Button button = collections.Find(collection.Id)?.GetComponent<Button>();
                bool unlocked = config.IsCollectionUnlocked(
                    collection,
                    progress.UniqueCompletedImages);
                Text label = button?.transform.Find("Label")?.GetComponent<Text>();
                if (button == null || button.interactable != unlocked || label == null)
                    throw new InvalidOperationException(
                        $"Collection {collection.DisplayName} has an invalid UI state.");
                if (!unlocked && !label.text.Contains("BLOQUEADO"))
                    throw new InvalidOperationException(
                        $"Collection {collection.DisplayName} does not show its lock.");
            }

            Text[] texts = menu.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].fontSize < 12 || !texts[i].alignByGeometry)
                    throw new InvalidOperationException(
                        $"Content UI text '{texts[i].name}' is not configured for crisp rendering.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void ValidateDifficultySelection(
        Transform difficulties,
        Transform options,
        ProceduralPuzzlePreviewGraphic preview,
        GameObject noPreviewMessage,
        Text previewTitle,
        Text difficultySummary,
        Texture2D previewTexture,
        PuzzleDifficultyProfile profile,
        PuzzleConfig config,
        PuzzleProgressData progress)
    {
        Transform difficultyOption = difficulties.Find(profile.Difficulty.ToString());
        Toggle difficultyToggle = difficultyOption?.GetComponent<Toggle>();
        if (difficultyToggle == null)
            throw new InvalidOperationException(
                $"Selection UI is missing difficulty {profile.Difficulty}.");

        difficultyToggle.isOn = true;
        Canvas.ForceUpdateCanvases();
        if (!difficultySummary.text.Contains(profile.Description) ||
            !difficultySummary.text.Contains(profile.BuildRuleSummary()))
            throw new InvalidOperationException(
                $"Selecting {profile.DisplayName} did not update its rule summary.");

        foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
        {
            Transform option = options.Find(style.ToString());
            if (option == null || option.gameObject.activeSelf != profile.AllowsStyle(style))
                throw new InvalidOperationException(
                    $"Difficulty {profile.DisplayName} exposed an invalid option state for {style}.");

            if (!profile.AllowsStyle(style)) continue;
            Toggle styleToggle = option.GetComponent<Toggle>();
            bool unlocked = progress.IsStyleUnlocked(config, style);
            if (styleToggle == null || styleToggle.interactable != unlocked)
                throw new InvalidOperationException(
                    $"Cut style {style} has an invalid unlock state.");
            if (!unlocked) continue;
            ValidateSelectionStyle(
                options,
                preview,
                noPreviewMessage,
                previewTitle,
                previewTexture,
                style);
        }
    }

    private static void ValidateSelectionStyle(
        Transform options,
        ProceduralPuzzlePreviewGraphic preview,
        GameObject noPreviewMessage,
        Text previewTitle,
        Texture2D previewTexture,
        PuzzleCutStyle style)
    {
        Transform option = options.Find(style.ToString());
        Toggle toggle = option?.GetComponent<Toggle>();
        Text label = option?.Find("Label")?.GetComponent<Text>();
        if (toggle == null || label == null)
            throw new InvalidOperationException($"Selection UI option '{style}' is incomplete.");
        if (label.preferredWidth > label.rectTransform.rect.width + 0.1f ||
            label.preferredHeight > label.rectTransform.rect.height + 0.1f)
            throw new InvalidOperationException($"Selection UI label '{label.text}' is clipped.");

        toggle.isOn = true;
        Canvas.ForceUpdateCanvases();

        int selectedCount = 0;
        foreach (Toggle candidate in options.GetComponentsInChildren<Toggle>(true))
            if (candidate.isOn) selectedCount++;
        if (selectedCount != 1)
            throw new InvalidOperationException(
                $"Selecting '{style}' left {selectedCount} options active; expected exactly one.");
        if (previewTitle.text != label.text)
            throw new InvalidOperationException($"Selecting '{style}' did not update the preview title.");

        Image selectedBackground = toggle.GetComponent<Image>();
        Image unselectedBackground = null;
        foreach (Toggle candidate in options.GetComponentsInChildren<Toggle>(true))
        {
            if (candidate.isOn) continue;
            unselectedBackground = candidate.GetComponent<Image>();
            if (unselectedBackground != null) break;
        }
        if (selectedBackground == null || unselectedBackground == null ||
            selectedBackground.color == unselectedBackground.color)
            throw new InvalidOperationException(
                $"Selecting '{style}' did not create a distinct persistent card state.");

        if (style == PuzzleCutStyle.FullyRandom)
        {
            if (preview.gameObject.activeSelf)
                throw new InvalidOperationException("FullyRandom must hide the lateral mesh preview.");
            if (!noPreviewMessage.activeSelf)
                throw new InvalidOperationException("FullyRandom must show its no-preview explanation.");
            return;
        }

        if (!preview.gameObject.activeSelf || noPreviewMessage.activeSelf)
            throw new InvalidOperationException($"Selecting '{style}' did not show the lateral preview.");
        if (preview.mainTexture != previewTexture)
            throw new InvalidOperationException($"Preview for '{style}' is missing the puzzle texture.");
        if (preview.canvasRenderer.GetAlpha() < 0.99f)
            throw new InvalidOperationException($"Preview for '{style}' is transparent.");

        Mesh mesh = preview.canvasRenderer.GetMesh();
        if (mesh == null || mesh.vertexCount < 3 || mesh.triangles.Length < 3)
            throw new InvalidOperationException($"Preview for '{style}' generated an empty mesh.");

        Rect viewport = preview.rectTransform.rect;
        Bounds bounds = mesh.bounds;
        const float epsilon = 0.1f;
        if (bounds.min.x < viewport.xMin - epsilon || bounds.max.x > viewport.xMax + epsilon ||
            bounds.min.y < viewport.yMin - epsilon || bounds.max.y > viewport.yMax + epsilon)
            throw new InvalidOperationException($"Preview for '{style}' exceeds its lateral viewport.");

        if (style == PuzzleCutStyle.Square &&
            Mathf.Abs(bounds.size.x - bounds.size.y) > epsilon)
            throw new InvalidOperationException("Square preview was stretched instead of scaled uniformly.");
    }

    private static void ValidateProceduralPrefabs()
    {
        if (typeof(UnityEngine.EventSystems.IDropHandler).IsAssignableFrom(typeof(Slot)))
            throw new InvalidOperationException(
                "Slot must not finalize placement from EventSystem drop events.");
        ValidatePrefab<PuzzlePiece>("Assets/Prefabs/PuzzlePiece.prefab");
        ValidatePrefab<Slot>("Assets/Prefabs/Slot.prefab");
    }

    private static void ValidatePrefab<T>(string path) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new InvalidOperationException($"Prefab '{path}' could not be loaded.");
        if (prefab.GetComponent<T>() == null)
            throw new InvalidOperationException($"Prefab '{path}' is missing {typeof(T).Name}.");
        if (prefab.GetComponent<ProceduralPuzzleImage>() == null)
            throw new InvalidOperationException($"Prefab '{path}' is missing ProceduralPuzzleImage.");

        var serializedComponent = new SerializedObject(prefab.GetComponent<T>());
        if (serializedComponent.FindProperty("image") != null)
            throw new InvalidOperationException(
                $"Prefab component {typeof(T).Name} must not serialize an image reference.");
    }

    private static void ValidateGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        PuzzleController controller = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<PuzzleController>(true);
            if (controller != null) break;
        }

        if (controller == null)
            throw new InvalidOperationException("Game scene is missing PuzzleController.");

        Canvas canvas = null;
        PieceTrayController tray = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
            if (tray == null) tray = root.GetComponentInChildren<PieceTrayController>(true);
        }

        CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        if (canvas == null || scaler == null || !canvas.pixelPerfect ||
            scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
            scaler.referenceResolution != new Vector2(1600f, 900f) ||
            !Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
            throw new InvalidOperationException(
                "Game Canvas must use pixel-perfect 1600x900 scale-with-screen-size rendering.");

        BoardBuilder builder = controller.GetComponent<BoardBuilder>();
        if (tray == null || builder == null)
            throw new InvalidOperationException("Game scene is missing the canonical piece tray.");
        var serializedBuilder = new SerializedObject(builder);
        SerializedProperty trayProperty = serializedBuilder.FindProperty("tray");
        if (trayProperty == null || trayProperty.objectReferenceValue != tray.transform)
            throw new InvalidOperationException(
                "BoardBuilder must reference the RectTransform that owns PieceTrayController.");

        var serializedController = new SerializedObject(controller);
        SerializedProperty selectionRoot = serializedController.FindProperty("selectionRoot");
        if (selectionRoot == null || selectionRoot.objectReferenceValue == null)
            throw new InvalidOperationException(
                "Game scene PuzzleController is missing its selection UI root reference.");

        ValidateMenuButton(scene, "RetryButton", "Retry");
        ValidateMenuButton(scene, "OptionsButton", "BackToOptions");
        ValidateMenuButton(scene, "QuitButton", "Quit");
    }

    private static void ValidateMenuButton(Scene scene, string objectName, string methodName)
    {
        Button button = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Button[] candidates = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].name != objectName) continue;
                button = candidates[i];
                break;
            }

            if (button != null) break;
        }

        if (button == null)
            throw new InvalidOperationException($"Game scene is missing button '{objectName}'.");
        if (button.onClick.GetPersistentEventCount() != 1 ||
            !(button.onClick.GetPersistentTarget(0) is GameMenu) ||
            button.onClick.GetPersistentMethodName(0) != methodName)
            throw new InvalidOperationException(
                $"Button '{objectName}' must call GameMenu.{methodName} exactly once.");
    }

    private static int SeedCount(PuzzleCutStyle style)
    {
        switch (style)
        {
            case PuzzleCutStyle.Square:
                return 1;
            case PuzzleCutStyle.Organic:
            case PuzzleCutStyle.Procedural:
                return 64;
            case PuzzleCutStyle.FullyRandom:
                return 256;
            default:
                return 16;
        }
    }

    private static bool Contains(
        System.Collections.Generic.IReadOnlyList<string> values,
        string expected)
    {
        for (int i = 0; i < values.Count; i++)
            if (values[i] == expected) return true;
        return false;
    }

    private static Rect WorldRect(RectTransform rectTransform)
    {
        if (rectTransform == null) throw new ArgumentNullException(nameof(rectTransform));
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static void ValidateBoard(
        ProceduralPuzzleGeometry[,] board,
        int divisions,
        float padding,
        PuzzleCutStyle style,
        int seed)
    {
        double area = 0d;
        float expandedSize = 1f + padding * 2f;
        int corners = 0;
        int edges = 0;
        int interiors = 0;

        for (int y = 0; y < divisions; y++)
        {
            for (int x = 0; x < divisions; x++)
            {
                ProceduralPuzzleGeometry geometry = board[x, y];
                switch (geometry.ClassifyByImageBoundary())
                {
                    case PuzzlePieceCategory.Corner:
                        corners++;
                        break;
                    case PuzzlePieceCategory.Edge:
                        edges++;
                        break;
                    case PuzzlePieceCategory.Interior:
                        interiors++;
                        break;
                    default:
                        Fail(style, seed, divisions, x, y, "piece classification is invalid");
                        break;
                }
                if (!geometry.Contains(Vector2.one * 0.5f))
                    Fail(style, seed, divisions, x, y, "piece does not contain its core center");

                for (int i = 0; i < geometry.Vertices.Count; i++)
                {
                    Vector2 vertex = geometry.Vertices[i];
                    Vector2 expectedUv = new Vector2(
                        (vertex.x * expandedSize - padding + x) / divisions,
                        (vertex.y * expandedSize - padding + y) / divisions);
                    if ((expectedUv - geometry.TextureUvs[i]).sqrMagnitude > 0.0000001f)
                        Fail(style, seed, divisions, x, y, "a vertex no longer maps to its original image UV");
                }

                for (int i = 0; i < geometry.Triangles.Count; i += 3)
                {
                    Vector2 a = geometry.Vertices[geometry.Triangles[i]];
                    Vector2 b = geometry.Vertices[geometry.Triangles[i + 1]];
                    Vector2 c = geometry.Vertices[geometry.Triangles[i + 2]];
                    float triangleArea = Cross(b - a, c - a) * 0.5f;
                    if (triangleArea <= 0f)
                        Fail(style, seed, divisions, x, y, "piece contains a reversed or empty triangle");
                    area += triangleArea * expandedSize * expandedSize;
                }
            }
        }

        double expectedArea = divisions * divisions;
        if (Math.Abs(area - expectedArea) > 0.002d)
            throw new InvalidOperationException(
                $"{style}, seed {seed}, {divisions}x{divisions}: shared cuts changed total image area " +
                $"from {expectedArea:F3} to {area:F3}.");

        int expectedCorners = 4;
        int expectedEdges = Math.Max(0, (divisions - 2) * 4);
        int expectedInteriors = Math.Max(0, (divisions - 2) * (divisions - 2));
        if (corners != expectedCorners || edges != expectedEdges ||
            interiors != expectedInteriors)
            throw new InvalidOperationException(
                $"{style}, seed {seed}, {divisions}x{divisions}: classification returned " +
                $"{corners} corners, {edges} edges, and {interiors} interiors; expected " +
                $"{expectedCorners}, {expectedEdges}, and {expectedInteriors}.");
    }

    private static void Fail(
        PuzzleCutStyle style,
        int seed,
        int divisions,
        int x,
        int y,
        string reason)
    {
        throw new InvalidOperationException(
            $"{style}, seed {seed}, {divisions}x{divisions}, piece ({x},{y}): {reason}.");
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
