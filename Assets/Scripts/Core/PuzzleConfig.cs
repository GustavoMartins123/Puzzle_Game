using System;
using System.Collections.Generic;
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
    [Header("Difficulty")]
    [SerializeField] private PuzzleDifficultyProfile[] difficultyProfiles;
    [SerializeField] private PuzzleDifficulty defaultDifficulty = PuzzleDifficulty.Casual;

    [Header("Session Seeds")]
    [SerializeField] private bool useFixedCutSeed;
    [SerializeField] private int cutSeed = 1729;

    [SerializeField] private string[] imagePaths;

    public int ImageCount => imagePaths == null ? 0 : imagePaths.Length;

    public IReadOnlyList<PuzzleDifficultyProfile> DifficultyProfiles => difficultyProfiles;

    public PuzzleDifficultyProfile DefaultDifficulty => GetDifficulty(defaultDifficulty);

    public bool TryValidate(out string error)
    {
        if (difficultyProfiles == null || difficultyProfiles.Length == 0)
        {
            error = "at least one difficulty profile is required";
            return false;
        }

        var difficulties = new HashSet<PuzzleDifficulty>();
        bool hasDefault = false;
        for (int i = 0; i < difficultyProfiles.Length; i++)
        {
            PuzzleDifficultyProfile profile = difficultyProfiles[i];
            if (profile == null)
            {
                error = $"difficulty profile {i} is missing";
                return false;
            }

            if (!profile.TryValidate(out string profileError))
            {
                error = $"difficulty profile '{profile.name}' is invalid: {profileError}";
                return false;
            }

            if (!difficulties.Add(profile.Difficulty))
            {
                error = $"difficulty {profile.Difficulty} is duplicated";
                return false;
            }

            if (profile.Difficulty == defaultDifficulty) hasDefault = true;
        }

        if (!hasDefault)
        {
            error = $"default difficulty {defaultDifficulty} is missing";
            return false;
        }

        if (useFixedCutSeed && cutSeed <= 0)
        {
            error = "fixed cut seed must be positive";
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

    public PuzzleDifficultyProfile GetDifficulty(PuzzleDifficulty difficulty)
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {error}.");

        for (int i = 0; i < difficultyProfiles.Length; i++)
            if (difficultyProfiles[i].Difficulty == difficulty) return difficultyProfiles[i];

        throw new InvalidOperationException($"Difficulty {difficulty} is not configured.");
    }

    public int CreateCutSeed() => useFixedCutSeed ? cutSeed : UnityEngine.Random.Range(1, int.MaxValue);

    public int CreateTraySeed() => UnityEngine.Random.Range(1, int.MaxValue);

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
