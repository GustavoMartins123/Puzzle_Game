using System;
using System.Collections.Generic;
using UnityEngine;

public enum PuzzleDifficulty
{
    Casual,
    Normal,
    Expert,
}

[Serializable]
public struct PuzzleLayout
{
    public int divisions;
    public float cellSize;
}

[CreateAssetMenu(fileName = "PuzzleDifficulty", menuName = "Puzzle/Difficulty Profile")]
public sealed class PuzzleDifficultyProfile : ScriptableObject
{
    [SerializeField] private PuzzleDifficulty difficulty;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 3)] private string description;
    [SerializeField] private PuzzleLayout[] layouts;
    [SerializeField] private PuzzleCutStyle defaultCutStyle;
    [SerializeField] private PuzzleCutStyle[] allowedCutStyles;
    [SerializeField, Range(0.08f, 0.3f)] private float cutDepth = 0.22f;
    [SerializeField, Range(0f, 1f)] private float referenceOpacity = 0.65f;
    [SerializeField, Range(0f, 30f)] private float initialTilt = 8f;

    public PuzzleDifficulty Difficulty => difficulty;

    public string DisplayName => displayName;

    public string Description => description;

    public IReadOnlyList<PuzzleLayout> Layouts => layouts;

    public PuzzleCutStyle DefaultCutStyle => defaultCutStyle;

    public IReadOnlyList<PuzzleCutStyle> AllowedCutStyles => allowedCutStyles;

    public float CutDepth => cutDepth;

    public float ReferenceOpacity => referenceOpacity;

    public float InitialTilt => initialTilt;

    public bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), difficulty))
        {
            error = $"difficulty value {(int)difficulty} is invalid";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "display name is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            error = "description is empty";
            return false;
        }

        if (layouts == null || layouts.Length == 0)
        {
            error = "at least one layout is required";
            return false;
        }

        var divisions = new HashSet<int>();
        for (int i = 0; i < layouts.Length; i++)
        {
            PuzzleLayout layout = layouts[i];
            if (layout.divisions < 2)
            {
                error = $"layout {i} must have at least 2 divisions";
                return false;
            }

            if (!float.IsFinite(layout.cellSize) || layout.cellSize <= 0f)
            {
                error = $"layout {i} must have a positive finite cell size";
                return false;
            }

            if (!divisions.Add(layout.divisions))
            {
                error = $"layout divisions {layout.divisions} are duplicated";
                return false;
            }
        }

        if (allowedCutStyles == null || allowedCutStyles.Length == 0)
        {
            error = "at least one cut style is required";
            return false;
        }

        var styles = new HashSet<PuzzleCutStyle>();
        for (int i = 0; i < allowedCutStyles.Length; i++)
        {
            PuzzleCutStyle style = allowedCutStyles[i];
            if (!Enum.IsDefined(typeof(PuzzleCutStyle), style))
            {
                error = $"cut style value {(int)style} is invalid";
                return false;
            }

            if (!styles.Add(style))
            {
                error = $"cut style {style} is duplicated";
                return false;
            }

            if (style == PuzzleCutStyle.FullyRandom && difficulty != PuzzleDifficulty.Expert)
            {
                error = "FullyRandom is restricted to Expert difficulty";
                return false;
            }
        }

        if (!styles.Contains(defaultCutStyle))
        {
            error = $"default cut style {defaultCutStyle} is not allowed";
            return false;
        }

        if (!float.IsFinite(cutDepth) || cutDepth < 0.08f || cutDepth > 0.3f)
        {
            error = "cut depth must be between 0.08 and 0.30";
            return false;
        }

        if (!float.IsFinite(referenceOpacity) || referenceOpacity < 0f || referenceOpacity > 1f)
        {
            error = "reference opacity must be between 0 and 1";
            return false;
        }

        if (!float.IsFinite(initialTilt) || initialTilt < 0f || initialTilt > 30f)
        {
            error = "initial tilt must be between 0 and 30 degrees";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool AllowsStyle(PuzzleCutStyle style)
    {
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), style)) return false;
        if (allowedCutStyles == null) return false;

        for (int i = 0; i < allowedCutStyles.Length; i++)
            if (allowedCutStyles[i] == style) return true;
        return false;
    }

    public bool ContainsLayout(PuzzleLayout layout)
    {
        if (layouts == null) return false;
        for (int i = 0; i < layouts.Length; i++)
        {
            if (layouts[i].divisions == layout.divisions &&
                Mathf.Approximately(layouts[i].cellSize, layout.cellSize))
                return true;
        }

        return false;
    }

    public PuzzleLayout PickLayout()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid difficulty profile '{name}': {error}.");

        return layouts[UnityEngine.Random.Range(0, layouts.Length)];
    }

    public string BuildRuleSummary()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid difficulty profile '{name}': {error}.");

        string layoutSummary = layouts.Length == 1
            ? $"{layouts[0].divisions}×{layouts[0].divisions}"
            : $"{layouts[0].divisions}×{layouts[0].divisions}–" +
              $"{layouts[layouts.Length - 1].divisions}×{layouts[layouts.Length - 1].divisions}";
        return $"{layoutSummary}  •  referência {Mathf.RoundToInt(referenceOpacity * 100f)}%  •  " +
               $"inclinação {Mathf.RoundToInt(initialTilt)}°";
    }
}
