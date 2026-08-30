using System;
using UnityEngine;

public enum PuzzleCutStyle
{
    Square,
    Round,
    Ellipse,
    Rectangle,
    Hexagon,
    Triangle,
    Diamond,
    Wave,
    Zigzag,
    DoubleLobe,
    Organic,
    Procedural,
    FullyRandom,
}

[CreateAssetMenu(fileName = "PuzzleConfig", menuName = "Puzzle/Puzzle Config")]
public class PuzzleConfig : ScriptableObject
{
    [System.Serializable]
    public struct Layout
    {
        public int divisions;
        public float cellSize;
    }

    [SerializeField] private Layout[] layouts =
    {
        new Layout { divisions = 2, cellSize = 300f },
        new Layout { divisions = 3, cellSize = 200f },
        new Layout { divisions = 4, cellSize = 150f },
        new Layout { divisions = 5, cellSize = 120f },
    };

    [Header("Procedural Pieces")]
    [SerializeField] private PuzzleCutStyle cutStyle = PuzzleCutStyle.FullyRandom;
    [SerializeField, Range(0.08f, 0.3f)] private float cutDepth = 0.22f;
    [SerializeField] private bool useFixedCutSeed;
    [SerializeField] private int cutSeed = 1729;

    [SerializeField] private string[] imagePaths;

    public int ImageCount => imagePaths == null ? 0 : imagePaths.Length;

    public PuzzleCutStyle CutStyle => cutStyle;

    public float CutDepth => cutDepth;

    public bool TryValidate(out string error)
    {
        if (layouts == null || layouts.Length == 0)
        {
            error = "at least one layout is required";
            return false;
        }

        for (int i = 0; i < layouts.Length; i++)
        {
            if (layouts[i].divisions < 2)
            {
                error = $"layout {i} must have at least 2 divisions";
                return false;
            }

            if (!float.IsFinite(layouts[i].cellSize) || layouts[i].cellSize <= 0f)
            {
                error = $"layout {i} must have a positive finite cell size";
                return false;
            }
        }

        if (!Enum.IsDefined(typeof(PuzzleCutStyle), cutStyle))
        {
            error = $"cut style value {(int)cutStyle} is invalid";
            return false;
        }

        if (!float.IsFinite(cutDepth) || cutDepth < 0.08f || cutDepth > 0.3f)
        {
            error = "cut depth must be between 0.08 and 0.30";
            return false;
        }

        if (imagePaths == null || imagePaths.Length == 0)
        {
            error = "the image library is empty";
            return false;
        }

        for (int i = 0; i < imagePaths.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(imagePaths[i])) continue;
            error = $"image path {i} is empty";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public Layout PickLayout()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {error}.");

        return layouts[UnityEngine.Random.Range(0, layouts.Length)];
    }

    public int CreateCutSeed() => useFixedCutSeed ? cutSeed : UnityEngine.Random.Range(1, int.MaxValue);

    public Texture2D PickImage()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {error}.");

        string path = imagePaths[UnityEngine.Random.Range(0, imagePaths.Length)];
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
            throw new InvalidOperationException($"Puzzle image '{path}' could not be loaded.");

        return texture;
    }
}
