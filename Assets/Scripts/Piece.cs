using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages puzzle piece generation and image slicing.
/// Refactored to use modular configuration and services.
/// </summary>
public class Piece: MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PieceClass pieceObjectReference;
    [SerializeField] private GameObject fatherOfPieces;
    [SerializeField] private Image BackGround;
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    
    [Header("Configuration")]
    [SerializeField] private PuzzleConfiguration configuration;
    [Tooltip("If true, selects a random configuration from available presets")]
    [SerializeField] private bool useRandomDifficulty = true;
    [SerializeField] private PuzzleConfiguration[] difficultyPresets;

    [Header("Animation")]
    [SerializeField] private PuzzleAnimationController animationController;
    [SerializeField] private bool enableSpawnAnimations = true;

    private List<Image> sprites = new List<Image>();
    private ImageSlicingService slicingService;

    private void Awake()
    {
        // Select configuration
        if (useRandomDifficulty && difficultyPresets != null && difficultyPresets.Length > 0)
        {
            configuration = difficultyPresets[Random.Range(0, difficultyPresets.Length)];
            Debug.Log($"Selected difficulty: {configuration.difficultyName} ({configuration.gridSize}x{configuration.gridSize})");
        }
        else if (configuration == null)
        {
            Debug.LogError("No puzzle configuration assigned! Creating default 3x3 configuration.");
            configuration = PuzzleConfiguration.CreatePreset("Default", 3, 200);
        }

        // Initialize services
        slicingService = new ImageSlicingService(configuration);
        
        // Setup grid layout
        ConfigureGridLayout();
        
        // Generate pieces
        InstantiateSprites(configuration.TotalPieces);
        LoadAndSliceTexture();
    }

    /// <summary>
    /// Configures the grid layout based on the configuration
    /// </summary>
    private void ConfigureGridLayout()
    {
        gridLayoutGroup.constraintCount = configuration.gridSize;
        gridLayoutGroup.cellSize = slicingService.GetCellSize();
        gridLayoutGroup.spacing = new Vector2(configuration.gridSpacing, configuration.gridSpacing);
    }

    /// <summary>
    /// Instantiates the specified number of piece game objects
    /// </summary>
    private void InstantiateSprites(int num)
    {
        for (int i = 0; i < num; i++)
        {
            GameObject gameObject = Instantiate(pieceObjectReference.gameObject, fatherOfPieces.transform);
            Image pieceImage = gameObject.GetComponent<Image>();
            sprites.Add(pieceImage);
            
            // Set size to match cell size
            pieceImage.rectTransform.sizeDelta = slicingService.GetCellSize();

            // Animate spawn if enabled
            if (enableSpawnAnimations && animationController != null)
            {
                animationController.AnimatePieceSpawn(gameObject.transform, i * 0.02f);
            }
        }
    }

    /// <summary>
    /// Loads and slices a random texture into puzzle pieces.
    /// Now using the ImageSlicingService for automatic dimension calculation!
    /// </summary>
    public void LoadAndSliceTexture()
    {
        // Load random texture
        Texture2D sourceTexture = slicingService.LoadRandomTexture();
        
        if (sourceTexture == null)
        {
            Debug.LogError("Failed to load texture. Check that images exist in Resources path and have Read/Write enabled.");
            return;
        }

        // Set background image
        BackGround.sprite = slicingService.CreateSpriteFromTexture(sourceTexture);

        // Slice texture into pieces - fully automatic now!
        List<Sprite> slicedSprites = slicingService.SliceTexture(sourceTexture);

        // Assign sprites to piece game objects
        for (int i = 0; i < sprites.Count && i < slicedSprites.Count; i++)
        {
            sprites[i].sprite = slicedSprites[i];
        }

        Debug.Log($"Successfully sliced image into {slicedSprites.Count} pieces ({configuration.gridSize}x{configuration.gridSize})");
    }

    public List<Image> GetSprites()
    {
        return sprites;
    }
}
