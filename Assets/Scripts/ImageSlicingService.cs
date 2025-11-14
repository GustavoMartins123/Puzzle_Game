using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Service responsible for slicing source images into puzzle pieces.
/// Handles automatic calculation of slice dimensions based on grid configuration.
/// </summary>
public class ImageSlicingService
{
    private readonly PuzzleConfiguration config;

    public ImageSlicingService(PuzzleConfiguration configuration)
    {
        config = configuration;
    }

    /// <summary>
    /// Loads a random texture from the configured resource path
    /// </summary>
    public Texture2D LoadRandomTexture()
    {
        Texture2D[] textures = Resources.LoadAll<Texture2D>(config.imageResourcePath);
        
        if (textures.Length == 0)
        {
            Debug.LogError($"No images found in Resources/{config.imageResourcePath}. Please add images and enable Read/Write in import settings.");
            return null;
        }

        int randomIndex = Random.Range(0, textures.Length);
        return textures[randomIndex];
    }

    /// <summary>
    /// Creates a sprite from a full texture
    /// </summary>
    public Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
    }

    /// <summary>
    /// Slices a texture into puzzle pieces based on the configuration.
    /// Automatically calculates proper dimensions - no manual proportions needed!
    /// </summary>
    public List<Sprite> SliceTexture(Texture2D sourceTexture)
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Source texture is null. Cannot slice.");
            return new List<Sprite>();
        }

        List<Sprite> slicedSprites = new List<Sprite>();
        int gridSize = config.gridSize;
        
        // Calculate piece dimensions automatically based on source texture
        int pieceWidth = sourceTexture.width / gridSize;
        int pieceHeight = sourceTexture.height / gridSize;

        // Slice the texture row by row, column by column
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                Sprite piece = SlicePiece(sourceTexture, col, row, pieceWidth, pieceHeight, gridSize);
                slicedSprites.Add(piece);
            }
        }

        return slicedSprites;
    }

    /// <summary>
    /// Slices a single piece from the source texture
    /// </summary>
    private Sprite SlicePiece(Texture2D sourceTexture, int col, int row, int pieceWidth, int pieceHeight, int gridSize)
    {
        // Create a new texture for this piece
        Texture2D pieceTexture = new Texture2D(pieceWidth, pieceHeight);

        // Calculate the position to read pixels from (inverted Y because Unity texture coordinates start at bottom-left)
        int xPos = col * pieceWidth;
        int yPos = (gridSize - 1 - row) * pieceHeight;

        // Get the pixels for this piece
        Color[] pixels = sourceTexture.GetPixels(xPos, yPos, pieceWidth, pieceHeight);
        pieceTexture.SetPixels(pixels);
        pieceTexture.Apply();

        // Create and return the sprite
        Sprite sprite = Sprite.Create(
            pieceTexture, 
            new Rect(0, 0, pieceWidth, pieceHeight), 
            new Vector2(0.5f, 0.5f) // Center pivot
        );

        return sprite;
    }

    /// <summary>
    /// Gets the configured cell size for puzzle pieces
    /// </summary>
    public Vector2 GetCellSize()
    {
        return new Vector2(config.cellSize, config.cellSize);
    }

    /// <summary>
    /// Gets the configured grid size
    /// </summary>
    public int GetGridSize()
    {
        return config.gridSize;
    }

    /// <summary>
    /// Gets the total number of pieces
    /// </summary>
    public int GetTotalPieces()
    {
        return config.TotalPieces;
    }
}
