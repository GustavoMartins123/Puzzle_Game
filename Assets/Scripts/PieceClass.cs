using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PieceClass : MonoBehaviour, IDragHandler, IDropHandler
{
    [SerializeField]Image sprite = null;
    [SerializeField]int myId = 0;
    public bool pieceInSlot = false;
    public UiDragPiece dragPiece;
    public Sprite GetSprite()
    {
        return sprite.sprite;
    }
    public void SetSprite(Sprite sprite)
    {
        this.sprite.sprite = sprite;
    }

    public void SetId(int id)
    {
        myId = id;
    }

    public int GetId()
    {
        return myId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        PieceClass piece = eventData.pointerDrag.transform.GetComponent<PieceClass>();
        if (piece != null && !piece.pieceInSlot)
        {
            dragPiece.piecePreviousId = myId;
            piece.transform.SetParent(dragPiece.mouseImg.transform);
            piece.transform.localPosition = Vector2.Lerp(piece.transform.localPosition, Vector2.zero, 0.1f);
            GameManager.Instance.pieceManager.OnPieceChanged?.Invoke(this, true);
        }
        else
        {
            return;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        PieceClass piece = eventData.pointerDrag.transform.GetComponent<PieceClass>();
        if (piece != null)
        {
            Slot slot = GameManager.Instance.GetClosestSlotToPiece(piece);
            if (slot != null && slot.id == dragPiece.piecePreviousId)
            {
                piece.transform.SetParent(slot.transform);
                piece.pieceInSlot = true;
                GameManager.Instance.pieceManager.OnPieceChanged?.Invoke(this, false);
                piece.transform.localRotation = Quaternion.identity;
                piece.transform.localPosition = Vector2.zero;
                dragPiece.mouseImg.transform.localPosition = Vector3.zero;
            }
            else
            {
                piece.transform.SetParent(GameManager.Instance.fatherOfPieces.transform);
                GameManager.Instance.pieceManager.OnPieceChanged?.Invoke(this, false);
                dragPiece.mouseImg.transform.localPosition = Vector3.zero;
            }
        }
    }
}
