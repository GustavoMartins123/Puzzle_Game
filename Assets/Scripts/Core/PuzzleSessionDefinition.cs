using System;
using UnityEngine;

public sealed class PuzzleSessionDefinition
{
    public PuzzleSessionDefinition(
        PuzzleDifficultyProfile difficulty,
        PuzzleLayout layout,
        PuzzleCutStyle cutStyle,
        Texture2D texture,
        int cutSeed,
        int traySeed)
    {
        Difficulty = difficulty != null ? difficulty : throw new ArgumentNullException(nameof(difficulty));
        Layout = layout;
        CutStyle = cutStyle;
        Texture = texture != null ? texture : throw new ArgumentNullException(nameof(texture));
        CutSeed = cutSeed;
        TraySeed = traySeed;

        if (!TryValidate(out string error))
            throw new ArgumentException($"Invalid puzzle session: {error}.");
    }

    public PuzzleDifficultyProfile Difficulty { get; }

    public PuzzleLayout Layout { get; }

    public PuzzleCutStyle CutStyle { get; }

    public Texture2D Texture { get; }

    public int CutSeed { get; }

    public int TraySeed { get; }

    public bool TryValidate(out string error)
    {
        if (Difficulty == null)
        {
            error = "difficulty profile is missing";
            return false;
        }

        if (!Difficulty.TryValidate(out string profileError))
        {
            error = $"difficulty profile is invalid: {profileError}";
            return false;
        }

        if (!Difficulty.ContainsLayout(Layout))
        {
            error = $"layout {Layout.divisions}×{Layout.divisions} does not belong to {Difficulty.DisplayName}";
            return false;
        }

        if (!Difficulty.AllowsStyle(CutStyle))
        {
            error = $"cut style {CutStyle} is not allowed by {Difficulty.DisplayName}";
            return false;
        }

        if (Texture == null || Texture.width <= 0 || Texture.height <= 0)
        {
            error = "puzzle texture is missing or invalid";
            return false;
        }

        if (CutSeed <= 0 || TraySeed <= 0)
        {
            error = "session seeds must be positive";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
