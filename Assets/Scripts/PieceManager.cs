using System;
using UnityEngine;
using UnityEngine.EventSystems;


public class PieceManager : MonoBehaviour
{
    public EventHandler<bool> OnPieceChanged;
    public EventHandler<PieceClass> OnPieceClassChanged;
    int numPieces = 3;//Base
    UiDragPiece dragPiece;

    public void InitializePiece(int numPieces, UiDragPiece dragPiece) 
    {
        this.numPieces = numPieces;
        this.dragPiece = dragPiece;
    }
}
