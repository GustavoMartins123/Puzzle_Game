using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the UI element that follows the mouse cursor when dragging pieces.
/// Stores the currently dragged piece and its original slot ID.
/// </summary>
public class UiDragPiece : MonoBehaviour
{
    [SerializeField] private Image mouseImg;
    [SerializeField] private int piecePreviousId;
    [SerializeField] private PieceClass pieceClass;

    /// <summary>
    /// Gets the image component that follows the mouse
    /// </summary>
    public Image GetMouseImg()
    {
        return mouseImg;
    }
    
    /// <summary>
    /// Sets the ID of the slot where the piece originated from
    /// </summary>
    public void SetPiecePreviousId(int piecePreviousId)
    {
        this.piecePreviousId = piecePreviousId;
    }

    /// <summary>
    /// Gets the ID of the slot where the piece originated from
    /// </summary>
    public int GetPiecePreviousId()
    {
        return piecePreviousId;
    }
    
    /// <summary>
    /// Sets the currently dragged piece
    /// </summary>
    public void SetPieceClass(PieceClass pieceClass)
    {
        this.pieceClass = pieceClass;
    }
    
    /// <summary>
    /// Gets the currently dragged piece
    /// </summary>
    public PieceClass GetPieceClass()
    {
        return pieceClass;
    }
}
