using UnityEngine;

/// <summary>
/// Helper class to create default puzzle configuration presets.
/// These configurations provide different difficulty levels without manual setup.
/// </summary>
public static class PuzzleConfigurationPresets
{
    /// <summary>
    /// Creates an Easy difficulty preset (2x2 grid, large pieces)
    /// </summary>
    public static PuzzleConfiguration CreateEasyPreset()
    {
        PuzzleConfiguration config = ScriptableObject.CreateInstance<PuzzleConfiguration>();
        config.difficultyName = "Easy";
        config.gridSize = 2;
        config.cellSize = 300;
        config.gridSpacing = 2;
        config.imageResourcePath = "Sprites/Fish";
        return config;
    }

    /// <summary>
    /// Creates a Medium difficulty preset (3x3 grid, medium pieces)
    /// </summary>
    public static PuzzleConfiguration CreateMediumPreset()
    {
        PuzzleConfiguration config = ScriptableObject.CreateInstance<PuzzleConfiguration>();
        config.difficultyName = "Medium";
        config.gridSize = 3;
        config.cellSize = 200;
        config.gridSpacing = 1;
        config.imageResourcePath = "Sprites/Fish";
        return config;
    }

    /// <summary>
    /// Creates a Hard difficulty preset (4x4 grid, small pieces)
    /// </summary>
    public static PuzzleConfiguration CreateHardPreset()
    {
        PuzzleConfiguration config = ScriptableObject.CreateInstance<PuzzleConfiguration>();
        config.difficultyName = "Hard";
        config.gridSize = 4;
        config.cellSize = 150;
        config.gridSpacing = 1;
        config.imageResourcePath = "Sprites/Fish";
        return config;
    }

    /// <summary>
    /// Creates an Expert difficulty preset (5x5 grid, very small pieces)
    /// </summary>
    public static PuzzleConfiguration CreateExpertPreset()
    {
        PuzzleConfiguration config = ScriptableObject.CreateInstance<PuzzleConfiguration>();
        config.difficultyName = "Expert";
        config.gridSize = 5;
        config.cellSize = 120;
        config.gridSpacing = 1;
        config.imageResourcePath = "Sprites/Fish";
        return config;
    }

    /// <summary>
    /// Gets all available presets
    /// </summary>
    public static PuzzleConfiguration[] GetAllPresets()
    {
        return new PuzzleConfiguration[]
        {
            CreateEasyPreset(),
            CreateMediumPreset(),
            CreateHardPreset(),
            CreateExpertPreset()
        };
    }
}
