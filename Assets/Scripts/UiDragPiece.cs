using UnityEngine;
using UnityEngine.UI;

public class UiDragPiece : MonoBehaviour
{
    [SerializeField] private Image mouseImg;
    [SerializeField] private int piecePreviousId;
    [SerializeField] private PieceClass pieceClass;

    public Image GetMouseImg()
    {
        return mouseImg;
    }
    public void SetPiecePreviousId(int piecePreviousId)
    {
        this.piecePreviousId = piecePreviousId;
    }

    public int GetPiecePreviousId()
    {
        return piecePreviousId;
    }
    public void SetPieceClass(PieceClass pieceClass)
    {
        this.pieceClass = pieceClass;
    }
    public PieceClass GetPieceClass()
    {
        return pieceClass;
    }
}
