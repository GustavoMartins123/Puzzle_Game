using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PieceClass : MonoBehaviour, IDragHandler, IDropHandler, IBeginDragHandler
{
    [SerializeField] private Image sprite = null;
    [SerializeField] private int myId = 0;
    public bool pieceInSlot = false;
    public UiDragPiece dragPiece;
    [SerializeField] private InputManager inputManager;
    private void Start()
    {
        inputManager.OnPauseCalled += InputManager_OnPauseCalled;
    }
    public void SetSprite(Sprite sprite)
    {
        this.sprite.sprite = sprite;
    }

    public void SetId(int id)
    {
        myId = id;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragPiece.mouseImg.transform.childCount > 0)
        {
            dragPiece.mouseImg.transform.GetChild(0).SetParent(GameManager.Instance.fatherOfPieces.transform);
            return;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(Time.timeScale != 1)
        {
            return;
        }
        PieceClass piece = eventData.pointerDrag.transform.GetComponent<PieceClass>();
        dragPiece.pieceClass = piece;
        if (piece != null && !piece.pieceInSlot)
        {
            
            dragPiece.piecePreviousId = myId;
            piece.transform.SetParent(dragPiece.mouseImg.transform);
            piece.transform.localPosition = Vector2.Lerp(piece.transform.localPosition, Vector2.zero, 0.1f);
            GameManager.Instance.InvokeInputManager(this, true);
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
                GameManager.Instance.InvokeInputManager(this, false);
                piece.transform.localRotation = Quaternion.identity;
                piece.transform.localPosition = Vector2.zero;
                dragPiece.mouseImg.transform.localPosition = Vector3.zero;
                dragPiece.mouseImg.transform.SetAsLastSibling();
                dragPiece.pieceClass = null;
            }
            else
            {
                piece.transform.SetParent(GameManager.Instance.fatherOfPieces.transform);
                GameManager.Instance.InvokeInputManager(this, false);
                dragPiece.mouseImg.transform.localPosition = Vector3.zero;
                dragPiece.mouseImg.transform.SetAsLastSibling();
                dragPiece.pieceClass = null;
            }
        }
    }

    private void InputManager_OnPauseCalled(object sender, EventArgs e)
    {
        if (dragPiece.pieceClass != null)
        {
            dragPiece.pieceClass.transform.SetParent(GameManager.Instance.fatherOfPieces.transform);
            GameManager.Instance.InvokeInputManager(this, false);
            dragPiece.mouseImg.transform.localPosition = Vector3.zero;
            dragPiece.mouseImg.transform.SetAsLastSibling();
        }
    }
}
