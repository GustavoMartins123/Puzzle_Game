using UnityEngine;

/// <summary>
/// ScriptableObject for configuring puzzle difficulty and grid settings.
/// Replaces hardcoded arrays with a modular configuration system.
/// </summary>
[CreateAssetMenu(fileName = "PuzzleConfiguration", menuName = "Puzzle/Configuration")]
public class PuzzleConfiguration : ScriptableObject
{
    [Header("Grid Settings")]
    [Tooltip("Number of columns and rows (e.g., 3 for a 3x3 grid)")]
    [Range(2, 10)]
    public int gridSize = 3;

    [Header("Display Settings")]
    [Tooltip("Size of each puzzle piece cell in pixels")]
    [Range(50, 500)]
    public int cellSize = 200;

    [Tooltip("Spacing between pieces in the grid")]
    [Range(0, 20)]
    public int gridSpacing = 1;

    [Header("Image Source")]
    [Tooltip("Path to the folder containing puzzle images in Resources")]
    public string imageResourcePath = "Sprites/Fish";

    [Header("Difficulty Presets")]
    [Tooltip("Name of this difficulty preset")]
    public string difficultyName = "Medium";

    /// <summary>
    /// Total number of pieces based on grid size
    /// </summary>
    public int TotalPieces => gridSize * gridSize;

    /// <summary>
    /// Validates the configuration values
    /// </summary>
    public void OnValidate()
    {
        if (gridSize < 2) gridSize = 2;
        if (cellSize < 50) cellSize = 50;
        if (gridSpacing < 0) gridSpacing = 0;
    }

    /// <summary>
    /// Creates a preset configuration
    /// </summary>
    public static PuzzleConfiguration CreatePreset(string name, int size, int cell)
    {
        PuzzleConfiguration config = CreateInstance<PuzzleConfiguration>();
        config.difficultyName = name;
        config.gridSize = size;
        config.cellSize = cell;
        return config;
    }
}
