using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PieceClass : MonoBehaviour, IDragHandler, IDropHandler, IBeginDragHandler
{
    [SerializeField] private Image sprite = null;
    [SerializeField] private int myId = 0;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private UiDragPiece dragPiece;
    private bool pieceInSlot = false;
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
        if (dragPiece.GetMouseImg().transform.childCount > 0)
        {
            dragPiece.GetMouseImg().transform.GetChild(0).SetParent(GameManager.Instance.GetFatherOfPiecesTransform());
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(Time.timeScale != 1)
        {
            return;
        }
        PieceClass piece = eventData.pointerDrag.transform.GetComponent<PieceClass>();
        dragPiece.SetPieceClass(piece);
        if (piece != null && !piece.pieceInSlot)
        {
            
            dragPiece.SetPiecePreviousId(myId);
            piece.transform.SetParent(dragPiece.GetMouseImg().transform);
            piece.transform.localPosition = Vector2.Lerp(piece.transform.localPosition, Vector2.zero, 0.1f);
            GameManager.Instance.InvokeInputManager(this, true);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        PieceClass piece = eventData.pointerDrag.transform.GetComponent<PieceClass>();
        if (piece != null)
        {
            Slot slot = GameManager.Instance.GetClosestSlotToPiece(piece);
            if (slot != null && slot.GetId() == dragPiece.GetPiecePreviousId())
            {
                piece.transform.SetParent(slot.transform);
                piece.pieceInSlot = true;
                GameManager.Instance.InvokeInputManager(this, false);
                piece.transform.localRotation = Quaternion.identity;
                piece.transform.localPosition = Vector2.zero;
                dragPiece.GetMouseImg().transform.localPosition = Vector3.zero;
                dragPiece.GetMouseImg().transform.SetAsLastSibling();
                dragPiece.SetPieceClass(null);
            }
            else
            {
                piece.transform.SetParent(GameManager.Instance.GetFatherOfPiecesTransform());
                GameManager.Instance.InvokeInputManager(this, false);
                dragPiece.GetMouseImg().transform.localPosition = Vector3.zero;
                dragPiece.GetMouseImg().transform.SetAsLastSibling();
                dragPiece.SetPieceClass(null);
            }
        }
    }

    private void InputManager_OnPauseCalled(object sender, EventArgs e)
    {
        if (dragPiece.GetPieceClass() != null)
        {
            dragPiece.GetPieceClass().transform.SetParent(GameManager.Instance.GetFatherOfPiecesTransform());
            GameManager.Instance.InvokeInputManager(this, false);
            dragPiece.GetMouseImg().transform.localPosition = Vector3.zero;
            dragPiece.GetMouseImg().transform.SetAsLastSibling();
        }
    }

    public UiDragPiece GetPiece()
    {
        return dragPiece;
    }

    public bool GetPieceInSlot()
    {
        return pieceInSlot;
    }
}
