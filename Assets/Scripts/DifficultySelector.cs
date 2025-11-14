using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for selecting puzzle difficulty at runtime.
/// Allows players to choose between Easy, Medium, Hard, and Expert modes.
/// </summary>
public class DifficultySelector : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button expertButton;
    [SerializeField] private GameObject difficultyPanel;

    [Header("Configuration")]
    [SerializeField] private PuzzleConfiguration[] presets;
    [SerializeField] private Piece pieceManager;

    private void Start()
    {
        // Setup button listeners
        if (easyButton != null)
            easyButton.onClick.AddListener(() => SelectDifficulty(0));
        
        if (mediumButton != null)
            mediumButton.onClick.AddListener(() => SelectDifficulty(1));
        
        if (hardButton != null)
            hardButton.onClick.AddListener(() => SelectDifficulty(2));
        
        if (expertButton != null)
            expertButton.onClick.AddListener(() => SelectDifficulty(3));
    }

    /// <summary>
    /// Selects a difficulty preset and starts the game
    /// </summary>
    private void SelectDifficulty(int presetIndex)
    {
        if (presets == null || presetIndex < 0 || presetIndex >= presets.Length)
        {
            Debug.LogError($"Invalid preset index: {presetIndex}");
            return;
        }

        PuzzleConfiguration selectedConfig = presets[presetIndex];
        Debug.Log($"Selected difficulty: {selectedConfig.difficultyName}");

        // Hide difficulty panel
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        // Apply configuration and start game
        // Note: This would require refactoring Piece.cs to support runtime configuration change
        // For now, this serves as a template for future implementation
    }

    /// <summary>
    /// Shows the difficulty selection panel
    /// </summary>
    public void ShowDifficultyPanel()
    {
        if (difficultyPanel != null)
            difficultyPanel.SetActive(true);
    }

    /// <summary>
    /// Hides the difficulty selection panel
    /// </summary>
    public void HideDifficultyPanel()
    {
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
    }
}
