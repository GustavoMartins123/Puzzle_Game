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
        ValidateProceduralPrefabs();
        ValidateGameScene();

        Debug.Log(
            $"Procedural puzzle validation passed: {boardsValidated} boards and " +
            $"{piecesValidated} pieces across every cut style; selection UI, prefabs, and scene are valid.");
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
            PuzzleCutSelectionMenu menu = PuzzleCutSelectionMenu.Show(
                (RectTransform)canvasObject.transform,
                PuzzleCutStyle.Square,
                CutDepth,
                previewTexture,
                _ => true);
            Canvas.ForceUpdateCanvases();

            int styleCount = Enum.GetValues(typeof(PuzzleCutStyle)).Length;
            Toggle[] toggles = menu.GetComponentsInChildren<Toggle>(true);
            ProceduralPuzzlePreviewGraphic[] previews =
                menu.GetComponentsInChildren<ProceduralPuzzlePreviewGraphic>(true);
            Button[] buttons = menu.GetComponentsInChildren<Button>(true);

            if (toggles.Length != styleCount)
                throw new InvalidOperationException(
                    $"Selection UI has {toggles.Length} toggles; expected {styleCount}.");
            if (previews.Length != 1)
                throw new InvalidOperationException(
                    $"Selection UI has {previews.Length} previews; expected one shared lateral preview.");
            if (buttons.Length != 1)
                throw new InvalidOperationException(
                    $"Selection UI has {buttons.Length} confirmation buttons; expected 1.");

            Transform options = menu.transform.Find("Panel/Options");
            Transform previewPanel = menu.transform.Find("Panel/PreviewPanel");
            Transform previewTransform = previewPanel?.Find("PreviewFrame/Preview");
            Transform noPreviewTransform = previewPanel?.Find("PreviewFrame/NoPreview");
            Transform previewTitleTransform = previewPanel?.Find("PreviewTitle");
            if (options == null || previewPanel == null || previewTransform == null ||
                noPreviewTransform == null || previewTitleTransform == null)
                throw new InvalidOperationException("Selection UI master-detail layout is incomplete.");

            ProceduralPuzzlePreviewGraphic preview = previews[0];
            Text previewTitle = previewTitleTransform.GetComponent<Text>();
            if (preview.transform != previewTransform || previewTitle == null)
                throw new InvalidOperationException("Selection UI lateral preview references are invalid.");

            foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
                ValidateSelectionStyle(
                    options,
                    preview,
                    noPreviewTransform.gameObject,
                    previewTitle,
                    previewTexture,
                    style);

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

        var serializedController = new SerializedObject(controller);
        SerializedProperty selectionRoot = serializedController.FindProperty("selectionRoot");
        if (selectionRoot == null || selectionRoot.objectReferenceValue == null)
            throw new InvalidOperationException(
                "Game scene PuzzleController is missing its selection UI root reference.");
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

        for (int y = 0; y < divisions; y++)
        {
            for (int x = 0; x < divisions; x++)
            {
                ProceduralPuzzleGeometry geometry = board[x, y];
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
