using System;
using System.Collections.Generic;
using UnityEngine;

public enum PuzzleDifficulty
{
    Casual,
    Normal,
    Expert,
}

public enum PuzzleHintAllowance
{
    Disabled,
    Unlimited,
    Limited,
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
    [SerializeField] private PuzzleHintAllowance hintAllowance = PuzzleHintAllowance.Limited;
    [SerializeField, Min(0)] private int maximumHints = 3;
    [SerializeField, Min(0f)] private float hintCooldown = 10f;
    [SerializeField, Min(0)] private int requiredUniqueCompletions;
    [SerializeField] private bool rotationEnabled;
    [SerializeField, Min(0f)] private float rotationStepDegrees;
    [SerializeField, Min(0f)] private float rotationToleranceDegrees;

    public PuzzleDifficulty Difficulty => difficulty;

    public string DisplayName => displayName;

    public string Description => description;

    public IReadOnlyList<PuzzleLayout> Layouts => layouts;

    public PuzzleCutStyle DefaultCutStyle => defaultCutStyle;

    public IReadOnlyList<PuzzleCutStyle> AllowedCutStyles => allowedCutStyles;

    public float CutDepth => cutDepth;

    public float ReferenceOpacity => referenceOpacity;

    public float InitialTilt => initialTilt;

    public PuzzleHintAllowance HintAllowance => hintAllowance;

    public int MaximumHints => maximumHints;

    public float HintCooldown => hintCooldown;

    public int RequiredUniqueCompletions => requiredUniqueCompletions;

    public bool RotationEnabled => rotationEnabled;

    public float RotationStepDegrees => rotationStepDegrees;

    public float RotationToleranceDegrees => rotationToleranceDegrees;

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

        if (!Enum.IsDefined(typeof(PuzzleHintAllowance), hintAllowance))
        {
            error = $"hint allowance value {(int)hintAllowance} is invalid";
            return false;
        }

        if (hintAllowance == PuzzleHintAllowance.Limited && maximumHints <= 0)
        {
            error = "limited hints require a positive maximum";
            return false;
        }

        if (hintAllowance != PuzzleHintAllowance.Limited && maximumHints != 0)
        {
            error = "maximum hints must be zero when hints are not limited";
            return false;
        }

        if (!float.IsFinite(hintCooldown) || hintCooldown < 0f)
        {
            error = "hint cooldown must be finite and non-negative";
            return false;
        }

        if (hintAllowance == PuzzleHintAllowance.Disabled && hintCooldown != 0f)
        {
            error = "disabled hints require a zero cooldown";
            return false;
        }

        if (requiredUniqueCompletions < 0)
        {
            error = "difficulty unlock requirement cannot be negative";
            return false;
        }

        if (!float.IsFinite(rotationStepDegrees) ||
            !float.IsFinite(rotationToleranceDegrees))
        {
            error = "rotation values must be finite";
            return false;
        }

        if (!rotationEnabled)
        {
            if (rotationStepDegrees != 0f || rotationToleranceDegrees != 0f)
            {
                error = "disabled rotation requires zero step and tolerance";
                return false;
            }
        }
        else
        {
            if (rotationStepDegrees <= 0f || rotationStepDegrees > 180f)
            {
                error = "rotation step must be greater than zero and at most 180 degrees";
                return false;
            }

            float stepCount = 360f / rotationStepDegrees;
            if (!Mathf.Approximately(stepCount, Mathf.Round(stepCount)))
            {
                error = "rotation step must divide 360 degrees exactly";
                return false;
            }

            if (rotationToleranceDegrees <= 0f ||
                rotationToleranceDegrees >= rotationStepDegrees * 0.5f)
            {
                error = "rotation tolerance must be positive and less than half the step";
                return false;
            }
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

    public bool CanUseHint(int hintsUsed)
    {
        if (hintsUsed < 0) throw new ArgumentOutOfRangeException(nameof(hintsUsed));
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid difficulty profile '{name}': {error}.");

        return hintAllowance switch
        {
            PuzzleHintAllowance.Disabled => false,
            PuzzleHintAllowance.Unlimited => true,
            PuzzleHintAllowance.Limited => hintsUsed < maximumHints,
            _ => throw new InvalidOperationException($"Invalid hint allowance {hintAllowance}."),
        };
    }

    public string BuildRuleSummary()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid difficulty profile '{name}': {error}.");

        string layoutSummary = layouts.Length == 1
            ? $"{layouts[0].divisions}×{layouts[0].divisions}"
            : $"{layouts[0].divisions}×{layouts[0].divisions}–" +
              $"{layouts[layouts.Length - 1].divisions}×{layouts[layouts.Length - 1].divisions}";
        string rotationSummary = rotationEnabled
            ? $"rotação {Mathf.RoundToInt(rotationStepDegrees)}°"
            : "sem rotação";
        return $"{layoutSummary}  •  referência {Mathf.RoundToInt(referenceOpacity * 100f)}%  •  " +
               $"inclinação {Mathf.RoundToInt(initialTilt)}°  •  {rotationSummary}";
    }
}
