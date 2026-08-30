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
        ValidateSessionDefinitions();
        ValidateSessionMetrics();
        ValidatePhaseFourRules();
        ValidateTrayLayouts();
        ValidateProceduralPrefabs();
        ValidateGameScene();

        Debug.Log(
            $"Procedural puzzle validation passed: {boardsValidated} boards and " +
            $"{piecesValidated} pieces across every cut style; difficulty profiles, sessions, " +
            "retry image rotation, session metrics, deterministic scoring, optional piece " +
            "rotation, geometric tray filters, selection UI, responsive trays, prefabs, and " +
            "scene are valid.");
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
                            texture,
                            cutSeed,
                            traySeed);

                        if (!session.TryValidate(out string sessionError))
                            throw new InvalidOperationException(
                                $"Valid session was rejected: {sessionError}.");
                        if (session.Difficulty != profile || !profile.ContainsLayout(session.Layout) ||
                            session.CutStyle != style || session.Texture != texture ||
                            session.CutSeed != cutSeed || session.TraySeed != traySeed)
                            throw new InvalidOperationException(
                                "Puzzle session did not retain its exact immutable definition.");
                    }
                }
            }

            Texture2D initialImage = config.PickImage();
            Texture2D retryImage = config.PickDifferentImage(initialImage);
            if (retryImage == initialImage)
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

            PuzzleCutSelectionMenu menu = PuzzleCutSelectionMenu.Show(
                (RectTransform)canvasObject.transform,
                config.DifficultyProfiles,
                initialDifficulty,
                initialDifficulty.DefaultCutStyle,
                previewTexture,
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
                ValidateDifficultySelection(
                    difficulties,
                    options,
                    preview,
                    noPreviewTransform.gameObject,
                    previewTitle,
                    difficultySummary,
                    previewTexture,
                    profile);
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

    private static void ValidateDifficultySelection(
        Transform difficulties,
        Transform options,
        ProceduralPuzzlePreviewGraphic preview,
        GameObject noPreviewMessage,
        Text previewTitle,
        Text difficultySummary,
        Texture2D previewTexture,
        PuzzleDifficultyProfile profile)
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
