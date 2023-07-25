using System;
using UnityEngine;


public class PieceManager : MonoBehaviour
{
    public EventHandler<bool> OnPieceChanged;
    int numPieces = 3;//Base
    UiDragPiece dragPiece;

    public void InitializePiece(int numPieces, UiDragPiece dragPiece) 
    {
        this.numPieces = numPieces;
        this.dragPiece = dragPiece;
    }
}
