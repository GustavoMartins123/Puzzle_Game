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

        try
        {
            PuzzleCutSelectionMenu menu = PuzzleCutSelectionMenu.Show(
                (RectTransform)canvasObject.transform,
                PuzzleCutStyle.FullyRandom,
                CutDepth,
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
            if (previews.Length != styleCount - 1)
                throw new InvalidOperationException(
                    $"Selection UI has {previews.Length} previews; expected {styleCount - 1}.");
            if (buttons.Length != 1)
                throw new InvalidOperationException(
                    $"Selection UI has {buttons.Length} confirmation buttons; expected 1.");

            Transform randomCard = menu.transform.Find("Panel/Options/FullyRandom");
            if (randomCard == null)
                throw new InvalidOperationException("Selection UI is missing the FullyRandom option.");
            if (randomCard.Find("Preview") != null)
                throw new InvalidOperationException("FullyRandom must not render a preview.");
            if (randomCard.Find("NoPreview") == null)
                throw new InvalidOperationException("FullyRandom must explain why it has no preview.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
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
